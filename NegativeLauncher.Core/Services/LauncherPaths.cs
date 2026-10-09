namespace Negative_Client.Services
{
    /// <summary>
    /// Centraliza la ubicación predeterminada de los datos del launcher.
    /// En Windows conserva la carpeta que ya utiliza la versión original.
    /// </summary>
    public static class LauncherPaths
    {
        public static string DefaultLauncherRoot { get; } = ResolveDefaultLauncherRoot();

        private static string ResolveDefaultLauncherRoot()
        {
            if (OperatingSystem.IsWindows())
            {
                // Debe permanecer idéntica a InstanceService.LauncherRoot en la versión original.
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "NegativeClient");
            }

            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (OperatingSystem.IsMacOS() && !string.IsNullOrWhiteSpace(home))
            {
                return Path.Combine(home, "Library", "Application Support", "NegativeClient");
            }

            string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(dataHome) && Path.IsPathRooted(dataHome))
            {
                return Path.Combine(dataHome, "NegativeClient");
            }

            if (!string.IsNullOrWhiteSpace(home))
            {
                return Path.Combine(home, ".local", "share", "NegativeClient");
            }

            // Último recurso para entornos restringidos sin HOME definido.
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NegativeClient");
        }
    }
}
