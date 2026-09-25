using System;

namespace Negative_Client.Models
{
    public sealed class OfflineAccountProfile
    {
        public string Username { get; set; } =
            string.Empty;

        public string SkinFilePath { get; set; } =
            string.Empty;

        // "wide" = modelo clásico / brazos normales.
        // "slim" = modelo delgado.
        public string SkinModel { get; set; } =
            "wide";

        public DateTime SavedAtUtc { get; set; } =
            DateTime.UtcNow;
    }
}
