using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ScreenshotArchiveStore
    {
        private readonly string _archiveRoot;

        private static readonly JsonSerializerOptions JsonOptions =
            new()
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

        public ScreenshotArchiveStore(string archiveRoot)
        {
            if (string.IsNullOrWhiteSpace(archiveRoot))
            {
                throw new ArgumentException("La ruta del archivo de capturas es obligatoria.", nameof(archiveRoot));
            }

            _archiveRoot = Path.GetFullPath(archiveRoot);
        }

        public string ArchiveInstanceScreenshots(
            string instanceId,
            string instanceName,
            string screenshotsDirectory)
        {
            LauncherPathRules.ValidateInstanceId(instanceId);

            if (!Directory.Exists(screenshotsDirectory))
            {
                return string.Empty;
            }

            if (!Directory.EnumerateFiles(
                    screenshotsDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .Any(IsSupportedImage))
            {
                return string.Empty;
            }

            Directory.CreateDirectory(_archiveRoot);

            string folderName = $"{instanceId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
            string archiveDirectory = Path.Combine(_archiveRoot, folderName);
            Directory.CreateDirectory(archiveDirectory);

            string targetScreenshotsDirectory = Path.Combine(archiveDirectory, "screenshots");
            string metadataPath = Path.Combine(archiveDirectory, "archive-info.json");
            string temporaryMetadataPath = metadataPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool movedByRename = false;
            bool copyAttempted = false;

            try
            {
                try
                {
                    Directory.Move(screenshotsDirectory, targetScreenshotsDirectory);
                    movedByRename = true;
                }
                catch (IOException) when (
                    Directory.Exists(screenshotsDirectory) &&
                    !Directory.Exists(targetScreenshotsDirectory))
                {
                    // El origen puede estar en un disco distinto al archivo. Copiamos
                    // primero y conservamos el original hasta guardar los metadatos.
                    copyAttempted = true;
                    DirectoryMigrationHelper.CopyDirectoryContents(
                        screenshotsDirectory,
                        targetScreenshotsDirectory);
                }

                ScreenshotArchiveInfo info = new()
                {
                    InstanceId = instanceId,
                    InstanceName = instanceName,
                    ScreenshotsDirectory = targetScreenshotsDirectory
                };

                string json = JsonSerializer.Serialize(info, JsonOptions);
                File.WriteAllText(temporaryMetadataPath, json);
                File.Move(temporaryMetadataPath, metadataPath, overwrite: true);

                // El archivo y sus metadatos ya existen; ahora es seguro retirar el origen.
                if (copyAttempted)
                    TryDeleteDirectoryTree(screenshotsDirectory);

                return targetScreenshotsDirectory;
            }
            catch
            {
                try
                {
                    if (movedByRename &&
                        Directory.Exists(targetScreenshotsDirectory) &&
                        !Directory.Exists(screenshotsDirectory))
                    {
                        Directory.Move(targetScreenshotsDirectory, screenshotsDirectory);
                    }
                    else if (copyAttempted && Directory.Exists(archiveDirectory))
                    {
                        // La copia no quedó archivada correctamente; se conserva el original.
                        TryDeleteDirectoryTree(archiveDirectory);
                    }

                    if (Directory.Exists(archiveDirectory) &&
                        !Directory.EnumerateFileSystemEntries(archiveDirectory).Any())
                    {
                        Directory.Delete(archiveDirectory);
                    }
                }
                catch
                {
                    // Si la restauración falla, se conserva la copia para recuperación manual.
                }

                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryMetadataPath))
                        File.Delete(temporaryMetadataPath);
                }
                catch
                {
                    // La limpieza temporal no debe ocultar el resultado del archivado.
                }
            }
        }

        /// <summary>
        /// Elimina capturas archivadas asociadas a una instancia que se elimina.
        /// Las rutas procedentes del JSON se validan antes de borrar cualquier directorio.
        /// </summary>
        public void DeleteArchivesForInstance(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || !Directory.Exists(_archiveRoot))
                return;

            foreach (ScreenshotArchiveInfo archive in GetArchives().Where(item =>
                string.Equals(item.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    string screenshotsDirectory = Path.GetFullPath(archive.ScreenshotsDirectory);
                    string? archiveDirectory = Path.GetDirectoryName(screenshotsDirectory);
                    if (string.IsNullOrWhiteSpace(archiveDirectory))
                        continue;

                    string fullArchiveRoot = Path.GetFullPath(_archiveRoot);
                    archiveDirectory = Path.GetFullPath(archiveDirectory);

                    // Nunca permitir que un archive-info.json manipulado cause borrados
                    // fuera de la carpeta de archivos de capturas del launcher.
                    if (LauncherPathRules.PathsEqual(archiveDirectory, fullArchiveRoot) ||
                        !LauncherPathRules.IsSameOrSubPath(archiveDirectory, fullArchiveRoot))
                    {
                        continue;
                    }

                    string expectedScreenshots = Path.Combine(archiveDirectory, "screenshots");
                    if (!LauncherPathRules.PathsEqual(expectedScreenshots, screenshotsDirectory))
                        continue;

                    if (Directory.Exists(archiveDirectory))
                        Directory.Delete(archiveDirectory, recursive: true);
                }
                catch
                {
                    // Un archivo bloqueado o unos metadatos inválidos no deben interrumpir el resto.
                }
            }
        }

        public ScreenshotArchiveInfo[] GetArchives()
        {
            if (!Directory.Exists(_archiveRoot))
            {
                return Array.Empty<ScreenshotArchiveInfo>();
            }

            string fullArchiveRoot = Path.GetFullPath(_archiveRoot);

            return Directory
                .EnumerateFiles(_archiveRoot, "archive-info.json", SearchOption.AllDirectories)
                .Select(filePath =>
                {
                    try
                    {
                        string metadataPath = Path.GetFullPath(filePath);
                        string? archiveDirectory = Path.GetDirectoryName(metadataPath);
                        if (string.IsNullOrWhiteSpace(archiveDirectory) ||
                            LauncherPathRules.PathsEqual(archiveDirectory, fullArchiveRoot) ||
                            !LauncherPathRules.IsSameOrSubPath(archiveDirectory, fullArchiveRoot))
                        {
                            return null;
                        }

                        string json = File.ReadAllText(metadataPath);
                        ScreenshotArchiveInfo? archive =
                            JsonSerializer.Deserialize<ScreenshotArchiveInfo>(json, JsonOptions);
                        if (archive is null || string.IsNullOrWhiteSpace(archive.ScreenshotsDirectory))
                            return null;

                        string expectedScreenshotsDirectory =
                            Path.Combine(archiveDirectory, "screenshots");
                        string storedScreenshotsDirectory =
                            Path.GetFullPath(archive.ScreenshotsDirectory);

                        // El metadato solo puede designar la carpeta esperada dentro
                        // de su propio archivo, nunca una ruta arbitraria del usuario.
                        if (!LauncherPathRules.PathsEqual(
                                storedScreenshotsDirectory,
                                expectedScreenshotsDirectory))
                        {
                            return null;
                        }

                        return archive;
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(archive => archive != null)
                .Cast<ScreenshotArchiveInfo>()
                .ToArray();
        }

        public void CleanupArchiveIfEmpty(string screenshotFilePath)
        {
            string? archiveDirectory = GetArchiveContainerDirectory(screenshotFilePath);

            if (string.IsNullOrWhiteSpace(archiveDirectory) ||
                !Directory.Exists(archiveDirectory))
            {
                return;
            }

            string screenshotsDirectory = Path.Combine(archiveDirectory, "screenshots");

            if (!Directory.Exists(screenshotsDirectory))
            {
                TryDeleteDirectoryTree(archiveDirectory);
                return;
            }

            bool hasAnyImages = Directory
                .EnumerateFiles(screenshotsDirectory, "*", SearchOption.AllDirectories)
                .Any(IsSupportedImage);

            if (!hasAnyImages)
            {
                TryDeleteDirectoryTree(archiveDirectory);
            }
        }

        private string? GetArchiveContainerDirectory(string screenshotFilePath)
        {
            string fullPath = Path.GetFullPath(screenshotFilePath);
            string archiveRoot = Path.GetFullPath(_archiveRoot);

            if (!LauncherPathRules.IsSameOrSubPath(fullPath, archiveRoot) ||
                LauncherPathRules.PathsEqual(fullPath, archiveRoot))
            {
                return null;
            }

            DirectoryInfo? directory = new FileInfo(fullPath).Directory;

            while (directory?.Parent is DirectoryInfo parent &&
                   !LauncherPathRules.PathsEqual(parent.FullName, archiveRoot))
            {
                directory = parent;
            }

            return directory?.FullName;
        }

        private static void TryDeleteDirectoryTree(string directoryPath)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);
            }
            catch
            {
                // Si Windows mantiene algún archivo abierto, se limpiará más adelante.
            }
        }

        private static bool IsSupportedImage(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".bmp";
        }
    }
}