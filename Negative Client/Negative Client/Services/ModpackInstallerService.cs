using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ModpackInstallerService
    {
        private const string ManagedFilesName =
            ".negativeclient-managed-files.json";


        public static string PackageCacheRoot { get; } =
            Path.Combine(
                InstanceService.LauncherRoot,
                "cache",
                "packages");


        private static readonly HashSet<string> PreservedUserFiles =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "options.txt",
                "optionsof.txt",
                "optionsshaders.txt",
                "servers.dat",
                "servers.dat_old",
                "usercache.json",
                "usernamecache.json"
            };


        private readonly GoogleDriveService _driveService;

        private readonly InstanceService _instanceService;


        private sealed class ManagedFilesData
        {
            public List<string> Files { get; set; } =
                new();
        }


        public ModpackInstallerService(
            GoogleDriveService driveService,
            InstanceService instanceService)
        {
            _driveService =
                driveService;

            _instanceService =
                instanceService;
        }


        // =====================================================
        // INSTALAR / ACTUALIZAR
        // =====================================================

        public async Task<InstalledInstance>
            InstallOrUpdateAsync(
                ModpackManifest manifest,
                string installCode,
                IProgress<double>? progress = null,
                DownloadOperationController? controller = null)
        {
            return
                await InstallArchiveAsync(
                    manifest,
                    installCode,
                    currentInstance: null,
                    progress,
                    controller);
        }


        // =====================================================
        // VERIFICAR INTEGRIDAD
        //
        // Reinstala todos los archivos administrados por el ZIP,
        // pero NO elimina archivos extra que el usuario haya añadido.
        // =====================================================

        public async Task<InstalledInstance>
            VerifyIntegrityAsync(
                ModpackManifest manifest,
                string installCode,
                InstalledInstance currentInstance,
                IProgress<double>? progress = null,
                DownloadOperationController? controller = null)
        {
            return
                await InstallArchiveAsync(
                    manifest,
                    installCode,
                    currentInstance,
                    progress,
                    controller);
        }


        private async Task<InstalledInstance>
            InstallArchiveAsync(
                ModpackManifest manifest,
                string installCode,
                InstalledInstance? currentInstance,
                IProgress<double>? progress,
                DownloadOperationController? controller)
        {
            bool ownsController =
                controller == null;


            controller ??=
                new DownloadOperationController();
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


            string cacheZipPath =
                GetPackageCachePath(
                    manifest);


            string archivePath;


            string extractedDirectory =
                Path.Combine(
                    tempRoot,
                    "extracted");


            string backupDirectory =
                Path.Combine(
                    tempRoot,
                    "backup-managed");


            Directory.CreateDirectory(
                tempRoot);

            Directory.CreateDirectory(
                extractedDirectory);

            Directory.CreateDirectory(
                backupDirectory);


            try
            {
                if (TryUseCachedArchive(
                        cacheZipPath))
                {
                    archivePath =
                        cacheZipPath;


                    progress?.Report(
                        100);
                }
                else
                {
                    await RunPauseAwarePhaseAsync(
                        controller,
                        cancellationToken =>
                            _driveService
                                .DownloadFileResumableAsync(
                                    manifest.ArchiveFileId,
                                    cacheZipPath,
                                    progress,
                                    cancellationToken));


                    ValidateZip(
                        cacheZipPath);


                    archivePath =
                        cacheZipPath;
                }


                controller.ThrowIfStopped();


                // Si el launcher se cerró durante una extracción anterior,
                // esta carpeta temporal es nueva. Extraemos de nuevo desde
                // el ZIP completo/resumido y después comparamos archivos.
                await RunPauseAwarePhaseAsync(
                    controller,
                    cancellationToken =>
                        Task.Run(
                            () =>
                            {
                                cancellationToken
                                    .ThrowIfCancellationRequested();


                                ExtractZipSafely(
                                    archivePath,
                                    extractedDirectory,
                                    cancellationToken);
                            },
                            cancellationToken));


                HashSet<string> newManagedFiles =
                    EnumerateManagedFiles(
                        extractedDirectory);


                if (!Directory.Exists(
                        instanceDirectory))
                {
                    Directory.Move(
                        extractedDirectory,
                        instanceDirectory);
                }
                else
                {
                    await RunPauseAwarePhaseAsync(
                        controller,
                        cancellationToken =>
                            ApplyArchiveInPlaceAsync(
                                instanceDirectory,
                                extractedDirectory,
                                backupDirectory,
                                newManagedFiles,
                                cancellationToken));
                }


                controller.ThrowIfStopped();


                await SaveManagedFilesAsync(
                    instanceDirectory,
                    newManagedFiles);


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

                        // Se vuelve a validar Minecraft/Java/Forge
                        // después de cualquier reinstalación del pack.
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


                // Si la instancia ya existía, conservamos datos de
                // ejecución que no dependan del ZIP hasta que el
                // Runtime vuelva a prepararse.
                if (currentInstance != null)
                {
                    instance.Name =
                        string.IsNullOrWhiteSpace(
                            manifest.Name)
                            ? currentInstance.Name
                            : manifest.Name;
                }


                await _instanceService
                    .SaveAsync(
                        instance);


                return instance;
            }
            finally
            {
                if (ownsController)
                {
                    controller.Dispose();
                }


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
        // SINCRONIZACIÓN DEL ZIP
        // =====================================================

        private async Task ApplyArchiveInPlaceAsync(
            string instanceDirectory,
            string extractedDirectory,
            string backupDirectory,
            HashSet<string> newManagedFiles,
            CancellationToken cancellationToken)
        {
            HashSet<string> oldManagedFiles =
                await LoadManagedFilesAsync(
                    instanceDirectory);


            // Instalaciones creadas antes de este sistema no tienen
            // lista de archivos administrados. En ese caso no borramos
            // absolutamente nada: solo sobrescribimos lo que viene
            // en el ZIP. Así ningún archivo extra se pierde.
            HashSet<string> filesToPotentiallyChange =
                new(
                    oldManagedFiles,
                    StringComparer.OrdinalIgnoreCase);


            filesToPotentiallyChange.UnionWith(
                newManagedFiles);


            HashSet<string> existedBefore =
                new(
                    StringComparer.OrdinalIgnoreCase);


            // Copiamos a backup únicamente los archivos que podrían
            // cambiar. No duplicamos assets/libraries/runtime completos.
            foreach (string relativePath in
                filesToPotentiallyChange)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
                string destinationPath =
                    CombineRelativePath(
                        instanceDirectory,
                        relativePath);


                if (!File.Exists(
                        destinationPath))
                {
                    continue;
                }


                existedBefore.Add(
                    relativePath);


                string backupPath =
                    CombineRelativePath(
                        backupDirectory,
                        relativePath);


                string? backupParent =
                    Path.GetDirectoryName(
                        backupPath);


                if (!string.IsNullOrWhiteSpace(
                        backupParent))
                {
                    Directory.CreateDirectory(
                        backupParent);
                }


                File.Copy(
                    destinationPath,
                    backupPath,
                    overwrite: true);
            }


            try
            {
                // Quitamos archivos que pertenecían al pack anterior
                // y ya no existen en el nuevo. Los archivos extra
                // nunca aparecen en oldManagedFiles, así que no se borran.
                foreach (string oldManagedFile in
                    oldManagedFiles)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    if (newManagedFiles.Contains(
                            oldManagedFile) ||
                        IsPreservedUserFile(
                            oldManagedFile))
                    {
                        continue;
                    }


                    string oldPath =
                        CombineRelativePath(
                            instanceDirectory,
                            oldManagedFile);


                    if (File.Exists(
                            oldPath))
                    {
                        File.Delete(
                            oldPath);
                    }
                }


                // Aplicamos TODOS los archivos del ZIP.
                foreach (string relativePath in
                    newManagedFiles)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    string sourcePath =
                        CombineRelativePath(
                            extractedDirectory,
                            relativePath);


                    string destinationPath =
                        CombineRelativePath(
                            instanceDirectory,
                            relativePath);


                    // options.txt y equivalentes representan preferencias
                    // del usuario. Si ya existen, no las pisamos durante
                    // una actualización o verificación.
                    if (IsPreservedUserFile(
                            relativePath) &&
                        File.Exists(
                            destinationPath))
                    {
                        continue;
                    }


                    // Reinicio/reanudación: si este archivo ya quedó bien
                    // copiado antes de que se cerrara el launcher, no lo
                    // volvemos a escribir.
                    if (File.Exists(
                            destinationPath) &&
                        FilesAreEquivalent(
                            sourcePath,
                            destinationPath))
                    {
                        continue;
                    }


                    string? destinationParent =
                        Path.GetDirectoryName(
                            destinationPath);


                    if (!string.IsNullOrWhiteSpace(
                            destinationParent))
                    {
                        Directory.CreateDirectory(
                            destinationParent);
                    }


                    File.Copy(
                        sourcePath,
                        destinationPath,
                        overwrite: true);
                }
            }
            catch
            {
                // Restauramos únicamente lo que tocamos.
                foreach (string relativePath in
                    filesToPotentiallyChange)
                {
                    string destinationPath =
                        CombineRelativePath(
                            instanceDirectory,
                            relativePath);


                    string backupPath =
                        CombineRelativePath(
                            backupDirectory,
                            relativePath);


                    if (File.Exists(
                            backupPath))
                    {
                        string? destinationParent =
                            Path.GetDirectoryName(
                                destinationPath);


                        if (!string.IsNullOrWhiteSpace(
                                destinationParent))
                        {
                            Directory.CreateDirectory(
                                destinationParent);
                        }


                        File.Copy(
                            backupPath,
                            destinationPath,
                            overwrite: true);
                    }
                    else if (!existedBefore.Contains(
                                 relativePath) &&
                             File.Exists(
                                 destinationPath))
                    {
                        File.Delete(
                            destinationPath);
                    }
                }


                throw;
            }
        }


        // =====================================================
        // ARCHIVOS ADMINISTRADOS POR NEGATIVE CLIENT
        // =====================================================

        private static HashSet<string>
            EnumerateManagedFiles(
                string rootDirectory)
        {
            return
                Directory
                    .EnumerateFiles(
                        rootDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .Select(
                        file =>
                            NormalizeRelativePath(
                                Path.GetRelativePath(
                                    rootDirectory,
                                    file)))
                    .Where(
                        relativePath =>
                            !string.Equals(
                                relativePath,
                                "instance.json",
                                StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(
                                relativePath,
                                ManagedFilesName,
                                StringComparison.OrdinalIgnoreCase))
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);
        }


        private static async Task<HashSet<string>>
            LoadManagedFilesAsync(
                string instanceDirectory)
        {
            string path =
                Path.Combine(
                    instanceDirectory,
                    ManagedFilesName);


            if (!File.Exists(
                    path))
            {
                return
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
            }


            try
            {
                string json =
                    await File.ReadAllTextAsync(
                        path);


                ManagedFilesData? data =
                    JsonSerializer.Deserialize<ManagedFilesData>(
                        json);


                return
                    data?.Files?
                        .Select(
                            NormalizeRelativePath)
                        .ToHashSet(
                            StringComparer.OrdinalIgnoreCase)
                    ??
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
            }
        }


        private static async Task
            SaveManagedFilesAsync(
                string instanceDirectory,
                IEnumerable<string> managedFiles)
        {
            string path =
                Path.Combine(
                    instanceDirectory,
                    ManagedFilesName);


            ManagedFilesData data =
                new()
                {
                    Files =
                        managedFiles
                            .OrderBy(
                                file =>
                                    file,
                                StringComparer.OrdinalIgnoreCase)
                            .ToList()
                };


            string json =
                JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions
                    {
                        WriteIndented =
                            true
                    });


            await File.WriteAllTextAsync(
                path,
                json);
        }


        private static bool IsPreservedUserFile(
            string relativePath)
        {
            string normalized =
                NormalizeRelativePath(
                    relativePath);


            return
                PreservedUserFiles.Contains(
                    normalized);
        }


        public void ClearPendingDownload(
            ModpackManifest manifest)
        {
            string partialPath =
                GetPackageCachePath(
                    manifest) +
                ".part";


            try
            {
                if (File.Exists(
                        partialPath))
                {
                    File.Delete(
                        partialPath);
                }
            }
            catch
            {
            }
        }


        private static async Task RunPauseAwarePhaseAsync(
            DownloadOperationController controller,
            Func<CancellationToken, Task> phase)
        {
            while (true)
            {
                await controller
                    .WaitWhilePausedAsync();


                controller.ThrowIfStopped();


                CancellationToken cancellationToken =
                    controller.BeginPhase();


                try
                {
                    await phase(
                        cancellationToken);


                    return;
                }
                catch (OperationCanceledException)
                    when (controller.IsPaused &&
                          !controller.IsStopped)
                {
                    // Pausa real: se conserva el .part / archivos ya
                    // escritos y esta fase se vuelve a intentar al reanudar.
                }
                finally
                {
                    controller.EndPhase();
                }
            }
        }


        private static bool FilesAreEquivalent(
            string sourcePath,
            string destinationPath)
        {
            FileInfo source =
                new FileInfo(
                    sourcePath);


            FileInfo destination =
                new FileInfo(
                    destinationPath);


            if (!source.Exists ||
                !destination.Exists ||
                source.Length !=
                destination.Length)
            {
                return false;
            }


            // File.Copy conserva normalmente LastWriteTimeUtc.
            if (source.LastWriteTimeUtc ==
                destination.LastWriteTimeUtc)
            {
                return true;
            }


            // Si tamaño coincide pero fecha no, comprobamos contenido.
            // Así una instalación interrumpida puede continuar sin volver
            // a copiar gigabytes que ya estaban correctos.
            using FileStream sourceStream =
                File.OpenRead(
                    sourcePath);


            using FileStream destinationStream =
                File.OpenRead(
                    destinationPath);


            byte[] sourceHash =
                SHA256.HashData(
                    sourceStream);


            byte[] destinationHash =
                SHA256.HashData(
                    destinationStream);


            return
                sourceHash.AsSpan()
                    .SequenceEqual(
                        destinationHash);
        }


        // =====================================================
        // CACHÉ DEL PAQUETE
        // =====================================================

        private static string GetPackageCachePath(
            ModpackManifest manifest)
        {
            Directory.CreateDirectory(
                PackageCacheRoot);


            string safeId =
                SanitizeFileName(
                    manifest.Id);


            string safeVersion =
                SanitizeFileName(
                    manifest.Version);


            string safeFileId =
                SanitizeFileName(
                    manifest.ArchiveFileId);


            return
                Path.Combine(
                    PackageCacheRoot,
                    $"{safeId}-{safeVersion}-{safeFileId}.zip");
        }


        private static bool TryUseCachedArchive(
            string cacheZipPath)
        {
            if (!File.Exists(
                    cacheZipPath))
            {
                return false;
            }


            try
            {
                ValidateZip(
                    cacheZipPath);


                return true;
            }
            catch
            {
                try
                {
                    File.Delete(
                        cacheZipPath);
                }
                catch
                {
                }


                return false;
            }
        }


        private static void SaveArchiveToCache(
            string sourceZipPath,
            string cacheZipPath)
        {
            string? parent =
                Path.GetDirectoryName(
                    cacheZipPath);


            if (!string.IsNullOrWhiteSpace(
                    parent))
            {
                Directory.CreateDirectory(
                    parent);
            }


            string temporaryCachePath =
                cacheZipPath +
                ".tmp-" +
                Guid.NewGuid()
                    .ToString("N");


            try
            {
                File.Copy(
                    sourceZipPath,
                    temporaryCachePath,
                    overwrite: true);


                ValidateZip(
                    temporaryCachePath);


                File.Move(
                    temporaryCachePath,
                    cacheZipPath,
                    overwrite: true);
            }
            finally
            {
                if (File.Exists(
                        temporaryCachePath))
                {
                    try
                    {
                        File.Delete(
                            temporaryCachePath);
                    }
                    catch
                    {
                    }
                }
            }
        }


        private static void ValidateZip(
            string zipPath)
        {
            using ZipArchive archive =
                ZipFile.OpenRead(
                    zipPath);


            _ =
                archive.Entries.Count;
        }


        private static string SanitizeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return "unknown";
            }


            char[] invalidCharacters =
                Path.GetInvalidFileNameChars();


            string safe =
                new string(
                    value
                        .Where(
                            character =>
                                !invalidCharacters.Contains(
                                    character))
                        .ToArray());


            return
                string.IsNullOrWhiteSpace(
                    safe)
                    ? "unknown"
                    : safe;
        }


        // =====================================================
        // EXTRAER ZIP DE FORMA SEGURA
        // =====================================================

        private static void ExtractZipSafely(
            string zipPath,
            string destinationDirectory,
            CancellationToken cancellationToken)
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
                cancellationToken
                    .ThrowIfCancellationRequested();
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


                if (!string.IsNullOrWhiteSpace(
                        parent))
                {
                    Directory.CreateDirectory(
                        parent);
                }


                entry.ExtractToFile(
                    destinationPath,
                    overwrite: true);
            }
        }


        private static string CombineRelativePath(
            string rootDirectory,
            string relativePath)
        {
            string normalized =
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar);


            string combined =
                Path.GetFullPath(
                    Path.Combine(
                        rootDirectory,
                        normalized));


            string root =
                Path.GetFullPath(
                    rootDirectory) +
                Path.DirectorySeparatorChar;


            if (!combined.StartsWith(
                    root,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Se detectó una ruta de archivo no segura.");
            }


            return combined;
        }


        private static string NormalizeRelativePath(
            string path)
        {
            return
                path
                    .Replace(
                        Path.DirectorySeparatorChar,
                        '/')
                    .Replace(
                        Path.AltDirectorySeparatorChar,
                        '/')
                    .TrimStart('/');
        }
    }
}
