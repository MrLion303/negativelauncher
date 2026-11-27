using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Rules;
using CmlLib.Core.Version;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class MinecraftGameService
    {
        private static readonly SemaphoreSlim VersionManifestLock =
            new SemaphoreSlim(1, 1);


        private readonly InstanceService
            _instanceService;


        private readonly SharedMinecraftStorageService
            _sharedMinecraftStorageService;


        private readonly HttpClient
            _httpClient =
                new HttpClient();


        public MinecraftGameService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;


            _sharedMinecraftStorageService =
                new SharedMinecraftStorageService(
                    instanceService);
        }




        // =====================================================
        // VERSIONES VANILLA PARA MODO DESARROLLADOR
        // =====================================================

        public async Task<(
            IReadOnlyList<MinecraftVersionOption> Versions,
            string LatestRelease)>
            GetAvailableVanillaVersionsAsync(
                string instanceId,
                CancellationToken cancellationToken = default)
        {
            MinecraftLauncher launcher =
                new MinecraftLauncher(
                    _sharedMinecraftStorageService
                        .CreateMinecraftPath(
                            instanceId));


            await VersionManifestLock.WaitAsync(
                cancellationToken);

            try
            {
                var versions =
                    await launcher
                        .GetAllVersionsAsync(
                            cancellationToken);


                List<MinecraftVersionOption> options =
                    versions
                        .Where(
                            version =>
                                !string.IsNullOrWhiteSpace(
                                    version.Name))
                        .Select(
                            version =>
                                new MinecraftVersionOption
                                {
                                    Name =
                                        version.Name,

                                    Type =
                                        version.Type ??
                                        string.Empty
                                })
                        .GroupBy(
                            option =>
                                option.Name,
                            StringComparer.OrdinalIgnoreCase)
                        .Select(
                            group =>
                                group.First())
                        .ToList();


                string latestRelease =
                    options
                        .FirstOrDefault(
                            option =>
                                string.Equals(
                                    option.Type,
                                    "release",
                                    StringComparison.OrdinalIgnoreCase))?
                        .Name ??
                    string.Empty;


                return (
                    options,
                    latestRelease);
            }
            finally
            {
                VersionManifestLock.Release();
            }
        }


        // =====================================================
        // PREPARAR MINECRAFT + JAVA + LOADER
        // =====================================================

        public async Task<string> PrepareAsync(
            InstalledInstance instance,
            LauncherPreferences preferences,
            IProgress<double>? progress = null,
            IProgress<string>? status = null,
            CancellationToken cancellationToken = default)
        {
            MinecraftPath minecraftPath =
                _sharedMinecraftStorageService
                    .CreateMinecraftPath(
                        instance.Id);


            MinecraftLauncher launcher =
                new MinecraftLauncher(
                    minecraftPath);


            string loader =
                instance.Loader
                    .Trim()
                    .ToLowerInvariant();


            if (string.IsNullOrWhiteSpace(loader) ||
                loader == "vanilla")
            {
                IVersion vanillaVersion =
                    await launcher.GetVersionAsync(
                        instance.MinecraftVersion,
                        cancellationToken);


                bool javaMissing =
                    preferences.UseAutomaticJava &&
                    !AutomaticJavaExists(
                        launcher,
                        vanillaVersion);


                status?.Report(
                    javaMissing
                        ? "Descargando Minecraft y Java..."
                        : "Descargando Minecraft...");


                /*
                 * Minecraft, assets, libraries, versions y Java se guardan
                 * bajo <StorageRoot>\minecraft. Si otra instancia ya usa
                 * exactamente estos archivos, CmlLib los reutiliza.
                 */
                await launcher.InstallAsync(
                    vanillaVersion,
                    CreateFileProgress(
                        status,
                        "Minecraft"),
                    CreateByteProgress(
                        progress,
                        0,
                        92),
                    cancellationToken);


                await EnsureJavaAvailableAsync(
                    launcher,
                    vanillaVersion,
                    preferences,
                    progress,
                    status,
                    92,
                    100,
                    cancellationToken);


                progress?.Report(
                    100);


                return
                    instance.MinecraftVersion;
            }


            if (loader == "forge")
            {
                return
                    await PrepareForgeAsync(
                        launcher,
                        instance,
                        preferences,
                        progress,
                        status,
                        cancellationToken);
            }


            throw new NotSupportedException(
                $"El loader '{instance.Loader}' todavía no está soportado.");
        }


        // =====================================================
        // FORGE
        // =====================================================

        private async Task<string> PrepareForgeAsync(
            MinecraftLauncher launcher,
            InstalledInstance instance,
            LauncherPreferences preferences,
            IProgress<double>? progress,
            IProgress<string>? status,
            CancellationToken cancellationToken)
        {
            IVersion vanillaVersion =
                await launcher.GetVersionAsync(
                    instance.MinecraftVersion,
                    cancellationToken);


            bool javaMissing =
                preferences.UseAutomaticJava &&
                !AutomaticJavaExists(
                    launcher,
                    vanillaVersion);


            status?.Report(
                javaMissing
                    ? $"Descargando Minecraft {instance.MinecraftVersion} y Java..."
                    : $"Descargando Minecraft {instance.MinecraftVersion}...");


            /*
             * Este paso instala en el almacenamiento compartido:
             * - client.jar
             * - assets
             * - librerías
             * - natives
             * - Java oficial requerido por esta versión
             */
            await launcher.InstallAsync(
                vanillaVersion,
                CreateFileProgress(
                    status,
                    "Minecraft"),
                CreateByteProgress(
                    progress,
                    0,
                    42),
                cancellationToken);


            string javaPath =
                await EnsureJavaAvailableAsync(
                    launcher,
                    vanillaVersion,
                    preferences,
                    progress,
                    status,
                    42,
                    50,
                    cancellationToken);


            status?.Report(
                $"Preparando Forge {instance.LoaderVersion}...");


            ForgeVersionLoader forgeVersionLoader =
                new ForgeVersionLoader(
                    _httpClient);


            var forgeVersions =
                await forgeVersionLoader
                    .GetForgeVersions(
                        instance.MinecraftVersion);


            ForgeVersion? forgeVersion =
                forgeVersions
                    .FirstOrDefault(
                        version =>
                            string.Equals(
                                version.ForgeVersionName,
                                instance.LoaderVersion,
                                StringComparison.OrdinalIgnoreCase));


            if (forgeVersion == null)
            {
                throw new InvalidOperationException(
                    $"No se encontró Forge {instance.LoaderVersion} " +
                    $"para Minecraft {instance.MinecraftVersion}.");
            }


            ForgeInstallerVersionMapper mapper =
                new ForgeInstallerVersionMapper();


            IForgeInstaller forgeInstaller =
                mapper.CreateInstaller(
                    forgeVersion);


            bool forgeAlreadyInstalled =
                await IsVersionInstalledAsync(
                    launcher,
                    forgeInstaller.VersionName,
                    cancellationToken);


            if (!forgeAlreadyInstalled)
            {
                ForgeInstallOptions options =
                    new ForgeInstallOptions
                    {
                        JavaPath =
                            javaPath,

                        FileProgress =
                            CreateFileProgress(
                                status,
                                "Forge"),

                        ByteProgress =
                            CreateByteProgress(
                                progress,
                                50,
                                84),

                        CancellationToken =
                            cancellationToken,

                        InstallerOutput =
                            new Progress<string>(
                                line =>
                                {
                                    if (!string.IsNullOrWhiteSpace(line))
                                    {
                                        status?.Report(
                                            "Instalando Forge...");
                                    }
                                })
                    };


                // Usamos el instalador público de bajo nivel para evitar
                // abrir una página publicitaria al terminar.
                await forgeInstaller.Install(
                    launcher.MinecraftPath,
                    launcher.GameInstaller,
                    options);


                await VersionManifestLock.WaitAsync(
                    cancellationToken);

                try
                {
                    await launcher.GetAllVersionsAsync(
                        cancellationToken);
                }
                finally
                {
                    VersionManifestLock.Release();
                }
            }


            status?.Report(
                "Completando archivos de Forge...");


            await launcher.InstallAsync(
                forgeInstaller.VersionName,
                CreateFileProgress(
                    status,
                    "Forge"),
                CreateByteProgress(
                    progress,
                    84,
                    100),
                cancellationToken);


            progress?.Report(
                100);


            return
                forgeInstaller.VersionName;
        }


        // =====================================================
        // JAVA AUTOMÁTICO
        // =====================================================

        private static bool AutomaticJavaExists(
            MinecraftLauncher launcher,
            IVersion version)
        {
            string? javaPath =
                launcher.GetJavaPath(
                    version);


            if (!string.IsNullOrWhiteSpace(javaPath) &&
                File.Exists(javaPath))
            {
                return true;
            }


            javaPath =
                launcher.GetDefaultJavaPath();


            return
                !string.IsNullOrWhiteSpace(javaPath) &&
                File.Exists(javaPath);
        }


        private static async Task<string>
            EnsureJavaAvailableAsync(
                MinecraftLauncher launcher,
                IVersion version,
                LauncherPreferences preferences,
                IProgress<double>? progress,
                IProgress<string>? status,
                double start,
                double end,
                CancellationToken cancellationToken)
        {
            if (!preferences.UseAutomaticJava)
            {
                if (string.IsNullOrWhiteSpace(
                        preferences.CustomJavaPath) ||
                    !File.Exists(
                        preferences.CustomJavaPath))
                {
                    throw new FileNotFoundException(
                        "La ruta de Java configurada no existe.");
                }


                return
                    preferences.CustomJavaPath;
            }


            string? javaPath =
                launcher.GetJavaPath(
                    version);


            if (string.IsNullOrWhiteSpace(javaPath) ||
                !File.Exists(javaPath))
            {
                javaPath =
                    launcher.GetDefaultJavaPath();
            }


            if (!string.IsNullOrWhiteSpace(javaPath) &&
                File.Exists(javaPath))
            {
                return javaPath;
            }


            /*
             * Si por alguna razón el primer InstallAsync no dejó
             * instalado Java, repetimos la verificación de archivos.
             * CmlLib descargará únicamente lo que falte.
             */
            status?.Report(
                "Java requerido no encontrado. Descargando Java oficial...");


            await launcher.InstallAsync(
                version,
                CreateFileProgress(
                    status,
                    "Java"),
                CreateByteProgress(
                    progress,
                    start,
                    end),
                cancellationToken);


            javaPath =
                launcher.GetJavaPath(
                    version);


            if (string.IsNullOrWhiteSpace(javaPath) ||
                !File.Exists(javaPath))
            {
                javaPath =
                    launcher.GetDefaultJavaPath();
            }


            if (string.IsNullOrWhiteSpace(javaPath) ||
                !File.Exists(javaPath))
            {
                throw new InvalidOperationException(
                    "No se pudo descargar o localizar el Java " +
                    "requerido por esta versión de Minecraft.");
            }


            return javaPath;
        }


        // =====================================================
        // INICIAR JUEGO
        // =====================================================

        public async Task<Process> LaunchAsync(
            InstalledInstance instance,
            MSession session,
            LauncherPreferences preferences,
            IProgress<string>? status = null)
        {
            if (!instance.RuntimePrepared ||
                string.IsNullOrWhiteSpace(
                    instance.LaunchVersionName))
            {
                throw new InvalidOperationException(
                    "La instalación todavía no está preparada para jugar.");
            }


            string instanceDirectory =
                _instanceService
                    .GetInstanceDirectory(
                        instance.Id);


            /*
             * DEFENSA EN PROFUNDIDAD:
             *
             * La interfaz ya prepara los texture packs antes del click de
             * JUGAR, pero el lanzamiento no debe depender de un evento visual.
             * Cualquier ruta futura que llame LaunchAsync pasa de nuevo por
             * esta validación.
             */
            ResourcePackSelectionService resourcePackSelectionService =
                new ResourcePackSelectionService(
                    _instanceService);


            ResourcePackSelectionResult resourcePackResult =
                resourcePackSelectionService
                    .ApplyBundledSelectionIfNeeded(
                        instance);


            if (resourcePackResult.MissingResourcePacks.Count >
                0)
            {
                throw new InvalidOperationException(
                    "Faltan texture packs requeridos por esta instancia: " +
                    string.Join(
                        ", ",
                        resourcePackResult.MissingResourcePacks) +
                    ". Usa VERIFICAR INTEGRIDAD antes de volver a jugar.");
            }


            MinecraftLauncher launcher =
                new MinecraftLauncher(
                    _sharedMinecraftStorageService
                        .CreateMinecraftPath(
                            instance.Id));


            MLaunchOption launchOption =
                new MLaunchOption
                {
                    Session =
                        session,

                    MaximumRamMb =
                        preferences.MaximumRamMb,

                    GameLauncherName =
                        "Negative Client",

                    GameLauncherVersion =
                        "0.1.0",

                    /*
                     * Negative Client NO fuerza pantalla completa.
                     * Minecraft leerá fullscreen y el resto de opciones
                     * desde options.txt de la propia instancia.
                     */
                    FullScreen =
                        false,

                    ScreenWidth =
                        0,

                    ScreenHeight =
                        0
                };


            if (!preferences.UseAutomaticJava)
            {
                if (string.IsNullOrWhiteSpace(
                        preferences.CustomJavaPath) ||
                    !File.Exists(
                        preferences.CustomJavaPath))
                {
                    throw new FileNotFoundException(
                        "La ruta de Java configurada no existe. " +
                        "Ve a Configuración → Minecraft.");
                }


                launchOption.JavaPath =
                    preferences.CustomJavaPath;
            }


            // Argumentos JVM adicionales configurados por el usuario.
            List<MArgument> extraJvmArguments =
                new List<MArgument>();

            if (preferences.EnableCustomJavaArguments &&
                !string.IsNullOrWhiteSpace(
                    preferences.CustomJavaArguments))
            {
                extraJvmArguments.Add(
                    MArgument.FromCommandLine(
                        preferences.CustomJavaArguments));
            }


            // Minecraft2Shell: proteger clientes que no usan FreeType 2.15.0.
            Minecraft2ShellProtectionService protectionService =
                new Minecraft2ShellProtectionService();

            string? protectionAgentPath =
                await protectionService.EnsureAgentIfRequiredAsync(
                    instance,
                    status);

            if (!string.IsNullOrWhiteSpace(protectionAgentPath))
            {
                extraJvmArguments.Add(
                    MArgument.FromCommandLine(
                        $"-javaagent:\"{protectionAgentPath}\""));

                extraJvmArguments.Add(
                    MArgument.FromCommandLine(
                        "-Dm2sfix.policy=safe"));

                extraJvmArguments.Add(
                    MArgument.FromCommandLine(
                        "-Dm2sfix.images=safe"));
            }

            if (extraJvmArguments.Count > 0)
            {
                launchOption.ExtraJvmArguments =
                    extraJvmArguments.ToArray();
            }


            /*
             * El gameDir sigue siendo exclusivo de la instancia:
             * mods, config, resourcepacks, shaderpacks, options.txt,
             * mundos y scripts NO se comparten.
             *
             * Solo assets/libraries/versions/runtime están en la
             * carpeta compartida de Minecraft.
             *
             * Si Windows bloquea temporalmente javaw.exe (por ejemplo,
             * mientras el antivirus termina de analizar un runtime recién
             * descargado), reintentamos y, si persiste, verificamos/reparamos
             * la instalación antes de mostrar un error al usuario.
             */
            return await BuildAndStartWithRecoveryAsync(
                launcher,
                instance.LaunchVersionName,
                launchOption,
                instanceDirectory,
                preferences.ShowGameConsole,
                status);
        }


        private static bool IsTransientWindowsStartError(
            Win32Exception exception)
        {
            // 5 = ACCESS_DENIED, 32 = SHARING_VIOLATION,
            // 33 = LOCK_VIOLATION.
            return exception.NativeErrorCode is 5 or 32 or 33;
        }


        private static async Task<Process> BuildAndStartOnceAsync(
            MinecraftLauncher launcher,
            string versionName,
            MLaunchOption launchOption,
            string instanceDirectory,
            bool showGameConsole)
        {
            if (!Directory.Exists(instanceDirectory))
            {
                Directory.CreateDirectory(instanceDirectory);
            }

            Process process =
                await launcher.BuildProcessAsync(
                    versionName,
                    launchOption);

            try
            {
                process.StartInfo.WorkingDirectory =
                    instanceDirectory;

                if (showGameConsole)
                {
                    process.StartInfo.UseShellExecute =
                        false;

                    process.StartInfo.RedirectStandardOutput =
                        true;

                    process.StartInfo.RedirectStandardError =
                        true;

                    process.StartInfo.CreateNoWindow =
                        true;
                }

                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Windows no pudo iniciar el proceso de Minecraft.");
                }

                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }


        private static async Task<Process> BuildAndStartWithRecoveryAsync(
            MinecraftLauncher launcher,
            string versionName,
            MLaunchOption launchOption,
            string instanceDirectory,
            bool showGameConsole,
            IProgress<string>? status)
        {
            Win32Exception? lastStartError = null;

            // Dos intentos rápidos para permitir que Windows/antivirus
            // libere el ejecutable o sus archivos.
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    return await BuildAndStartOnceAsync(
                        launcher,
                        versionName,
                        launchOption,
                        instanceDirectory,
                        showGameConsole);
                }
                catch (Win32Exception exception)
                    when (IsTransientWindowsStartError(exception))
                {
                    lastStartError = exception;

                    if (attempt < 2)
                    {
                        status?.Report(
                            "Windows está bloqueando temporalmente Java. Reintentando...");

                        await Task.Delay(900);
                    }
                }
            }

            if (lastStartError == null)
            {
                throw new InvalidOperationException(
                    "No se pudo iniciar el proceso de Minecraft.");
            }

            // La comprobación completa solo se ejecuta cuando el arranque
            // falla por permisos o bloqueo. CmlLib vuelve a revisar los
            // archivos de la versión y descarga los que detecte dañados.
            status?.Report(
                "El inicio sigue bloqueado. Verificando y reparando Minecraft y Java...");

            try
            {
                await launcher.InstallAsync(
                    versionName,
                    CreateFileProgress(
                        status,
                        "Reparando"),
                    CreateByteProgress(
                        null,
                        0,
                        100),
                    CancellationToken.None);
            }
            catch (Exception repairException)
            {
                throw new InvalidOperationException(
                    "Windows impidió ejecutar Java y Negative Client no pudo completar " +
                    "la comprobación automática de los archivos. Cierra otros procesos " +
                    "de Minecraft y revisa si tu antivirus está bloqueando javaw.exe.",
                    new AggregateException(
                        lastStartError,
                        repairException));
            }

            await Task.Delay(700);

            try
            {
                return await BuildAndStartOnceAsync(
                    launcher,
                    versionName,
                    launchOption,
                    instanceDirectory,
                    showGameConsole);
            }
            catch (Win32Exception finalException)
                when (IsTransientWindowsStartError(finalException))
            {
                throw new InvalidOperationException(
                    "Windows sigue impidiendo iniciar Java (Acceso denegado o archivo bloqueado). " +
                    "Negative Client ya reintentó el inicio y verificó/reparó los archivos. " +
                    "Comprueba que tu antivirus no haya bloqueado javaw.exe y vuelve a intentarlo.",
                    new AggregateException(
                        lastStartError,
                        finalException));
            }
        }


        // =====================================================
        // PROGRESO
        // =====================================================

        private static IProgress<InstallerProgressChangedEventArgs>
            CreateFileProgress(
                IProgress<string>? status,
                string prefix)
        {
            return
                new Progress<InstallerProgressChangedEventArgs>(
                    e =>
                    {
                        if (!string.IsNullOrWhiteSpace(
                                e.Name))
                        {
                            status?.Report(
                                $"{prefix}: {e.Name}");
                        }
                    });
        }


        private static IProgress<ByteProgress>
            CreateByteProgress(
                IProgress<double>? target,
                double start,
                double end)
        {
            return
                new Progress<ByteProgress>(
                    e =>
                    {
                        /*
                         * ByteProgress.ToRatio() puede producir NaN
                         * temporalmente cuando TotalBytes == 0.
                         */
                        if (e.TotalBytes <= 0)
                        {
                            return;
                        }


                        double ratio =
                            (double)e.ProgressedBytes /
                            e.TotalBytes;


                        if (double.IsNaN(ratio) ||
                            double.IsInfinity(ratio))
                        {
                            return;
                        }


                        ratio =
                            Math.Clamp(
                                ratio,
                                0,
                                1);


                        double mapped =
                            start +
                            ((end - start) *
                             ratio);


                        if (double.IsNaN(mapped) ||
                            double.IsInfinity(mapped))
                        {
                            return;
                        }


                        target?.Report(
                            Math.Clamp(
                                mapped,
                                0,
                                100));
                    });
        }


        private static async Task<bool> IsVersionInstalledAsync(
            MinecraftLauncher launcher,
            string versionName,
            CancellationToken cancellationToken)
        {
            try
            {
                await launcher.GetVersionAsync(
                    versionName,
                    cancellationToken);

                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }
    }
}
