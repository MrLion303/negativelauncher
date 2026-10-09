using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    private readonly MicrosoftAccountService _accountService = MicrosoftAccountService.Instance;
    private readonly ObservableCollection<MicrosoftAccountInfo> _accounts = new();
    private readonly ObservableCollection<InstalledInstance> _instances = new();
    private InstalledInstance? _selectedInstance;
    private GameConsoleWindow? _gameConsoleWindow;
    private Process? _runningGameProcess;
    private LauncherPreferences _preferences = new();
    private string? _offlineSkinPath;
    private readonly System.Collections.Generic.List<GalleryEntry> _galleryItems = new();
    private readonly System.Collections.Generic.HashSet<string> _gallerySelection = new(StringComparer.OrdinalIgnoreCase);
    private bool _gallerySelectionMode;
    private string _galleryInstanceFilter = string.Empty;
    private string _gallerySortMode = "newest";
    private int _galleryViewerIndex = -1;
    private DownloadOperationController? _activeDownloadController;
    private System.Threading.CancellationTokenSource? _runtimePreparationCancellation;

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
        var accounts = _accountService.GetPremiumAccounts();
        var activeName = _accountService.Username;
        QuickAccountButton.Content = string.IsNullOrWhiteSpace(activeName)
            ? "Cuenta ▾"
            : activeName + " ▾";

        foreach (var account in accounts)
        {
            var button = new Button
            {
                Tag = account.Identifier,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Padding = new Avalonia.Thickness(10, 8),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(account.IsSelected ? "#26333A" : "#181E25")),
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E6E9EC")),
                Content = (account.IsSelected ? "✓  " : "    ") + account.Username
            };
            button.Click += QuickAccountEntry_Click;
            QuickAccountsListPanel.Children.Add(button);
        }

        QuickAddAccountButton.IsEnabled = accounts.Count < MicrosoftAccountService.MaxAccounts;
        QuickAddAccountButton.Content = accounts.Count < MicrosoftAccountService.MaxAccounts
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

    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.Source is Button)
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void ToggleWindowSize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
        => Close();

    public MainWindow()
    {
        InitializeComponent();
        LoadBrandingAssets();
        _runtimeService = new MinecraftRuntimeService(_instanceService);
        InstancesList.ItemsSource = _instances;
        SidebarInstancesList.ItemsSource = _instances;
        AccountsList.ItemsSource = _accounts;
        OpenPage("Inicio");
        Opened += async (_, _) => await InitializeAsync();
    }


    private void LoadBrandingAssets()
    {
        string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "negativeclient_logo.png");
        if (File.Exists(logoPath))
        {
            try
            {
                BrandLogo.Source = new Bitmap(logoPath);
                BrandLogo.IsVisible = true;
                BrandLogoFallback.IsVisible = false;
            }
            catch
            {
                // Si falta el archivo de marca, se conserva el distintivo de respaldo.
            }
        }

        string backgroundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "negativeclient_bg.png");
        if (File.Exists(backgroundPath))
        {
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

    private async Task InitializeAsync()
    {
        try
        {
            _preferences = await _preferencesService.LoadAsync();
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
            HeaderStatus.Text = "Datos cargados";
        }
        catch (Exception ex)
        {
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
        if (!Directory.Exists(path)) return 0;
        long total = 0;
        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try { total += new FileInfo(file).Length; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
        catch (Exception ex) { StorageUsageLabel.Text = "No se pudo calcular el almacenamiento: " + ex.Message; }
    }

    private async void RefreshStorageUsage_Click(object? sender, RoutedEventArgs e)
        => await RefreshStorageUsageAsync();

    private async void ClearPackageCache_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeDownloadController is not null)
        {
            ShowSettingsMessage("Detén o espera a que termine la operación de modpack antes de limpiar la caché.", false);
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
                foreach (string file in Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                foreach (string directory in Directory.EnumerateDirectories(cache, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                {
                    try { if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            ShowSettingsMessage("Se limpió la caché de paquetes que se pudo eliminar.", true);
            await RefreshStorageUsageAsync();
        }
        catch (Exception ex) { ShowSettingsMessage("No se pudo limpiar la caché: " + ex.Message, false); }
    }

    private async void ClearTemporaryFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeDownloadController is not null)
        {
            ShowSettingsMessage("Detén o espera a que termine la operación de modpack antes de limpiar los temporales.", false);
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
                foreach (string directory in Directory.EnumerateDirectories(tempRoot))
                {
                    try { Directory.Delete(directory, recursive: true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }

                foreach (string file in Directory.EnumerateFiles(tempRoot))
                {
                    try { File.Delete(file); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }

            ShowSettingsMessage("Se limpiaron los archivos temporales que se pudieron eliminar.", true);
            await RefreshStorageUsageAsync();
        }
        catch (Exception ex)
        {
            ShowSettingsMessage("No se pudieron limpiar los temporales: " + ex.Message, false);
        }
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

            if (_selectedInstance is not null)
                _selectedInstance = _instances.FirstOrDefault(x => x.Id == _selectedInstance.Id);
            _selectedInstance ??= _instances.FirstOrDefault(x => string.Equals(x.Id, _preferences.LastSelectedInstanceId, StringComparison.OrdinalIgnoreCase));
            _selectedInstance ??= _instances.FirstOrDefault();
            UpdateSelectedInstanceUi();
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
        }
        catch (Exception ex)
        {
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
        foreach (MicrosoftAccountInfo account in _accountService.GetPremiumAccounts())
            _accounts.Add(account);

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

        try
        {
            await _accountService.ReauthenticateAccountAsync(account.Identifier);
            await RefreshAccountsAsync();
            AccountStatus.Text = "La cuenta se autenticó de nuevo correctamente.";
        }
        catch (Exception ex)
        {
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
            AccountStatus.Text = "No se pudo activar el modo Microsoft: " + ex.Message;
        }
    }

    private void SetAccountButtonsEnabled(bool enabled)
    {
        AddMicrosoftAccountButton.IsEnabled = enabled;
        UseSelectedAccountButton.IsEnabled = enabled;
        ReauthenticateAccountButton.IsEnabled = enabled;
        SignOutAccountButton.IsEnabled = enabled;
        UseMicrosoftModeButton.IsEnabled = enabled;
        AccountsList.IsEnabled = enabled;
    }

    private async void SelectInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        _selectedInstance = _instances.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        UpdateSelectedInstanceUi();
        await RememberSelectedInstanceAsync(_selectedInstance);
        HeaderStatus.Text = $"Instalación seleccionada: {instance.Name}";
    }

    private async void SidebarInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        _selectedInstance = _instances.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        UpdateSelectedInstanceUi();
        OpenPage("Inicio");
        await RememberSelectedInstanceAsync(_selectedInstance);
    }

    private async Task RememberSelectedInstanceAsync(InstalledInstance? instance)
    {
        if (instance is null || string.Equals(_preferences.LastSelectedInstanceId, instance.Id, StringComparison.Ordinal))
            return;
        _preferences.LastSelectedInstanceId = instance.Id;
        try { await _preferencesService.SaveAsync(_preferences); }
        catch (Exception ex) { HeaderStatus.Text = "No se pudo guardar la última instalación: " + ex.Message; }
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
            OpenPage("Instancias");
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
        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(1500))
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            GameStatus.Text = "Minecraft se cerró.";
            HeaderStatus.Text = "Minecraft cerrado";
        }
        catch (Exception ex)
        {
            GameStatus.Text = "No se pudo cerrar Minecraft: " + ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_runningGameProcess, process))
                _runningGameProcess = null;
            HomePlayButton.Content = "JUGAR";
            HomePlayButton.IsEnabled = _selectedInstance is not null;
            try { process.Dispose(); } catch { }
        }
    }

    private void UpdateSelectedInstanceUi()
    {
        HomeSelectedInstanceText.Text = _selectedInstance is null
            ? "Selecciona o instala una instancia"
            : $"{_selectedInstance.Name}  ·  Minecraft {_selectedInstance.MinecraftVersion}";

        HomePlayButton.IsEnabled = _selectedInstance is not null || _runningGameProcess is not null;
        HomePlayButton.DataContext = _selectedInstance;
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
        if (page == "Ajustes")
            SetSettingsTab(false);

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
            var loaded = new System.Collections.Generic.List<GalleryEntry>();
            foreach (var archive in store.GetArchives())
            {
                if (string.IsNullOrWhiteSpace(archive.ScreenshotsDirectory) ||
                    !Directory.Exists(archive.ScreenshotsDirectory))
                    continue;

                foreach (string file in Directory.EnumerateFiles(archive.ScreenshotsDirectory, "*", SearchOption.AllDirectories)
                    .Where(IsGalleryImage))
                {
                    loaded.Add(new GalleryEntry
                    {
                        InstanceId = archive.InstanceId,
                        InstanceName = archive.InstanceName,
                        FilePath = file,
                        CapturedAt = File.GetLastWriteTime(file)
                    });
                }
            }

            _galleryItems.Clear();
            _galleryItems.AddRange(loaded);
            _gallerySelection.RemoveWhere(path => !_galleryItems.Any(item => string.Equals(item.FilePath, path, StringComparison.OrdinalIgnoreCase)));
            RefreshGalleryInstanceOptions();
            ApplyGalleryFilters();
        }
        catch (Exception ex)
        {
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
        _galleryViewerIndex = visible.FindIndex(item => string.Equals(item.FilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase));
        ShowGalleryViewer();
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
                GalleryMessage.Text = "No se pudo eliminar la captura: " + ex.Message;
                GalleryMessage.IsVisible = true;
            }
        };
        viewer.Show(this);
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
            OfflineProfileStatus.Text = "No se pudo guardar el perfil: " + ex.Message;
            OfflineProfileStatus.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F0C674"));
            OfflineProfileStatus.IsVisible = true;
        }
    }

    private void CancelRuntimePreparation_Click(object? sender, RoutedEventArgs e)
    {
        _runtimePreparationCancellation?.Cancel();
        GameStatus.Text = "Cancelando la preparación de Minecraft…";
    }

    private async void PlayInstance_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InstalledInstance instance)
            return;

        _selectedInstance = _instances.FirstOrDefault(x => x.Id == instance.Id) ?? instance;
        UpdateSelectedInstanceUi();
        OpenPage("Inicio");
        await RememberSelectedInstanceAsync(instance);
        button.IsEnabled = false;
        GameProgress.Value = 0;
        GameProgress.IsVisible = true;
        GameStatus.Text = $"Preparando {instance.Name}…";

        bool needsPreparation = !instance.RuntimePrepared || string.IsNullOrWhiteSpace(instance.LaunchVersionName);
        _runtimePreparationCancellation?.Dispose();
        _runtimePreparationCancellation = new System.Threading.CancellationTokenSource();
        RuntimePreparationControls.IsVisible = needsPreparation;

        var progress = new Progress<double>(value =>
            GameProgress.Value = Math.Clamp(value, 0, 100));
        var status = new Progress<string>(value => GameStatus.Text = value);

        try
        {
            if (needsPreparation)
            {
                await _runtimeService.PrepareAsync(
                    instance, _preferences, progress, status,
                    _runtimePreparationCancellation.Token);
                await RefreshInstancesAsync();
            }

            var session = await _accountService.GetValidSessionAsync();
            if (session is null)
            {
                throw new InvalidOperationException(
                    "La instalación está preparada, pero no hay una sesión válida. " +
                    "Inicia sesión con Microsoft o activa un perfil sin conexión en Ajustes.");
            }

            GameStatus.Text = $"Iniciando Minecraft con la cuenta {_accountService.Username}…";
            Process process = await _runtimeService.LaunchAsync(instance, _preferences, session);
            _runningGameProcess = process;
            HomePlayButton.Content = "CERRAR";
            HomePlayButton.IsEnabled = true;
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_runningGameProcess, process))
                    {
                        _runningGameProcess = null;
                        HomePlayButton.Content = "JUGAR";
                        HomePlayButton.IsEnabled = _selectedInstance is not null;
                        GameStatus.Text = "Minecraft se ha cerrado.";
                        HeaderStatus.Text = "Minecraft cerrado";
                        try { process.Dispose(); } catch { }
                    }
                });
            };
            if (_preferences.ShowGameConsole)
            {
                _gameConsoleWindow = new GameConsoleWindow(process);
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
            RuntimePreparationControls.IsVisible = false;
            _runtimePreparationCancellation?.Dispose();
            _runtimePreparationCancellation = null;
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
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#12171D")),
            Foreground = Avalonia.Media.Brushes.White,
            SystemDecorations = SystemDecorations.Full
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
        dialog.Content = root;

        return await dialog.ShowDialog<string?>(this);
    }

    private async Task RunInstanceMaintenanceAsync(InstalledInstance instance, bool verifyIntegrity)
    {
        string code = instance.InstallCode?.Trim() ?? string.Empty;
        string catalogId = _preferences.ModpackCatalogFileId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(catalogId))
        {
            InstancesMessage.Text = "No se puede realizar esta operación: falta el código de instalación o el ID del catálogo en Ajustes.";
            InstancesMessage.IsVisible = true;
            return;
        }

        OpenPage("Modpacks");
        if (_activeDownloadController is not null)
        {
            ShowModpackMessage("Ya hay una operación de modpack en curso.", false);
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
                await _runtimeService.PrepareAsync(
                    updatedInstance,
                    _preferences,
                    progress,
                    new Progress<string>(message => ShowModpackMessage(message, true)),
                    controller.StopToken);
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
        if (!await ConfirmDeleteInstanceAsync(instance))
            return;

        try
        {
            await _instanceService.DeleteInstanceAsync(instance.Id);
            await RefreshInstancesAsync();
            await RefreshStorageUsageAsync();
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
            OfflineProfileStatus.Text = "No se pudo seleccionar la skin: " + ex.Message;
            OfflineProfileStatus.IsVisible = true;
        }
    }

    private async void RemoveOfflineSkin_Click(object? sender, RoutedEventArgs e)
    {
        _offlineSkinPath = null;
        OfflineSkinPathLabel.Text = "Se quitará la skin personalizada al guardar.";
        string username = OfflineUsernameInput.Text?.Trim() ?? string.Empty;
        if (username.Length is < 3 or > 16)
        {
            OfflineProfileStatus.Text = "Selecciona un nombre válido y guarda el perfil para aplicar el cambio.";
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
            ShowSettingsMessage("No se pudo abrir el selector de carpetas: " + ex.Message, false);
        }
    }

    private void RamSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (RamValueText is not null)
            RamValueText.Text = $"{(int)Math.Round(e.NewValue)} MB";
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
            int ram = (int)Math.Round(RamSlider.Value);

            string newRoot = string.IsNullOrWhiteSpace(StoragePathInput.Text)
                ? InstanceService.DefaultStorageRoot
                : Path.GetFullPath(StoragePathInput.Text.Trim());

            string currentRoot = _instanceService.GetStorageRoot();
            if (!PathsEqual(currentRoot, newRoot))
                await _instanceService.ChangeStorageRootAsync(newRoot);

            _preferences.MaximumRamMb = Math.Clamp(ram, 1024, 32768);
            _preferences.UseAutomaticJava = AutomaticJavaInput.IsChecked == true;
            _preferences.EnableCustomJavaArguments = EnableCustomJavaArgumentsInput.IsChecked == true;
            _preferences.CustomJavaArguments = CustomJavaArgumentsInput.Text?.Trim() ?? string.Empty;
            _preferences.EnableHolidayLauncherThemes = HolidayThemesInput.IsChecked == true;
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

    private async Task<string?> ShowAddModpackDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Instalar modpack",
            Width = 460,
            Height = 260,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#14181D")),
            Foreground = Avalonia.Media.Brushes.White,
            SystemDecorations = SystemDecorations.Full
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
        dialog.Content = root;
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
            var progress = new Progress<double>(value =>
            {
                InstallProgress.Value = Math.Clamp(value, 0, 100);
                ModpackMessage.Text = $"Instalando {manifest.Name}… {InstallProgress.Value:0}%";
                ModpackMessage.IsVisible = true;
            });

            InstalledInstance installedInstance = await installer.InstallOrUpdateAsync(manifest, code, existing, progress, controller);
            _selectedInstance = installedInstance;
            await RememberSelectedInstanceAsync(installedInstance);
            await RefreshInstancesAsync();
            await RefreshStorageUsageAsync();
            ShowModpackMessage($"'{manifest.Name}' se instaló o actualizó correctamente. La preparación del juego aún debe completarse por separado.", true);
            HeaderStatus.Text = "Modpack instalado";
        }
        catch (OperationCanceledException)
        {
            ShowModpackMessage(controller.IsStopped ? "Operación detenida." : "La operación se canceló.", false);
        }
        catch (Exception ex)
        {
            ShowModpackMessage("No se pudo instalar el modpack: " + ex.Message, false);
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
    {
        string a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
