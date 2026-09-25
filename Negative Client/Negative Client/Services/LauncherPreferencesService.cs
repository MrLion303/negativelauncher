using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class LauncherPreferencesService
    {
        private static readonly string PreferencesFilePath =
            Path.Combine(
                InstanceService.LauncherRoot,
                "launcher-preferences.json");


        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };


        public async Task<LauncherPreferences> LoadAsync()
        {
            try
            {
                if (!File.Exists(
                        PreferencesFilePath))
                {
                    LauncherPreferences defaults =
                        new LauncherPreferences();


                    await SaveAsync(
                        defaults);


                    return defaults;
                }


                string json =
                    await File.ReadAllTextAsync(
                        PreferencesFilePath);


                LauncherPreferences? preferences =
                    JsonSerializer.Deserialize<LauncherPreferences>(
                        json,
                        _jsonOptions);


                if (preferences == null)
                {
                    return
                        new LauncherPreferences();
                }


                preferences.MaximumRamMb =
                    Math.Clamp(
                        preferences.MaximumRamMb,
                        1024,
                        32768);


                preferences.CustomJavaPath ??=
                    string.Empty;


                preferences.CustomJavaArguments ??=
                    string.Empty;


                return preferences;
            }
            catch
            {
                return
                    new LauncherPreferences();
            }
        }


        public async Task SaveAsync(
            LauncherPreferences preferences)
        {
            Directory.CreateDirectory(
                InstanceService.LauncherRoot);


            preferences.MaximumRamMb =
                Math.Clamp(
                    preferences.MaximumRamMb,
                    1024,
                    32768);


            preferences.CustomJavaPath ??=
                string.Empty;


            preferences.CustomJavaArguments ??=
                string.Empty;


            string json =
                JsonSerializer.Serialize(
                    preferences,
                    _jsonOptions);


            string temporaryPath =
                PreferencesFilePath +
                ".tmp";


            await File.WriteAllTextAsync(
                temporaryPath,
                json);


            File.Move(
                temporaryPath,
                PreferencesFilePath,
                overwrite: true);
        }
    }
}
