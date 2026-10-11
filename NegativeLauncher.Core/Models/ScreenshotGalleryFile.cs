using System;
using System.IO;

namespace Negative_Client.Models
{
    /// <summary>
    /// Metadatos de una captura que pueden consumirse desde cualquier interfaz gráfica.
    /// </summary>
    public sealed class ScreenshotGalleryFile
    {
        public string InstanceId { get; init; } = string.Empty;

        public string InstanceName { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;

        public string FileName => Path.GetFileName(FilePath);

        public DateTime CapturedAt { get; init; }

        public bool IsArchived { get; init; }
    }
}
