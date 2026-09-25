namespace Negative_Client.Models
{
    public sealed class InstalledInstance
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string InstallCode { get; set; } = string.Empty;


        // El ZIP del modpack ya está descargado.
        public bool IsInstalled { get; set; }


        // Minecraft + loader + Java ya están preparados.
        public bool RuntimePrepared { get; set; }


        // Nombre real de la versión que CmlLib debe iniciar.
        // Ejemplo:
        // 1.20.1-forge-47.4.0
        public string LaunchVersionName { get; set; } = string.Empty;


        public string InstalledVersion { get; set; } = string.Empty;


        public string MinecraftVersion { get; set; } = string.Empty;


        public string Loader { get; set; } = string.Empty;

        public string LoaderVersion { get; set; } = string.Empty;


        public string IconFileId { get; set; } = string.Empty;

        public string BackgroundFileId { get; set; } = string.Empty;
    }
}
