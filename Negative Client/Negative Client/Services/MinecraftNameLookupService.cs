using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class MinecraftNameLookupResult
    {
        public bool IsRegistered { get; init; }

        public bool IsAvailable =>
            !IsRegistered;

        public string CanonicalName { get; init; } =
            string.Empty;

        public string Uuid { get; init; } =
            string.Empty;
    }

    public sealed class MinecraftNameLookupService
    {
        private static readonly Regex UsernamePattern =
            new Regex(
                "^[A-Za-z0-9_]{3,16}$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly HttpClient _httpClient;

        public MinecraftNameLookupService()
        {
            _httpClient =
                new HttpClient
                {
                    Timeout =
                        TimeSpan.FromSeconds(12)
                };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "NegativeClient/0.1.0");
        }

        public static bool IsValidMinecraftUsername(
            string username)
        {
            return
                !string.IsNullOrWhiteSpace(username) &&
                UsernamePattern.IsMatch(username.Trim());
        }

        public async Task<MinecraftNameLookupResult> LookupAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            username =
                username.Trim();

            if (!IsValidMinecraftUsername(username))
            {
                throw new InvalidOperationException(
                    "El nombre debe tener de 3 a 16 caracteres y usar solo letras, números o guion bajo.");
            }

            string encoded =
                Uri.EscapeDataString(username);

            string[] endpoints =
            {
                $"https://api.minecraftservices.com/minecraft/profile/lookup/name/{encoded}",
                $"https://api.mojang.com/users/profiles/minecraft/{encoded}"
            };

            Exception? lastError =
                null;

            foreach (string endpoint in endpoints)
            {
                try
                {
                    using HttpRequestMessage request =
                        new HttpRequestMessage(
                            HttpMethod.Get,
                            endpoint);

                    request.Headers.Accept.ParseAdd(
                        "application/json");

                    using HttpResponseMessage response =
                        await _httpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken);

                    if (response.StatusCode == HttpStatusCode.NotFound ||
                        response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return new MinecraftNameLookupResult
                        {
                            IsRegistered =
                                false
                        };
                    }

                    if ((int)response.StatusCode == 429)
                    {
                        throw new InvalidOperationException(
                            "El servicio de Minecraft está limitando temporalmente las consultas de nombres. Inténtalo de nuevo en unos segundos.");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        lastError =
                            new HttpRequestException(
                                $"Minecraft respondió {(int)response.StatusCode} ({response.ReasonPhrase}).");

                        continue;
                    }

                    string json =
                        await response.Content.ReadAsStringAsync(
                            cancellationToken);

                    using JsonDocument document =
                        JsonDocument.Parse(json);

                    string canonicalName =
                        document.RootElement.TryGetProperty(
                            "name",
                            out JsonElement nameElement)
                            ? nameElement.GetString() ?? username
                            : username;

                    string uuid =
                        document.RootElement.TryGetProperty(
                            "id",
                            out JsonElement idElement)
                            ? idElement.GetString() ?? string.Empty
                            : string.Empty;

                    return new MinecraftNameLookupResult
                    {
                        IsRegistered =
                            true,

                        CanonicalName =
                            canonicalName,

                        Uuid =
                            uuid
                    };
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError =
                        ex;
                }
            }

            throw new InvalidOperationException(
                "No se pudo comprobar el nombre con los servicios oficiales de Minecraft. Por seguridad no se guardó el perfil no premium.",
                lastError);
        }
    }
}
