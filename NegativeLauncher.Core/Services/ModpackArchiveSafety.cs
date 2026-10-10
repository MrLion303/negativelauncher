using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace Negative_Client.Services
{
    /// <summary>
    /// Reúne las operaciones de archivo y validación de rutas que no dependen de la interfaz.
    /// </summary>
    public static class ModpackArchiveSafety
    {
        public static void ValidateZip(string zipPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            _ = archive.Entries.Count;
        }

        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            string safe = new string(value.Where(character => !invalidCharacters.Contains(character)).ToArray());

            return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
        }

        public static void ExtractZipSafely(
            string zipPath,
            string destinationDirectory,
            CancellationToken cancellationToken)
        {
            string destinationRoot = Path.GetFullPath(destinationDirectory);

            using ZipArchive archive = ZipFile.OpenRead(zipPath);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ZIP usa '/', pero algunos archivos creados en Windows contienen '\\'.
                // Normalizamos ambos separadores para que la validación sea idéntica en cada SO.
                string entryPath = entry.FullName.Replace('\\', '/')
                    .Replace('/', Path.DirectorySeparatorChar);
                string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entryPath));

                if (!LauncherPathRules.IsSameOrSubPath(destinationPath, destinationRoot))
                {
                    throw new InvalidOperationException("El ZIP contiene una ruta no segura.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                string? parent = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }

        public static string CombineRelativePath(string rootDirectory, string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/')
                .Replace('/', Path.DirectorySeparatorChar);
            string combined = Path.GetFullPath(Path.Combine(rootDirectory, normalized));

            if (!LauncherPathRules.IsSameOrSubPath(combined, rootDirectory))
            {
                throw new InvalidOperationException("Se detectó una ruta de archivo no segura.");
            }

            return combined;
        }

        public static string NormalizeRelativePath(string path)
        {
            return path
                .Replace('\\', '/')
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .TrimStart('/');
        }
    }
}