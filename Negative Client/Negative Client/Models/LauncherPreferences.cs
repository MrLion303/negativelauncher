namespace Negative_Client.Models
{
    public sealed class LauncherPreferences
    {
        // 4 GB por defecto.
        public int MaximumRamMb { get; set; } = 4096;


        // Si es true, CmlLib usa el Java que descarga Mojang.
        public bool UseAutomaticJava { get; set; } = true;


        // Solo se usa cuando UseAutomaticJava == false.
        public string CustomJavaPath { get; set; } =
            string.Empty;


        // DESACTIVADO por defecto, como pidió el usuario.
        public bool CloseLauncherOnGameStart { get; set; } =
            false;


        // Permite añadir argumentos adicionales a la JVM.
        public bool EnableCustomJavaArguments { get; set; } =
            false;


        public string CustomJavaArguments { get; set; } =
            string.Empty;
    }
}
