using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    /// <summary>
    /// Recorre las capturas activas y archivadas sin depender de WPF o Avalonia.
    /// </summary>
    public sealed class ScreenshotGalleryScanner
    {
        private readonly InstanceService _instanceService;
        private readonly ScreenshotArchiveStore _archiveStore;

        public ScreenshotGalleryScanner(
            InstanceService instanceService,
            ScreenshotArchiveStore archiveStore)
        {
            _instanceService = instanceService ??
                throw new ArgumentNullException(nameof(instanceService));
            _archiveStore = archiveStore ??
                throw new ArgumentNullException(nameof(archiveStore));
        }

        public Task<IReadOnlyList<ScreenshotGalleryFile>> LoadAllAsync(
            IEnumerable<InstalledInstance> instances,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(instances);
            InstalledInstance[] snapshot = instances
                .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Id))
                .ToArray();

            return Task.Run<IReadOnlyList<ScreenshotGalleryFile>>(
                () => Scan(snapshot, cancellationToken),
                cancellationToken);
        }

        private IReadOnlyList<ScreenshotGalleryFile> Scan(
            IEnumerable<InstalledInstance> instances,
            CancellationToken cancellationToken)
        {
            List<ScreenshotGalleryFile> results = new();

            foreach (InstalledInstance instance in instances)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string instanceDirectory = _instanceService.GetInstanceDirectory(instance.Id);
                    string screenshotsDirectory = Path.Combine(instanceDirectory, "screenshots");

                    AddImagesFromDirectory(
                        results,
                        screenshotsDirectory,
                        instance.Id,
                        instance.Name,
                        isArchived: false,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                {
                    // Una instalación inaccesible no debe impedir cargar las capturas restantes.
                }
            }

            foreach (ScreenshotArchiveInfo archive in _archiveStore.GetArchives())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(archive.ScreenshotsDirectory))
                    continue;

                AddImagesFromDirectory(
                    results,
                    archive.ScreenshotsDirectory,
                    archive.InstanceId,
                    archive.InstanceName,
                    isArchived: true,
                    cancellationToken);
            }

            return results;
        }

        private static void AddImagesFromDirectory(
            List<ScreenshotGalleryFile> results,
            string screenshotsDirectory,
            string instanceId,
            string instanceName,
            bool isArchived,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(screenshotsDirectory))
                return;

            Stack<string> pending = new();
            pending.Push(screenshotsDirectory);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                string[] files;
                string[] subdirectories;

                try
                {
                    files = Directory.GetFiles(directory);
                    subdirectories = Directory.GetDirectories(directory);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsSupportedImage(file))
                        continue;

                    try
                    {
                        // Las capturas deben ser archivos normales, no enlaces hacia árboles externos.
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                            continue;

                        results.Add(new ScreenshotGalleryFile
                        {
                            InstanceId = instanceId,
                            InstanceName = instanceName,
                            FilePath = Path.GetFullPath(file),
                            CapturedAt = File.GetLastWriteTime(file),
                            IsArchived = isArchived
                        });
                    }
                    catch (IOException)
                    {
                        // El archivo pudo desaparecer durante el recorrido.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Omitir un archivo inaccesible no impide listar el resto.
                    }
                }

                foreach (string subdirectory in subdirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        // Evita seguir enlaces simbólicos y ciclos hacia árboles externos.
                        if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0)
                            pending.Push(subdirectory);
                    }
                    catch (IOException)
                    {
                        // Ignorar entradas que cambian durante el escaneo.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignorar una carpeta inaccesible y continuar.
                    }
                }
            }
        }

        private static bool IsSupportedImage(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
