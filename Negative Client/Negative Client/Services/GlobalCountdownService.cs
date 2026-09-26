using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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

        private static readonly TimeSpan CacheMaximumAge =
            TimeSpan.FromSeconds(10);

        private static readonly HttpClient HttpClient =
            CreateHttpClient();

        private static readonly JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

        private static readonly object DiagnosticLock = new();

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

            GlobalCountdownFeed? rawFeed =
                await TryDownloadFeedAsync(
                    RawFeedUrl,
                    "GitHub Raw",
                    failures,
                    cancellationToken);

            if (rawFeed != null)
            {
                await TrySaveCacheAsync(
                    rawFeed,
                    cancellationToken);

                return rawFeed;
            }

            GlobalCountdownFeed? pagesFeed =
                await TryDownloadFeedAsync(
                    PagesFeedUrl,
                    "GitHub Pages",
                    failures,
                    cancellationToken);

            if (pagesFeed != null)
            {
                await TrySaveCacheAsync(
                    pagesFeed,
                    cancellationToken);

                return pagesFeed;
            }

            GlobalCountdownFeed? cachedFeed =
                await TryLoadFreshCacheAsync(
                    cancellationToken);

            if (cachedFeed != null)
            {
                WriteDiagnostic(
                    "FUENTES REMOTAS FALLARON. Se usa caché reciente. " +
                    string.Join(" | ", failures));

                return cachedFeed;
            }

            WriteDiagnostic(
                "FUENTES REMOTAS FALLARON Y NO HAY CACHÉ RECIENTE. " +
                string.Join(" | ", failures));

            return new GlobalCountdownFeed();
        }


        private static async Task<GlobalCountdownFeed?>
            TryDownloadFeedAsync(
                string feedUrl,
                string sourceName,
                List<string> failures,
                CancellationToken cancellationToken)
        {
            try
            {
                string separator =
                    feedUrl.Contains('?')
                        ? "&"
                        : "?";

                long cacheBuster =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                string url =
                    $"{feedUrl}{separator}nc={cacheBuster}";

                using CancellationTokenSource timeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(6));

                using HttpRequestMessage request =
                    new(HttpMethod.Get, url);

                request.Headers.TryAddWithoutValidation(
                    "Cache-Control",
                    "no-cache, no-store, max-age=0");

                request.Headers.TryAddWithoutValidation(
                    "Pragma",
                    "no-cache");

                using HttpResponseMessage response =
                    await HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeout.Token);

                response.EnsureSuccessStatusCode();

                string json =
                    await response.Content.ReadAsStringAsync(
                        timeout.Token);

                GlobalCountdownFeed feed =
                    ParseFeed(json);

                WriteDiagnostic(
                    $"FETCH OK [{sourceName}] total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}.");

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

            feed.Countdowns ??= new();

            foreach (GlobalCountdown countdown in feed.Countdowns)
            {
                countdown.Id =
                    countdown.Id?.Trim() ?? string.Empty;

                countdown.Name =
                    countdown.Name?.Trim() ?? string.Empty;

                if (countdown.EndAtUtc != default)
                {
                    countdown.EndAtUtc =
                        countdown.EndAtUtc.ToUniversalTime();
                }
            }

            return feed;
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

                string tempPath =
                    CacheFilePath + ".tmp";

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


        private static async Task<GlobalCountdownFeed?>
            TryLoadFreshCacheAsync(
                CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                {
                    return null;
                }

                DateTime lastWriteUtc =
                    File.GetLastWriteTimeUtc(CacheFilePath);

                TimeSpan cacheAge =
                    DateTime.UtcNow - lastWriteUtc;

                if (cacheAge > CacheMaximumAge)
                {
                    return null;
                }

                string json =
                    await File.ReadAllTextAsync(
                        CacheFilePath,
                        cancellationToken);

                return ParseFeed(json);
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"CACHE LOAD FAIL: {ex.GetType().Name}: {ex.Message}");

                return null;
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
                        $"[{DateTimeOffset.UtcNow:O}] " +
                        message +
                        Environment.NewLine;

                    File.AppendAllText(
                        DiagnosticFilePath,
                        line,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // El diagnóstico nunca debe romper el launcher.
            }
        }


        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new();

            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "NegativeClient/0.1");

            return client;
        }
    }
}
