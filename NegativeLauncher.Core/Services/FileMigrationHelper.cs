using System;
using System.IO;

namespace Negative_Client.Services
{
    /// <summary>
    /// Mueve archivos entre ubicaciones y protege el origen si la copia falla.
    /// </summary>
    public static class FileMigrationHelper
    {
        public static void MoveFile(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("La ruta de origen no puede estar vacía.", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("La ruta de destino no puede estar vacía.", nameof(destinationPath));

            string source = Path.GetFullPath(sourcePath);
            string destination = Path.GetFullPath(destinationPath);
            if (LauncherPathRules.PathsEqual(source, destination))
                return;

            if (!File.Exists(source))
                throw new FileNotFoundException("No se encontró el archivo que se intenta migrar.", source);

            FileAttributes sourceAttributes = File.GetAttributes(source);
            if ((sourceAttributes & FileAttributes.ReparsePoint) != 0 ||
                (sourceAttributes & FileAttributes.Directory) != 0)
            {
                throw new IOException(
                    "La migración de archivos solo admite archivos normales; " +
                    "no sigue enlaces simbólicos ni puntos de reanálisis.");
            }

            string? destinationDirectory = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new InvalidOperationException("La carpeta de destino no es válida.");

            Directory.CreateDirectory(destinationDirectory);
            DirectoryMigrationHelper.EnsureNoReparsePointsUnderRoot(destinationDirectory, destination);

            try
            {
                File.Move(source, destination);
                return;
            }
            catch (IOException) when (File.Exists(source) && !File.Exists(destination))
            {
                // Una operación de rename puede fallar entre volúmenes en Unix.
                // Copiamos a un temporal en el destino y lo renombramos solo al completar.
            }

            string temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".migration.tmp";
            try
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(source);
                UnixFileMode? unixMode = OperatingSystem.IsWindows()
                    ? null
                    : File.GetUnixFileMode(source);

                File.Copy(source, temporaryPath, overwrite: false);
                File.SetLastWriteTimeUtc(temporaryPath, lastWriteTimeUtc);
                if (!OperatingSystem.IsWindows() && unixMode.HasValue)
                    File.SetUnixFileMode(temporaryPath, unixMode.Value);

                File.Move(temporaryPath, destination);
                File.Delete(source);
            }
            catch
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                    // La copia parcial es temporal; se conserva el error que causó el fallo.
                }

                throw;
            }
        }
    }
}
