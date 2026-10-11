using System;
using System.IO;

namespace Negative_Client.Services
{
    /// <summary>
    /// Garantiza que un ejecutable de Java pueda iniciarse en sistemas Unix.
    /// </summary>
    public static class JavaExecutableHelper
    {
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
