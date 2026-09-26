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


        private readonly SharedMinecraftStorageService
            _sharedMinecraftStorageService;


        public UnusedFilesCleanupService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;


            _sharedMinecraftStorageService =
                new SharedMinecraftStorageService(
                    instanceService);
        }


        // =====================================================
        // ANALIZAR
        // =====================================================

        public async Task<UnusedFilesCleanupPlan>
            AnalyzeAsync(
                LauncherPreferences preferences)
        {
            /*
             * Primero migramos archivos antiguos desde cada instancia.
             * Así el botón LIMPIAR nunca borra una copia vieja antes de
             * que exista su equivalente en el almacenamiento compartido.
             */
            await _sharedMinecraftStorageService
                .MigrateAllLegacyRuntimeAsync();


            List<UnusedFilesCleanupEntry> entries =
                new();


            List<InstalledInstance> instances =
                await _instanceService
                    .LoadAllAsync();


            HashSet<string> requiredVersionFolders =
                new(
                    StringComparer.OrdinalIgnoreCase);


            HashSet<string> requiredMinecraftBaseVersions =
                new(
                    StringComparer.OrdinalIgnoreCase);


            foreach (InstalledInstance instance in
                instances)
            {
                if (!instance.IsInstalled)
                {
                    continue;
                }


                AddRequiredVersions(
                    requiredVersionFolders,
                    requiredMinecraftBaseVersions,
                    instance);
            }


            // La instancia DEV no se guarda como modpack normal. Mientras
            // el modo desarrollador esté activo conservamos la Vanilla
            // seleccionada actualmente.
            if (preferences.DeveloperMode &&
                !string.IsNullOrWhiteSpace(
                    preferences.DeveloperMinecraftVersion))
            {
                string developerVersion =
                    preferences.DeveloperMinecraftVersion
                        .Trim();


                requiredVersionFolders.Add(
                    developerVersion);


                requiredMinecraftBaseVersions.Add(
                    developerVersion);
            }


            AddUnusedSharedVersionDirectories(
                entries,
                requiredVersionFolders);


            AddUnusedSharedJavaRuntimeDirectories(
                entries,
                requiredMinecraftBaseVersions);


            /*
             * Las versiones anteriores del launcher guardaban estos
             * directorios dentro de cada instancia. Después de migrarlos
             * al almacén compartido son duplicados seguros de eliminar.
             */
            AddLegacyPerInstanceRuntimeDirectories(
                entries);


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


        private static void AddRequiredVersions(
            HashSet<string> requiredVersionFolders,
            HashSet<string> requiredMinecraftBaseVersions,
            InstalledInstance instance)
        {
            if (!string.IsNullOrWhiteSpace(
                    instance.MinecraftVersion))
            {
                string minecraftVersion =
                    instance.MinecraftVersion
                        .Trim();


                requiredVersionFolders.Add(
                    minecraftVersion);


                requiredMinecraftBaseVersions.Add(
                    minecraftVersion);
            }


            if (!string.IsNullOrWhiteSpace(
                    instance.LaunchVersionName))
            {
                requiredVersionFolders.Add(
                    instance.LaunchVersionName
                        .Trim());


                return;
            }


            // Una instancia instalada podría todavía no haber guardado
            // LaunchVersionName. Para Forge podemos proteger su nombre
            // esperado igualmente.
            if (string.Equals(
                    instance.Loader,
                    "forge",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(
                    instance.MinecraftVersion) &&
                !string.IsNullOrWhiteSpace(
                    instance.LoaderVersion))
            {
                requiredVersionFolders.Add(
                    instance.MinecraftVersion.Trim() +
                    "-forge-" +
                    instance.LoaderVersion.Trim());
            }
        }


        // =====================================================
        // VERSIONES COMPARTIDAS DE MINECRAFT
        // =====================================================

        private static void AddUnusedSharedVersionDirectories(
            List<UnusedFilesCleanupEntry> entries,
            HashSet<string> requiredVersionFolders)
        {
            string versionsDirectory =
                InstanceService
                    .SharedMinecraftVersionsRoot;


            if (!Directory.Exists(
                    versionsDirectory))
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


                if (requiredVersionFolders.Contains(
                        versionName))
                {
                    continue;
                }


                entries.Add(
                    new UnusedFilesCleanupEntry(
                        versionDirectory,
                        CalculateDirectorySize(
                            versionDirectory),
                        "Minecraft compartido",
                        $"Minecraft {versionName}"));
            }
        }


        // =====================================================
        // JAVA COMPARTIDO
        // =====================================================

        private static void AddUnusedSharedJavaRuntimeDirectories(
            List<UnusedFilesCleanupEntry> entries,
            HashSet<string> requiredMinecraftBaseVersions)
        {
            string runtimeDirectory =
                InstanceService
                    .SharedMinecraftRuntimeRoot;


            if (!Directory.Exists(
                    runtimeDirectory))
            {
                return;
            }


            HashSet<string> requiredComponents =
                new(
                    StringComparer.OrdinalIgnoreCase);


            bool canDetermineAllRequiredComponents =
                true;


            foreach (string minecraftVersion in
                requiredMinecraftBaseVersions)
            {
                string? component =
                    TryReadJavaComponent(
                        minecraftVersion);


                /*
                 * Si una versión activa no permite identificar de forma
                 * segura su Java, no eliminamos ningún runtime compartido.
                 */
                if (string.IsNullOrWhiteSpace(
                        component))
                {
                    canDetermineAllRequiredComponents =
                        false;

                    break;
                }


                requiredComponents.Add(
                    component);
            }


            if (!canDetermineAllRequiredComponents)
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


                    if (requiredComponents.Contains(
                            componentName))
                    {
                        continue;
                    }


                    entries.Add(
                        new UnusedFilesCleanupEntry(
                            componentDirectory,
                            CalculateDirectorySize(
                                componentDirectory),
                            "Minecraft compartido",
                            $"Java {componentName}"));
                }
            }
        }


        private static string? TryReadJavaComponent(
            string minecraftVersion)
        {
            if (string.IsNullOrWhiteSpace(
                    minecraftVersion))
            {
                return null;
            }


            string versionJsonPath =
                Path.Combine(
                    InstanceService
                        .SharedMinecraftVersionsRoot,
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
        // DUPLICADOS DE INSTALACIONES ANTIGUAS
        // =====================================================

        private static void AddLegacyPerInstanceRuntimeDirectories(
            List<UnusedFilesCleanupEntry> entries)
        {
            if (!Directory.Exists(
                    InstanceService.InstancesRoot))
            {
                return;
            }


            string[] legacyDirectoryNames =
            {
                "assets",
                "libraries",
                "versions",
                "runtime"
            };


            foreach (string instanceDirectory in
                Directory.EnumerateDirectories(
                    InstanceService.InstancesRoot))
            {
                string instanceName =
                    Path.GetFileName(
                        instanceDirectory);


                foreach (string directoryName in
                    legacyDirectoryNames)
                {
                    string path =
                        Path.Combine(
                            instanceDirectory,
                            directoryName);


                    if (!Directory.Exists(
                            path))
                    {
                        continue;
                    }


                    entries.Add(
                        new UnusedFilesCleanupEntry(
                            path,
                            CalculateDirectorySize(
                                path),
                            instanceName,
                            $"Copia antigua de {directoryName}"));
                }
            }
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
