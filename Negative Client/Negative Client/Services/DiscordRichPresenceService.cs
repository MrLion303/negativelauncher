using DiscordRPC;
using DiscordRPC.Logging;

namespace Negative_Client.Services
{
    public sealed class DiscordRichPresenceService : IDisposable
    {
        private readonly DiscordRpcClient _client;
        private readonly DateTime _launcherStartedAt = DateTime.UtcNow;
        private bool _disposed;

        public DiscordRichPresenceService(string applicationId)
        {
            _client = new DiscordRpcClient(applicationId)
            {
                Logger = new ConsoleLogger
                {
                    Level = LogLevel.Warning
                }
            };

            try
            {
                _client.Initialize();
                UpdatePresence();
            }
            catch
            {
                // La ausencia de Discord no debe impedir abrir el launcher.
            }
        }

        public void UpdatePresence(string? instanceName = null, string? instanceAssetKey = null, bool gameRunning = false)
        {
            if (_disposed || !_client.IsInitialized)
            {
                return;
            }

            var presence = new RichPresence
            {
                Details = "NegativeClient",
                State = gameRunning
                    ? $"Jugando Minecraft • {instanceName ?? "Minecraft"}"
                    : string.IsNullOrWhiteSpace(instanceName)
                        ? "En el launcher"
                        : $"Preparando {instanceName}",
                Timestamps = new Timestamps
                {
                    Start = _launcherStartedAt
                },
                Assets = new Assets
                {
                    LargeImageKey = "home_icon",
                    LargeImageText = "NegativeClient",
                    SmallImageKey = string.IsNullOrWhiteSpace(instanceAssetKey) ? null : instanceAssetKey,
                    SmallImageText = string.IsNullOrWhiteSpace(instanceName) ? null : instanceName
                }
            };

            _client.SetPresence(presence);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (_client.IsInitialized)
                {
                    _client.ClearPresence();
                }
            }
            catch
            {
                // Discord puede cerrarse antes que el launcher.
            }

            _client.Dispose();
        }
    }
}