using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class OfflineSkinService
    {
        /*
         * CustomSkinLoader 14.28 ForgeV2:
         * - cliente solamente
         * - compatible con Forge 1.17.1, 1.18.x, 1.19.x y 1.20 - 1.20.4
         * - permite cargar skins locales por nombre de usuario.
         *
         * Negative Client no empaqueta el JAR. La primera vez que haga falta
         * lo obtiene desde el archivo oficial publicado en Modrinth.
         */
        private const string CustomSkinLoaderVersionApi =
            "https://api.modrinth.com/v2/version/Rcbx2QhV";

        private const string LegacyLocalSkinPackDirectoryName =
            "NegativeClient_LocalSkin";

        private const string LegacyLocalSkinPackIdentifier =
            "file/NegativeClient_LocalSkin";

        private const string CustomSkinLoaderDirectoryName =
            "CustomSkinLoader";

        private const string MarkerFileName =
            ".negativeclient-offline-skin.json";

        private static readonly HttpClient HttpClient =
            CreateHttpClient();


        private sealed class InstalledSkinMarker
        {
            public string Username { get; set; } =
                string.Empty;

            public string Model { get; set; } =
                "wide";
        }


        public void ApplyLocalSkin(
            string instanceDirectory,
            string minecraftVersion,
            string loader,
            OfflineAccountProfile profile)
        {
            /*
             * Versiones anteriores de Negative Client intentaban convertir
             * la skin en un resource pack. Eso no sustituye de forma fiable
             * la textura real del GameProfile del jugador, así que lo
             * retiramos antes de usar el cargador de skins.
             */
            RemoveLegacyResourcePackImplementation(
                instanceDirectory);

            if (string.IsNullOrWhiteSpace(
                    profile.SkinFilePath) ||
                !File.Exists(
                    profile.SkinFilePath))
            {
                DisableLocalSkin(
                    instanceDirectory);

                return;
            }

            if (!string.Equals(
                    loader,
                    "forge",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "La skin local del perfil no premium requiere una " +
                    "instancia Forge. En Minecraft Vanilla sin mods se " +
                    "usará la skin predeterminada del juego.");
            }

            if (!IsCompatibleMinecraftVersion(
                    minecraftVersion))
            {
                throw new NotSupportedException(
                    "El soporte automático de skin local está preparado " +
                    "para Forge 1.17.1, 1.18.x, 1.19.x y 1.20 - 1.20.4.");
            }

            EnsureCustomSkinLoaderInstalled(
                instanceDirectory);

            string dataDirectory =
                Path.Combine(
                    instanceDirectory,
                    CustomSkinLoaderDirectoryName);

            string localSkinDirectory =
                Path.Combine(
                    dataDirectory,
                    "LocalSkin",
                    "skins");

            Directory.CreateDirectory(
                localSkinDirectory);

            InstalledSkinMarker? previousMarker =
                LoadMarker(
                    dataDirectory);

            if (previousMarker != null &&
                !string.IsNullOrWhiteSpace(
                    previousMarker.Username) &&
                !string.Equals(
                    previousMarker.Username,
                    profile.Username,
                    StringComparison.Ordinal))
            {
                TryDeleteFile(
                    Path.Combine(
                        localSkinDirectory,
                        previousMarker.Username +
                        ".png"));
            }

            string targetSkinPath =
                Path.Combine(
                    localSkinDirectory,
                    profile.Username +
                    ".png");

            File.Copy(
                profile.SkinFilePath,
                targetSkinPath,
                overwrite: true);

            string normalizedModel =
                NormalizeLauncherModel(
                    profile.SkinModel);

            PatchCustomSkinLoaderConfig(
                dataDirectory,
                normalizedModel);

            SaveMarker(
                dataDirectory,
                new InstalledSkinMarker
                {
                    Username =
                        profile.Username,

                    Model =
                        normalizedModel
                });
        }


        public void DisableLocalSkin(
            string instanceDirectory)
        {
            RemoveLegacyResourcePackImplementation(
                instanceDirectory);

            string dataDirectory =
                Path.Combine(
                    instanceDirectory,
                    CustomSkinLoaderDirectoryName);

            InstalledSkinMarker? marker =
                LoadMarker(
                    dataDirectory);

            if (marker != null &&
                !string.IsNullOrWhiteSpace(
                    marker.Username))
            {
                TryDeleteFile(
                    Path.Combine(
                        dataDirectory,
                        "LocalSkin",
                        "skins",
                        marker.Username +
                        ".png"));
            }

            TryDeleteFile(
                Path.Combine(
                    dataDirectory,
                    MarkerFileName));
        }


        private static HttpClient CreateHttpClient()
        {
            HttpClient client =
                new HttpClient
                {
                    Timeout =
                        TimeSpan.FromSeconds(
                            45)
                };

            client.DefaultRequestHeaders
                .UserAgent
                .ParseAdd(
                    "NegativeClient/0.1.0");

            return client;
        }


        private static bool IsCompatibleMinecraftVersion(
            string minecraftVersion)
        {
            string version =
                minecraftVersion.Trim();

            if (string.Equals(
                    version,
                    "1.17.1",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (version.StartsWith(
                    "1.18",
                    StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith(
                    "1.19",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(
                    version,
                    "1.20",
                    StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith(
                    "1.20.1",
                    StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith(
                    "1.20.2",
                    StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith(
                    "1.20.3",
                    StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith(
                    "1.20.4",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }


        private static void EnsureCustomSkinLoaderInstalled(
            string instanceDirectory)
        {
            string modsDirectory =
                Path.Combine(
                    instanceDirectory,
                    "mods");

            Directory.CreateDirectory(
                modsDirectory);

            bool alreadyInstalled =
                Directory
                    .EnumerateFiles(
                        modsDirectory,
                        "*.jar",
                        SearchOption.TopDirectoryOnly)
                    .Any(
                        path =>
                            Path.GetFileName(path)
                                .Contains(
                                    "CustomSkinLoader",
                                    StringComparison.OrdinalIgnoreCase));

            if (alreadyInstalled)
            {
                return;
            }

            byte[] versionJson =
                HttpClient
                    .GetByteArrayAsync(
                        CustomSkinLoaderVersionApi)
                    .GetAwaiter()
                    .GetResult();

            using JsonDocument versionDocument =
                JsonDocument.Parse(
                    versionJson);

            JsonElement filesElement =
                versionDocument
                    .RootElement
                    .GetProperty(
                        "files");

            JsonElement? selectedFile =
                null;

            foreach (JsonElement file in
                filesElement.EnumerateArray())
            {
                bool primary =
                    file.TryGetProperty(
                        "primary",
                        out JsonElement primaryElement) &&
                    primaryElement.ValueKind ==
                        JsonValueKind.True;

                if (primary)
                {
                    selectedFile =
                        file;

                    break;
                }

                selectedFile ??=
                    file;
            }

            if (selectedFile == null)
            {
                throw new InvalidOperationException(
                    "Modrinth no devolvió el archivo de CustomSkinLoader.");
            }

            string downloadUrl =
                selectedFile.Value
                    .GetProperty(
                        "url")
                    .GetString() ??
                string.Empty;

            string fileName =
                selectedFile.Value
                    .GetProperty(
                        "filename")
                    .GetString() ??
                "CustomSkinLoader_ForgeV2_14.28.jar";

            fileName =
                Path.GetFileName(
                    fileName);

            if (string.IsNullOrWhiteSpace(
                    downloadUrl) ||
                string.IsNullOrWhiteSpace(
                    fileName))
            {
                throw new InvalidOperationException(
                    "La información de descarga de CustomSkinLoader es inválida.");
            }

            string destinationPath =
                Path.Combine(
                    modsDirectory,
                    fileName);

            string temporaryPath =
                destinationPath +
                ".download";

            try
            {
                byte[] jarBytes =
                    HttpClient
                        .GetByteArrayAsync(
                            downloadUrl)
                        .GetAwaiter()
                        .GetResult();

                File.WriteAllBytes(
                    temporaryPath,
                    jarBytes);

                File.Move(
                    temporaryPath,
                    destinationPath,
                    overwrite: true);
            }
            finally
            {
                TryDeleteFile(
                    temporaryPath);
            }
        }


        private static void PatchCustomSkinLoaderConfig(
            string dataDirectory,
            string launcherModel)
        {
            Directory.CreateDirectory(
                dataDirectory);

            string configPath =
                Path.Combine(
                    dataDirectory,
                    "CustomSkinLoader.json");

            string customSkinLoaderModel =
                string.Equals(
                    launcherModel,
                    "slim",
                    StringComparison.OrdinalIgnoreCase)
                    ? "slim"
                    : "default";

            JsonObject root =
                LoadOrCreateConfig(
                    configPath,
                    customSkinLoaderModel);

            JsonArray loadList =
                root["loadlist"]
                    as JsonArray ??
                new JsonArray();

            root["loadlist"] =
                loadList;

            JsonObject? localSkin =
                loadList
                    .OfType<JsonObject>()
                    .FirstOrDefault(
                        item =>
                            string.Equals(
                                item["name"]?
                                    .GetValue<string>(),
                                "LocalSkin",
                                StringComparison.OrdinalIgnoreCase));

            if (localSkin == null)
            {
                localSkin =
                    CreateLocalSkinProfile(
                        customSkinLoaderModel);

                loadList.Add(
                    localSkin);
            }
            else
            {
                localSkin["name"] =
                    "LocalSkin";

                localSkin["type"] =
                    "Legacy";

                localSkin["checkPNG"] =
                    false;

                localSkin["skin"] =
                    "LocalSkin/skins/{USERNAME}.png";

                localSkin["model"] =
                    customSkinLoaderModel;

                localSkin["cape"] =
                    "LocalSkin/capes/{USERNAME}.png";

                localSkin["elytra"] =
                    "LocalSkin/elytras/{USERNAME}.png";
            }

            string json =
                root.ToJsonString(
                    new JsonSerializerOptions
                    {
                        WriteIndented =
                            true
                    });

            File.WriteAllText(
                configPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
        }


        private static JsonObject LoadOrCreateConfig(
            string configPath,
            string model)
        {
            if (File.Exists(
                    configPath))
            {
                try
                {
                    JsonNode? parsed =
                        JsonNode.Parse(
                            File.ReadAllText(
                                configPath));

                    if (parsed is JsonObject existing)
                    {
                        return existing;
                    }
                }
                catch
                {
                    try
                    {
                        string brokenPath =
                            configPath +
                            ".negativeclient-broken";

                        File.Move(
                            configPath,
                            brokenPath,
                            overwrite: true);
                    }
                    catch
                    {
                    }
                }
            }

            JsonArray loadList =
                new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] =
                            "Mojang",

                        ["type"] =
                            "MojangAPI",

                        ["apiRoot"] =
                            "https://api.mojang.com/",

                        ["sessionRoot"] =
                            "https://sessionserver.mojang.com/"
                    },

                    CreateLocalSkinProfile(
                        model)
                };

            return new JsonObject
            {
                ["version"] =
                    "14.28",

                ["buildNumber"] =
                    0,

                ["loadlist"] =
                    loadList,

                ["enableTransparentSkin"] =
                    true,

                ["forceLoadAllTextures"] =
                    true,

                ["enableCape"] =
                    true,

                ["threadPoolSize"] =
                    8,

                ["enableLogStdOut"] =
                    false,

                ["cacheExpiry"] =
                    30,

                ["forceUpdateSkull"] =
                    false,

                ["enableLocalProfileCache"] =
                    false,

                ["enableCacheAutoClean"] =
                    false,

                ["forceDisableCache"] =
                    false
            };
        }


        private static JsonObject CreateLocalSkinProfile(
            string model)
        {
            return new JsonObject
            {
                ["name"] =
                    "LocalSkin",

                ["type"] =
                    "Legacy",

                ["checkPNG"] =
                    false,

                ["skin"] =
                    "LocalSkin/skins/{USERNAME}.png",

                ["model"] =
                    model,

                ["cape"] =
                    "LocalSkin/capes/{USERNAME}.png",

                ["elytra"] =
                    "LocalSkin/elytras/{USERNAME}.png"
            };
        }


        private static string NormalizeLauncherModel(
            string? model)
        {
            return string.Equals(
                    model,
                    "slim",
                    StringComparison.OrdinalIgnoreCase)
                ? "slim"
                : "wide";
        }


        private static void RemoveLegacyResourcePackImplementation(
            string instanceDirectory)
        {
            /*
             * Negative Client ya NO modifica options.txt durante la preparación
             * de skins. Versiones antiguas usaban un resource pack llamado
             * NegativeClient_LocalSkin; ahora solo retiramos esa carpeta física
             * si todavía existe.
             *
             * El options.txt queda completamente en manos del modpack/Minecraft.
             */
            try
            {
                string oldPackDirectory =
                    Path.Combine(
                        instanceDirectory,
                        "resourcepacks",
                        LegacyLocalSkinPackDirectoryName);

                if (Directory.Exists(
                        oldPackDirectory))
                {
                    Directory.Delete(
                        oldPackDirectory,
                        recursive: true);
                }
            }
            catch
            {
            }
        }


        private static InstalledSkinMarker? LoadMarker(
            string dataDirectory)
        {
            string markerPath =
                Path.Combine(
                    dataDirectory,
                    MarkerFileName);

            if (!File.Exists(
                    markerPath))
            {
                return null;
            }

            try
            {
                return JsonSerializer
                    .Deserialize<InstalledSkinMarker>(
                        File.ReadAllText(
                            markerPath));
            }
            catch
            {
                return null;
            }
        }


        private static void SaveMarker(
            string dataDirectory,
            InstalledSkinMarker marker)
        {
            Directory.CreateDirectory(
                dataDirectory);

            string json =
                JsonSerializer.Serialize(
                    marker,
                    new JsonSerializerOptions
                    {
                        WriteIndented =
                            true
                    });

            File.WriteAllText(
                Path.Combine(
                    dataDirectory,
                    MarkerFileName),
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
        }


        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
