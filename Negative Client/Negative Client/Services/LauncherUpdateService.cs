using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed record LauncherUpdateInfo(string Version, string DownloadUrl);

    public sealed class LauncherUpdateService
    {
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/MrLion303/negativelauncher/releases/latest";

        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NegativeClient-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public async Task<LauncherUpdateInfo?> CheckForUpdatesAsync()
        {
            using HttpResponseMessage response = await Http.GetAsync(
                LatestReleaseUrl,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using Stream responseStream = await response.Content.ReadAsStreamAsync();
            using JsonDocument document = await JsonDocument.ParseAsync(responseStream);
            JsonElement root = document.RootElement;

            string tag = root.TryGetProperty("tag_name", out JsonElement tagElement)
                ? tagElement.GetString() ?? string.Empty
                : string.Empty;

            string versionText = tag.TrimStart('v', 'V');
            if (!Version.TryParse(versionText, out Version? latestVersion) ||
                latestVersion.Build < 0)
            {
                return null;
            }

            Version assemblyVersion =
                Assembly.GetEntryAssembly()?.GetName().Version ??
                new Version(1, 0, 0);
            Version currentVersion = new(
                assemblyVersion.Major,
                assemblyVersion.Minor,
                Math.Max(assemblyVersion.Build, 0));

            if (latestVersion <= currentVersion)
            {
                return null;
            }

            if (!root.TryGetProperty("assets", out JsonElement assets) ||
                assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string expectedAssetName =
                $"Negative-Client-Instalador-Windows-x64-{latestVersion}.exe";

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = asset.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;
                string url = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;

                if (string.Equals(name, expectedAssetName, StringComparison.OrdinalIgnoreCase) &&
                    Uri.TryCreate(url, UriKind.Absolute, out Uri? installerUri) &&
                    (installerUri.Scheme == Uri.UriSchemeHttps))
                {
                    return new LauncherUpdateInfo(latestVersion.ToString(3), installerUri.AbsoluteUri);
                }
            }

            return null;
        }

        public async Task<string> DownloadInstallerAsync(
            LauncherUpdateInfo update,
            Action<int>? progress = null)
        {
            string downloadDirectory = Path.Combine(
                Path.GetTempPath(),
                "NegativeClient",
                "Updates");
            Directory.CreateDirectory(downloadDirectory);

            string installerPath = Path.Combine(
                downloadDirectory,
                $"Negative-Client-Setup-{update.Version}-{Guid.NewGuid():N}.exe");

            try
            {
                using HttpResponseMessage response = await Http.GetAsync(
                    update.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                await using Stream input = await response.Content.ReadAsStreamAsync();
                await using FileStream output = new(
                    installerPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                byte[] buffer = new byte[81920];
                long bytesReadTotal = 0;
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                    bytesReadTotal += bytesRead;

                    if (totalBytes is > 0)
                    {
                        int percent = (int)Math.Clamp(
                            bytesReadTotal * 100d / totalBytes.Value,
                            0d,
                            100d);
                        progress?.Invoke(percent);
                    }
                }

                await output.FlushAsync();
                progress?.Invoke(100);
                return installerPath;
            }
            catch
            {
                try
                {
                    if (File.Exists(installerPath))
                    {
                        File.Delete(installerPath);
                    }
                }
                catch
                {
                    // No reemplazar el error original por uno de limpieza.
                }

                throw;
            }
        }
    }
}
