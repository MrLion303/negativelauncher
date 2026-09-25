using System.Collections.Generic;

namespace Negative_Client.Models
{
    public sealed class ModpackCatalog
    {
        public int Schema { get; set; }

        public List<ModpackCatalogEntry> Packs { get; set; } = new();
    }


    public sealed class ModpackCatalogEntry
    {
        public string Code { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        public string ManifestFileId { get; set; } = string.Empty;
    }
}