using System.IO;

namespace Negative_Client.Services
{
    /// <summary>
    /// Centraliza la ubicación predeterminada de los datos del launcher.
    /// En Windows conserva la carpeta que ya utiliza la versión original.
    /// </summary>
    public static class LauncherPaths
    {
        public static string DefaultLauncherRoot { get; } = ResolveDefaultLauncherRoot();

        private static string ResolveDefaultLauncherRoot() =>
            ResolveDefaultLauncherRoot(
                OperatingSystem.IsWindows(),
                OperatingSystem.IsMacOS(),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetEnvironmentVariable("XDG_DATA_HOME"));

        // Separar la selección de ruta del entorno permite verificar cada política de SO
        // en CI sin modificar variables de entorno ni depender del sistema del runner.
        internal static string ResolveDefaultLauncherRoot(
            bool isWindows,
            bool isMacOS,
            string? home,
            string? applicationData,
            string? xdgDataHome)
        {
            if (isWindows)
            {
                // Debe permanecer idéntica a InstanceService.LauncherRoot en la versión original.
                string basePath = !string.IsNullOrWhiteSpace(applicationData)
                    ? applicationData
                    : !string.IsNullOrWhiteSpace(home)
                        ? home
                        : Path.GetTempPath();
                return Path.Combine(basePath, "NegativeClient");
            }

            if (isMacOS && !string.IsNullOrWhiteSpace(home))
                return Path.Combine(home, "Library", "Application Support", "NegativeClient");

            if (!string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathRooted(xdgDataHome))
                return Path.Combine(xdgDataHome, "NegativeClient");

            if (!string.IsNullOrWhiteSpace(home))
                return Path.Combine(home, ".local", "share", "NegativeClient");

            // Último recurso para entornos restringidos sin HOME ni XDG_DATA_HOME válidos.
            string fallback = !string.IsNullOrWhiteSpace(applicationData)
                ? applicationData
                : Path.GetTempPath();
            return Path.Combine(fallback, "NegativeClient");
        }
    }
}
