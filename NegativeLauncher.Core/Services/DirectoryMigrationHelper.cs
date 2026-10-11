using System;
using System.IO;
using System.Linq;

namespace Negative_Client.Services
{
    /// <summary>
    /// Copia directorios entre ubicaciones manteniendo el origen intacto si la copia falla.
    /// </summary>
    public static class DirectoryMigrationHelper
    {
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

            if (Directory.Exists(destination))
            {
                if (Directory.EnumerateFileSystemEntries(destination).Any())
                    throw new IOException("La carpeta de destino ya contiene archivos.");

                Directory.Delete(destination, recursive: false);
            }

            try
            {
                Directory.CreateDirectory(destination);

                string[] sourceDirectories = Directory.EnumerateDirectories(
                    source, "*", SearchOption.AllDirectories).ToArray();

                foreach (string directory in sourceDirectories)
                {
                    string relative = Path.GetRelativePath(source, directory);
                    string targetDirectory = Path.GetFullPath(Path.Combine(destination, relative));
                    if (!LauncherPathRules.IsSameOrSubPath(targetDirectory, destination))
                        throw new InvalidOperationException("La copia detectó una subruta no segura.");

                    Directory.CreateDirectory(targetDirectory);
                }

                foreach (string sourceFile in Directory.EnumerateFiles(
                    source, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(source, sourceFile);
                    string targetFile = Path.GetFullPath(Path.Combine(destination, relative));
                    if (!LauncherPathRules.IsSameOrSubPath(targetFile, destination))
                        throw new InvalidOperationException("La copia detectó una ruta de archivo no segura.");

                    string? parent = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);

                    File.Copy(sourceFile, targetFile, overwrite: false);

                    // El runtime de Java y otros ejecutables necesitan conservar sus bits
                    // de ejecución cuando el cambio de volumen obliga a copiar en vez de renombrar.
                    File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
                    if (!OperatingSystem.IsWindows())
                        File.SetUnixFileMode(targetFile, File.GetUnixFileMode(sourceFile));
                }

                if (!OperatingSystem.IsWindows())
                {
                    // Restaurar permisos y marcas de tiempo de carpetas al final, después
                    // de crear todo el contenido, para no bloquear la propia copia.
                    foreach (string sourceDirectory in sourceDirectories
                        .Append(source)
                        .OrderByDescending(path => path.Length))
                    {
                        string relative = Path.GetRelativePath(source, sourceDirectory);
                        string targetDirectory = relative == "."
                            ? destination
                            : Path.Combine(destination, relative);

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
    }
}
