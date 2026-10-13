using System.Diagnostics;
using Negative_Client.Models;
using Negative_Client.Services;

internal static class LinuxMinecraftRuntimeSmoke
{
    private const string MinecraftVersion = "1.20.1";
    private const string Username = "NegativeLauncherRuntimeSmoke";

    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("El smoke del runtime de Minecraft solo está habilitado en Linux.");

        string? configuredRoot = Environment.GetEnvironmentVariable("NEGATIVE_LAUNCHER_RUNTIME_SMOKE_ROOT");
        if (string.IsNullOrWhiteSpace(configuredRoot) || !Path.IsPathFullyQualified(configuredRoot))
        {
            throw new InvalidOperationException(
                "NEGATIVE_LAUNCHER_RUNTIME_SMOKE_ROOT debe señalar una ruta absoluta aislada para el smoke.");
        }

        string storageRoot = Path.GetFullPath(configuredRoot);
        string javaPath = JavaExecutableHelper.FindOnPath(
            Environment.GetEnvironmentVariable("PATH"),
            isWindows: false)
            ?? throw new InvalidOperationException("La CI no encontró Java 17 en PATH para el smoke del runtime.");

        var instances = new InstanceService();
        instances.ConfigureStorageRoot(storageRoot);

        var instance = new InstalledInstance
        {
            Id = "runtime-smoke-" + Guid.NewGuid().ToString("N"),
            Name = "Prueba de runtime Linux",
            MinecraftVersion = MinecraftVersion,
            Loader = "vanilla",
            IsInstalled = true
        };
        await instances.SaveAsync(instance);

        string instanceDirectory = instances.GetInstanceDirectory(instance.Id);
        var preferences = new LauncherPreferences
        {
            MaximumRamMb = 2048,
            UseAutomaticJava = false,
            CustomJavaPath = javaPath,
            ShowGameConsole = false
        };
        var runtime = new MinecraftRuntimeService(instances);
        Process? gameProcess = null;

        try
        {
            using var preparationTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
            Console.WriteLine($"Preparando Minecraft {MinecraftVersion} con Java 17 para el smoke de Linux…");
            string preparedVersion = await runtime.PrepareAsync(
                instance,
                preferences,
                status: new Progress<string>(message => Console.WriteLine($"Runtime: {message}")),
                cancellationToken: preparationTimeout.Token);

            if (!string.Equals(preparedVersion, MinecraftVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Se esperaba preparar Minecraft {MinecraftVersion}, pero el runtime devolvió {preparedVersion}.");
            }

            gameProcess = await runtime.LaunchOfflineAsync(
                instance,
                preferences,
                new OfflineAccountProfile { Username = Username });

            string latestLogPath = Path.Combine(instanceDirectory, "logs", "latest.log");
            DateTime startupDeadline = DateTime.UtcNow.AddMinutes(4);
            bool clientInitialized = false;

            while (DateTime.UtcNow < startupDeadline)
            {
                if (gameProcess.HasExited)
                {
                    string log = await ReadLogTailAsync(latestLogPath);
                    throw new InvalidOperationException(
                        $"Minecraft terminó antes de inicializarse (código {gameProcess.ExitCode}).\n{log}");
                }

                if (File.Exists(latestLogPath))
                {
                    string log = await File.ReadAllTextAsync(latestLogPath);
                    if (log.Contains($"Setting user: {Username}", StringComparison.Ordinal))
                    {
                        clientInitialized = true;
                        break;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            if (!clientInitialized)
            {
                throw new InvalidOperationException(
                    "Minecraft no registró el perfil sin conexión dentro del tiempo esperado.\n" +
                    await ReadLogTailAsync(latestLogPath));
            }

            // Mantener el proceso abierto otro intervalo comprueba que la inicialización gráfica
            // no se cierre inmediatamente tras aceptar el perfil.
            await Task.Delay(TimeSpan.FromSeconds(20));
            if (gameProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"Minecraft se cerró tras inicializar el perfil (código {gameProcess.ExitCode}).\n" +
                    await ReadLogTailAsync(latestLogPath));
            }

            Console.WriteLine(
                $"NEGATIVE_LAUNCHER_MINECRAFT_RUNTIME_SMOKE_OK: Minecraft {MinecraftVersion} inició con perfil sin conexión.");
        }
        finally
        {
            if (gameProcess is not null)
            {
                try
                {
                    if (!gameProcess.HasExited)
                        gameProcess.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // El juego puede cerrar entre la comprobación y la solicitud de terminación.
                }

                try
                {
                    await gameProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                }
                catch (TimeoutException)
                {
                    Console.Error.WriteLine("Minecraft no terminó tras solicitar la limpieza del smoke.");
                }
                finally
                {
                    gameProcess.Dispose();
                }
            }

            try
            {
                if (Directory.Exists(instanceDirectory))
                    Directory.Delete(instanceDirectory, recursive: true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"No se pudo retirar la instancia temporal del smoke: {exception.Message}");
            }
        }
    }

    private static async Task<string> ReadLogTailAsync(string path)
    {
        if (!File.Exists(path))
            return $"No se encontró el log de Minecraft: {path}";

        try
        {
            string contents = await File.ReadAllTextAsync(path);
            return contents.Length <= 5000 ? contents : contents[^5000..];
        }
        catch (IOException exception)
        {
            return $"No se pudo leer el log de Minecraft: {exception.Message}";
        }
    }
}
