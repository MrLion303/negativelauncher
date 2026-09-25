using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negative_Client.Services
{
    public sealed class MinecraftSkinService
    {
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


            if (File.Exists(outputPath))
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


                await Application.Current.Dispatcher
                    .InvokeAsync(
                        () =>
                        {
                            CreateHeadPng(
                                skinBytes,
                                outputPath);
                        });


                return
                    File.Exists(outputPath)
                        ? outputPath
                        : null;
            }
            catch
            {
                // Si el servicio de skins falla, usamos la caché
                // anterior en vez de romper Configuración.
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


        private static void CreateHeadPng(
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


            BitmapSource skin =
                decoder.Frames[0];


            if (skin.PixelWidth < 48 ||
                skin.PixelHeight < 16)
            {
                throw new InvalidDataException(
                    "La skin descargada no tiene un formato válido.");
            }


            CroppedBitmap baseHead =
                new CroppedBitmap(
                    skin,
                    new Int32Rect(
                        8,
                        8,
                        8,
                        8));


            CroppedBitmap overlayHead =
                new CroppedBitmap(
                    skin,
                    new Int32Rect(
                        40,
                        8,
                        8,
                        8));


            RenderOptions.SetBitmapScalingMode(
                baseHead,
                BitmapScalingMode.NearestNeighbor);


            RenderOptions.SetBitmapScalingMode(
                overlayHead,
                BitmapScalingMode.NearestNeighbor);


            DrawingVisual visual =
                new DrawingVisual();


            RenderOptions.SetBitmapScalingMode(
                visual,
                BitmapScalingMode.NearestNeighbor);


            using (DrawingContext context =
                visual.RenderOpen())
            {
                Rect destination =
                    new Rect(
                        0,
                        0,
                        64,
                        64);


                context.DrawImage(
                    baseHead,
                    destination);


                context.DrawImage(
                    overlayHead,
                    destination);
            }


            RenderTargetBitmap rendered =
                new RenderTargetBitmap(
                    64,
                    64,
                    96,
                    96,
                    PixelFormats.Pbgra32);


            rendered.Render(
                visual);


            PngBitmapEncoder encoder =
                new PngBitmapEncoder();


            encoder.Frames.Add(
                BitmapFrame.Create(
                    rendered));


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
