namespace Negative_Client.Models
{
    public sealed class LauncherPreferences
    {
        public int MaximumRamMb { get; set; } = 4096;

        public bool UseAutomaticJava { get; set; } = true;

        public string CustomJavaPath { get; set; } =
            string.Empty;

        public bool CloseLauncherOnGameStart { get; set; } =
            false;

        public bool EnableCustomJavaArguments { get; set; } =
            false;

        public string CustomJavaArguments { get; set; } =
            string.Empty;

        public bool ShowGameConsole { get; set; } =
            false;

        public bool DeveloperMode { get; set; } =
            false;

        public string DeveloperMinecraftVersion { get; set; } =
            string.Empty;

        public bool DeveloperShowSnapshots { get; set; } =
            false;

        public bool DeveloperShowBetas { get; set; } =
            false;

        public string StorageRootPath { get; set; } =
            string.Empty;

        public bool EnableHolidayLauncherThemes { get; set; } =
            true;

        // "premium" = cuenta Microsoft autenticada.
        // "offline" = perfil local / no premium.
        public string AccountMode { get; set; } =
            "premium";
    }
}
