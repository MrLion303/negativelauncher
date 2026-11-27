using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    /// <summary>
    /// Aplica una mitigación preventiva para Minecraft2Shell únicamente cuando
    /// la versión instalada no declara FreeType 2.15.0 o no se puede verificar.
    ///
    /// El agente M2SFix es un componente de terceros, fijado por versión y SHA-256.
    /// No sustituye una actualización oficial de Minecraft/Mojang.
    /// </summary>
    public sealed class Minecraft2ShellProtectionService
    {
        private const string AgentVersion = "2.3.0";
        private const string AgentSha256 =
            "c073f82a460559151f720f635266077248807eb6939a0eee4aa10d5081405bbe";

        private const int MaximumAgentSize = 1_000_000;

        private static readonly Uri AgentUri =
            new Uri(
                "https://github.com/sunrisemc-tw/minecraft2shell-fix/releases/download/" +
                "v2.3.0/m2sfix-agent-2.3.0.jar");

        private static readonly HttpClient HttpClient =
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45)
            };

        /// <summary>
        /// Devuelve la ruta del agente si la versión necesita protección.
        /// Devuelve null cuando la versión no está afectada o ya declara FreeType 2.15.0.
        /// Si hace falta proteger y no se puede verificar/descargar el agente, falla cerrado.
        /// </summary>
        public async Task<string?> EnsureAgentIfRequiredAsync(
            InstalledInstance instance,
            IProgress<string>? status = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsPotentiallyAffectedVersion(instance.MinecraftVersion))
            {
                return null;
            }

            status?.Report(
                "Comprobando la protección contra Minecraft2Shell...");

            if (await UsesPatchedFreeTypeAsync(
                    instance.MinecraftVersion,
                    cancellationToken))
            {
                status?.Report(
                    "La versión de Minecraft ya incluye FreeType 2.15.0 o posterior.");

                return null;
            }

            status?.Report(
                "Preparando protección preventiva para Minecraft2Shell...");

            return await EnsureAgentAsync(
                status,
                cancellationToken);
        }

        private static bool IsPotentiallyAffectedVersion(
            string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                // Un identificador desconocido se trata de forma conservadora.
                return true;
            }

            string[] parts =
                version.Split(
                    '.',
                    '-',
                    '+');

            if (parts.Length < 2 ||
                !int.TryParse(parts[0], out int major) ||
                !int.TryParse(parts[1], out int minor))
            {
                return true;
            }

            int patch = 0;

            if (parts.Length >= 3)
            {
                string patchDigits =
                    new string(
                        parts[2]
                            .TakeWhile(char.IsDigit)
                            .ToArray());

                if (patchDigits.Length > 0)
                {
                    int.TryParse(
                        patchDigits,
                        out patch);
                }
            }

            // FreeType entra en el cliente desde Minecraft 1.20.5.
            if (major > 1)
            {
                return true;
            }

            if (major < 1)
            {
                return false;
            }

            if (minor > 20)
            {
                return true;
            }

            if (minor < 20)
            {
                return false;
            }

            return patch >= 5;
        }

        private static async Task<bool> UsesPatchedFreeTypeAsync(
            string minecraftVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(minecraftVersion) ||
                    !string.Equals(
                        Path.GetFileName(minecraftVersion),
                        minecraftVersion,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                string versionJsonPath =
                    Path.Combine(
                        InstanceService.SharedMinecraftVersionsRoot,
                        minecraftVersion,
                        minecraftVersion + ".json");

                if (!File.Exists(versionJsonPath))
                {
                    return false;
                }

                await using FileStream stream =
                    new FileStream(
                        versionJsonPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 16 * 1024,
                        useAsync: true);

                using JsonDocument document =
                    await JsonDocument.ParseAsync(
                        stream,
                        cancellationToken: cancellationToken);

                if (!document.RootElement.TryGetProperty(
                        "libraries",
                        out JsonElement libraries) ||
                    libraries.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                bool foundFreeType = false;
                bool everyFreeTypeEntryIsPatched = true;

                foreach (JsonElement library in libraries.EnumerateArray())
                {
                    if (!library.TryGetProperty(
                            "name",
                            out JsonElement nameElement) ||
                        nameElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string name =
                        nameElement.GetString() ?? string.Empty;

                    if (!name.StartsWith(
                            "org.lwjgl:lwjgl-freetype:",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foundFreeType = true;

                    string[] coordinates =
                        name.Split(':');

                    string libraryVersion =
                        coordinates.Length >= 3
                            ? coordinates[2]
                            : string.Empty;

                    if (!libraryVersion.Contains(
                            "2.15.0",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        everyFreeTypeEntryIsPatched = false;
                    }
                }

                // Si falta información o aparece una coordenada antigua/desconocida,
                // no damos por segura la instalación y activamos el agente.
                return
                    foundFreeType &&
                    everyFreeTypeEntryIsPatched;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // No poder verificar el manifiesto nunca se interpreta como "seguro".
                return false;
            }
        }

        private static async Task<string> EnsureAgentAsync(
            IProgress<string>? status,
            CancellationToken cancellationToken)
        {
            string securityDirectory =
                Path.Combine(
                    InstanceService.LauncherRoot,
                    "Security");

            Directory.CreateDirectory(
                securityDirectory);

            string agentPath =
                Path.Combine(
                    securityDirectory,
                    $"m2sfix-agent-{AgentVersion}.jar");

            if (await HasExpectedHashAsync(
                    agentPath,
                    cancellationToken))
            {
                return agentPath;
            }

            if (File.Exists(agentPath))
            {
                File.Delete(agentPath);
            }

            string temporaryPath =
                agentPath + "." +
                Guid.NewGuid().ToString("N") +
                ".part";

            try
            {
                status?.Report(
                    "Descargando el componente de protección...");

                using HttpResponseMessage response =
                    await HttpClient.GetAsync(
                        AgentUri,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength is long contentLength &&
                    (contentLength <= 0 || contentLength > MaximumAgentSize))
                {
                    throw new InvalidDataException(
                        "El tamaño del componente de protección no es válido.");
                }

                byte[] bytes =
                    await response.Content.ReadAsByteArrayAsync(
                        cancellationToken);

                if (bytes.Length == 0 ||
                    bytes.Length > MaximumAgentSize)
                {
                    throw new InvalidDataException(
                        "El archivo descargado supera el tamaño permitido.");
                }

                byte[] actualHash =
                    SHA256.HashData(bytes);

                byte[] expectedHash =
                    Convert.FromHexString(AgentSha256);

                if (!CryptographicOperations.FixedTimeEquals(
                        actualHash,
                        expectedHash))
                {
                    throw new InvalidDataException(
                        "La comprobación SHA-256 del componente de protección no coincide.");
                }

                await File.WriteAllBytesAsync(
                    temporaryPath,
                    bytes,
                    cancellationToken);

                File.Move(
                    temporaryPath,
                    agentPath,
                    overwrite: true);

                status?.Report(
                    "Protección Minecraft2Shell preparada.");

                return agentPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "No se inició Minecraft porque Negative Client no pudo verificar " +
                    "o preparar la protección preventiva contra Minecraft2Shell. " +
                    "Comprueba tu conexión e inténtalo de nuevo.",
                    exception);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // La limpieza del temporal no debe ocultar el resultado principal.
                }
            }
        }

        private static async Task<bool> HasExpectedHashAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var fileInfo = new FileInfo(filePath);

                if (fileInfo.Length <= 0 ||
                    fileInfo.Length > MaximumAgentSize)
                {
                    return false;
                }

                byte[] bytes =
                    await File.ReadAllBytesAsync(
                        filePath,
                        cancellationToken);

                byte[] actualHash =
                    SHA256.HashData(bytes);

                byte[] expectedHash =
                    Convert.FromHexString(AgentSha256);

                return CryptographicOperations.FixedTimeEquals(
                    actualHash,
                    expectedHash);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }
    }
}
