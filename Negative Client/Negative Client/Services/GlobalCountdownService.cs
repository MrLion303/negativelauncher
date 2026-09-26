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


        public async Task<GlobalCountdownFeed> GetFeedAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                GlobalCountdownFeed remoteFeed =
                    await DownloadFeedAsync(
                        cancellationToken);


                await TrySaveCacheAsync(
                    remoteFeed,
                    cancellationToken);


                return remoteFeed;
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return
                    await TryLoadCacheAsync(
                        cancellationToken);
            }
            catch
            {
                return
                    await TryLoadCacheAsync(
                        cancellationToken);
            }
        }


        private static async Task<GlobalCountdownFeed>
            DownloadFeedAsync(
                CancellationToken cancellationToken)
        {
            long cacheBuster =
                DateTimeOffset.UtcNow
                    .ToUnixTimeSeconds();


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


            GlobalCountdownFeed? feed =
                JsonSerializer
                    .Deserialize<GlobalCountdownFeed>(
                        json,
                        JsonOptions);


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
            catch
            {
                // La caché es auxiliar. Un error aquí nunca debe impedir
                // que Negative Client abra o muestre datos remotos válidos.
            }
        }


        private static async Task<GlobalCountdownFeed>
            TryLoadCacheAsync(
                CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(
                        CacheFilePath))
                {
                    return
                        new GlobalCountdownFeed();
                }


                string json =
                    await File.ReadAllTextAsync(
                        CacheFilePath,
                        cancellationToken);


                GlobalCountdownFeed? feed =
                    JsonSerializer
                        .Deserialize<GlobalCountdownFeed>(
                            json,
                            JsonOptions);


                return
                    NormalizeFeed(
                        feed);
            }
            catch
            {
                return
                    new GlobalCountdownFeed();
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


            return client;
        }
    }
}
