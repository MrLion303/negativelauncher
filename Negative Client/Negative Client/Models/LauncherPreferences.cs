namespace Negative_Client.Models
{
    public sealed class LauncherPreferences
    {
        // 4 GB por defecto.
        public int MaximumRamMb { get; set; } = 4096;


        // Si es true, CmlLib usa el Java que descarga Mojang
        // para la versión correspondiente.
        public bool UseAutomaticJava { get; set; } = true;


        // Solo se usa cuando UseAutomaticJava == false.
        public string CustomJavaPath { get; set; } = string.Empty;
    }
}
