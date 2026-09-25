namespace Negative_Client.Models
{
    public sealed class InstalledInstance
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string InstallCode { get; set; } = string.Empty;


        // Indica si los archivos del pack
        // realmente están descargados.
        public bool IsInstalled { get; set; }


        // Vacío mientras no se haya descargado.
        public string InstalledVersion { get; set; } = string.Empty;


        public string MinecraftVersion { get; set; } = string.Empty;


        public string Loader { get; set; } = string.Empty;

        public string LoaderVersion { get; set; } = string.Empty;


        public string IconFileId { get; set; } = string.Empty;

        public string BackgroundFileId { get; set; } = string.Empty;
    }
}