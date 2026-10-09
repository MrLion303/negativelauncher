using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    /// <summary>
    /// Resuelve códigos de instalación usando un catálogo y un descargador proporcionado por la aplicación.
    /// No depende de la interfaz ni del proveedor de almacenamiento remoto.
    /// </summary>
    public sealed class ModpackCatalogReader
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<ModpackManifest?> FindByCodeAsync(
            string installCode,
            string catalogFileId,
            Func<string, CancellationToken, Task<string>> downloadTextFileAsync,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(installCode))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(catalogFileId))
            {
                throw new ArgumentException("El ID del catálogo es obligatorio.", nameof(catalogFileId));
            }

            ArgumentNullException.ThrowIfNull(downloadTextFileAsync);

            string catalogJson = await downloadTextFileAsync(catalogFileId, cancellationToken);
            ModpackCatalog? catalog = JsonSerializer.Deserialize<ModpackCatalog>(catalogJson, _jsonOptions);

            if (catalog == null)
            {
                throw new InvalidOperationException("El catálogo de modpacks está vacío.");
            }

            if (catalog.Schema != 1)
            {
                throw new InvalidOperationException($"Versión de catálogo no compatible: {catalog.Schema}");
            }

            ModpackCatalogEntry? packEntry = catalog.Packs.FirstOrDefault(pack =>
                string.Equals(pack.Code, installCode, StringComparison.OrdinalIgnoreCase));

            if (packEntry == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(packEntry.ManifestFileId))
            {
                throw new InvalidOperationException($"El modpack '{packEntry.Id}' no tiene manifestFileId.");
            }

            string manifestJson = await downloadTextFileAsync(packEntry.ManifestFileId, cancellationToken);
            ModpackManifest? manifest = JsonSerializer.Deserialize<ModpackManifest>(manifestJson, _jsonOptions);

            if (manifest == null)
            {
                throw new InvalidOperationException("No se pudo leer el manifest del modpack.");
            }

            if (string.IsNullOrWhiteSpace(manifest.Id) ||
                string.IsNullOrWhiteSpace(manifest.Name) ||
                string.IsNullOrWhiteSpace(manifest.MinecraftVersion))
            {
                throw new InvalidOperationException("El manifest del modpack está incompleto.");
            }

            if (!string.Equals(manifest.Id, packEntry.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El ID del catálogo no coincide con el ID del manifest.");
            }

            return manifest;
        }
    }
}