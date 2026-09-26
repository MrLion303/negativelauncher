using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
        /*
         * La GitHub Contents API es ahora la fuente PRINCIPAL.
         *
         * Motivo:
         * GitHub Raw / Pages pueden entregar una versión cacheada durante
         * unos segundos. Eso hacía que una cuenta detenida siguiera visible.
         *
         * La API responde con ETag. Al consultar de nuevo enviamos
         * If-None-Match; si nada cambió, GitHub responde 304.
         * Si pulsas INICIAR o DETENER, el ETag cambia y recibimos inmediatamente
         * el nuevo countdowns.json.
         */
        private const string ApiFeedUrl =
            "https://api.github.com/repos/MrLion303/negativeclient-countdowns/contents/data/countdowns.json?ref=main";

        private const string RawFeedUrl =
            "https://raw.githubusercontent.com/MrLion303/negativeclient-countdowns/main/data/countdowns.json";

        private const string PagesFeedUrl =
            "https://mrlion303.github.io/negativeclient-countdowns/data/countdowns.json";

        /*
         * Si todas las fuentes de Internet fallan, no queremos conservar
         * indefinidamente un contador que quizá ya fue detenido.
         */
        private static readonly TimeSpan CacheMaximumAge =
            TimeSpan.FromSeconds(
                20);

        private static readonly HttpClient HttpClient =
            CreateHttpClient();

        private static readonly JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };

        private static readonly object DiagnosticLock =
            new();

        private static string? _apiETag;

        private static GlobalCountdownFeed? _lastApiFeed;

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
            List<string> failures =
                new();

            /*
             * 1) API de GitHub con ETag.
             *
             * Esta es la ruta que permite que DETENER quite el banner casi
             * inmediatamente y que INICIAR lo muestre en vivo.
             */
            GlobalCountdownFeed? apiFeed =
                await TryDownloadApiFeedAsync(
                    failures,
                    cancellationToken);

            if (apiFeed !=
                null)
            {
                await TrySaveCacheAsync(
                    apiFeed,
                    cancellationToken);

                return apiFeed;
            }

            /*
             * 2) Raw como fallback.
             */
            GlobalCountdownFeed? rawFeed =
                await TryDownloadDirectFeedAsync(
                    RawFeedUrl,
                    "GitHub Raw",
                    failures,
                    cancellationToken);

            if (rawFeed !=
                null)
            {
                await TrySaveCacheAsync(
                    rawFeed,
                    cancellationToken);

                return rawFeed;
            }

            /*
             * 3) Pages como segundo fallback.
             */
            GlobalCountdownFeed? pagesFeed =
                await TryDownloadDirectFeedAsync(
                    PagesFeedUrl,
                    "GitHub Pages",
                    failures,
                    cancellationToken);

            if (pagesFeed !=
                null)
            {
                await TrySaveCacheAsync(
                    pagesFeed,
                    cancellationToken);

                return pagesFeed;
            }

            /*
             * 4) Caché SOLO si es reciente.
             *
             * De este modo una cuenta detenida no puede quedarse pegada
             * durante minutos/horas solamente porque Internet falló después.
             */
            GlobalCountdownFeed? cachedFeed =
                await TryLoadFreshCacheAsync(
                    cancellationToken);

            if (cachedFeed !=
                null)
            {
                WriteDiagnostic(
                    "TODAS LAS FUENTES REMOTAS FALLARON. " +
                    "Se usa caché reciente. " +
                    string.Join(
                        " | ",
                        failures));

                return cachedFeed;
            }

            WriteDiagnostic(
                "TODAS LAS FUENTES REMOTAS FALLARON Y LA CACHÉ ESTÁ VENCIDA. " +
                "Se devuelve una lista vacía para evitar mostrar un contador obsoleto. " +
                string.Join(
                    " | ",
                    failures));

            return
                new GlobalCountdownFeed();
        }


        private static async Task<GlobalCountdownFeed?>
            TryDownloadApiFeedAsync(
                List<string> failures,
                CancellationToken cancellationToken)
        {
            try
            {
                using HttpRequestMessage request =
                    new(
                        HttpMethod.Get,
                        ApiFeedUrl);

                request.Headers.CacheControl =
                    new CacheControlHeaderValue
                    {
                        NoCache =
                            true
                    };

                if (!string.IsNullOrWhiteSpace(
                        _apiETag))
                {
                    request.Headers.IfNoneMatch.Add(
                        new EntityTagHeaderValue(
                            _apiETag));
                }

                using CancellationTokenSource timeout =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            cancellationToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(
                        8));

                using HttpResponseMessage response =
                    await HttpClient
                        .SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            timeout.Token);

                /*
                 * Sin cambios desde la consulta anterior.
                 */
                if (response.StatusCode ==
                    HttpStatusCode.NotModified)
                {
                    if (_lastApiFeed !=
                        null)
                    {
                        WriteDiagnostic(
                            "FETCH 304 [GitHub API] sin cambios.");

                        return _lastApiFeed;
                    }

                    /*
                     * No deberíamos recibir 304 sin tener un feed previo,
                     * pero si sucediera dejamos que pase a los fallbacks.
                     */
                    failures.Add(
                        "GitHub API: 304 recibido sin un feed previo.");

                    return null;
                }

                response
                    .EnsureSuccessStatusCode();

                string envelopeJson =
                    await response.Content
                        .ReadAsStringAsync(
                            timeout.Token);

                using JsonDocument envelope =
                    JsonDocument.Parse(
                        envelopeJson);

                if (!envelope.RootElement
                        .TryGetProperty(
                            "content",
                            out JsonElement contentElement))
                {
                    throw new InvalidDataException(
                        "La API de GitHub no devolvió el campo content.");
                }

                string base64 =
                    contentElement
                        .GetString() ??
                    string.Empty;

                base64 =
                    base64
                        .Replace(
                            "\n",
                            string.Empty)
                        .Replace(
                            "\r",
                            string.Empty)
                        .Trim();

                byte[] bytes =
                    Convert.FromBase64String(
                        base64);

                string json =
                    Encoding.UTF8.GetString(
                        bytes);

                GlobalCountdownFeed feed =
                    ParseFeed(
                        json);

                _apiETag =
                    response.Headers.ETag?
                        .Tag;

                _lastApiFeed =
                    feed;

                WriteDiagnostic(
                    $"FETCH 200 [GitHub API] total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}, " +
                    $"updatedAt={feed.UpdatedAt:O}, " +
                    $"etag={_apiETag ?? "(sin etag)"}.");

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
                    $"GitHub API: {ex.GetType().Name}: {ex.Message}";

                failures.Add(
                    message);

                WriteDiagnostic(
                    $"FETCH FAIL [{message}]");

                return null;
            }
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
                string separator =
                    feedUrl.Contains(
                        '?')
                        ? "&"
                        : "?";

                long cacheBuster =
                    DateTimeOffset.UtcNow
                        .ToUnixTimeMilliseconds();

                string url =
                    $"{feedUrl}{separator}v={cacheBuster}";

                using HttpRequestMessage request =
                    new(
                        HttpMethod.Get,
                        url);

                request.Headers.CacheControl =
                    new CacheControlHeaderValue
                    {
                        NoCache =
                            true,

                        NoStore =
                            true
                    };

                using CancellationTokenSource timeout =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            cancellationToken);

                timeout.CancelAfter(
                    TimeSpan.FromSeconds(
                        8));

                using HttpResponseMessage response =
                    await HttpClient
                        .SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            timeout.Token);

                response
                    .EnsureSuccessStatusCode();

                string json =
                    await response.Content
                        .ReadAsStringAsync(
                            timeout.Token);

                GlobalCountdownFeed feed =
                    ParseFeed(
                        json);

                WriteDiagnostic(
                    $"FETCH OK [{sourceName}] total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}, " +
                    $"updatedAt={feed.UpdatedAt:O}.");

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

                failures.Add(
                    message);

                WriteDiagnostic(
                    $"FETCH FAIL [{message}]");

                return null;
            }
        }


        private static GlobalCountdownFeed ParseFeed(
            string json)
        {
            if (string.IsNullOrWhiteSpace(
                    json))
            {
                throw new InvalidDataException(
                    "El feed llegó vacío.");
            }

            GlobalCountdownFeed? feed =
                JsonSerializer
                    .Deserialize<GlobalCountdownFeed>(
                        json,
                        JsonOptions);

            if (feed ==
                null)
            {
                throw new InvalidDataException(
                    "El feed no pudo deserializarse.");
            }

            return
                NormalizeFeed(
                    feed);
        }


        private static GlobalCountdownFeed NormalizeFeed(
            GlobalCountdownFeed? feed)
        {
            GlobalCountdownFeed normalized =
                feed ??
                new GlobalCountdownFeed();

            normalized.Countdowns ??=
                new();

            foreach (GlobalCountdown countdown in
                normalized.Countdowns)
            {
                countdown.Id =
                    countdown.Id?
                        .Trim() ??
                    string.Empty;

                countdown.Name =
                    countdown.Name?
                        .Trim() ??
                    string.Empty;

                if (countdown.EndAtUtc !=
                    default)
                {
                    countdown.EndAtUtc =
                        countdown.EndAtUtc
                            .ToUniversalTime();
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
                Directory.CreateDirectory(
                    CacheDirectory);

                string json =
                    JsonSerializer.Serialize(
                        feed,
                        new JsonSerializerOptions
                        {
                            WriteIndented =
                                true
                        });

                string tempPath =
                    CacheFilePath +
                    ".tmp";

                await File.WriteAllTextAsync(
                    tempPath,
                    json,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false),
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
                if (!File.Exists(
                        CacheFilePath))
                {
                    return null;
                }

                DateTime lastWriteUtc =
                    File.GetLastWriteTimeUtc(
                        CacheFilePath);

                TimeSpan cacheAge =
                    DateTime.UtcNow -
                    lastWriteUtc;

                if (cacheAge >
                    CacheMaximumAge)
                {
                    WriteDiagnostic(
                        $"CACHE IGNORADA: antigüedad={cacheAge.TotalSeconds:F1}s.");

                    return null;
                }

                string json =
                    await File.ReadAllTextAsync(
                        CacheFilePath,
                        cancellationToken);

                GlobalCountdownFeed feed =
                    ParseFeed(
                        json);

                WriteDiagnostic(
                    $"CACHE LOAD OK: edad={cacheAge.TotalSeconds:F1}s, " +
                    $"total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}.");

                return feed;
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
                    Directory.CreateDirectory(
                        CacheDirectory);

                    string line =
                        $"[{DateTimeOffset.UtcNow:O}] " +
                        $"{message}" +
                        $"{Environment.NewLine}";

                    File.AppendAllText(
                        DiagnosticFilePath,
                        line,
                        new UTF8Encoding(
                            encoderShouldEmitUTF8Identifier: false));
                }
            }
            catch
            {
                // El diagnóstico nunca debe romper el launcher.
            }
        }


        private static HttpClient CreateHttpClient()
        {
            HttpClient client =
                new();

            client.DefaultRequestHeaders
                .UserAgent
                .ParseAdd(
                    "NegativeClient/0.1");

            client.DefaultRequestHeaders
                .Accept
                .Add(
                    new MediaTypeWithQualityHeaderValue(
                        "application/json"));

            return client;
        }
    }
}
