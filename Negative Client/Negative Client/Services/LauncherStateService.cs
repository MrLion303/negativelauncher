using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class LauncherStateService
    {
        private static readonly string StateFilePath =
            Path.Combine(
                InstanceService.LauncherRoot,
                "launcher-state.json");


        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };


        private sealed class LauncherStateData
        {
            public string LastPlayedInstanceId { get; set; } =
                string.Empty;
        }


        // =====================================================
        // LEER ÚLTIMA INSTANCIA JUGADA
        // =====================================================

        public async Task<string?> GetLastPlayedInstanceIdAsync()
        {
            try
            {
                if (!File.Exists(
                        StateFilePath))
                {
                    return null;
                }

                string json =
                    await File.ReadAllTextAsync(
                        StateFilePath);

                LauncherStateData? data =
                    JsonSerializer.Deserialize<LauncherStateData>(
                        json,
                        _jsonOptions);

                if (data == null ||
                    string.IsNullOrWhiteSpace(
                        data.LastPlayedInstanceId))
                {
                    return null;
                }

                return
                    data.LastPlayedInstanceId;
            }
            catch
            {
                // Un archivo de estado dañado no debe impedir
                // abrir el launcher.
                return null;
            }
        }


        // =====================================================
        // GUARDAR ÚLTIMA INSTANCIA JUGADA
        // =====================================================

        public async Task SetLastPlayedInstanceIdAsync(
            string instanceId)
        {
            if (string.IsNullOrWhiteSpace(
                    instanceId))
            {
                throw new ArgumentException(
                    "El ID de la instancia está vacío.",
                    nameof(instanceId));
            }

            Directory.CreateDirectory(
                InstanceService.LauncherRoot);

            LauncherStateData data =
                new()
                {
                    LastPlayedInstanceId =
                        instanceId
                };

            string json =
                JsonSerializer.Serialize(
                    data,
                    _jsonOptions);

            string temporaryPath =
                StateFilePath +
                ".tmp";

            await File.WriteAllTextAsync(
                temporaryPath,
                json);

            File.Move(
                temporaryPath,
                StateFilePath,
                overwrite: true);
        }


        public async Task ClearLastPlayedInstanceAsync()
        {
            Directory.CreateDirectory(
                InstanceService.LauncherRoot);


            LauncherStateData data =
                new()
                {
                    LastPlayedInstanceId =
                        string.Empty
                };


            string json =
                JsonSerializer.Serialize(
                    data,
                    _jsonOptions);


            string temporaryPath =
                StateFilePath +
                ".tmp";


            await File.WriteAllTextAsync(
                temporaryPath,
                json);


            File.Move(
                temporaryPath,
                StateFilePath,
                overwrite: true);
        }
    }
}
