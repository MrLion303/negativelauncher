using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Negative_Client.Services
{
    /// <summary>
    /// Copia directorios entre ubicaciones manteniendo el origen intacto si la copia falla.
    /// La migración rechaza enlaces simbólicos y puntos de reanálisis para no seguir rutas
    /// fuera del árbol elegido ni cambiar sus destinos durante una copia entre volúmenes.
    /// </summary>
    public static class DirectoryMigrationHelper
    {
        public static void EnsureTreeContainsNoReparsePoints(string rootDirectory)
        {
            string root = RequireDirectory(rootDirectory);
            _ = EnumerateTree(root);
        }

        public static IReadOnlyList<string> EnumerateFilesWithoutReparsePoints(string rootDirectory)
        {
            string root = RequireDirectory(rootDirectory);
            return EnumerateTree(root).Files;
        }

        public static IReadOnlyList<string> EnumerateDirectoriesWithoutReparsePoints(string rootDirectory)
        {
            string root = RequireDirectory(rootDirectory);
            return EnumerateTree(root).Directories;
        }

        /// <summary>
        /// Rechaza enlaces existentes entre una raíz controlada por el launcher y una ruta destino.
        /// Los padres de la raíz pueden ser enlaces propios del sistema; el árbol administrado no.
        /// </summary>
        public static void EnsureNoReparsePointsUnderRoot(string rootDirectory, string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("La carpeta raíz no puede estar vacía.", nameof(rootDirectory));
            if (string.IsNullOrWhiteSpace(candidatePath))
                throw new ArgumentException("La ruta candidata no puede estar vacía.", nameof(candidatePath));

            string root = Path.GetFullPath(rootDirectory);
            string candidate = Path.GetFullPath(candidatePath);
            if (!LauncherPathRules.IsSameOrSubPath(candidate, root))
                throw new InvalidOperationException("La ruta de destino sale de la carpeta administrada.");

            EnsureExistingEntryIsNotReparsePoint(root);
            string relative = Path.GetRelativePath(root, candidate);
            if (relative == ".")
                return;

            string current = root;
            foreach (string segment in relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                EnsureExistingEntryIsNotReparsePoint(current);
            }
        }

        public static void CopyDirectoryContents(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("La carpeta de origen no puede estar vacía.", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("La carpeta de destino no puede estar vacía.", nameof(destinationPath));

            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);

            if (LauncherPathRules.IsSameOrSubPath(source, destination) ||
                LauncherPathRules.IsSameOrSubPath(destination, source))
            {
                throw new InvalidOperationException(
                    "El origen y el destino de la copia no pueden solaparse.");
            }

            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException("No se encontró la carpeta que se intenta copiar.");

            // Analizar todo el árbol antes de crear, vaciar o modificar el destino.
            // Si se encuentra un enlace, no se toca ninguna de las dos ubicaciones.
            DirectoryTree tree = EnumerateTree(source);

            if (EntryExists(destination))
            {
                FileAttributes destinationAttributes = GetAttributesRejectReparsePoint(destination);
                if ((destinationAttributes & FileAttributes.Directory) == 0)
                    throw new IOException("La carpeta de destino existe y no es un directorio.");

                if (Directory.EnumerateFileSystemEntries(destination).Any())
                    throw new IOException("La carpeta de destino ya contiene archivos.");

                Directory.Delete(destination, recursive: false);
            }

            try
            {
                Directory.CreateDirectory(destination);
                EnsureExistingEntryIsNotReparsePoint(destination);

                foreach (string sourceDirectory in tree.Directories)
                {
                    GetAttributesRejectReparsePoint(sourceDirectory);
                    string relative = Path.GetRelativePath(source, sourceDirectory);
                    string targetDirectory = Path.GetFullPath(Path.Combine(destination, relative));
                    EnsureNoReparsePointsUnderRoot(destination, targetDirectory);
                    Directory.CreateDirectory(targetDirectory);
                }

                foreach (string sourceFile in tree.Files)
                {
                    GetAttributesRejectReparsePoint(sourceFile);
                    string relative = Path.GetRelativePath(source, sourceFile);
                    string targetFile = Path.GetFullPath(Path.Combine(destination, relative));
                    EnsureNoReparsePointsUnderRoot(destination, targetFile);

                    string? parent = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);

                    File.Copy(sourceFile, targetFile, overwrite: false);
                    File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
                    if (!OperatingSystem.IsWindows())
                        File.SetUnixFileMode(targetFile, File.GetUnixFileMode(sourceFile));
                }

                if (!OperatingSystem.IsWindows())
                {
                    // Restaurar permisos al final, después de crear todo el contenido.
                    foreach (string sourceDirectory in tree.Directories
                        .Append(source)
                        .OrderByDescending(path => path.Length))
                    {
                        GetAttributesRejectReparsePoint(sourceDirectory);
                        string relative = Path.GetRelativePath(source, sourceDirectory);
                        string targetDirectory = relative == "."
                            ? destination
                            : Path.Combine(destination, relative);

                        EnsureNoReparsePointsUnderRoot(destination, targetDirectory);
                        File.SetUnixFileMode(targetDirectory, File.GetUnixFileMode(sourceDirectory));
                        Directory.SetLastWriteTimeUtc(
                            targetDirectory,
                            Directory.GetLastWriteTimeUtc(sourceDirectory));
                    }
                }
            }
            catch
            {
                try
                {
                    if (Directory.Exists(destination))
                        Directory.Delete(destination, recursive: true);
                }
                catch
                {
                    // No ocultar la excepción original: el origen permanece intacto.
                }

                throw;
            }
        }

        private static string RequireDirectory(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("La carpeta raíz no puede estar vacía.", nameof(rootDirectory));

            string root = Path.GetFullPath(rootDirectory);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("No se encontró la carpeta que se intenta recorrer.");

            GetAttributesRejectReparsePoint(root);
            return root;
        }

        private static DirectoryTree EnumerateTree(string root)
        {
            GetAttributesRejectReparsePoint(root);
            var directories = new List<string>();
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                foreach (string entry in Directory.EnumerateFileSystemEntries(current))
                {
                    FileAttributes attributes = GetAttributesRejectReparsePoint(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        directories.Add(entry);
                        pending.Push(entry);
                    }
                    else
                    {
                        files.Add(entry);
                    }
                }
            }

            return new DirectoryTree(directories, files);
        }

        private static FileAttributes GetAttributesRejectReparsePoint(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "La migración encontró un enlace simbólico o punto de reanálisis; " +
                    "no se modificó el árbol para evitar copiar datos externos: " + path);
            }

            return attributes;
        }

        private static bool EntryExists(string path)
        {
            try
            {
                _ = File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static void EnsureExistingEntryIsNotReparsePoint(string path)
        {
            if (EntryExists(path))
                GetAttributesRejectReparsePoint(path);
        }

        private sealed record DirectoryTree(List<string> Directories, List<string> Files);
    }
}
