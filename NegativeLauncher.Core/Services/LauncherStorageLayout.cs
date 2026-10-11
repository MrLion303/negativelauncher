using System.Text.RegularExpressions;

namespace Negative_Client.Services
{
    /// <summary>
    /// Describe las carpetas administradas por el almacenamiento de Minecraft.
    /// No crea, mueve ni elimina archivos.
    /// </summary>
    public sealed class LauncherStorageLayout
    {
        public string Root { get; }
        public string InstancesRoot { get; }
        public string PackageCacheRoot { get; }
        public string TempRoot { get; }
        public string SharedMinecraftRoot { get; }
        public string SharedMinecraftAssetsRoot { get; }
        public string SharedMinecraftLibrariesRoot { get; }
        public string SharedMinecraftVersionsRoot { get; }
        public string SharedMinecraftRuntimeRoot { get; }

        public static string NormalizeRoot(string? storageRootPath, string defaultStorageRoot)
        {
            string candidate = string.IsNullOrWhiteSpace(storageRootPath)
                ? defaultStorageRoot
                : storageRootPath.Trim();

            // Aceptar variables de entorno en la ruta configurada, algo habitual
            // al copiar rutas desde una terminal o la configuración del sistema.
            candidate = Environment.ExpandEnvironmentVariables(candidate);

            if (!OperatingSystem.IsWindows())
            {
                // En Linux y macOS también se usan las formas habituales de shell
                // $VARIABLE y ${VARIABLE} dentro de rutas escritas en Ajustes.
                candidate = Regex.Replace(
                    candidate,
                    @"\$(?:\{(?<braced>[A-Za-z_][A-Za-z0-9_]*)\}|(?<plain>[A-Za-z_][A-Za-z0-9_]*))",
                    match =>
                    {
                        string variableName = match.Groups["braced"].Success
                            ? match.Groups["braced"].Value
                            : match.Groups["plain"].Value;

                        return Environment.GetEnvironmentVariable(variableName) ?? match.Value;
                    });
            }

            // En Linux/macOS, "~/..." es una abreviatura habitual en rutas escritas
            // manualmente. No debe terminar como una carpeta literal llamada "~".
            if (!OperatingSystem.IsWindows())
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(home))
                {
                    if (string.Equals(candidate, "~", StringComparison.Ordinal))
                    {
                        candidate = home;
                    }
                    else if (candidate.StartsWith("~/", StringComparison.Ordinal) ||
                             candidate.StartsWith("~\\", StringComparison.Ordinal))
                    {
                        candidate = Path.Combine(home, candidate[2..]);
                    }
                }
            }

            return LauncherPathRules.NormalizeFullPath(candidate);
        }

        public LauncherStorageLayout(string storageRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("La carpeta de almacenamiento no puede estar vacía.", nameof(storageRoot));
            }

            Root = Path.GetFullPath(storageRoot);
            InstancesRoot = Path.Combine(Root, "instances");
            PackageCacheRoot = Path.Combine(Root, "cache", "packages");
            TempRoot = Path.Combine(Root, "temp");
            SharedMinecraftRoot = Path.Combine(Root, "minecraft");
            SharedMinecraftAssetsRoot = Path.Combine(SharedMinecraftRoot, "assets");
            SharedMinecraftLibrariesRoot = Path.Combine(SharedMinecraftRoot, "libraries");
            SharedMinecraftVersionsRoot = Path.Combine(SharedMinecraftRoot, "versions");
            SharedMinecraftRuntimeRoot = Path.Combine(SharedMinecraftRoot, "runtime");
        }
    }
}
