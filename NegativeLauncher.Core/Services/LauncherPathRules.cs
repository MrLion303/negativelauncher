namespace Negative_Client.Services
{
    /// <summary>
    /// Comparaciones de rutas usadas al validar cambios de almacenamiento.
    /// Esta clase no modifica el sistema de archivos.
    /// </summary>
    public static class LauncherPathRules
    {
        public static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                PathComparison);
        }

        /// <summary>
        /// Usa la semántica de comparación habitual del sistema operativo.
        /// Linux y macOS pueden usar volúmenes sensibles a mayúsculas; en
        /// sistemas Unix no se deben fusionar rutas solo por su capitalización.
        /// </summary>
        public static StringComparison PathComparison =>
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        public static StringComparer PathComparer =>
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        public static void ValidateInstanceId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new InvalidOperationException("La instancia no tiene ID.");
            }

            foreach (char character in instanceId)
            {
                bool valid = char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_';

                if (!valid)
                {
                    throw new InvalidOperationException(
                        "El ID de la instancia contiene caracteres no permitidos.");
                }
            }
        }

        public static void ValidateStorageRootChange(
            string newRoot,
            string oldInstancesRoot,
            string oldMinecraftRoot)
        {
            if (IsSameOrSubPath(newRoot, oldInstancesRoot) ||
                IsSameOrSubPath(newRoot, oldMinecraftRoot))
            {
                throw new InvalidOperationException(
                    "La nueva ubicación no puede estar dentro de las carpetas " +
                    "de instalaciones o Minecraft actuales.");
            }
        }

        public static bool IsSameOrSubPath(string candidate, string parent)
        {
            string fullCandidate = Path.GetFullPath(candidate).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            string fullParent = Path.GetFullPath(parent).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return fullCandidate.StartsWith(fullParent, PathComparison);
        }
    }
}
