using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    public sealed class OfflineAccountService
    {
        private readonly string AccountsDirectory;
        private readonly string ProfilePath;
        private readonly string StoredSkinPath;

        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

        public OfflineAccountService()
            : this(LauncherPaths.DefaultLauncherRoot)
        {
        }

        // Permite probar el almacenamiento en una carpeta temporal sin tocar datos reales.
        public OfflineAccountService(string launcherRoot)
        {
            if (string.IsNullOrWhiteSpace(launcherRoot))
            {
                throw new ArgumentException(
                    "La carpeta de datos no puede estar vacía.",
                    nameof(launcherRoot));
            }

            AccountsDirectory = Path.Combine(
                Path.GetFullPath(launcherRoot),
                "accounts");

            ProfilePath = Path.Combine(
                AccountsDirectory,
                "offline-profile.json");

            StoredSkinPath = Path.Combine(
                AccountsDirectory,
                "offline-skin.png");
        }

        public async Task<OfflineAccountProfile?> LoadAsync()
        {
            try
            {
                if (!File.Exists(ProfilePath))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(ProfilePath);
                OfflineAccountProfile? profile =
                    JsonSerializer.Deserialize<OfflineAccountProfile>(json, _jsonOptions);

                if (profile == null || string.IsNullOrWhiteSpace(profile.Username))
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(profile.SkinFilePath) &&
                    !File.Exists(profile.SkinFilePath) &&
                    File.Exists(StoredSkinPath))
                {
                    profile.SkinFilePath = StoredSkinPath;
                }

                profile.SkinModel = NormalizeSkinModel(profile.SkinModel);
                return profile;
            }
            catch
            {
                return null;
            }
        }

        public async Task<OfflineAccountProfile> SaveAsync(
            string username,
            string? sourceSkinPath,
            string skinModel,
            bool removeSkin = false)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(
                    "El nombre del perfil no puede estar vacío.",
                    nameof(username));
            }

            Directory.CreateDirectory(AccountsDirectory);
            string savedSkinPath = string.Empty;

            if (removeSkin)
            {
                TryDelete(StoredSkinPath);
            }
            else if (!string.IsNullOrWhiteSpace(sourceSkinPath))
            {
                if (!File.Exists(sourceSkinPath))
                {
                    throw new FileNotFoundException(
                        "La skin seleccionada ya no existe.",
                        sourceSkinPath);
                }

                if (!string.Equals(
                    Path.GetExtension(sourceSkinPath),
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "La skin debe ser un archivo PNG.");
                }

                string sourceFullPath = Path.GetFullPath(sourceSkinPath);
                string storedFullPath = Path.GetFullPath(StoredSkinPath);

                if (!string.Equals(
                    sourceFullPath,
                    storedFullPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceFullPath, storedFullPath, overwrite: true);
                }

                savedSkinPath = StoredSkinPath;
            }
            else if (File.Exists(StoredSkinPath))
            {
                savedSkinPath = StoredSkinPath;
            }

            OfflineAccountProfile profile = new()
            {
                Username = username.Trim(),
                SkinFilePath = savedSkinPath,
                SkinModel = NormalizeSkinModel(skinModel),
                SavedAtUtc = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(profile, _jsonOptions);
            string temporaryPath = ProfilePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json);
            File.Move(temporaryPath, ProfilePath, overwrite: true);
            return profile;
        }

        public async Task DeleteAsync()
        {
            await Task.Run(() =>
            {
                TryDelete(ProfilePath);
                TryDelete(StoredSkinPath);
            });
        }

        private static string NormalizeSkinModel(string? model)
        {
            return string.Equals(model, "slim", StringComparison.OrdinalIgnoreCase)
                ? "slim"
                : "wide";
        }

        private static void TryDelete(string path)
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
