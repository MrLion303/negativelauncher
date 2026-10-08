namespace Negative_Client.Models
{
    public sealed class ModpackManifest
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;


        public string MinecraftVersion { get; set; } = string.Empty;


        public string Loader { get; set; } = string.Empty;

        public string LoaderVersion { get; set; } = string.Empty;


        public string ArchiveFileId { get; set; } = string.Empty;


        // Imagen del círculo de la barra izquierda
        public string IconFileId { get; set; } = string.Empty;


        // Fondo de la pantalla de la instalación
        public string BackgroundFileId { get; set; } = string.Empty;
    }
}