using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ModpackInstallerService
    {
        private readonly GoogleDriveService _driveService;

        private readonly InstanceService _instanceService;


        public ModpackInstallerService(
            GoogleDriveService driveService,
            InstanceService instanceService)
        {
            _driveService =
                driveService;

            _instanceService =
                instanceService;
        }


        public async Task<InstalledInstance>
            InstallOrUpdateAsync(
                ModpackManifest manifest,
                string installCode,
                IProgress<double>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(
                    manifest.ArchiveFileId))
            {
                throw new InvalidOperationException(
                    "Este modpack no tiene archiveFileId.");
            }


            string instanceDirectory =
                _instanceService
                    .GetInstanceDirectory(
                        manifest.Id);


            string tempRoot =
                Path.Combine(
                    InstanceService.LauncherRoot,
                    "temp",
                    manifest.Id +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N"));


            string zipPath =
                Path.Combine(
                    tempRoot,
                    "modpack.zip");


            string extractedDirectory =
                Path.Combine(
                    tempRoot,
                    "extracted");


            Directory.CreateDirectory(
                tempRoot);

            Directory.CreateDirectory(
                extractedDirectory);


            string? backupDirectory =
                null;


            try
            {
                // =============================================
                // DESCARGAR
                // =============================================

                await _driveService
                    .DownloadFileAsync(
                        manifest.ArchiveFileId,
                        zipPath,
                        progress);


                // =============================================
                // EXTRAER
                // =============================================

                ExtractZipSafely(
                    zipPath,
                    extractedDirectory);


                // =============================================
                // CONSERVAR DATOS DEL USUARIO
                // =============================================

                if (Directory.Exists(
                        instanceDirectory))
                {
                    PreserveExistingData(
                        instanceDirectory,
                        extractedDirectory);
                }


                // =============================================
                // REEMPLAZAR INSTANCIA
                // =============================================

                if (Directory.Exists(
                        instanceDirectory))
                {
                    backupDirectory =
                        instanceDirectory +
                        ".backup-" +
                        Guid.NewGuid()
                            .ToString("N");


                    Directory.Move(
                        instanceDirectory,
                        backupDirectory);
                }


                Directory.Move(
                    extractedDirectory,
                    instanceDirectory);


                InstalledInstance instance =
                    new()
                    {
                        Id =
                            manifest.Id,

                        Name =
                            manifest.Name,

                        InstallCode =
                            installCode,

                        IsInstalled =
                            true,

                        // Después de cambiar los archivos del pack
                        // volvemos a validar/preparar el runtime.
                        RuntimePrepared =
                            false,

                        LaunchVersionName =
                            string.Empty,

                        InstalledVersion =
                            manifest.Version,

                        MinecraftVersion =
                            manifest.MinecraftVersion,

                        Loader =
                            manifest.Loader,

                        LoaderVersion =
                            manifest.LoaderVersion,

                        IconFileId =
                            manifest.IconFileId,

                        BackgroundFileId =
                            manifest.BackgroundFileId
                    };


                await _instanceService
                    .SaveAsync(
                        instance);


                if (backupDirectory != null &&
                    Directory.Exists(
                        backupDirectory))
                {
                    Directory.Delete(
                        backupDirectory,
                        recursive: true);
                }


                return instance;
            }
            catch
            {
                if (backupDirectory != null &&
                    Directory.Exists(
                        backupDirectory))
                {
                    if (Directory.Exists(
                            instanceDirectory))
                    {
                        Directory.Delete(
                            instanceDirectory,
                            recursive: true);
                    }


                    Directory.Move(
                        backupDirectory,
                        instanceDirectory);
                }


                throw;
            }
            finally
            {
                if (Directory.Exists(
                        tempRoot))
                {
                    try
                    {
                        Directory.Delete(
                            tempRoot,
                            recursive: true);
                    }
                    catch
                    {
                    }
                }
            }
        }


        // =====================================================
        // EXTRAER ZIP DE FORMA SEGURA
        // =====================================================

        private static void ExtractZipSafely(
            string zipPath,
            string destinationDirectory)
        {
            string destinationRoot =
                Path.GetFullPath(
                    destinationDirectory) +
                Path.DirectorySeparatorChar;


            using ZipArchive archive =
                ZipFile.OpenRead(
                    zipPath);


            foreach (ZipArchiveEntry entry in
                archive.Entries)
            {
                string entryPath =
                    entry.FullName.Replace(
                        '/',
                        Path.DirectorySeparatorChar);


                string destinationPath =
                    Path.GetFullPath(
                        Path.Combine(
                            destinationDirectory,
                            entryPath));


                if (!destinationPath.StartsWith(
                        destinationRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "El ZIP contiene una ruta no segura.");
                }


                if (string.IsNullOrEmpty(
                        entry.Name))
                {
                    Directory.CreateDirectory(
                        destinationPath);

                    continue;
                }


                string? parent =
                    Path.GetDirectoryName(
                        destinationPath);


                if (parent != null)
                {
                    Directory.CreateDirectory(
                        parent);
                }


                entry.ExtractToFile(
                    destinationPath,
                    overwrite: true);
            }
        }


        // =====================================================
        // DATOS QUE SE CONSERVAN AL ACTUALIZAR
        // =====================================================

        private static void PreserveExistingData(
            string oldDirectory,
            string newDirectory)
        {
            string[] preservedDirectories =
            {
                "saves",
                "screenshots",

                // Archivos instalados por CmlLib/Minecraft.
                "assets",
                "libraries",
                "versions",
                "runtime",
                "jre"
            };


            string[] preservedFiles =
            {
                "options.txt",
                "servers.dat",
                "servers.dat_old"
            };


            foreach (string directoryName in
                preservedDirectories)
            {
                string source =
                    Path.Combine(
                        oldDirectory,
                        directoryName);


                string destination =
                    Path.Combine(
                        newDirectory,
                        directoryName);


                if (Directory.Exists(source))
                {
                    CopyDirectory(
                        source,
                        destination);
                }
            }


            foreach (string fileName in
                preservedFiles)
            {
                string source =
                    Path.Combine(
                        oldDirectory,
                        fileName);


                string destination =
                    Path.Combine(
                        newDirectory,
                        fileName);


                if (File.Exists(source))
                {
                    File.Copy(
                        source,
                        destination,
                        overwrite: true);
                }
            }


            foreach (string directory in
                Directory.GetDirectories(
                    oldDirectory,
                    "XaeroWaypoints*"))
            {
                string name =
                    Path.GetFileName(
                        directory);


                CopyDirectory(
                    directory,
                    Path.Combine(
                        newDirectory,
                        name));
            }
        }


        private static void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory)
        {
            Directory.CreateDirectory(
                destinationDirectory);


            foreach (string file in
                Directory.GetFiles(
                    sourceDirectory))
            {
                string destination =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(
                            file));


                File.Copy(
                    file,
                    destination,
                    overwrite: true);
            }


            foreach (string directory in
                Directory.GetDirectories(
                    sourceDirectory))
            {
                string destination =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(
                            directory));


                CopyDirectory(
                    directory,
                    destination);
            }
        }
    }
}
