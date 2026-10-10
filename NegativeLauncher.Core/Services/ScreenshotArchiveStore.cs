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

        public ScreenshotArchiveInfo[] GetArchives()
        {
            if (!Directory.Exists(_archiveRoot))
            {
                return Array.Empty<ScreenshotArchiveInfo>();
            }

            return Directory
                .EnumerateFiles(_archiveRoot, "archive-info.json", SearchOption.AllDirectories)
                .Select(filePath =>
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        return JsonSerializer.Deserialize<ScreenshotArchiveInfo>(json, JsonOptions);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(archive =>
                    archive != null &&
                    !string.IsNullOrWhiteSpace(archive.ScreenshotsDirectory))
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