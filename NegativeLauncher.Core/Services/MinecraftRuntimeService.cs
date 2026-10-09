using System;
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

        cancellationToken.ThrowIfCancellationRequested();
        MinecraftPath path = CreateMinecraftPath(instance.Id);
        var launcher = new MinecraftLauncher(path);
        IVersion vanilla = await launcher.GetVersionAsync(instance.MinecraftVersion, cancellationToken);

        status?.Report($"Preparando Minecraft {instance.MinecraftVersion}…");
        await launcher.InstallAsync(
            vanilla,
            CreateFileProgress(status, "Minecraft"),
            CreateByteProgress(progress, 0, 70),
            cancellationToken);

        string javaPath = await EnsureJavaAvailableAsync(
            launcher, vanilla, preferences, progress, status, 70, 80, cancellationToken);

        string launchVersion = instance.MinecraftVersion;
        string loader = (instance.Loader ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(loader) &&
            !loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase))
        {
            if (!loader.Equals("forge", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"El loader '{loader}' aún no está implementado en la versión multiplataforma.");

            if (string.IsNullOrWhiteSpace(instance.LoaderVersion))
                throw new InvalidOperationException("La instancia no tiene una versión de Forge configurada.");

            status?.Report($"Buscando Forge {instance.LoaderVersion}…");
            var versionLoader = new ForgeVersionLoader(_httpClient);
            var forgeVersions = await versionLoader.GetForgeVersions(instance.MinecraftVersion);
            ForgeVersion? forgeVersion = forgeVersions.FirstOrDefault(version =>
                string.Equals(version.ForgeVersionName, instance.LoaderVersion, StringComparison.OrdinalIgnoreCase) ||
                version.ForgeVersionName.EndsWith("-forge-" + instance.LoaderVersion, StringComparison.OrdinalIgnoreCase) ||
                version.ForgeVersionName.EndsWith("-" + instance.LoaderVersion, StringComparison.OrdinalIgnoreCase));

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
                await launcher.GetAllVersionsAsync();
            }

            status?.Report("Comprobando los archivos de Forge…");
            await launcher.InstallAsync(
                launchVersion,
                CreateFileProgress(status, "Forge"),
                CreateByteProgress(progress, 96, 100),
                cancellationToken);
        }
        else
        {
            await EnsureJavaAvailableAsync(launcher, vanilla, preferences, progress, status, 70, 100, cancellationToken);
            launchVersion = instance.MinecraftVersion;
        }

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
            if (string.IsNullOrWhiteSpace(preferences.CustomJavaPath) ||
                !File.Exists(preferences.CustomJavaPath))
                throw new FileNotFoundException("La ruta de Java configurada no existe.");
            launchOptions.JavaPath = preferences.CustomJavaPath;
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
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar Minecraft.");

        return process;
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
            if (string.IsNullOrWhiteSpace(preferences.CustomJavaPath) || !File.Exists(preferences.CustomJavaPath))
                throw new FileNotFoundException("La ruta de Java configurada no existe. Revisa Ajustes.");
            return preferences.CustomJavaPath;
        }

        string? javaPath = launcher.GetJavaPath(version);
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            javaPath = launcher.GetDefaultJavaPath();

        if (!string.IsNullOrWhiteSpace(javaPath) && File.Exists(javaPath))
            return javaPath;

        status?.Report("Buscando el runtime de Java…");
        await launcher.InstallAsync(
            version,
            CreateFileProgress(status, "Java"),
            CreateByteProgress(progress, start, end),
            cancellationToken);

        javaPath = launcher.GetJavaPath(version);
        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            javaPath = launcher.GetDefaultJavaPath();

        if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            throw new InvalidOperationException("No se pudo localizar Java después de preparar Minecraft.");

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
