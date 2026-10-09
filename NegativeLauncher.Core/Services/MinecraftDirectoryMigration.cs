using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Negative_Client.Services
{
    /// <summary>
    /// Migra los archivos compartibles de instalaciones antiguas al almacenamiento común.
    /// El directorio de mods, configuraciones y mundos de cada instancia no se toca.
    /// </summary>
    public sealed class MinecraftDirectoryMigration
    {
        private static readonly object MigrationSync = new object();

        private readonly string _assetsRoot;
        private readonly string _librariesRoot;
        private readonly string _versionsRoot;
        private readonly string _runtimeRoot;

        public MinecraftDirectoryMigration(
            string assetsRoot,
            string librariesRoot,
            string versionsRoot,
            string runtimeRoot)
        {
            _assetsRoot = RequirePath(assetsRoot, nameof(assetsRoot));
            _librariesRoot = RequirePath(librariesRoot, nameof(librariesRoot));
            _versionsRoot = RequirePath(versionsRoot, nameof(versionsRoot));
            _runtimeRoot = RequirePath(runtimeRoot, nameof(runtimeRoot));
        }

        public void MigrateLegacyRuntimeForDirectory(string instanceDirectory)
        {
            if (string.IsNullOrWhiteSpace(instanceDirectory) ||
                !Directory.Exists(instanceDirectory))
            {
                return;
            }

            lock (MigrationSync)
            {
                MergeDirectoryIntoShared(Path.Combine(instanceDirectory, "assets"), _assetsRoot);
                MergeDirectoryIntoShared(Path.Combine(instanceDirectory, "libraries"), _librariesRoot);
                MergeDirectoryIntoShared(Path.Combine(instanceDirectory, "versions"), _versionsRoot);
                MergeDirectoryIntoShared(Path.Combine(instanceDirectory, "runtime"), _runtimeRoot);
            }
        }

        private static void MergeDirectoryIntoShared(
            string sourceDirectory,
            string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            Directory.CreateDirectory(destinationDirectory);

            List<string> sourceFiles = Directory
                .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .ToList();

            foreach (string sourceFile in sourceFiles)
            {
                string relative = Path.GetRelativePath(sourceDirectory, sourceFile);
                string destinationFile = Path.GetFullPath(Path.Combine(destinationDirectory, relative));
                string destinationRoot = Path.GetFullPath(destinationDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (!destinationFile.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Se detectó una ruta no segura al migrar Minecraft.");
                }

                // Si ya hay una copia compartida, se conserva y el original no se elimina.
                if (File.Exists(destinationFile))
                {
                    continue;
                }

                string? destinationParent = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrWhiteSpace(destinationParent))
                {
                    Directory.CreateDirectory(destinationParent);
                }

                try
                {
                    File.Move(sourceFile, destinationFile);
                }
                catch (IOException)
                {
                    File.Copy(sourceFile, destinationFile, overwrite: false);
                    File.Delete(sourceFile);
                }
            }

            DeleteEmptyDirectories(sourceDirectory);
        }

        private static void DeleteEmptyDirectories(string rootDirectory)
        {
            if (!Directory.Exists(rootDirectory))
            {
                return;
            }

            foreach (string directory in Directory
                .EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
                .OrderByDescending(directory => directory.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory, recursive: false);
                    }
                }
                catch
                {
                }
            }

            try
            {
                if (Directory.Exists(rootDirectory) &&
                    !Directory.EnumerateFileSystemEntries(rootDirectory).Any())
                {
                    Directory.Delete(rootDirectory, recursive: false);
                }
            }
            catch
            {
            }
        }

        private static string RequirePath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("La carpeta no puede estar vacía.", parameterName);
            }

            return Path.GetFullPath(path);
        }
    }
}
