using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class GoogleDriveService
    {
        // Un buffer mayor reduce llamadas de lectura/escritura.
        private const int DownloadBufferSize =
            1024 * 1024;

        // No actualizamos la UI por cada bloque descargado.
        // Eso evita miles de mensajes al hilo de WPF.
        private static readonly TimeSpan ProgressReportInterval =
            TimeSpan.FromMilliseconds(120);


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
                        8
                };

            HttpClient client =
                new(handler)
                {
                    Timeout =
                        TimeSpan.FromMinutes(60),

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
