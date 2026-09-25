using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class GoogleDriveService
    {
        private static readonly HttpClient HttpClient =
            new()
            {
                Timeout = TimeSpan.FromMinutes(30)
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
                    cancellationToken);


            response.EnsureSuccessStatusCode();


            string content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);


            string cleanContent =
                content.TrimStart();


            if (cleanContent.StartsWith(
                    "<!DOCTYPE html",
                    StringComparison.OrdinalIgnoreCase) ||
                cleanContent.StartsWith(
                    "<html",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Google Drive devolvió HTML en lugar " +
                    "del archivo. Comprueba que el archivo " +
                    "sea público.");
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


            using HttpResponseMessage response =
                await HttpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);


            response.EnsureSuccessStatusCode();


            string? contentType =
                response.Content.Headers
                    .ContentType?
                    .MediaType;


            if (!string.IsNullOrWhiteSpace(contentType) &&
                contentType.Contains(
                    "text/html",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Google Drive devolvió una página web " +
                    "en lugar del archivo. Comprueba que " +
                    "el ZIP tenga acceso público.");
            }


            long? totalBytes =
                response.Content.Headers.ContentLength;


            string? directory =
                Path.GetDirectoryName(
                    destinationPath);


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


            long downloadedBytes = 0;


            while (true)
            {
                int read =
                    await input.ReadAsync(
                        buffer,
                        cancellationToken);


                if (read <= 0)
                {
                    break;
                }


                await output.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);


                downloadedBytes += read;


                if (totalBytes.HasValue &&
                    totalBytes.Value > 0)
                {
                    double percentage =
                        (double)downloadedBytes /
                        totalBytes.Value *
                        100.0;


                    progress?.Report(
                        percentage);
                }
            }


            progress?.Report(100);
        }


        private static string CreateDownloadUrl(
            string fileId)
        {
            return
                "https://drive.google.com/uc" +
                "?export=download" +
                "&confirm=t" +
                $"&id={Uri.EscapeDataString(fileId)}";
        }


        private static void ValidateFileId(
            string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                throw new ArgumentException(
                    "El ID del archivo de Google Drive " +
                    "está vacío.",
                    nameof(fileId));
            }
        }
    }
}