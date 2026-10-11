using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace NegativeLauncher.AvaloniaPrototype.Converters
{
    /// <summary>Recorta la cara y la capa exterior de una skin de Minecraft.</summary>
    public sealed class MinecraftSkinLayerConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, Bitmap> SourceBitmaps = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, CroppedBitmap> CroppedImages = new(StringComparer.Ordinal);

        public string Layer { get; set; } = "face";

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(path);
                Bitmap bitmap = SourceBitmaps.GetOrAdd(fullPath, key => new Bitmap(key));
                if (bitmap.PixelSize.Width < 48 || bitmap.PixelSize.Height < 16)
                    return null;

                bool hat = string.Equals(Layer, "hat", StringComparison.OrdinalIgnoreCase);
                string cropKey = fullPath + (hat ? "|hat" : "|face");
                PixelRect sourceRect = hat ? new PixelRect(40, 8, 8, 8) : new PixelRect(8, 8, 8, 8);
                return CroppedImages.GetOrAdd(cropKey, _ => new CroppedBitmap(bitmap, sourceRect));
            }
            catch
            {
                // Una imagen inválida deja el icono de reserva y no rompe la lista de cuentas.
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
