using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class InstanceService
    {
        public static string LauncherRoot { get; } =
            LauncherPaths.DefaultLauncherRoot;


        public static string DefaultStorageRoot =>
            LauncherRoot;


        private static string _storageRoot =
            LauncherRoot;


        public static string StorageRoot =>
            _storageRoot;


        public static string InstancesRoot =>
            new LauncherStorageLayout(_storageRoot).InstancesRoot;


        public static string PackageCacheRoot =>
            new LauncherStorageLayout(_storageRoot).PackageCacheRoot;


        public static string TempRoot =>
            new LauncherStorageLayout(_storageRoot).TempRoot;


        // =====================================================
        // MINECRAFT COMPARTIDO
        //
        // Los archivos pesados que son iguales entre instancias
        // viven una sola vez bajo <StorageRoot>\minecraft.
        // El gameDir de cada instancia sigue siendo independiente.
        // =====================================================

        public static string SharedMinecraftRoot =>
            new LauncherStorageLayout(_storageRoot).SharedMinecraftRoot;


        public static string SharedMinecraftAssetsRoot =>
            new LauncherStorageLayout(_storageRoot).SharedMinecraftAssetsRoot;


        public static string SharedMinecraftLibrariesRoot =>
            new LauncherStorageLayout(_storageRoot).SharedMinecraftLibrariesRoot;


        public static string SharedMinecraftVersionsRoot =>
            new LauncherStorageLayout(_storageRoot).SharedMinecraftVersionsRoot;


        public static string SharedMinecraftRuntimeRoot =>
            new LauncherStorageLayout(_storageRoot).SharedMinecraftRuntimeRoot;


        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

        private readonly InstanceDataStore _instanceDataStore;


        public InstanceService()
        {
            _instanceDataStore = new InstanceDataStore(
                () => InstancesRoot,
                _jsonOptions);

            EnsureStorageDirectories();
        }


        public string GetStorageRoot()
        {
            return
                _storageRoot;
        }


        // =====================================================
        // CONFIGURAR UBICACIÓN DE INSTALACIONES
        // =====================================================

        public void ConfigureStorageRoot(
            string? storageRootPath)
        {
            string normalized =
                NormalizeStorageRoot(
                    storageRootPath);


            _storageRoot =
                normalized;


            EnsureStorageDirectories();
        }


        public async Task ChangeStorageRootAsync(
            string newStorageRootPath)
        {
            string newRoot =
                NormalizeStorageRoot(
                    newStorageRootPath);


            string oldRoot =
                _storageRoot;


            if (PathsEqual(
                    oldRoot,
                    newRoot))
            {
                EnsureStorageDirectories();

                return;
            }


            string oldInstances =
                Path.Combine(
                    oldRoot,
                    "instances");


            string newInstances =
                Path.Combine(
                    newRoot,
                    "instances");


            string oldMinecraft =
                Path.Combine(
                    oldRoot,
                    "minecraft");


            string newMinecraft =
                Path.Combine(
                    newRoot,
                    "minecraft");


            LauncherPathRules.ValidateStorageRootChange(
                newRoot,
                oldInstances,
                oldMinecraft);


            Directory.CreateDirectory(
                newRoot);


            await MoveDirectorySafelyAsync(
                oldInstances,
                newInstances);


            // El runtime compartido de Minecraft es información importante:
            // versiones, assets, librerías y Java. Lo movemos junto con
            // las instancias para que cambiar de disco no obligue a descargarlo.
            await MoveDirectorySafelyAsync(
                oldMinecraft,
                newMinecraft);


            string oldCache =
                Path.Combine(
                    oldRoot,
                    "cache");


            string newCache =
                Path.Combine(
                    newRoot,
                    "cache");


            try
            {
                await MoveDirectorySafelyAsync(
                    oldCache,
                    newCache);
            }
            catch
            {
                // La caché es regenerable; no bloquea el cambio de disco.
            }


            _storageRoot =
                newRoot;


            EnsureStorageDirectories();


            string oldTemp =
                Path.Combine(
                    oldRoot,
                    "temp");


            if (!PathsEqual(
                    oldTemp,
                    TempRoot))
            {
                try
                {
                    if (Directory.Exists(
                            oldTemp))
                    {
                        Directory.Delete(
                            oldTemp,
                            recursive: true);
                    }
                }
                catch
                {
                }
            }
        }


        private static string NormalizeStorageRoot(
            string? storageRootPath)
        {
            return LauncherStorageLayout.NormalizeRoot(
                storageRootPath,
                LauncherRoot);
        }


        private static void EnsureStorageDirectories()
        {
            Directory.CreateDirectory(
                LauncherRoot);


            Directory.CreateDirectory(
                _storageRoot);


            Directory.CreateDirectory(
                InstancesRoot);


            Directory.CreateDirectory(
                PackageCacheRoot);


            Directory.CreateDirectory(
                TempRoot);


            Directory.CreateDirectory(
                SharedMinecraftRoot);


            Directory.CreateDirectory(
                SharedMinecraftAssetsRoot);


            Directory.CreateDirectory(
                SharedMinecraftLibrariesRoot);


            Directory.CreateDirectory(
                SharedMinecraftVersionsRoot);


            Directory.CreateDirectory(
                SharedMinecraftRuntimeRoot);
        }


        private static async Task MoveDirectorySafelyAsync(
            string sourceDirectory,
            string destinationDirectory)
        {
            if (!Directory.Exists(
                    sourceDirectory))
            {
                Directory.CreateDirectory(
                    destinationDirectory);

                return;
            }


            if (!Directory
                    .EnumerateFileSystemEntries(
                        sourceDirectory)
                    .Any())
            {
                Directory.CreateDirectory(
                    destinationDirectory);

                try
                {
                    Directory.Delete(
                        sourceDirectory,
                        recursive: false);
                }
                catch
                {
                }

                return;
            }


            if (Directory.Exists(
                    destinationDirectory) &&
                Directory
                    .EnumerateFileSystemEntries(
                        destinationDirectory)
                    .Any())
            {
                throw new InvalidOperationException(
                    "La carpeta de destino ya contiene archivos. " +
                    "Selecciona una carpeta vacía para evitar mezclar instalaciones.");
            }


            string? sourceDrive =
                Path.GetPathRoot(
                    sourceDirectory);


            string? destinationDrive =
                Path.GetPathRoot(
                    destinationDirectory);


            bool sameDrive =
                !string.IsNullOrWhiteSpace(
                    sourceDrive) &&
                !string.IsNullOrWhiteSpace(
                    destinationDrive) &&
                string.Equals(
                    sourceDrive,
                    destinationDrive,
                    StringComparison.OrdinalIgnoreCase);


            if (sameDrive)
            {
                if (Directory.Exists(
                        destinationDirectory))
                {
                    Directory.Delete(
                        destinationDirectory,
                        recursive: true);
                }


                string? destinationParent =
                    Path.GetDirectoryName(
                        destinationDirectory);


                if (string.IsNullOrWhiteSpace(
                        destinationParent))
                {
                    throw new InvalidOperationException(
                        "La ruta de destino no es válida.");
                }


                Directory.CreateDirectory(
                    destinationParent);


                Directory.Move(
                    sourceDirectory,
                    destinationDirectory);

                return;
            }


            await Task.Run(
                () =>
                {
                    CopyDirectory(
                        sourceDirectory,
                        destinationDirectory);


                    Directory.Delete(
                        sourceDirectory,
                        recursive: true);
                });
        }


        private static void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory)
        {
            Directory.CreateDirectory(
                destinationDirectory);


            foreach (string directory in
                Directory.EnumerateDirectories(
                    sourceDirectory,
                    "*",
                    SearchOption.AllDirectories))
            {
                string relative =
                    Path.GetRelativePath(
                        sourceDirectory,
                        directory);


                Directory.CreateDirectory(
                    Path.Combine(
                        destinationDirectory,
                        relative));
            }


            foreach (string sourceFile in
                Directory.EnumerateFiles(
                    sourceDirectory,
                    "*",
                    SearchOption.AllDirectories))
            {
                string relative =
                    Path.GetRelativePath(
                        sourceDirectory,
                        sourceFile);


                string destinationFile =
                    Path.Combine(
                        destinationDirectory,
                        relative);


                string? parent =
                    Path.GetDirectoryName(
                        destinationFile);


                if (!string.IsNullOrWhiteSpace(
                        parent))
                {
                    Directory.CreateDirectory(
                        parent);
                }


                File.Copy(
                    sourceFile,
                    destinationFile,
                    overwrite: true);
            }
        }


        private static bool PathsEqual(
            string left,
            string right)
        {
            return LauncherPathRules.PathsEqual(left, right);
        }


        // =====================================================
        // RUTA DE UNA INSTANCIA
        // =====================================================

        public string GetInstanceDirectory(
            string instanceId)
        {
            return _instanceDataStore.GetInstanceDirectory(instanceId);
        }


        // =====================================================
        // CARGAR INSTANCIAS
        // =====================================================

        public Task<List<InstalledInstance>> LoadAllAsync()
        {
            return _instanceDataStore.LoadAllAsync();
        }


        // =====================================================
        // ELIMINAR UNA INSTANCIA REGISTRADA
        // =====================================================

        public async Task DeleteInstanceAsync(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException("El ID de la instancia es obligatorio.", nameof(instanceId));
            }

            // La validación del almacén rechaza IDs que podrían salir de la carpeta instances.
            string instanceDirectory = _instanceDataStore.GetInstanceDirectory(instanceId);
            string instancesRoot = Path.GetFullPath(InstancesRoot);
            string fullDirectory = Path.GetFullPath(instanceDirectory);
            string? parentDirectory = Path.GetDirectoryName(fullDirectory);

            if (!string.Equals(
                    parentDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    instancesRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new InvalidOperationException("La ruta de la instancia no pertenece al almacenamiento configurado.");
            }

            List<InstalledInstance> instances = await LoadAllAsync();
            bool isRegistered = instances.Any(instance =>
                string.Equals(instance.Id, instanceId, StringComparison.Ordinal));

            if (!isRegistered)
            {
                throw new InvalidOperationException("La instancia ya no está registrada o no existe.");
            }

            if (Directory.Exists(fullDirectory))
            {
                Directory.Delete(fullDirectory, recursive: true);
            }
        }


        // =====================================================
        // GUARDAR INSTANCIA
        // =====================================================

        public Task SaveAsync(
            InstalledInstance instance)
        {
            return _instanceDataStore.SaveAsync(instance);
        }
    }
}
