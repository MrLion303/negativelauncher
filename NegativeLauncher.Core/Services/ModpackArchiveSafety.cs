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
        // Evita que un ZIP pequeño fuerce millones de operaciones o expanda hasta
        // agotar el disco. Los límites son amplios para modpacks reales.
        private const int MaxArchiveEntries = 100_000;
        private const long MaxEntryUncompressedBytes = 2L * 1024 * 1024 * 1024;
        private const long MaxArchiveUncompressedBytes = 8L * 1024 * 1024 * 1024;
        public static void ValidateZip(string zipPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ValidateArchiveLimits(archive);
        }

        private static void ValidateArchiveLimits(ZipArchive archive)
        {
            if (archive.Entries.Count > MaxArchiveEntries)
                throw new InvalidOperationException("El ZIP contiene demasiados archivos.");

            long totalUncompressedBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.Length < 0 || entry.Length > MaxEntryUncompressedBytes)
                    throw new InvalidOperationException("El ZIP contiene un archivo que supera el tamaño permitido.");

                if (entry.Length > MaxArchiveUncompressedBytes - totalUncompressedBytes)
                    throw new InvalidOperationException("El contenido descomprimido del ZIP supera el tamaño permitido.");

                totalUncompressedBytes += entry.Length;
            }
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
            Directory.CreateDirectory(destinationRoot);
            RejectReparsePoint(destinationRoot);

            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ValidateArchiveLimits(archive);

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

                // La validación léxica no basta si una carpeta existente dentro del
                // destino es un enlace a otra ubicación: la escritura podría escapar.
                RejectReparsePointPath(destinationRoot, destinationPath);

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

        private static void RejectReparsePointPath(string rootDirectory, string destinationPath)
        {
            string relativePath = Path.GetRelativePath(rootDirectory, destinationPath);
            if (relativePath == ".")
            {
                RejectReparsePoint(rootDirectory);
                return;
            }

            string currentPath = rootDirectory;
            foreach (string segment in relativePath.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                currentPath = Path.Combine(currentPath, segment);
                RejectReparsePoint(currentPath);
            }
        }

        private static void RejectReparsePoint(string path)
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);

            // LinkTarget también detecta enlaces rotos que Exists no reconoce.
            if (info.LinkTarget is not null ||
                (info.Exists && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
            {
                throw new InvalidOperationException(
                    "La extracción se detuvo porque la ruta de destino contiene un enlace simbólico o punto de reanálisis.");
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