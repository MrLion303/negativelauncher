using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class GoogleDriveService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };


        public async Task<string> DownloadTextFileAsync(
            string fileId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                throw new ArgumentException(
                    "El ID del archivo de Google Drive está vacío.",
                    nameof(fileId));
            }


            string url =
                "https://drive.google.com/uc" +
                $"?export=download&id={Uri.EscapeDataString(fileId)}";


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


            // Si Drive devuelve una página HTML en lugar
            // del JSON, sabemos que algo salió mal.
            if (cleanContent.StartsWith("<!DOCTYPE html",
                    StringComparison.OrdinalIgnoreCase) ||
                cleanContent.StartsWith("<html",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Google Drive devolvió una página web " +
                    "en lugar del archivo solicitado. " +
                    "Comprueba que el archivo sea público.");
            }


            return content;
        }
    }
}