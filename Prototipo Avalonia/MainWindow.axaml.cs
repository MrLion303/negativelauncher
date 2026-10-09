using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Negative_Client.Models;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow : Window
{
    private readonly LauncherPreferencesService _preferencesService = new();
    private readonly InstanceService _instanceService = new();
    private readonly MinecraftRuntimeService _runtimeService;
    private readonly OfflineAccountService _offlineAccountService = new();
    private readonly ObservableCollection<InstalledInstance> _instances = new();
    private LauncherPreferences _preferences = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadBrandingAssets();
        _runtimeService = new MinecraftRuntimeService(_instanceService);
        InstancesList.ItemsSource = _instances;
        OpenPage("Inicio");
        Opened += async (_, _) => await InitializeAsync();
    }


    private void LoadBrandingAssets()
    {
        string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "negativeclient_logo.png");
        if (!File.Exists(logoPath))
            return;

        try
        {
            BrandLogo.Source = new Bitmap(logoPath);
            BrandLogo.IsVisible = true;
            BrandLogoFallback.IsVisible = false;
        }
        catch
        {
            // Si el archivo de marca no está disponible, se conserva el distintivo de respaldo.
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            _preferences = await _preferencesService.LoadAsync();
            _instanceService.ConfigureStorageRoot(_preferences.StorageRootPath);
            FillSettings();
            var offlineProfile = await _offlineAccountService.LoadAsync();
            OfflineUsernameInput.Text = offlineProfile?.Username ?? string.Empty;
            if (offlineProfile is not null)
            {
                OfflineProfileStatus.Text = "Perfil guardado: " + offlineProfile.Username;
                OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3B1"));
                OfflineProfileStatus.IsVisible = true;
            }
            await RefreshInstancesAsync();
            await RefreshGalleryAsync();
            HeaderStatus.Text = "Datos cargados";
        }
        catch (Exception ex)
        {
            HeaderStatus.Text = "No se pudieron cargar los datos";
            ShowSettingsMessage("No se pudieron cargar los datos del launcher: " + ex.Message, false);
        }
    }

    private void FillSettings()
    {
        RamInput.Text = _preferences.MaximumRamMb.ToString();
        AutomaticJavaInput.IsChecked = _preferences.UseAutomaticJava;
        JavaPathInput.Text = _preferences.CustomJavaPath;
        CloseLauncherInput.IsChecked = _preferences.CloseLauncherOnGameStart;
        ShowConsoleInput.IsChecked = _preferences.ShowGameConsole;
        CatalogFileIdInput.Text = _preferences.ModpackCatalogFileId;
        StoragePathInput.Text = string.IsNullOrWhiteSpace(_preferences.StorageRootPath)
            ? InstanceService.DefaultStorageRoot
            : _preferences.StorageRootPath;
        HomeRam.Text = $"{_preferences.MaximumRamMb} MB";
    }

    private async Task RefreshInstancesAsync()
    {
        try
        {
            var loaded = await _instanceService.LoadAllAsync();
            _instances.Clear();
            foreach (var instance in loaded.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                _instances.Add(instance);

            InstancesEmpty.IsVisible = _instances.Count == 0;
            HomeInstanceCount.Text = _instances.Count.ToString();
            InstancesPathLabel.Text = "Carpeta: " + _instanceService.GetStorageRoot();
            InstancesMessage.IsVisible = false;
        }
        catch (Exception ex)
        {
            InstancesMessage.Text = "No se pudieron leer las instancias: " + ex.Message;
            InstancesMessage.IsVisible = true;
        }
    }

    private void OpenPage(string page)
    {
        HomePage.IsVisible = page == "Inicio";
        InstancesPage.IsVisible = page == "Instancias";
        GalleryPage.IsVisible = page == "Galería";
        SettingsPage.IsVisible = page == "Ajustes";
        ModpacksPage.IsVisible = page == "Modpacks";

        SetNav(HomeNav, page == "Inicio");
        SetNav(InstancesNav, page == "Instancias");
        SetNav(ModpacksNav, page == "Modpacks");
        SetNav(GalleryNav, page == "Galería");
        SetNav(SettingsNav, page == "Ajustes");
    }

    private static void SetNav(Button button, bool selected)
    {
        button.Background = selected ? Avalonia.Media.Brushes.Transparent : Avalonia.Media.Brushes.Transparent;
        button.Foreground = selected
            ? Avalonia.Media.Brushes.Cyan
            : Avalonia.Media.Brushes.Gainsboro;
        if (selected)
            button.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1A2C35"));
    }

    private void HomeNav_Click(object? sender, RoutedEventArgs e) => OpenPage("Inicio");
    private async void InstancesNav_Click(object? sender, RoutedEventArgs e)
    {
        OpenPage("Instancias");
        await RefreshInstancesAsync();
    }
    private void ModpacksNav_Click(object? sender, RoutedEventArgs e) => OpenPage("Modpacks");
    private async void GalleryNav_Click(object? sender, RoutedEventArgs e)
    {
        OpenPage("Galería");
        await RefreshGalleryAsync();
    }

    private async void RefreshGallery_Click(object? sender, RoutedEventArgs e)
        => await RefreshGalleryAsync();

    private async Task RefreshGalleryAsync()
    {
        GalleryImagesPanel.Children.Clear();
        string archiveRoot = Path.Combine(LauncherPaths.DefaultLauncherRoot, "archived-screenshots");
        GalleryPathLabel.Text = "Carpeta: " + archiveRoot;

        try
        {
            var store = new ScreenshotArchiveStore(archiveRoot);
            var archives = store.GetArchives();
            int imageCount = 0;

            foreach (var archive in archives.OrderByDescending(item => item.InstanceName, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(archive.ScreenshotsDirectory) ||
                    !Directory.Exists(archive.ScreenshotsDirectory))
                    continue;

                foreach (string file in Directory.EnumerateFiles(archive.ScreenshotsDirectory, "*", SearchOption.AllDirectories)
                    .Where(IsGalleryImage)
                    .OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    try
                    {
                        var bitmap = new Bitmap(file);
                        var image = new Image
                        {
                            Source = bitmap,
                            Width = 190,
                            Height = 112,
                            Stretch = Avalonia.Media.Stretch.UniformToFill
                        };

                        var card = new Border
                        {
                            Width = 210,
                            Margin = new Avalonia.Thickness(0, 0, 12, 12),
                            Padding = new Avalonia.Thickness(9),
                            CornerRadius = new Avalonia.CornerRadius(8),
                            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0D1218")),
                            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#27323D")),
                            BorderThickness = new Avalonia.Thickness(1),
                            Child = new StackPanel
                            {
                                Spacing = 7,
                                Children =
                                {
                                    image,
                                    new TextBlock
                                    {
                                        Text = archive.InstanceName,
                                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                                    },
                                    new TextBlock
                                    {
                                        Text = Path.GetFileName(file),
                                        FontSize = 10,
                                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5")),
                                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                                    },
                                    new TextBlock
                                    {
                                        Text = File.GetLastWriteTime(file).ToString("dd/MM/yyyy HH:mm"),
                                        FontSize = 10,
                                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5"))
                                    }
                                }
                            }
                        };

                        GalleryImagesPanel.Children.Add(card);
                        imageCount++;
                    }
                    catch
                    {
                        // Una imagen dañada no debe impedir que se muestren las demás.
                    }
                }
            }

            GalleryEmpty.IsVisible = imageCount == 0;
            GalleryMessage.IsVisible = false;
            if (imageCount > 0)
                GalleryEmpty.Text = $"Se encontraron {imageCount} capturas archivadas.";
        }
        catch (Exception ex)
        {
            GalleryMessage.Text = "No se pudo cargar la galería: " + ex.Message;
            GalleryMessage.IsVisible = true;
            GalleryEmpty.IsVisible = true;
        }

        await Task.CompletedTask;
    }

    private static bool IsGalleryImage(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private async void SaveOfflineProfile_Click(object? sender, RoutedEventArgs e)
    {
        string username = OfflineUsernameInput.Text?.Trim() ?? string.Empty;
        bool valid = username.Length is >= 3 and <= 16 &&
            username.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

        if (!valid)
        {
            OfflineProfileStatus.Text = "Usa entre 3 y 16 caracteres: letras, números o guion bajo.";
            OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
            OfflineProfileStatus.IsVisible = true;
            return;
        }

        try
        {
            await _offlineAccountService.SaveAsync(username, null, "wide");
            _preferences.AccountMode = "offline";
            await _preferencesService.SaveAsync(_preferences);
            OfflineProfileStatus.Text = $"Perfil sin conexión guardado: {username}";
            OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3B1"));
            OfflineProfileStatus.IsVisible = true;
            GameStatus.Text = "Perfil sin conexión listo para probar Minecraft.";
        }
        catch (Exception ex)
        {
            OfflineProfileStatus.Text = "No se pudo guardar el perfil: " + ex.Message;
            OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
            OfflineProfileStatus.IsVisible = true;
        }
    }

    private async void PlayInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        button.IsEnabled = false;
        GameProgress.Value = 0;
        GameProgress.IsVisible = true;
        GameStatus.Text = $"Preparando {instance.Name}…";

        var progress = new Progress<double>(value =>
            GameProgress.Value = Math.Clamp(value, 0, 100));
        var status = new Progress<string>(value => GameStatus.Text = value);

        try
        {
            if (!instance.RuntimePrepared || string.IsNullOrWhiteSpace(instance.LaunchVersionName))
            {
                await _runtimeService.PrepareAsync(instance, _preferences, progress, status);
                await RefreshInstancesAsync();
            }

            OfflineAccountProfile? profile = await _offlineAccountService.LoadAsync();
            if (profile is null)
            {
                throw new InvalidOperationException(
                    "Minecraft ya puede prepararse, pero falta guardar un perfil sin conexión en Ajustes. " +
                    "La autenticación Microsoft todavía no está conectada a esta interfaz.");
            }

            GameStatus.Text = "Iniciando Minecraft…";
            Process process = await _runtimeService.LaunchOfflineAsync(instance, _preferences, profile);
            GameProgress.Value = 100;
            GameStatus.Text = $"Minecraft se inició correctamente (PID {process.Id}).";

            if (_preferences.CloseLauncherOnGameStart)
                Close();
        }
        catch (OperationCanceledException)
        {
            GameStatus.Text = "La preparación se canceló.";
        }
        catch (Exception ex)
        {
            GameStatus.Text = "No se pudo iniciar el juego: " + ex.Message;
        }
        finally
        {
            button.IsEnabled = true;
            GameProgress.IsVisible = false;
            await RefreshInstancesAsync();
        }
    }

    private async void DeleteInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        var answer = await ConfirmDeleteInstanceAsync(instance);
        if (!answer)
            return;

        try
        {
            await _instanceService.DeleteInstanceAsync(instance.Id);
            await RefreshInstancesAsync();
            HeaderStatus.Text = "Instancia eliminada";
            InstancesMessage.Text = $"Se eliminó la instancia «{instance.Name}» y sus archivos locales.";
            InstancesMessage.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3B1"));
            InstancesMessage.IsVisible = true;
        }
        catch (Exception ex)
        {
            InstancesMessage.Text = "No se pudo eliminar la instancia: " + ex.Message;
            InstancesMessage.IsVisible = true;
        }
    }

    private async Task<bool> ConfirmDeleteInstanceAsync(InstalledInstance instance)
    {
        var dialog = new Window
        {
            Title = "Eliminar instancia",
            Width = 430,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0D1218")),
            Foreground = Avalonia.Media.Brushes.Gainsboro
        };

        var cancel = new Button { Content = "Cancelar", Padding = new Avalonia.Thickness(14, 8) };
        var confirm = new Button
        {
            Content = "Eliminar definitivamente",
            Padding = new Avalonia.Thickness(14, 8),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8E343B")),
            Foreground = Avalonia.Media.Brushes.White
        };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(22),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = $"¿Eliminar «{instance.Name}»?", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                new TextBlock
                {
                    Text = "Se borrarán los archivos de esta instancia y su configuración local. Esta acción no se puede deshacer. Los archivos compartidos de Minecraft no se eliminarán.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { cancel, confirm }
                }
            }
        };

        return await dialog.ShowDialog<bool>(this);
    }
    private void SettingsNav_Click(object? sender, RoutedEventArgs e) => OpenPage("Ajustes");

    private async void RefreshInstances_Click(object? sender, RoutedEventArgs e)
        => await RefreshInstancesAsync();

    private async void CheckJava_Click(object? sender, RoutedEventArgs e)
    {
        JavaStatus.Text = "Comprobando Java…";
        JavaStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5"));

        try
        {
            string configuredPath = JavaPathInput.Text?.Trim() ?? string.Empty;
            string javaPath = configuredPath;

            if (string.IsNullOrWhiteSpace(javaPath))
            {
                string executableName = OperatingSystem.IsWindows() ? "java.exe" : "java";
                string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                javaPath = pathValue.Split(Path.PathSeparator)
                    .Where(directory => !string.IsNullOrWhiteSpace(directory))
                    .Select(directory => Path.Combine(directory.Trim(), executableName))
                    .FirstOrDefault(File.Exists) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
            {
                JavaStatus.Text = string.IsNullOrWhiteSpace(configuredPath)
                    ? "No se encontró Java en PATH."
                    : "El ejecutable indicado no existe.";
                JavaStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
                return;
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
                throw new InvalidOperationException("No se pudo iniciar el ejecutable de Java.");

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string output = (await standardOutput) + Environment.NewLine + (await standardError);
            string versionLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.Contains("version", StringComparison.OrdinalIgnoreCase))
                ?? output.Trim();

            if (process.ExitCode == 0)
            {
                JavaStatus.Text = "Java disponible: " + versionLine;
                JavaStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3B1"));
            }
            else
            {
                JavaStatus.Text = "Java respondió con un error: " + versionLine;
                JavaStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
            }
        }
        catch (Exception ex)
        {
            JavaStatus.Text = "No se pudo comprobar Java: " + ex.Message;
            JavaStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
        }
    }

    private async void SaveSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(RamInput.Text, out int ram))
            {
                ShowSettingsMessage("Escribe la memoria en MB usando un número entero.", false);
                return;
            }

            string newRoot = string.IsNullOrWhiteSpace(StoragePathInput.Text)
                ? InstanceService.DefaultStorageRoot
                : Path.GetFullPath(StoragePathInput.Text.Trim());

            string currentRoot = _instanceService.GetStorageRoot();
            if (!PathsEqual(currentRoot, newRoot))
                await _instanceService.ChangeStorageRootAsync(newRoot);

            _preferences.MaximumRamMb = Math.Clamp(ram, 1024, 32768);
            _preferences.UseAutomaticJava = AutomaticJavaInput.IsChecked == true;
            _preferences.CustomJavaPath = JavaPathInput.Text?.Trim() ?? string.Empty;
            _preferences.CloseLauncherOnGameStart = CloseLauncherInput.IsChecked == true;
            _preferences.ShowGameConsole = ShowConsoleInput.IsChecked == true;
            _preferences.ModpackCatalogFileId = CatalogFileIdInput.Text?.Trim() ?? string.Empty;
            _preferences.StorageRootPath = PathsEqual(newRoot, InstanceService.DefaultStorageRoot)
                ? string.Empty
                : newRoot;

            await _preferencesService.SaveAsync(_preferences);
            FillSettings();
            await RefreshInstancesAsync();
            HomeRam.Text = $"{_preferences.MaximumRamMb} MB";
            ShowSettingsMessage("Ajustes guardados correctamente.", true);
            HeaderStatus.Text = "Ajustes guardados";
        }
        catch (Exception ex)
        {
            ShowSettingsMessage("No se pudieron guardar los ajustes: " + ex.Message, false);
        }
    }

    private async void ReloadSettings_Click(object? sender, RoutedEventArgs e)
    {
        _preferences = await _preferencesService.LoadAsync();
        _instanceService.ConfigureStorageRoot(_preferences.StorageRootPath);
        FillSettings();
        ShowSettingsMessage("Formulario restaurado con los últimos ajustes guardados.", true);
    }

    private async void InstallModpack_Click(object? sender, RoutedEventArgs e)
    {
        string code = InstallCodeInput.Text?.Trim() ?? string.Empty;
        string catalogId = _preferences.ModpackCatalogFileId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            ShowModpackMessage("Escribe el código del modpack.", false);
            return;
        }
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            ShowModpackMessage("Primero guarda el ID del catálogo en Ajustes.", false);
            return;
        }

        InstallModpackButton.IsEnabled = false;
        InstallProgress.Value = 0;
        InstallProgress.IsVisible = true;
        try
        {
            var drive = new GoogleDriveService();
            var reader = new ModpackCatalogReader();
            ShowModpackMessage("Buscando el modpack en el catálogo…", true);
            ModpackManifest? manifest = await reader.FindByCodeAsync(
                code,
                catalogId,
                (fileId, cancellationToken) => drive.DownloadTextFileAsync(fileId, cancellationToken));

            if (manifest is null)
            {
                ShowModpackMessage("No se encontró ningún modpack con ese código.", false);
                return;
            }

            var existing = (await _instanceService.LoadAllAsync())
                .FirstOrDefault(instance => string.Equals(instance.Id, manifest.Id, StringComparison.OrdinalIgnoreCase));

            var installer = new ModpackInstallerService(drive, _instanceService);
            var progress = new Progress<double>(value =>
            {
                InstallProgress.Value = Math.Clamp(value, 0, 100);
                ModpackMessage.Text = $"Instalando {manifest.Name}… {InstallProgress.Value:0}%";
                ModpackMessage.IsVisible = true;
            });

            await installer.InstallOrUpdateAsync(manifest, code, existing, progress);
            await RefreshInstancesAsync();
            ShowModpackMessage($"'{manifest.Name}' se instaló o actualizó correctamente. La preparación del juego aún debe completarse por separado.", true);
            HeaderStatus.Text = "Modpack instalado";
        }
        catch (OperationCanceledException)
        {
            ShowModpackMessage("La operación fue cancelada.", false);
        }
        catch (Exception ex)
        {
            ShowModpackMessage("No se pudo instalar el modpack: " + ex.Message, false);
        }
        finally
        {
            InstallModpackButton.IsEnabled = true;
            InstallProgress.IsVisible = false;
        }
    }

    private void ShowModpackMessage(string message, bool success)

    {
        ModpackMessage.Text = message;
        ModpackMessage.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(success ? "#A6E3B1" : "#F0C674"));
        ModpackMessage.IsVisible = true;
    }

    private void ShowSettingsMessage(string message, bool success)
    {
        SettingsMessage.Text = message;
        SettingsMessage.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(success ? "#A6E3B1" : "#F0C674"));
        SettingsMessage.IsVisible = true;
    }

    private static bool PathsEqual(string left, string right)
    {
        string a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
