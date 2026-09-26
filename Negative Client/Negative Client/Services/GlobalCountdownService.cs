using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class GlobalCountdownService
    {
        private const string RepositoryOwner =
            "MrLion303";

        private const string RepositoryName =
            "negativeclient-countdowns";

        private const string BranchName =
            "main";

        private const string CountdownFilePath =
            "data/countdowns.json";


        /*
         * El problema de usar directamente:
         *
         * raw.githubusercontent.com/.../main/...
         *
         * para un estado "en vivo" es que la URL apunta a una rama mutable y
         * puede atravesar cachés intermedias.
         *
         * Ahora primero averiguamos el commit más reciente de main y después
         * descargamos countdowns.json usando ESE SHA.
         *
         * Una URL raw fijada a un SHA es inmutable:
         *
         * raw.githubusercontent.com/.../<SHA>/data/countdowns.json
         *
         * Por eso INICIAR / DETENER / EDITAR se reflejan como un nuevo commit
         * y el launcher puede obtener exactamente ese estado, sin depender de
         * que una caché actualice la referencia "main".
         */
        private static string AtomFeedUrl =>
            $"https://github.com/{RepositoryOwner}/{RepositoryName}/commits/{BranchName}.atom";

        private static string CommitsHtmlUrl =>
            $"https://github.com/{RepositoryOwner}/{RepositoryName}/commits/{BranchName}";

        private static string GitSmartRefsUrl =>
            $"https://github.com/{RepositoryOwner}/{RepositoryName}.git/info/refs?service=git-upload-pack";

        private static string MutableRawFeedUrl =>
            $"https://raw.githubusercontent.com/{RepositoryOwner}/{RepositoryName}/{BranchName}/{CountdownFilePath}";


        private static readonly HttpClient HttpClient =
            CreateHttpClient();


        private static readonly JsonSerializerOptions JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };


        private static readonly Regex CommitLinkRegex =
            new(
                $@"/{Regex.Escape(RepositoryOwner)}/{Regex.Escape(RepositoryName)}/commit/(?<sha>[0-9a-fA-F]{{40}})",
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);


        private static readonly Regex SmartMainRefRegex =
            new(
                @"(?<sha>[0-9a-fA-F]{40})\s+refs/heads/main",
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);


        private static readonly object DiagnosticLock =
            new();


        private readonly SemaphoreSlim _refreshGate =
            new(
                1,
                1);


        private string? _lastCommitSha;

        private GlobalCountdownFeed? _lastFeed;


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


        public async Task<GlobalCountdownFeed> GetFeedAsync(
            CancellationToken cancellationToken = default)
        {
            await _refreshGate
                .WaitAsync(
                    cancellationToken);


            try
            {
                string? latestCommitSha =
                    await TryGetLatestCommitShaAsync(
                        cancellationToken);


                if (!string.IsNullOrWhiteSpace(
                        latestCommitSha))
                {
                    if (string.Equals(
                            latestCommitSha,
                            _lastCommitSha,
                            StringComparison.OrdinalIgnoreCase) &&
                        _lastFeed !=
                            null)
                    {
                        /*
                         * No hubo ningún commit nuevo.
                         * El contador en pantalla sigue descontando localmente;
                         * no hace falta volver a bajar el mismo JSON.
                         */
                        return
                            _lastFeed;
                    }


                    GlobalCountdownFeed? commitFeed =
                        await TryDownloadFeedFromCommitAsync(
                            latestCommitSha,
                            cancellationToken);


                    if (commitFeed !=
                        null)
                    {
                        _lastCommitSha =
                            latestCommitSha;

                        _lastFeed =
                            commitFeed;


                        WriteDiagnostic(
                            $"LIVE UPDATE OK: commit={latestCommitSha}, " +
                            $"total={commitFeed.Countdowns.Count}, " +
                            $"active={commitFeed.Countdowns.Count(x => x.Active)}, " +
                            $"updatedAt={commitFeed.UpdatedAt:O}");


                        return
                            commitFeed;
                    }
                }


                /*
                 * Fallback:
                 * si GitHub no permite leer temporalmente los endpoints usados
                 * para descubrir el SHA, intentamos el RAW tradicional.
                 *
                 * No dejamos que un estado raw ANTIGUO sustituya a otro más
                 * reciente que ya hubiéramos recibido por SHA.
                 */
                GlobalCountdownFeed? mutableFeed =
                    await TryDownloadMutableRawFeedAsync(
                        cancellationToken);


                if (mutableFeed !=
                    null)
                {
                    if (IsFeedNewerOrEqual(
                            mutableFeed,
                            _lastFeed))
                    {
                        _lastFeed =
                            mutableFeed;
                    }


                    return
                        _lastFeed ??
                        mutableFeed;
                }


                /*
                 * Un fallo momentáneo de red no debe borrar una cuenta válida
                 * que ya estaba visible. Conservamos el último estado conocido.
                 */
                return
                    _lastFeed ??
                    new GlobalCountdownFeed();
            }
            finally
            {
                _refreshGate.Release();
            }
        }


        private static async Task<string?>
            TryGetLatestCommitShaAsync(
                CancellationToken cancellationToken)
        {
            string? atomSha =
                await TryGetCommitShaFromAtomAsync(
                    cancellationToken);


            if (!string.IsNullOrWhiteSpace(
                    atomSha))
            {
                return
                    atomSha;
            }


            string? htmlSha =
                await TryGetCommitShaFromHtmlAsync(
                    cancellationToken);


            if (!string.IsNullOrWhiteSpace(
                    htmlSha))
            {
                return
                    htmlSha;
            }


            return
                await TryGetCommitShaFromSmartGitAsync(
                    cancellationToken);
        }


        private static async Task<string?>
            TryGetCommitShaFromAtomAsync(
                CancellationToken cancellationToken)
        {
            try
            {
                string xml =
                    await DownloadTextAsync(
                        AddCacheBuster(
                            AtomFeedUrl),
                        "application/atom+xml",
                        cancellationToken);


                XDocument document =
                    XDocument.Parse(
                        xml);


                XNamespace atom =
                    "http://www.w3.org/2005/Atom";


                XElement? firstEntry =
                    document.Root?
                        .Elements(
                            atom +
                            "entry")
                        .FirstOrDefault();


                if (firstEntry ==
                    null)
                {
                    throw new InvalidDataException(
                        "El feed Atom no contiene commits.");
                }


                string? commitHref =
                    firstEntry
                        .Elements(
                            atom +
                            "link")
                        .Select(
                            link =>
                                (string?)link.Attribute(
                                    "href"))
                        .FirstOrDefault(
                            href =>
                                !string.IsNullOrWhiteSpace(
                                    href) &&
                                href.Contains(
                                    "/commit/",
                                    StringComparison.OrdinalIgnoreCase));


                string? sha =
                    ExtractShaFromCommitUrl(
                        commitHref);


                if (string.IsNullOrWhiteSpace(
                        sha))
                {
                    /*
                     * Fallback al <id> del Atom.
                     * GitHub suele terminarlo con el SHA del commit.
                     */
                    string id =
                        firstEntry
                            .Element(
                                atom +
                                "id")?
                            .Value ??
                        string.Empty;


                    Match idMatch =
                        Regex.Match(
                            id,
                            @"(?<sha>[0-9a-fA-F]{40})",
                            RegexOptions.CultureInvariant);


                    if (idMatch.Success)
                    {
                        sha =
                            idMatch.Groups[
                                "sha"]
                                .Value;
                    }
                }


                if (string.IsNullOrWhiteSpace(
                        sha))
                {
                    throw new InvalidDataException(
                        "No se pudo extraer el SHA del feed Atom.");
                }


                return
                    sha;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"REF WATCH Atom FAIL: {ex.GetType().Name}: {ex.Message}");


                return
                    null;
            }
        }


        private static async Task<string?>
            TryGetCommitShaFromHtmlAsync(
                CancellationToken cancellationToken)
        {
            try
            {
                string html =
                    await DownloadTextAsync(
                        AddCacheBuster(
                            CommitsHtmlUrl),
                        "text/html",
                        cancellationToken);


                Match match =
                    CommitLinkRegex.Match(
                        html);


                if (!match.Success)
                {
                    throw new InvalidDataException(
                        "No se encontró un enlace de commit en la página de commits.");
                }


                return
                    match.Groups[
                        "sha"]
                        .Value;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"REF WATCH HTML FAIL: {ex.GetType().Name}: {ex.Message}");


                return
                    null;
            }
        }


        private static async Task<string?>
            TryGetCommitShaFromSmartGitAsync(
                CancellationToken cancellationToken)
        {
            try
            {
                string advertisement =
                    await DownloadTextAsync(
                        AddCacheBuster(
                            GitSmartRefsUrl),
                        "application/x-git-upload-pack-advertisement",
                        cancellationToken);


                Match match =
                    SmartMainRefRegex.Match(
                        advertisement);


                if (!match.Success)
                {
                    throw new InvalidDataException(
                        "No se encontró refs/heads/main en Git Smart HTTP.");
                }


                return
                    match.Groups[
                        "sha"]
                        .Value;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"REF WATCH GIT FAIL: {ex.GetType().Name}: {ex.Message}");


                return
                    null;
            }
        }


        private static async Task<GlobalCountdownFeed?>
            TryDownloadFeedFromCommitAsync(
                string commitSha,
                CancellationToken cancellationToken)
        {
            try
            {
                string url =
                    $"https://raw.githubusercontent.com/{RepositoryOwner}/{RepositoryName}/{commitSha}/{CountdownFilePath}";


                string json =
                    await DownloadTextAsync(
                        AddCacheBuster(
                            url),
                        "application/json",
                        cancellationToken);


                return
                    ParseFeed(
                        json);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                WriteDiagnostic(
                    $"COMMIT RAW FAIL [{commitSha}]: {ex.GetType().Name}: {ex.Message}");


                return
                    null;
            }
        }


        private static async Task<GlobalCountdownFeed?>
            TryDownloadMutableRawFeedAsync(
                CancellationToken cancellationToken)
        {
            try
            {
                string json =
                    await DownloadTextAsync(
                        AddCacheBuster(
                            MutableRawFeedUrl),
                        "application/json",
                        cancellationToken);


                GlobalCountdownFeed feed =
                    ParseFeed(
                        json);


                WriteDiagnostic(
                    $"RAW FALLBACK: total={feed.Countdowns.Count}, " +
                    $"active={feed.Countdowns.Count(x => x.Active)}, " +
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
                    $"RAW FALLBACK FAIL: {ex.GetType().Name}: {ex.Message}");


                return
                    null;
            }
        }


        private static async Task<string> DownloadTextAsync(
            string url,
            string acceptMediaType,
            CancellationToken cancellationToken)
        {
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
                        true,

                    MaxAge =
                        TimeSpan.Zero
                };


            request.Headers.Pragma.ParseAdd(
                "no-cache");


            request.Headers.Accept.Clear();

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    acceptMediaType));


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


            return
                await response.Content
                    .ReadAsStringAsync(
                        timeout.Token);
        }


        private static string AddCacheBuster(
            string url)
        {
            string separator =
                url.Contains(
                    '?',
                    StringComparison.Ordinal)
                    ? "&"
                    : "?";


            return
                $"{url}{separator}negativeclient_live={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        }


        private static string? ExtractShaFromCommitUrl(
            string? url)
        {
            if (string.IsNullOrWhiteSpace(
                    url))
            {
                return
                    null;
            }


            Match match =
                CommitLinkRegex.Match(
                    url);


            if (!match.Success)
            {
                return
                    null;
            }


            return
                match.Groups[
                    "sha"]
                    .Value;
        }


        private static bool IsFeedNewerOrEqual(
            GlobalCountdownFeed candidate,
            GlobalCountdownFeed? current)
        {
            if (current ==
                null)
            {
                return
                    true;
            }


            if (!candidate.UpdatedAt.HasValue)
            {
                return
                    !current.UpdatedAt.HasValue;
            }


            if (!current.UpdatedAt.HasValue)
            {
                return
                    true;
            }


            return
                candidate.UpdatedAt.Value >=
                current.UpdatedAt.Value;
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


            return
                client;
        }
    }
}
