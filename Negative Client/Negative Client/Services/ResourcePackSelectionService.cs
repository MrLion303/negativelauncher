using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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


        private sealed class AppliedMarker
        {
            public string InstalledVersion { get; set; } =
                string.Empty;

            public string PackageFileName { get; set; } =
                string.Empty;

            public DateTime AppliedAtUtc { get; set; }
        }


        private sealed class ResourcePackPreset
        {
            public List<string> ResourcePacks { get; set; } =
                new List<string>();

            public List<string> IncompatibleResourcePacks { get; set; } =
                new List<string>();

            public string SourcePackageFileName { get; set; } =
                string.Empty;
        }


        private readonly InstanceService _instanceService;


        public ResourcePackSelectionService(
            InstanceService instanceService)
        {
            _instanceService =
                instanceService;
        }


        /*
         * IMPORTANTE:
         * Esta rutina YA NO deja de aplicar el preset porque exista el marker.
         *
         * El marker anterior provocaba un caso problemático:
         * 1. Negative Client escribía resourcePacks.
         * 2. Minecraft podía reescribir options.txt posteriormente.
         * 3. El marker seguía diciendo "ya aplicado".
         * 4. En el siguiente inicio el launcher no restauraba la selección.
         *
         * Ahora se compara y sincroniza la selección EN CADA ARRANQUE.
         * Solo se tocan resourcePacks e incompatibleResourcePacks; el resto
         * de options.txt se conserva exactamente como lo dejó el usuario.
         */
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
             * Si todavía tenemos el ZIP original del modpack, éste manda.
             * De ahí sacamos exactamente qué packs estaban seleccionados
             * cuando el creador preparó options.txt.
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


            /*
             * Si la caché del ZIP ya no existe, usamos el preset persistente
             * que se guardó dentro de la instancia. Así la selección no
             * depende de que el usuario conserve la caché de descargas.
             */
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


            ResourcePackPreset normalizedPreset =
                NormalizePresetAgainstInstalledFiles(
                    instanceDirectory,
                    preset);


            List<string> missingPacks =
                FindMissingResourcePacks(
                    instanceDirectory,
                    normalizedPreset);


            string destinationOptionsPath =
                Path.Combine(
                    instanceDirectory,
                    "options.txt");


            bool changed =
                MergePresetIntoOptions(
                    destinationOptionsPath,
                    normalizedPreset);


            /*
             * Guardamos el marker únicamente como diagnóstico.
             * Ya NO se usa para saltarse la sincronización.
             */
            SaveMarker(
                instanceDirectory,
                instance.InstalledVersion,
                normalizedPreset.SourcePackageFileName);


            return new ResourcePackSelectionResult
            {
                Applied =
                    changed,

                AlreadyApplied =
                    !changed,

                MissingResourcePacks =
                    missingPacks
            };
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
                        Path.GetFileName(path)
                            .StartsWith(
                                prefix,
                                StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(
                    path =>
                        File.GetLastWriteTimeUtc(
                            path))
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
                    new StreamReader(
                        stream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks:
                            true);


                string content =
                    reader.ReadToEnd();


                string[] lines =
                    SplitLines(
                        content);


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
                                true
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


        private static ResourcePackPreset NormalizePresetAgainstInstalledFiles(
            string instanceDirectory,
            ResourcePackPreset preset)
        {
            string resourcePacksDirectory =
                Path.Combine(
                    instanceDirectory,
                    "resourcepacks");


            List<string> normalizedSelected =
                new List<string>();


            foreach (string packId in
                preset.ResourcePacks)
            {
                if (!packId.StartsWith(
                        "file/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(
                        normalizedSelected,
                        packId);

                    continue;
                }


                string requestedName =
                    packId[5..]
                        .Replace(
                            '\\',
                            '/');


                string? actualName =
                    FindActualResourcePackName(
                        resourcePacksDirectory,
                        requestedName);


                string normalizedId =
                    "file/" +
                    (actualName ??
                     requestedName);


                AddUnique(
                    normalizedSelected,
                    normalizedId);
            }


            List<string> incompatible =
                new List<string>();


            foreach (string packId in
                preset.IncompatibleResourcePacks)
            {
                string normalized =
                    NormalizeIncompatibleId(
                        resourcePacksDirectory,
                        packId);

                AddUnique(
                    incompatible,
                    normalized);
            }


            /*
             * Minecraft usa incompatibleResourcePacks para recordar que el
             * usuario aceptó packs cuyo pack_format no coincide exactamente
             * con la versión. Si el modpack fue preparado con uno de esos
             * packs pero esta lista se pierde, Minecraft puede dejarlo
             * deseleccionado al iniciar.
             *
             * Para un preset administrado por el modpack, cada file/... que
             * estaba seleccionado se considera explícitamente aprobado.
             */
            foreach (string selectedId in
                normalizedSelected.Where(
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
                    normalizedSelected,

                IncompatibleResourcePacks =
                    incompatible,

                SourcePackageFileName =
                    preset.SourcePackageFileName
            };
        }


        private static string NormalizeIncompatibleId(
            string resourcePacksDirectory,
            string packId)
        {
            if (!packId.StartsWith(
                    "file/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return packId;
            }

            string requestedName =
                packId[5..]
                    .Replace(
                        '\\',
                        '/');

            string? actualName =
                FindActualResourcePackName(
                    resourcePacksDirectory,
                    requestedName);

            return
                "file/" +
                (actualName ??
                 requestedName);
        }


        private static string? FindActualResourcePackName(
            string resourcePacksDirectory,
            string requestedName)
        {
            if (!Directory.Exists(
                    resourcePacksDirectory))
            {
                return null;
            }

            string normalizedRequested =
                requestedName
                    .Replace(
                        '\\',
                        '/')
                    .TrimStart('/');


            /*
             * Primer intento: ruta exacta.
             */
            string exactPath =
                Path.Combine(
                    resourcePacksDirectory,
                    normalizedRequested
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar));


            if (File.Exists(
                    exactPath) ||
                Directory.Exists(
                    exactPath))
            {
                return normalizedRequested;
            }


            /*
             * Segundo intento: comparación sin distinguir mayúsculas y
             * minúsculas. Es útil si el ZIP y options.txt no conservaron
             * exactamente el casing del nombre.
             */
            foreach (string candidate in
                Directory.EnumerateFileSystemEntries(
                    resourcePacksDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly))
            {
                string candidateName =
                    Path.GetFileName(
                        candidate);

                if (string.Equals(
                        candidateName,
                        Path.GetFileName(
                            normalizedRequested),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidateName
                        .Replace(
                            '\\',
                            '/');
                }
            }


            return null;
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
                    preset.ResourcePacks);


            string incompatibleLine =
                "incompatibleResourcePacks:" +
                JsonSerializer.Serialize(
                    preset.IncompatibleResourcePacks);


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
                    new List<string>();

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
                new List<string>();

            foreach (string packId in
                preset.ResourcePacks)
            {
                if (!packId.StartsWith(
                        "file/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative =
                    packId[5..]
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar)
                        .Replace(
                            '\\',
                            Path.DirectorySeparatorChar);

                string candidate =
                    Path.GetFullPath(
                        Path.Combine(
                            resourcePacksDirectory,
                            relative));

                string root =
                    Path.GetFullPath(
                        resourcePacksDirectory) +
                    Path.DirectorySeparatorChar;

                if (!candidate.StartsWith(
                        root,
                        StringComparison.OrdinalIgnoreCase) ||
                    (!File.Exists(
                         candidate) &&
                     !Directory.Exists(
                         candidate)))
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
                    new AppliedMarker
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
                new string(
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
