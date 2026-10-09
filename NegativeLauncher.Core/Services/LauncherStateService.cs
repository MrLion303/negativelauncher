using System.Text.Json;

namespace Negative_Client.Services
{
    public sealed class LauncherStateService
    {
        private readonly string _launcherRoot;
        private readonly string _stateFilePath;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private sealed class LauncherStateData
        {
            public string LastPlayedInstanceId { get; set; } = string.Empty;
        }

        public LauncherStateService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NegativeClient"))
        {
        }

        public LauncherStateService(string launcherRoot)
        {
            if (string.IsNullOrWhiteSpace(launcherRoot))
            {
                throw new ArgumentException("La carpeta de datos no puede estar vacía.", nameof(launcherRoot));
            }

            _launcherRoot = Path.GetFullPath(launcherRoot);
            _stateFilePath = Path.Combine(_launcherRoot, "launcher-state.json");
        }

        public async Task<string?> GetLastPlayedInstanceIdAsync()
        {
            try
            {
                if (!File.Exists(_stateFilePath))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(_stateFilePath);
                LauncherStateData? data =
                    JsonSerializer.Deserialize<LauncherStateData>(json, _jsonOptions);

                if (data == null || string.IsNullOrWhiteSpace(data.LastPlayedInstanceId))
                {
                    return null;
                }

                return data.LastPlayedInstanceId;
            }
            catch
            {
                // Un estado dañado no debe impedir que abra el launcher.
                return null;
            }
        }

        public async Task SetLastPlayedInstanceIdAsync(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException("El ID de la instancia está vacío.", nameof(instanceId));
            }

            await SaveStateAsync(new LauncherStateData
            {
                LastPlayedInstanceId = instanceId
            });
        }

        public async Task ClearLastPlayedInstanceAsync()
        {
            await SaveStateAsync(new LauncherStateData
            {
                LastPlayedInstanceId = string.Empty
            });
        }

        private async Task SaveStateAsync(LauncherStateData data)
        {
            Directory.CreateDirectory(_launcherRoot);

            string json = JsonSerializer.Serialize(data, _jsonOptions);
            string temporaryPath = _stateFilePath + ".tmp";

            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, _stateFilePath, overwrite: true);
        }
    }
}
