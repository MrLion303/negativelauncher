using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class InstanceService
    {
        public static string LauncherRoot { get; } =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "NegativeClient");


        public static string InstancesRoot { get; } =
            Path.Combine(
                LauncherRoot,
                "instances");


        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };


        public InstanceService()
        {
            Directory.CreateDirectory(
                LauncherRoot);

            Directory.CreateDirectory(
                InstancesRoot);
        }


        // =====================================================
        // RUTA DE UNA INSTANCIA
        // =====================================================

        public string GetInstanceDirectory(
            string instanceId)
        {
            ValidateInstanceId(instanceId);

            return Path.Combine(
                InstancesRoot,
                instanceId);
        }


        // =====================================================
        // CARGAR INSTANCIAS
        // =====================================================

        public async Task<List<InstalledInstance>>
            LoadAllAsync()
        {
            List<InstalledInstance> result =
                new();


            Directory.CreateDirectory(
                InstancesRoot);


            foreach (string directory in
                Directory.GetDirectories(InstancesRoot))
            {
                string instanceFile =
                    Path.Combine(
                        directory,
                        "instance.json");


                if (!File.Exists(instanceFile))
                {
                    continue;
                }


                try
                {
                    string json =
                        await File.ReadAllTextAsync(
                            instanceFile);


                    InstalledInstance? instance =
                        JsonSerializer.Deserialize<InstalledInstance>(
                            json,
                            _jsonOptions);


                    if (instance != null &&
                        !string.IsNullOrWhiteSpace(instance.Id))
                    {
                        result.Add(instance);
                    }
                }
                catch
                {
                    // Una instancia dañada no impide
                    // abrir el launcher.
                }
            }


            return result;
        }


        // =====================================================
        // GUARDAR INSTANCIA
        // =====================================================

        public async Task SaveAsync(
            InstalledInstance instance)
        {
            string directory =
                GetInstanceDirectory(
                    instance.Id);


            Directory.CreateDirectory(
                directory);


            string instanceFile =
                Path.Combine(
                    directory,
                    "instance.json");


            string json =
                JsonSerializer.Serialize(
                    instance,
                    _jsonOptions);


            await File.WriteAllTextAsync(
                instanceFile,
                json);
        }


        // =====================================================
        // SEGURIDAD DEL ID
        // =====================================================

        private static void ValidateInstanceId(
            string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new InvalidOperationException(
                    "La instancia no tiene ID.");
            }


            foreach (char character in instanceId)
            {
                bool valid =
                    char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_';


                if (!valid)
                {
                    throw new InvalidOperationException(
                        "El ID de la instancia contiene " +
                        "caracteres no permitidos.");
                }
            }
        }
    }
}