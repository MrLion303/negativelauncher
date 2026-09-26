using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
             * El options.txt ORIGINAL del ZIP sigue siendo la fuente de verdad.
             * Por tanto, si allí aparece:
             *
             *   file/§3OVERLAND.zip
             *
             * conservamos exactamente ese identificador.
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
             * IMPORTANTE:
             *
             * Si la extracción del ZIP exterior alteró un carácter Unicode del
             * nombre del resource pack, NO cambiamos options.txt para aceptar
             * el nombre alterado.
             *
             * Hacemos lo contrario:
             * restauramos físicamente el archivo al nombre que decía el
             * options.txt original del modpack.
             *
             * Ejemplo:
             *   options original -> §3OVERLAND.zip
             *   archivo extraído -> nombre dañado por codificación
             *   resultado        -> §3OVERLAND.zip
             */
            RepairResourcePackFileNames(
                resourcePacksDirectory,
                preset);


            ResourcePackPreset normalizedPreset =
                NormalizePresetWithoutChangingFileNames(
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


                if (File.Exists(
                        requestedPath) ||
                    Directory.Exists(
                        requestedPath))
                {
                    continue;
                }


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


                List<string> candidates =
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


                /*
                 * Solo renombramos si hay UNA coincidencia inequívoca.
                 * Así nunca podemos confundir dos texture packs distintos.
                 */
                if (candidates.Count !=
                    1)
                {
                    continue;
                }


                string candidatePath =
                    candidates[0];


                if (string.Equals(
                        Path.GetFullPath(
                            candidatePath),
                        Path.GetFullPath(
                            requestedPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                string? parent =
                    Path.GetDirectoryName(
                        requestedPath);

                if (!string.IsNullOrWhiteSpace(
                        parent))
                {
                    Directory.CreateDirectory(
                        parent);
                }


                try
                {
                    if (File.Exists(
                            candidatePath))
                    {
                        File.Move(
                            candidatePath,
                            requestedPath);
                    }
                    else if (Directory.Exists(
                                 candidatePath))
                    {
                        Directory.Move(
                            candidatePath,
                            requestedPath);
                    }
                }
                catch
                {
                    /*
                     * Si Windows impide el rename, no sustituimos el carácter
                     * especial por otro. El pack quedará marcado como faltante
                     * y el launcher no corromperá su nombre.
                     */
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
