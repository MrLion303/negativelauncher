using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class GlobalCountdownService
    {
        private const string RawFeedUrl =
            "https://raw.githubusercontent.com/MrLion303/negativeclient-countdowns/main/data/countdowns.json";

        private const string PagesFeedUrl =
            "https://mrlion303.github.io/negativeclient-countdowns/data/countdowns.json";

        private const string ApiFeedUrl =
            "https://api.github.com/repos/MrLion303/negativeclient-countdowns/contents/data/countdowns.json?ref=main";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static readonly JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

        private static readonly object DiagnosticLock = new();

        private static DateTimeOffset _nextApiFallbackAllowedUtc =
            DateTimeOffset.MinValue;

        private static GlobalCountdownFeed? _lastApiFallbackFeed;

        private static string CacheDirectory =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "NegativeClient",
                "cache");

        private static string CacheFilePath =>
            Path.Combine(
                CacheDirectory,
                "global-countdowns.json");

        private static string DiagnosticFilePath =>
            Path.Combine(
                CacheDirectory,
                "global-countdowns-diagnostics.log");


        public async Task<GlobalCountdownFeed> GetFeedAsync(
            CancellationToken cancellationToken = default)
        {
            List<string> failures = new();

            GlobalCountdownFeed? raw =
                await TryDownloadDirectFeedAsync(
                    RawFeedUrl,
                    "GitHub Raw",
                    failures,
                    cancellationToken);

            if (raw != null)
            {
                await TrySaveCacheAsync(raw, cancellationToken);
                return raw;
            }

            GlobalCountdownFeed? pages =
                await TryDownloadDirectFeedAsync(
                    PagesFeedUrl,
                    "GitHub Pages",
                    failures,
                    cancellationToken);

            if (pages != null)
            {
                await TrySaveCacheAsync(pages, cancellationToken);
                return pages;
            }

            GlobalCountdownFeed? api =
                await TryDownloadApiFeedAsync(
                    failures,
                    cancellationToken);

            if (api != null)
            {
                await TrySaveCacheAsync(api, cancellationToken);
                return api;
            }

            GlobalCountdownFeed cached =
                await TryLoadCacheAsync(cancellationToken);

            WriteDiagnostic(
                "TODAS LAS FUENTES FALLARON. " +
                string.Join(" | ", failures) +
                $" | cache_total={cached.Countdowns.Count}");

            return cached;
        }


        private static async Task<GlobalCountdownFeed?>
            TryDownloadDirectFeedAsync(
                string feedUrl,
                string sourceName,
                List<string> failures,
                CancellationToken cancellationToken)
        {
            try
            {
                string separator = feedUrl.Contains('?') ? "&" : "?";
                long cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string url = $"{feedUrl}{separator}v={cacheBuster}";

                using HttpRequestMessage request =
                    new(HttpMethod.Get, url);

                request.Headers.CacheControl =
                    new CacheControlHeaderValue
                    {
                        NoCache = true,
                        NoStore = true
                    };

                using CancellationTokenSource timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                timeout.CancelAfter(TimeSpan.FromSeconds(8));

                using HttpResponseMessage response =
                    await HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeout.Token);

                response.EnsureSuccessStatusCode();

                string json =
                    await response.Content.ReadAsStringAsync(timeout.Token);

                GlobalCountdownFeed feed = ParseFeed(json);

                WriteDiagnostic(
                    $"FETCH OK [{sourceName}] total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}, " +
                    $"updatedAt={feed.UpdatedAt:O}");

                return feed;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                string message =
                    $"{sourceName}: {ex.GetType().Name}: {ex.Message}";

                failures.Add(message);
                WriteDiagnostic($"FETCH FAIL [{message}]");
                return null;
            }
        }


        private static async Task<GlobalCountdownFeed?>
            TryDownloadApiFeedAsync(
                List<string> failures,
                CancellationToken cancellationToken)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (now < _nextApiFallbackAllowedUtc)
            {
                if (_lastApiFallbackFeed != null)
                {
                    WriteDiagnostic(
                        "API FALLBACK: reutilizando último resultado para no agotar el rate limit.");
                }

                return _lastApiFallbackFeed;
            }

            _nextApiFallbackAllowedUtc = now.AddMinutes(1);

            try
            {
                using HttpRequestMessage request =
                    new(HttpMethod.Get, ApiFeedUrl);

                request.Headers.CacheControl =
                    new CacheControlHeaderValue
                    {
                        NoCache = true
                    };

                using CancellationTokenSource timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                timeout.CancelAfter(TimeSpan.FromSeconds(8));

                using HttpResponseMessage response =
                    await HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeout.Token);

                response.EnsureSuccessStatusCode();

                string envelopeJson =
                    await response.Content.ReadAsStringAsync(timeout.Token);

                using JsonDocument envelope =
                    JsonDocument.Parse(envelopeJson);

                if (!envelope.RootElement.TryGetProperty(
                        "content",
                        out JsonElement contentElement))
                {
                    throw new InvalidDataException(
                        "La API de GitHub no devolvió el campo content.");
                }

                string base64 =
                    contentElement.GetString() ?? string.Empty;

                base64 = base64.Replace("\n", string.Empty)
                               .Replace("\r", string.Empty)
                               .Trim();

                byte[] bytes = Convert.FromBase64String(base64);
                string json = Encoding.UTF8.GetString(bytes);

                GlobalCountdownFeed feed = ParseFeed(json);
                _lastApiFallbackFeed = feed;

                WriteDiagnostic(
                    $"FETCH OK [GitHub API fallback] total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}, " +
                    $"updatedAt={feed.UpdatedAt:O}");

                return feed;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                string message =
                    $"GitHub API fallback: {ex.GetType().Name}: {ex.Message}";

                failures.Add(message);
                WriteDiagnostic($"FETCH FAIL [{message}]");
                return _lastApiFallbackFeed;
            }
        }


        private static GlobalCountdownFeed ParseFeed(
            string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException(
                    "El feed llegó vacío.");
            }

            GlobalCountdownFeed? feed =
                JsonSerializer.Deserialize<GlobalCountdownFeed>(
                    json,
                    JsonOptions);

            if (feed == null)
            {
                throw new InvalidDataException(
                    "El feed no pudo deserializarse.");
            }

            return NormalizeFeed(feed);
        }


        private static GlobalCountdownFeed NormalizeFeed(
            GlobalCountdownFeed? feed)
        {
            GlobalCountdownFeed normalized =
                feed ?? new GlobalCountdownFeed();

            normalized.Countdowns ??= new();

            foreach (GlobalCountdown countdown in normalized.Countdowns)
            {
                countdown.Id = countdown.Id?.Trim() ?? string.Empty;
                countdown.Name = countdown.Name?.Trim() ?? string.Empty;

                if (countdown.EndAtUtc != default)
                {
                    countdown.EndAtUtc =
                        countdown.EndAtUtc.ToUniversalTime();
                }
            }

            return normalized;
        }


        private static async Task TrySaveCacheAsync(
            GlobalCountdownFeed feed,
            CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);

                string json =
                    JsonSerializer.Serialize(
                        feed,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                string tempPath = CacheFilePath + ".tmp";

                await File.WriteAllTextAsync(
                    tempPath,
                    json,
                    new UTF8Encoding(false),
                    cancellationToken);

                File.Move(
                    tempPath,
                    CacheFilePath,
                    overwrite: true);
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"CACHE SAVE FAIL: {ex.GetType().Name}: {ex.Message}");
            }
        }


        private static async Task<GlobalCountdownFeed>
            TryLoadCacheAsync(
                CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                {
                    return new GlobalCountdownFeed();
                }

                string json =
                    await File.ReadAllTextAsync(
                        CacheFilePath,
                        cancellationToken);

                GlobalCountdownFeed feed = ParseFeed(json);

                WriteDiagnostic(
                    $"CACHE LOAD OK: total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}.");

                return feed;
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"CACHE LOAD FAIL: {ex.GetType().Name}: {ex.Message}");

                return new GlobalCountdownFeed();
            }
        }


        public static void WriteDiagnostic(
            string message)
        {
            try
            {
                lock (DiagnosticLock)
                {
                    Directory.CreateDirectory(CacheDirectory);

                    string line =
                        $"[{DateTimeOffset.UtcNow:O}] {message}{Environment.NewLine}";

                    File.AppendAllText(
                        DiagnosticFilePath,
                        line,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // El diagnóstico jamás debe romper el launcher.
            }
        }


        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new();

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "NegativeClient/0.1");

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

            return client;
        }
    }
}
