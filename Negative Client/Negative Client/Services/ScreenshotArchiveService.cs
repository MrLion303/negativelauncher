using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ScreenshotArchiveService
    {
        public static readonly string ArchiveRoot =
            Path.Combine(
                InstanceService.LauncherRoot,
                "archived-screenshots");


        private static readonly JsonSerializerOptions
            JsonOptions =
                new()
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };


        public string ArchiveInstanceScreenshots(
            string instanceId,
            string instanceName,
            string screenshotsDirectory)
        {
            if (!Directory.Exists(
                    screenshotsDirectory))
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


            Directory.CreateDirectory(
                ArchiveRoot);


            string folderName =
                $"{instanceId}_{DateTime.Now:yyyyMMddHHmmss}";


            string archiveDirectory =
                Path.Combine(
                    ArchiveRoot,
                    folderName);


            Directory.CreateDirectory(
                archiveDirectory);


            string targetScreenshotsDirectory =
                Path.Combine(
                    archiveDirectory,
                    "screenshots");


            Directory.Move(
                screenshotsDirectory,
                targetScreenshotsDirectory);


            ScreenshotArchiveInfo info =
                new()
                {
                    InstanceId =
                        instanceId,

                    InstanceName =
                        instanceName,

                    ScreenshotsDirectory =
                        targetScreenshotsDirectory
                };


            string metadataPath =
                Path.Combine(
                    archiveDirectory,
                    "archive-info.json");


            string json =
                JsonSerializer.Serialize(
                    info,
                    JsonOptions);


            File.WriteAllText(
                metadataPath,
                json);


            return targetScreenshotsDirectory;
        }


        public ScreenshotArchiveInfo[] GetArchives()
        {
            if (!Directory.Exists(
                    ArchiveRoot))
            {
                return Array.Empty<ScreenshotArchiveInfo>();
            }


            return Directory
                .EnumerateFiles(
                    ArchiveRoot,
                    "archive-info.json",
                    SearchOption.AllDirectories)
                .Select(
                    filePath =>
                    {
                        try
                        {
                            string json =
                                File.ReadAllText(
                                    filePath);

                            return JsonSerializer.Deserialize<ScreenshotArchiveInfo>(
                                json,
                                JsonOptions);
                        }
                        catch
                        {
                            return null;
                        }
                    })
                .Where(
                    archive =>
                        archive != null &&
                        !string.IsNullOrWhiteSpace(
                            archive.ScreenshotsDirectory))
                .Cast<ScreenshotArchiveInfo>()
                .ToArray();
        }


        public void CleanupArchiveIfEmpty(
            string screenshotFilePath)
        {
            string? archiveDirectory =
                GetArchiveContainerDirectory(
                    screenshotFilePath);


            if (string.IsNullOrWhiteSpace(
                    archiveDirectory) ||
                !Directory.Exists(
                    archiveDirectory))
            {
                return;
            }


            string screenshotsDirectory =
                Path.Combine(
                    archiveDirectory,
                    "screenshots");


            if (!Directory.Exists(
                    screenshotsDirectory))
            {
                TryDeleteDirectoryTree(
                    archiveDirectory);

                return;
            }


            bool hasAnyImages =
                Directory.EnumerateFiles(
                    screenshotsDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .Any(IsSupportedImage);


            if (!hasAnyImages)
            {
                TryDeleteDirectoryTree(
                    archiveDirectory);
            }
        }


        private static string? GetArchiveContainerDirectory(
            string screenshotFilePath)
        {
            string fullPath =
                Path.GetFullPath(
                    screenshotFilePath);

            string archiveRoot =
                Path.GetFullPath(
                    ArchiveRoot);

            if (!fullPath.StartsWith(
                    archiveRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }


            DirectoryInfo? directory =
                new FileInfo(
                    fullPath)
                    .Directory;


            while (directory != null &&
                   !string.Equals(
                       directory.Parent?.FullName,
                       archiveRoot,
                       StringComparison.OrdinalIgnoreCase))
            {
                directory =
                    directory.Parent;
            }


            return directory?.FullName;
        }


        private static void TryDeleteDirectoryTree(
            string directoryPath)
        {
            try
            {
                Directory.Delete(
                    directoryPath,
                    recursive: true);
            }
            catch
            {
                // Ignorar bloqueo eventual de miniaturas
            }
        }


        private static bool IsSupportedImage(
            string filePath)
        {
            string extension =
                Path.GetExtension(
                    filePath)
                .ToLowerInvariant();

            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".bmp";
        }
    }
}
