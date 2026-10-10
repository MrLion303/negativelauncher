using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CmlLib.Core;

namespace Negative_Client.Services
{
    /// <summary>
    /// Mantiene un único conjunto de archivos de Minecraft para todas
    /// las instancias, sin compartir el gameDir de mods/config/options.
    /// </summary>
    public sealed class SharedMinecraftStorageService
    {
        private static readonly object MigrationSync =
            new object();


        private readonly InstanceService
            _instanceService;


        public SharedMinecraftStorageService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;
        }


        // =====================================================
        // CREAR MINECRAFTPATH
        // =====================================================

        public MinecraftPath CreateMinecraftPath(
            string instanceId)
        {
            string instanceDirectory =
                _instanceService
                    .GetInstanceDirectory(
                        instanceId);


            Directory.CreateDirectory(
                instanceDirectory);


            // Compatibilidad con instalaciones creadas antes de que
            // Negative Client tuviera almacenamiento compartido.
            MigrateLegacyRuntimeForDirectory(
                instanceDirectory);


            MinecraftPath path =
                new MinecraftPath(
                    instanceDirectory)
                {
                    Assets =
                        InstanceService
                            .SharedMinecraftAssetsRoot,

                    Library =
                        InstanceService
                            .SharedMinecraftLibrariesRoot,

                    Versions =
                        InstanceService
                            .SharedMinecraftVersionsRoot,

                    Runtime =
                        InstanceService
                            .SharedMinecraftRuntimeRoot

                    // Resource se deja dentro de la instancia.
                    // En Minecraft moderno casi no se usa, y mantenerlo
                    // separado evita interferir con mods antiguos.
                };


            path.CreateDirs();


            return path;
        }


        // =====================================================
        // MIGRAR INSTALACIONES ANTIGUAS
        // =====================================================

        public Task MigrateAllLegacyRuntimeAsync()
        {
            return
                Task.Run(
                    () =>
                    {
                        if (!Directory.Exists(
                                InstanceService.InstancesRoot))
                        {
                            return;
                        }


                        foreach (string instanceDirectory in
                            Directory.EnumerateDirectories(
                                InstanceService.InstancesRoot))
                        {
                            MigrateLegacyRuntimeForDirectory(
                                instanceDirectory);
                        }
                    });
        }


        public void MigrateLegacyRuntimeForDirectory(
            string instanceDirectory)
        {
            if (string.IsNullOrWhiteSpace(
                    instanceDirectory) ||
                !Directory.Exists(
                    instanceDirectory))
            {
                return;
            }


            lock (MigrationSync)
            {
                MergeDirectoryIntoShared(
                    Path.Combine(
                        instanceDirectory,
                        "assets"),
                    InstanceService
                        .SharedMinecraftAssetsRoot);


                MergeDirectoryIntoShared(
                    Path.Combine(
                        instanceDirectory,
                        "libraries"),
                    InstanceService
                        .SharedMinecraftLibrariesRoot);


                MergeDirectoryIntoShared(
                    Path.Combine(
                        instanceDirectory,
                        "versions"),
                    InstanceService
                        .SharedMinecraftVersionsRoot);


                MergeDirectoryIntoShared(
                    Path.Combine(
                        instanceDirectory,
                        "runtime"),
                    InstanceService
                        .SharedMinecraftRuntimeRoot);
            }
        }


        private static void MergeDirectoryIntoShared(
            string sourceDirectory,
            string destinationDirectory)
        {
            if (!Directory.Exists(
                    sourceDirectory))
            {
                return;
            }


            Directory.CreateDirectory(
                destinationDirectory);


            List<string> sourceFiles =
                Directory
                    .EnumerateFiles(
                        sourceDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .ToList();


            foreach (string sourceFile in
                sourceFiles)
            {
                string relative =
                    Path.GetRelativePath(
                        sourceDirectory,
                        sourceFile);


                string destinationFile =
                    Path.GetFullPath(
                        Path.Combine(
                            destinationDirectory,
                            relative));


                string destinationRoot =
                    Path.GetFullPath(
                        destinationDirectory)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;


                if (!destinationFile.StartsWith(
                        destinationRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Se detectó una ruta no segura al migrar Minecraft.");
                }


                // Si ya existe en la carpeta compartida, conservamos esa
                // copia y dejamos la antigua hasta que el usuario pulse
                // LIMPIAR ARCHIVOS NO UTILIZADOS.
                if (File.Exists(
                        destinationFile))
                {
                    continue;
                }


                string? destinationParent =
                    Path.GetDirectoryName(
                        destinationFile);


                if (!string.IsNullOrWhiteSpace(
                        destinationParent))
                {
                    Directory.CreateDirectory(
                        destinationParent);
                }


                try
                {
                    File.Move(
                        sourceFile,
                        destinationFile);
                }
                catch (IOException)
                {
                    File.Copy(
                        sourceFile,
                        destinationFile,
                        overwrite: false);


                    File.Delete(
                        sourceFile);
                }
            }


            DeleteEmptyDirectories(
                sourceDirectory);
        }


        private static void DeleteEmptyDirectories(
            string rootDirectory)
        {
            if (!Directory.Exists(
                    rootDirectory))
            {
                return;
            }


            foreach (string directory in
                Directory
                    .EnumerateDirectories(
                        rootDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .OrderByDescending(
                        directory =>
                            directory.Length))
            {
                try
                {
                    if (!Directory
                            .EnumerateFileSystemEntries(
                                directory)
                            .Any())
                    {
                        Directory.Delete(
                            directory,
                            recursive: false);
                    }
                }
                catch
                {
                }
            }


            try
            {
                if (Directory.Exists(
                        rootDirectory) &&
                    !Directory
                        .EnumerateFileSystemEntries(
                            rootDirectory)
                        .Any())
                {
                    Directory.Delete(
                        rootDirectory,
                        recursive: false);
                }
            }
            catch
            {
            }
        }
    }
}
