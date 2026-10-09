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
