using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Version;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class MinecraftGameService
    {
        private readonly InstanceService
            _instanceService;


        private readonly HttpClient
            _httpClient =
                new HttpClient();


        public MinecraftGameService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;
        }


        // =====================================================
        // PREPARAR MINECRAFT + JAVA + LOADER
        // =====================================================

        public async Task<string> PrepareAsync(
            InstalledInstance instance,
            LauncherPreferences preferences,
            IProgress<double>? progress = null,
            IProgress<string>? status = null)
        {
            string instanceDirectory =
                _instanceService
                    .GetInstanceDirectory(
                        instance.Id);


            MinecraftPath minecraftPath =
                new MinecraftPath(
                    instanceDirectory);


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
                status?.Report(
                    "Descargando Minecraft...");


                await launcher.InstallAsync(
                    instance.MinecraftVersion,
                    CreateFileProgress(
                        status,
                        "Minecraft"),
                    CreateByteProgress(
                        progress,
                        0,
                        100));


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
                        status);
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
            IProgress<string>? status)
        {
            status?.Report(
                $"Preparando Minecraft {instance.MinecraftVersion}...");


            IVersion vanillaVersion =
                await launcher.GetVersionAsync(
                    instance.MinecraftVersion);


            // Esto descarga Minecraft, assets, librerías y el Java
            // oficial que Mojang requiere para esta versión.
            await launcher.InstallAsync(
                vanillaVersion,
                CreateFileProgress(
                    status,
                    "Minecraft"),
                CreateByteProgress(
                    progress,
                    0,
                    45));


            string javaPath =
                ResolveJavaPath(
                    launcher,
                    vanillaVersion,
                    preferences);


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
                    forgeInstaller.VersionName);


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
                                45,
                                82),

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


                // Usamos directamente el instalador interno para no abrir
                // la página publicitaria que ForgeInstaller abre al final.
                await forgeInstaller.Install(
                    launcher.MinecraftPath,
                    launcher.GameInstaller,
                    options);


                await launcher.GetAllVersionsAsync();
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
                    82,
                    100));


            progress?.Report(
                100);


            return
                forgeInstaller.VersionName;
        }


        // =====================================================
        // INICIAR JUEGO
        // =====================================================

        public async Task<Process> LaunchAsync(
            InstalledInstance instance,
            MSession session,
            LauncherPreferences preferences)
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


            MinecraftLauncher launcher =
                new MinecraftLauncher(
                    new MinecraftPath(
                        instanceDirectory));


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
                        "0.1.0"
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


            Process process =
                await launcher.BuildProcessAsync(
                    instance.LaunchVersionName,
                    launchOption);


            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Windows no pudo iniciar Minecraft.");
            }


            return process;
        }


        // =====================================================
        // JAVA
        // =====================================================

        private static string ResolveJavaPath(
            MinecraftLauncher launcher,
            IVersion version,
            LauncherPreferences preferences)
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


            if (string.IsNullOrWhiteSpace(javaPath) ||
                !File.Exists(javaPath))
            {
                throw new InvalidOperationException(
                    "No se pudo encontrar el Java automático de Minecraft.");
            }


            return javaPath;
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
                         * CmlLib puede emitir temporalmente:
                         *
                         * TotalBytes = 0
                         * ProgressedBytes = 0
                         *
                         * ByteProgress.ToRatio() hace:
                         *
                         * 0 / 0 = NaN
                         *
                         * y WPF ProgressBar NO acepta NaN.
                         *
                         * Por eso ignoramos cualquier evento cuyo
                         * total todavía no sea válido.
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
            string versionName)
        {
            try
            {
                await launcher.GetVersionAsync(
                    versionName);

                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }
    }
}
