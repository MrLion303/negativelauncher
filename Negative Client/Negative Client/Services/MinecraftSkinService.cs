using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negative_Client.Services
{
    public sealed class MinecraftSkinService
    {
        private const int SourceHeadSize = 8;
        private const int OutputHeadSize = 96;


        private static readonly HttpClient HttpClient =
            new()
            {
                Timeout =
                    TimeSpan.FromSeconds(20)
            };


        private static readonly string CacheRoot =
            Path.Combine(
                InstanceService.LauncherRoot,
                "cache",
                "accounts");


        private static readonly TimeSpan CacheLifetime =
            TimeSpan.FromMinutes(30);


        public MinecraftSkinService()
        {
            Directory.CreateDirectory(
                CacheRoot);
        }


        public async Task<string?> GetHeadPathAsync(
            string uuid)
        {
            string normalizedUuid =
                NormalizeUuid(
                    uuid);


            if (string.IsNullOrWhiteSpace(
                    normalizedUuid))
            {
                return null;
            }


            string outputPath =
                Path.Combine(
                    CacheRoot,
                    normalizedUuid +
                    "-head.png");


            if (File.Exists(
                    outputPath))
            {
                DateTime ageReference =
                    File.GetLastWriteTimeUtc(
                        outputPath);


                if (DateTime.UtcNow -
                    ageReference <
                    CacheLifetime)
                {
                    return outputPath;
                }
            }


            try
            {
                string? skinUrl =
                    await GetSkinUrlAsync(
                        normalizedUuid);


                if (string.IsNullOrWhiteSpace(
                        skinUrl))
                {
                    return
                        File.Exists(outputPath)
                            ? outputPath
                            : null;
                }


                byte[] skinBytes =
                    await HttpClient
                        .GetByteArrayAsync(
                            skinUrl);


                CreateHeadPngNearestNeighbor(
                    skinBytes,
                    outputPath);


                return
                    File.Exists(outputPath)
                        ? outputPath
                        : null;
            }
            catch
            {
                return
                    File.Exists(outputPath)
                        ? outputPath
                        : null;
            }
        }


        private static async Task<string?>
            GetSkinUrlAsync(
                string normalizedUuid)
        {
            string profileUrl =
                "https://sessionserver.mojang.com/" +
                "session/minecraft/profile/" +
                Uri.EscapeDataString(
                    normalizedUuid) +
                "?unsigned=false";


            using HttpResponseMessage response =
                await HttpClient.GetAsync(
                    profileUrl);


            if (!response.IsSuccessStatusCode)
            {
                return null;
            }


            string json =
                await response.Content
                    .ReadAsStringAsync();


            using JsonDocument document =
                JsonDocument.Parse(
                    json);


            if (!document.RootElement
                    .TryGetProperty(
                        "properties",
                        out JsonElement properties) ||
                properties.ValueKind !=
                JsonValueKind.Array)
            {
                return null;
            }


            JsonElement texturesProperty =
                properties
                    .EnumerateArray()
                    .FirstOrDefault(
                        property =>
                            property.TryGetProperty(
                                "name",
                                out JsonElement name) &&
                            string.Equals(
                                name.GetString(),
                                "textures",
                                StringComparison.OrdinalIgnoreCase));


            if (texturesProperty.ValueKind ==
                JsonValueKind.Undefined ||
                !texturesProperty.TryGetProperty(
                    "value",
                    out JsonElement valueElement))
            {
                return null;
            }


            string? encodedTextures =
                valueElement.GetString();


            if (string.IsNullOrWhiteSpace(
                    encodedTextures))
            {
                return null;
            }


            string decodedTextures =
                Encoding.UTF8.GetString(
                    Convert.FromBase64String(
                        encodedTextures));


            using JsonDocument texturesDocument =
                JsonDocument.Parse(
                    decodedTextures);


            if (!texturesDocument.RootElement
                    .TryGetProperty(
                        "textures",
                        out JsonElement textures) ||
                !textures.TryGetProperty(
                    "SKIN",
                    out JsonElement skin) ||
                !skin.TryGetProperty(
                    "url",
                    out JsonElement url))
            {
                return null;
            }


            return
                url.GetString();
        }


        // =====================================================
        // CABEZA PIXEL-PERFECT
        // =====================================================

        private static void CreateHeadPngNearestNeighbor(
            byte[] skinBytes,
            string outputPath)
        {
            using MemoryStream input =
                new MemoryStream(
                    skinBytes);


            PngBitmapDecoder decoder =
                new PngBitmapDecoder(
                    input,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);


            BitmapSource original =
                decoder.Frames[0];


            if (original.PixelWidth < 48 ||
                original.PixelHeight < 16)
            {
                throw new InvalidDataException(
                    "La skin descargada no tiene un formato válido.");
            }


            FormatConvertedBitmap skin =
                new FormatConvertedBitmap(
                    original,
                    PixelFormats.Bgra32,
                    null,
                    0);


            int skinStride =
                skin.PixelWidth *
                4;


            byte[] skinPixels =
                new byte[
                    skinStride *
                    skin.PixelHeight];


            skin.CopyPixels(
                skinPixels,
                skinStride,
                0);


            // Primero componemos exactamente los 8x8 píxeles de la
            // cara + segunda capa. No usamos DrawImage para evitar
            // cualquier interpolación o suavizado.
            byte[] headPixels =
                new byte[
                    SourceHeadSize *
                    SourceHeadSize *
                    4];


            for (int y = 0;
                y < SourceHeadSize;
                y++)
            {
                for (int x = 0;
                    x < SourceHeadSize;
                    x++)
                {
                    ReadPixel(
                        skinPixels,
                        skinStride,
                        8 + x,
                        8 + y,
                        out byte baseB,
                        out byte baseG,
                        out byte baseR,
                        out byte baseA);


                    ReadPixel(
                        skinPixels,
                        skinStride,
                        40 + x,
                        8 + y,
                        out byte overlayB,
                        out byte overlayG,
                        out byte overlayR,
                        out byte overlayA);


                    BlendPixels(
                        baseB,
                        baseG,
                        baseR,
                        baseA,
                        overlayB,
                        overlayG,
                        overlayR,
                        overlayA,
                        out byte resultB,
                        out byte resultG,
                        out byte resultR,
                        out byte resultA);


                    int destination =
                        ((y * SourceHeadSize) +
                         x) *
                        4;


                    headPixels[destination + 0] =
                        resultB;

                    headPixels[destination + 1] =
                        resultG;

                    headPixels[destination + 2] =
                        resultR;

                    headPixels[destination + 3] =
                        resultA;
                }
            }


            // 96 / 8 = 12 exacto. Cada píxel de la skin se convierte
            // en un bloque 12x12, sin ningún filtrado.
            int scale =
                OutputHeadSize /
                SourceHeadSize;


            int outputStride =
                OutputHeadSize *
                4;


            byte[] outputPixels =
                new byte[
                    outputStride *
                    OutputHeadSize];


            for (int sourceY = 0;
                sourceY < SourceHeadSize;
                sourceY++)
            {
                for (int sourceX = 0;
                    sourceX < SourceHeadSize;
                    sourceX++)
                {
                    int source =
                        ((sourceY * SourceHeadSize) +
                         sourceX) *
                        4;


                    for (int offsetY = 0;
                        offsetY < scale;
                        offsetY++)
                    {
                        for (int offsetX = 0;
                            offsetX < scale;
                            offsetX++)
                        {
                            int outputX =
                                (sourceX * scale) +
                                offsetX;


                            int outputY =
                                (sourceY * scale) +
                                offsetY;


                            int destination =
                                (outputY *
                                 outputStride) +
                                (outputX * 4);


                            outputPixels[destination + 0] =
                                headPixels[source + 0];

                            outputPixels[destination + 1] =
                                headPixels[source + 1];

                            outputPixels[destination + 2] =
                                headPixels[source + 2];

                            outputPixels[destination + 3] =
                                headPixels[source + 3];
                        }
                    }
                }
            }


            BitmapSource outputBitmap =
                BitmapSource.Create(
                    OutputHeadSize,
                    OutputHeadSize,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    outputPixels,
                    outputStride);


            outputBitmap.Freeze();


            PngBitmapEncoder encoder =
                new PngBitmapEncoder();


            encoder.Frames.Add(
                BitmapFrame.Create(
                    outputBitmap));


            string? directory =
                Path.GetDirectoryName(
                    outputPath);


            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }


            using FileStream output =
                new FileStream(
                    outputPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);


            encoder.Save(
                output);
        }


        private static void ReadPixel(
            byte[] pixels,
            int stride,
            int x,
            int y,
            out byte b,
            out byte g,
            out byte r,
            out byte a)
        {
            int index =
                (y * stride) +
                (x * 4);


            b =
                pixels[index + 0];

            g =
                pixels[index + 1];

            r =
                pixels[index + 2];

            a =
                pixels[index + 3];
        }


        private static void BlendPixels(
            byte baseB,
            byte baseG,
            byte baseR,
            byte baseA,
            byte overlayB,
            byte overlayG,
            byte overlayR,
            byte overlayA,
            out byte resultB,
            out byte resultG,
            out byte resultR,
            out byte resultA)
        {
            double overlayAlpha =
                overlayA /
                255.0;


            double baseAlpha =
                baseA /
                255.0;


            double resultAlpha =
                overlayAlpha +
                (baseAlpha *
                 (1.0 - overlayAlpha));


            if (resultAlpha <= 0)
            {
                resultB =
                    0;

                resultG =
                    0;

                resultR =
                    0;

                resultA =
                    0;

                return;
            }


            resultB =
                ToByte(
                    ((overlayB * overlayAlpha) +
                     (baseB *
                      baseAlpha *
                      (1.0 - overlayAlpha))) /
                    resultAlpha);


            resultG =
                ToByte(
                    ((overlayG * overlayAlpha) +
                     (baseG *
                      baseAlpha *
                      (1.0 - overlayAlpha))) /
                    resultAlpha);


            resultR =
                ToByte(
                    ((overlayR * overlayAlpha) +
                     (baseR *
                      baseAlpha *
                      (1.0 - overlayAlpha))) /
                    resultAlpha);


            resultA =
                ToByte(
                    resultAlpha *
                    255.0);
        }


        private static byte ToByte(
            double value)
        {
            return
                (byte)Math.Clamp(
                    Math.Round(value),
                    0,
                    255);
        }


        private static string NormalizeUuid(
            string uuid)
        {
            if (string.IsNullOrWhiteSpace(
                    uuid))
            {
                return string.Empty;
            }


            return
                new string(
                    uuid
                        .Where(
                            character =>
                                char.IsLetterOrDigit(
                                    character))
                        .ToArray())
                    .ToLowerInvariant();
        }
    }
}
