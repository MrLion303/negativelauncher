using System;
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
        private const string FeedUrl =
            "https://raw.githubusercontent.com/MrLion303/negativeclient-countdowns/main/data/countdowns.json";


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


        private static string CacheDirectory =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "NegativeClient",
                "cache");


        private static string DiagnosticFilePath =>
            Path.Combine(
                CacheDirectory,
                "global-countdowns-diagnostics.log");


        /*
         * Para que INICIAR/DETENER se refleje de verdad en vivo:
         *
         * - no usamos la caché local como fuente visual;
         * - no usamos GitHub Pages como fallback porque Pages puede tardar en
         *   desplegar y devolver durante un rato un estado antiguo;
         * - consultamos el archivo RAW de main con cache-buster;
         * - si la red falla, devolvemos lista vacía para no dejar un anuncio
         *   detenido apareciendo eternamente.
         */
        public async Task<GlobalCountdownFeed> GetFeedAsync(
            CancellationToken cancellationToken = default)
        {
            long cacheBuster =
                DateTimeOffset.UtcNow
                    .ToUnixTimeMilliseconds();


            string url =
                $"{FeedUrl}?v={cacheBuster}";


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


            request.Headers.Pragma.ParseAdd(
                "no-cache");


            using CancellationTokenSource timeout =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);


            timeout.CancelAfter(
                TimeSpan.FromSeconds(
                    8));


            try
            {
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
                    $"FETCH OK: total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.FindAll(x => x.Active).Count}, " +
                    $"updatedAt={feed.UpdatedAt:O}");


                return
                    feed;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"FETCH FAIL: {ex.GetType().Name}: {ex.Message}");


                /*
                 * No devolvemos un feed activo guardado anteriormente.
                 * Preferimos ocultar el banner antes que mostrar información
                 * global obsoleta después de pulsar DETENER.
                 */
                return
                    new GlobalCountdownFeed();
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


            feed.Countdowns ??=
                new();


            foreach (GlobalCountdown countdown in
                feed.Countdowns)
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


            return
                feed;
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
                        $"[{DateTimeOffset.UtcNow:O}] {message}" +
                        Environment.NewLine;


                    File.AppendAllText(
                        DiagnosticFilePath,
                        line,
                        new UTF8Encoding(
                            encoderShouldEmitUTF8Identifier: false));
                }
            }
            catch
            {
                // El diagnóstico jamás debe romper el launcher.
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


            return
                client;
        }
    }
}
