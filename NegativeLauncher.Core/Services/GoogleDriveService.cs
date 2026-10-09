using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class GoogleDriveService
    {
        // Un buffer mayor reduce llamadas de lectura/escritura.
        private const int DownloadBufferSize =
            4 * 1024 * 1024;


        private const int MaxTransientDownloadRetries =
            8;

        // No actualizamos la UI por cada bloque descargado.
        // Eso evita miles de mensajes al hilo de WPF.
        private static readonly TimeSpan ProgressReportInterval =
            TimeSpan.FromMilliseconds(90);


        private static readonly CookieContainer Cookies =
            new CookieContainer();


        private static readonly HttpClient HttpClient =
            CreateHttpClient();


        private static HttpClient CreateHttpClient()
        {
            HttpClientHandler handler =
                new()
                {
                    AllowAutoRedirect =
                        true,

                    UseCookies =
                        true,

                    CookieContainer =
                        Cookies,

                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate,

                    MaxConnectionsPerServer =
                        16
                };

            HttpClient client =
                new(handler)
                {
                    Timeout =
                        Timeout.InfiniteTimeSpan,

                    DefaultRequestVersion =
                        HttpVersion.Version20,

                    DefaultVersionPolicy =
                        HttpVersionPolicy.RequestVersionOrLower
                };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "NegativeClient/0.1");

            return client;
        }


        // =====================================================
        // DESCARGAR TEXTO
        // =====================================================

        public async Task<string> DownloadTextFileAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            ValidateFileId(fileId);

            string url =
                CreateDownloadUrl(fileId);

            using HttpResponseMessage response =
                await HttpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseContentRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            string content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (LooksLikeHtml(
                    response.Content.Headers
                        .ContentType?
                        .MediaType,
                    content))
            {
                throw new InvalidOperationException(
                    "Google Drive devolvió una página web en lugar " +
                    "del archivo solicitado. Comprueba que el archivo " +
                    "sea público y que el ID sea correcto.");
            }

            return content;
        }


        // =====================================================
        // DESCARGAR ARCHIVO
        // =====================================================

        public async Task DownloadFileAsync(
            string fileId,
            string destinationPath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateFileId(fileId);

            string url =
                CreateDownloadUrl(fileId);

            using HttpResponseMessage firstResponse =
                await HttpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            firstResponse.EnsureSuccessStatusCode();

            string? firstContentType =
                firstResponse.Content.Headers
                    .ContentType?
                    .MediaType;

            // Camino rápido:
            // Drive ya nos está entregando el archivo.
            if (!IsHtmlContentType(
                    firstContentType))
            {
                await SaveResponseToFileAsync(
                    firstResponse,
                    destinationPath,
                    progress,
                    cancellationToken);

                return;
            }

            // Algunos archivos grandes muestran una página
            // de confirmación antes de entregar el archivo.
            string html =
                await firstResponse.Content.ReadAsStringAsync(
                    cancellationToken);

            string? confirmationUrl =
                BuildConfirmationUrlFromHtml(
                    html);

            if (string.IsNullOrWhiteSpace(
                    confirmationUrl))
            {
                throw new InvalidOperationException(
                    DetectGoogleDriveError(
                        html));
            }

            using HttpResponseMessage secondResponse =
                await HttpClient.GetAsync(
                    confirmationUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            secondResponse.EnsureSuccessStatusCode();

            string? secondContentType =
                secondResponse.Content.Headers
                    .ContentType?
                    .MediaType;

            if (IsHtmlContentType(
                    secondContentType))
            {
                string secondHtml =
                    await secondResponse.Content.ReadAsStringAsync(
                        cancellationToken);

                throw new InvalidOperationException(
                    DetectGoogleDriveError(
                        secondHtml));
            }

            await SaveResponseToFileAsync(
                secondResponse,
                destinationPath,
                progress,
                cancellationToken);
        }


        // =====================================================
        // DESCARGA REANUDABLE
        // =====================================================

        public async Task DownloadFileResumableAsync(
            string fileId,
            string destinationPath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateFileId(
                fileId);


            string partialPath =
                destinationPath +
                ".part";


            string? directory =
                Path.GetDirectoryName(
                    destinationPath);


            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }


            int transientFailureCount =
                0;


            int rangeResetCount =
                0;


            while (true)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                long existingBytes =
                    File.Exists(
                        partialPath)
                        ? new FileInfo(
                            partialPath)
                            .Length
                        : 0;


                try
                {
                    using HttpResponseMessage response =
                        await GetDownloadResponseAsync(
                            fileId,
                            existingBytes,
                            cancellationToken);


                    if (response.StatusCode ==
                        HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        long? serverLength =
                            response.Content.Headers
                                .ContentRange?
                                .Length;


                        if (serverLength.HasValue &&
                            serverLength.Value ==
                                existingBytes &&
                            existingBytes > 0)
                        {
                            File.Move(
                                partialPath,
                                destinationPath,
                                overwrite: true);


                            progress?.Report(
                                100);

                            return;
                        }


                        if (rangeResetCount >=
                            1)
                        {
                            throw new InvalidOperationException(
                                "Google Drive rechazó la reanudación del archivo.");
                        }


                        rangeResetCount++;


                        TryDeleteFile(
                            partialPath);


                        continue;
                    }


                    response.EnsureSuccessStatusCode();


                    bool append =
                        existingBytes > 0 &&
                        response.StatusCode ==
                            HttpStatusCode.PartialContent;


                    if (!append &&
                        existingBytes > 0)
                    {
                        if (rangeResetCount >=
                            1)
                        {
                            throw new InvalidOperationException(
                                "Google Drive no permitió reanudar esta descarga.");
                        }


                        rangeResetCount++;


                        TryDeleteFile(
                            partialPath);


                        continue;
                    }


                    await SaveResumableResponseAsync(
                        response,
                        partialPath,
                        progress,
                        append,
                        existingBytes,
                        cancellationToken);


                    File.Move(
                        partialPath,
                        destinationPath,
                        overwrite: true);


                    progress?.Report(
                        100);


                    return;
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                    when (IsTransientDownloadFailure(
                              ex) &&
                          transientFailureCount <
                              MaxTransientDownloadRetries)
                {
                    transientFailureCount++;


                    int delayMilliseconds =
                        Math.Min(
                            5000,
                            500 *
                            (1 <<
                             Math.Min(
                                 transientFailureCount - 1,
                                 3)));


                    await Task.Delay(
                        delayMilliseconds,
                        cancellationToken);
                }
            }
        }


        private static bool IsTransientDownloadFailure(
            Exception exception)
        {
            return
                exception is HttpRequestException ||
                exception is IOException ||
                exception is TimeoutException ||
                exception.InnerException is HttpRequestException ||
                exception.InnerException is IOException;
        }


        private static async Task<HttpResponseMessage>
            GetDownloadResponseAsync(
                string fileId,
                long offset,
                CancellationToken cancellationToken)
        {
            string url =
                CreateDownloadUrl(
                    fileId);


            HttpResponseMessage firstResponse =
                await SendDownloadRequestAsync(
                    url,
                    offset,
                    cancellationToken);


            if (firstResponse.StatusCode ==
                HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                return firstResponse;
            }


            firstResponse.EnsureSuccessStatusCode();


            string? firstContentType =
                firstResponse.Content.Headers
                    .ContentType?
                    .MediaType;


            if (!IsHtmlContentType(
                    firstContentType))
            {
                return firstResponse;
            }


            string html =
                await firstResponse.Content
                    .ReadAsStringAsync(
                        cancellationToken);


            firstResponse.Dispose();


            string? confirmationUrl =
                BuildConfirmationUrlFromHtml(
                    html);


            if (string.IsNullOrWhiteSpace(
                    confirmationUrl))
            {
                throw new InvalidOperationException(
                    DetectGoogleDriveError(
                        html));
            }


            HttpResponseMessage secondResponse =
                await SendDownloadRequestAsync(
                    confirmationUrl,
                    offset,
                    cancellationToken);


            if (secondResponse.StatusCode ==
                HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                return secondResponse;
            }


            secondResponse.EnsureSuccessStatusCode();


            string? secondContentType =
                secondResponse.Content.Headers
                    .ContentType?
                    .MediaType;


            if (IsHtmlContentType(
                    secondContentType))
            {
                string secondHtml =
                    await secondResponse.Content
                        .ReadAsStringAsync(
                            cancellationToken);


                secondResponse.Dispose();


                throw new InvalidOperationException(
                    DetectGoogleDriveError(
                        secondHtml));
            }


            return secondResponse;
        }


        private static async Task<HttpResponseMessage>
            SendDownloadRequestAsync(
                string url,
                long offset,
                CancellationToken cancellationToken)
        {
            HttpRequestMessage request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url);


            if (offset > 0)
            {
                request.Headers.Range =
                    new RangeHeaderValue(
                        offset,
                        null);
            }


            try
            {
                return
                    await HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);
            }
            finally
            {
                request.Dispose();
            }
        }


        private static async Task SaveResumableResponseAsync(
            HttpResponseMessage response,
            string partialPath,
            IProgress<double>? progress,
            bool append,
            long existingBytes,
            CancellationToken cancellationToken)
        {
            long? totalBytes =
                response.Content.Headers
                    .ContentRange?
                    .Length;


            if (!totalBytes.HasValue)
            {
                long? responseLength =
                    response.Content.Headers
                        .ContentLength;


                if (responseLength.HasValue)
                {
                    totalBytes =
                        append
                            ? existingBytes +
                              responseLength.Value
                            : responseLength.Value;
                }
            }


            await using Stream input =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken);


            await using FileStream output =
                new(
                    partialPath,
                    append
                        ? FileMode.Append
                        : FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    DownloadBufferSize,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);


            byte[] buffer =
                new byte[
                    DownloadBufferSize];


            long downloadedBytes =
                append
                    ? existingBytes
                    : 0;


            if (totalBytes.HasValue &&
                totalBytes.Value > 0)
            {
                progress?.Report(
                    Math.Clamp(
                        (double)downloadedBytes /
                        totalBytes.Value *
                        100.0,
                        0,
                        100));
            }


            double lastReportedPercentage =
                -1;


            Stopwatch progressTimer =
                Stopwatch.StartNew();


            while (true)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();


                int read =
                    await input.ReadAsync(
                        buffer.AsMemory(
                            0,
                            buffer.Length),
                        cancellationToken);


                if (read <= 0)
                {
                    break;
                }


                await output.WriteAsync(
                    buffer.AsMemory(
                        0,
                        read),
                    cancellationToken);


                downloadedBytes +=
                    read;


                if (!totalBytes.HasValue ||
                    totalBytes.Value <= 0)
                {
                    continue;
                }


                double percentage =
                    Math.Clamp(
                        (double)downloadedBytes /
                        totalBytes.Value *
                        100.0,
                        0,
                        100);


                bool enoughTimePassed =
                    progressTimer.Elapsed >=
                    ProgressReportInterval;


                bool finished =
                    percentage >= 100;


                if ((enoughTimePassed || finished) &&
                    Math.Abs(
                        percentage -
                        lastReportedPercentage) >=
                    0.1)
                {
                    progress?.Report(
                        percentage);


                    lastReportedPercentage =
                        percentage;


                    progressTimer.Restart();
                }
            }


            await output.FlushAsync(
                cancellationToken);
        }


        private static void TryDeleteFile(
            string filePath)
        {
            try
            {
                if (File.Exists(
                        filePath))
                {
                    File.Delete(
                        filePath);
                }
            }
            catch
            {
            }
        }


        // =====================================================
        // ESCRIBIR DESCARGA EN DISCO
        // =====================================================

        private static async Task SaveResponseToFileAsync(
            HttpResponseMessage response,
            string destinationPath,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            long? totalBytes =
                response.Content.Headers
                    .ContentLength;

            string? directory =
                Path.GetDirectoryName(
                    destinationPath);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            await using Stream input =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            await using FileStream output =
                new(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    DownloadBufferSize,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

            byte[] buffer =
                new byte[DownloadBufferSize];

            long downloadedBytes =
                0;

            double lastReportedPercentage =
                -1;

            Stopwatch progressTimer =
                Stopwatch.StartNew();

            while (true)
            {
                int read =
                    await input.ReadAsync(
                        buffer.AsMemory(
                            0,
                            buffer.Length),
                        cancellationToken);

                if (read <= 0)
                {
                    break;
                }

                await output.WriteAsync(
                    buffer.AsMemory(
                        0,
                        read),
                    cancellationToken);

                downloadedBytes +=
                    read;

                if (!totalBytes.HasValue ||
                    totalBytes.Value <= 0)
                {
                    continue;
                }

                double percentage =
                    Math.Clamp(
                        (double)downloadedBytes /
                        totalBytes.Value *
                        100.0,
                        0,
                        100);

                bool enoughTimePassed =
                    progressTimer.Elapsed >=
                    ProgressReportInterval;

                bool finished =
                    percentage >= 100;

                if ((enoughTimePassed || finished) &&
                    Math.Abs(
                        percentage -
                        lastReportedPercentage) >= 0.1)
                {
                    progress?.Report(
                        percentage);

                    lastReportedPercentage =
                        percentage;

                    progressTimer.Restart();
                }
            }

            await output.FlushAsync(
                cancellationToken);

            progress?.Report(100);
        }


        // =====================================================
        // URL DE DESCARGA
        // =====================================================

        private static string CreateDownloadUrl(
            string fileId)
        {
            return
                "https://drive.usercontent.google.com/download" +
                $"?id={Uri.EscapeDataString(fileId)}" +
                "&export=download" +
                "&confirm=t";
        }


        // =====================================================
        // PÁGINA DE CONFIRMACIÓN DE DRIVE
        // =====================================================

        private static string? BuildConfirmationUrlFromHtml(
            string html)
        {
            if (string.IsNullOrWhiteSpace(
                    html))
            {
                return null;
            }

            Match formMatch =
                Regex.Match(
                    html,
                    @"<form[^>]+action=""(?<action>[^""]+)""[^>]*>",
                    RegexOptions.IgnoreCase);

            if (!formMatch.Success)
            {
                return null;
            }

            string action =
                WebUtility.HtmlDecode(
                    formMatch.Groups["action"].Value);

            MatchCollection inputMatches =
                Regex.Matches(
                    html,
                    @"<input[^>]+name=""(?<name>[^""]+)""[^>]+value=""(?<value>[^""]*)""[^>]*>",
                    RegexOptions.IgnoreCase);

            Dictionary<string, string> values =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (Match inputMatch in inputMatches)
            {
                string name =
                    WebUtility.HtmlDecode(
                        inputMatch.Groups["name"].Value);

                string value =
                    WebUtility.HtmlDecode(
                        inputMatch.Groups["value"].Value);

                values[name] =
                    value;
            }

            if (!values.TryGetValue(
                    "id",
                    out string? id) ||
                string.IsNullOrWhiteSpace(
                    id))
            {
                return null;
            }

            List<string> query =
                new()
                {
                    "id=" +
                    Uri.EscapeDataString(
                        id)
                };

            if (values.TryGetValue(
                    "export",
                    out string? exportValue) &&
                !string.IsNullOrWhiteSpace(
                    exportValue))
            {
                query.Add(
                    "export=" +
                    Uri.EscapeDataString(
                        exportValue));
            }
            else
            {
                query.Add(
                    "export=download");
            }

            if (values.TryGetValue(
                    "confirm",
                    out string? confirmValue) &&
                !string.IsNullOrWhiteSpace(
                    confirmValue))
            {
                query.Add(
                    "confirm=" +
                    Uri.EscapeDataString(
                        confirmValue));
            }
            else
            {
                query.Add(
                    "confirm=t");
            }

            if (values.TryGetValue(
                    "uuid",
                    out string? uuidValue) &&
                !string.IsNullOrWhiteSpace(
                    uuidValue))
            {
                query.Add(
                    "uuid=" +
                    Uri.EscapeDataString(
                        uuidValue));
            }

            return
                action +
                (action.Contains('?')
                    ? "&"
                    : "?") +
                string.Join(
                    "&",
                    query);
        }


        // =====================================================
        // ERRORES DE GOOGLE DRIVE
        // =====================================================

        private static string DetectGoogleDriveError(
            string html)
        {
            string lower =
                html.ToLowerInvariant();

            if (lower.Contains(
                    "too many users have viewed or downloaded") ||
                lower.Contains(
                    "too many users have viewed this file"))
            {
                return
                    "Google Drive limitó temporalmente " +
                    "la descarga de este archivo.";
            }

            if (lower.Contains(
                    "you need access") ||
                lower.Contains(
                    "request access"))
            {
                return
                    "Google Drive indica que el archivo " +
                    "no tiene acceso público.";
            }

            if (lower.Contains(
                    "can't scan this file for viruses") ||
                lower.Contains(
                    "couldn't preview file"))
            {
                return
                    "Google Drive sigue mostrando la " +
                    "confirmación de archivo grande y " +
                    "no entregó el ZIP.";
            }

            return
                "Google Drive devolvió una página web " +
                "en lugar del archivo descargable.";
        }


        // =====================================================
        // DETECCIÓN HTML
        // =====================================================

        private static bool IsHtmlContentType(
            string? contentType)
        {
            return
                !string.IsNullOrWhiteSpace(
                    contentType) &&
                contentType.Contains(
                    "text/html",
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool LooksLikeHtml(
            string? contentType,
            string content)
        {
            if (IsHtmlContentType(
                    contentType))
            {
                return true;
            }

            string clean =
                content.TrimStart();

            return
                clean.StartsWith(
                    "<!DOCTYPE html",
                    StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith(
                    "<html",
                    StringComparison.OrdinalIgnoreCase);
        }


        // =====================================================
        // VALIDACIÓN
        // =====================================================

        private static void ValidateFileId(
            string fileId)
        {
            if (string.IsNullOrWhiteSpace(
                    fileId))
            {
                throw new ArgumentException(
                    "El ID del archivo de Google Drive " +
                    "está vacío.",
                    nameof(fileId));
            }
        }
    }
}
