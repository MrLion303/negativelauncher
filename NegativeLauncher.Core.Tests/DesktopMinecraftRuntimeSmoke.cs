using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Negative_Client.Models;
using Negative_Client.Services;

internal static class DesktopMinecraftRuntimeSmoke
{
    private const string MinecraftVersion = "1.20.1";
    private const string ForgeVersion = "47.4.0";
    private const string VanillaUsername = "NegativeLauncherRuntimeSmoke";
    private const string ForgeUsername = "NegativeLauncherForgeSmoke";

    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            throw new InvalidOperationException("El smoke del runtime de Minecraft solo está habilitado en Linux y macOS.");

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
        var preferences = new LauncherPreferences
        {
            MaximumRamMb = 2048,
            UseAutomaticJava = false,
            CustomJavaPath = javaPath,
            ShowGameConsole = false
        };
        var runtime = new MinecraftRuntimeService(instances);
        Console.WriteLine($"Arquitectura del smoke de Minecraft Linux/macOS: {RuntimeInformation.ProcessArchitecture}.");

        var vanillaInstance = new InstalledInstance
        {
            Id = "runtime-smoke-vanilla-" + Guid.NewGuid().ToString("N"),
            Name = "Prueba Vanilla del runtime Linux/macOS",
            MinecraftVersion = MinecraftVersion,
            Loader = "vanilla",
            IsInstalled = true
        };
        string vanillaDirectory = instances.GetInstanceDirectory(vanillaInstance.Id);

        try
        {
            await instances.SaveAsync(vanillaInstance);
            InstalledInstance preparedVanilla = await PrepareAndReloadAsync(
                runtime,
                instances,
                vanillaInstance,
                preferences,
                expectedLaunchVersion: MinecraftVersion);
            await RunOfflineClientSmokeAsync(
                runtime,
                preparedVanilla,
                preferences,
                VanillaUsername,
                "Vanilla");
            Console.WriteLine("NEGATIVE_LAUNCHER_VANILLA_RUNTIME_SMOKE_OK");
        }
        finally
        {
            TryDeleteDirectory(vanillaDirectory);
        }

        var forgeManifest = new ModpackManifest
        {
            Id = "runtime-smoke-forge",
            Name = "Prueba Forge del runtime Linux/macOS",
            Version = "1.0.0",
            MinecraftVersion = MinecraftVersion,
            Loader = "Forge",
            LoaderVersion = ForgeVersion,
            ArchiveFileId = "runtime-smoke-forge-archive"
        };
        CreateCachedForgeArchive(forgeManifest);

        var modpackInstaller = new ModpackInstallerService(new GoogleDriveService(), instances);
        InstalledInstance forgeInstance = await modpackInstaller.InstallOrUpdateAsync(
            forgeManifest,
            "DESKTOP-SMOKE",
            currentInstance: null);

        string forgeDirectory = instances.GetInstanceDirectory(forgeManifest.Id);
        try
        {
            string fixtureModpackFile = Path.Combine(forgeDirectory, "mods", "runtime-smoke.txt");
            if (!forgeInstance.IsInstalled ||
                !File.Exists(fixtureModpackFile) ||
                await File.ReadAllTextAsync(fixtureModpackFile) != "paquete de prueba")
            {
                throw new InvalidOperationException(
                    "El instalador no aplicó el paquete ZIP de prueba en la instancia Forge.");
            }

            InstalledInstance preparedForge = await PrepareAndReloadAsync(
                runtime,
                instances,
                forgeInstance,
                preferences,
                expectedLaunchVersion: null);
            if (!string.Equals(preparedForge.Loader, "Forge", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(preparedForge.LoaderVersion, ForgeVersion, StringComparison.OrdinalIgnoreCase) ||
                preparedForge.LaunchVersionName.Length == 0)
            {
                throw new InvalidOperationException(
                    "La instancia recargada no conservó el loader Forge preparado.");
            }

            await RunOfflineClientSmokeAsync(
                runtime,
                preparedForge,
                preferences,
                ForgeUsername,
                "Forge");
            Console.WriteLine(
                $"NEGATIVE_LAUNCHER_FORGE_RUNTIME_SMOKE_OK: Forge {ForgeVersion} para Minecraft {MinecraftVersion}.");
        }
        finally
        {
            TryDeleteDirectory(forgeDirectory);
        }

        // La instalación NeoForge de CmlLib comienza en Minecraft 1.20.2.
        await RunLoaderSmokeAsync(runtime, instances, preferences, "NeoForge", "NegativeLauncherNeoForgeSmoke", "1.20.2");
        await RunLoaderSmokeAsync(runtime, instances, preferences, "Fabric", "NegativeLauncherFabricSmoke");
        await RunLoaderSmokeAsync(runtime, instances, preferences, "Quilt", "NegativeLauncherQuiltSmoke");

        Console.WriteLine(
            $"NEGATIVE_LAUNCHER_MINECRAFT_RUNTIME_SMOKE_OK: Vanilla, Forge {ForgeVersion}, NeoForge, Fabric y Quilt iniciaron con Java 17 en Linux/macOS {RuntimeInformation.ProcessArchitecture}.");
    }

    private static async Task<InstalledInstance> PrepareAndReloadAsync(
        MinecraftRuntimeService runtime,
        InstanceService instances,
        InstalledInstance instance,
        LauncherPreferences preferences,
        string? expectedLaunchVersion)
    {
        using var preparationTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        Console.WriteLine(
            $"Preparando Minecraft {instance.MinecraftVersion} ({instance.Loader}) para el smoke de Linux/macOS…");
        var printedMilestones = new HashSet<string>(StringComparer.Ordinal);
        string preparedVersion = await runtime.PrepareAsync(
            instance,
            preferences,
            status: new Progress<string>(message =>
            {
                bool isMilestone =
                    message.StartsWith("Preparando Minecraft ", StringComparison.Ordinal) ||
                    message.StartsWith("Buscando Forge ", StringComparison.Ordinal) ||
                    message.StartsWith("Instalando Forge ", StringComparison.Ordinal) ||
                    message.StartsWith("Instalando NeoForge ", StringComparison.Ordinal) ||
                    message.StartsWith("Instalando Fabric ", StringComparison.Ordinal) ||
                    message.StartsWith("Instalando Quilt ", StringComparison.Ordinal) ||
                    string.Equals(message, "Minecraft está preparado.", StringComparison.Ordinal);
                if (isMilestone && printedMilestones.Add(message))
                    Console.WriteLine($"Runtime: {message}");
            }),
            cancellationToken: preparationTimeout.Token);

        if (string.IsNullOrWhiteSpace(preparedVersion) ||
            (expectedLaunchVersion is not null &&
             !string.Equals(preparedVersion, expectedLaunchVersion, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"El runtime devolvió una versión preparada inesperada: {preparedVersion}.");
        }

        InstalledInstance? persistedInstance = (await instances.LoadAllAsync())
            .SingleOrDefault(candidate => string.Equals(
                candidate.Id,
                instance.Id,
                StringComparison.Ordinal));

        if (persistedInstance is null ||
            !persistedInstance.IsInstalled ||
            !persistedInstance.RuntimePrepared ||
            !string.Equals(
                persistedInstance.MinecraftVersion,
                instance.MinecraftVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                persistedInstance.LaunchVersionName,
                preparedVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La instancia no conservó el estado preparado de Minecraft al recargarse desde almacenamiento.");
        }

        return persistedInstance;
    }

    private static async Task RunLoaderSmokeAsync(
        MinecraftRuntimeService runtime,
        InstanceService instances,
        LauncherPreferences preferences,
        string loader,
        string username,
        string minecraftVersion = MinecraftVersion)
    {
        var instance = new InstalledInstance
        {
            Id = "runtime-smoke-" + loader.ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N"),
            Name = $"Prueba {loader} del runtime Linux/macOS",
            MinecraftVersion = minecraftVersion,
            Loader = loader,
            LoaderVersion = string.Empty,
            IsInstalled = true
        };
        string instanceDirectory = instances.GetInstanceDirectory(instance.Id);

        try
        {
            await instances.SaveAsync(instance);
            InstalledInstance prepared = await PrepareAndReloadAsync(
                runtime,
                instances,
                instance,
                preferences,
                expectedLaunchVersion: null);
            if (!string.Equals(prepared.Loader, loader, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(prepared.LaunchVersionName) ||
                string.Equals(prepared.LaunchVersionName, MinecraftVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"La instancia recargada no conservó el loader {loader} preparado.");
            }

            await RunOfflineClientSmokeAsync(
                runtime,
                prepared,
                preferences,
                username,
                loader);
            Console.WriteLine(
                $"NEGATIVE_LAUNCHER_{loader.ToUpperInvariant()}_RUNTIME_SMOKE_OK: {prepared.LaunchVersionName}.");
        }
        finally
        {
            TryDeleteDirectory(instanceDirectory);
        }
    }

    private static async Task RunOfflineClientSmokeAsync(
        MinecraftRuntimeService runtime,
        InstalledInstance instance,
        LauncherPreferences preferences,
        string username,
        string loaderLabel)
    {
        string instanceDirectory = Path.Combine(
            InstanceService.InstancesRoot,
            instance.Id);
        string latestLogPath = Path.Combine(instanceDirectory, "logs", "latest.log");
        Process? gameProcess = null;

        try
        {
            gameProcess = await runtime.LaunchOfflineAsync(
                instance,
                preferences,
                new OfflineAccountProfile { Username = username });

            DateTime startupDeadline = DateTime.UtcNow.AddMinutes(4);
            bool clientInitialized = false;
            bool graphicsInitialized = false;

            while (DateTime.UtcNow < startupDeadline)
            {
                if (gameProcess.HasExited)
                {
                    throw new InvalidOperationException(
                        $"Minecraft ({loaderLabel}) terminó antes de inicializarse (código {gameProcess.ExitCode}).\n" +
                        await ReadLogTailAsync(latestLogPath));
                }

                if (File.Exists(latestLogPath))
                {
                    string log = await File.ReadAllTextAsync(latestLogPath);
                    clientInitialized = log.Contains($"Setting user: {username}", StringComparison.Ordinal);
                    graphicsInitialized = log.Contains(
                        "Backend library: LWJGL version",
                        StringComparison.Ordinal);
                    if (clientInitialized && graphicsInitialized)
                        break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            if (!clientInitialized || !graphicsInitialized)
            {
                throw new InvalidOperationException(
                    $"Minecraft ({loaderLabel}) no confirmó el perfil sin conexión y la inicialización de LWJGL.\n" +
                    await ReadLogTailAsync(latestLogPath));
            }

            await Task.Delay(TimeSpan.FromSeconds(20));
            if (gameProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"Minecraft ({loaderLabel}) se cerró tras inicializar (código {gameProcess.ExitCode}).\n" +
                    await ReadLogTailAsync(latestLogPath));
            }
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
                    Console.Error.WriteLine($"Minecraft ({loaderLabel}) no terminó tras solicitar la limpieza del smoke.");
                }
                finally
                {
                    gameProcess.Dispose();
                }
            }

            CopyRuntimeDiagnostic(latestLogPath);
        }
    }

    private static void CreateCachedForgeArchive(ModpackManifest manifest)
    {
        Directory.CreateDirectory(InstanceService.PackageCacheRoot);
        string archivePath = Path.Combine(
            InstanceService.PackageCacheRoot,
            $"{manifest.Id}-{manifest.Version}-{manifest.ArchiveFileId}.zip");

        if (File.Exists(archivePath))
        {
            try
            {
                ModpackArchiveSafety.ValidateZip(archivePath);
                return;
            }
            catch (InvalidOperationException)
            {
                File.Delete(archivePath);
            }
        }

        using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry("mods/runtime-smoke.txt").Open());
        writer.Write("paquete de prueba");
    }

    private static void CopyRuntimeDiagnostic(string latestLogPath)
    {
        string? diagnosticPath = Environment.GetEnvironmentVariable("NEGATIVE_LAUNCHER_RUNTIME_SMOKE_LOG");
        if (string.IsNullOrWhiteSpace(diagnosticPath) || !File.Exists(latestLogPath))
            return;

        try
        {
            string fullDiagnosticPath = Path.GetFullPath(diagnosticPath);
            string diagnosticDirectory = Path.GetDirectoryName(fullDiagnosticPath)
                ?? throw new InvalidOperationException("La ruta de diagnóstico no contiene una carpeta.");
            Directory.CreateDirectory(diagnosticDirectory);
            File.Copy(latestLogPath, fullDiagnosticPath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"No se pudo guardar el log temporal de Minecraft: {exception.Message}");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"No se pudo retirar la instancia temporal del smoke: {exception.Message}");
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
