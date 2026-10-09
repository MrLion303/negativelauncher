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
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSameOrSubPath(string candidate, string parent)
        {
            string fullCandidate = Path.GetFullPath(candidate).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            string fullParent = Path.GetFullPath(parent).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return fullCandidate.StartsWith(fullParent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
