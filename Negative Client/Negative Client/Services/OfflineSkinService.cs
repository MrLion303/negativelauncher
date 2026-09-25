using System;
using System.IO;
using System.Text;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class OfflineSkinService
    {
        public const string LocalSkinPackDirectoryName =
            "NegativeClient_LocalSkin";

        public const string LocalSkinPackIdentifier =
            "file/NegativeClient_LocalSkin";

        private static readonly string[] DefaultSkinNames =
        {
            "alex",
            "ari",
            "efe",
            "kai",
            "makena",
            "noor",
            "steve",
            "sunny",
            "zuri"
        };

        public void ApplyLocalSkin(
            string instanceDirectory,
            string minecraftVersion,
            OfflineAccountProfile profile)
        {
            string optionsPath =
                Path.Combine(
                    instanceDirectory,
                    "options.txt");

            if (string.IsNullOrWhiteSpace(profile.SkinFilePath) ||
                !File.Exists(profile.SkinFilePath))
            {
                DisableLocalSkin(
                    instanceDirectory);

                return;
            }

            string resourcePacksDirectory =
                Path.Combine(
                    instanceDirectory,
                    "resourcepacks");

            string packDirectory =
                Path.Combine(
                    resourcePacksDirectory,
                    LocalSkinPackDirectoryName);

            Directory.CreateDirectory(
                packDirectory);

            int packFormat =
                GetPackFormat(
                    minecraftVersion);

            string packMetadata =
                "{\n" +
                "  \"pack\": {\n" +
                $"    \"pack_format\": {packFormat},\n" +
                "    \"description\": \"Negative Client - skin local no premium\"\n" +
                "  }\n" +
                "}\n";

            File.WriteAllText(
                Path.Combine(
                    packDirectory,
                    "pack.mcmeta"),
                packMetadata,
                new UTF8Encoding(false));

            foreach (string modelFolder in
                new[] { "wide", "slim" })
            {
                string targetDirectory =
                    Path.Combine(
                        packDirectory,
                        "assets",
                        "minecraft",
                        "textures",
                        "entity",
                        "player",
                        modelFolder);

                Directory.CreateDirectory(
                    targetDirectory);

                foreach (string skinName in
                    DefaultSkinNames)
                {
                    File.Copy(
                        profile.SkinFilePath,
                        Path.Combine(
                            targetDirectory,
                            skinName + ".png"),
                        overwrite: true);
                }
            }

            ResourcePackSelectionService
                .SetExtraResourcePackEnabled(
                    optionsPath,
                    LocalSkinPackIdentifier,
                    enabled: true,
                    markIncompatible: false);
        }

        public void DisableLocalSkin(
            string instanceDirectory)
        {
            string optionsPath =
                Path.Combine(
                    instanceDirectory,
                    "options.txt");

            if (!File.Exists(optionsPath))
            {
                return;
            }

            ResourcePackSelectionService
                .SetExtraResourcePackEnabled(
                    optionsPath,
                    LocalSkinPackIdentifier,
                    enabled: false);
        }

        private static int GetPackFormat(
            string minecraftVersion)
        {
            string version =
                minecraftVersion.Trim();

            if (version.StartsWith("1.20.1", StringComparison.OrdinalIgnoreCase) ||
                version.Equals("1.20", StringComparison.OrdinalIgnoreCase))
            {
                return 15;
            }

            if (version.StartsWith("1.20.2", StringComparison.OrdinalIgnoreCase))
            {
                return 18;
            }

            if (version.StartsWith("1.20.3", StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith("1.20.4", StringComparison.OrdinalIgnoreCase))
            {
                return 22;
            }

            if (version.StartsWith("1.20.5", StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith("1.20.6", StringComparison.OrdinalIgnoreCase))
            {
                return 32;
            }

            if (version.StartsWith("1.21", StringComparison.OrdinalIgnoreCase))
            {
                return 34;
            }

            // Para versiones desconocidas dejamos el formato de 1.20/1.20.1.
            // Si Minecraft lo considera incompatible, el pack sigue estando
            // disponible y no afecta al funcionamiento del launcher.
            return 15;
        }
    }
}
