using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Negative_Client.Services
{
    public sealed class LauncherUpdateService
    {
        private readonly UpdateManager _manager;

        public LauncherUpdateService()
        {
            _manager = new UpdateManager(
                new GithubSource(
                    "https://github.com/MrLion303/negativelauncher",
                    accessToken: null,
                    prerelease: false));
        }

        public bool IsInstalled => _manager.IsInstalled;

        public string CurrentVersion =>
            _manager.CurrentVersion?.ToString() ?? "desconocida";

        public Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (!_manager.IsInstalled)
            {
                return Task.FromResult<UpdateInfo?>(null);
            }

            return _manager.CheckForUpdatesAsync();
        }

        public Task DownloadUpdatesAsync(
            UpdateInfo update,
            Action<int>? progress = null)
        {
            if (!_manager.IsInstalled)
            {
                throw new InvalidOperationException(
                    "Las actualizaciones están disponibles después de instalar Negative Client con el instalador.");
            }

            return _manager.DownloadUpdatesAsync(update, progress);
        }

        public void ApplyAndRestart(UpdateInfo update)
        {
            if (!_manager.IsInstalled)
            {
                throw new InvalidOperationException(
                    "Negative Client no se ha instalado con el actualizador.");
            }

            _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
        }
    }
}
