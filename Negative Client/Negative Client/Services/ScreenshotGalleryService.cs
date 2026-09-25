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


                if (!Directory.Exists(
                        screenshotsDirectory))
                {
                    continue;
                }


                IEnumerable<string> files;


                try
                {
                    // AllDirectories también contempla carpetas que
                    // algunos mods de capturas creen dentro de screenshots.
                    files =
                        Directory.EnumerateFiles(
                            screenshotsDirectory,
                            "*",
                            SearchOption.AllDirectories)
                        .ToArray();
                }
                catch
                {
                    continue;
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
                                    instance.Id,

                                InstanceName =
                                    instance.Name,

                                CapturedAt =
                                    capturedAt,

                                Thumbnail =
                                    thumbnail
                            });
                    }
                    catch
                    {
                        // Una imagen dañada no impide abrir la galería.
                    }
                }
            }


            return result
                .OrderByDescending(
                    item =>
                        item.CapturedAt)
                .ToList();
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
                if (File.Exists(
                        filePath))
                {
                    File.Delete(
                        filePath);
                }


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


            return
                extension == ".png" ||
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
