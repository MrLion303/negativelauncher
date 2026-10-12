using System;
using System.IO;

namespace Negative_Client.Services
{
    /// <summary>
    /// Garantiza que un ejecutable de Java pueda iniciarse en sistemas Unix.
    /// </summary>
    public static class JavaExecutableHelper
    {
        public static string? FindOnPath(string? pathValue, bool isWindows)
        {
            if (string.IsNullOrWhiteSpace(pathValue))
                return null;

            string executableName = isWindows ? "java.exe" : "java";
            foreach (string entry in pathValue.Split(Path.PathSeparator))
            {
                string directory = entry.Trim().Trim('"');
                if (directory.Length == 0)
                    continue;

                string candidate;
                try
                {
                    candidate = Path.Combine(directory, executableName);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        public static void EnsureExecutable(string javaPath)
        {
            if (string.IsNullOrWhiteSpace(javaPath))
                throw new ArgumentException("La ruta de Java no puede estar vacía.", nameof(javaPath));

            if (!File.Exists(javaPath))
                throw new FileNotFoundException("No se encontró el ejecutable de Java.", javaPath);

            if (OperatingSystem.IsWindows())
                return;

            try
            {
                // No cambiar permisos del destino real de un enlace del sistema
                // (por ejemplo, /usr/bin/java en Linux o macOS).
                if ((File.GetAttributes(javaPath) & FileAttributes.ReparsePoint) != 0)
                    return;

                UnixFileMode mode = File.GetUnixFileMode(javaPath);
                // Solo habilitar la ejecución para el propietario: no ampliar sin necesidad
                // los permisos de acceso de Java configurados por el usuario.
                UnixFileMode executableMode = mode | UnixFileMode.UserExecute;

                if (mode != executableMode)
                    File.SetUnixFileMode(javaPath, executableMode);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                throw new InvalidOperationException(
                    "No se pudieron habilitar los permisos de ejecución de Java en Linux o macOS. " +
                    "Comprueba los permisos del directorio de datos del launcher.",
                    ex);
            }
        }
    }
}
