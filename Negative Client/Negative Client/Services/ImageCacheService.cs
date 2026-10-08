using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class ImageCacheService
    {
        private readonly GoogleDriveService _driveService;

        public static string CacheRoot { get; } =
            Path.Combine(
                InstanceService.LauncherRoot,
                "cache");


        public ImageCacheService(
            GoogleDriveService driveService)
        {
            _driveService =
                driveService;

            Directory.CreateDirectory(
                CacheRoot);
        }


        // =====================================================
        // ICONO
        // =====================================================

        public Task<string?> GetIconPathAsync(
            string instanceId,
            string fileId)
        {
            return GetImagePathAsync(
                instanceId,
                "icon",
                fileId);
        }


        // =====================================================
        // FONDO
        // =====================================================

        public Task<string?> GetBackgroundPathAsync(
            string instanceId,
            string fileId)
        {
            return GetImagePathAsync(
                instanceId,
                "background",
                fileId);
        }


        // =====================================================
        // CACHE
        // =====================================================

        private async Task<string?> GetImagePathAsync(
            string instanceId,
            string imageType,
            string fileId)
        {
            if (string.IsNullOrWhiteSpace(
                    instanceId) ||
                string.IsNullOrWhiteSpace(
                    fileId))
            {
                return null;
            }


            string safeInstanceId =
                SanitizeFileName(
                    instanceId);


            string safeFileId =
                SanitizeFileName(
                    fileId);


            string directory =
                Path.Combine(
                    CacheRoot,
                    safeInstanceId);


            Directory.CreateDirectory(
                directory);


            string filePath =
                Path.Combine(
                    directory,
                    $"{imageType}-{safeFileId}.img");


            if (File.Exists(filePath))
            {
                FileInfo info =
                    new FileInfo(filePath);

                if (info.Length > 0)
                {
                    return filePath;
                }

                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                }
            }


            string tempPath =
                filePath +
                ".tmp";


            try
            {
                await _driveService.DownloadFileAsync(
                    fileId,
                    tempPath);


                File.Move(
                    tempPath,
                    filePath,
                    overwrite: true);


                return filePath;
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }

                return null;
            }
        }


        private static string SanitizeFileName(
            string value)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();


            string safe =
                new string(
                    value
                        .Where(character =>
                            !invalid.Contains(character))
                        .ToArray());


            if (string.IsNullOrWhiteSpace(
                    safe))
            {
                return
                    Guid.NewGuid()
                        .ToString("N");
            }


            return safe;
        }
    }
}
