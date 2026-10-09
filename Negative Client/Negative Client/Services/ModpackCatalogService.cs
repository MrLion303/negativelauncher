using System.Threading;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ModpackCatalogService
    {
        private const string CatalogFileId = "14PLsvXVZMzhsDWW9i0RKTT143qnMDk2Y";

        private readonly GoogleDriveService _driveService;
        private readonly ModpackCatalogReader _catalogReader;

        public ModpackCatalogService()
        {
            _driveService = new GoogleDriveService();
            _catalogReader = new ModpackCatalogReader();
        }

        public Task<ModpackManifest?> FindByCodeAsync(string installCode)
        {
            return _catalogReader.FindByCodeAsync(
                installCode,
                CatalogFileId,
                (fileId, cancellationToken) =>
                    _driveService.DownloadTextFileAsync(fileId, cancellationToken),
                CancellationToken.None);
        }
    }
}