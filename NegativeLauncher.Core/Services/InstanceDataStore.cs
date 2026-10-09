using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    /// <summary>
    /// Lee y guarda los archivos instance.json sin depender de WPF.
    /// </summary>
    public sealed class InstanceDataStore
    {
        private readonly Func<string> _instancesRootProvider;
        private readonly JsonSerializerOptions _jsonOptions;

        public InstanceDataStore(
            Func<string> instancesRootProvider,
            JsonSerializerOptions? jsonOptions = null)
        {
            _instancesRootProvider = instancesRootProvider ??
                throw new ArgumentNullException(nameof(instancesRootProvider));
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        public string GetInstanceDirectory(string instanceId)
        {
            LauncherPathRules.ValidateInstanceId(instanceId);
            return Path.Combine(_instancesRootProvider(), instanceId);
        }

        public async Task<List<InstalledInstance>> LoadAllAsync()
        {
            List<InstalledInstance> result = new();
            string instancesRoot = _instancesRootProvider();
            Directory.CreateDirectory(instancesRoot);

            foreach (string directory in Directory.GetDirectories(instancesRoot))
            {
                string instanceFile = Path.Combine(directory, "instance.json");
                if (!File.Exists(instanceFile))
                {
                    continue;
                }

                try
                {
                    string json = await File.ReadAllTextAsync(instanceFile);
                    InstalledInstance? instance =
                        JsonSerializer.Deserialize<InstalledInstance>(json, _jsonOptions);

                    if (instance != null && !string.IsNullOrWhiteSpace(instance.Id))
                    {
                        result.Add(instance);
                    }
                }
                catch
                {
                    // Un archivo de instancia inválido no debe bloquear la carga de las demás.
                }
            }

            return result;
        }

        public async Task SaveAsync(InstalledInstance instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            string directory = GetInstanceDirectory(instance.Id);
            Directory.CreateDirectory(directory);

            string instanceFile = Path.Combine(directory, "instance.json");
            string json = JsonSerializer.Serialize(instance, _jsonOptions);
            await File.WriteAllTextAsync(instanceFile, json);
        }
    }
}
