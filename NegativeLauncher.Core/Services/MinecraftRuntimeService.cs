using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.QuiltMC;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Version;
using Negative_Client.Models;

namespace Negative_Client.Services;

/// <summary>
/// Prepara y ejecuta Minecraft sin depender de WPF.
/// La autenticación Microsoft queda separada; este servicio admite perfiles sin conexión.
/// </summary>
public sealed class MinecraftRuntimeService
{
    private readonly InstanceService _instances;
    private readonly HttpClient _httpClient = new();

    public MinecraftRuntimeService(InstanceService instances)
    {
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
    }

    internal static string ValidateSupportedLoader(string? loaderValue, string? loaderVersion)
    {
        string loader = (loaderValue ?? string.Empty).Trim();
        string key = loader.Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(key) || key == "vanilla")
            return "vanilla";

        string normalized = key switch
        {
            "forge" => "forge",
            "neoforge" or "neoforged" => "neoforge",
            "fabric" or "fabricmc" => "fabric",
            "quilt" or "quiltmc" => "quilt",
            _ => throw new NotSupportedException(
                $"El loader '{loader}' no está disponible en el port multiplataforma. " +
                "Se admiten Vanilla, Forge, NeoForge, Fabric y Quilt.")
        };

        if (normalized == "forge" && string.IsNullOrWhiteSpace(loaderVersion))
            throw new InvalidOperationException("La instancia no tiene una versión de Forge configurada.");

        return normalized;
    }

    internal static string NormalizeConfiguredLoaderVersion(
        string? loaderVersion,
        string loader,
        string minecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(loaderVersion))
            return string.Empty;

        string normalized = NormalizeLoaderVersion(loaderVersion, loader, minecraftVersion);
        if (string.IsNullOrWhiteSpace(normalized) ||
            string.Equals(normalized, minecraftVersion?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(
                normalized,
                @"^\d+(?:\.\d+)+(?:[-+][0-9A-Za-z.-]+)?$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                $"La versión configurada de {loader} no contiene un identificador válido del loader. Corrige el perfil antes de instalar Minecraft.");
        }

        return normalized;
    }

    internal static string ValidateForgeVersion(string? loaderVersion, string minecraftVersion)
    {
        return NormalizeConfiguredLoaderVersion(loaderVersion, "forge", minecraftVersion);
    }

    internal static string ResolveCustomJavaPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "Has seleccionado Java personalizado, pero no has indicado la ruta del ejecutable. " +
                "Selecciona java o java.exe en Ajustes, o activa Java automático.");
        }

        return JavaExecutableHelper.ResolveExistingPath(configuredPath);
    }

    internal static string NormalizeLoaderVersion(
        string? loaderVersion,
        string loader,
        string minecraftVersion)
    {
        string value = (loaderVersion ?? string.Empty).Trim();
        string gameVersion = (minecraftVersion ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        string normalizedLoader = (loader ?? string.Empty).Trim().ToLowerInvariant();
        string[] prefixes = normalizedLoader switch
        {
            "forge" => new[] { gameVersion + "-forge-", "forge-", gameVersion + "-" },
            "neoforge" => new[] { gameVersion + "-neoforge-", "neoforge-", gameVersion + "-" },
            "fabric" => new[] { gameVersion + "-fabric-loader-", gameVersion + "-fabric-", "fabric-loader-", "fabric-", gameVersion + "-" },
            "quilt" => new[] { gameVersion + "-quilt-loader-", gameVersion + "-quilt-", "quilt-loader-", "quilt-", gameVersion + "-" },
            _ => Array.Empty<string>()
        };

        // Some catalogs save the loader version, while others save a profile name
        // that prefixes the Minecraft version and the loader ID. Keep removing known
        // prefixes until the plain installer version remains.
        bool removedPrefix;
        do
        {
            removedPrefix = false;
            foreach (string prefix in prefixes)
            {
                if (string.IsNullOrEmpty(prefix) ||
                    !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = value[prefix.Length..];
                removedPrefix = true;
                break;
            }
        }
        while (removedPrefix);

        string gameSuffix = "-" + gameVersion;
        if (!string.IsNullOrEmpty(gameVersion) &&
            value.EndsWith(gameSuffix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^gameSuffix.Length];
        }

        return value.Trim();
    }

    public async Task<string> PrepareAsync(
        InstalledInstance instance,
        LauncherPreferences preferences,
        IProgress<double>? progress = null,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(preferences);

        if (!instance.IsInstalled)
            throw new InvalidOperationException("Primero instala el modpack de esta instancia.");

        if (string.IsNullOrWhiteSpace(instance.MinecraftVersion))
            throw new InvalidOperationException("La instancia no tiene una versión de Minecraft configurada.");

        // Validar antes de crear carpetas o iniciar descargas.
        string loader = ValidateSupportedLoader(instance.Loader, instance.LoaderVersion);
        // Rechazar versiones configuradas mal formadas antes de crear carpetas o descargar el juego base.
        if (loader != "vanilla" && !string.IsNullOrWhiteSpace(instance.LoaderVersion))
            _ = NormalizeConfiguredLoaderVersion(instance.LoaderVersion, loader, instance.MinecraftVersion);

        // Validar Java manual antes de crear carpetas o descargar cientos de MB del juego.
        if (!preferences.UseAutomaticJava)
        {
            string customJavaPath = ResolveCustomJavaPath(preferences.CustomJavaPath);
            JavaExecutableHelper.EnsureExecutable(customJavaPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Si se vuelve a preparar una instancia, no conservar una versión anterior ejecutable
        // cuando la instalación nueva falle o se cancele a mitad del proceso.
        instance.RuntimePrepared = false;
        instance.LaunchVersionName = string.Empty;
        await _instances.SaveAsync(instance);
        cancellationToken.ThrowIfCancellationRequested();

        MinecraftPath path = CreateMinecraftPath(instance.Id);
        var launcher = new MinecraftLauncher(path);
        IVersion vanilla = await launcher.GetVersionAsync(instance.MinecraftVersion, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        status?.Report($"Preparando Minecraft {instance.MinecraftVersion}…");
        await launcher.InstallAsync(
            vanilla,
            CreateFileProgress(status, "Minecraft"),
            CreateByteProgress(progress, 0, 70),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        double javaProgressEnd = loader switch
        {
            "vanilla" => 100,
            "fabric" or "quilt" => 85,
            _ => 80
        };
        string javaPath = await EnsureJavaAvailableAsync(
            launcher, vanilla, preferences, progress, status,
            70, javaProgressEnd, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        string launchVersion = instance.MinecraftVersion;

        switch (loader)
        {
            case "forge":
            {
                string forgeVersionValue = NormalizeConfiguredLoaderVersion(
                    instance.LoaderVersion, "forge", instance.MinecraftVersion);
                status?.Report($"Buscando Forge {forgeVersionValue}…");
                cancellationToken.ThrowIfCancellationRequested();
                var versionLoader = new ForgeVersionLoader(_httpClient);
                var forgeVersions = await versionLoader.GetForgeVersions(instance.MinecraftVersion);
                cancellationToken.ThrowIfCancellationRequested();
                ForgeVersion? forgeVersion = forgeVersions.FirstOrDefault(version =>
                    string.Equals(version.ForgeVersionName, forgeVersionValue, StringComparison.OrdinalIgnoreCase) ||
                    version.ForgeVersionName.EndsWith("-forge-" + forgeVersionValue, StringComparison.OrdinalIgnoreCase) ||
                    version.ForgeVersionName.EndsWith("-" + forgeVersionValue, StringComparison.OrdinalIgnoreCase));

                if (forgeVersion is null)
                    throw new InvalidOperationException($"No se encontró Forge {instance.LoaderVersion} para Minecraft {instance.MinecraftVersion}.");

                var installer = new ForgeInstallerVersionMapper().CreateInstaller(forgeVersion);
                launchVersion = installer.VersionName;

                if (!await IsVersionInstalledAsync(launcher, launchVersion, cancellationToken))
                {
                    status?.Report($"Instalando Forge {instance.LoaderVersion}…");
                    var options = new ForgeInstallOptions
                    {
                        JavaPath = javaPath,
                        FileProgress = CreateFileProgress(status, "Forge"),
                        ByteProgress = CreateByteProgress(progress, 80, 96),
                        CancellationToken = cancellationToken,
                        InstallerOutput = new Progress<string>(_ => status?.Report("Instalando componentes de Forge…"))
                    };

                    await installer.Install(launcher.MinecraftPath, launcher.GameInstaller, options);
                    cancellationToken.ThrowIfCancellationRequested();
                    await launcher.GetAllVersionsAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                status?.Report("Comprobando los archivos de Forge…");
                await launcher.InstallAsync(
                    launchVersion,
                    CreateFileProgress(status, "Forge"),
                    CreateByteProgress(progress, 96, 100),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                break;
            }

            case "neoforge":
            {
                string neoForgeVersionValue = NormalizeLoaderVersion(
                    instance.LoaderVersion, "neoforge", instance.MinecraftVersion);
                status?.Report($"Instalando NeoForge {neoForgeVersionValue}…");
                cancellationToken.ThrowIfCancellationRequested();
                var installer = new NeoForgeInstaller(launcher);
                var options = new NeoForgeInstallOptions
                {
                    JavaPath = javaPath,
                    FileProgress = CreateFileProgress(status, "NeoForge"),
                    ByteProgress = CreateByteProgress(progress, 80, 96),
                    CancellationToken = cancellationToken,
                    InstallerOutput = new Progress<string>(message =>
                    {
                        if (!string.IsNullOrWhiteSpace(message))
                            status?.Report(message);
                    }),
                    SkipIfAlreadyInstalled = true
                };

                launchVersion = string.IsNullOrWhiteSpace(neoForgeVersionValue)
                    ? await installer.Install(instance.MinecraftVersion, options)
                    : await installer.Install(instance.MinecraftVersion, neoForgeVersionValue, options);
                cancellationToken.ThrowIfCancellationRequested();
                await launcher.InstallAsync(
                    launchVersion,
                    CreateFileProgress(status, "NeoForge"),
                    CreateByteProgress(progress, 96, 100),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                break;
            }

            case "fabric":
            {
                launchVersion = await InstallFabricAsync(launcher, instance, status, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await launcher.InstallAsync(
                    launchVersion,
                    CreateFileProgress(status, "Fabric"),
                    CreateByteProgress(progress, 85, 100),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                break;
            }

            case "quilt":
            {
                launchVersion = await InstallQuiltAsync(launcher, instance, status, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await launcher.InstallAsync(
                    launchVersion,
                    CreateFileProgress(status, "Quilt"),
                    CreateByteProgress(progress, 85, 100),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                break;
            }

            default:
                // Java ya se comprobó para Vanilla arriba; no volver a consultar ni instalar el mismo runtime.
                progress?.Report(100);
                launchVersion = instance.MinecraftVersion;
                break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        instance.LaunchVersionName = launchVersion;
        instance.RuntimePrepared = true;
        await _instances.SaveAsync(instance);
        progress?.Report(100);
        status?.Report("Minecraft está preparado.");
        return launchVersion;
    }

    public Task<Process> LaunchOfflineAsync(
        InstalledInstance instance,
        LauncherPreferences preferences,
        OfflineAccountProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Username))
            throw new InvalidOperationException("Guarda un perfil sin conexión en Ajustes antes de jugar.");

        return LaunchAsync(
            instance,
            preferences,
            MSession.CreateOfflineSession(profile.Username.Trim()));
    }

    public async Task<Process> LaunchAsync(
        InstalledInstance instance,
        LauncherPreferences preferences,
        MSession session)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(session);

        if (!instance.IsInstalled || !instance.RuntimePrepared ||
            string.IsNullOrWhiteSpace(instance.LaunchVersionName))
            throw new InvalidOperationException("Primero prepara la instalación antes de jugar.");

        string instanceDirectory = _instances.GetInstanceDirectory(instance.Id);
        var launcher = new MinecraftLauncher(CreateMinecraftPath(instance.Id));
        var launchOptions = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = Math.Clamp(preferences.MaximumRamMb, 1024, 32768),
            GameLauncherName = "Negative Launcher",
            GameLauncherVersion = "0.1.1",
            FullScreen = false,
            ScreenWidth = 0,
            ScreenHeight = 0
        };

        if (!preferences.UseAutomaticJava)
        {
            string customJavaPath = ResolveCustomJavaPath(preferences.CustomJavaPath);
            JavaExecutableHelper.EnsureExecutable(customJavaPath);
            launchOptions.JavaPath = customJavaPath;
        }

        if (preferences.EnableCustomJavaArguments &&
            !string.IsNullOrWhiteSpace(preferences.CustomJavaArguments))
        {
            launchOptions.ExtraJvmArguments = new[]
            {
                MArgument.FromCommandLine(preferences.CustomJavaArguments)
            };
        }

        Process process = await launcher.BuildProcessAsync(instance.LaunchVersionName, launchOptions);
        process.StartInfo.WorkingDirectory = instanceDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        // Si la consola está desactivada, no redirigir streams que nadie consumirá:
        // los buffers llenos pueden bloquear el proceso Java durante sesiones largas.
        process.StartInfo.RedirectStandardOutput = preferences.ShowGameConsole;
        process.StartInfo.RedirectStandardError = preferences.ShowGameConsole;

        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar Minecraft.");

        return process;
    }

    private async Task<string> InstallFabricAsync(
        MinecraftLauncher launcher,
        InstalledInstance instance,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        var installer = new FabricInstaller(_httpClient);
        string loaderVersion = NormalizeLoaderVersion(
            instance.LoaderVersion, "fabric", instance.MinecraftVersion);

        if (string.IsNullOrWhiteSpace(loaderVersion))
        {
            status?.Report("Buscando la versión estable de Fabric…");
            cancellationToken.ThrowIfCancellationRequested();
            var availableLoaders = await installer.GetLoaders(instance.MinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            loaderVersion = availableLoaders.FirstOrDefault(loader => loader.Stable)?.Version
                ?? availableLoaders.FirstOrDefault()?.Version
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(loaderVersion))
            throw new InvalidOperationException($"No se encontró una versión de Fabric para Minecraft {instance.MinecraftVersion}.");

        string versionName = FabricInstaller.GetVersionName(instance.MinecraftVersion, loaderVersion);
        if (!await IsVersionInstalledAsync(launcher, versionName, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            status?.Report($"Instalando Fabric {loaderVersion}…");
            await installer.Install(instance.MinecraftVersion, loaderVersion, launcher.MinecraftPath);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return versionName;
    }

    private async Task<string> InstallQuiltAsync(
        MinecraftLauncher launcher,
        InstalledInstance instance,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        var installer = new QuiltInstaller(_httpClient);
        string loaderVersion = NormalizeLoaderVersion(
            instance.LoaderVersion, "quilt", instance.MinecraftVersion);

        if (string.IsNullOrWhiteSpace(loaderVersion))
        {
            status?.Report("Buscando la versión estable de Quilt…");
            cancellationToken.ThrowIfCancellationRequested();
            var availableLoaders = await installer.GetLoaders(instance.MinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            loaderVersion = availableLoaders.FirstOrDefault(loader => loader.Stable)?.Version
                ?? availableLoaders.FirstOrDefault()?.Version
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(loaderVersion))
            throw new InvalidOperationException($"No se encontró una versión de Quilt para Minecraft {instance.MinecraftVersion}.");

        string versionName = QuiltInstaller.GetVersionName(instance.MinecraftVersion, loaderVersion);
        if (!await IsVersionInstalledAsync(launcher, versionName, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            status?.Report($"Instalando Quilt {loaderVersion}…");
            await installer.Install(instance.MinecraftVersion, loaderVersion, launcher.MinecraftPath);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return versionName;
    }

    private static MinecraftPath CreateMinecraftPath(string instanceId)
    {
        string instanceDirectory = InstanceService.InstancesRoot;
        LauncherPathRules.ValidateInstanceId(instanceId);
        instanceDirectory = Path.Combine(instanceDirectory, instanceId);
        Directory.CreateDirectory(instanceDirectory);

        var path = new MinecraftPath(instanceDirectory)
        {
            Assets = InstanceService.SharedMinecraftAssetsRoot,
            Library = InstanceService.SharedMinecraftLibrariesRoot,
            Versions = InstanceService.SharedMinecraftVersionsRoot,
            Runtime = InstanceService.SharedMinecraftRuntimeRoot
        };
        path.CreateDirs();
        return path;
    }

    private static async Task<string> EnsureJavaAvailableAsync(
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
            string customJavaPath = ResolveCustomJavaPath(preferences.CustomJavaPath);
            JavaExecutableHelper.EnsureExecutable(customJavaPath);
            return customJavaPath;
        }

        // Priorizar siempre el runtime asociado a esta versión de Minecraft.
        // Un Java genérico del sistema puede existir y aun así ser demasiado antiguo
        // para versiones modernas del juego; no debe evitar la instalación del runtime requerido.
        string? javaPath = launcher.GetJavaPath(version);
        if (!string.IsNullOrWhiteSpace(javaPath) && File.Exists(javaPath))
        {
            // CmlLib puede devolver rutas dependientes del directorio actual. Normalizarlas
            // aquí evita que el proceso falle cuando Minecraft usa su propio directorio de trabajo.
            javaPath = JavaExecutableHelper.ResolveExistingPath(javaPath);
            JavaExecutableHelper.EnsureExecutable(javaPath);
            return javaPath;
        }

        status?.Report("Buscando el runtime de Java compatible con esta versión…");
        await launcher.InstallAsync(
            version,
            CreateFileProgress(status, "Java"),
            CreateByteProgress(progress, start, end),
            cancellationToken);

        javaPath = launcher.GetJavaPath(version);
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            javaPath = launcher.GetDefaultJavaPath();

        // Como último recurso, localizar Java desde PATH. Esto ayuda en Linux
        // cuando el runtime descargado no queda registrado en la ruta predeterminada
        // de CmlLib, sin anteponer un Java genérico al runtime específico del juego.
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
        {
            javaPath = JavaExecutableHelper.FindOnPath(
                Environment.GetEnvironmentVariable("PATH"),
                OperatingSystem.IsWindows());
        }

        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            throw new InvalidOperationException("No se pudo localizar Java después de preparar Minecraft.");

        // Devolver siempre una ruta absoluta al proceso de lanzamiento, independientemente
        // de si Java proviene del runtime descargado, de CmlLib o del PATH del sistema.
        javaPath = JavaExecutableHelper.ResolveExistingPath(javaPath);
        JavaExecutableHelper.EnsureExecutable(javaPath);
        return javaPath;
    }

    private static IProgress<InstallerProgressChangedEventArgs> CreateFileProgress(
        IProgress<string>? status, string prefix) =>
        new Progress<InstallerProgressChangedEventArgs>(e =>
        {
            if (!string.IsNullOrWhiteSpace(e.Name))
                status?.Report($"{prefix}: {e.Name}");
        });

    private static IProgress<ByteProgress> CreateByteProgress(
        IProgress<double>? target, double start, double end) =>
        new Progress<ByteProgress>(e =>
        {
            if (e.TotalBytes <= 0) return;
            double ratio = (double)e.ProgressedBytes / e.TotalBytes;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio)) return;
            target?.Report(Math.Clamp(start + (end - start) * Math.Clamp(ratio, 0, 1), 0, 100));
        });

    private static async Task<bool> IsVersionInstalledAsync(
        MinecraftLauncher launcher, string versionName, CancellationToken cancellationToken)
    {
        try
        {
            await launcher.GetVersionAsync(versionName, cancellationToken);
            return true;
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            return false;
        }
    }
}
