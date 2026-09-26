using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ResourcePackSelectionResult
    {
        public bool Applied { get; init; }

        public bool AlreadyApplied { get; init; }

        public IReadOnlyList<string> MissingResourcePacks { get; init; } =
            Array.Empty<string>();
    }


    public sealed class ResourcePackSelectionService
    {
        private const string PresetFileName =
            ".negativeclient-resourcepacks-preset.json";

        private const string MarkerFileName =
            ".negativeclient-resourcepacks-applied.json";


        /*
         * ModpackInstallerService guarda aquí qué archivos pertenecen al ZIP.
         * Cuando reparamos un nombre Unicode también debemos reparar esta lista;
         * de lo contrario una actualización futura podría volver a crear el
         * nombre dañado y dejar dos copias del mismo texture pack.
         */
        private const string ManagedFilesName =
            ".negativeclient-managed-files.json";


        /*
         * Minecraft Java en Windows puede rechazar al arrancar un resource
         * pack cuyo identificador contiene el carácter de formato §, aunque
         * el archivo exista. Además, algunos ZIP antiguos llegan a .NET con
         * ese carácter decodificado como U+FFFD.
         *
         * Conservamos el archivo ORIGINAL y creamos un alias ASCII exclusivo
         * para el arranque. El usuario no pierde §3OVERLAND.zip; Minecraft
         * recibe, por ejemplo, NegativeClient_OVERLAND_A1B2C3D4.zip.
         */
        private const string AliasMarkerFileName =
            ".negativeclient-resourcepack-aliases.json";


        private sealed class ResourcePackPreset
        {
            public List<string> ResourcePacks { get; set; } =
                new();

            public List<string> IncompatibleResourcePacks { get; set; } =
                new();

            public string SourcePackageFileName { get; set; } =
                string.Empty;
        }


        private sealed class AppliedMarker
        {
            public string InstalledVersion { get; set; } =
                string.Empty;

            public string PackageFileName { get; set; } =
                string.Empty;

            public DateTime AppliedAtUtc { get; set; }
        }


        private sealed class ManagedFilesData
        {
            public List<string> Files { get; set; } =
                new();
        }


        private sealed class ResourcePackAliasState
        {
            public string InstalledVersion { get; set; } =
                string.Empty;

            public List<ResourcePackAliasEntry> Entries { get; set; } =
                new();
        }


        private sealed class ResourcePackAliasEntry
        {
            public string OriginalPackId { get; set; } =
                string.Empty;

            public string AliasPackId { get; set; } =
                string.Empty;

            public string SourceFileName { get; set; } =
                string.Empty;

            public long SourceLength { get; set; }

            public long SourceLastWriteTimeUtcTicks { get; set; }
        }


        private static readonly JsonSerializerOptions OptionsJsonSerializerOptions =
            new()
            {
                /*
                 * No escapamos § como \u00A7 al escribir options.txt.
                 * Minecraft 1.20.1 usa UTF-8, así que el nombre queda exactamente
                 * como fue preparado en el modpack.
                 */
                Encoder =
                    JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };


        private readonly InstanceService _instanceService;


        public ResourcePackSelectionService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;
        }


        public ResourcePackSelectionResult ApplyBundledSelectionIfNeeded(
            InstalledInstance instance)
        {
            if (!instance.IsInstalled ||
                string.IsNullOrWhiteSpace(
                    instance.Id))
            {
                return new ResourcePackSelectionResult();
            }


            string instanceDirectory =
                _instanceService
                    .GetInstanceDirectory(
                        instance.Id);

            Directory.CreateDirectory(
                instanceDirectory);


            string? packagePath =
                FindPackageArchive(
                    instance);

            ResourcePackPreset? preset =
                null;


            /*
             * El options.txt ORIGINAL del ZIP es únicamente nuestra referencia
             * para saber cómo se llamaban los resource packs seleccionados.
             *
             * MUY IMPORTANTE:
             * Negative Client NO escribe, mezcla ni reemplaza options.txt aquí.
             *
             * Si el modpack trae:
             *
             *   resourcePacks:[...,"file/§3OVERLAND.zip"]
             *
             * esa línea permanece exactamente como la dejó el creador.
             */
            if (!string.IsNullOrWhiteSpace(
                    packagePath) &&
                File.Exists(
                    packagePath))
            {
                preset =
                    ReadPresetFromArchive(
                        packagePath);

                if (preset !=
                    null)
                {
                    preset.SourcePackageFileName =
                        Path.GetFileName(
                            packagePath);

                    SavePreset(
                        instanceDirectory,
                        preset);
                }
            }


            preset ??=
                LoadSavedPreset(
                    instanceDirectory);


            if (preset ==
                    null ||
                preset.ResourcePacks.Count ==
                    0)
            {
                return new ResourcePackSelectionResult();
            }


            string resourcePacksDirectory =
                Path.Combine(
                    instanceDirectory,
                    "resourcepacks");

            Directory.CreateDirectory(
                resourcePacksDirectory);


            /*
             * ÚNICA reparación que hacemos antes de iniciar Minecraft:
             *
             * Si la extracción dejó algo como:
             *
             *   �3OVERLAND.zip
             *
             * pero el options.txt ORIGINAL esperaba:
             *
             *   §3OVERLAND.zip
             *
             * renombramos físicamente el archivo a §3OVERLAND.zip.
             *
             * No creamos alias.
             * No sustituimos el identificador del pack.
             * No reordenamos resourcePacks.
             * No tocamos incompatibleResourcePacks.
             * No escribimos options.txt.
             */
            RepairResourcePackFileNames(
                resourcePacksDirectory,
                preset);


            ResourcePackPreset normalizedPreset =
                NormalizePresetWithoutChangingFileNames(
                    preset);


            /*
             * Solo verificamos que los archivos que el options.txt original
             * dejó seleccionados existan físicamente con ESE MISMO nombre
             * después de la reparación.
             */
            List<string> missingPacks =
                FindMissingResourcePacks(
                    instanceDirectory,
                    normalizedPreset);


            /*
             * Este marker es interno de Negative Client y NO modifica
             * options.txt. Solo sirve como diagnóstico del preset detectado.
             */
            SaveMarker(
                instanceDirectory,
                instance.InstalledVersion,
                normalizedPreset.SourcePackageFileName);


            return new ResourcePackSelectionResult
            {
                Applied =
                    false,

                AlreadyApplied =
                    missingPacks.Count == 0,

                MissingResourcePacks =
                    missingPacks
            };
        }


        public static void SetExtraResourcePackEnabled(
            string optionsPath,
            string packIdentifier,
            bool enabled,
            bool markIncompatible = false)
        {
            if (string.IsNullOrWhiteSpace(
                    optionsPath) ||
                string.IsNullOrWhiteSpace(
                    packIdentifier))
            {
                return;
            }


            List<string> lines =
                File.Exists(
                    optionsPath)
                    ? SplitLines(
                            File.ReadAllText(
                                optionsPath))
                        .ToList()
                    : new List<string>();


            string resourcePacksLine =
                lines.FirstOrDefault(
                    line =>
                        line.StartsWith(
                            "resourcePacks:",
                            StringComparison.OrdinalIgnoreCase)) ??
                string.Empty;

            string incompatibleLine =
                lines.FirstOrDefault(
                    line =>
                        line.StartsWith(
                            "incompatibleResourcePacks:",
                            StringComparison.OrdinalIgnoreCase)) ??
                string.Empty;


            List<string> resourcePacks =
                ParsePackList(
                    resourcePacksLine);

            List<string> incompatiblePacks =
                ParsePackList(
                    incompatibleLine);


            resourcePacks.RemoveAll(
                pack =>
                    string.Equals(
                        pack,
                        packIdentifier,
                        StringComparison.OrdinalIgnoreCase));

            incompatiblePacks.RemoveAll(
                pack =>
                    string.Equals(
                        pack,
                        packIdentifier,
                        StringComparison.OrdinalIgnoreCase));


            int vanillaIndex =
                resourcePacks.FindIndex(
                    pack =>
                        string.Equals(
                            pack,
                            "vanilla",
                            StringComparison.OrdinalIgnoreCase));

            if (vanillaIndex <
                0)
            {
                resourcePacks.Insert(
                    0,
                    "vanilla");
            }
            else if (vanillaIndex >
                     0)
            {
                resourcePacks.RemoveAt(
                    vanillaIndex);

                resourcePacks.Insert(
                    0,
                    "vanilla");
            }


            if (enabled)
            {
                AddUnique(
                    resourcePacks,
                    packIdentifier);

                if (markIncompatible)
                {
                    AddUnique(
                        incompatiblePacks,
                        packIdentifier);
                }
            }


            MergePresetIntoOptions(
                optionsPath,
                new ResourcePackPreset
                {
                    ResourcePacks =
                        resourcePacks,

                    IncompatibleResourcePacks =
                        incompatiblePacks
                });
        }


        private static void RepairResourcePackFileNames(
            string resourcePacksDirectory,
            ResourcePackPreset preset)
        {
            if (!Directory.Exists(
                    resourcePacksDirectory))
            {
                return;
            }


            string? instanceDirectory =
                Directory.GetParent(
                    resourcePacksDirectory)?
                    .FullName;


            HashSet<string> managedFiles =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);


            bool managedFilesLoaded =
                false;


            if (!string.IsNullOrWhiteSpace(
                    instanceDirectory))
            {
                managedFilesLoaded =
                    TryLoadManagedFiles(
                        instanceDirectory!,
                        out managedFiles);
            }


            bool managedFilesChanged =
                false;


            foreach (string packId in
                preset.ResourcePacks)
            {
                if (!packId.StartsWith(
                        "file/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                string requestedName =
                    packId[5..]
                        .Replace(
                            '\\',
                            '/')
                        .TrimStart('/');


                if (string.IsNullOrWhiteSpace(
                        requestedName))
                {
                    continue;
                }


                string requestedPath =
                    SafeCombineResourcePackPath(
                        resourcePacksDirectory,
                        requestedName);


                string requestedFileName =
                    Path.GetFileName(
                        requestedName);


                string requestedKey =
                    BuildResourcePackComparisonKey(
                        requestedFileName);


                if (string.IsNullOrWhiteSpace(
                        requestedKey))
                {
                    continue;
                }


                string requestedFullPath =
                    Path.GetFullPath(
                        requestedPath);


                bool requestedExists =
                    File.Exists(
                        requestedPath) ||
                    Directory.Exists(
                        requestedPath);


                List<string> matchingEntries =
                    Directory
                        .EnumerateFileSystemEntries(
                            resourcePacksDirectory,
                            "*",
                            SearchOption.TopDirectoryOnly)
                        .Where(
                            candidate =>
                                string.Equals(
                                    BuildResourcePackComparisonKey(
                                        Path.GetFileName(
                                            candidate)),
                                    requestedKey,
                                    StringComparison.OrdinalIgnoreCase))
                        .ToList();


                List<string> alternativeCandidates =
                    matchingEntries
                        .Where(
                            candidate =>
                                !string.Equals(
                                    Path.GetFullPath(
                                        candidate),
                                    requestedFullPath,
                                    StringComparison.OrdinalIgnoreCase))
                        .ToList();


                string? candidatePath =
                    null;


                if (requestedExists)
                {
                    /*
                     * Caso importante de actualización:
                     *
                     * 1. Una versión anterior ya reparó:
                     *      §3OVERLAND.zip
                     *
                     * 2. El instalador extrae la actualización otra vez como:
                     *      �3OVERLAND.zip
                     *
                     * 3. .negativeclient-managed-files.json apunta al archivo
                     *    recién extraído (�...), no al viejo (§...).
                     *
                     * Antes el método veía que §... ya existía y se rendía,
                     * dejando al juego con la copia VIEJA seleccionada.
                     *
                     * Si hay exactamente un candidato alternativo administrado
                     * por el modpack y el nombre correcto no está administrado,
                     * el candidato es la copia nueva y debe reemplazar a la vieja.
                     */
                    if (managedFilesLoaded &&
                        !IsManagedPath(
                            managedFiles,
                            instanceDirectory!,
                            requestedPath))
                    {
                        List<string> managedAlternatives =
                            alternativeCandidates
                                .Where(
                                    candidate =>
                                        IsManagedPath(
                                            managedFiles,
                                            instanceDirectory!,
                                            candidate))
                                .ToList();


                        if (managedAlternatives.Count ==
                            1)
                        {
                            candidatePath =
                                managedAlternatives[0];
                        }
                    }


                    if (candidatePath ==
                        null)
                    {
                        /*
                         * Puede quedar una instalación creada por una versión
                         * anterior del launcher donde el archivo físico YA fue
                         * reparado a §..., pero el manifiesto administrado aún
                         * conserva resourcepacks/�3.... Si esa ruta dañada ya
                         * no existe físicamente, podemos reparar solo el
                         * manifiesto sin tocar el pack correcto.
                         */
                        if (managedFilesLoaded &&
                            !IsManagedPath(
                                managedFiles,
                                instanceDirectory!,
                                requestedPath))
                        {
                            List<string> managedEquivalentEntries =
                                GetManagedResourcePackEntriesByComparisonKey(
                                    managedFiles,
                                    requestedKey);


                            if (managedEquivalentEntries.Count ==
                                1)
                            {
                                string oldManagedRelative =
                                    managedEquivalentEntries[0];


                                string oldManagedFullPath =
                                    Path.GetFullPath(
                                        Path.Combine(
                                            instanceDirectory!,
                                            oldManagedRelative
                                                .Replace(
                                                    '/',
                                                    Path.DirectorySeparatorChar)));


                                if (!File.Exists(
                                        oldManagedFullPath) &&
                                    !Directory.Exists(
                                        oldManagedFullPath))
                                {
                                    managedFilesChanged |=
                                        RewriteManagedRelativePath(
                                            managedFiles,
                                            oldManagedRelative,
                                            NormalizeManagedRelativePath(
                                                Path.GetRelativePath(
                                                    instanceDirectory!,
                                                    requestedPath)));
                                }
                            }
                        }


                        continue;
                    }


                    string oldCandidatePath =
                        candidatePath;


                    ReplaceExistingPathWithCandidate(
                        candidatePath,
                        requestedPath);


                    if (managedFilesLoaded)
                    {
                        managedFilesChanged |=
                            RewriteManagedPathsAfterRename(
                                managedFiles,
                                instanceDirectory!,
                                oldCandidatePath,
                                requestedPath);
                    }


                    continue;
                }


                /*
                 * Instalación nueva:
                 * preferimos una única coincidencia. Si hay más de una,
                 * intentamos identificar de forma inequívoca cuál fue
                 * administrada por el ZIP del modpack.
                 */
                if (alternativeCandidates.Count ==
                    1)
                {
                    candidatePath =
                        alternativeCandidates[0];
                }
                else if (managedFilesLoaded)
                {
                    List<string> managedAlternatives =
                        alternativeCandidates
                            .Where(
                                candidate =>
                                    IsManagedPath(
                                        managedFiles,
                                        instanceDirectory!,
                                        candidate))
                            .ToList();


                    if (managedAlternatives.Count ==
                        1)
                    {
                        candidatePath =
                            managedAlternatives[0];
                    }
                }


                if (candidatePath ==
                    null)
                {
                    continue;
                }


                string candidateBeforeRename =
                    candidatePath;


                MovePath(
                    candidatePath,
                    requestedPath);


                if (managedFilesLoaded)
                {
                    managedFilesChanged |=
                        RewriteManagedPathsAfterRename(
                            managedFiles,
                            instanceDirectory!,
                            candidateBeforeRename,
                            requestedPath);
                }
            }


            if (managedFilesLoaded &&
                managedFilesChanged)
            {
                SaveManagedFiles(
                    instanceDirectory!,
                    managedFiles);
            }
        }


        private static bool TryLoadManagedFiles(
            string instanceDirectory,
            out HashSet<string> managedFiles)
        {
            managedFiles =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);


            string path =
                Path.Combine(
                    instanceDirectory,
                    ManagedFilesName);


            if (!File.Exists(
                    path))
            {
                return false;
            }


            try
            {
                ManagedFilesData? data =
                    JsonSerializer.Deserialize<ManagedFilesData>(
                        File.ReadAllText(
                            path),
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive =
                                true
                        });


                if (data?.Files ==
                    null)
                {
                    return false;
                }


                foreach (string file in
                    data.Files)
                {
                    string normalized =
                        NormalizeManagedRelativePath(
                            file);


                    if (!string.IsNullOrWhiteSpace(
                            normalized))
                    {
                        managedFiles.Add(
                            normalized);
                    }
                }


                return true;
            }
            catch
            {
                return false;
            }
        }


        private static void SaveManagedFiles(
            string instanceDirectory,
            IEnumerable<string> managedFiles)
        {
            string path =
                Path.Combine(
                    instanceDirectory,
                    ManagedFilesName);


            ManagedFilesData data =
                new()
                {
                    Files =
                        managedFiles
                            .Select(
                                NormalizeManagedRelativePath)
                            .Where(
                                value =>
                                    !string.IsNullOrWhiteSpace(
                                        value))
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .OrderBy(
                                value =>
                                    value,
                                StringComparer.OrdinalIgnoreCase)
                            .ToList()
                };


            string json =
                JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions
                    {
                        WriteIndented =
                            true
                    });


            string temporaryPath =
                path +
                ".negativeclient.tmp";


            File.WriteAllText(
                temporaryPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false));


            File.Move(
                temporaryPath,
                path,
                overwrite:
                    true);
        }


        private static bool IsManagedPath(
            HashSet<string> managedFiles,
            string instanceDirectory,
            string fullPath)
        {
            string relativePath =
                NormalizeManagedRelativePath(
                    Path.GetRelativePath(
                        instanceDirectory,
                        fullPath));


            if (managedFiles.Contains(
                    relativePath))
            {
                return true;
            }


            string prefix =
                relativePath
                    .TrimEnd('/') +
                "/";


            return managedFiles.Any(
                value =>
                    value.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase));
        }


        private static List<string>
            GetManagedResourcePackEntriesByComparisonKey(
                HashSet<string> managedFiles,
                string comparisonKey)
        {
            return managedFiles
                .Where(
                    value =>
                    {
                        string normalized =
                            NormalizeManagedRelativePath(
                                value);


                        if (!normalized.StartsWith(
                                "resourcepacks/",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }


                        string fileName =
                            Path.GetFileName(
                                normalized.Replace(
                                    '/',
                                    Path.DirectorySeparatorChar));


                        return string.Equals(
                            BuildResourcePackComparisonKey(
                                fileName),
                            comparisonKey,
                            StringComparison.OrdinalIgnoreCase);
                    })
                .ToList();
        }


        private static bool RewriteManagedRelativePath(
            HashSet<string> managedFiles,
            string oldRelative,
            string newRelative)
        {
            oldRelative =
                NormalizeManagedRelativePath(
                    oldRelative);


            newRelative =
                NormalizeManagedRelativePath(
                    newRelative);


            List<string> affected =
                managedFiles
                    .Where(
                        value =>
                            string.Equals(
                                value,
                                oldRelative,
                                StringComparison.OrdinalIgnoreCase) ||
                            value.StartsWith(
                                oldRelative +
                                "/",
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();


            if (affected.Count ==
                0)
            {
                return false;
            }


            foreach (string oldValue in
                affected)
            {
                managedFiles.Remove(
                    oldValue);


                string suffix =
                    oldValue.Length >
                    oldRelative.Length
                        ? oldValue[
                            oldRelative.Length..]
                        : string.Empty;


                managedFiles.Add(
                    newRelative +
                    suffix);
            }


            return true;
        }


        private static bool RewriteManagedPathsAfterRename(
            HashSet<string> managedFiles,
            string instanceDirectory,
            string oldFullPath,
            string newFullPath)
        {
            string oldRelative =
                NormalizeManagedRelativePath(
                    Path.GetRelativePath(
                        instanceDirectory,
                        oldFullPath));


            string newRelative =
                NormalizeManagedRelativePath(
                    Path.GetRelativePath(
                        instanceDirectory,
                        newFullPath));


            List<string> affected =
                managedFiles
                    .Where(
                        value =>
                            string.Equals(
                                value,
                                oldRelative,
                                StringComparison.OrdinalIgnoreCase) ||
                            value.StartsWith(
                                oldRelative +
                                "/",
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();


            if (affected.Count ==
                0)
            {
                return false;
            }


            foreach (string oldValue in
                affected)
            {
                managedFiles.Remove(
                    oldValue);


                string suffix =
                    oldValue.Length >
                    oldRelative.Length
                        ? oldValue[
                            oldRelative.Length..]
                        : string.Empty;


                managedFiles.Add(
                    newRelative +
                    suffix);
            }


            return true;
        }


        private static string NormalizeManagedRelativePath(
            string path)
        {
            return path
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/')
                .Replace(
                    Path.AltDirectorySeparatorChar,
                    '/')
                .TrimStart('/');
        }


        private static void ReplaceExistingPathWithCandidate(
            string candidatePath,
            string requestedPath)
        {
            string backupPath =
                requestedPath +
                ".negativeclient-old-" +
                Guid.NewGuid()
                    .ToString("N");


            bool backupCreated =
                false;


            try
            {
                if (File.Exists(
                        requestedPath) ||
                    Directory.Exists(
                        requestedPath))
                {
                    MovePath(
                        requestedPath,
                        backupPath);


                    backupCreated =
                        true;
                }


                MovePath(
                    candidatePath,
                    requestedPath);


                if (backupCreated)
                {
                    DeletePath(
                        backupPath);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(
                            requestedPath) ||
                        Directory.Exists(
                            requestedPath))
                    {
                        DeletePath(
                            requestedPath);
                    }


                    if (backupCreated &&
                        (File.Exists(
                             backupPath) ||
                         Directory.Exists(
                             backupPath)))
                    {
                        MovePath(
                            backupPath,
                            requestedPath);
                    }
                }
                catch
                {
                }


                throw;
            }
        }


        private static void MovePath(
            string sourcePath,
            string destinationPath)
        {
            string? parent =
                Path.GetDirectoryName(
                    destinationPath);


            if (!string.IsNullOrWhiteSpace(
                    parent))
            {
                Directory.CreateDirectory(
                    parent);
            }


            if (File.Exists(
                    sourcePath))
            {
                File.Move(
                    sourcePath,
                    destinationPath);


                return;
            }


            if (Directory.Exists(
                    sourcePath))
            {
                Directory.Move(
                    sourcePath,
                    destinationPath);


                return;
            }


            throw new FileNotFoundException(
                "No se encontró el texture pack que debía repararse.",
                sourcePath);
        }


        private static void DeletePath(
            string path)
        {
            if (File.Exists(
                    path))
            {
                File.Delete(
                    path);


                return;
            }


            if (Directory.Exists(
                    path))
            {
                Directory.Delete(
                    path,
                    recursive:
                        true);
            }
        }



        private static ResourcePackPreset BuildMinecraftSafeLaunchPreset(
            string instanceDirectory,
            ResourcePackPreset preset,
            string installedVersion)
        {
            string resourcePacksDirectory =
                Path.Combine(
                    instanceDirectory,
                    "resourcepacks");


            Directory.CreateDirectory(
                resourcePacksDirectory);


            ResourcePackAliasState previousState =
                LoadAliasState(
                    instanceDirectory) ??
                new ResourcePackAliasState();


            previousState.Entries ??=
                new List<ResourcePackAliasEntry>();


            Dictionary<string, ResourcePackAliasEntry> previousByOriginal =
                previousState.Entries
                    .Where(
                        entry =>
                            !string.IsNullOrWhiteSpace(
                                entry.OriginalPackId))
                    .GroupBy(
                        entry =>
                            entry.OriginalPackId,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.First(),
                        StringComparer.OrdinalIgnoreCase);


            Dictionary<string, string> aliasByOriginal =
                new(
                    StringComparer.OrdinalIgnoreCase);


            List<ResourcePackAliasEntry> activeEntries =
                new();


            List<string> launchSelected =
                new();


            foreach (string originalPackId in
                preset.ResourcePacks)
            {
                string normalizedOriginal =
                    NormalizePackIdentifierSlashesOnly(
                        originalPackId);


                if (!NeedsMinecraftSafeAlias(
                        normalizedOriginal))
                {
                    AddUnique(
                        launchSelected,
                        normalizedOriginal);

                    continue;
                }


                string aliasPackId =
                    BuildSafeAliasPackIdentifier(
                        normalizedOriginal);


                aliasByOriginal[
                    normalizedOriginal] =
                    aliasPackId;


                string? sourcePath =
                    ResolveResourcePackSourcePath(
                        resourcePacksDirectory,
                        normalizedOriginal);


                if (!string.IsNullOrWhiteSpace(
                        sourcePath))
                {
                    ResourcePackAliasEntry? previousEntry =
                        previousByOriginal.TryGetValue(
                            normalizedOriginal,
                            out ResourcePackAliasEntry? found)
                            ? found
                            : null;


                    ResourcePackAliasEntry currentEntry =
                        EnsureLaunchAlias(
                            resourcePacksDirectory,
                            sourcePath,
                            normalizedOriginal,
                            aliasPackId,
                            installedVersion,
                            previousState.InstalledVersion,
                            previousEntry);


                    activeEntries.Add(
                        currentEntry);
                }


                /*
                 * Añadimos el alias incluso si la creación falló/no encontró
                 * fuente. FindMissingResourcePacks lo detectará y bloqueará
                 * el arranque en vez de permitir que Minecraft borre el pack.
                 */
                AddUnique(
                    launchSelected,
                    aliasPackId);
            }


            List<string> launchIncompatible =
                new();


            foreach (string incompatiblePackId in
                preset.IncompatibleResourcePacks)
            {
                string normalized =
                    NormalizePackIdentifierSlashesOnly(
                        incompatiblePackId);


                if (aliasByOriginal.TryGetValue(
                        normalized,
                        out string? aliasPackId))
                {
                    AddUnique(
                        launchIncompatible,
                        aliasPackId);
                }
                else
                {
                    AddUnique(
                        launchIncompatible,
                        normalized);
                }
            }


            foreach (string selectedPackId in
                launchSelected.Where(
                    value =>
                        value.StartsWith(
                            "file/",
                            StringComparison.OrdinalIgnoreCase)))
            {
                AddUnique(
                    launchIncompatible,
                    selectedPackId);
            }


            CleanupStaleAliases(
                resourcePacksDirectory,
                previousState,
                activeEntries);


            SaveAliasState(
                instanceDirectory,
                new ResourcePackAliasState
                {
                    InstalledVersion =
                        installedVersion ??
                        string.Empty,

                    Entries =
                        activeEntries
                });


            return new ResourcePackPreset
            {
                ResourcePacks =
                    launchSelected,

                IncompatibleResourcePacks =
                    launchIncompatible,

                SourcePackageFileName =
                    preset.SourcePackageFileName
            };
        }


        private static bool NeedsMinecraftSafeAlias(
            string packId)
        {
            if (!packId.StartsWith(
                    "file/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }


            string name =
                packId[5..];


            return
                name.Contains(
                    '§') ||
                name.Contains(
                    '\uFFFD') ||
                name.Contains(
                    "Â§",
                    StringComparison.Ordinal) ||
                name.Contains(
                    "Ã‚Â§",
                    StringComparison.Ordinal);
        }


        private static string BuildSafeAliasPackIdentifier(
            string originalPackId)
        {
            string requestedName =
                originalPackId.StartsWith(
                    "file/",
                    StringComparison.OrdinalIgnoreCase)
                    ? originalPackId[5..]
                    : originalPackId;


            requestedName =
                requestedName
                    .Replace(
                        '\\',
                        '/')
                    .TrimStart('/');


            string fileName =
                Path.GetFileName(
                    requestedName);


            string extension =
                Path.GetExtension(
                    fileName);


            string stem =
                string.IsNullOrWhiteSpace(
                    extension)
                    ? fileName
                    : Path.GetFileNameWithoutExtension(
                        fileName);


            stem =
                RemoveMinecraftFormattingSequences(
                    stem);


            StringBuilder safeStem =
                new();


            foreach (char character in
                stem)
            {
                if ((character >= 'a' &&
                     character <= 'z') ||
                    (character >= 'A' &&
                     character <= 'Z') ||
                    (character >= '0' &&
                     character <= '9') ||
                    character == '-' ||
                    character == '_')
                {
                    safeStem.Append(
                        character);
                }
                else if (character == ' ' ||
                         character == '.')
                {
                    safeStem.Append(
                        '_');
                }
            }


            string safeName =
                safeStem
                    .ToString()
                    .Trim('_');


            if (string.IsNullOrWhiteSpace(
                    safeName))
            {
                safeName =
                    "ResourcePack";
            }


            byte[] hashBytes =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        originalPackId));


            string shortHash =
                Convert.ToHexString(
                        hashBytes)
                    [..8];


            string safeExtension =
                string.Equals(
                    extension,
                    ".zip",
                    StringComparison.OrdinalIgnoreCase)
                    ? ".zip"
                    : string.Empty;


            return
                "file/NegativeClient_" +
                safeName +
                "_" +
                shortHash +
                safeExtension;
        }


        private static string RemoveMinecraftFormattingSequences(
            string value)
        {
            StringBuilder result =
                new();


            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char character =
                    value[index];


                if (character == '§' &&
                    index + 1 < value.Length &&
                    IsMinecraftFormattingCode(
                        value[index + 1]))
                {
                    index++;
                    continue;
                }


                if (character == '\uFFFD' &&
                    index + 1 < value.Length &&
                    IsMinecraftFormattingCode(
                        value[index + 1]))
                {
                    index++;
                    continue;
                }


                result.Append(
                    character);
            }


            return result.ToString();
        }


        private static string? ResolveResourcePackSourcePath(
            string resourcePacksDirectory,
            string packId)
        {
            if (!packId.StartsWith(
                    "file/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }


            string requestedName =
                packId[5..]
                    .Replace(
                        '\\',
                        '/')
                    .TrimStart('/');


            if (string.IsNullOrWhiteSpace(
                    requestedName))
            {
                return null;
            }


            string exactPath =
                SafeCombineResourcePackPath(
                    resourcePacksDirectory,
                    requestedName);


            if (File.Exists(
                    exactPath) ||
                Directory.Exists(
                    exactPath))
            {
                return exactPath;
            }


            string comparisonKey =
                BuildResourcePackComparisonKey(
                    Path.GetFileName(
                        requestedName));


            if (string.IsNullOrWhiteSpace(
                    comparisonKey))
            {
                return null;
            }


            List<string> candidates =
                Directory
                    .EnumerateFileSystemEntries(
                        resourcePacksDirectory,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(
                        candidate =>
                            !Path.GetFileName(
                                    candidate)
                                .StartsWith(
                                    "NegativeClient_",
                                    StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(
                                BuildResourcePackComparisonKey(
                                    Path.GetFileName(
                                        candidate)),
                                comparisonKey,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();


            return candidates.Count ==
                    1
                ? candidates[0]
                : null;
        }


        private static ResourcePackAliasEntry EnsureLaunchAlias(
            string resourcePacksDirectory,
            string sourcePath,
            string originalPackId,
            string aliasPackId,
            string installedVersion,
            string previousInstalledVersion,
            ResourcePackAliasEntry? previousEntry)
        {
            string aliasName =
                aliasPackId[5..];


            string aliasPath =
                SafeCombineResourcePackPath(
                    resourcePacksDirectory,
                    aliasName);


            bool sourceIsFile =
                File.Exists(
                    sourcePath);


            long sourceLength =
                sourceIsFile
                    ? new FileInfo(
                            sourcePath)
                        .Length
                    : -1;


            long sourceLastWriteTicks =
                sourceIsFile
                    ? File.GetLastWriteTimeUtc(
                            sourcePath)
                        .Ticks
                    : Directory.GetLastWriteTimeUtc(
                            sourcePath)
                        .Ticks;


            bool canReuse =
                previousEntry != null &&
                string.Equals(
                    installedVersion,
                    previousInstalledVersion,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    previousEntry.AliasPackId,
                    aliasPackId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    previousEntry.SourceFileName,
                    Path.GetFileName(
                        sourcePath),
                    StringComparison.Ordinal) &&
                previousEntry.SourceLength ==
                    sourceLength &&
                previousEntry.SourceLastWriteTimeUtcTicks ==
                    sourceLastWriteTicks &&
                ((sourceIsFile &&
                  File.Exists(
                      aliasPath)) ||
                 (!sourceIsFile &&
                  Directory.Exists(
                      aliasPath)));


            if (!canReuse)
            {
                DeletePath(
                    aliasPath);


                if (sourceIsFile)
                {
                    string temporaryPath =
                        aliasPath +
                        ".negativeclient-copying";


                    try
                    {
                        File.Copy(
                            sourcePath,
                            temporaryPath,
                            overwrite:
                                true);


                        File.SetLastWriteTimeUtc(
                            temporaryPath,
                            File.GetLastWriteTimeUtc(
                                sourcePath));


                        File.Move(
                            temporaryPath,
                            aliasPath,
                            overwrite:
                                true);
                    }
                    finally
                    {
                        if (File.Exists(
                                temporaryPath))
                        {
                            try
                            {
                                File.Delete(
                                    temporaryPath);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                else if (Directory.Exists(
                             sourcePath))
                {
                    CopyDirectoryForAlias(
                        sourcePath,
                        aliasPath);
                }
            }


            return new ResourcePackAliasEntry
            {
                OriginalPackId =
                    originalPackId,

                AliasPackId =
                    aliasPackId,

                SourceFileName =
                    Path.GetFileName(
                        sourcePath),

                SourceLength =
                    sourceLength,

                SourceLastWriteTimeUtcTicks =
                    sourceLastWriteTicks
            };
        }


        private static void CopyDirectoryForAlias(
            string sourceDirectory,
            string destinationDirectory)
        {
            Directory.CreateDirectory(
                destinationDirectory);


            foreach (string sourceSubdirectory in
                Directory.EnumerateDirectories(
                    sourceDirectory,
                    "*",
                    SearchOption.AllDirectories))
            {
                string relative =
                    Path.GetRelativePath(
                        sourceDirectory,
                        sourceSubdirectory);


                Directory.CreateDirectory(
                    Path.Combine(
                        destinationDirectory,
                        relative));
            }


            foreach (string sourceFile in
                Directory.EnumerateFiles(
                    sourceDirectory,
                    "*",
                    SearchOption.AllDirectories))
            {
                string relative =
                    Path.GetRelativePath(
                        sourceDirectory,
                        sourceFile);


                string destinationFile =
                    Path.Combine(
                        destinationDirectory,
                        relative);


                string? parent =
                    Path.GetDirectoryName(
                        destinationFile);


                if (!string.IsNullOrWhiteSpace(
                        parent))
                {
                    Directory.CreateDirectory(
                        parent);
                }


                File.Copy(
                    sourceFile,
                    destinationFile,
                    overwrite:
                        true);
            }
        }


        private static ResourcePackAliasState? LoadAliasState(
            string instanceDirectory)
        {
            string path =
                Path.Combine(
                    instanceDirectory,
                    AliasMarkerFileName);


            if (!File.Exists(
                    path))
            {
                return null;
            }


            try
            {
                return JsonSerializer.Deserialize<ResourcePackAliasState>(
                    File.ReadAllText(
                        path),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    });
            }
            catch
            {
                return null;
            }
        }


        private static void SaveAliasState(
            string instanceDirectory,
            ResourcePackAliasState state)
        {
            try
            {
                string path =
                    Path.Combine(
                        instanceDirectory,
                        AliasMarkerFileName);


                string json =
                    JsonSerializer.Serialize(
                        state,
                        new JsonSerializerOptions
                        {
                            WriteIndented =
                                true,
                            Encoder =
                                JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });


                string temporaryPath =
                    path +
                    ".tmp";


                File.WriteAllText(
                    temporaryPath,
                    json,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier:
                            false));


                File.Move(
                    temporaryPath,
                    path,
                    overwrite:
                        true);
            }
            catch
            {
            }
        }


        private static void CleanupStaleAliases(
            string resourcePacksDirectory,
            ResourcePackAliasState previousState,
            IReadOnlyCollection<ResourcePackAliasEntry> activeEntries)
        {
            HashSet<string> activeAliasIds =
                activeEntries
                    .Select(
                        entry =>
                            entry.AliasPackId)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);


            foreach (ResourcePackAliasEntry oldEntry in
                previousState.Entries)
            {
                if (string.IsNullOrWhiteSpace(
                        oldEntry.AliasPackId) ||
                    activeAliasIds.Contains(
                        oldEntry.AliasPackId) ||
                    !oldEntry.AliasPackId.StartsWith(
                        "file/NegativeClient_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                try
                {
                    string oldAliasPath =
                        SafeCombineResourcePackPath(
                            resourcePacksDirectory,
                            oldEntry.AliasPackId[5..]);


                    DeletePath(
                        oldAliasPath);
                }
                catch
                {
                }
            }
        }


        private static string BuildResourcePackComparisonKey(
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(
                    fileName))
            {
                return string.Empty;
            }


            string normalized =
                fileName.Normalize(
                    NormalizationForm.FormKC);

            StringBuilder result =
                new();


            for (int index = 0;
                 index <
                 normalized.Length;
                 index++)
            {
                char character =
                    normalized[index];


                /*
                 * Quitamos los códigos de formato de Minecraft ÚNICAMENTE
                 * para comparar candidatos.
                 *
                 * El nombre real NO se modifica con esta operación.
                 */
                if (character ==
                    '§')
                {
                    if (index + 1 <
                        normalized.Length)
                    {
                        index++;
                    }

                    continue;
                }


                /*
                 * Algunos ZIP guardados sin la bandera/codificación correcta
                 * llegan a .NET con el carácter § convertido en U+FFFD (�).
                 * En ese caso Windows termina creando algo como:
                 *
                 *     �3OVERLAND.zip
                 *
                 * El '3' sigue siendo el código de color que iba después de §,
                 * por lo que para COMPARAR lo descartamos junto con U+FFFD.
                 * Después RepairResourcePackFileNames renombra físicamente el
                 * archivo al nombre ORIGINAL del options.txt: §3OVERLAND.zip.
                 */
                if (character ==
                        '\uFFFD' &&
                    index + 1 <
                        normalized.Length &&
                    IsMinecraftFormattingCode(
                        normalized[index + 1]))
                {
                    index++;

                    continue;
                }


                /*
                 * Otras herramientas ZIP antiguas pueden decodificar 0xA7
                 * como º. También aceptamos esa forma SOLO para comparar.
                 * Nunca la escribimos en options.txt ni la convertimos en
                 * el nombre definitivo del pack.
                 */
                if (character ==
                        '\u00BA' &&
                    index + 1 <
                        normalized.Length &&
                    IsMinecraftFormattingCode(
                        normalized[index + 1]))
                {
                    index++;

                    continue;
                }


                /*
                 * Defensa adicional para nombres ya dañados por una cadena
                 * intermedia que haya representado § como '?'. En Windows
                 * '?' no puede formar parte de un nombre real, pero este caso
                 * puede aparecer en rutas importadas o metadatos.
                 */
                if (character ==
                        '?' &&
                    index + 1 <
                        normalized.Length &&
                    IsMinecraftFormattingCode(
                        normalized[index + 1]))
                {
                    index++;

                    continue;
                }


                /*
                 * Nos quedamos con la parte ASCII estable del nombre para
                 * poder reconocer mojibake como "Â§3..." sin adoptar ese
                 * nombre dañado.
                 */
                if ((character >= 'a' &&
                     character <= 'z') ||
                    (character >= 'A' &&
                     character <= 'Z') ||
                    (character >= '0' &&
                     character <= '9') ||
                    character == '.' ||
                    character == '_' ||
                    character == '-' ||
                    character == ' ')
                {
                    result.Append(
                        character);
                }
            }


            return result
                .ToString()
                .Trim()
                .ToUpperInvariant();
        }


        private static bool IsMinecraftFormattingCode(
            char character)
        {
            char lower =
                char.ToLowerInvariant(
                    character);

            return
                (lower >= '0' &&
                 lower <= '9') ||
                (lower >= 'a' &&
                 lower <= 'f') ||
                lower == 'k' ||
                lower == 'l' ||
                lower == 'm' ||
                lower == 'n' ||
                lower == 'o' ||
                lower == 'r' ||
                lower == 'x';
        }


        private static ResourcePackPreset NormalizePresetWithoutChangingFileNames(
            ResourcePackPreset preset)
        {
            List<string> selected =
                new();

            foreach (string packId in
                preset.ResourcePacks)
            {
                string normalized =
                    NormalizePackIdentifierSlashesOnly(
                        packId);

                AddUnique(
                    selected,
                    normalized);
            }


            List<string> incompatible =
                new();

            foreach (string packId in
                preset.IncompatibleResourcePacks)
            {
                string normalized =
                    NormalizePackIdentifierSlashesOnly(
                        packId);

                AddUnique(
                    incompatible,
                    normalized);
            }


            /*
             * Los packs que venían activos se consideran aprobados por el
             * creador del modpack, igual que en la implementación anterior.
             */
            foreach (string selectedId in
                selected.Where(
                    value =>
                        value.StartsWith(
                            "file/",
                            StringComparison.OrdinalIgnoreCase)))
            {
                AddUnique(
                    incompatible,
                    selectedId);
            }


            return new ResourcePackPreset
            {
                ResourcePacks =
                    selected,

                IncompatibleResourcePacks =
                    incompatible,

                SourcePackageFileName =
                    preset.SourcePackageFileName
            };
        }


        private static string NormalizePackIdentifierSlashesOnly(
            string packId)
        {
            if (!packId.StartsWith(
                    "file/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return packId;
            }


            return
                "file/" +
                packId[5..]
                    .Replace(
                        '\\',
                        '/')
                    .TrimStart('/');
        }


        private static string SafeCombineResourcePackPath(
            string resourcePacksDirectory,
            string relativeName)
        {
            string root =
                Path.GetFullPath(
                    resourcePacksDirectory);

            string rootWithSeparator =
                root.EndsWith(
                    Path.DirectorySeparatorChar)
                    ? root
                    : root +
                      Path.DirectorySeparatorChar;


            string combined =
                Path.GetFullPath(
                    Path.Combine(
                        root,
                        relativeName
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar)
                            .Replace(
                                '\\',
                                Path.DirectorySeparatorChar)));


            if (!combined.StartsWith(
                    rootWithSeparator,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Se detectó una ruta de texture pack no segura.");
            }


            return combined;
        }


        private static string? FindPackageArchive(
            InstalledInstance instance)
        {
            string cacheRoot =
                InstanceService.PackageCacheRoot;


            if (!Directory.Exists(
                    cacheRoot))
            {
                return null;
            }


            string safeId =
                SanitizeFileName(
                    instance.Id);

            string safeVersion =
                SanitizeFileName(
                    instance.InstalledVersion);

            string prefix =
                safeId +
                "-" +
                safeVersion +
                "-";


            return Directory
                .EnumerateFiles(
                    cacheRoot,
                    "*.zip",
                    SearchOption.TopDirectoryOnly)
                .Where(
                    path =>
                        Path.GetFileName(
                                path)
                            .StartsWith(
                                prefix,
                                StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(
                    File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }


        private static ResourcePackPreset? ReadPresetFromArchive(
            string packagePath)
        {
            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(
                        packagePath);


                ZipArchiveEntry? optionsEntry =
                    archive.Entries
                        .FirstOrDefault(
                            entry =>
                                string.Equals(
                                    NormalizeArchivePath(
                                        entry.FullName),
                                    "options.txt",
                                    StringComparison.OrdinalIgnoreCase));


                optionsEntry ??=
                    archive.Entries
                        .Where(
                            entry =>
                                NormalizeArchivePath(
                                        entry.FullName)
                                    .EndsWith(
                                        "/options.txt",
                                        StringComparison.OrdinalIgnoreCase))
                        .OrderBy(
                            entry =>
                                NormalizeArchivePath(
                                        entry.FullName)
                                    .Count(
                                        character =>
                                            character ==
                                            '/'))
                        .FirstOrDefault();


                if (optionsEntry ==
                    null)
                {
                    return null;
                }


                using Stream stream =
                    optionsEntry.Open();

                using StreamReader reader =
                    new(
                        stream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks:
                            true);


                string[] lines =
                    SplitLines(
                        reader.ReadToEnd());


                string resourcePacksLine =
                    lines.FirstOrDefault(
                        line =>
                            line.StartsWith(
                                "resourcePacks:",
                                StringComparison.OrdinalIgnoreCase)) ??
                    string.Empty;

                string incompatibleLine =
                    lines.FirstOrDefault(
                        line =>
                            line.StartsWith(
                                "incompatibleResourcePacks:",
                                StringComparison.OrdinalIgnoreCase)) ??
                    string.Empty;


                List<string> selected =
                    ParsePackList(
                        resourcePacksLine);


                if (selected.Count ==
                    0)
                {
                    return null;
                }


                return new ResourcePackPreset
                {
                    ResourcePacks =
                        selected,

                    IncompatibleResourcePacks =
                        ParsePackList(
                            incompatibleLine)
                };
            }
            catch
            {
                return null;
            }
        }


        private static ResourcePackPreset? LoadSavedPreset(
            string instanceDirectory)
        {
            string path =
                Path.Combine(
                    instanceDirectory,
                    PresetFileName);


            if (!File.Exists(
                    path))
            {
                return null;
            }


            try
            {
                return JsonSerializer
                    .Deserialize<ResourcePackPreset>(
                        File.ReadAllText(
                            path));
            }
            catch
            {
                return null;
            }
        }


        private static void SavePreset(
            string instanceDirectory,
            ResourcePackPreset preset)
        {
            try
            {
                string json =
                    JsonSerializer.Serialize(
                        preset,
                        new JsonSerializerOptions
                        {
                            WriteIndented =
                                true,
                            Encoder =
                                JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });


                File.WriteAllText(
                    Path.Combine(
                        instanceDirectory,
                        PresetFileName),
                    json,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier:
                            false));
            }
            catch
            {
            }
        }


        private static bool MergePresetIntoOptions(
            string optionsPath,
            ResourcePackPreset preset)
        {
            List<string> lines =
                File.Exists(
                    optionsPath)
                    ? SplitLines(
                            File.ReadAllText(
                                optionsPath))
                        .ToList()
                    : new List<string>();


            string selectedLine =
                "resourcePacks:" +
                JsonSerializer.Serialize(
                    preset.ResourcePacks,
                    OptionsJsonSerializerOptions);

            string incompatibleLine =
                "incompatibleResourcePacks:" +
                JsonSerializer.Serialize(
                    preset.IncompatibleResourcePacks,
                    OptionsJsonSerializerOptions);


            bool changed =
                false;


            changed |=
                ReplaceOrAppendLine(
                    lines,
                    "resourcePacks:",
                    selectedLine);

            changed |=
                ReplaceOrAppendLine(
                    lines,
                    "incompatibleResourcePacks:",
                    incompatibleLine);


            if (!changed)
            {
                return false;
            }


            string output =
                string.Join(
                    Environment.NewLine,
                    lines);


            if (!output.EndsWith(
                    Environment.NewLine,
                    StringComparison.Ordinal))
            {
                output +=
                    Environment.NewLine;
            }


            string tempPath =
                optionsPath +
                ".negativeclient.tmp";


            File.WriteAllText(
                tempPath,
                output,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false));


            if (File.Exists(
                    optionsPath))
            {
                File.Copy(
                    tempPath,
                    optionsPath,
                    overwrite:
                        true);

                File.Delete(
                    tempPath);
            }
            else
            {
                File.Move(
                    tempPath,
                    optionsPath);
            }


            return true;
        }


        private static bool ReplaceOrAppendLine(
            List<string> lines,
            string prefix,
            string replacement)
        {
            for (int index = 0;
                 index <
                 lines.Count;
                 index++)
            {
                if (!lines[index]
                        .StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                if (string.Equals(
                        lines[index],
                        replacement,
                        StringComparison.Ordinal))
                {
                    return false;
                }


                lines[index] =
                    replacement;

                return true;
            }


            lines.Add(
                replacement);

            return true;
        }


        private static List<string> ParsePackList(
            string line)
        {
            if (string.IsNullOrWhiteSpace(
                    line))
            {
                return new List<string>();
            }


            int separatorIndex =
                line.IndexOf(
                    ':');


            if (separatorIndex <
                    0 ||
                separatorIndex >=
                    line.Length -
                    1)
            {
                return new List<string>();
            }


            string json =
                line[(separatorIndex + 1)..]
                    .Trim();


            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(
                        json);


                if (document.RootElement.ValueKind !=
                    JsonValueKind.Array)
                {
                    return new List<string>();
                }


                List<string> result =
                    new();


                foreach (JsonElement element in
                    document.RootElement
                        .EnumerateArray())
                {
                    if (element.ValueKind !=
                        JsonValueKind.String)
                    {
                        continue;
                    }


                    string? value =
                        element.GetString();


                    if (string.IsNullOrWhiteSpace(
                            value))
                    {
                        continue;
                    }


                    AddUnique(
                        result,
                        value);
                }


                return result;
            }
            catch
            {
                return new List<string>();
            }
        }


        private static List<string> FindMissingResourcePacks(
            string instanceDirectory,
            ResourcePackPreset preset)
        {
            string resourcePacksDirectory =
                Path.Combine(
                    instanceDirectory,
                    "resourcepacks");

            List<string> missing =
                new();


            foreach (string packId in
                preset.ResourcePacks)
            {
                if (!packId.StartsWith(
                        "file/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                string requestedName =
                    packId[5..]
                        .Replace(
                            '\\',
                            '/')
                        .TrimStart('/');


                string candidate =
                    SafeCombineResourcePackPath(
                        resourcePacksDirectory,
                        requestedName);


                if (!File.Exists(
                        candidate) &&
                    !Directory.Exists(
                        candidate))
                {
                    missing.Add(
                        packId);
                }
            }


            return missing;
        }


        private static void SaveMarker(
            string instanceDirectory,
            string installedVersion,
            string packageFileName)
        {
            try
            {
                AppliedMarker marker =
                    new()
                    {
                        InstalledVersion =
                            installedVersion ??
                            string.Empty,

                        PackageFileName =
                            packageFileName ??
                            string.Empty,

                        AppliedAtUtc =
                            DateTime.UtcNow
                    };


                string json =
                    JsonSerializer.Serialize(
                        marker,
                        new JsonSerializerOptions
                        {
                            WriteIndented =
                                true
                        });


                File.WriteAllText(
                    Path.Combine(
                        instanceDirectory,
                        MarkerFileName),
                    json,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier:
                            false));
            }
            catch
            {
            }
        }


        private static void AddUnique(
            List<string> target,
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return;
            }


            if (target.Any(
                    existing =>
                        string.Equals(
                            existing,
                            value,
                            StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }


            target.Add(
                value);
        }


        private static string[] SplitLines(
            string text)
        {
            return text
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    '\r',
                    '\n')
                .Split(
                    '\n',
                    StringSplitOptions.None);
        }


        private static string NormalizeArchivePath(
            string path)
        {
            return path
                .Replace(
                    '\\',
                    '/')
                .TrimStart('/');
        }


        private static string SanitizeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return "unknown";
            }


            char[] invalidCharacters =
                Path.GetInvalidFileNameChars();


            string safe =
                new(
                    value
                        .Where(
                            character =>
                                !invalidCharacters.Contains(
                                    character))
                        .ToArray());


            return string.IsNullOrWhiteSpace(
                    safe)
                ? "unknown"
                : safe;
        }
    }
}
