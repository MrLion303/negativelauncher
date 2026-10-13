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
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "NegativeClient");


        public static string DefaultStorageRoot =>
            LauncherRoot;


        private static string _storageRoot =
            LauncherRoot;


        public static string StorageRoot =>
            _storageRoot;


        public static string InstancesRoot =>
            Path.Combine(
                _storageRoot,
                "instances");


        public static string PackageCacheRoot =>
            Path.Combine(
                _storageRoot,
                "cache",
                "packages");


        public static string TempRoot =>
            Path.Combine(
                _storageRoot,
                "temp");


        // =====================================================
        // MINECRAFT COMPARTIDO
        //
        // Los archivos pesados que son iguales entre instancias
        // viven una sola vez bajo <StorageRoot>\minecraft.
        // El gameDir de cada instancia sigue siendo independiente.
        // =====================================================

        public static string SharedMinecraftRoot =>
            Path.Combine(
                _storageRoot,
                "minecraft");


        public static string SharedMinecraftAssetsRoot =>
            Path.Combine(
                SharedMinecraftRoot,
                "assets");


        public static string SharedMinecraftLibrariesRoot =>
            Path.Combine(
                SharedMinecraftRoot,
                "libraries");


        public static string SharedMinecraftVersionsRoot =>
            Path.Combine(
                SharedMinecraftRoot,
                "versions");


        public static string SharedMinecraftRuntimeRoot =>
            Path.Combine(
                SharedMinecraftRoot,
                "runtime");


        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };


        public InstanceService()
        {
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


            // Evitar raíces anidadas en ambos sentidos. En especial, una raíz
            // nueva dentro de "cache" haría que el traslado intentara copiar una
            // carpeta dentro de sí misma; una raíz padre puede mezclar datos existentes.
            if (IsSameOrSubPath(
                    newRoot,
                    oldRoot) ||
                IsSameOrSubPath(
                    oldRoot,
                    newRoot))
            {
                throw new InvalidOperationException(
                    "La nueva ubicación debe ser una carpeta independiente, fuera de la ubicación actual y de sus subcarpetas.");
            }


            Directory.CreateDirectory(
                newRoot);

            string oldCache =
                Path.Combine(
                    oldRoot,
                    "cache");

            string newCache =
                Path.Combine(
                    newRoot,
                    "cache");

            var movedDirectories =
                new List<(string Source, string Destination)>();

            try
            {
                bool hadInstances =
                    Directory.Exists(oldInstances);

                await MoveDirectorySafelyAsync(
                    oldInstances,
                    newInstances);

                if (hadInstances)
                {
                    movedDirectories.Add(
                        (oldInstances, newInstances));
                }

                // El runtime compartido de Minecraft es información importante:
                // versiones, assets, librerías y Java. Lo movemos junto con
                // las instancias para que cambiar de disco no obligue a descargarlo.
                bool hadMinecraft =
                    Directory.Exists(oldMinecraft);

                await MoveDirectorySafelyAsync(
                    oldMinecraft,
                    newMinecraft);

                if (hadMinecraft)
                {
                    movedDirectories.Add(
                        (oldMinecraft, newMinecraft));
                }

                // La caché es regenerable, por lo que un fallo al moverla no bloquea
                // el cambio de ubicación de los datos esenciales.
                bool hadCache =
                    Directory.Exists(oldCache);

                try
                {
                    await MoveDirectorySafelyAsync(
                        oldCache,
                        newCache);

                    if (hadCache)
                    {
                        movedDirectories.Add(
                            (oldCache, newCache));
                    }
                }
                catch
                {
                    // La caché se puede volver a descargar.
                }

                _storageRoot =
                    newRoot;

                EnsureStorageDirectories();
            }
            catch (Exception migrationException)
            {
                // No dejar las instancias en la ubicación nueva si falla el traslado
                // del runtime o la preparación final de la nueva raíz.
                _storageRoot =
                    oldRoot;

                var rollbackErrors =
                    new List<Exception>();

                for (int i = movedDirectories.Count - 1; i >= 0; i--)
                {
                    (string source, string destination) =
                        movedDirectories[i];

                    try
                    {
                        if (Directory.Exists(destination) &&
                            !Directory.Exists(source))
                        {
                            await MoveDirectorySafelyAsync(
                                destination,
                                source);
                        }
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackErrors.Add(
                            rollbackException);
                    }
                }

                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(
                        0,
                        migrationException);

                    throw new AggregateException(
                        "Falló la migración del almacenamiento y no se pudieron revertir todos los movimientos.",
                        rollbackErrors);
                }

                throw;
            }

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
            string candidate =
                string.IsNullOrWhiteSpace(
                    storageRootPath)
                    ? LauncherRoot
                    : storageRootPath.Trim();


            // TrimEndingDirectorySeparator conserva intacta una raíz real
            // ("/" en Linux y "C:\\" en Windows) en lugar de convertirla
            // en una cadena vacía o en una ruta relativa de unidad.
            return
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        candidate));
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


            string sourceVolume =
                GetVolumeRoot(
                    sourceDirectory);


            string destinationVolume =
                GetVolumeRoot(
                    destinationDirectory);


            bool sameDrive =
                string.Equals(
                    sourceVolume,
                    destinationVolume,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal);


            if (sameDrive)
            {
                if (Directory.Exists(
                        destinationDirectory))
                {
                    Directory.Delete(
                        destinationDirectory,
                        recursive: true);
                }


                string? sameVolumeDestinationParent =
                    Path.GetDirectoryName(
                        destinationDirectory);


                if (string.IsNullOrWhiteSpace(
                        sameVolumeDestinationParent))
                {
                    throw new InvalidOperationException(
                        "La ruta de destino no es válida.");
                }


                Directory.CreateDirectory(
                    sameVolumeDestinationParent);


                Directory.Move(
                    sourceDirectory,
                    destinationDirectory);

                return;
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

            string destinationName =
                Path.GetFileName(
                    destinationDirectory);

            string stagingDirectory =
                Path.Combine(
                    destinationParent,
                    "." + destinationName + ".moving-" +
                    Guid.NewGuid().ToString("N"));

            await Task.Run(
                () =>
                {
                    try
                    {
                        // Copiar primero a una carpeta provisional en el destino.
                        // La carpeta final solo aparece cuando la copia está completa.
                        CopyDirectory(
                            sourceDirectory,
                            stagingDirectory);

                        if (Directory.Exists(
                                destinationDirectory))
                        {
                            if (Directory
                                    .EnumerateFileSystemEntries(
                                        destinationDirectory)
                                    .Any())
                            {
                                throw new InvalidOperationException(
                                    "La carpeta de destino contiene archivos.");
                            }

                            Directory.Delete(
                                destinationDirectory,
                                recursive: false);
                        }

                        Directory.Move(
                            stagingDirectory,
                            destinationDirectory);

                        // La copia ya está completa y publicada. Si el sistema impide
                        // limpiar el origen, conservar ambas ubicaciones es más seguro que
                        // lanzar un error y dejar el destino fuera del registro de rollback.
                        try
                        {
                            Directory.Delete(
                                sourceDirectory,
                                recursive: true);
                        }
                        catch
                        {
                            // El destino ya contiene la copia completa; el origen queda
                            // como respaldo recuperable para una limpieza posterior.
                        }
                    }
                    catch
                    {
                        try
                        {
                            if (Directory.Exists(
                                    stagingDirectory))
                            {
                                Directory.Delete(
                                    stagingDirectory,
                                    recursive: true);
                            }
                        }
                        catch
                        {
                            // La limpieza no debe ocultar el error original.
                        }

                        throw;
                    }
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


        private static string GetVolumeRoot(
            string path)
        {
            string fullPath =
                Path.GetFullPath(
                    path);

            string normalizedPath =
                Path.TrimEndingDirectorySeparator(
                    fullPath);

            StringComparison comparison =
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

            string? bestRoot = null;

            // En Linux, Path.GetPathRoot devuelve "/" incluso para puntos de montaje
            // distintos. Elegir la raíz de volumen más específica evita tratar un
            // traslado entre discos montados como si fuera un simple rename.
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                string root;

                try
                {
                    root =
                        Path.GetFullPath(
                            drive.RootDirectory.FullName);
                }
                catch
                {
                    continue;
                }

                string normalizedRoot =
                    Path.TrimEndingDirectorySeparator(
                        root);

                string rootPrefix =
                    normalizedRoot.EndsWith(
                        Path.DirectorySeparatorChar)
                        ? normalizedRoot
                        : normalizedRoot + Path.DirectorySeparatorChar;

                bool matches =
                    string.Equals(
                        normalizedPath,
                        normalizedRoot,
                        comparison) ||
                    normalizedPath.StartsWith(
                        rootPrefix,
                        comparison);

                if (matches &&
                    (bestRoot is null ||
                     normalizedRoot.Length > bestRoot.Length))
                {
                    bestRoot =
                        normalizedRoot;
                }
            }

            return
                bestRoot ??
                Path.GetPathRoot(fullPath) ??
                fullPath;
        }


        private static bool PathsEqual(
            string left,
            string right)
        {
            return
                string.Equals(
                    Path.GetFullPath(
                        left)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(
                        right)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool IsSameOrSubPath(
            string candidate,
            string parent)
        {
            string fullCandidate =
                Path.GetFullPath(
                    candidate)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;


            string fullParent =
                Path.GetFullPath(
                    parent)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;


            return
                fullCandidate.StartsWith(
                    fullParent,
                    StringComparison.OrdinalIgnoreCase);
        }


        // =====================================================
        // RUTA DE UNA INSTANCIA
        // =====================================================

        public string GetInstanceDirectory(
            string instanceId)
        {
            ValidateInstanceId(
                instanceId);


            return
                Path.Combine(
                    InstancesRoot,
                    instanceId);
        }


        // =====================================================
        // CARGAR INSTANCIAS
        // =====================================================

        public async Task<List<InstalledInstance>>
            LoadAllAsync()
        {
            List<InstalledInstance> result =
                new();


            Directory.CreateDirectory(
                InstancesRoot);


            foreach (string directory in
                Directory.GetDirectories(
                    InstancesRoot))
            {
                string instanceFile =
                    Path.Combine(
                        directory,
                        "instance.json");


                if (!File.Exists(
                        instanceFile))
                {
                    continue;
                }


                try
                {
                    string json =
                        await File.ReadAllTextAsync(
                            instanceFile);


                    InstalledInstance? instance =
                        JsonSerializer.Deserialize<InstalledInstance>(
                            json,
                            _jsonOptions);


                    if (instance != null &&
                        !string.IsNullOrWhiteSpace(
                            instance.Id))
                    {
                        result.Add(
                            instance);
                    }
                }
                catch
                {
                }
            }


            return result;
        }


        // =====================================================
        // GUARDAR INSTANCIA
        // =====================================================

        public async Task SaveAsync(
            InstalledInstance instance)
        {
            string directory =
                GetInstanceDirectory(
                    instance.Id);


            Directory.CreateDirectory(
                directory);


            string instanceFile =
                Path.Combine(
                    directory,
                    "instance.json");


            string json =
                JsonSerializer.Serialize(
                    instance,
                    _jsonOptions);


            await File.WriteAllTextAsync(
                instanceFile,
                json);
        }


        private static void ValidateInstanceId(
            string instanceId)
        {
            if (string.IsNullOrWhiteSpace(
                    instanceId))
            {
                throw new InvalidOperationException(
                    "La instancia no tiene ID.");
            }


            foreach (char character in
                instanceId)
            {
                bool valid =
                    char.IsLetterOrDigit(
                        character) ||
                    character == '-' ||
                    character == '_';


                if (!valid)
                {
                    throw new InvalidOperationException(
                        "El ID de la instancia contiene caracteres no permitidos.");
                }
            }
        }
    }
}
