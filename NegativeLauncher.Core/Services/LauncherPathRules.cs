using System;
using System.Collections.Concurrent;
using System.IO;

namespace Negative_Client.Services
{
    /// <summary>
    /// Comparaciones de rutas basadas en el comportamiento del sistema de archivos real.
    /// </summary>
    public static class LauncherPathRules
    {
        private static readonly ConcurrentDictionary<string, StringComparison> PathSemantics =
            new(StringComparer.Ordinal);

        public static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("La ruta no puede estar vacía.", nameof(path));

            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath) ?? string.Empty;
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                return root;

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                NormalizeFullPath(left),
                NormalizeFullPath(right),
                GetPathComparison(left));
        }

        /// <summary>
        /// Valor predeterminado cuando todavía no hay una ruta concreta.
        /// Las operaciones sobre archivos deben utilizar GetPathComparison(path).
        /// </summary>
        public static StringComparison PathComparison =>
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        public static StringComparer PathComparer =>
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        /// <summary>
        /// Detecta la sensibilidad a mayúsculas del volumen donde vive la ruta.
        /// El resultado se almacena por carpeta existente, porque macOS permite
        /// volúmenes sensibles y no sensibles dentro del mismo sistema.
        /// </summary>
        public static StringComparison GetPathComparison(string path)
        {
            if (OperatingSystem.IsWindows())
                return StringComparison.OrdinalIgnoreCase;

            string directory = FindExistingDirectory(path);
            if (string.IsNullOrWhiteSpace(directory))
                return PathComparison;

            string cacheKey = NormalizeFullPath(directory);
            return PathSemantics.GetOrAdd(cacheKey, _ => DetectPathComparison(directory));
        }

        public static StringComparer GetPathComparerForPath(string path)
        {
            return GetPathComparison(path) == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        private static string FindExistingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string fullPath;
            try { fullPath = Path.GetFullPath(path); }
            catch { return string.Empty; }

            if (Directory.Exists(fullPath))
                return fullPath;

            string? directory = File.Exists(fullPath)
                ? Path.GetDirectoryName(fullPath)
                : fullPath;

            while (!string.IsNullOrWhiteSpace(directory))
            {
                if (Directory.Exists(directory))
                    return directory;

                string? parent;
                try { parent = Directory.GetParent(directory)?.FullName; }
                catch { return string.Empty; }

                if (string.Equals(parent, directory, StringComparison.Ordinal))
                    break;

                directory = parent;
            }

            return string.Empty;
        }

        private static StringComparison DetectPathComparison(string directory)
        {
            string originalPath = string.Empty;
            try
            {
                string fileName = ".negative-case-probe-" + Guid.NewGuid().ToString("N") + ".tmp";
                originalPath = Path.Combine(directory, fileName);
                string alternatePath = Path.Combine(
                    directory,
                    fileName.ToLowerInvariant().Replace(".tmp", ".TMP", StringComparison.Ordinal));

                using (new FileStream(originalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }

                return File.Exists(alternatePath)
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
            }
            catch
            {
                // Si el directorio no permite la prueba, usa el comportamiento predeterminado.
                return OperatingSystem.IsMacOS()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalPath))
                {
                    try { if (File.Exists(originalPath)) File.Delete(originalPath); }
                    catch { /* La limpieza de la prueba no debe afectar al launcher. */ }
                }
            }
        }

        public static void ValidateInstanceId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new InvalidOperationException("La instancia no tiene ID.");

            foreach (char character in instanceId)
            {
                bool valid = char.IsLetterOrDigit(character) ||
                    character == '-' || character == '_';
                if (!valid)
                    throw new InvalidOperationException("El ID de la instancia contiene caracteres no permitidos.");
            }
        }

        public static void ValidateStorageRootChange(
            string newRoot,
            string oldInstancesRoot,
            string oldMinecraftRoot)
        {
            if (IsSameOrSubPath(newRoot, oldInstancesRoot) ||
                IsSameOrSubPath(newRoot, oldMinecraftRoot))
                throw new InvalidOperationException(
                    "La nueva ubicación no puede estar dentro de las carpetas " +
                    "de instalaciones o Minecraft actuales.");
        }

        public static bool IsSameOrSubPath(string candidate, string parent)
        {
            string fullCandidate = NormalizeFullPath(candidate);
            string fullParent = NormalizeFullPath(parent);

            if (PathsEqual(fullCandidate, fullParent))
                return true;

            string parentPrefix = fullParent.EndsWith(Path.DirectorySeparatorChar) ||
                fullParent.EndsWith(Path.AltDirectorySeparatorChar)
                ? fullParent
                : fullParent + Path.DirectorySeparatorChar;

            return fullCandidate.StartsWith(parentPrefix, GetPathComparison(parent));
        }
    }
}
