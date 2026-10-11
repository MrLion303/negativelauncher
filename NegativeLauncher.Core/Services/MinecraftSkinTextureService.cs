using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    /// <summary>Descarga y cachea las skins públicas de Minecraft sin APIs de imagen de Windows.</summary>
    public sealed class MinecraftSkinTextureService
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
        private const int MaxSkinBytes = 8 * 1024 * 1024;
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);
        private readonly string _cacheDirectory;

        public MinecraftSkinTextureService(string? cacheDirectory = null)
        {
            _cacheDirectory = Path.GetFullPath(cacheDirectory ??
                Path.Combine(LauncherPaths.DefaultLauncherRoot, "cache", "accounts"));
        }

        public async Task<string?> GetSkinPathAsync(string? uuid, CancellationToken cancellationToken = default)
        {
            string normalizedUuid = NormalizeUuid(uuid);
            if (normalizedUuid.Length != 32 || normalizedUuid.Any(c => !Uri.IsHexDigit(c)))
                return null;

            string outputPath = Path.Combine(_cacheDirectory, normalizedUuid + "-skin.png");
            string? cachedPath = IsUsableCachedPng(outputPath) ? outputPath : null;
            if (cachedPath is not null)
            {
                try
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(cachedPath) < CacheLifetime)
                        return cachedPath;
                }
                catch { }
            }

            string temporaryPath = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
                string profileUrl = "https://sessionserver.mojang.com/session/minecraft/profile/" +
                    Uri.EscapeDataString(normalizedUuid) + "?unsigned=false";
                using HttpResponseMessage profileResponse = await HttpClient.GetAsync(profileUrl, cancellationToken);
                if (!profileResponse.IsSuccessStatusCode)
                    return cachedPath;

                string profileJson = await profileResponse.Content.ReadAsStringAsync(cancellationToken);
                if (!TryGetSkinTextureUrl(profileJson, out Uri skinUri))
                    return cachedPath;

                using HttpResponseMessage skinResponse = await HttpClient.GetAsync(skinUri, cancellationToken);
                if (!skinResponse.IsSuccessStatusCode)
                    return cachedPath;

                long? length = skinResponse.Content.Headers.ContentLength;
                if (length.HasValue && (length.Value < PngSignature.Length || length.Value > MaxSkinBytes))
                    return cachedPath;

                byte[] bytes = await skinResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                if (!IsPng(bytes))
                    return cachedPath;

                await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
                File.Move(temporaryPath, outputPath, overwrite: true);
                return outputPath;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Un fallo de red en un avatar nunca bloquea el acceso o la selección de cuenta.
                return cachedPath;
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch { }
            }
        }

        public static bool TryGetSkinTextureUrl(string profileJson, out Uri skinUri)
        {
            skinUri = null!;
            try
            {
                using JsonDocument profile = JsonDocument.Parse(profileJson);
                if (!profile.RootElement.TryGetProperty("properties", out JsonElement properties) ||
                    properties.ValueKind != JsonValueKind.Array)
                    return false;

                JsonElement textureProperty = properties.EnumerateArray().FirstOrDefault(property =>
                    property.TryGetProperty("name", out JsonElement name) &&
                    string.Equals(name.GetString(), "textures", StringComparison.Ordinal));
                if (textureProperty.ValueKind == JsonValueKind.Undefined ||
                    !textureProperty.TryGetProperty("value", out JsonElement valueElement))
                    return false;

                string? encoded = valueElement.GetString();
                if (string.IsNullOrWhiteSpace(encoded))
                    return false;

                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                using JsonDocument texturesDocument = JsonDocument.Parse(decoded);
                if (!texturesDocument.RootElement.TryGetProperty("textures", out JsonElement textures) ||
                    !textures.TryGetProperty("SKIN", out JsonElement skin) ||
                    !skin.TryGetProperty("url", out JsonElement urlElement))
                    return false;

                string? url = urlElement.GetString();
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) ||
                    (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
                    !string.Equals(parsed.Host, "textures.minecraft.net", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(parsed.UserInfo) || (parsed.Port != 80 && parsed.Port != 443))
                    return false;

                skinUri = parsed.Scheme == Uri.UriSchemeHttps
                    ? parsed
                    : new UriBuilder(parsed) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
                return true;
            }
            catch { return false; }
        }

        private static bool IsUsableCachedPng(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                using FileStream stream = File.OpenRead(path);
                if (stream.Length < PngSignature.Length || stream.Length > MaxSkinBytes)
                    return false;
                Span<byte> signature = stackalloc byte[PngSignature.Length];
                return stream.Read(signature) == signature.Length && signature.SequenceEqual(PngSignature);
            }
            catch { return false; }
        }

        private static bool IsPng(byte[] bytes) =>
            bytes.Length >= PngSignature.Length && bytes.Length <= MaxSkinBytes &&
            bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);

        private static string NormalizeUuid(string? uuid) =>
            string.IsNullOrWhiteSpace(uuid)
                ? string.Empty
                : new string(uuid.Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
    }
}
