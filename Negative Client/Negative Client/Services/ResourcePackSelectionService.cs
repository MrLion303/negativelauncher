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
        private const string MarkerFileName =
            ".negativeclient-resourcepacks-applied.json";


        private sealed class AppliedMarker
        {
            public string InstalledVersion { get; set; } =
                string.Empty;

            public string PackageFileName { get; set; } =
                string.Empty;
        }


        private sealed class ResourcePackPreset
        {
            public string ResourcePacksLine { get; init; } =
                string.Empty;

            public string IncompatibleResourcePacksLine { get; init; } =
                string.Empty;

            public List<string> SelectedFilePacks { get; init; } =
                new();
        }


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
                    instance.Id) ||
                string.IsNullOrWhiteSpace(
                    instance.InstalledVersion))
            {
                return new ResourcePackSelectionResult();
            }


            string? packagePath =
                FindPackageArchive(
                    instance);


            if (string.IsNullOrWhiteSpace(
                    packagePath) ||
                !File.Exists(
                    packagePath))
            {
                return new ResourcePackSelectionResult();
            }


            string instanceDirectory =
                _instanceService
                    .GetInstanceDirectory(
                        instance.Id);


            string packageFileName =
                Path.GetFileName(
                    packagePath);


            if (MarkerMatches(
                    instanceDirectory,
                    instance.InstalledVersion,
                    packageFileName))
            {
                return new ResourcePackSelectionResult
                {
                    AlreadyApplied =
                        true
                };
            }


            ResourcePackPreset? preset =
                ReadPresetFromArchive(
                    packagePath);


            if (preset == null ||
                string.IsNullOrWhiteSpace(
                    preset.ResourcePacksLine))
            {
                return new ResourcePackSelectionResult();
            }


            Directory.CreateDirectory(
                instanceDirectory);


            string destinationOptionsPath =
                Path.Combine(
                    instanceDirectory,
                    "options.txt");


            MergePresetIntoOptions(
                destinationOptionsPath,
                preset);


            List<string> missingPacks =
                FindMissingResourcePacks(
                    instanceDirectory,
                    preset);


            // Si todos los packs seleccionados existen, marcamos el preset
            // como aplicado para no volver a forzar la selección en cada
            // inicio. Si falta alguno, se volverá a comprobar la próxima vez.
            if (missingPacks.Count ==
                0)
            {
                SaveMarker(
                    instanceDirectory,
                    instance.InstalledVersion,
                    packageFileName);
            }


            return new ResourcePackSelectionResult
            {
                Applied =
                    true,

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
                        File.GetLastWriteTimeUtc(path))
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
                                            character == '/'))
                        .FirstOrDefault();


                if (optionsEntry == null)
                {
                    return null;
                }


                using Stream stream =
                    optionsEntry.Open();

                using StreamReader reader =
                    new StreamReader(
                        stream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: true);


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


                if (string.IsNullOrWhiteSpace(
                        resourcePacksLine))
                {
                    return null;
                }


                return new ResourcePackPreset
                {
                    ResourcePacksLine =
                        resourcePacksLine,

                    IncompatibleResourcePacksLine =
                        incompatibleLine,

                    SelectedFilePacks =
                        ParseSelectedFilePacks(
                            resourcePacksLine)
                };
            }
            catch
            {
                return null;
            }
        }


        private static void MergePresetIntoOptions(
            string optionsPath,
            ResourcePackPreset preset)
        {
            List<string> lines =
                File.Exists(optionsPath)
                    ? SplitLines(
                            File.ReadAllText(
                                optionsPath))
                        .ToList()
                    : new List<string>();


            ReplaceOrAppendLine(
                lines,
                "resourcePacks:",
                preset.ResourcePacksLine);


            if (!string.IsNullOrWhiteSpace(
                    preset.IncompatibleResourcePacksLine))
            {
                ReplaceOrAppendLine(
                    lines,
                    "incompatibleResourcePacks:",
                    preset.IncompatibleResourcePacksLine);
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


            File.WriteAllText(
                optionsPath,
                output,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
        }


        private static void ReplaceOrAppendLine(
            List<string> lines,
            string prefix,
            string replacement)
        {
            for (int index = 0;
                 index < lines.Count;
                 index++)
            {
                if (!lines[index]
                        .StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                lines[index] =
                    replacement;

                return;
            }


            lines.Add(
                replacement);
        }


        private static List<string> ParseSelectedFilePacks(
            string resourcePacksLine)
        {
            int separatorIndex =
                resourcePacksLine.IndexOf(':');


            if (separatorIndex < 0 ||
                separatorIndex >=
                resourcePacksLine.Length - 1)
            {
                return new List<string>();
            }


            string json =
                resourcePacksLine[(separatorIndex + 1)..]
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
                            value) ||
                        !value.StartsWith(
                            "file/",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }


                    string relative =
                        value[5..]
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar)
                            .Replace(
                                '\\',
                                Path.DirectorySeparatorChar);


                    if (!string.IsNullOrWhiteSpace(
                            relative))
                    {
                        result.Add(
                            relative);
                    }
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


            foreach (string relativePack in
                preset.SelectedFilePacks)
            {
                string candidate =
                    Path.GetFullPath(
                        Path.Combine(
                            resourcePacksDirectory,
                            relativePack));


                string root =
                    Path.GetFullPath(
                        resourcePacksDirectory) +
                    Path.DirectorySeparatorChar;


                if (!candidate.StartsWith(
                        root,
                        StringComparison.OrdinalIgnoreCase))
                {
                    missing.Add(
                        relativePack);

                    continue;
                }


                if (!File.Exists(candidate) &&
                    !Directory.Exists(candidate))
                {
                    missing.Add(
                        relativePack);
                }
            }


            return missing;
        }


        private static bool MarkerMatches(
            string instanceDirectory,
            string installedVersion,
            string packageFileName)
        {
            string markerPath =
                Path.Combine(
                    instanceDirectory,
                    MarkerFileName);


            if (!File.Exists(
                    markerPath))
            {
                return false;
            }


            try
            {
                AppliedMarker? marker =
                    JsonSerializer.Deserialize<AppliedMarker>(
                        File.ReadAllText(
                            markerPath));


                return marker != null &&
                    string.Equals(
                        marker.InstalledVersion,
                        installedVersion,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        marker.PackageFileName,
                        packageFileName,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
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
                            installedVersion,

                        PackageFileName =
                            packageFileName
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
                        encoderShouldEmitUTF8Identifier: false));
            }
            catch
            {
            }
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
