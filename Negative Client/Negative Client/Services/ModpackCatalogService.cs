using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class ModpackCatalogService
    {
        /*
         * IMPORTANTE:
         *
         * Aquí debes poner el ID REAL
         * de catalog.json en Google Drive.
         */
        private const string CatalogFileId =
            "14PLsvXVZMzhsDWW9i0RKTT143qnMDk2Y";


        private readonly GoogleDriveService _driveService;


        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };


        public ModpackCatalogService()
        {
            _driveService = new GoogleDriveService();
        }


        public async Task<ModpackManifest?> FindByCodeAsync(
            string installCode)
        {
            if (string.IsNullOrWhiteSpace(installCode))
            {
                return null;
            }


            // ==========================================
            // 1. DESCARGAR CATÁLOGO
            // ==========================================

            string catalogJson =
                await _driveService.DownloadTextFileAsync(
                    CatalogFileId);


            ModpackCatalog? catalog =
                JsonSerializer.Deserialize<ModpackCatalog>(
                    catalogJson,
                    _jsonOptions);


            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "El catálogo de modpacks está vacío.");
            }


            if (catalog.Schema != 1)
            {
                throw new InvalidOperationException(
                    $"Versión de catálogo no compatible: " +
                    $"{catalog.Schema}");
            }


            // ==========================================
            // 2. BUSCAR EL CÓDIGO
            // ==========================================

            ModpackCatalogEntry? packEntry =
                catalog.Packs.FirstOrDefault(pack =>
                    string.Equals(
                        pack.Code,
                        installCode,
                        StringComparison.OrdinalIgnoreCase));


            if (packEntry == null)
            {
                return null;
            }


            if (string.IsNullOrWhiteSpace(
                    packEntry.ManifestFileId))
            {
                throw new InvalidOperationException(
                    $"El modpack '{packEntry.Id}' " +
                    "no tiene manifestFileId.");
            }


            // ==========================================
            // 3. DESCARGAR MANIFEST
            // ==========================================

            string manifestJson =
                await _driveService.DownloadTextFileAsync(
                    packEntry.ManifestFileId);


            ModpackManifest? manifest =
                JsonSerializer.Deserialize<ModpackManifest>(
                    manifestJson,
                    _jsonOptions);


            if (manifest == null)
            {
                throw new InvalidOperationException(
                    "No se pudo leer el manifest del modpack.");
            }


            // ==========================================
            // 4. VALIDACIÓN
            // ==========================================

            if (string.IsNullOrWhiteSpace(manifest.Id) ||
                string.IsNullOrWhiteSpace(manifest.Name) ||
                string.IsNullOrWhiteSpace(
                    manifest.MinecraftVersion))
            {
                throw new InvalidOperationException(
                    "El manifest del modpack está incompleto.");
            }


            if (!string.Equals(
                    manifest.Id,
                    packEntry.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "El ID del catálogo no coincide " +
                    "con el ID del manifest.");
            }


            return manifest;
        }
    }
}