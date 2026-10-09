using System.IO;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ScreenshotArchiveService
    {
        public static readonly string ArchiveRoot =
            Path.Combine(
                InstanceService.LauncherRoot,
                "archived-screenshots");

        private readonly ScreenshotArchiveStore _store =
            new ScreenshotArchiveStore(ArchiveRoot);

        public string ArchiveInstanceScreenshots(
            string instanceId,
            string instanceName,
            string screenshotsDirectory)
        {
            return _store.ArchiveInstanceScreenshots(
                instanceId,
                instanceName,
                screenshotsDirectory);
        }

        public ScreenshotArchiveInfo[] GetArchives()
        {
            return _store.GetArchives();
        }

        public void CleanupArchiveIfEmpty(string screenshotFilePath)
        {
            _store.CleanupArchiveIfEmpty(screenshotFilePath);
        }
    }
}