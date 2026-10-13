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


        // Los autoguardados pueden solaparse con el cierre de la ventana. Serializar escrituras
        // evita que dos operaciones compitan por el mismo archivo de preferencias.
        private static readonly System.Threading.SemaphoreSlim SaveGate = new(1, 1);


        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };


        public async Task<LauncherPreferences> LoadAsync()
        {
            if (!File.Exists(PreferencesFilePath))
            {
                LauncherPreferences defaults = new LauncherPreferences();
                await SaveAsync(defaults);
                return defaults;
            }

            // Un error de acceso al disco no significa que el JSON esté dañado:
            // no mover ni reemplazar el archivo si simplemente no se puede leer.
            string json = await File.ReadAllTextAsync(PreferencesFilePath);
            LauncherPreferences? preferences;
            try
            {
                preferences = JsonSerializer.Deserialize<LauncherPreferences>(json, _jsonOptions);
                if (preferences is null)
                    throw new JsonException("El archivo de preferencias no contiene un objeto válido.");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                LauncherDiagnostics.WriteException(
                    "LauncherPreferencesService.LoadAsync: el JSON de preferencias no es válido.",
                    ex);

                // Conservar el archivo dañado antes de restaurar valores predeterminados.
                // Si no se puede crear la copia, no sobrescribir el único original.
                string backupPath = PreferencesFilePath + ".corrupt-" +
                    DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

                try
                {
                    File.Move(PreferencesFilePath, backupPath);
                }
                catch (Exception backupException)
                {
                    LauncherDiagnostics.WriteException(
                        "LauncherPreferencesService.LoadAsync: no se pudo conservar la copia dañada.",
                        backupException);
                    return new LauncherPreferences();
                }

                LauncherPreferences defaults = new LauncherPreferences();
                try
                {
                    await SaveAsync(defaults);
                }
                catch (Exception saveException)
                {
                    LauncherDiagnostics.WriteException(
                        "LauncherPreferencesService.LoadAsync: no se pudieron restaurar las preferencias predeterminadas.",
                        saveException);
                }

                return defaults;
            }

            // Normalizar solo después de deserializar correctamente.
            preferences.MaximumRamMb = Math.Clamp(preferences.MaximumRamMb, 1024, 32768);
            preferences.CustomJavaPath ??= string.Empty;
            preferences.CustomJavaArguments ??= string.Empty;
            preferences.DeveloperMinecraftVersion ??= string.Empty;
            preferences.StorageRootPath ??= string.Empty;
            return preferences;
        }


        public async Task SaveAsync(
            LauncherPreferences preferences)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            await SaveGate.WaitAsync();

            string? temporaryPath = null;
            try
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

                preferences.DeveloperMinecraftVersion ??=
                    string.Empty;

                preferences.StorageRootPath ??=
                    string.Empty;

                string json =
                    JsonSerializer.Serialize(
                        preferences,
                        _jsonOptions);

                // Cada escritura tiene su propio temporal; el reemplazo final es atómico.
                temporaryPath =
                    PreferencesFilePath + "." +
                    Guid.NewGuid().ToString("N") + ".tmp";

                await File.WriteAllTextAsync(
                    temporaryPath,
                    json);

                File.Move(
                    temporaryPath,
                    PreferencesFilePath,
                    overwrite: true);
            }
            finally
            {
                try
                {
                    if (temporaryPath is not null && File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                    // Un temporal huérfano no debe ocultar el error real del guardado.
                }
                finally
                {
                    SaveGate.Release();
                }
            }
        }
    }
}
