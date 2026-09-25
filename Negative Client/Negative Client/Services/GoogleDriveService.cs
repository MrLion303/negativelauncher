using System;
using System.Collections.Generic;
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
        private static readonly CookieContainer Cookies =
            new CookieContainer();

        private static readonly HttpClient HttpClient =
            new(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    UseCookies = true,
                    CookieContainer = Cookies,
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate
                })
            {
                Timeout = TimeSpan.FromMinutes(60)
            };


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
                    response.Content.Headers.ContentType?.MediaType,
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
        // DESCARGAR ARCHIVO GRANDE
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
                firstResponse.Content.Headers.ContentType?.MediaType;

            // Si ya recibimos el ZIP/archivo directamente, lo guardamos.
            if (!IsHtmlContentType(firstContentType))
            {
                await SaveResponseToFileAsync(
                    firstResponse,
                    destinationPath,
                    progress,
                    cancellationToken);

                return;
            }

            // Google Drive puede devolver una pantalla de confirmación
            // para archivos grandes ("Google Drive can't scan this file").
            string html =
                await firstResponse.Content.ReadAsStringAsync(
                    cancellationToken);

            string? confirmationUrl =
                BuildConfirmationUrlFromHtml(html);

            if (string.IsNullOrWhiteSpace(confirmationUrl))
            {
                throw new InvalidOperationException(
                    "Google Drive devolvió una página web en lugar del archivo. " +
                    "Esto suele ocurrir con archivos grandes, límites de descarga " +
                    "o enlaces que no son accesibles públicamente.");
            }

            using HttpResponseMessage secondResponse =
                await HttpClient.GetAsync(
                    confirmationUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            secondResponse.EnsureSuccessStatusCode();

            string? secondContentType =
                secondResponse.Content.Headers.ContentType?.MediaType;

            if (IsHtmlContentType(secondContentType))
            {
                string secondHtml =
                    await secondResponse.Content.ReadAsStringAsync(
                        cancellationToken);

                string message =
                    DetectGoogleDriveError(secondHtml);

                throw new InvalidOperationException(message);
            }

            await SaveResponseToFileAsync(
                secondResponse,
                destinationPath,
                progress,
                cancellationToken);
        }


        // =====================================================
        // GUARDAR RESPUESTA A DISCO
        // =====================================================

        private static async Task SaveResponseToFileAsync(
            HttpResponseMessage response,
            string destinationPath,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            long? totalBytes =
                response.Content.Headers.ContentLength;

            string? directory =
                Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
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
                    81920,
                    useAsync: true);

            byte[] buffer =
                new byte[81920];

            long downloadedBytes =
                0;

            while (true)
            {
                int read =
                    await input.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);

                if (read <= 0)
                {
                    break;
                }

                await output.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);

                downloadedBytes +=
                    read;

                if (totalBytes.HasValue &&
                    totalBytes.Value > 0)
                {
                    double percentage =
                        (double)downloadedBytes /
                        totalBytes.Value *
                        100.0;

                    progress?.Report(
                        Math.Clamp(
                            percentage,
                            0,
                            100));
                }
            }

            progress?.Report(100);
        }


        // =====================================================
        // URL INICIAL
        // =====================================================

        private static string CreateDownloadUrl(
            string fileId)
        {
            // Este endpoint funciona mejor actualmente para
            // descargas públicas grandes que drive.google.com/uc.
            return
                "https://drive.usercontent.google.com/download" +
                $"?id={Uri.EscapeDataString(fileId)}" +
                "&export=download" +
                "&confirm=t";
        }


        // =====================================================
        // CONFIRMACIÓN DE ARCHIVOS GRANDES
        // =====================================================

        private static string? BuildConfirmationUrlFromHtml(
            string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            // Google suele devolver un formulario parecido a:
            //
            // <form action="https://drive.usercontent.google.com/download">
            // <input name="id" value="...">
            // <input name="export" value="download">
            // <input name="confirm" value="t">
            // <input name="uuid" value="...">
            //
            // Extraemos los campos ocultos y reconstruimos el GET.

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
                string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            List<string> query =
                new()
                {
                    "id=" +
                    Uri.EscapeDataString(id)
                };

            if (values.TryGetValue(
                    "export",
                    out string? exportValue) &&
                !string.IsNullOrWhiteSpace(exportValue))
            {
                query.Add(
                    "export=" +
                    Uri.EscapeDataString(exportValue));
            }
            else
            {
                query.Add("export=download");
            }

            if (values.TryGetValue(
                    "confirm",
                    out string? confirmValue) &&
                !string.IsNullOrWhiteSpace(confirmValue))
            {
                query.Add(
                    "confirm=" +
                    Uri.EscapeDataString(confirmValue));
            }
            else
            {
                query.Add("confirm=t");
            }

            if (values.TryGetValue(
                    "uuid",
                    out string? uuidValue) &&
                !string.IsNullOrWhiteSpace(uuidValue))
            {
                query.Add(
                    "uuid=" +
                    Uri.EscapeDataString(uuidValue));
            }

            return
                action +
                (action.Contains('?') ? "&" : "?") +
                string.Join("&", query);
        }


        // =====================================================
        // ERRORES DE DRIVE
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
                    "Google Drive bloqueó temporalmente la descarga porque " +
                    "el archivo recibió demasiadas descargas o vistas.";
            }

            if (lower.Contains(
                    "you need access") ||
                lower.Contains(
                    "request access"))
            {
                return
                    "Google Drive indica que el archivo no tiene acceso público.";
            }

            if (lower.Contains(
                    "can't scan this file for viruses") ||
                lower.Contains(
                    "couldn't preview file"))
            {
                return
                    "Google Drive sigue mostrando la pantalla de confirmación " +
                    "para este archivo grande y no entregó el ZIP.";
            }

            return
                "Google Drive devolvió HTML en vez del archivo descargable.";
        }


        // =====================================================
        // DETECCIÓN HTML
        // =====================================================

        private static bool IsHtmlContentType(
            string? contentType)
        {
            return
                !string.IsNullOrWhiteSpace(contentType) &&
                contentType.Contains(
                    "text/html",
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool LooksLikeHtml(
            string? contentType,
            string content)
        {
            if (IsHtmlContentType(contentType))
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
            if (string.IsNullOrWhiteSpace(fileId))
            {
                throw new ArgumentException(
                    "El ID del archivo de Google Drive está vacío.",
                    nameof(fileId));
            }
        }
    }
}
