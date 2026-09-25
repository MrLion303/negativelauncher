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

        // Desactivado por defecto.
        public bool ShowGameConsole { get; set; } =
            false;
    }
}
