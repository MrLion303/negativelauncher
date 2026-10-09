using System.Text.Json;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class LauncherPreferencesService
    {
        private readonly string _launcherRoot;
        private readonly string _preferencesFilePath;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public LauncherPreferencesService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NegativeClient"))
        {
        }

        public LauncherPreferencesService(string launcherRoot)
        {
            if (string.IsNullOrWhiteSpace(launcherRoot))
            {
                throw new ArgumentException("La carpeta de datos no puede estar vacía.", nameof(launcherRoot));
            }

            _launcherRoot = Path.GetFullPath(launcherRoot);
            _preferencesFilePath = Path.Combine(_launcherRoot, "launcher-preferences.json");
        }

        public async Task<LauncherPreferences> LoadAsync()
        {
            try
            {
                if (!File.Exists(_preferencesFilePath))
                {
                    LauncherPreferences defaults = new();
                    await SaveAsync(defaults);
                    return defaults;
                }

                string json = await File.ReadAllTextAsync(_preferencesFilePath);
                LauncherPreferences? preferences =
                    JsonSerializer.Deserialize<LauncherPreferences>(json, _jsonOptions);

                if (preferences == null)
                {
                    return new LauncherPreferences();
                }

                Normalize(preferences);
                return preferences;
            }
            catch
            {
                return new LauncherPreferences();
            }
        }

        public async Task SaveAsync(LauncherPreferences preferences)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            Directory.CreateDirectory(_launcherRoot);
            Normalize(preferences);

            string json = JsonSerializer.Serialize(preferences, _jsonOptions);
            string temporaryPath = _preferencesFilePath + ".tmp";

            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, _preferencesFilePath, overwrite: true);
        }

        private static void Normalize(LauncherPreferences preferences)
        {
            preferences.MaximumRamMb = Math.Clamp(preferences.MaximumRamMb, 1024, 32768);
            preferences.CustomJavaPath ??= string.Empty;
            preferences.CustomJavaArguments ??= string.Empty;
            preferences.DeveloperMinecraftVersion ??= string.Empty;
            preferences.StorageRootPath ??= string.Empty;
        }
    }
}
