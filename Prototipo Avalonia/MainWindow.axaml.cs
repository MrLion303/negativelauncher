using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;
using NegativeLauncher.AvaloniaPrototype.Converters;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Negative_Client.Models;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow : Window
{
    private readonly LauncherPreferencesService _preferencesService = new();
    private readonly LauncherStateService _launcherStateService = new();
    private readonly InstanceService _instanceService = new();
    private readonly GoogleDriveService _sidebarIconDrive = new();
    private readonly MinecraftRuntimeService _runtimeService;
    private readonly DiscordRichPresenceService _discordRichPresenceService;
    private readonly OfflineAccountService _offlineAccountService = new();
    private readonly MicrosoftAccountService _accountService = MicrosoftAccountService.Instance;
    private readonly MinecraftSkinTextureService _minecraftSkinTextureService = new();
    private CancellationTokenSource? _accountHeadsCancellation;
    private CancellationTokenSource? _quickAccountHeadsCancellation;
    private readonly ResourcePackSelectionService _resourcePackSelectionService;
    private readonly OfflineSkinService _offlineSkinService = new();
    private readonly ObservableCollection<MicrosoftAccountInfo> _accounts = new();
    private readonly ObservableCollection<InstalledInstance> _instances = new();
    private InstalledInstance? _selectedInstance;
    private string? _lastPlayedInstanceId;
    private GameConsoleWindow? _gameConsoleWindow;
    private Process? _runningGameProcess;
    private string? _runningGameInstanceId;
    private LauncherPreferences _preferences = new();
    private string? _offlineSkinPath;
    private string? _defaultHomeBackgroundPath;
    private int _backgroundLoadGeneration;
    private bool _developerVanillaBackgroundActive;
    private readonly System.Collections.Generic.List<GalleryEntry> _galleryItems = new();
    private readonly System.Collections.Generic.HashSet<string> _gallerySelection = new(LauncherPathRules.PathComparer);
    private bool _gallerySelectionMode;
    private string _galleryInstanceFilter = string.Empty;
    private string _gallerySortMode = "newest";
    private int _galleryViewerIndex = -1;
    private DownloadOperationController? _activeDownloadController;
    private DownloadOperationController? _runtimeOperationController;
    private const string DeveloperInstanceId = "__developer_vanilla__";
    private static readonly HttpClient DeveloperHttpClient = new();
    private static readonly HttpClient UpdateHttpClient = new();
    private string? _latestReleaseDownloadUrl;
    private readonly List<(string Id, string Type)> _developerVersions = new();
    private bool _developerModeEnabled;
    private bool _developerVersionsLoaded;
    private bool _developerFiltersLoading;
    private bool _runningDeveloperVanilla;
    private bool _fillingSettings;
    private bool _handlingDeveloperModeToggle;
    private System.Threading.CancellationTokenSource? _settingsSaveCancellation;
    private Task? _settingsSaveTask;
    private bool _closingForSettingsSave;
    private bool _allowCloseAfterSettingsSave;
    private bool _settingsHaveUnsavedChanges;

    private sealed class GalleryEntry
    {
        public string InstanceId { get; init; } = string.Empty;
        public string InstanceName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public DateTime CapturedAt { get; init; }
    }

    private async Task RefreshQuickAccountUiAsync()
    {
        if (QuickAccountsListPanel is null)
            return;

        QuickAccountsListPanel.Children.Clear();
        // La referencia Windows muestra la cuenta sin conexión en el menú rápido cuando ese modo está activo.
        var accounts = _accountService.GetAccounts();
        int premiumAccountCount = _accountService.GetPremiumAccounts().Count;
        MicrosoftAccountInfo? activeAccount = accounts.FirstOrDefault(account => account.IsSelected);
        var activeName = _accountService.Username;
        if (activeAccount is null)
        {
            QuickAccountButton.Content = "Cuenta ▾";
        }
        else
        {
            var currentAccountContent = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 7,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            currentAccountContent.Children.Add(CreateSkinAvatar(activeAccount, 26));
            currentAccountContent.Children.Add(new TextBlock
            {
                Text = (string.IsNullOrWhiteSpace(activeName) ? activeAccount.Username : activeName) + " ▾",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            });
            QuickAccountButton.Content = currentAccountContent;
        }

        foreach (var account in accounts)
        {
            var button = new Button
            {
                Tag = account.Identifier,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Padding = new Avalonia.Thickness(8, 6),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(account.IsSelected ? "#26333A" : "#181E25")),
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E6E9EC"))
            };

            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            row.Children.Add(CreateSkinAvatar(account, 34));
            row.Children.Add(new TextBlock
            {
                Text = (account.IsSelected ? "✓  " : "") + account.Username,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            button.Content = row;
            button.Click += QuickAccountEntry_Click;
            QuickAccountsListPanel.Children.Add(button);
        }

        _quickAccountHeadsCancellation?.Cancel();
        var quickSkinLoad = new CancellationTokenSource();
        _quickAccountHeadsCancellation = quickSkinLoad;
        _ = LoadAccountSkinTexturesAsync(accounts.ToArray(), quickSkinLoad, isQuickAccountList: true);

        QuickAddAccountButton.IsEnabled = premiumAccountCount < MicrosoftAccountService.MaxAccounts;
        QuickAddAccountButton.Content = premiumAccountCount < MicrosoftAccountService.MaxAccounts
            ? "+ Añadir otra cuenta"
            : "Máximo de 3 cuentas";
        await Task.CompletedTask;
    }

    private async void QuickAccountButton_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshQuickAccountUiAsync();
        AccountQuickPopup.IsOpen = !AccountQuickPopup.IsOpen;
    }

    private async void QuickAccountEntry_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string identifier })
            return;

        AccountQuickPopup.IsOpen = false;
        try
        {
            bool selected = await _accountService.SelectAccountAsync(identifier);
            if (!selected)
            {
                AccountStatus.Text = "La sesión guardada ya no es válida. Reautentica esta cuenta desde Ajustes.";
                AccountStatus.IsVisible = true;
            }
            await RefreshAccountsAsync();
            await RefreshQuickAccountUiAsync();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("QuickAccountEntry_Click: falló una operación.", ex);
            AccountStatus.Text = "No se pudo cambiar de cuenta: " + ex.Message;
            AccountStatus.IsVisible = true;
        }
    }

    private async void QuickAddAccountButton_Click(object? sender, RoutedEventArgs e)
    {
        AccountQuickPopup.IsOpen = false;
        if (_accountService.GetPremiumAccounts().Count >= MicrosoftAccountService.MaxAccounts)
            return;

        try
        {
            await _accountService.AddAccountInteractivelyAsync();
            await RefreshAccountsAsync();
            await RefreshQuickAccountUiAsync();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("No se pudo añadir una cuenta Microsoft.", ex);
            AccountStatus.Text = "No se pudo añadir la cuenta: " + ex.Message;
            AccountStatus.IsVisible = true;
        }
    }

    private void QuickOpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        AccountQuickPopup.IsOpen = false;
        OpenPage("Ajustes");
        ShowAccountsSettingsTab_Click(null, new RoutedEventArgs());
    }

    private void ShowLauncherAfterLogin()
    {
        LoginGate.IsVisible = false;
        CanResize = true;
        WindowState = WindowState.Normal;
        OpenPage("Inicio");
        _ = RefreshQuickAccountUiAsync();
        RefreshDiscordPresence();
    }

    private void RefreshDiscordPresence()
    {
        try
        {
            bool gameRunning = false;
            try
            {
                gameRunning = _runningGameProcess is not null && !_runningGameProcess.HasExited;
            }
            catch
            {
                gameRunning = false;
            }

            string? instanceId = gameRunning ? _runningGameInstanceId : _selectedInstance?.Id;
            string? instanceName = null;
            string? assetKey = null;

            if (string.Equals(instanceId, DeveloperInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                instanceName = "Minecraft Vanilla (Desarrollador)";
            }
            else if (!string.IsNullOrWhiteSpace(instanceId))
            {
                InstalledInstance? instance = _instances.FirstOrDefault(x =>
                    string.Equals(x.Id, instanceId, StringComparison.OrdinalIgnoreCase));
                if (instance is not null)
                {
                    instanceName = instance.Name;
                    assetKey = instance.Id;
                }
            }

            _discordRichPresenceService.UpdatePresence(instanceName, assetKey, gameRunning);
        }
        catch
        {
            // Un fallo de Discord no debe afectar al launcher ni al juego.
        }
    }

    private void SetLoginBusy(bool busy, string message)
    {
        LoginMicrosoftButton.IsEnabled = !busy;
        LoginOfflineButton.IsEnabled = !busy;
        ChooseOfflineLoginButton.IsEnabled = !busy;
        OfflineUsernameBox.IsEnabled = !busy;
        LoginStatusText.Text = message;
    }

    private async void SaveMicrosoftOAuthClientIdLogin_Click(object? sender, RoutedEventArgs e) =>
        await SaveMicrosoftOAuthClientIdAsync(MicrosoftOAuthClientIdLoginInput.Text, fromLoginScreen: true);

    private async void SaveMicrosoftOAuthClientIdSettings_Click(object? sender, RoutedEventArgs e) =>
        await SaveMicrosoftOAuthClientIdAsync(MicrosoftOAuthClientIdInput.Text, fromLoginScreen: false);

    private async Task SaveMicrosoftOAuthClientIdAsync(string? input, bool fromLoginScreen)
    {
        if (!MicrosoftOAuthClientConfiguration.TryNormalizeClientId(input, out string normalized))
        {
            string message = "Introduce un Client ID válido (GUID) de una app pública de Microsoft Entra.";
            if (fromLoginScreen)
                LoginStatusText.Text = message;
            else
                ShowSettingsMessage(message, false);
            return;
        }

        try
        {
            _preferences.MicrosoftOAuthClientId = normalized;
            await _preferencesService.SaveAsync(_preferences);
            MicrosoftOAuthClientIdInput.Text = normalized;
            MicrosoftOAuthClientIdLoginInput.Text = normalized;
            if (fromLoginScreen)
                LoginStatusText.Text = "Client ID guardado. Ya puedes iniciar sesión con Microsoft.";
            else
                ShowSettingsMessage("Client ID guardado. Las siguientes autenticaciones usarán MSAL.", true);
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("SaveMicrosoftOAuthClientIdAsync: falló una operación.", ex);
            if (fromLoginScreen)
                LoginStatusText.Text = "No se pudo guardar el Client ID: " + ex.Message;
            else
                ShowSettingsMessage("No se pudo guardar el Client ID: " + ex.Message, false);
        }
    }

    private async void OpenMicrosoftAuthHelp_Click(object? sender, RoutedEventArgs e)
    {
        const string helpAddress = "https://cmllib.github.io/CmlLib.Core-wiki/en/auth.microsoft/xboxauthnet.game.msal/clientid/";
        try
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                throw new InvalidOperationException("No se pudo acceder al servicio de enlaces del sistema.");

            await topLevel.Launcher.LaunchUriAsync(new Uri(helpAddress));
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("OpenMicrosoftAuthHelp_Click: falló una operación.", ex);
            const string message = "No se pudo abrir la guía de autenticación: ";
            if (LoginGate.IsVisible)
                LoginStatusText.Text = message + ex.Message;
            else
                ShowSettingsMessage(message + ex.Message, false);
        }
    }

    private async void LoginMicrosoftButton_Click(object? sender, RoutedEventArgs e)
    {
        SetLoginBusy(true, "Conectando con Microsoft…");
        try
        {
            await _accountService.AddAccountInteractivelyAsync();
            await RefreshAccountsAsync();
            await RefreshQuickAccountUiAsync();
            ShowLauncherAfterLogin();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("LoginMicrosoftButton_Click: falló una operación.", ex);
            LoginStatusText.Text = "No se pudo iniciar sesión: " + ex.Message;
        }
        finally
        {
            LoginMicrosoftButton.IsEnabled = true;
            ChooseOfflineLoginButton.IsEnabled = true;
            LoginOfflineButton.IsEnabled = true;
            OfflineUsernameBox.IsEnabled = true;
        }
    }

    private void ChooseOfflineLoginButton_Click(object? sender, RoutedEventArgs e)
    {
        LoginOptionsPanel.IsVisible = false;
        OfflineLoginPanel.IsVisible = true;
        LoginStatusText.Text = string.Empty;
        OfflineUsernameBox.Focus();
    }

    private void BackToLoginOptionsButton_Click(object? sender, RoutedEventArgs e)
    {
        OfflineLoginPanel.IsVisible = false;
        LoginOptionsPanel.IsVisible = true;
        LoginStatusText.Text = string.Empty;
    }

    private async void LoginOfflineButton_Click(object? sender, RoutedEventArgs e)
    {
        string username = OfflineUsernameBox.Text?.Trim() ?? string.Empty;
        if (!OfflineAccountService.IsValidUsername(username))
        {
            LoginStatusText.Text = "Escribe un nombre de 3 a 16 caracteres ASCII: letras, números o _.";
            return;
        }

        SetLoginBusy(true, "Preparando perfil sin conexión…");
        try
        {
            await _offlineAccountService.SaveAsync(username, null, "wide");
            await _accountService.SetAccountModeAsync(MicrosoftAccountService.OfflineAccountMode);
            await _accountService.InitializeAsync();
            await RefreshAccountsAsync();
            await RefreshQuickAccountUiAsync();
            ShowLauncherAfterLogin();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("LoginOfflineButton_Click: falló una operación.", ex);
            LoginStatusText.Text = "No se pudo crear el perfil: " + ex.Message;
        }
        finally
        {
            LoginMicrosoftButton.IsEnabled = true;
            ChooseOfflineLoginButton.IsEnabled = true;
            LoginOfflineButton.IsEnabled = true;
            OfflineUsernameBox.IsEnabled = true;
        }
    }

    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.Source is Button ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Reproduce el comportamiento habitual de la barra de título del launcher.
        // Durante el acceso, el doble clic nunca maximiza la ventana.
        if (e.ClickCount == 2)
        {
            if (!LoginGate.IsVisible)
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            return;
        }

        BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void ToggleWindowSize_Click(object? sender, RoutedEventArgs e)
    {
        if (LoginGate.IsVisible)
            return;
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
        => Close();

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowCloseAfterSettingsSave || !_settingsHaveUnsavedChanges)
            return;

        e.Cancel = true;
        if (_closingForSettingsSave)
            return;

        _closingForSettingsSave = true;
        Task? pendingSave = _settingsSaveTask;
        _settingsSaveCancellation?.Cancel();

        try
        {
            if (pendingSave is not null)
                await pendingSave;

            _preferences.MaximumRamMb = Math.Clamp((int)Math.Round(RamSlider.Value), 1024, 32768);
            _preferences.UseAutomaticJava = AutomaticJavaInput.IsChecked == true;
            _preferences.EnableCustomJavaArguments = EnableCustomJavaArgumentsInput.IsChecked == true;
            _preferences.CustomJavaArguments = CustomJavaArgumentsInput.Text?.Trim() ?? string.Empty;
            _preferences.EnableHolidayLauncherThemes = HolidayThemesInput.IsChecked == true;
            _preferences.CustomJavaPath = JavaPathInput.Text?.Trim() ?? string.Empty;
            _preferences.CloseLauncherOnGameStart = CloseLauncherInput.IsChecked == true;
            _preferences.ShowGameConsole = ShowConsoleInput.IsChecked == true;
            _preferences.ModpackCatalogFileId = CatalogFileIdInput.Text?.Trim() ?? string.Empty;

            await _preferencesService.SaveAsync(_preferences);
            _allowCloseAfterSettingsSave = true;
            Close();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("MainWindow_Closing: falló una operación.", ex);
            _closingForSettingsSave = false;
            ShowSettingsMessage("No se pudieron guardar los cambios antes de cerrar: " + ex.Message, false);
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        // La ventana de acceso no puede cambiar de tamaño antes de cargar la sesión.
        CanResize = false;
        MicrosoftOAuthClientPanel.IsVisible = !OperatingSystem.IsWindows();
        MicrosoftOAuthClientSettingsPanel.IsVisible = !OperatingSystem.IsWindows();
        _discordRichPresenceService = new DiscordRichPresenceService("1071536677820055733");
        Version currentLauncherVersion = typeof(MainWindow).Assembly.GetName().Version ?? new Version(0, 1, 1, 0);
        string displayedVersion = currentLauncherVersion.ToString(3);
        Title = $"Negative Client {displayedVersion}";
        LauncherVersionText.Text = $"Negative Client {displayedVersion}";
        LoadBrandingAssets();
        _runtimeService = new MinecraftRuntimeService(_instanceService);
        _resourcePackSelectionService = new ResourcePackSelectionService(_instanceService);
        InstancesList.ItemsSource = _instances;
        SidebarInstancesList.ItemsSource = _instances;
        SidebarInstancesList.LayoutUpdated += SidebarInstancesList_LayoutUpdated;
        AccountsList.ItemsSource = _accounts;
        OpenPage("Inicio");
        DiagnosticsLogPathLabel.Text = LauncherDiagnostics.LogPath;
        LauncherDiagnostics.WriteInfo("Se creó la ventana principal.");

        Opened += async (_, _) =>
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                LauncherDiagnostics.WriteException("Falló la inicialización de la ventana principal.", ex);
                HeaderStatus.Text = "Error al cargar datos";
                GameStatus.Text = "No se pudo completar el inicio. Abre Ajustes → Diagnóstico y comparte el registro tras revisar sus datos.";
            }

            try
            {
                StartGlobalCountdowns();
            }
            catch (Exception ex)
            {
                LauncherDiagnostics.WriteException("No se pudieron iniciar los contadores o temas.", ex);
            }

            _ = CheckForLauncherUpdateAsync();
        };
        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            StopGlobalCountdowns();
            try { _discordRichPresenceService.Dispose(); } catch { }
            _accountHeadsCancellation?.Cancel();
            _accountHeadsCancellation = null;
            _quickAccountHeadsCancellation?.Cancel();
            _quickAccountHeadsCancellation = null;
            _settingsSaveCancellation?.Cancel();
            _settingsSaveCancellation?.Dispose();
            _settingsSaveCancellation = null;
        };
    }


    private async Task CheckForLauncherUpdateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/repos/MrLion303/negativelauncher/releases/latest");
            request.Headers.UserAgent.ParseAdd("NegativeClient");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await UpdateHttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            JsonElement release = document.RootElement;

            string releaseName = release.TryGetProperty("name", out var name)
                ? name.GetString() ?? string.Empty : string.Empty;
            string releaseTag = release.TryGetProperty("tag_name", out var tag)
                ? tag.GetString() ?? string.Empty : string.Empty;
            Version? latestVersion = ExtractReleaseVersion(releaseName) ?? ExtractReleaseVersion(releaseTag);
            Version currentVersion = typeof(MainWindow).Assembly.GetName().Version ?? new Version(0, 1, 1, 0);

            if (latestVersion is null || latestVersion <= currentVersion)
                return;

            string? downloadUrl = null;
            string[] compatibleAssetNames = GetCompatibleReleaseAssetNames();
            if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                // Respetar el orden de preferencia por sistema, no el orden de subida del release.
                // En macOS se prefiere el DMG con enlace a Applications; el ZIP queda como alternativa.
                foreach (string preferredAssetName in compatibleAssetNames)
                {
                    foreach (JsonElement asset in assets.EnumerateArray())
                    {
                        if (!asset.TryGetProperty("name", out var assetName) ||
                            !string.Equals(
                                assetName.GetString(),
                                preferredAssetName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (asset.TryGetProperty("browser_download_url", out var url))
                            downloadUrl = url.GetString();
                        if (!string.IsNullOrWhiteSpace(downloadUrl))
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(downloadUrl))
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl) &&
                release.TryGetProperty("html_url", out var releaseUrl))
                downloadUrl = releaseUrl.GetString();

            if (string.IsNullOrWhiteSpace(downloadUrl))
                return;

            _latestReleaseDownloadUrl = downloadUrl;
            UpdateAvailableText.Text = $"Nueva versión {latestVersion} disponible";
            UpdateAvailableBorder.IsVisible = true;
        }
        catch
        {
            // La comprobación es opcional; si GitHub no responde, el launcher sigue funcionando.
        }
    }

    private static string[] GetCompatibleReleaseAssetNames()
    {
        string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            _ => string.Empty
        };

        if (OperatingSystem.IsWindows())
        {
            // Conserva compatibilidad con el instalador/ejecutable de Windows ya publicado.
            return architecture == "x64"
                ? new[] { "Negative.Client.exe", "NegativeLauncher-win-x64.zip" }
                : Array.Empty<string>();
        }

        if (OperatingSystem.IsLinux() && architecture.Length > 0)
            return new[] { $"NegativeLauncher-linux-{architecture}.tar.gz" };

        if (OperatingSystem.IsMacOS() && architecture.Length > 0)
            return new[]
            {
                $"NegativeLauncher-osx-{architecture}.dmg",
                $"NegativeLauncher-osx-{architecture}.zip"
            };

        return Array.Empty<string>();
    }

    private static Version? ExtractReleaseVersion(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\d+(?:\.\d+){1,3}");
        return match.Success && Version.TryParse(match.Value, out Version? version) ? version : null;
    }

    private async void UpdateAvailable_Click(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestReleaseDownloadUrl))
            return;

        try
        {
            if (!Uri.TryCreate(_latestReleaseDownloadUrl, UriKind.Absolute, out Uri? updateUri) ||
                (updateUri.Scheme != Uri.UriSchemeHttps && updateUri.Scheme != Uri.UriSchemeHttp))
            {
                throw new InvalidOperationException("La dirección de actualización no es válida.");
            }

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                throw new InvalidOperationException("No se pudo acceder al servicio de enlaces del sistema.");

            await topLevel.Launcher.LaunchUriAsync(updateUri);
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("UpdateAvailable_Click: falló una operación.", ex);
            HeaderStatus.Text = "No se pudo abrir la página de actualización: " + ex.Message;
        }
    }

    private void LoadBrandingAssets()
    {
        string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "negativeclient_logo.png");
        string homeIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "home_icon.png");
        string loginBackgroundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "login_bg.png");
        if (File.Exists(homeIconPath))
        {
            try
            {
                HomeNav.Content = new Image
                {
                    Source = new Bitmap(homeIconPath),
                    Width = 44,
                    Height = 44,
                    Stretch = Avalonia.Media.Stretch.UniformToFill,
                    IsHitTestVisible = false,
                    Clip = new Avalonia.Media.EllipseGeometry(new Avalonia.Rect(0, 0, 44, 44))
                };
            }
            catch
            {
                // Si el recurso no se puede leer, se conserva el icono de respaldo.
            }
        }

        if (File.Exists(loginBackgroundPath))
        {
            try { LoginBackgroundImage.Source = new Bitmap(loginBackgroundPath); }
            catch { LoginBackgroundImage.Source = null; }
        }
        if (File.Exists(logoPath))
        {
            try
            {
                BrandLogo.Source = new Bitmap(logoPath);
                BrandLogo.IsVisible = true;
                BrandLogoFallback.IsVisible = false;
                LoginBrandLogo.Source = new Bitmap(logoPath);
            }
            catch
            {
                // Si falta el archivo de marca, se conserva el distintivo de respaldo.
            }
        }

        string backgroundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "negativeclient_bg.png");
        if (File.Exists(backgroundPath))
        {
            _defaultHomeBackgroundPath = backgroundPath;
            try
            {
                HomeBackgroundImage.Source = new Bitmap(backgroundPath);
                HomeBackgroundImage.IsVisible = true;
            }
            catch
            {
                // El inicio mantiene el fondo oscuro si no se puede cargar la imagen.
            }
        }
    }

    private void RefreshDeveloperVanillaBackground()
    {
        if (!DeveloperPage.IsVisible || !_developerModeEnabled)
        {
            ClearDeveloperVanillaBackground();
            return;
        }

        if (_developerVanillaBackgroundActive && DeveloperBackgroundImage.Source is not null)
        {
            DeveloperBackgroundImage.IsVisible = true;
            DeveloperBackgroundOverlay.IsVisible = true;
            return;
        }

        string imagePath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "minecraft_vanilla.png");

        if (!File.Exists(imagePath))
        {
            ClearDeveloperVanillaBackground();
            return;
        }

        try
        {
            var bitmap = new Bitmap(imagePath);
            if (DeveloperBackgroundImage.Source is IDisposable previous && !ReferenceEquals(previous, bitmap))
                previous.Dispose();

            DeveloperBackgroundImage.Source = bitmap;
            DeveloperBackgroundImage.Stretch = Avalonia.Media.Stretch.UniformToFill;
            DeveloperBackgroundImage.IsVisible = true;
            DeveloperBackgroundOverlay.IsVisible = true;
            _developerVanillaBackgroundActive = true;
        }
        catch
        {
            ClearDeveloperVanillaBackground();
        }
    }

    private void ClearDeveloperVanillaBackground()
    {
        var previous = DeveloperBackgroundImage.Source;
        DeveloperBackgroundImage.Source = null;
        DeveloperBackgroundImage.IsVisible = false;
        DeveloperBackgroundOverlay.IsVisible = false;
        _developerVanillaBackgroundActive = false;

        if (previous is IDisposable disposable)
        {
            try { disposable.Dispose(); } catch { }
        }
    }

    private async Task LoadSelectedInstanceBackgroundAsync(InstalledInstance? instance)
    {
        int generation = ++_backgroundLoadGeneration;
        if (instance is null || string.IsNullOrWhiteSpace(instance.BackgroundFileId))
        {
            ApplyDefaultHomeBackground();
            return;
        }

        try
        {
            string safeId = new string(instance.Id.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_').ToArray());
            if (string.IsNullOrWhiteSpace(safeId))
            {
                ApplyDefaultHomeBackground();
                return;
            }

            string cacheDirectory = Path.Combine(LauncherPaths.DefaultLauncherRoot, "image-cache");
            Directory.CreateDirectory(cacheDirectory);
            string imagePath = Path.Combine(cacheDirectory, safeId + "-background.png");
            if (!File.Exists(imagePath) || new FileInfo(imagePath).Length == 0)
            {
                var drive = new GoogleDriveService();
                await drive.DownloadFileAsync(instance.BackgroundFileId, imagePath);
            }

            if (generation != _backgroundLoadGeneration ||
                !string.Equals(_selectedInstance?.Id, instance.Id, StringComparison.OrdinalIgnoreCase))
                return;

            var image = new Bitmap(imagePath);
            var previous = HomeBackgroundImage.Source;
            HomeBackgroundImage.Source = image;
            HomeBackgroundImage.IsVisible = true;
            if (previous is IDisposable disposable && !ReferenceEquals(previous, image))
                disposable.Dispose();
        }
        catch
        {
            if (generation == _backgroundLoadGeneration)
                ApplyDefaultHomeBackground();
        }
    }

    private void ApplyDefaultHomeBackground()
    {
        try
        {
            HomeBackgroundImage.Source = !string.IsNullOrWhiteSpace(_defaultHomeBackgroundPath) &&
                File.Exists(_defaultHomeBackgroundPath)
                ? new Bitmap(_defaultHomeBackgroundPath)
                : null;
            HomeBackgroundImage.IsVisible = HomeBackgroundImage.Source is not null;
        }
        catch
        {
            HomeBackgroundImage.Source = null;
            HomeBackgroundImage.IsVisible = false;
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            _preferences = await _preferencesService.LoadAsync();
            _lastPlayedInstanceId = await _launcherStateService.GetLastPlayedInstanceIdAsync();
            _instanceService.ConfigureStorageRoot(_preferences.StorageRootPath);
            FillSettings();
            try
            {
                await _accountService.InitializeAsync();
                await RefreshAccountsAsync();
                await RefreshQuickAccountUiAsync();
            }
            catch (Exception accountException)
            {
                LauncherDiagnostics.WriteException("InitializeAsync: no se pudieron cargar las cuentas.", accountException);
                AccountStatus.Text = "No se pudieron cargar las cuentas: " + accountException.Message;
            }
            var offlineProfile = await _offlineAccountService.LoadAsync();
            OfflineUsernameInput.Text = offlineProfile?.Username ?? string.Empty;
            OfflineSkinModelComboBox.SelectedIndex = string.Equals(offlineProfile?.SkinModel, "slim", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _offlineSkinPath = offlineProfile?.SkinFilePath;
            OfflineSkinPathLabel.Text = string.IsNullOrWhiteSpace(_offlineSkinPath) ? "No hay una skin personalizada seleccionada." : _offlineSkinPath;
            if (offlineProfile is not null)
            {
                OfflineProfileStatus.Text = "Perfil guardado: " + offlineProfile.Username;
                OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3B1"));
                OfflineProfileStatus.IsVisible = true;
            }
            await RefreshInstancesAsync();
            await RefreshGalleryAsync();
            await RefreshStorageUsageAsync();
            LoginGate.IsVisible = !_accountService.IsSignedIn;
            CanResize = _accountService.IsSignedIn;
            if (LoginGate.IsVisible)
            {
                WindowState = WindowState.Normal;
                LoginOptionsPanel.IsVisible = true;
                OfflineLoginPanel.IsVisible = false;
                LoginStatusText.Text = string.Empty;
            }
            LauncherDiagnostics.WriteInfo("Inicialización de datos completada; la ventana principal está lista.");
            HeaderStatus.Text = "Datos cargados";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("InitializeAsync: falló una operación.", ex);
            HeaderStatus.Text = "No se pudieron cargar los datos";
            ShowSettingsMessage("No se pudieron cargar los datos del launcher: " + ex.Message, false);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = Math.Max(0, bytes);
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }

    private static long GetDirectorySize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return 0;

        try
        {
            // No medir contenido externo si la propia raíz configurada es un enlace.
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }

        long total = 0;
        var pending = new Stack<string>();
        pending.Push(path);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory).ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in files)
            {
                try
                {
                    total = checked(total + new FileInfo(file).Length);
                }
                catch (IOException)
                {
                    // Un archivo puede desaparecer o quedar inaccesible mientras se calcula.
                }
                catch (UnauthorizedAccessException)
                {
                    // Un archivo inaccesible no debe impedir medir el resto del almacenamiento.
                }
                catch (OverflowException)
                {
                    return long.MaxValue;
                }
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string child in directories)
            {
                try
                {
                    // Evita seguir enlaces a otras ubicaciones o ciclos de enlaces simbólicos.
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        pending.Push(child);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        return total;
    }

    private async Task RefreshStorageUsageAsync()
    {
        if (StorageUsageLabel is null) return;
        StorageUsageLabel.Text = "Calculando uso de almacenamiento…";
        try
        {
            string instances = InstanceService.InstancesRoot;
            string minecraft = InstanceService.SharedMinecraftRoot;
            string cache = InstanceService.PackageCacheRoot;
            string temp = InstanceService.TempRoot;
            long[] sizes = await Task.Run(() => new[]
            {
                GetDirectorySize(instances), GetDirectorySize(minecraft),
                GetDirectorySize(cache), GetDirectorySize(temp)
            });
            StorageUsageLabel.Text =
                $"Instancias: {FormatBytes(sizes[0])}  ·  Minecraft compartido: {FormatBytes(sizes[1])}\n" +
                $"Caché de paquetes: {FormatBytes(sizes[2])}  ·  Temporales: {FormatBytes(sizes[3])}\n" +
                $"Total medido: {FormatBytes(sizes.Sum())}";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("RefreshStorageUsageAsync: falló una operación.", ex);
            StorageUsageLabel.Text = "No se pudo calcular el almacenamiento: " + ex.Message;
        }
    }

    private async void RefreshStorageUsage_Click(object? sender, RoutedEventArgs e)
        => await RefreshStorageUsageAsync();

    private bool IsStorageMaintenanceBlocked()
    {
        if (_activeDownloadController is not null || _runtimeOperationController is not null)
            return true;

        if (_runningGameProcess is null)
            return false;

        try { return !_runningGameProcess.HasExited; }
        catch { return true; }
    }

    private async void ClearPackageCache_Click(object? sender, RoutedEventArgs e)
    {
        if (IsStorageMaintenanceBlocked())
        {
            ShowSettingsMessage("Detén la operación activa y cierra Minecraft antes de limpiar archivos del launcher.", false);
            return;
        }

        bool confirmed = await ShowConfirmationAsync(
            "Limpiar caché de paquetes",
            "Se eliminarán los ZIP de modpacks almacenados en la caché. No se borrarán tus instancias ni los archivos de Minecraft. Los paquetes pueden volver a descargarse si se necesitan.");
        if (!confirmed) return;

        try
        {
            string cache = InstanceService.PackageCacheRoot;
            if (Directory.Exists(cache))
            {
                // Preparar ambas listas antes de borrar nada. Si hay enlaces o
                // una ruta no inspeccionable, la limpieza se detiene sin tocar el árbol.
                var files = DirectoryMigrationHelper.EnumerateFilesWithoutReparsePoints(cache);
                var directories = DirectoryMigrationHelper
                    .EnumerateDirectoriesWithoutReparsePoints(cache)
                    .OrderByDescending(path => path.Length)
                    .ToArray();

                foreach (string file in files)
                {
                    try { File.Delete(file); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }

                foreach (string directory in directories)
                {
                    try { if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            ShowSettingsMessage("Se limpió la caché de paquetes que se pudo eliminar.", true);
            await RefreshStorageUsageAsync();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("ClearPackageCache_Click: falló una operación.", ex);
            ShowSettingsMessage("No se pudo limpiar la caché: " + ex.Message, false);
        }
    }

    private async void ClearTemporaryFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (IsStorageMaintenanceBlocked())
        {
            ShowSettingsMessage("Detén la operación activa y cierra Minecraft antes de limpiar archivos del launcher.", false);
            return;
        }

        bool confirmed = await ShowConfirmationAsync(
            "Limpiar archivos temporales",
            "Se eliminarán los archivos temporales que dejó el launcher. No se tocarán las instancias, las capturas ni los archivos compartidos de Minecraft.");
        if (!confirmed)
            return;

        try
        {
            string tempRoot = InstanceService.TempRoot;
            if (Directory.Exists(tempRoot))
            {
                // No seguir enlaces ni junctions durante la eliminación, incluso si
                // alguna operación dejó un árbol temporal parcialmente creado.
                var files = DirectoryMigrationHelper.EnumerateFilesWithoutReparsePoints(tempRoot);
                var directories = DirectoryMigrationHelper
                    .EnumerateDirectoriesWithoutReparsePoints(tempRoot)
                    .OrderByDescending(path => path.Length)
                    .ToArray();

                foreach (string file in files)
                {
                    try { File.Delete(file); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }

                foreach (string directory in directories)
                {
                    try { if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }

            ShowSettingsMessage("Se limpiaron los archivos temporales que se pudieron eliminar.", true);
            await RefreshStorageUsageAsync();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("ClearTemporaryFiles_Click: falló una operación.", ex);
            ShowSettingsMessage("No se pudieron limpiar los temporales: " + ex.Message, false);
        }
    }

    private async void CleanupUnusedFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (IsStorageMaintenanceBlocked())
        {
            ShowCleanupStatus("Detén la operación activa y cierra Minecraft antes de limpiar archivos del launcher.", false);
            return;
        }

        CleanupUnusedFilesButton.IsEnabled = false;
        ShowCleanupStatus("Analizando archivos no utilizados…", false);

        try
        {
            var cleanupService = new UnusedFilesCleanupService(_instanceService);
            UnusedFilesCleanupPlan plan = await cleanupService.AnalyzeAsync(_preferences);
            if (plan.Entries.Count == 0)
            {
                ShowCleanupStatus("No se encontraron archivos seguros que limpiar.", true);
                return;
            }

            string summary = $"Se pueden liberar {FormatBytes(plan.TotalBytes)} en {plan.Entries.Count} carpeta(s).\n\n" +
                "La limpieza conserva las versiones y runtimes necesarios para las instalaciones activas. No elimina mods, configuraciones, mundos, capturas, resource packs ni los archivos compartidos de assets y libraries.\n\n" +
                "¿Quieres continuar?";
            if (!await ShowConfirmationAsync("Limpiar archivos no utilizados", summary))
            {
                ShowCleanupStatus("Limpieza cancelada.", false);
                return;
            }

            ShowCleanupStatus("Eliminando archivos no utilizados…", false);
            UnusedFilesCleanupResult result = await cleanupService.ExecuteAsync(plan);
            string freed = FormatBytes(result.FreedBytes);
            ShowCleanupStatus(
                result.FailedEntries == 0
                    ? $"Limpieza completada. Se liberaron {freed} en {result.DeletedEntries} carpeta(s)."
                    : $"Limpieza parcial. Se liberaron {freed}; {result.FailedEntries} carpeta(s) no se pudieron eliminar.",
                result.FailedEntries == 0);
            await RefreshStorageUsageAsync();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("CleanupUnusedFiles_Click: falló una operación.", ex);
            ShowCleanupStatus("No se pudo completar la limpieza: " + ex.Message, false);
        }
        finally
        {
            CleanupUnusedFilesButton.IsEnabled = true;
        }
    }

    private void OpenLogDirectory_Click(object? sender, RoutedEventArgs e)
    {
        string logDirectory = LauncherDiagnostics.LogDirectory;
        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo("explorer.exe");
                startInfo.ArgumentList.Add(logDirectory);
            }
            else if (OperatingSystem.IsMacOS())
            {
                startInfo = new ProcessStartInfo("open");
                startInfo.ArgumentList.Add(logDirectory);
            }
            else
            {
                startInfo = new ProcessStartInfo("xdg-open");
                startInfo.ArgumentList.Add(logDirectory);
            }

            startInfo.UseShellExecute = false;
            Process.Start(startInfo);
            ShowSettingsMessage("Carpeta de registros: " + logDirectory, true);
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("No se pudo abrir la carpeta de registros.", ex);
            ShowSettingsMessage(
                "No se pudo abrir la carpeta automáticamente. Ruta del registro: " +
                LauncherDiagnostics.LogPath,
                false);
        }
    }

    private void ShowCleanupStatus(string message, bool success)
    {
        CleanupUnusedFilesStatus.Text = message;
        CleanupUnusedFilesStatus.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(success ? "#A6E3B1" : "#F0C674"));
        CleanupUnusedFilesStatus.IsVisible = true;
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title, Width = 440, SizeToContent = Avalonia.Controls.SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0D1218")),
            Foreground = Avalonia.Media.Brushes.Gainsboro
        };
        var cancel = new Button { Content = "Cancelar", Padding = new Avalonia.Thickness(14, 8) };
        var confirm = new Button { Content = "Continuar", Padding = new Avalonia.Thickness(14, 8),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#743737")),
            Foreground = Avalonia.Media.Brushes.White };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8,
            Children = { cancel, confirm } };
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(22), Spacing = 14,
            Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons } };
        return await dialog.ShowDialog<bool>(this);
    }

    private void FillSettings()
    {
        _fillingSettings = true;
        RamSlider.Value = _preferences.MaximumRamMb;
        RamValueText.Text = $"{_preferences.MaximumRamMb} MB";
        AutomaticJavaInput.IsChecked = _preferences.UseAutomaticJava;
        EnableCustomJavaArgumentsInput.IsChecked = _preferences.EnableCustomJavaArguments;
        CustomJavaArgumentsInput.Text = _preferences.CustomJavaArguments;
        HolidayThemesInput.IsChecked = _preferences.EnableHolidayLauncherThemes;
        JavaPathInput.Text = _preferences.CustomJavaPath;
        CloseLauncherInput.IsChecked = _preferences.CloseLauncherOnGameStart;
        ShowConsoleInput.IsChecked = _preferences.ShowGameConsole;
        CatalogFileIdInput.Text = _preferences.ModpackCatalogFileId;
        MicrosoftOAuthClientIdInput.Text = _preferences.MicrosoftOAuthClientId ?? string.Empty;
        MicrosoftOAuthClientIdLoginInput.Text = _preferences.MicrosoftOAuthClientId ?? string.Empty;
        StoragePathInput.Text = string.IsNullOrWhiteSpace(_preferences.StorageRootPath)
            ? InstanceService.DefaultStorageRoot
            : _preferences.StorageRootPath;
        HomeRam.Text = $"{_preferences.MaximumRamMb} MB";
        DeveloperModeInput.IsChecked = _preferences.DeveloperMode;
        DeveloperNav.IsVisible = _preferences.DeveloperMode;
        _developerModeEnabled = _preferences.DeveloperMode;
        _developerFiltersLoading = true;
        DeveloperShowSnapshotsCheckBox.IsChecked = _preferences.DeveloperShowSnapshots;
        DeveloperShowBetasCheckBox.IsChecked = _preferences.DeveloperShowBetas;
        _developerFiltersLoading = false;
        if (!_developerModeEnabled && DeveloperPage.IsVisible) OpenPage("Inicio");
        _fillingSettings = false;
    }

    private void SidebarInstancesList_LayoutUpdated(object? sender, EventArgs e)
    {
        foreach (Button button in SidebarInstancesList.GetVisualDescendants().OfType<Button>())
        {
            if (button.DataContext is not InstalledInstance instance)
                continue;

            string marker = string.IsNullOrWhiteSpace(instance.IconFileId)
                ? instance.Id + "|no-icon"
                : instance.Id + "|" + instance.IconFileId;

            if (string.Equals(button.Tag as string, marker, StringComparison.Ordinal))
                continue;

            button.Tag = marker;
            if (!string.IsNullOrWhiteSpace(instance.IconFileId))
                _ = LoadSidebarInstanceIconAsync(button, instance, marker);
        }
    }

    private async Task LoadSidebarInstanceIconAsync(Button button, InstalledInstance instance, string marker)
    {
        try
        {
            string cacheRoot = Path.Combine(LauncherPaths.DefaultLauncherRoot, "cache", "instance-icons");
            string cacheKey = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(instance.IconFileId))).ToLowerInvariant();
            string filePath = Path.Combine(cacheRoot, cacheKey + ".img");
            Directory.CreateDirectory(cacheRoot);

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await _sidebarIconDrive.DownloadFileAsync(instance.IconFileId, temporaryPath);
                    if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                        return;
                    }

                    File.Move(temporaryPath, filePath, overwrite: true);
                }
                catch
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch { }
                    return;
                }
            }

            var bitmap = new Bitmap(filePath);
            if (!string.Equals(button.Tag as string, marker, StringComparison.Ordinal) ||
                button.DataContext is not InstalledInstance current ||
                !string.Equals(current.Id, instance.Id, StringComparison.OrdinalIgnoreCase))
            {
                bitmap.Dispose();
                return;
            }

            button.Content = new Image
            {
                Source = bitmap,
                Width = 40,
                Height = 40,
                Stretch = Avalonia.Media.Stretch.UniformToFill,
                IsHitTestVisible = false,
                Clip = new Avalonia.Media.EllipseGeometry(new Avalonia.Rect(0, 0, 40, 40))
            };
        }
        catch
        {
            // Un fallo de caché o de imagen no debe afectar a la navegación; queda el nombre como fallback.
        }
    }

    private async Task RefreshInstancesAsync()
    {
        try
        {
            var loaded = await _instanceService.LoadAllAsync();
            _instances.Clear();
            foreach (var instance in loaded.Where(x => !string.Equals(x.Id, DeveloperInstanceId, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                _instances.Add(instance);

            if (_selectedInstance is not null)
                _selectedInstance = _instances.FirstOrDefault(x => x.Id == _selectedInstance.Id);
            _selectedInstance ??= _instances.FirstOrDefault(x => string.Equals(x.Id, _preferences.LastSelectedInstanceId, StringComparison.OrdinalIgnoreCase));
            _selectedInstance ??= GetLastPlayedInstance();
            UpdateSelectedInstanceUi();
            await LoadSelectedInstanceBackgroundAsync(_selectedInstance);
            if (_selectedInstance is not null &&
                !string.Equals(_preferences.LastSelectedInstanceId, _selectedInstance.Id, StringComparison.Ordinal))
            {
                await RememberSelectedInstanceAsync(_selectedInstance);
            }
            else if (_selectedInstance is null && !string.IsNullOrWhiteSpace(_preferences.LastSelectedInstanceId))
            {
                _preferences.LastSelectedInstanceId = string.Empty;
                await _preferencesService.SaveAsync(_preferences);
            }

            InstancesEmpty.IsVisible = _instances.Count == 0;
            HomeInstanceCount.Text = _instances.Count.ToString();
            InstancesPathLabel.Text = "Carpeta: " + _instanceService.GetStorageRoot();
            InstancesMessage.IsVisible = false;
            RefreshDiscordPresence();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("RefreshInstancesAsync: falló una operación.", ex);
            InstancesMessage.Text = "No se pudieron leer las instancias: " + ex.Message;
            InstancesMessage.IsVisible = true;
        }
    }

    private void OpenAccountsFromWarning_Click(object? sender, RoutedEventArgs e)
    {
        OpenPage("Ajustes");
        SetSettingsTab(true);
    }

    private Task RefreshAccountsAsync()
    {
        _accounts.Clear();
        foreach (MicrosoftAccountInfo account in _accountService.GetAccounts())
            _accounts.Add(account);

        MicrosoftAccountInfo? selected = _accounts.FirstOrDefault(account => account.IsSelected)
            ?? _accounts.FirstOrDefault();
        AccountsList.SelectedItem = selected;
        RefreshSelectedAccountDetails();

        _accountHeadsCancellation?.Cancel();
        CancellationTokenSource skinLoad = new();
        _accountHeadsCancellation = skinLoad;
        _ = LoadAccountSkinTexturesAsync(_accounts.ToArray(), skinLoad);

        AccountStatus.Text = _accountService.IsOfflineModeActive
            ? $"Perfil local en uso: {_accountService.Username}"
            : _accountService.IsSignedIn
                ? $"Cuenta en uso: {_accountService.Username}"
                : _accounts.Count == 0
                    ? "No hay cuentas Microsoft añadidas."
                    : "Selecciona una cuenta para usarla con JUGAR.";

        HomeAccountWarningBorder.IsVisible =
            !_accountService.IsSignedIn && !_accountService.IsOfflineModeActive;
        return Task.CompletedTask;
    }

    private static Grid CreateSkinAvatar(MicrosoftAccountInfo account, double size)
    {
        var avatar = new Grid
        {
            Width = size,
            Height = size
        };

        avatar.Children.Add(new Border
        {
            CornerRadius = new Avalonia.CornerRadius(5),
            Background = new SolidColorBrush(Color.Parse("#252C34")),
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(account.Username) ? "?" : account.Username[..1].ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.Parse("#89939E")),
                FontWeight = Avalonia.Media.FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        });

        var face = new Image
        {
            Width = size,
            Height = size,
            Stretch = Avalonia.Media.Stretch.Fill
        };
        face.Bind(Image.SourceProperty, new Binding(nameof(MicrosoftAccountInfo.SkinTexturePath))
        {
            Source = account,
            Converter = new MinecraftSkinLayerConverter { Layer = "face" }
        });
        RenderOptions.SetBitmapInterpolationMode(face, BitmapInterpolationMode.None);
        avatar.Children.Add(face);

        var hat = new Image
        {
            Width = size,
            Height = size,
            Stretch = Avalonia.Media.Stretch.Fill
        };
        hat.Bind(Image.SourceProperty, new Binding(nameof(MicrosoftAccountInfo.SkinTexturePath))
        {
            Source = account,
            Converter = new MinecraftSkinLayerConverter { Layer = "hat" }
        });
        RenderOptions.SetBitmapInterpolationMode(hat, BitmapInterpolationMode.None);
        avatar.Children.Add(hat);

        return avatar;
    }

    private async Task LoadAccountSkinTexturesAsync(
        IReadOnlyList<MicrosoftAccountInfo> accounts,
        CancellationTokenSource source,
        bool isQuickAccountList = false)
    {
        try
        {
            CancellationToken token = source.Token;
            await Task.WhenAll(accounts
                .Where(account => !account.IsOffline && !string.IsNullOrWhiteSpace(account.Uuid))
                .Select(async account =>
                {
                    string? skinPath = await _minecraftSkinTextureService.GetSkinPathAsync(account.Uuid, token);
                    if (!token.IsCancellationRequested && !string.IsNullOrWhiteSpace(skinPath))
                        account.SkinTexturePath = skinPath;
                }));
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            // Al actualizar la lista, las cargas anteriores dejan de ser necesarias.
        }
        catch
        {
            // Los avatares son decorativos: un fallo de red no bloquea las cuentas ni el acceso.
        }
        finally
        {
            if (isQuickAccountList)
            {
                if (ReferenceEquals(_quickAccountHeadsCancellation, source))
                    _quickAccountHeadsCancellation = null;
            }
            else if (ReferenceEquals(_accountHeadsCancellation, source))
            {
                _accountHeadsCancellation = null;
            }

            source.Dispose();
        }
    }

    private void AccountsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedAccountDetails();
    }

    private void RefreshSelectedAccountDetails()
    {
        MicrosoftAccountInfo? selected = AccountsList.SelectedItem as MicrosoftAccountInfo;
        AccountDetailsPanel.IsVisible = selected is not null;

        MicrosoftUsernameText.Text = selected is null
            ? "—"
            : string.IsNullOrWhiteSpace(selected.Username)
                ? selected.Identifier
                : selected.Username;
        MicrosoftUuidText.Text = selected is null || string.IsNullOrWhiteSpace(selected.Uuid)
            ? "—"
            : selected.Uuid;

        UseSelectedAccountButton.IsEnabled = selected is not null;
        ReauthenticateAccountButton.IsEnabled = selected is not null && !selected.IsOffline;
        SignOutAccountButton.IsEnabled = selected is not null;

        bool isConnected = selected is not null &&
            !selected.IsOffline &&
            selected.IsSelected &&
            _accountService.IsSignedIn;
        string stateText = selected is null
            ? "NO CONECTADO"
            : selected.IsOffline
                ? "PERFIL LOCAL"
                : isConnected
                    ? "CONECTADO"
                    : "GUARDADA";

        SelectedAccountStateText.Text = stateText;
        SelectedAccountStateBadge.Background = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(
                selected is null ? "#343B43" :
                selected.IsOffline ? "#51451F" :
                isConnected ? "#1E6350" : "#3A454F"));
        SelectedAccountStateText.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(
                selected?.IsOffline == true ? "#F4D58D" :
                isConnected ? "#C0F0D5" : "#DCE2E8"));
    }

    private async void AddMicrosoftAccount_Click(object? sender, RoutedEventArgs e)
    {
        SetAccountButtonsEnabled(false);
        AccountStatus.Text = "Abriendo la autenticación de Microsoft…";
        try
        {
            await _accountService.AddAccountInteractivelyAsync();
            await RefreshAccountsAsync();
            AccountStatus.Text = "Cuenta Microsoft añadida y seleccionada.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("AddMicrosoftAccount_Click: falló una operación.", ex);
            AccountStatus.Text = "No se pudo añadir la cuenta: " + ex.Message;
        }
        finally
        {
            SetAccountButtonsEnabled(true);
        }
    }

    private async void UseSelectedAccount_Click(object? sender, RoutedEventArgs e)
    {
        if (AccountsList.SelectedItem is not MicrosoftAccountInfo account)
        {
            AccountStatus.Text = "Selecciona primero una cuenta Microsoft.";
            return;
        }

        try
        {
            bool selected = await _accountService.SelectAccountAsync(account.Identifier);
            if (!selected)
                throw new InvalidOperationException("No se pudo activar esa cuenta.");
            await RefreshAccountsAsync();
            AccountStatus.Text = "Cuenta seleccionada: " + _accountService.Username;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("UseSelectedAccount_Click: falló una operación.", ex);
            AccountStatus.Text = "No se pudo activar la cuenta: " + ex.Message;
        }
    }

    private async void ReauthenticateSelectedAccount_Click(object? sender, RoutedEventArgs e)
    {
        if (AccountsList.SelectedItem is not MicrosoftAccountInfo account)
        {
            AccountStatus.Text = "Selecciona primero una cuenta Microsoft.";
            return;
        }

        if (account.IsOffline)
        {
            AccountStatus.Text = "El perfil local no utiliza autenticación Microsoft.";
            return;
        }

        try
        {
            await _accountService.ReauthenticateAccountAsync(account.Identifier);
            await RefreshAccountsAsync();
            AccountStatus.Text = "La cuenta se autenticó de nuevo correctamente.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("ReauthenticateSelectedAccount_Click: falló una operación.", ex);
            AccountStatus.Text = "No se pudo reautenticar la cuenta: " + ex.Message;
        }
    }

    private async void SignOutSelectedAccount_Click(object? sender, RoutedEventArgs e)
    {
        if (AccountsList.SelectedItem is not MicrosoftAccountInfo account)
        {
            AccountStatus.Text = "Selecciona primero una cuenta.";
            return;
        }

        try
        {
            await _accountService.SignOutAccountAsync(account.Identifier);
            await RefreshAccountsAsync();
            AccountStatus.Text = "Se cerró la sesión de la cuenta seleccionada.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("SignOutSelectedAccount_Click: falló una operación.", ex);
            AccountStatus.Text = "No se pudo cerrar la sesión: " + ex.Message;
        }
    }

    private async void UseMicrosoftMode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _accountService.SetAccountModeAsync(MicrosoftAccountService.PremiumAccountMode);
            await RefreshAccountsAsync();
            if (!_accountService.IsSignedIn)
                AccountStatus.Text = "Modo Microsoft activado. Selecciona o añade una cuenta.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("UseMicrosoftMode_Click: falló una operación.", ex);
            AccountStatus.Text = "No se pudo activar el modo Microsoft: " + ex.Message;
        }
    }

    private void SetAccountButtonsEnabled(bool enabled)
    {
        AddMicrosoftAccountButton.IsEnabled = enabled &&
            _accountService.GetPremiumAccounts().Count < MicrosoftAccountService.MaxAccounts;
        UseSelectedAccountButton.IsEnabled = enabled &&
            AccountsList.SelectedItem is MicrosoftAccountInfo;
        ReauthenticateAccountButton.IsEnabled = enabled &&
            AccountsList.SelectedItem is MicrosoftAccountInfo { IsOffline: false };
        SignOutAccountButton.IsEnabled = enabled &&
            AccountsList.SelectedItem is MicrosoftAccountInfo;
        UseMicrosoftModeButton.IsEnabled = enabled;
        AccountsList.IsEnabled = enabled;
    }

    private async void SelectInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        _selectedInstance = _instances.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        UpdateSelectedInstanceUi();
        RefreshDiscordPresence();
        await RememberSelectedInstanceAsync(_selectedInstance);
        HeaderStatus.Text = $"Instalación seleccionada: {instance.Name}";
    }

    private async void SidebarInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        _selectedInstance = _instances.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        UpdateSelectedInstanceUi();
        RefreshDiscordPresence();
        OpenPage("Inicio");
        await RememberSelectedInstanceAsync(_selectedInstance);
    }

    private async Task RememberSelectedInstanceAsync(InstalledInstance? instance)
    {
        if (instance is null || string.Equals(_preferences.LastSelectedInstanceId, instance.Id, StringComparison.Ordinal))
            return;
        _preferences.LastSelectedInstanceId = instance.Id;
        try { await _preferencesService.SaveAsync(_preferences); }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("RememberSelectedInstanceAsync: falló una operación.", ex);
            HeaderStatus.Text = "No se pudo guardar la última instalación: " + ex.Message;
        }
    }

    private async void PlaySelectedInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (_runningGameProcess is not null)
        {
            await StopRunningMinecraftAsync();
            return;
        }

        if (_selectedInstance is null)
        {
            InstalledInstance? lastPlayed = GetLastPlayedInstance();
            if (lastPlayed is null)
            {
                OpenPage("Instancias");
                return;
            }

            _selectedInstance = lastPlayed;
            UpdateSelectedInstanceUi();
            await LoadSelectedInstanceBackgroundAsync(lastPlayed);
            await RememberSelectedInstanceAsync(lastPlayed);
            return;
        }

        HomePlayButton.DataContext = _selectedInstance;
        HomeInstanceOptionsButton.DataContext = _selectedInstance;
        HomeInstanceOptionsButton.IsEnabled = true;
        PlayInstance_Click(HomePlayButton, e);
    }

    private async Task StopRunningMinecraftAsync()
    {
        Process? process = _runningGameProcess;
        if (process is null)
            return;

        HomePlayButton.IsEnabled = false;
        HomePlayButton.Content = "CERRANDO...";
        GameStatus.Text = "Cerrando Minecraft…";

        try
        {
            if (!process.HasExited)
            {
                bool closeRequested = process.CloseMainWindow();
                if (closeRequested)
                    await Task.Run(() => process.WaitForExit(7000));

                if (!process.HasExited)
                {
                    bool forceClose = await ShowConfirmationAsync(
                        "Forzar cierre de Minecraft",
                        "Minecraft no respondió al cierre normal.\n\n" +
                        "¿Quieres forzar el cierre? Esto podría hacer que se pierda " +
                        "progreso que todavía no se haya guardado.");

                    if (!forceClose)
                    {
                        GameStatus.Text = "Minecraft sigue abierto.";
                        return;
                    }

                    process.Kill(entireProcessTree: true);
                    await Task.Run(() => process.WaitForExit(5000));
                }
            }

            if (process.HasExited)
            {
                GameStatus.Text = "Minecraft se cerró.";
                HeaderStatus.Text = "Minecraft cerrado";
            }
            else
            {
                GameStatus.Text = "Minecraft todavía está ejecutándose.";
            }
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("StopRunningMinecraftAsync: falló una operación.", ex);
            GameStatus.Text = "No se pudo cerrar Minecraft: " + ex.Message;
        }
        finally
        {
            bool hasExited;
            try { hasExited = process.HasExited; }
            catch { hasExited = true; }

            if (hasExited)
            {
                if (ReferenceEquals(_runningGameProcess, process))
                {
                    _runningGameProcess = null;
                    _runningGameInstanceId = null;
                    RefreshDiscordPresence();
                }

                if (_runningDeveloperVanilla)
                {
                    _runningDeveloperVanilla = false;
                    DeveloperPlayButton.Content = "JUGAR";
                    DeveloperPlayButton.IsEnabled = _developerModeEnabled;
                }

                HomePlayButton.Content = "JUGAR";
                try { process.Dispose(); } catch { }
            }
            else
            {
                HomePlayButton.Content = "CERRAR";
            }

            HomePlayButton.IsEnabled = _selectedInstance is not null || !hasExited;
        }
    }

    private InstalledInstance? GetLastPlayedInstance()
    {
        if (string.IsNullOrWhiteSpace(_lastPlayedInstanceId))
            return null;

        InstalledInstance? instance = _instances.FirstOrDefault(x =>
            string.Equals(x.Id, _lastPlayedInstanceId, StringComparison.OrdinalIgnoreCase));

        return instance is not null && instance.IsInstalled && instance.RuntimePrepared
            ? instance
            : null;
    }

    private void UpdateSelectedInstanceUi()
    {
        InstalledInstance? lastPlayed = GetLastPlayedInstance();
        InstalledInstance? displayInstance = _selectedInstance ?? lastPlayed;

        HomeSelectedInstanceText.Text = displayInstance is null
            ? "Selecciona o instala una instancia"
            : _selectedInstance is null
                ? $"Última instancia jugada: {displayInstance.Name}  ·  Minecraft {displayInstance.MinecraftVersion}"
                : $"{displayInstance.Name}  ·  Minecraft {displayInstance.MinecraftVersion}";

        HomePlayButton.IsEnabled = _selectedInstance is not null || lastPlayed is not null || _runningGameProcess is not null;
        HomePlayButton.DataContext = _selectedInstance ?? lastPlayed;
        HomeInstanceOptionsButton.DataContext = _selectedInstance;
        HomeInstanceOptionsButton.IsEnabled = _selectedInstance is not null;
    }

    private void SetSettingsTab(bool showAccounts)
    {
        AccountsSettingsPanel.IsVisible = showAccounts;
        NormalSettingsPanel.IsVisible = !showAccounts;

        AccountsTabButton.Background = showAccounts
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#26333A"))
            : Avalonia.Media.Brushes.Transparent;
        AccountsTabButton.Foreground = showAccounts
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6AD8E8"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5"));

        NormalSettingsTabButton.Background = showAccounts
            ? Avalonia.Media.Brushes.Transparent
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#26333A"));
        NormalSettingsTabButton.Foreground = showAccounts
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#6AD8E8"));
    }

    private void ShowNormalSettingsTab_Click(object? sender, RoutedEventArgs e)
        => SetSettingsTab(false);

    private void ShowAccountsSettingsTab_Click(object? sender, RoutedEventArgs e)
        => SetSettingsTab(true);

    private void OpenPage(string page)
    {
        HomePage.IsVisible = page == "Inicio";
        InstancesPage.IsVisible = page == "Instancias";
        GalleryPage.IsVisible = page == "Galería";
        SettingsPage.IsVisible = page == "Ajustes";
        ModpacksPage.IsVisible = page == "Modpacks";
        DeveloperPage.IsVisible = page == "Minecraft Vanilla" && _developerModeEnabled;
        RefreshDeveloperVanillaBackground();
        if (page == "Ajustes")
            SetSettingsTab(false);

        SetNav(HomeNav, page == "Inicio");
        SetNav(InstancesNav, page == "Instancias");
        SetNav(ModpacksNav, page == "Modpacks");
        SetNav(GalleryNav, page == "Galería");
        SetNav(SettingsNav, page == "Ajustes");
        SetNav(DeveloperNav, page == "Minecraft Vanilla");
        UpdateGlobalCountdownOpacity();
    }

    private async void DeveloperNav_Click(object? sender, RoutedEventArgs e)
    {
        if (!_developerModeEnabled) return;
        OpenPage("Minecraft Vanilla");
        await LoadDeveloperVersionsAsync(false);
    }

    private async void DeveloperReloadVersions_Click(object? sender, RoutedEventArgs e) => await LoadDeveloperVersionsAsync(true);

    private async Task LoadDeveloperVersionsAsync(bool reload)
    {
        if (!_developerModeEnabled) return;
        if (_developerVersionsLoaded && !reload) { ApplyDeveloperVersionFilters(_preferences.DeveloperMinecraftVersion, string.Empty); return; }
        DeveloperReloadVersionsButton.IsEnabled = false;
        DeveloperVersionComboBox.IsEnabled = false;
        DeveloperPlayButton.IsEnabled = false;
        DeveloperStatus.Text = "Cargando versiones oficiales de Minecraft…";
        try
        {
            using var response = await DeveloperHttpClient.GetAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            string latestRelease = root.GetProperty("latest").GetProperty("release").GetString() ?? string.Empty;
            _developerVersions.Clear();
            foreach (var version in root.GetProperty("versions").EnumerateArray())
            {
                string id = version.GetProperty("id").GetString() ?? string.Empty;
                string type = version.GetProperty("type").GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(id)) _developerVersions.Add((id, type));
            }
            _developerVersionsLoaded = true;
            ApplyDeveloperVersionFilters(_preferences.DeveloperMinecraftVersion, latestRelease);
            DeveloperStatus.Text = $"Se cargaron {_developerVersions.Count} versiones oficiales.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("LoadDeveloperVersionsAsync: falló una operación.", ex);
            _developerVersionsLoaded = false; DeveloperStatus.Text = "No se pudieron cargar las versiones de Minecraft: " + ex.Message;
        }
        finally { DeveloperReloadVersionsButton.IsEnabled = true; DeveloperVersionComboBox.IsEnabled = true; }
    }

    private void ApplyDeveloperVersionFilters(string preferredVersion, string fallbackRelease)
    {
        bool snapshots = DeveloperShowSnapshotsCheckBox.IsChecked == true;
        bool betas = DeveloperShowBetasCheckBox.IsChecked == true;
        var visible = _developerVersions.Where(v => v.Type.Equals("release", StringComparison.OrdinalIgnoreCase) || (snapshots && v.Type.Equals("snapshot", StringComparison.OrdinalIgnoreCase)) || (betas && (v.Type.Equals("old_beta", StringComparison.OrdinalIgnoreCase) || v.Type.Equals("old_alpha", StringComparison.OrdinalIgnoreCase)))).Select(v => v.Id).ToList();
        DeveloperVersionComboBox.ItemsSource = visible;
        string desired = preferredVersion;
        if (string.IsNullOrWhiteSpace(desired) || !visible.Contains(desired, StringComparer.OrdinalIgnoreCase)) desired = fallbackRelease;
        if (string.IsNullOrWhiteSpace(desired) || !visible.Contains(desired, StringComparer.OrdinalIgnoreCase)) desired = visible.FirstOrDefault() ?? string.Empty;
        DeveloperVersionComboBox.SelectedItem = visible.FirstOrDefault(v => string.Equals(v, desired, StringComparison.OrdinalIgnoreCase));
        DeveloperPlayButton.IsEnabled = DeveloperVersionComboBox.SelectedItem is string;
    }

    private async void DeveloperVersionFilter_Changed(object? sender, RoutedEventArgs e)
    {
        if (_developerFiltersLoading || !_developerModeEnabled) return;

        bool previousSnapshots = _preferences.DeveloperShowSnapshots;
        bool previousBetas = _preferences.DeveloperShowBetas;
        _preferences.DeveloperShowSnapshots = DeveloperShowSnapshotsCheckBox.IsChecked == true;
        _preferences.DeveloperShowBetas = DeveloperShowBetasCheckBox.IsChecked == true;

        try
        {
            await _preferencesService.SaveAsync(_preferences);
            ApplyDeveloperVersionFilters(
                DeveloperVersionComboBox.SelectedItem as string ?? _preferences.DeveloperMinecraftVersion,
                string.Empty);
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("DeveloperVersionFilter_Changed: falló una operación.", ex);
            _preferences.DeveloperShowSnapshots = previousSnapshots;
            _preferences.DeveloperShowBetas = previousBetas;
            _developerFiltersLoading = true;
            DeveloperShowSnapshotsCheckBox.IsChecked = previousSnapshots;
            DeveloperShowBetasCheckBox.IsChecked = previousBetas;
            _developerFiltersLoading = false;
            DeveloperStatus.Text = "No se pudieron guardar los filtros de versiones: " + ex.Message;
        }
    }

    private async void DeveloperVersionComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_developerModeEnabled || _developerFiltersLoading) return;

        string version = DeveloperVersionComboBox.SelectedItem as string ?? string.Empty;
        string previousVersion = _preferences.DeveloperMinecraftVersion;
        _preferences.DeveloperMinecraftVersion = version;

        try
        {
            await _preferencesService.SaveAsync(_preferences);
            DeveloperPlayButton.IsEnabled = !string.IsNullOrWhiteSpace(version) && _runningGameProcess is null;
            DeveloperStatus.Text = string.IsNullOrWhiteSpace(version)
                ? "Selecciona una versión de Minecraft."
                : $"Minecraft Vanilla {version} listo para preparar.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("DeveloperVersionComboBox_SelectionChanged: falló una operación.", ex);
            _preferences.DeveloperMinecraftVersion = previousVersion;
            _developerFiltersLoading = true;
            DeveloperVersionComboBox.SelectedItem = string.IsNullOrWhiteSpace(previousVersion)
                ? null
                : previousVersion;
            _developerFiltersLoading = false;
            DeveloperPlayButton.IsEnabled = !string.IsNullOrWhiteSpace(previousVersion) && _runningGameProcess is null;
            DeveloperStatus.Text = "No se pudo guardar la versión seleccionada: " + ex.Message;
        }
    }

    private void BeginRuntimeOperation(DownloadOperationController controller, string message)
    {
        _runtimeOperationController = controller;
        RuntimeGlobalOperationMessage.Text = message;
        RuntimeGlobalOperationProgress.Value = 0;
        RuntimeGlobalOperationPauseResumeButton.Content = "Pausar";
        RuntimeGlobalOperationPauseResumeButton.IsEnabled = true;
        RuntimeGlobalOperationStopButton.IsEnabled = true;
        RuntimeGlobalOperationPanel.IsVisible = true;
    }

    private void HideRuntimeOperationControls()
    {
        RuntimeGlobalOperationPanel.IsVisible = false;
    }

    private void EndRuntimeOperation(DownloadOperationController? controller)
    {
        if (controller is null)
            return;

        if (ReferenceEquals(_runtimeOperationController, controller))
            _runtimeOperationController = null;

        RuntimeGlobalOperationPanel.IsVisible = false;
        RuntimeGlobalOperationProgress.Value = 0;
        RuntimeGlobalOperationPauseResumeButton.Content = "Pausar";
        RuntimeGlobalOperationPauseResumeButton.IsEnabled = true;
        RuntimeGlobalOperationStopButton.IsEnabled = true;
        try { controller.Dispose(); } catch { }
    }

    private void PauseResumeRuntimeOperation_Click(object? sender, RoutedEventArgs e)
    {
        DownloadOperationController? controller = _runtimeOperationController;
        if (controller is null || controller.IsStopped)
            return;

        if (controller.IsPaused)
        {
            controller.Resume();
            RuntimeGlobalOperationPauseResumeButton.Content = "Pausar";
            RuntimeGlobalOperationMessage.Text = "Reanudando la preparación de Minecraft…";
        }
        else
        {
            controller.Pause();
            RuntimeGlobalOperationPauseResumeButton.Content = "Reanudar";
            RuntimeGlobalOperationMessage.Text = "Preparación pausada. Pulsa Reanudar para continuar.";
        }
    }

    private void StopRuntimeOperation_Click(object? sender, RoutedEventArgs e)
    {
        StopRuntimeOperation();
    }

    private void StopRuntimeOperation()
    {
        DownloadOperationController? controller = _runtimeOperationController;
        if (controller is null)
            return;

        controller.Stop();
        RuntimeGlobalOperationStopButton.IsEnabled = false;
        RuntimeGlobalOperationPauseResumeButton.IsEnabled = false;
        RuntimeGlobalOperationMessage.Text = "Deteniendo la preparación de Minecraft…";
    }

    private void CancelDeveloperPreparation_Click(object? sender, RoutedEventArgs e)
    {
        if (_runtimeOperationController is null)
            return;

        CancelDeveloperPreparationButton.IsEnabled = false;
        StopRuntimeOperation();
        DeveloperStatus.Text = "Deteniendo la preparación de Minecraft Vanilla…";
    }

    private async void DeveloperVanillaPlay_Click(object? sender, RoutedEventArgs e)
    {
        if (!_developerModeEnabled) return;
        if (_runningGameProcess is not null)
        {
            if (_runningDeveloperVanilla) await StopRunningMinecraftAsync();
            else DeveloperStatus.Text = "Cierra la otra instancia antes de iniciar Minecraft Vanilla.";
            return;
        }

        if (_activeDownloadController is not null || _runtimeOperationController is not null)
        {
            DeveloperStatus.Text = "Espera a que termine la operación o preparación actual antes de iniciar Minecraft Vanilla.";
            return;
        }

        string? version = DeveloperVersionComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(version)) { DeveloperStatus.Text = "Selecciona una versión de Minecraft."; return; }
        var session = await _accountService.GetValidSessionAsync();
        if (session is null) { DeveloperStatus.Text = "Debes iniciar sesión con una cuenta válida para jugar Minecraft Vanilla."; return; }
        _preferences.DeveloperMinecraftVersion = version;
        await _preferencesService.SaveAsync(_preferences);
        var instance = new InstalledInstance { Id = DeveloperInstanceId, Name = $"Minecraft {version} Vanilla", IsInstalled = true, RuntimePrepared = false, MinecraftVersion = version, InstalledVersion = version, Loader = "vanilla" };
        DeveloperPlayButton.IsEnabled = false;
        DeveloperReloadVersionsButton.IsEnabled = false;
        DeveloperProgress.Value = 0;
        DeveloperProgress.IsVisible = true;
        CancelDeveloperPreparationButton.IsVisible = true;
        CancelDeveloperPreparationButton.IsEnabled = true;
        DeveloperStatus.Text = $"Preparando Minecraft Vanilla {version}…";

        var controller = new DownloadOperationController();
        BeginRuntimeOperation(controller, $"Preparando Minecraft Vanilla {version}…");
        var progress = new Progress<double>(value =>
        {
            double safe = double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
            DeveloperProgress.Value = safe;
            RuntimeGlobalOperationProgress.Value = safe;
        });
        var status = new Progress<string>(message =>
        {
            DeveloperStatus.Text = message;
            if (RuntimeGlobalOperationPanel.IsVisible)
                RuntimeGlobalOperationMessage.Text = message;
        });

        try
        {
            string launchVersion = await RunPausableRuntimePhaseAsync(
                controller,
                cancellationToken => _runtimeService.PrepareAsync(
                    instance, _preferences, progress, status, cancellationToken));

            HideRuntimeOperationControls();
            CancelDeveloperPreparationButton.IsVisible = false;
            instance.RuntimePrepared = true;
            instance.LaunchVersionName = launchVersion;
            DeveloperStatus.Text = $"Iniciando Minecraft Vanilla {version}…";
            Process process = await _runtimeService.LaunchAsync(instance, _preferences, session);
            _runningGameProcess = process;
            _runningGameInstanceId = DeveloperInstanceId;
            _runningDeveloperVanilla = true;
            void HandleGameExited(object? exitSender, EventArgs exitArgs)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!ReferenceEquals(_runningGameProcess, process))
                        return;

                    _runningGameProcess = null;
                    _runningGameInstanceId = null;
                    _runningDeveloperVanilla = false;
                    DeveloperPlayButton.Content = "JUGAR";
                    DeveloperPlayButton.IsEnabled = _developerModeEnabled;
                    DeveloperStatus.Text = $"Minecraft Vanilla {version} se ha cerrado.";
                    RefreshDiscordPresence();
                    try { process.Dispose(); } catch { }
                });
            }

            // Conectar antes de activar las notificaciones y comprobar también la carrera
            // en la que Java termina entre Start() y la suscripción de la interfaz.
            process.Exited += HandleGameExited;
            process.EnableRaisingEvents = true;
            if (process.HasExited)
                HandleGameExited(process, EventArgs.Empty);

            RefreshDiscordPresence();
            if (_preferences.ShowGameConsole)
            {
                _gameConsoleWindow = new GameConsoleWindow(process, instance.Name);
                _gameConsoleWindow.Show();
            }
            else
            {
                process.OutputDataReceived += (_, _) => { };
                process.ErrorDataReceived += (_, _) => { };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            bool vanillaStillRunning;
            try { vanillaStillRunning = !process.HasExited; }
            catch { vanillaStillRunning = false; }

            if (vanillaStillRunning)
            {
                DeveloperPlayButton.Content = "CERRAR";
                DeveloperPlayButton.IsEnabled = true;
                DeveloperStatus.Text = $"Minecraft Vanilla {version} se inició correctamente.";
                if (_preferences.CloseLauncherOnGameStart)
                    Close();
            }
            else
            {
                HandleGameExited(process, EventArgs.Empty);
                DeveloperStatus.Text = $"Minecraft Vanilla {version} se cerró durante el inicio.";
            }
        }
        catch (OperationCanceledException)
        {
            DeveloperStatus.Text = controller.IsStopped
                ? "La preparación de Minecraft Vanilla se detuvo."
                : "La preparación de Minecraft Vanilla se canceló.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("DeveloperVanillaPlay_Click: falló una operación.", ex);
            DeveloperStatus.Text = "No se pudo iniciar Minecraft Vanilla: " + ex.Message;
        }
        finally
        {
            EndRuntimeOperation(controller);
            DeveloperProgress.IsVisible = false;
            CancelDeveloperPreparationButton.IsVisible = false;
            CancelDeveloperPreparationButton.IsEnabled = true;
            DeveloperReloadVersionsButton.IsEnabled = true;
            DeveloperPlayButton.IsEnabled = _developerModeEnabled;
        }
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
            var scanner = new ScreenshotGalleryScanner(_instanceService, store);
            var loaded = new System.Collections.Generic.List<GalleryEntry>();
            IReadOnlyList<ScreenshotGalleryFile> files =
                await scanner.LoadAllAsync(_instances.ToArray());

            foreach (ScreenshotGalleryFile file in files)
            {
                loaded.Add(new GalleryEntry
                {
                    InstanceId = file.InstanceId,
                    InstanceName = file.InstanceName,
                    FilePath = file.FilePath,
                    CapturedAt = file.CapturedAt
                });
            }

            _galleryItems.Clear();
            _galleryItems.AddRange(loaded);
            _gallerySelection.RemoveWhere(path => !_galleryItems.Any(item => LauncherPathRules.PathsEqual(item.FilePath, path)));
            RefreshGalleryInstanceOptions();
            ApplyGalleryFilters();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("RefreshGalleryAsync: falló una operación.", ex);
            GalleryMessage.Text = "No se pudo cargar la galería: " + ex.Message;
            GalleryMessage.IsVisible = true;
            GalleryEmpty.IsVisible = true;
        }

        await Task.CompletedTask;
    }

    private void RefreshGalleryInstanceOptions()
    {
        string previous = _galleryInstanceFilter;
        GalleryInstanceFilter.Items.Clear();
        GalleryInstanceFilter.Items.Add(new ComboBoxItem { Content = "Todas las instalaciones", Tag = string.Empty });
        foreach (var instance in _galleryItems
            .GroupBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.InstanceName, StringComparer.OrdinalIgnoreCase))
        {
            GalleryInstanceFilter.Items.Add(new ComboBoxItem { Content = instance.InstanceName, Tag = instance.InstanceId });
        }

        var selected = GalleryInstanceFilter.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString() ?? string.Empty, previous, StringComparison.OrdinalIgnoreCase))
            ?? GalleryInstanceFilter.Items.OfType<ComboBoxItem>().First();
        GalleryInstanceFilter.SelectedItem = selected;
        _galleryInstanceFilter = selected.Tag?.ToString() ?? string.Empty;
    }

    private void GalleryFilter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        // Avalonia puede disparar SelectionChanged durante InitializeComponent,
        // antes de que todos los controles del XAML hayan sido creados.
        if (GalleryInstanceFilter is null ||
            GallerySortFilter is null ||
            GalleryImagesPanel is null)
        {
            return;
        }

        if (GalleryInstanceFilter.SelectedItem is ComboBoxItem instance)
            _galleryInstanceFilter = instance.Tag?.ToString() ?? string.Empty;
        if (GallerySortFilter.SelectedItem is ComboBoxItem sort)
            _gallerySortMode = sort.Tag?.ToString() ?? "newest";
        ApplyGalleryFilters();
    }

    private System.Collections.Generic.List<GalleryEntry> GetVisibleGalleryItems()
    {
        var query = _galleryItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_galleryInstanceFilter))
            query = query.Where(item => string.Equals(item.InstanceId, _galleryInstanceFilter, StringComparison.OrdinalIgnoreCase));

        return _gallerySortMode switch
        {
            "oldest" => query.OrderBy(item => item.CapturedAt).ToList(),
            "name" => query.OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
            "instance" => query.OrderBy(item => item.InstanceName, StringComparer.OrdinalIgnoreCase).ThenByDescending(item => item.CapturedAt).ToList(),
            _ => query.OrderByDescending(item => item.CapturedAt).ToList()
        };
    }

    private void ApplyGalleryFilters()
    {
        if (GalleryInstanceFilter is null || GallerySortFilter is null || GalleryImagesPanel is null)
            return;

        var visible = GetVisibleGalleryItems();
        GalleryImagesPanel.Children.Clear();
        foreach (var entry in visible)
        {
            try
            {
                var image = new Image
                {
                    Source = new Bitmap(entry.FilePath),
                    Width = 190,
                    Height = 112,
                    Stretch = Avalonia.Media.Stretch.UniformToFill
                };
                var name = new TextBlock
                {
                    Text = entry.InstanceName,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };
                var fileName = new TextBlock
                {
                    Text = entry.FileName,
                    FontSize = 10,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5")),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };
                var date = new TextBlock
                {
                    Text = entry.CapturedAt.ToString("dd/MM/yyyy HH:mm"),
                    FontSize = 10,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5"))
                };
                var selectState = new TextBlock
                {
                    Text = _gallerySelection.Contains(entry.FilePath) ? "✓ Seleccionada" : (_gallerySelectionMode ? "Toca para seleccionar" : "Abrir captura"),
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(_gallerySelection.Contains(entry.FilePath) ? "#6AD8E8" : "#8D99A5")),
                    FontSize = 10
                };
                var content = new StackPanel { Spacing = 7 };
                content.Children.Add(image);
                content.Children.Add(name);
                content.Children.Add(fileName);
                content.Children.Add(date);
                content.Children.Add(selectState);
                var card = new Button
                {
                    Width = 230,
                    Margin = new Avalonia.Thickness(0, 0, 12, 12),
                    Padding = new Avalonia.Thickness(9),
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(_gallerySelection.Contains(entry.FilePath) ? "#1A3038" : "#0D1218")),
                    BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(_gallerySelection.Contains(entry.FilePath) ? "#6AD8E8" : "#27323D")),
                    BorderThickness = new Avalonia.Thickness(1),
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    Content = content,
                    Tag = entry
                };
                card.Click += GalleryCard_Click;
                card.PointerPressed += GalleryCard_PointerPressed;
                GalleryImagesPanel.Children.Add(card);
            }
            catch
            {
                // Una captura dañada no debe impedir que se muestren las demás.
            }
        }

        int count = visible.Count;
        GalleryEmpty.IsVisible = count == 0;
        GalleryEmpty.Text = count == 0
            ? "No hay capturas que coincidan con estos filtros."
            : $"Se encontraron {count} capturas.";
        GalleryMessage.IsVisible = false;
        GalleryCountText.Text = $"{count} capturas · {_gallerySelection.Count} seleccionadas";
        GallerySelectionButton.Content = _gallerySelectionMode ? "Cancelar selección" : "Seleccionar";
        GalleryDeleteSelectedButton.IsVisible = _gallerySelectionMode;
        GalleryDeleteSelectedButton.IsEnabled = _gallerySelection.Count > 0;
        GalleryDeleteSelectedButton.Content = $"Eliminar seleccionadas ({_gallerySelection.Count})";
    }

    private void GalleryCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GalleryEntry entry })
            return;

        if (_gallerySelectionMode)
        {
            if (!_gallerySelection.Add(entry.FilePath))
                _gallerySelection.Remove(entry.FilePath);
            ApplyGalleryFilters();
            return;
        }

        var visible = GetVisibleGalleryItems();
        _galleryViewerIndex = visible.FindIndex(item => LauncherPathRules.PathsEqual(item.FilePath, entry.FilePath));
        ShowGalleryViewer();
    }

    private async void GalleryCard_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind !=
            Avalonia.Input.PointerUpdateKind.RightButtonPressed)
            return;

        if (sender is not Button { Tag: GalleryEntry entry })
            return;

        e.Handled = true;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                throw new InvalidOperationException("El sistema no ofrece acceso al portapapeles.");

            using var bitmap = new Bitmap(entry.FilePath);
            await clipboard.SetValueAsync(Avalonia.Input.DataFormat.Bitmap, bitmap);
            GalleryMessage.Text = $"Captura copiada al portapapeles: {entry.FileName}";
            GalleryMessage.IsVisible = true;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("GalleryCard_PointerPressed: falló una operación.", ex);
            GalleryMessage.Text = "No se pudo copiar la captura: " + ex.Message;
            GalleryMessage.IsVisible = true;
        }
    }

    private void ToggleGallerySelection_Click(object? sender, RoutedEventArgs e)
    {
        _gallerySelectionMode = !_gallerySelectionMode;
        if (!_gallerySelectionMode)
            _gallerySelection.Clear();
        ApplyGalleryFilters();
    }

    private async void DeleteSelectedGallery_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _galleryItems.Where(item => _gallerySelection.Contains(item.FilePath)).ToList();
        if (selected.Count == 0)
            return;
        if (!await ConfirmGalleryActionAsync($"¿Eliminar definitivamente {selected.Count} captura(s)?"))
            return;

        var store = new ScreenshotArchiveStore(Path.Combine(LauncherPaths.DefaultLauncherRoot, "archived-screenshots"));
        int deleted = 0;
        foreach (var item in selected)
        {
            try
            {
                if (!File.Exists(item.FilePath))
                    continue;
                File.Delete(item.FilePath);
                store.CleanupArchiveIfEmpty(item.FilePath);
                deleted++;
            }
            catch
            {
                // Se continúa con las demás capturas aunque una no se pueda borrar.
            }
        }

        _gallerySelection.Clear();
        _gallerySelectionMode = false;
        await RefreshGalleryAsync();
        GalleryMessage.Text = $"Se eliminaron {deleted} capturas.";
        GalleryMessage.IsVisible = true;
    }

    private async Task<bool> ConfirmGalleryActionAsync(string message)
    {
        var dialog = new Window
        {
            Title = "Confirmar eliminación",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#111820")),
            Padding = new Avalonia.Thickness(22)
        };
        var label = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 0, 0, 20) };
        var yes = new Button { Content = "Eliminar", Padding = new Avalonia.Thickness(16, 8), Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#9B343B")) };
        var no = new Button { Content = "Cancelar", Padding = new Avalonia.Thickness(16, 8) };
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
        buttons.Children.Add(no);
        buttons.Children.Add(yes);
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(label);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }

    private void ShowGalleryViewer()
    {
        var visible = GetVisibleGalleryItems();
        if (_galleryViewerIndex < 0 || _galleryViewerIndex >= visible.Count)
            return;
        var entry = visible[_galleryViewerIndex];

        var viewer = new Window
        {
            Title = $"{entry.InstanceName} — {entry.FileName}",
            Width = 1000,
            Height = 720,
            MinWidth = 640,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#090C10"))
        };
        var image = new Image { Source = new Bitmap(entry.FilePath), Stretch = Avalonia.Media.Stretch.Uniform };
        var imageBorder = new Border { Child = image, Padding = new Avalonia.Thickness(16) };
        var info = new TextBlock { Text = $"{entry.InstanceName}  •  {entry.FileName}  •  {entry.CapturedAt:dd/MM/yyyy HH:mm}", Foreground = Avalonia.Media.Brushes.Gainsboro, Margin = new Avalonia.Thickness(12), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var previous = new Button { Content = "Anterior", Padding = new Avalonia.Thickness(14, 8) };
        var next = new Button { Content = "Siguiente", Padding = new Avalonia.Thickness(14, 8) };
        var delete = new Button { Content = "Eliminar", Padding = new Avalonia.Thickness(14, 8) };
        var close = new Button { Content = "Cerrar", Padding = new Avalonia.Thickness(14, 8) };
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Spacing = 8, Margin = new Avalonia.Thickness(8) };
        buttons.Children.Add(previous);
        buttons.Children.Add(next);
        buttons.Children.Add(delete);
        buttons.Children.Add(close);
        var layout = new DockPanel();
        DockPanel.SetDock(info, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(info);
        layout.Children.Add(buttons);
        layout.Children.Add(imageBorder);
        viewer.Content = layout;

        void SetViewerImage()
        {
            var current = GetVisibleGalleryItems();
            if (current.Count == 0)
            {
                viewer.Close();
                return;
            }
            _galleryViewerIndex = Math.Clamp(_galleryViewerIndex, 0, current.Count - 1);
            var selected = current[_galleryViewerIndex];
            try { image.Source = new Bitmap(selected.FilePath); } catch { image.Source = null; }
            info.Text = $"{selected.InstanceName}  •  {selected.FileName}  •  {selected.CapturedAt:dd/MM/yyyy HH:mm}";
            viewer.Title = $"{selected.InstanceName} — {selected.FileName}";
        }

        previous.Click += (_, _) => { _galleryViewerIndex = (_galleryViewerIndex - 1 + GetVisibleGalleryItems().Count) % Math.Max(1, GetVisibleGalleryItems().Count); SetViewerImage(); };
        next.Click += (_, _) => { _galleryViewerIndex = (_galleryViewerIndex + 1) % Math.Max(1, GetVisibleGalleryItems().Count); SetViewerImage(); };
        close.Click += (_, _) => viewer.Close();
        delete.Click += async (_, _) =>
        {
            var current = GetVisibleGalleryItems();
            if (_galleryViewerIndex < 0 || _galleryViewerIndex >= current.Count)
                return;
            var target = current[_galleryViewerIndex];
            if (!await ConfirmGalleryActionAsync($"¿Eliminar definitivamente la captura {target.FileName}?"))
                return;
            try
            {
                File.Delete(target.FilePath);
                new ScreenshotArchiveStore(Path.Combine(LauncherPaths.DefaultLauncherRoot, "archived-screenshots")).CleanupArchiveIfEmpty(target.FilePath);
                await RefreshGalleryAsync();
                if (GetVisibleGalleryItems().Count == 0) { viewer.Close(); return; }
                _galleryViewerIndex = Math.Min(_galleryViewerIndex, GetVisibleGalleryItems().Count - 1);
                SetViewerImage();
            }
            catch (Exception ex)
            {
                LauncherDiagnostics.WriteException("ShowGalleryViewer: falló una operación.", ex);
                GalleryMessage.Text = "No se pudo eliminar la captura: " + ex.Message;
                GalleryMessage.IsVisible = true;
            }
        };
        viewer.Show(this);
    }

    private static IEnumerable<string> EnumerateGalleryImages(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            string[] files;
            string[] subdirectories;

            try
            {
                files = Directory.GetFiles(directory);
                subdirectories = Directory.GetDirectories(directory);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string file in files)
            {
                if (IsGalleryImage(file))
                    yield return file;
            }

            foreach (string subdirectory in subdirectories)
            {
                try
                {
                    // Evita seguir enlaces simbólicos y entrar en árboles externos o ciclos.
                    if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0)
                        pending.Push(subdirectory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
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
        bool valid = OfflineAccountService.IsValidUsername(username);

        if (!valid)
        {
            OfflineProfileStatus.Text = "Usa entre 3 y 16 caracteres: letras, números o guion bajo.";
            OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
            OfflineProfileStatus.IsVisible = true;
            return;
        }

        try
        {
            string skinModel = (OfflineSkinModelComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "wide";
            await _offlineAccountService.SaveAsync(username, _offlineSkinPath, skinModel);
            _preferences.AccountMode = "offline";
            await _preferencesService.SaveAsync(_preferences);
            await _accountService.RefreshAccountModeAsync();
            await RefreshAccountsAsync();
            OfflineProfileStatus.Text = $"Perfil sin conexión guardado: {username}";
            OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3B1"));
            OfflineProfileStatus.IsVisible = true;
            GameStatus.Text = "Perfil sin conexión listo para probar Minecraft.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("SaveOfflineProfile_Click: falló una operación.", ex);
            OfflineProfileStatus.Text = "No se pudo guardar el perfil: " + ex.Message;
            OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
            OfflineProfileStatus.IsVisible = true;
        }
    }

    private void CancelRuntimePreparation_Click(object? sender, RoutedEventArgs e)
    {
        if (_runtimeOperationController is null)
            return;

        StopRuntimeOperation();
        GameStatus.Text = "Deteniendo la preparación de Minecraft…";
    }

    private async Task<bool> PrepareLaunchAssetsAsync(InstalledInstance instance)
    {
        try
        {
            var result = await Task.Run(() => _resourcePackSelectionService.ApplyBundledSelectionIfNeeded(instance));
            if (result.MissingResourcePacks.Count > 0)
            {
                GameStatus.Text = "No se puede iniciar: faltan resource packs requeridos.";
                await ShowInformationAsync("Resource packs requeridos", "No se pudieron encontrar o reparar los resource packs seleccionados por este modpack.\n\nFaltan:\n" + string.Join(", ", result.MissingResourcePacks) + "\n\nPrueba a verificar la integridad de la instancia antes de volver a iniciar Minecraft.");
                return false;
            }
            string instanceDirectory = _instanceService.GetInstanceDirectory(instance.Id);
            if (_accountService.IsOfflineModeActive && _accountService.OfflineProfile is { } profile)
                await Task.Run(() => _offlineSkinService.ApplyLocalSkin(instanceDirectory, instance.MinecraftVersion, instance.Loader, profile));
            else
                await Task.Run(() => _offlineSkinService.DisableLocalSkin(instanceDirectory));
            return true;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("PrepareLaunchAssetsAsync: falló una operación.", ex);
            GameStatus.Text = "No se pudieron preparar los archivos del perfil.";
            await ShowInformationAsync("No se pudo preparar Minecraft", "Negative Launcher no iniciará el juego para evitar que Minecraft reemplace la selección de resource packs o se abra con la configuración de skin incompleta.\n\n" + ex.Message);
            return false;
        }
    }

    private async Task ShowInformationAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title, Width = 500, SizeToContent = Avalonia.Controls.SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0D1218")),
            Foreground = Avalonia.Media.Brushes.Gainsboro
        };
        var close = new Button { Content = "Aceptar", Padding = new Avalonia.Thickness(14, 8), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(22), Spacing = 14, Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, close } };
        await dialog.ShowDialog(this);
    }
    private async void PlayInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        if (_activeDownloadController is not null || _runtimeOperationController is not null)
        {
            await ShowInformationAsync(
                "Operación en curso",
                "Espera a que termine la operación o preparación actual antes de iniciar otra instancia.");
            return;
        }

        if (_runningGameProcess is not null)
        {
            GameStatus.Text = "Ya hay una instancia de Minecraft abierta. Ciérrala antes de iniciar otra.";
            await ShowInformationAsync("Minecraft ya está abierto", GameStatus.Text);
            return;
        }

        _selectedInstance = _instances.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        UpdateSelectedInstanceUi();
        await LoadSelectedInstanceBackgroundAsync(_selectedInstance);
        OpenPage("Inicio");
        await RememberSelectedInstanceAsync(instance);
        button.IsEnabled = false;
        GameProgress.Value = 0;
        GameProgress.IsVisible = true;
        GameStatus.Text = $"Preparando {instance.Name}…";

        bool needsPreparation = !instance.RuntimePrepared || string.IsNullOrWhiteSpace(instance.LaunchVersionName);
        var runtimeController = new DownloadOperationController();
        if (needsPreparation)
            BeginRuntimeOperation(runtimeController, $"Preparando {instance.Name}…");
        else
            _runtimeOperationController = runtimeController;
        RuntimePreparationControls.IsVisible = needsPreparation;

        var progress = new Progress<double>(value =>
        {
            double safe = double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
            GameProgress.Value = safe;
            RuntimeGlobalOperationProgress.Value = safe;
        });
        var status = new Progress<string>(value =>
        {
            GameStatus.Text = value;
            if (RuntimeGlobalOperationPanel.IsVisible)
                RuntimeGlobalOperationMessage.Text = value;
        });

        try
        {
            if (needsPreparation)
            {
                await RunPausableRuntimePhaseAsync(
                    runtimeController,
                    cancellationToken => _runtimeService.PrepareAsync(
                        instance, _preferences, progress, status, cancellationToken));
                HideRuntimeOperationControls();
                RuntimePreparationControls.IsVisible = false;
                await RefreshInstancesAsync();
            }

            var session = await _accountService.GetValidSessionAsync();
            if (session is null)
            {
                throw new InvalidOperationException(
                    "La instalación está preparada, pero no hay una sesión válida. " +
                    "Inicia sesión con Microsoft o activa un perfil sin conexión en Ajustes.");
            }

            GameStatus.Text = "Preparando resource packs y skin del perfil…";
            bool launchAssetsReady = await PrepareLaunchAssetsAsync(instance);
            if (!launchAssetsReady)
                return;

            GameStatus.Text = $"Iniciando Minecraft con la cuenta {_accountService.Username}…";
            Process process = await _runtimeService.LaunchAsync(instance, _preferences, session);
            _runningGameProcess = process;
            _runningGameInstanceId = instance.Id;

            // Conectar el evento de salida antes de esperar escrituras de preferencias/estado.
            // Minecraft puede cerrarse muy rápido; no se debe perder el evento ni dejar el botón atascado.
            HomePlayButton.Content = "CERRAR";
            HomePlayButton.IsEnabled = true;
            bool processExitHandled = false;
            void HandleGameExited(object? exitSender, EventArgs exitArgs)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!ReferenceEquals(_runningGameProcess, process))
                        return;

                    processExitHandled = true;
                    _runningGameProcess = null;
                    _runningGameInstanceId = null;
                    RefreshDiscordPresence();
                    HomePlayButton.Content = "JUGAR";
                    HomePlayButton.IsEnabled = _selectedInstance is not null || GetLastPlayedInstance() is not null;
                    GameStatus.Text = "Minecraft se ha cerrado.";
                    HeaderStatus.Text = "Minecraft cerrado";
                    try { process.Dispose(); } catch { }
                });
            }

            // No depender exclusivamente del evento: un proceso muy corto puede cerrarse
            // justo antes de que Avalonia termine de enlazar sus controles.
            process.Exited += HandleGameExited;
            process.EnableRaisingEvents = true;
            if (process.HasExited)
                HandleGameExited(process, EventArgs.Empty);

            _lastPlayedInstanceId = instance.Id;
            try
            {
                await _launcherStateService.SetLastPlayedInstanceIdAsync(instance.Id);
            }
            catch (Exception stateException)
            {
                LauncherDiagnostics.WriteException("No se pudo guardar la última instancia jugada.", stateException);
                HeaderStatus.Text = "Minecraft se inició, pero no se pudo guardar la última instancia jugada: " + stateException.Message;
            }

            // Guardar la última instancia implica un await: Minecraft puede morir mientras
            // la interfaz está fuera del hilo principal. No abrir la consola sobre un Process
            // que el manejador de salida ya haya cerrado.
            if (processExitHandled)
            {
                GameStatus.Text = "Minecraft se cerró durante el inicio.";
                return;
            }

            bool processSurvivedStateSave;
            try { processSurvivedStateSave = !process.HasExited; }
            catch { processSurvivedStateSave = false; }

            if (!processSurvivedStateSave)
            {
                HandleGameExited(process, EventArgs.Empty);
                GameStatus.Text = "Minecraft se cerró durante el inicio.";
                return;
            }

            RefreshDiscordPresence();
            if (_preferences.ShowGameConsole)
            {
                _gameConsoleWindow = new GameConsoleWindow(process, instance.Name);
                _gameConsoleWindow.Show();
            }
            else
            {
                // Aunque la consola no se muestre, se drena la salida para que Minecraft no se bloquee.
                process.OutputDataReceived += (_, _) => { };
                process.ErrorDataReceived += (_, _) => { };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            GameProgress.Value = 100;
            bool processStillRunning;
            try { processStillRunning = !process.HasExited; }
            catch { processStillRunning = false; }

            if (processStillRunning)
            {
                GameStatus.Text = $"Minecraft se inició correctamente (PID {process.Id}).";
                if (_preferences.CloseLauncherOnGameStart)
                    Close();
            }
            else
            {
                HandleGameExited(process, EventArgs.Empty);
                GameStatus.Text = "Minecraft se cerró durante el inicio.";
            }
        }
        catch (OperationCanceledException)
        {
            GameStatus.Text = runtimeController.IsStopped
                ? "La preparación se detuvo."
                : "La preparación se canceló.";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("No se pudo iniciar Minecraft desde la página principal.", ex);
            GameStatus.Text = "No se pudo iniciar el juego: " + ex.Message;
        }
        finally
        {
            button.IsEnabled = true;
            GameProgress.IsVisible = false;
            RuntimePreparationControls.IsVisible = false;
            EndRuntimeOperation(runtimeController);
            await RefreshInstancesAsync();
        }
    }

    private async void DeleteInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        await DeleteInstanceByIdAsync(instance);
    }

    private async void OpenInstanceOptions_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        _selectedInstance = _instances.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        UpdateSelectedInstanceUi();
        await RememberSelectedInstanceAsync(instance);

        string? action = await ShowInstanceOptionsDialogAsync(instance);
        if (string.IsNullOrWhiteSpace(action))
            return;

        if (action == "delete")
        {
            await DeleteInstanceByIdAsync(instance);
            return;
        }

        await RunInstanceMaintenanceAsync(instance, action == "verify");
    }

    private async Task<string?> ShowInstanceOptionsDialogAsync(InstalledInstance instance)
    {
        var dialog = new Window
        {
            Title = $"Opciones de {instance.Name}",
            Width = 450,
            Height = 390,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = Avalonia.Media.Brushes.White,
            SystemDecorations = SystemDecorations.None
        };

        Button MakeOption(string label, string action, string background = "#252D36")
        {
            var option = new Button
            {
                Content = label,
                Height = 46,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Avalonia.Thickness(18, 0, 12, 0),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(background)),
                Foreground = Avalonia.Media.Brushes.White,
                BorderThickness = new Avalonia.Thickness(0),
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            };
            option.Click += (_, _) => dialog.Close(action);
            return option;
        }

        var content = new StackPanel { Margin = new Avalonia.Thickness(24, 22), Spacing = 9 };
        content.Children.Add(new TextBlock
        {
            Text = "MANTENIMIENTO",
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#7D8792")),
            FontSize = 11,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 2)
        });
        content.Children.Add(MakeOption("BUSCAR ACTUALIZACIONES", "updates"));
        content.Children.Add(MakeOption("VERIFICAR INTEGRIDAD DE LOS ARCHIVOS", "verify"));
        content.Children.Add(new TextBlock
        {
            Text = "La verificación reinstala los archivos oficiales del modpack y de Minecraft, pero conserva los archivos extra que hayas añadido.",
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#7E8994")),
            FontSize = 11,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(4, 0, 4, 12)
        });
        content.Children.Add(new TextBlock
        {
            Text = "ZONA DE PELIGRO",
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A97373")),
            FontSize = 11,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 0, 0, 2)
        });
        content.Children.Add(MakeOption("ELIMINAR INSTALACIÓN", "delete", "#743737"));

        var root = new DockPanel();
        var titleBar = new Grid
        {
            Height = 50,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#20262D")),
        };
        titleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(titleBar).Properties.IsLeftButtonPressed)
                dialog.BeginMoveDrag(e);
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        titleBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var title = new TextBlock
        {
            Text = $"Opciones de {instance.Name}",
            Foreground = Avalonia.Media.Brushes.White,
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(18, 0, 0, 0)
        };
        var close = new Button
        {
            Content = "×",
            Width = 44,
            Height = 44,
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = Avalonia.Media.Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            FontSize = 18
        };
        close.Click += (_, _) => dialog.Close(null);
        Grid.SetColumn(close, 1);
        titleBar.Children.Add(title);
        titleBar.Children.Add(close);
        DockPanel.SetDock(titleBar, Dock.Top);
        root.Children.Add(titleBar);
        root.Children.Add(content);
        dialog.Content = new Border
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#12171D")),
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#38414A")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(10),
            Child = root
        };

        return await dialog.ShowDialog<string?>(this);
    }

    private async Task RunInstanceMaintenanceAsync(InstalledInstance instance, bool verifyIntegrity)
    {
        string code = instance.InstallCode?.Trim() ?? string.Empty;
        string catalogId = _preferences.ModpackCatalogFileId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(catalogId))
        {
            await ShowInformationAsync(
                "No se puede realizar el mantenimiento",
                "Falta el código de instalación o el ID del catálogo en Ajustes.");
            return;
        }

        OpenPage("Modpacks");
        if (_activeDownloadController is not null || _runtimeOperationController is not null)
        {
            ShowModpackMessage("Termina la operación o preparación activa antes de iniciar el mantenimiento.", false);
            return;
        }

        if (_runningGameProcess is not null &&
            string.Equals(_runningGameInstanceId, instance.Id, StringComparison.OrdinalIgnoreCase))
        {
            ShowModpackMessage("Cierra esta instancia de Minecraft antes de actualizarla o verificarla.", false);
            return;
        }

        var controller = new DownloadOperationController();
        _activeDownloadController = controller;
        InstallModpackButton.IsEnabled = false;
        InstallProgress.Value = 0;
        InstallProgress.IsVisible = true;
        ModpackOperationControls.IsVisible = true;
        PauseResumeModpackButton.Content = "Pausar";

        try
        {
            var drive = new GoogleDriveService();
            var reader = new ModpackCatalogReader();
            ShowModpackMessage(verifyIntegrity
                ? $"Preparando la verificación de «{instance.Name}»…"
                : $"Buscando actualizaciones para «{instance.Name}»…", true);

            ModpackManifest? manifest = await reader.FindByCodeAsync(
                code,
                catalogId,
                async (fileId, cancellationToken) =>
                {
                    using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, controller.StopToken);
                    return await drive.DownloadTextFileAsync(fileId, linked.Token);
                });

            controller.ThrowIfStopped();
            if (manifest is null)
                throw new InvalidOperationException("No se encontró el modpack en el catálogo. Comprueba el código y la configuración del catálogo.");

            if (!string.Equals(manifest.Id, instance.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El código del catálogo ahora apunta a otra instalación. Se canceló la operación para evitar modificar una instancia distinta.");

            var installer = new ModpackInstallerService(drive, _instanceService);
            var progress = new Progress<double>(value =>
            {
                InstallProgress.Value = Math.Clamp(value, 0, 100);
                ModpackMessage.Text = (verifyIntegrity ? "Verificando archivos… " : "Buscando/instalando actualización… ") + $"{InstallProgress.Value:0}%";
                ModpackMessage.IsVisible = true;
            });

            InstalledInstance updatedInstance = verifyIntegrity
                ? await installer.VerifyIntegrityAsync(manifest, code, instance, progress, controller)
                : await installer.InstallOrUpdateAsync(manifest, code, instance, progress, controller);

            // Al verificar también se vuelve a comprobar la instalación oficial de Minecraft y del loader.
            if (verifyIntegrity)
            {
                ShowModpackMessage("Archivos del modpack restaurados. Comprobando Minecraft y el loader…", true);
                await RunPausableRuntimePhaseAsync(
                    controller,
                    cancellationToken => _runtimeService.PrepareAsync(
                        updatedInstance,
                        _preferences,
                        progress,
                        new Progress<string>(message => ShowModpackMessage(message, true)),
                        cancellationToken));
            }
            else
            {
                // Una actualización puede cambiar mods/configuración; se vuelve a preparar al jugar.
                updatedInstance.RuntimePrepared = false;
                updatedInstance.LaunchVersionName = string.Empty;
                await _instanceService.SaveAsync(updatedInstance);
            }

            _selectedInstance = updatedInstance;
            await RememberSelectedInstanceAsync(updatedInstance);
            await RefreshInstancesAsync();
            await RefreshStorageUsageAsync();

            ShowModpackMessage(verifyIntegrity
                ? $"Se verificaron y restauraron los archivos administrados de «{instance.Name}»."
                : $"Se comprobó y actualizó «{instance.Name}» correctamente.", true);
            HeaderStatus.Text = verifyIntegrity ? "Integridad verificada" : "Actualización comprobada";
        }
        catch (OperationCanceledException)
        {
            ShowModpackMessage(controller.IsStopped ? "Operación detenida." : "La operación se canceló.", false);
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("RunInstanceMaintenanceAsync: falló una operación.", ex);
            ShowModpackMessage("No se pudo completar la operación: " + ex.Message, false);
        }
        finally
        {
            controller.Dispose();
            _activeDownloadController = null;
            ModpackOperationControls.IsVisible = false;
            InstallModpackButton.IsEnabled = true;
            InstallProgress.IsVisible = false;
        }
    }

    private async Task DeleteInstanceByIdAsync(InstalledInstance instance)
    {
        if (_activeDownloadController is not null)
        {
            await ShowInformationAsync(
                "No se puede eliminar la instalación",
                "Espera a que termine la operación activa antes de eliminar una instalación.");
            return;
        }

        if (_runtimeOperationController is not null)
        {
            await ShowInformationAsync(
                "No se puede eliminar la instalación",
                "Espera a que termine la preparación de Minecraft antes de eliminar una instalación.");
            return;
        }

        if (_runningGameProcess is not null &&
            string.Equals(_runningGameInstanceId, instance.Id, StringComparison.OrdinalIgnoreCase))
        {
            await ShowInformationAsync(
                "No se puede eliminar la instalación",
                "Cierra Minecraft antes de eliminar esta instalación.");
            return;
        }

        if (!await ConfirmDeleteInstanceAsync(instance))
            return;

        try
        {
            await _instanceService.DeleteInstanceAsync(instance.Id);
            // Mantiene la semántica de Windows: al eliminar una instalación también se
            // eliminan sus archivos de capturas archivados. La limpieza valida las rutas
            // y no permite que metadatos externos borren archivos ajenos al launcher.
            new ScreenshotArchiveStore(
                Path.Combine(LauncherPaths.DefaultLauncherRoot, "archived-screenshots"))
                .DeleteArchivesForInstance(instance.Id);

            // El original también elimina la caché de iconos/fondos de esta instalación.
            string instanceImageCache = Path.Combine(
                InstanceService.LauncherRoot, "cache", instance.Id);
            if (Directory.Exists(instanceImageCache))
            {
                try
                {
                    await Task.Run(() => Directory.Delete(instanceImageCache, recursive: true));
                }
                catch
                {
                    // Un archivo de caché bloqueado no debe deshacer la eliminación de la instancia.
                }
            }

            if (string.Equals(_lastPlayedInstanceId, instance.Id, StringComparison.OrdinalIgnoreCase))
            {
                _lastPlayedInstanceId = null;
                try
                {
                    await _launcherStateService.ClearLastPlayedInstanceAsync();
                }
                catch (Exception stateException)
                {
                    LauncherDiagnostics.WriteException("No se pudo actualizar el historial tras eliminar la instancia.", stateException);
                    HeaderStatus.Text = "La instancia se eliminó, pero no se pudo actualizar el historial: " + stateException.Message;
                }
            }
            await RefreshInstancesAsync();
            await RefreshStorageUsageAsync();
            HeaderStatus.Text = "Instancia eliminada";
            InstancesMessage.Text = $"Se eliminó la instancia «{instance.Name}» y sus archivos locales.";
            InstancesMessage.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A6E3B1"));
            InstancesMessage.IsVisible = true;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("DeleteInstanceByIdAsync: falló una operación.", ex);
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

    private async void BrowseOfflineSkin_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Seleccionar skin PNG",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Imágenes PNG") { Patterns = new[] { "*.png" } }
                }
            });

            if (files.Count == 0)
                return;

            _offlineSkinPath = files[0].Path.LocalPath;
            OfflineSkinPathLabel.Text = _offlineSkinPath;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("BrowseOfflineSkin_Click: falló una operación.", ex);
            OfflineProfileStatus.Text = "No se pudo seleccionar la skin: " + ex.Message;
            OfflineProfileStatus.IsVisible = true;
        }
    }

    private async void RemoveOfflineSkin_Click(object? sender, RoutedEventArgs e)
    {
        _offlineSkinPath = null;
        OfflineSkinPathLabel.Text = "Se quitará la skin personalizada al guardar.";
        string username = OfflineUsernameInput.Text?.Trim() ?? string.Empty;
        if (!OfflineAccountService.IsValidUsername(username))
        {
            OfflineProfileStatus.Text = "Selecciona un nombre de 3 a 16 caracteres ASCII y guarda el perfil para aplicar el cambio.";
            OfflineProfileStatus.IsVisible = true;
            return;
        }

        try
        {
            string skinModel = (OfflineSkinModelComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "wide";
            await _offlineAccountService.SaveAsync(username, null, skinModel, removeSkin: true);
            OfflineSkinPathLabel.Text = "Skin personalizada eliminada.";
            OfflineProfileStatus.Text = "Se quitó la skin personalizada del perfil local.";
            OfflineProfileStatus.IsVisible = true;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("RemoveOfflineSkin_Click: falló una operación.", ex);
            OfflineProfileStatus.Text = "No se pudo quitar la skin: " + ex.Message;
            OfflineProfileStatus.IsVisible = true;
        }
    }

    private async void BrowseJava_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Seleccionar el ejecutable de Java",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Java") { Patterns = new[] { "java", "java.exe" } },
                    new FilePickerFileType("Todos los archivos") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
                JavaPathInput.Text = files[0].Path.LocalPath;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("BrowseJava_Click: falló una operación.", ex);
            JavaStatus.Text = "No se pudo abrir el selector: " + ex.Message;
        }
    }

    private async void BrowseStorage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Seleccionar carpeta de instalaciones",
                AllowMultiple = false
            });

            if (folders.Count > 0)
                StoragePathInput.Text = folders[0].Path.LocalPath;
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("BrowseStorage_Click: falló una operación.", ex);
            ShowSettingsMessage("No se pudo abrir el selector de carpetas: " + ex.Message, false);
        }
    }

    private async void DeveloperModeInput_Changed(object? sender, RoutedEventArgs e)
    {
        if (_fillingSettings || _handlingDeveloperModeToggle)
            return;

        bool requestedEnabled = DeveloperModeInput.IsChecked == true;
        _handlingDeveloperModeToggle = true;
        DeveloperModeInput.IsChecked = _preferences.DeveloperMode;
        _handlingDeveloperModeToggle = false;

        if (requestedEnabled == _preferences.DeveloperMode)
            return;

        if (requestedEnabled)
        {
            string expectedPassword = Environment.GetEnvironmentVariable("NEGATIVE_LAUNCHER_DEV_PASSWORD") ?? string.Empty;
            if (string.IsNullOrEmpty(expectedPassword))
            {
                ShowSettingsMessage(
                    "El modo desarrollador está protegido. Configura la variable de entorno NEGATIVE_LAUNCHER_DEV_PASSWORD y reinicia el launcher para habilitarlo.",
                    false);
                return;
            }

            if (!await AuthenticateDeveloperModeAsync(expectedPassword))
            {
                ShowSettingsMessage("No se habilitó el modo desarrollador.", false);
                return;
            }
        }

        try
        {
            _preferences.DeveloperMode = requestedEnabled;
            _developerModeEnabled = requestedEnabled;
            DeveloperNav.IsVisible = requestedEnabled;
            if (!requestedEnabled && DeveloperPage.IsVisible)
                OpenPage("Inicio");

            await _preferencesService.SaveAsync(_preferences);
            ShowSettingsMessage(
                requestedEnabled ? "Modo desarrollador habilitado." : "Modo desarrollador deshabilitado.",
                true);
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("DeveloperModeInput_Changed: falló una operación.", ex);
            _preferences.DeveloperMode = !requestedEnabled;
            _developerModeEnabled = _preferences.DeveloperMode;
            DeveloperNav.IsVisible = _developerModeEnabled;
            _handlingDeveloperModeToggle = true;
            DeveloperModeInput.IsChecked = _preferences.DeveloperMode;
            _handlingDeveloperModeToggle = false;
            ShowSettingsMessage("No se pudo cambiar el modo desarrollador: " + ex.Message, false);
        }
    }

    private async Task<bool> AuthenticateDeveloperModeAsync(string expectedPassword)
    {
        string expectedUsername = Environment.GetEnvironmentVariable("NEGATIVE_LAUNCHER_DEV_USERNAME") ?? "admin";
        var dialog = new Window
        {
            Title = "Modo desarrollador",
            Width = 400,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#12171D")),
            Foreground = Avalonia.Media.Brushes.White,
            SystemDecorations = SystemDecorations.None
        };

        var username = new TextBox
        {
            Watermark = "Nombre",
            Text = string.Empty,
            Height = 38,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1C232A")),
            Foreground = Avalonia.Media.Brushes.White
        };
        var password = new TextBox
        {
            Watermark = "Contraseña",
            PasswordChar = '●',
            Height = 38,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1C232A")),
            Foreground = Avalonia.Media.Brushes.White
        };
        var cancel = new Button { Content = "Cancelar", Padding = new Avalonia.Thickness(14, 8) };
        var login = new Button
        {
            Content = "Entrar",
            Padding = new Avalonia.Thickness(14, 8),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#287A8D")),
            Foreground = Avalonia.Media.Brushes.White
        };
        cancel.Click += (_, _) => dialog.Close(false);
        login.Click += (_, _) => dialog.Close(true);

        var fields = new StackPanel { Spacing = 8 };
        fields.Children.Add(new TextBlock { Text = "Nombre", FontSize = 12 });
        fields.Children.Add(username);
        fields.Children.Add(new TextBlock { Text = "Contraseña", FontSize = 12, Margin = new Avalonia.Thickness(0, 8, 0, 0) });
        fields.Children.Add(password);
        fields.Children.Add(new TextBlock
        {
            Text = "La contraseña se obtiene de NEGATIVE_LAUNCHER_DEV_PASSWORD; no está guardada en el código.",
            FontSize = 10,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5")),
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        });
        fields.Children.Add(new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
            Children = { cancel, login }
        });
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 4, Children = { fields } };

        bool accepted = await dialog.ShowDialog<bool>(this);
        return accepted &&
            string.Equals(username.Text?.Trim(), expectedUsername, StringComparison.Ordinal) &&
            string.Equals(password.Text, expectedPassword, StringComparison.Ordinal);
    }

    private void RamSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (RamValueText is not null)
            RamValueText.Text = $"{(int)Math.Round(e.NewValue)} MB";
        ScheduleSettingsAutoSave();
    }

    private void SettingsAutoSave_Changed(object? sender, RoutedEventArgs e)
        => ScheduleSettingsAutoSave();

    private void SettingsAutoSave_Changed(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        => ScheduleSettingsAutoSave();

    private void ScheduleSettingsAutoSave()
    {
        if (_fillingSettings || !IsLoaded)
            return;

        _settingsSaveCancellation?.Cancel();
        _settingsSaveCancellation?.Dispose();
        var cancellation = new System.Threading.CancellationTokenSource();
        _settingsHaveUnsavedChanges = true;
        _settingsSaveCancellation = cancellation;
        _settingsSaveTask = PersistSettingsAfterDelayAsync(cancellation);
    }

    private async Task PersistSettingsAfterDelayAsync(System.Threading.CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(650, cancellation.Token);
            if (cancellation.IsCancellationRequested || _fillingSettings)
                return;

            _preferences.MaximumRamMb = Math.Clamp((int)Math.Round(RamSlider.Value), 1024, 32768);
            _preferences.UseAutomaticJava = AutomaticJavaInput.IsChecked == true;
            _preferences.EnableCustomJavaArguments = EnableCustomJavaArgumentsInput.IsChecked == true;
            _preferences.CustomJavaArguments = CustomJavaArgumentsInput.Text?.Trim() ?? string.Empty;
            _preferences.EnableHolidayLauncherThemes = HolidayThemesInput.IsChecked == true;
            _preferences.CustomJavaPath = JavaPathInput.Text?.Trim() ?? string.Empty;
            _preferences.CloseLauncherOnGameStart = CloseLauncherInput.IsChecked == true;
            _preferences.ShowGameConsole = ShowConsoleInput.IsChecked == true;
            _preferences.ModpackCatalogFileId = CatalogFileIdInput.Text?.Trim() ?? string.Empty;

            SettingsMessage.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#8D99A5"));
            SettingsMessage.Text = "Guardando cambios automáticamente…";
            SettingsMessage.IsVisible = true;
            await _preferencesService.SaveAsync(_preferences);
            if (!cancellation.IsCancellationRequested)
            {
                if (ReferenceEquals(_settingsSaveCancellation, cancellation))
                    _settingsHaveUnsavedChanges = false;
                ApplySeasonalTheme(force: true);
                HomeRam.Text = $"{_preferences.MaximumRamMb} MB";
                ShowSettingsMessage("Cambios guardados automáticamente.", true);
            }
        }
        catch (OperationCanceledException)
        {
            // Se escribió otro cambio antes de terminar la espera.
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("PersistSettingsAfterDelayAsync: falló una operación.", ex);
            if (!cancellation.IsCancellationRequested)
                ShowSettingsMessage("No se pudieron guardar los cambios automáticamente: " + ex.Message, false);
        }
        finally
        {
            if (ReferenceEquals(_settingsSaveCancellation, cancellation))
            {
                _settingsSaveCancellation = null;
                cancellation.Dispose();
            }
        }
    }

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

            // La prueba debe usar las mismas reglas de permisos que el lanzamiento real.
            JavaExecutableHelper.EnsureExecutable(javaPath);

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
            LauncherDiagnostics.WriteException("CheckJava_Click: falló una operación.", ex);
            JavaStatus.Text = "No se pudo comprobar Java: " + ex.Message;
            JavaStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
        }
    }

    private async void SaveSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            int ram = (int)Math.Round(RamSlider.Value);

            string newRoot = string.IsNullOrWhiteSpace(StoragePathInput.Text)
                ? InstanceService.DefaultStorageRoot
                : Path.GetFullPath(StoragePathInput.Text.Trim());

            string currentRoot = _instanceService.GetStorageRoot();
            if (!PathsEqual(currentRoot, newRoot) && IsStorageMaintenanceBlocked())
            {
                ShowSettingsMessage(
                    "Cierra Minecraft y termina las operaciones activas antes de cambiar la carpeta de instalaciones.",
                    false);
                return;
            }

            if (!PathsEqual(currentRoot, newRoot))
                await _instanceService.ChangeStorageRootAsync(newRoot);

            _preferences.MaximumRamMb = Math.Clamp(ram, 1024, 32768);
            _preferences.UseAutomaticJava = AutomaticJavaInput.IsChecked == true;
            _preferences.EnableCustomJavaArguments = EnableCustomJavaArgumentsInput.IsChecked == true;
            _preferences.CustomJavaArguments = CustomJavaArgumentsInput.Text?.Trim() ?? string.Empty;
            _preferences.EnableHolidayLauncherThemes = HolidayThemesInput.IsChecked == true;
            _preferences.DeveloperMode = DeveloperModeInput.IsChecked == true;
            _developerModeEnabled = _preferences.DeveloperMode;
            DeveloperNav.IsVisible = _developerModeEnabled;
            if (!_developerModeEnabled && DeveloperPage.IsVisible) OpenPage("Inicio");
            _preferences.CustomJavaPath = JavaPathInput.Text?.Trim() ?? string.Empty;
            _preferences.CloseLauncherOnGameStart = CloseLauncherInput.IsChecked == true;
            _preferences.ShowGameConsole = ShowConsoleInput.IsChecked == true;
            _preferences.ModpackCatalogFileId = CatalogFileIdInput.Text?.Trim() ?? string.Empty;
            _preferences.StorageRootPath = PathsEqual(newRoot, InstanceService.DefaultStorageRoot)
                ? string.Empty
                : newRoot;

            await _preferencesService.SaveAsync(_preferences);
            ApplySeasonalTheme(force: true);
            FillSettings();
            await RefreshInstancesAsync();
            HomeRam.Text = $"{_preferences.MaximumRamMb} MB";
            ShowSettingsMessage("Ajustes guardados correctamente.", true);
            HeaderStatus.Text = "Ajustes guardados";
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("SaveSettings_Click: falló una operación.", ex);
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

    private async Task<string?> ShowAddModpackDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Instalar modpack",
            Width = 460,
            Height = 260,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = Avalonia.Media.Brushes.White,
            SystemDecorations = SystemDecorations.None
        };

        var codeInput = new TextBox
        {
            Height = 43,
            Padding = new Avalonia.Thickness(12, 9),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#20262D")),
            Foreground = Avalonia.Media.Brushes.White,
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3A434C")),
            FontSize = 16,
            MaxLength = 32,
            Watermark = "Código de instalación"
        };
        codeInput.TextChanged += (_, _) =>
        {
            string current = codeInput.Text ?? string.Empty;
            string normalized = current.ToUpperInvariant();
            if (!string.Equals(current, normalized, StringComparison.Ordinal))
            {
                int caret = codeInput.CaretIndex;
                codeInput.Text = normalized;
                codeInput.CaretIndex = Math.Min(caret, normalized.Length);
            }
        };

        var error = new TextBlock
        {
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E57373")),
            FontSize = 12,
            Margin = new Avalonia.Thickness(2, 7, 0, 0)
        };

        var install = new Button
        {
            Content = "INSTALAR",
            Width = 105,
            Height = 38,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#38899A")),
            Foreground = Avalonia.Media.Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };
        var cancel = new Button
        {
            Content = "CANCELAR",
            Width = 105,
            Height = 38,
            Margin = new Avalonia.Thickness(0, 0, 10, 0),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#252C34")),
            Foreground = Avalonia.Media.Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };

        void TryAccept()
        {
            string code = (codeInput.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                error.Text = "Introduce un código de instalación.";
                codeInput.Focus();
                return;
            }
            if (code.Length < 4)
            {
                error.Text = "El código introducido es demasiado corto.";
                codeInput.Focus();
                return;
            }
            dialog.Close(code);
        }

        install.Click += (_, _) => TryAccept();
        codeInput.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
                TryAccept();
        };
        cancel.Click += (_, _) => dialog.Close(null);

        var titleBar = new Grid
        {
            Height = 50,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1D2228")),
        };
        titleBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(titleBar).Properties.IsLeftButtonPressed)
                dialog.BeginMoveDrag(e);
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        titleBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Parse("42")));
        titleBar.Children.Add(new TextBlock
        {
            Text = "Instalar modpack",
            Foreground = Avalonia.Media.Brushes.White,
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(18, 0, 0, 0)
        });
        var close = new Button
        {
            Content = "×",
            Width = 42,
            Height = 42,
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC")),
            BorderThickness = new Avalonia.Thickness(0),
            FontSize = 18
        };
        close.Click += (_, _) => dialog.Close(null);
        Grid.SetColumn(close, 1);
        titleBar.Children.Add(close);

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Children = { cancel, install }
        };
        var body = new Grid { Margin = new Avalonia.Thickness(28, 22, 28, 24) };
        body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        body.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        body.Children.Add(new TextBlock
        {
            Text = "Código de instalación",
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#EAEAEA")),
            FontSize = 14,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        Grid.SetRow(codeInput, 1);
        codeInput.Margin = new Avalonia.Thickness(0, 10, 0, 0);
        body.Children.Add(codeInput);
        Grid.SetRow(error, 2);
        body.Children.Add(error);
        Grid.SetRow(buttons, 3);
        body.Children.Add(buttons);

        var root = new DockPanel();
        DockPanel.SetDock(titleBar, Dock.Top);
        root.Children.Add(titleBar);
        root.Children.Add(body);
        dialog.Content = new Border
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#14181D")),
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#30363D")),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(10),
            Child = root
        };
        dialog.Opened += (_, _) => codeInput.Focus();

        return await dialog.ShowDialog<string?>(this);
    }

    private async void InstallModpack_Click(object? sender, RoutedEventArgs e)
    {
        string code = InstallCodeInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            code = await ShowAddModpackDialogAsync() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
                return;
            InstallCodeInput.Text = code;
        }

        string catalogId = _preferences.ModpackCatalogFileId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            ShowModpackMessage("Primero guarda el ID del catálogo en Ajustes.", false);
            return;
        }

        if (_activeDownloadController is not null)
        {
            ShowModpackMessage("Ya hay una operación de modpack en curso.", false);
            return;
        }

        var controller = new DownloadOperationController();
        InstalledInstance? workingInstance = null;
        _activeDownloadController = controller;
        InstallModpackButton.IsEnabled = false;
        InstallProgress.Value = 0;
        InstallProgress.IsVisible = true;
        ModpackOperationControls.IsVisible = true;
        PauseResumeModpackButton.Content = "Pausar";

        try
        {
            var drive = new GoogleDriveService();
            var reader = new ModpackCatalogReader();
            ShowModpackMessage("Buscando el modpack en el catálogo…", true);

            ModpackManifest? manifest = await reader.FindByCodeAsync(
                code,
                catalogId,
                async (fileId, cancellationToken) =>
                {
                    using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, controller.StopToken);
                    return await drive.DownloadTextFileAsync(fileId, linked.Token);
                });

            controller.ThrowIfStopped();
            if (manifest is null)
            {
                ShowModpackMessage("No se encontró ningún modpack con ese código.", false);
                return;
            }

            var existing = (await _instanceService.LoadAllAsync())
                .FirstOrDefault(instance => string.Equals(instance.Id, manifest.Id, StringComparison.OrdinalIgnoreCase));

            var installer = new ModpackInstallerService(drive, _instanceService);
            var modpackProgress = new Progress<double>(value =>
            {
                double safeValue = double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
                InstallProgress.Value = safeValue * 0.6;
                ModpackMessage.Text = $"Instalando {manifest.Name}… {safeValue:0}%";
                ModpackMessage.IsVisible = true;
            });

            workingInstance = await installer.InstallOrUpdateAsync(
                manifest, code, existing, modpackProgress, controller);
            controller.ThrowIfStopped();

            // Un cambio del modpack invalida el estado de runtime anterior.
            workingInstance.RuntimePrepared = false;
            workingInstance.LaunchVersionName = string.Empty;
            await _instanceService.SaveAsync(workingInstance);
            _selectedInstance = workingInstance;
            await RememberSelectedInstanceAsync(workingInstance);

            string runtimeStage = "Preparando Minecraft…";
            var runtimeProgress = new Progress<double>(value =>
            {
                double safeValue = double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
                InstallProgress.Value = 60 + safeValue * 0.4;
                ModpackMessage.Text = $"{runtimeStage} {safeValue:0}%";
                ModpackMessage.IsVisible = true;
            });
            var runtimeStatus = new Progress<string>(message =>
            {
                if (string.IsNullOrWhiteSpace(message))
                    return;

                runtimeStage = message;
                ModpackMessage.Text = runtimeStage;
                ModpackMessage.IsVisible = true;
            });

            ShowModpackMessage("Preparando Minecraft, Java y el cargador…", true);
            string launchVersionName = await RunPausableRuntimePhaseAsync(
                controller,
                cancellationToken => _runtimeService.PrepareAsync(
                    workingInstance, _preferences, runtimeProgress, runtimeStatus, cancellationToken));

            controller.ThrowIfStopped();
            workingInstance.RuntimePrepared = true;
            workingInstance.LaunchVersionName = launchVersionName;
            await _instanceService.SaveAsync(workingInstance);

            _selectedInstance = workingInstance;
            await RememberSelectedInstanceAsync(workingInstance);
            await RefreshInstancesAsync();
            await RefreshStorageUsageAsync();
            InstallProgress.Value = 100;
            ShowModpackMessage($"'{manifest.Name}' está instalado y listo para jugar.", true);
            HeaderStatus.Text = "Modpack preparado";
        }
        catch (OperationCanceledException)
        {
            if (workingInstance is not null)
            {
                try
                {
                    await RefreshInstancesAsync();
                    await RefreshStorageUsageAsync();
                }
                catch { }
            }

            ShowModpackMessage(
                controller.IsStopped ? "Operación detenida. Si el modpack ya se descargó, puedes continuar su preparación con JUGAR." : "La operación se canceló.",
                false);
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException("InstallModpack_Click: falló una operación.", ex);
            if (workingInstance is not null)
            {
                try
                {
                    await RefreshInstancesAsync();
                    await RefreshStorageUsageAsync();
                }
                catch { }
            }

            ShowModpackMessage("No se pudo completar la instalación o preparación: " + ex.Message, false);
        }
        finally
        {
            controller.Dispose();
            _activeDownloadController = null;
            ModpackOperationControls.IsVisible = false;
            InstallModpackButton.IsEnabled = true;
            InstallProgress.IsVisible = false;
        }
    }

    private static async Task<T> RunPausableRuntimePhaseAsync<T>(
        DownloadOperationController controller,
        Func<System.Threading.CancellationToken, Task<T>> phase)
    {
        while (true)
        {
            await controller.WaitWhilePausedAsync();
            controller.ThrowIfStopped();
            System.Threading.CancellationToken cancellationToken = controller.BeginPhase();

            try
            {
                return await phase(cancellationToken);
            }
            catch (OperationCanceledException) when (controller.IsPaused && !controller.IsStopped)
            {
                // Al reanudar, el runtime compartido vuelve a comprobar los archivos ya descargados.
            }
            finally
            {
                controller.EndPhase();
            }
        }
    }

    private void PauseResumeModpack_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeDownloadController is null)
            return;

        if (_activeDownloadController.IsPaused)
        {
            _activeDownloadController.Resume();
            PauseResumeModpackButton.Content = "Pausar";
            ShowModpackMessage("Reanudando la operación…", true);
        }
        else
        {
            _activeDownloadController.Pause();
            PauseResumeModpackButton.Content = "Reanudar";
            ShowModpackMessage("Operación pausada.", true);
        }
    }

    private void StopModpack_Click(object? sender, RoutedEventArgs e)
    {
        _activeDownloadController?.Stop();
        ShowModpackMessage("Deteniendo la operación…", false);
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
        => LauncherPathRules.PathsEqual(left, right);
}
