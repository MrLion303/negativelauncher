using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ScreenshotGalleryService
    {
        private readonly InstanceService
            _instanceService;


        private readonly ScreenshotArchiveService
            _archiveService =
                new ScreenshotArchiveService();


        public ScreenshotGalleryService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;
        }


        public Task<List<ScreenshotGalleryItem>>
            LoadAllAsync(
                IEnumerable<InstalledInstance> instances)
        {
            InstalledInstance[] snapshot =
                instances.ToArray();


            return Task.Run(
                () =>
                    ScanScreenshots(
                        snapshot));
        }


        private List<ScreenshotGalleryItem>
            ScanScreenshots(
                IEnumerable<InstalledInstance> instances)
        {
            List<ScreenshotGalleryItem> result =
                new();


            foreach (InstalledInstance instance in
                instances)
            {
                string screenshotsDirectory =
                    Path.Combine(
                        _instanceService
                            .GetInstanceDirectory(
                                instance.Id),
                        "screenshots");


                AddItemsFromScreenshotsDirectory(
                    result,
                    screenshotsDirectory,
                    instance.Id,
                    instance.Name);
            }


            foreach (ScreenshotArchiveInfo archive in
                _archiveService
                    .GetArchives())
            {
                AddItemsFromScreenshotsDirectory(
                    result,
                    archive.ScreenshotsDirectory,
                    archive.InstanceId,
                    archive.InstanceName);
            }


            return result
                .OrderByDescending(
                    item =>
                        item.CapturedAt)
                .ToList();
        }


        private void AddItemsFromScreenshotsDirectory(
            List<ScreenshotGalleryItem> result,
            string screenshotsDirectory,
            string instanceId,
            string instanceName)
        {
            if (!Directory.Exists(
                    screenshotsDirectory))
            {
                return;
            }


            IEnumerable<string> files;


            try
            {
                files =
                    Directory.EnumerateFiles(
                        screenshotsDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .ToArray();
            }
            catch
            {
                return;
            }


            foreach (string filePath in
                files)
            {
                if (!IsSupportedImage(
                        filePath))
                {
                    continue;
                }


                try
                {
                    DateTime capturedAt =
                        File.GetLastWriteTime(
                            filePath);

                    BitmapSource thumbnail =
                        LoadBitmap(
                            filePath,
                            decodePixelWidth: 300);

                    result.Add(
                        new ScreenshotGalleryItem
                        {
                            FilePath =
                                filePath,

                            FileName =
                                Path.GetFileName(
                                    filePath),

                            InstanceId =
                                instanceId,

                            InstanceName =
                                instanceName,

                            CapturedAt =
                                capturedAt,

                            Thumbnail =
                                thumbnail
                        });
                }
                catch
                {
                    // Ignorar archivos dañados
                }
            }
        }


        public BitmapSource LoadFullImage(
            string filePath)
        {
            return
                LoadBitmap(
                    filePath,
                    decodePixelWidth: 0);
        }


        public bool Delete(
            string filePath)
        {
            try
            {
                bool exists =
                    File.Exists(
                        filePath);

                if (!exists)
                {
                    return false;
                }

                File.Delete(
                    filePath);

                _archiveService
                    .CleanupArchiveIfEmpty(
                        filePath);

                return true;
            }
            catch
            {
                return false;
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


        private static BitmapSource LoadBitmap(
            string filePath,
            int decodePixelWidth)
        {
            BitmapImage bitmap =
                new();

            bitmap.BeginInit();

            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;

            bitmap.CreateOptions =
                BitmapCreateOptions.IgnoreColorProfile;

            if (decodePixelWidth > 0)
            {
                bitmap.DecodePixelWidth =
                    decodePixelWidth;
            }

            bitmap.UriSource =
                new Uri(
                    Path.GetFullPath(
                        filePath),
                    UriKind.Absolute);

            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
    }
}
