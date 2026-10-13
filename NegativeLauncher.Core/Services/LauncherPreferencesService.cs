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
            : this(LauncherPaths.DefaultLauncherRoot)
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
                    PreserveCorruptPreferencesFile();
                    LauncherPreferences defaults = new();
                    await TryRestoreDefaultPreferencesAsync(defaults);
                    return defaults;
                }

                Normalize(preferences);
                return preferences;
            }
            catch (JsonException)
            {
                PreserveCorruptPreferencesFile();
                LauncherPreferences defaults = new();
                await TryRestoreDefaultPreferencesAsync(defaults);
                return defaults;
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
            string temporaryPath = _preferencesFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                await File.WriteAllTextAsync(temporaryPath, json);
                File.Move(temporaryPath, _preferencesFilePath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // Un archivo temporal huérfano no debe ocultar el resultado del guardado.
                }
            }
        }

        private async Task TryRestoreDefaultPreferencesAsync(LauncherPreferences defaults)
        {
            try
            {
                await SaveAsync(defaults);
            }
            catch
            {
                // El launcher debe poder abrirse aunque la carpeta no permita escribir.
            }
        }

        private void PreserveCorruptPreferencesFile()
        {
            try
            {
                if (!File.Exists(_preferencesFilePath))
                {
                    return;
                }

                string backupPath = _preferencesFilePath + ".corrupt-" +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                    Guid.NewGuid().ToString("N");

                File.Move(_preferencesFilePath, backupPath);
            }
            catch
            {
                // La recuperación del launcher tiene prioridad; no se elimina el archivo original.
            }
        }

        private static void Normalize(LauncherPreferences preferences)
        {
            preferences.MaximumRamMb = Math.Clamp(preferences.MaximumRamMb, 1024, 32768);
            preferences.CustomJavaPath ??= string.Empty;
            preferences.CustomJavaArguments ??= string.Empty;
            preferences.DeveloperMinecraftVersion ??= string.Empty;
            preferences.StorageRootPath ??= string.Empty;
            preferences.ModpackCatalogFileId = string.IsNullOrWhiteSpace(preferences.ModpackCatalogFileId)
                ? LauncherPreferences.DefaultModpackCatalogFileId
                : preferences.ModpackCatalogFileId.Trim();
        }
    }
}
