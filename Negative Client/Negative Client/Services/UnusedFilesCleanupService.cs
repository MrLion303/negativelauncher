using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class UnusedFilesCleanupService
    {
        private const string DeveloperInstanceId =
            "__developer_vanilla__";


        private readonly InstanceService
            _instanceService;


        public UnusedFilesCleanupService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;
        }


        // =====================================================
        // ANALIZAR
        // =====================================================

        public async Task<UnusedFilesCleanupPlan>
            AnalyzeAsync(
                LauncherPreferences preferences)
        {
            List<UnusedFilesCleanupEntry> entries =
                new();


            List<InstalledInstance> instances =
                await _instanceService
                    .LoadAllAsync();


            foreach (InstalledInstance instance in
                instances)
            {
                if (!instance.IsInstalled ||
                    !instance.RuntimePrepared)
                {
                    continue;
                }


                string instanceDirectory =
                    _instanceService
                        .GetInstanceDirectory(
                            instance.Id);


                AddObsoleteVersionDirectories(
                    entries,
                    instanceDirectory,
                    instance.MinecraftVersion,
                    instance.LaunchVersionName,
                    instance.Name);


                AddObsoleteJavaRuntimeDirectories(
                    entries,
                    instanceDirectory,
                    instance.MinecraftVersion,
                    instance.Name);
            }


            // La instancia DEV no se registra como un modpack normal,
            // pero también puede acumular versiones Vanilla antiguas.
            if (preferences.DeveloperMode &&
                !string.IsNullOrWhiteSpace(
                    preferences.DeveloperMinecraftVersion))
            {
                string developerDirectory =
                    _instanceService
                        .GetInstanceDirectory(
                            DeveloperInstanceId);


                if (Directory.Exists(
                        developerDirectory))
                {
                    AddObsoleteVersionDirectories(
                        entries,
                        developerDirectory,
                        preferences.DeveloperMinecraftVersion,
                        preferences.DeveloperMinecraftVersion,
                        "Minecraft Vanilla (DEV)");


                    AddObsoleteJavaRuntimeDirectories(
                        entries,
                        developerDirectory,
                        preferences.DeveloperMinecraftVersion,
                        "Minecraft Vanilla (DEV)");
                }
            }


            // Evitamos duplicados si dos comprobaciones apuntaran
            // accidentalmente a la misma carpeta.
            List<UnusedFilesCleanupEntry> distinctEntries =
                entries
                    .GroupBy(
                        entry =>
                            Path.GetFullPath(
                                entry.DirectoryPath),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(
                        group =>
                            group.First())
                    .OrderByDescending(
                        entry =>
                            entry.DirectoryPath.Length)
                    .ToList();


            return
                new UnusedFilesCleanupPlan(
                    distinctEntries);
        }


        // =====================================================
        // EJECUTAR
        // =====================================================

        public Task<UnusedFilesCleanupResult>
            ExecuteAsync(
                UnusedFilesCleanupPlan plan)
        {
            return
                Task.Run(
                    () =>
                    {
                        long freedBytes =
                            0;


                        int deletedEntries =
                            0;


                        int failedEntries =
                            0;


                        foreach (UnusedFilesCleanupEntry entry in
                            plan.Entries
                                .OrderByDescending(
                                    item =>
                                        item.DirectoryPath.Length))
                        {
                            try
                            {
                                if (!Directory.Exists(
                                        entry.DirectoryPath))
                                {
                                    continue;
                                }


                                Directory.Delete(
                                    entry.DirectoryPath,
                                    recursive: true);


                                freedBytes +=
                                    entry.SizeBytes;


                                deletedEntries++;
                            }
                            catch
                            {
                                failedEntries++;
                            }
                        }


                        return
                            new UnusedFilesCleanupResult(
                                freedBytes,
                                deletedEntries,
                                failedEntries);
                    });
        }


        // =====================================================
        // VERSIONES DE MINECRAFT
        // =====================================================

        private static void AddObsoleteVersionDirectories(
            List<UnusedFilesCleanupEntry> entries,
            string instanceDirectory,
            string minecraftVersion,
            string launchVersionName,
            string instanceName)
        {
            string versionsDirectory =
                Path.Combine(
                    instanceDirectory,
                    "versions");


            if (!Directory.Exists(
                    versionsDirectory))
            {
                return;
            }


            HashSet<string> keepVersions =
                new(
                    StringComparer.OrdinalIgnoreCase);


            if (!string.IsNullOrWhiteSpace(
                    minecraftVersion))
            {
                keepVersions.Add(
                    minecraftVersion.Trim());
            }


            if (!string.IsNullOrWhiteSpace(
                    launchVersionName))
            {
                keepVersions.Add(
                    launchVersionName.Trim());
            }


            if (keepVersions.Count ==
                0)
            {
                return;
            }


            foreach (string versionDirectory in
                Directory.EnumerateDirectories(
                    versionsDirectory))
            {
                string versionName =
                    Path.GetFileName(
                        versionDirectory);


                if (keepVersions.Contains(
                        versionName))
                {
                    continue;
                }


                long size =
                    CalculateDirectorySize(
                        versionDirectory);


                entries.Add(
                    new UnusedFilesCleanupEntry(
                        versionDirectory,
                        size,
                        instanceName,
                        $"Minecraft {versionName}"));
            }
        }


        // =====================================================
        // RUNTIMES DE JAVA
        // =====================================================

        private static void AddObsoleteJavaRuntimeDirectories(
            List<UnusedFilesCleanupEntry> entries,
            string instanceDirectory,
            string minecraftVersion,
            string instanceName)
        {
            string? requiredComponent =
                TryReadJavaComponent(
                    instanceDirectory,
                    minecraftVersion);


            // Si no podemos identificar con certeza el Java requerido,
            // no eliminamos ningún runtime. La limpieza debe ser segura.
            if (string.IsNullOrWhiteSpace(
                    requiredComponent))
            {
                return;
            }


            string runtimeDirectory =
                Path.Combine(
                    instanceDirectory,
                    "runtime");


            if (!Directory.Exists(
                    runtimeDirectory))
            {
                return;
            }


            foreach (string platformDirectory in
                Directory.EnumerateDirectories(
                    runtimeDirectory))
            {
                foreach (string componentDirectory in
                    Directory.EnumerateDirectories(
                        platformDirectory))
                {
                    string componentName =
                        Path.GetFileName(
                            componentDirectory);


                    if (string.Equals(
                            componentName,
                            requiredComponent,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }


                    long size =
                        CalculateDirectorySize(
                            componentDirectory);


                    entries.Add(
                        new UnusedFilesCleanupEntry(
                            componentDirectory,
                            size,
                            instanceName,
                            $"Java {componentName}"));
                }
            }
        }


        private static string? TryReadJavaComponent(
            string instanceDirectory,
            string minecraftVersion)
        {
            if (string.IsNullOrWhiteSpace(
                    minecraftVersion))
            {
                return null;
            }


            string versionJsonPath =
                Path.Combine(
                    instanceDirectory,
                    "versions",
                    minecraftVersion,
                    minecraftVersion +
                    ".json");


            if (!File.Exists(
                    versionJsonPath))
            {
                return null;
            }


            try
            {
                using FileStream stream =
                    File.OpenRead(
                        versionJsonPath);


                using JsonDocument document =
                    JsonDocument.Parse(
                        stream);


                if (!document.RootElement.TryGetProperty(
                        "javaVersion",
                        out JsonElement javaVersion))
                {
                    return null;
                }


                if (!javaVersion.TryGetProperty(
                        "component",
                        out JsonElement component))
                {
                    return null;
                }


                return
                    component.GetString();
            }
            catch
            {
                return null;
            }
        }


        // =====================================================
        // TAMAÑO
        // =====================================================

        private static long CalculateDirectorySize(
            string directory)
        {
            if (!Directory.Exists(
                    directory))
            {
                return 0;
            }


            long total =
                0;


            try
            {
                foreach (string file in
                    Directory.EnumerateFiles(
                        directory,
                        "*",
                        SearchOption.AllDirectories))
                {
                    try
                    {
                        total +=
                            new FileInfo(
                                file)
                                .Length;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }


            return total;
        }
    }


    public sealed class UnusedFilesCleanupPlan
    {
        public UnusedFilesCleanupPlan(
            IReadOnlyList<UnusedFilesCleanupEntry> entries)
        {
            Entries =
                entries;


            TotalBytes =
                entries.Sum(
                    entry =>
                        entry.SizeBytes);
        }


        public IReadOnlyList<UnusedFilesCleanupEntry> Entries
        {
            get;
        }


        public long TotalBytes
        {
            get;
        }
    }


    public sealed class UnusedFilesCleanupEntry
    {
        public UnusedFilesCleanupEntry(
            string directoryPath,
            long sizeBytes,
            string instanceName,
            string description)
        {
            DirectoryPath =
                directoryPath;


            SizeBytes =
                Math.Max(
                    0,
                    sizeBytes);


            InstanceName =
                instanceName;


            Description =
                description;
        }


        public string DirectoryPath
        {
            get;
        }


        public long SizeBytes
        {
            get;
        }


        public string InstanceName
        {
            get;
        }


        public string Description
        {
            get;
        }
    }


    public sealed class UnusedFilesCleanupResult
    {
        public UnusedFilesCleanupResult(
            long freedBytes,
            int deletedEntries,
            int failedEntries)
        {
            FreedBytes =
                Math.Max(
                    0,
                    freedBytes);


            DeletedEntries =
                Math.Max(
                    0,
                    deletedEntries);


            FailedEntries =
                Math.Max(
                    0,
                    failedEntries);
        }


        public long FreedBytes
        {
            get;
        }


        public int DeletedEntries
        {
            get;
        }


        public int FailedEntries
        {
            get;
        }
    }
}
