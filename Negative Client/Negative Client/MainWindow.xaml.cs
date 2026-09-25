using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow : Window
    {
        private readonly GoogleDriveService _driveService;
        private readonly ModpackCatalogService _modpackCatalogService;
        private readonly InstanceService _instanceService;
        private readonly ModpackInstallerService _modpackInstallerService;
        private readonly LauncherStateService _launcherStateService;
        private readonly ImageCacheService _imageCacheService;
        private readonly MicrosoftAccountService _microsoftAccountService;
        private readonly LauncherPreferencesService _launcherPreferencesService;
        private readonly MinecraftGameService _minecraftGameService;
        private readonly MinecraftSkinService _minecraftSkinService;

        private readonly Dictionary<string, InstalledInstance> _instances =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Button> _instanceButtons =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ModpackManifest?> _remoteManifests =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, InstanceOperationState> _operations =
            new(StringComparer.OrdinalIgnoreCase);

        private InstalledInstance? _selectedInstance;
        private string? _lastPlayedInstanceId;

        private Process? _runningMinecraftProcess;
        private string? _runningMinecraftInstanceId;

        private static readonly Brush AccentBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    79,
                    195,
                    215));

        private static readonly Brush NormalBorderBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    70,
                    81,
                    92));


        private sealed class InstanceOperationState
        {
            public bool IsRunning { get; set; }

            public bool IsUpdate { get; set; }

            public double Progress { get; set; }

            public string Message { get; set; } =
                string.Empty;


            public string StageMessage { get; set; } =
                string.Empty;


            public string ButtonText { get; set; } =
                string.Empty;
        }


        public MainWindow()
        {
            InitializeComponent();

            _driveService =
                new GoogleDriveService();

            _modpackCatalogService =
                new ModpackCatalogService();

            _instanceService =
                new InstanceService();

            _modpackInstallerService =
                new ModpackInstallerService(
                    _driveService,
                    _instanceService);

            _launcherStateService =
                new LauncherStateService();

            _imageCacheService =
                new ImageCacheService(
                    _driveService);

            _microsoftAccountService =
                MicrosoftAccountService.Instance;

            _launcherPreferencesService =
                new LauncherPreferencesService();

            _minecraftGameService =
                new MinecraftGameService(
                    _instanceService);

            _minecraftSkinService =
                new MinecraftSkinService();

            Loaded +=
                MainWindow_Loaded;
        }


        // =====================================================
        // INICIO DEL LAUNCHER
        // =====================================================

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            List<InstalledInstance> instances =
                await _instanceService
                    .LoadAllAsync();

            foreach (InstalledInstance instance in instances)
            {
                _instances[instance.Id] =
                    instance;
            }

            _lastPlayedInstanceId =
                await _launcherStateService
                    .GetLastPlayedInstanceIdAsync();

            await _microsoftAccountService
                .InitializeAsync();

            LoadLauncherBrandingAssets();

            RefreshMicrosoftWarning();

            await RefreshQuickAccountUiAsync();

            RefreshInstanceButtons();

            ShowHome();
        }


        // =====================================================
        // VENTANA
        // =====================================================

        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (e.LeftButton ==
                MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState =
                WindowState.Minimized;
        }


        private void Maximize_Click(
            object sender,
            RoutedEventArgs e)
        {
            ToggleMaximize();
        }


        private void ToggleMaximize()
        {
            WindowState =
                WindowState ==
                WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
        }


        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }


        // =====================================================
        // HOME
        // =====================================================

        private void HomeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowHome();
        }


        private void ShowHome()
        {
            _selectedInstance =
                null;

            AccountQuickPopup.IsOpen =
                false;

            AccountQuickRoot.Visibility =
                Visibility.Collapsed;

            InstanceOptionsButton.Visibility =
                Visibility.Collapsed;

            MainCenterPanel.Visibility =
                Visibility.Collapsed;

            ClearInstanceBackground();

            ShowHomeBackground();

            MainTitleText.Text =
                "NEGATIVE STUDIOS";

            SelectedModpackText.Text =
                "Selecciona o instala una instancia";

            HideProgress();


            if (IsMinecraftRunning())
            {
                PlayButton.Content =
                    "CERRAR";

                PlayButton.IsEnabled =
                    true;

                string runningName =
                    GetRunningInstanceName();

                StatusText.Text =
                    string.IsNullOrWhiteSpace(runningName)
                        ? "Minecraft está abierto."
                        : $"Minecraft abierto: {runningName}";
            }
            else
            {
                InstalledInstance? lastPlayed =
                    GetLastPlayedInstance();


                if (lastPlayed != null)
                {
                    PlayButton.Content =
                        "JUGAR";

                    PlayButton.IsEnabled =
                        true;

                    StatusText.Text =
                        $"Última instancia jugada: {lastPlayed.Name}";
                }
                else
                {
                    PlayButton.Content =
                        "JUGAR";

                    PlayButton.IsEnabled =
                        false;

                    StatusText.Text =
                        "Negative Client listo";
                }
            }


            UpdateSidebarSelection();
        }


        private InstalledInstance? GetLastPlayedInstance()
        {
            if (string.IsNullOrWhiteSpace(
                    _lastPlayedInstanceId))
            {
                return null;
            }

            if (!_instances.TryGetValue(
                    _lastPlayedInstanceId,
                    out InstalledInstance? instance))
            {
                return null;
            }

            if (!instance.IsInstalled ||
                !instance.RuntimePrepared)
            {
                return null;
            }

            return instance;
        }


        private async Task OpenLastPlayedInstanceAsync()
        {
            InstalledInstance? instance =
                GetLastPlayedInstance();

            if (instance == null)
            {
                return;
            }

            await SelectInstanceAsync(
                instance);
        }


        // =====================================================
        // INSTANCIAS - BARRA IZQUIERDA
        // =====================================================

        private void RefreshInstanceButtons()
        {
            ModpackList.Children.Clear();

            _instanceButtons.Clear();

            foreach (InstalledInstance instance in
                _instances.Values
                    .OrderBy(
                        instance =>
                            instance.Name))
            {
                string letter =
                    string.IsNullOrWhiteSpace(
                        instance.Name)
                        ? "?"
                        : instance.Name[..1]
                            .ToUpperInvariant();

                Button button =
                    new()
                    {
                        Content =
                            letter,

                        Tag =
                            instance.Id,

                        ToolTip =
                            instance.Name,

                        Style =
                            (Style)FindResource(
                                "CircleButton"),

                        Margin =
                            new Thickness(
                                0,
                                0,
                                0,
                                10),

                        BorderBrush =
                            NormalBorderBrush
                    };

                button.Click +=
                    InstanceButton_Click;

                ModpackList.Children.Add(
                    button);

                _instanceButtons[
                    instance.Id] =
                    button;

                _ =
                    LoadInstanceIconAsync(
                        instance,
                        button);
            }

            UpdateSidebarSelection();
        }


        private async Task LoadInstanceIconAsync(
            InstalledInstance instance,
            Button button)
        {
            if (string.IsNullOrWhiteSpace(
                    instance.IconFileId))
            {
                return;
            }

            string? imagePath =
                await _imageCacheService
                    .GetIconPathAsync(
                        instance.Id,
                        instance.IconFileId);

            if (string.IsNullOrWhiteSpace(
                    imagePath) ||
                !File.Exists(imagePath))
            {
                return;
            }

            if (button.Tag is not string buttonId ||
                !string.Equals(
                    buttonId,
                    instance.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                BitmapImage bitmap =
                    LoadBitmap(
                        imagePath);

                Image image =
                    new()
                    {
                        Source =
                            bitmap,

                        Width =
                            50,

                        Height =
                            50,

                        Stretch =
                            Stretch.UniformToFill,

                        IsHitTestVisible =
                            false,

                        Clip =
                            new EllipseGeometry(
                                new Point(
                                    25,
                                    25),
                                25,
                                25)
                    };

                button.Content =
                    image;
            }
            catch
            {
                // La letra queda como fallback.
            }
        }


        private async void InstanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not string instanceId)
            {
                return;
            }

            if (!_instances.TryGetValue(
                    instanceId,
                    out InstalledInstance? instance))
            {
                return;
            }

            await SelectInstanceAsync(
                instance);
        }


        // =====================================================
        // SELECCIONAR INSTANCIA
        // =====================================================

        private async Task SelectInstanceAsync(
            InstalledInstance instance)
        {
            _selectedInstance =
                instance;

            string selectionId =
                instance.Id;

            ClearHomeBackground();

            AccountQuickRoot.Visibility =
                Visibility.Visible;

            InstanceOptionsButton.Visibility =
                Visibility.Visible;

            AccountQuickPopup.IsOpen =
                false;

            _ =
                RefreshQuickAccountUiAsync();

            ClearInstanceBackground();

            MainCenterPanel.Visibility =
                string.IsNullOrWhiteSpace(
                    instance.BackgroundFileId)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            _ =
                LoadInstanceBackgroundAsync(
                    instance);

            MainTitleText.Text =
                instance.Name.ToUpperInvariant();

            string loaderText =
                instance.Loader;

            if (!string.IsNullOrWhiteSpace(
                    instance.LoaderVersion))
            {
                loaderText +=
                    " " +
                    instance.LoaderVersion;
            }

            SelectedModpackText.Text =
                $"Minecraft {instance.MinecraftVersion}" +
                $"  •  {loaderText}";

            UpdateSidebarSelection();

            if (TryShowRunningOperation(
                    instance.Id))
            {
                return;
            }


            if (IsMinecraftRunning(
                    instance.Id))
            {
                HideProgress();

                PlayButton.Content =
                    "CERRAR";

                PlayButton.IsEnabled =
                    true;

                StatusText.Text =
                    "Minecraft está abierto.";

                return;
            }


            if (IsMinecraftRunning())
            {
                HideProgress();

                PlayButton.Content =
                    "JUEGO ABIERTO";

                PlayButton.IsEnabled =
                    false;

                StatusText.Text =
                    "Cierra la instancia que está abierta antes de iniciar otra.";

                return;
            }


            HideProgress();

            if (string.IsNullOrWhiteSpace(
                    instance.InstallCode))
            {
                ApplyLocalInstanceState(
                    instance);

                return;
            }

            try
            {
                StatusText.Text =
                    "Buscando actualizaciones...";

                PlayButton.IsEnabled =
                    false;

                ModpackManifest? remote =
                    await _modpackCatalogService
                        .FindByCodeAsync(
                            instance.InstallCode);

                _remoteManifests[
                    instance.Id] =
                    remote;

                if (!IsSelected(
                        selectionId))
                {
                    return;
                }

                if (remote != null)
                {
                    bool appearanceChanged =
                        !string.Equals(
                            instance.IconFileId,
                            remote.IconFileId,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            instance.BackgroundFileId,
                            remote.BackgroundFileId,
                            StringComparison.Ordinal);

                    if (appearanceChanged)
                    {
                        instance.IconFileId =
                            remote.IconFileId;

                        instance.BackgroundFileId =
                            remote.BackgroundFileId;

                        _instances[
                            instance.Id] =
                            instance;

                        await _instanceService
                            .SaveAsync(
                                instance);

                        RefreshInstanceButtons();

                        ClearInstanceBackground();

                        _ =
                            LoadInstanceBackgroundAsync(
                                instance);
                    }
                }

                if (TryShowRunningOperation(
                        selectionId))
                {
                    return;
                }

                // Si todavía faltan los archivos del modpack o
                // Minecraft/Forge/Java, el botón sigue siendo DESCARGAR.
                if (!instance.IsInstalled ||
                    !instance.RuntimePrepared)
                {
                    PlayButton.Content =
                        "DESCARGAR";

                    PlayButton.IsEnabled =
                        remote != null ||
                        instance.IsInstalled;

                    StatusText.Text =
                        !instance.IsInstalled
                            ? $"Disponible • v{remote?.Version ?? "?"} • sin descargar"
                            : "Faltan archivos de Minecraft/Forge. Pulsa DESCARGAR para completar.";

                    return;
                }

                if (remote == null)
                {
                    PlayButton.Content =
                        "JUGAR";

                    PlayButton.IsEnabled =
                        true;

                    StatusText.Text =
                        $"Instalado • v{instance.InstalledVersion}";

                    return;
                }

                if (!string.Equals(
                        remote.Version,
                        instance.InstalledVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    PlayButton.Content =
                        "ACTUALIZAR";

                    PlayButton.IsEnabled =
                        true;

                    StatusText.Text =
                        $"Actualización disponible: " +
                        $"v{instance.InstalledVersion} → " +
                        $"v{remote.Version}";
                }
                else
                {
                    PlayButton.Content =
                        "JUGAR";

                    PlayButton.IsEnabled =
                        true;

                    StatusText.Text =
                        $"Actualizado • v{instance.InstalledVersion}";
                }
            }
            catch
            {
                if (!IsSelected(
                        selectionId))
                {
                    return;
                }

                if (TryShowRunningOperation(
                        selectionId))
                {
                    return;
                }

                ApplyLocalInstanceState(
                    instance);
            }
        }


        private void ApplyLocalInstanceState(
            InstalledInstance instance)
        {
            if (!instance.IsInstalled ||
                !instance.RuntimePrepared)
            {
                PlayButton.Content =
                    "DESCARGAR";

                PlayButton.IsEnabled =
                    instance.IsInstalled;

                StatusText.Text =
                    instance.IsInstalled
                        ? "Faltan archivos de Minecraft/Forge y no hay conexión para completarlos."
                        : "Sin conexión. No se puede descargar esta instancia.";

                return;
            }

            PlayButton.Content =
                "JUGAR";

            PlayButton.IsEnabled =
                true;

            StatusText.Text =
                $"Modo sin conexión • v{instance.InstalledVersion}";
        }


        private async Task LoadInstanceBackgroundAsync(
            InstalledInstance instance)
        {
            if (string.IsNullOrWhiteSpace(
                    instance.BackgroundFileId))
            {
                if (IsSelected(
                        instance.Id))
                {
                    MainCenterPanel.Visibility =
                        Visibility.Visible;
                }

                return;
            }


            string instanceId =
                instance.Id;


            string? imagePath =
                await _imageCacheService
                    .GetBackgroundPathAsync(
                        instance.Id,
                        instance.BackgroundFileId);


            if (!IsSelected(
                    instanceId))
            {
                return;
            }


            if (string.IsNullOrWhiteSpace(
                    imagePath) ||
                !File.Exists(
                    imagePath))
            {
                MainCenterPanel.Visibility =
                    Visibility.Visible;

                return;
            }


            try
            {
                InstanceBackgroundImage.Source =
                    LoadBitmap(
                        imagePath);


                InstanceBackgroundImage.Visibility =
                    Visibility.Visible;


                InstanceBackgroundOverlay.Visibility =
                    Visibility.Visible;


                // Si hay imagen de fondo, ocultamos nombre y versión.
                MainCenterPanel.Visibility =
                    Visibility.Collapsed;
            }
            catch
            {
                ClearInstanceBackground();


                MainCenterPanel.Visibility =
                    Visibility.Visible;
            }
        }


        private void LoadLauncherBrandingAssets()
        {
            string? logoPath =
                ResolveAssetPath(
                    "negativeclient_logo.png");


            if (!string.IsNullOrWhiteSpace(
                    logoPath) &&
                File.Exists(
                    logoPath))
            {
                try
                {
                    TopLeftLogoImage.Source =
                        LoadBitmap(
                            logoPath);

                    TopLeftLogoImage.Visibility =
                        Visibility.Visible;

                    TopLeftLogoFallbackText.Visibility =
                        Visibility.Collapsed;
                }
                catch
                {
                    TopLeftLogoImage.Visibility =
                        Visibility.Collapsed;

                    TopLeftLogoFallbackText.Visibility =
                        Visibility.Visible;
                }
            }


            string? iconPath =
                ResolveAssetPath(
                    "negativeclient.ico");


            if (!string.IsNullOrWhiteSpace(
                    iconPath) &&
                File.Exists(
                    iconPath))
            {
                try
                {
                    Icon =
                        BitmapFrame.Create(
                            new Uri(
                                Path.GetFullPath(
                                    iconPath),
                                UriKind.Absolute));
                }
                catch
                {
                }
            }
        }


        private static string? ResolveAssetPath(
            string fileName)
        {
            string outputPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    fileName);


            if (File.Exists(
                    outputPath))
            {
                return outputPath;
            }


            string projectPath =
                Path.GetFullPath(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "..",
                        "..",
                        "..",
                        "Assets",
                        fileName));


            return
                File.Exists(
                    projectPath)
                    ? projectPath
                    : null;
        }


        private void ShowHomeBackground()
        {
            string? imagePath =
                ResolveHomeBackgroundPath();


            if (string.IsNullOrWhiteSpace(
                    imagePath) ||
                !File.Exists(
                    imagePath))
            {
                ClearHomeBackground();

                return;
            }


            try
            {
                HomeBackgroundImage.Source =
                    LoadBitmap(
                        imagePath);

                HomeBackgroundImage.Visibility =
                    Visibility.Visible;

                HomeBackgroundOverlay.Visibility =
                    Visibility.Visible;
            }
            catch
            {
                ClearHomeBackground();
            }
        }


        private void ClearHomeBackground()
        {
            HomeBackgroundImage.Source =
                null;

            HomeBackgroundImage.Visibility =
                Visibility.Collapsed;

            HomeBackgroundOverlay.Visibility =
                Visibility.Collapsed;
        }


        private static string? ResolveHomeBackgroundPath()
        {
            return
                ResolveAssetPath(
                    "negativeclient_bg.png");
        }


        private void ClearInstanceBackground()
        {
            InstanceBackgroundImage.Source =
                null;

            InstanceBackgroundImage.Visibility =
                Visibility.Collapsed;

            InstanceBackgroundOverlay.Visibility =
                Visibility.Collapsed;
        }


        private static BitmapImage LoadBitmap(
            string filePath)
        {
            BitmapImage bitmap =
                new();

            bitmap.BeginInit();

            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;

            bitmap.CreateOptions =
                BitmapCreateOptions.IgnoreImageCache;

            bitmap.UriSource =
                new Uri(
                    Path.GetFullPath(
                        filePath),
                    UriKind.Absolute);

            bitmap.EndInit();

            bitmap.Freeze();

            return bitmap;
        }


        // =====================================================
        // AÑADIR INSTANCIA DESDE CÓDIGO
        // =====================================================

        private async void AddModpackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddModpackWindow window =
                new()
                {
                    Owner =
                        this
                };

            bool? result =
                window.ShowDialog();

            if (result != true)
            {
                return;
            }

            string code =
                window.InstallCode;

            try
            {
                AddModpackButton.IsEnabled =
                    false;

                if (_selectedInstance != null &&
                    !_operations.ContainsKey(
                        _selectedInstance.Id))
                {
                    PlayButton.IsEnabled =
                        false;
                }

                StatusText.Text =
                    $"Buscando instalación {code}...";

                ModpackManifest? manifest =
                    await _modpackCatalogService
                        .FindByCodeAsync(
                            code);

                if (manifest == null)
                {
                    StatusText.Text =
                        "Código no encontrado.";

                    MessageBox.Show(
                        $"No existe ninguna instalación " +
                        $"asociada al código {code}.",
                        "Código no encontrado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                if (_instances.TryGetValue(
                        manifest.Id,
                        out InstalledInstance?
                            existingInstance))
                {
                    _remoteManifests[
                        manifest.Id] =
                        manifest;

                    existingInstance.IconFileId =
                        manifest.IconFileId;

                    existingInstance.BackgroundFileId =
                        manifest.BackgroundFileId;

                    await _instanceService
                        .SaveAsync(
                            existingInstance);

                    RefreshInstanceButtons();

                    await SelectInstanceAsync(
                        existingInstance);

                    return;
                }

                InstalledInstance instance =
                    new()
                    {
                        Id =
                            manifest.Id,

                        Name =
                            manifest.Name,

                        InstallCode =
                            code,

                        IsInstalled =
                            false,

                        RuntimePrepared =
                            false,

                        LaunchVersionName =
                            string.Empty,

                        InstalledVersion =
                            string.Empty,

                        MinecraftVersion =
                            manifest.MinecraftVersion,

                        Loader =
                            manifest.Loader,

                        LoaderVersion =
                            manifest.LoaderVersion,

                        IconFileId =
                            manifest.IconFileId,

                        BackgroundFileId =
                            manifest.BackgroundFileId
                    };

                await _instanceService
                    .SaveAsync(
                        instance);

                _instances[
                    instance.Id] =
                    instance;

                _remoteManifests[
                    instance.Id] =
                    manifest;

                RefreshInstanceButtons();

                await SelectInstanceAsync(
                    instance);

                if (IsSelected(
                        instance.Id))
                {
                    StatusText.Text =
                        $"{instance.Name} añadido. " +
                        "Pulsa DESCARGAR para instalarlo.";
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    "No se pudo consultar el catálogo.\n\n" +
                    ex.Message,
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text =
                    "Error de conexión.";
            }
            catch (JsonException ex)
            {
                MessageBox.Show(
                    "El manifest o catálogo contiene " +
                    "JSON inválido.\n\n" +
                    ex.Message,
                    "JSON inválido",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text =
                    "JSON inválido.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error al añadir instalación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text =
                    "No se pudo añadir la instalación.";
            }
            finally
            {
                AddModpackButton.IsEnabled =
                    true;

                if (_selectedInstance != null)
                {
                    if (!TryShowRunningOperation(
                            _selectedInstance.Id))
                    {
                        await RefreshSelectedInstanceButtonAsync();
                    }
                }
                else
                {
                    ShowHome();
                }
            }
        }


        // =====================================================
        // BOTÓN PRINCIPAL
        // =====================================================

        private async void PlayButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // HOME:
            // si Minecraft está abierto, el botón lo cierra.
            // si no, abre la última instancia jugada.
            if (_selectedInstance == null)
            {
                if (IsMinecraftRunning())
                {
                    await CloseRunningMinecraftAsync();

                    ShowHome();

                    return;
                }


                await OpenLastPlayedInstanceAsync();

                return;
            }


            string instanceId =
                _selectedInstance.Id;


            // La instancia actualmente abierta puede cerrarse
            // desde el mismo botón que antes decía JUGAR.
            if (IsMinecraftRunning(
                    instanceId))
            {
                await CloseRunningMinecraftAsync();

                if (_instances.TryGetValue(
                        instanceId,
                        out InstalledInstance? refreshed))
                {
                    await SelectInstanceAsync(
                        refreshed);
                }

                return;
            }


            // Solo permitimos una instancia de Minecraft abierta
            // desde este launcher al mismo tiempo.
            if (IsMinecraftRunning())
            {
                MessageBox.Show(
                    "Ya hay una instancia de Minecraft abierta.\n\n" +
                    "Ciérrala antes de iniciar otra.",
                    "Minecraft ya está abierto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (TryShowRunningOperation(
                    instanceId))
            {
                return;
            }


            // El botón DESCARGAR también prepara Minecraft,
            // Forge y Java. Al terminar cambiará a JUGAR.
            if (!_selectedInstance.IsInstalled ||
                !_selectedInstance.RuntimePrepared)
            {
                await DownloadOrUpdateInstanceAsync(
                    _selectedInstance,
                    isUpdate: false);

                return;
            }


            ModpackManifest? remote =
                GetRemoteManifest(
                    instanceId);


            if (remote != null &&
                !string.Equals(
                    remote.Version,
                    _selectedInstance.InstalledVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                await DownloadOrUpdateInstanceAsync(
                    _selectedInstance,
                    isUpdate: true);

                return;
            }


            // =============================================
            // CUENTA MICROSOFT
            // =============================================

            var validSession =
                await _microsoftAccountService
                    .GetValidSessionAsync();


            if (validSession == null)
            {
                RefreshMicrosoftWarning();


                StatusText.Text =
                    "Debes elegir una cuenta Microsoft válida desde Configuración.";


                MessageBox.Show(
                    "No puedes iniciar Minecraft sin una cuenta Microsoft " +
                    "conectada.\n\nAbre Configuración con el botón ⚙, añade o " +
                    "selecciona una cuenta y vuelve a intentarlo.",
                    "Cuenta Microsoft requerida",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);


                return;
            }


            RefreshMicrosoftWarning();


            // =============================================
            // INICIAR MINECRAFT
            // =============================================

            try
            {
                PlayButton.IsEnabled =
                    false;

                PlayButton.Content =
                    "INICIANDO...";

                StatusText.Text =
                    $"Iniciando como {validSession.Username}...";


                LauncherPreferences preferences =
                    await _launcherPreferencesService
                        .LoadAsync();


                Process process =
                    await _minecraftGameService
                        .LaunchAsync(
                            _selectedInstance,
                            validSession,
                            preferences);


                _runningMinecraftProcess =
                    process;

                _runningMinecraftInstanceId =
                    instanceId;


                _lastPlayedInstanceId =
                    _selectedInstance.Id;


                await _launcherStateService
                    .SetLastPlayedInstanceIdAsync(
                        _selectedInstance.Id);


                StatusText.Text =
                    $"Minecraft abierto como {validSession.Username}.";


                PlayButton.Content =
                    "CERRAR";

                PlayButton.IsEnabled =
                    true;


                process.EnableRaisingEvents =
                    true;


                process.Exited +=
                    (_, _) =>
                    {
                        Dispatcher.Invoke(
                            () =>
                            {
                                HandleMinecraftExited(
                                    process,
                                    instanceId);
                            });
                    };
                if (preferences.CloseLauncherOnGameStart)
                {
                    Close();

                    return;
                }
            }
            catch (Exception ex)
            {
                PlayButton.Content =
                    "JUGAR";

                PlayButton.IsEnabled =
                    true;


                StatusText.Text =
                    "No se pudo iniciar Minecraft.";


                MessageBox.Show(
                    ex.Message,
                    "Error al iniciar Minecraft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private bool IsMinecraftRunning(
            string? instanceId = null)
        {
            Process? process =
                _runningMinecraftProcess;


            if (process == null)
            {
                return false;
            }


            try
            {
                if (process.HasExited)
                {
                    _runningMinecraftProcess =
                        null;

                    _runningMinecraftInstanceId =
                        null;

                    return false;
                }
            }
            catch
            {
                _runningMinecraftProcess =
                    null;

                _runningMinecraftInstanceId =
                    null;

                return false;
            }


            if (string.IsNullOrWhiteSpace(
                    instanceId))
            {
                return true;
            }


            return
                string.Equals(
                    instanceId,
                    _runningMinecraftInstanceId,
                    StringComparison.OrdinalIgnoreCase);
        }


        private string GetRunningInstanceName()
        {
            if (string.IsNullOrWhiteSpace(
                    _runningMinecraftInstanceId))
            {
                return string.Empty;
            }


            return
                _instances.TryGetValue(
                    _runningMinecraftInstanceId,
                    out InstalledInstance? instance)
                    ? instance.Name
                    : _runningMinecraftInstanceId;
        }


        private async Task CloseRunningMinecraftAsync()
        {
            Process? process =
                _runningMinecraftProcess;


            if (process == null)
            {
                return;
            }


            try
            {
                PlayButton.Content =
                    "CERRANDO...";

                PlayButton.IsEnabled =
                    false;

                StatusText.Text =
                    "Cerrando Minecraft...";


                if (!process.HasExited)
                {
                    bool closeRequested =
                        process.CloseMainWindow();


                    if (closeRequested)
                    {
                        await Task.Run(
                            () =>
                                process.WaitForExit(
                                    7000));
                    }


                    if (!process.HasExited)
                    {
                        MessageBoxResult forceClose =
                            MessageBox.Show(
                                "Minecraft no respondió al cierre normal.\n\n" +
                                "¿Quieres forzar el cierre? Esto podría hacer " +
                                "que se pierda progreso que todavía no se haya guardado.",
                                "Forzar cierre de Minecraft",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);


                        if (forceClose ==
                            MessageBoxResult.Yes)
                        {
                            process.Kill(
                                entireProcessTree: true);


                            await Task.Run(
                                () =>
                                    process.WaitForExit(
                                        5000));
                        }
                        else
                        {
                            StatusText.Text =
                                "Minecraft sigue abierto.";

                            PlayButton.Content =
                                "CERRAR";

                            PlayButton.IsEnabled =
                                true;

                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "No se pudo cerrar Minecraft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (process.HasExited)
                {
                    HandleMinecraftExited(
                        process,
                        _runningMinecraftInstanceId ??
                        string.Empty);
                }
            }
        }


        private void HandleMinecraftExited(
            Process process,
            string instanceId)
        {
            try
            {
                if (_runningMinecraftProcess != null &&
                    _runningMinecraftProcess.Id ==
                    process.Id)
                {
                    _runningMinecraftProcess =
                        null;

                    _runningMinecraftInstanceId =
                        null;
                }
            }
            catch
            {
                _runningMinecraftProcess =
                    null;

                _runningMinecraftInstanceId =
                    null;
            }


            if (_selectedInstance == null)
            {
                ShowHome();

                return;
            }


            string selectedId =
                _selectedInstance.Id;


            if (_instances.TryGetValue(
                    selectedId,
                    out InstalledInstance? current))
            {
                _ =
                    SelectInstanceAsync(
                        current);
            }
            else
            {
                ShowHome();
            }
        }


        // =====================================================
        // DESCARGA / ACTUALIZACIÓN / PREPARACIÓN DE MINECRAFT
        // =====================================================

        private async Task DownloadOrUpdateInstanceAsync(
            InstalledInstance instance,
            bool isUpdate)
        {
            string instanceId =
                instance.Id;


            if (_operations.TryGetValue(
                    instanceId,
                    out InstanceOperationState?
                        existingOperation) &&
                existingOperation.IsRunning)
            {
                ShowOperationState(
                    instanceId,
                    existingOperation);

                return;
            }


            InstanceOperationState operation =
                new()
                {
                    IsRunning =
                        true,

                    IsUpdate =
                        isUpdate,

                    Progress =
                        0,

                    Message =
                        isUpdate
                            ? "Preparando actualización..."
                            : "Preparando descarga...",

                    ButtonText =
                        isUpdate
                            ? "ACTUALIZANDO..."
                            : "DESCARGANDO..."
                };


            _operations[
                instanceId] =
                operation;


            ShowOperationState(
                instanceId,
                operation);


            bool success =
                false;


            InstalledInstance workingInstance =
                instance;


            try
            {
                ModpackManifest? manifest =
                    GetRemoteManifest(
                        instanceId);


                if (manifest == null)
                {
                    manifest =
                        await _modpackCatalogService
                            .FindByCodeAsync(
                                instance.InstallCode);


                    _remoteManifests[
                        instanceId] =
                        manifest;
                }


                // =========================================
                // 1. DESCARGAR / ACTUALIZAR MODPACK
                // =========================================

                bool needsModpackFiles =
                    !instance.IsInstalled ||
                    isUpdate;


                if (needsModpackFiles)
                {
                    if (manifest == null)
                    {
                        throw new InvalidOperationException(
                            "No se encontró el manifest remoto de esta instalación.");
                    }


                    if (string.IsNullOrWhiteSpace(
                            manifest.ArchiveFileId))
                    {
                        throw new InvalidOperationException(
                            "El manifest no tiene archiveFileId.");
                    }


                    operation.Message =
                        isUpdate
                            ? "Actualizando modpack... 0%"
                            : "Descargando modpack... 0%";


                    if (IsSelected(
                            instanceId))
                    {
                        ShowOperationState(
                            instanceId,
                            operation);
                    }


                    Progress<double> modpackProgress =
                        new(
                            percentage =>
                            {
                                if (!IsValidProgressValue(
                                        percentage))
                                {
                                    return;
                                }


                                double safePercentage =
                                    Math.Clamp(
                                        percentage,
                                        0,
                                        100);


                                double mapped =
                                    safePercentage *
                                    0.60;


                                operation.Progress =
                                    mapped;


                                operation.Message =
                                    isUpdate
                                        ? $"Actualizando modpack... {safePercentage:0}%"
                                        : $"Descargando modpack... {safePercentage:0}%";


                                if (IsSelected(
                                        instanceId))
                                {
                                    ShowOperationState(
                                        instanceId,
                                        operation);
                                }
                            });


                    workingInstance =
                        await _modpackInstallerService
                            .InstallOrUpdateAsync(
                                manifest,
                                instance.InstallCode,
                                modpackProgress);


                    _instances[
                        instanceId] =
                        workingInstance;
                }


                // =========================================
                // 2. MINECRAFT + JAVA + LOADER
                // =========================================

                LauncherPreferences preferences =
                    await _launcherPreferencesService
                        .LoadAsync();


                double runtimeStart =
                    needsModpackFiles
                        ? 60
                        : 0;


                string runtimeStage =
                    "Preparando Minecraft...";


                double runtimePercentage =
                    0;


                Progress<double> runtimeProgress =
                    new(
                        percentage =>
                        {
                            if (!IsValidProgressValue(
                                    percentage))
                            {
                                return;
                            }


                            double safePercentage =
                                Math.Clamp(
                                    percentage,
                                    0,
                                    100);


                            runtimePercentage =
                                safePercentage;


                            double mapped =
                                runtimeStart +
                                ((100 - runtimeStart) *
                                 safePercentage /
                                 100.0);


                            if (!IsValidProgressValue(
                                    mapped))
                            {
                                return;
                            }


                            operation.Progress =
                                Math.Clamp(
                                    mapped,
                                    0,
                                    100);


                            operation.Message =
                                $"{runtimeStage} {safePercentage:0}%";


                            if (IsSelected(
                                    instanceId))
                            {
                                ShowOperationState(
                                    instanceId,
                                    operation);
                            }
                        });


                Progress<string> runtimeStatus =
                    new(
                        message =>
                        {
                            if (!string.IsNullOrWhiteSpace(
                                    message))
                            {
                                runtimeStage =
                                    message;
                            }


                            operation.Message =
                                $"{runtimeStage} {runtimePercentage:0}%";


                            if (IsSelected(
                                    instanceId))
                            {
                                ShowOperationState(
                                    instanceId,
                                    operation);
                            }
                        });


                string launchVersionName =
                    await _minecraftGameService
                        .PrepareAsync(
                            workingInstance,
                            preferences,
                            runtimeProgress,
                            runtimeStatus);


                workingInstance.RuntimePrepared =
                    true;


                workingInstance.LaunchVersionName =
                    launchVersionName;


                await _instanceService
                    .SaveAsync(
                        workingInstance);


                _instances[
                    instanceId] =
                    workingInstance;


                operation.Progress =
                    100;


                success =
                    true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    isUpdate
                        ? "Error de actualización"
                        : "Error de descarga",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                operation.IsRunning =
                    false;


                _operations.Remove(
                    instanceId);
            }


            RefreshInstanceButtons();


            if (IsSelected(
                    instanceId))
            {
                InstalledInstance current =
                    _instances.TryGetValue(
                        instanceId,
                        out InstalledInstance?
                            updated)
                        ? updated
                        : workingInstance;


                await SelectInstanceAsync(
                    current);


                if (success &&
                    IsSelected(
                        instanceId))
                {
                    StatusText.Text =
                        isUpdate
                            ? $"{current.Name} actualizado y listo para jugar."
                            : $"{current.Name} descargado y listo para jugar.";
                }
            }
        }


        // =====================================================
        // ESTADO VISUAL DE DESCARGA
        // =====================================================

        private bool TryShowRunningOperation(
            string instanceId)
        {
            if (!_operations.TryGetValue(
                    instanceId,
                    out InstanceOperationState?
                        operation) ||
                !operation.IsRunning)
            {
                return false;
            }

            ShowOperationState(
                instanceId,
                operation);

            return true;
        }


        private void ShowOperationState(
            string instanceId,
            InstanceOperationState operation)
        {
            if (!IsSelected(
                    instanceId))
            {
                return;
            }


            DownloadProgressPanel.Visibility =
                Visibility.Visible;


            /*
             * Protección final de UI:
             * aunque cualquier librería externa mande NaN o Infinity,
             * jamás se lo pasamos al ProgressBar de WPF.
             */
            double safeProgress =
                operation.Progress;


            if (!IsValidProgressValue(
                    safeProgress))
            {
                safeProgress =
                    DownloadProgressBar.Value;


                if (!IsValidProgressValue(
                        safeProgress))
                {
                    safeProgress =
                        0;
                }
            }


            safeProgress =
                Math.Clamp(
                    safeProgress,
                    0,
                    100);


            operation.Progress =
                safeProgress;


            DownloadProgressBar.Value =
                safeProgress;


            DownloadProgressText.Text =
                operation.Message;

            StatusText.Text =
                operation.Message;

            PlayButton.Content =
                !string.IsNullOrWhiteSpace(
                    operation.ButtonText)
                    ? operation.ButtonText
                    : operation.IsUpdate
                        ? "ACTUALIZANDO..."
                        : "DESCARGANDO...";

            PlayButton.IsEnabled =
                false;
        }


        private static bool IsValidProgressValue(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }


        private void HideProgress()
        {
            DownloadProgressPanel.Visibility =
                Visibility.Collapsed;

            DownloadProgressBar.Value =
                0;

            DownloadProgressText.Text =
                string.Empty;
        }


        // =====================================================
        // ACTUALIZAR ESTADO SELECCIONADO
        // =====================================================

        private async Task RefreshSelectedInstanceButtonAsync()
        {
            if (_selectedInstance == null)
            {
                ShowHome();
                return;
            }

            if (!_instances.TryGetValue(
                    _selectedInstance.Id,
                    out InstalledInstance?
                        current))
            {
                ShowHome();
                return;
            }

            await SelectInstanceAsync(
                current);
        }


        private ModpackManifest? GetRemoteManifest(
            string instanceId)
        {
            return _remoteManifests.TryGetValue(
                    instanceId,
                    out ModpackManifest?
                        manifest)
                ? manifest
                : null;
        }


        private bool IsSelected(
            string instanceId)
        {
            return
                _selectedInstance != null &&
                string.Equals(
                    _selectedInstance.Id,
                    instanceId,
                    StringComparison.OrdinalIgnoreCase);
        }


        // =====================================================
        // OPCIONES DE LA INSTALACIÓN
        // =====================================================

        private async void InstanceOptionsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedInstance == null)
            {
                return;
            }


            InstanceOptionsWindow window =
                new InstanceOptionsWindow(
                    _selectedInstance.Name)
                {
                    Owner =
                        this
                };


            bool? result =
                window.ShowDialog();


            if (result != true)
            {
                return;
            }


            switch (window.RequestedAction)
            {
                case InstanceOptionsAction.CheckUpdates:
                    await CheckSelectedInstanceUpdatesAsync(
                        showResultMessage: true);
                    break;


                case InstanceOptionsAction.VerifyIntegrity:
                    await VerifySelectedInstanceIntegrityAsync();
                    break;


                case InstanceOptionsAction.Delete:
                    await DeleteSelectedInstanceAsync();
                    break;
            }
        }


        private async Task CheckSelectedInstanceUpdatesAsync(
            bool showResultMessage)
        {
            if (_selectedInstance == null)
            {
                return;
            }


            InstalledInstance instance =
                _selectedInstance;


            if (string.IsNullOrWhiteSpace(
                    instance.InstallCode))
            {
                if (showResultMessage)
                {
                    MessageBox.Show(
                        "Esta instalación no tiene un código remoto asociado.",
                        "Actualizaciones",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }


            try
            {
                StatusText.Text =
                    "Buscando actualizaciones...";


                ModpackManifest? remote =
                    await _modpackCatalogService
                        .FindByCodeAsync(
                            instance.InstallCode);


                _remoteManifests[
                    instance.Id] =
                    remote;


                if (remote == null)
                {
                    throw new InvalidOperationException(
                        "No se encontró el manifest remoto de esta instalación.");
                }


                bool updateAvailable =
                    !string.Equals(
                        remote.Version,
                        instance.InstalledVersion,
                        StringComparison.OrdinalIgnoreCase);


                if (IsSelected(
                        instance.Id))
                {
                    if (updateAvailable)
                    {
                        PlayButton.Content =
                            "ACTUALIZAR";


                        PlayButton.IsEnabled =
                            true;


                        StatusText.Text =
                            $"Actualización disponible: " +
                            $"v{instance.InstalledVersion} → " +
                            $"v{remote.Version}";
                    }
                    else
                    {
                        StatusText.Text =
                            $"La instalación está actualizada • " +
                            $"v{instance.InstalledVersion}";
                    }
                }


                if (showResultMessage)
                {
                    MessageBox.Show(
                        updateAvailable
                            ? $"Hay una actualización disponible:\n\n" +
                              $"v{instance.InstalledVersion} → v{remote.Version}"
                            : $"No hay actualizaciones disponibles.\n\n" +
                              $"Versión actual: {instance.InstalledVersion}",
                        "Actualizaciones",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "No se pudo comprobar actualizaciones.";


                MessageBox.Show(
                    ex.Message,
                    "Actualizaciones",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private async Task VerifySelectedInstanceIntegrityAsync()
        {
            if (_selectedInstance == null)
            {
                return;
            }


            InstalledInstance instance =
                _selectedInstance;


            if (IsMinecraftRunning(
                    instance.Id))
            {
                MessageBox.Show(
                    "Cierra Minecraft antes de verificar esta instalación.",
                    "Verificar integridad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (_operations.TryGetValue(
                    instance.Id,
                    out InstanceOperationState?
                        existingOperation) &&
                existingOperation.IsRunning)
            {
                ShowOperationState(
                    instance.Id,
                    existingOperation);

                return;
            }


            MessageBoxResult answer =
                MessageBox.Show(
                    "Negative Client volverá a instalar los archivos " +
                    "administrados por el modpack y comprobará Minecraft, " +
                    "Forge y Java.\n\nLos archivos extra que hayas añadido " +
                    "no se eliminarán.\n\n¿Continuar?",
                    "Verificar integridad",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);


            if (answer !=
                MessageBoxResult.Yes)
            {
                return;
            }


            InstanceOperationState operation =
                new()
                {
                    IsRunning =
                        true,

                    IsUpdate =
                        true,

                    Progress =
                        0,

                    Message =
                        "Preparando verificación...",

                    StageMessage =
                        "Preparando verificación...",

                    ButtonText =
                        "VERIFICANDO..."
                };


            _operations[
                instance.Id] =
                operation;


            ShowOperationState(
                instance.Id,
                operation);


            try
            {
                ModpackManifest? manifest =
                    await _modpackCatalogService
                        .FindByCodeAsync(
                            instance.InstallCode);


                if (manifest == null)
                {
                    throw new InvalidOperationException(
                        "No se encontró el manifest remoto.");
                }


                _remoteManifests[
                    instance.Id] =
                    manifest;


                Progress<double> archiveProgress =
                    new(
                        percentage =>
                        {
                            if (!IsValidProgressValue(
                                    percentage))
                            {
                                return;
                            }


                            double safe =
                                Math.Clamp(
                                    percentage,
                                    0,
                                    100);


                            operation.Progress =
                                safe *
                                0.55;


                            operation.Message =
                                $"Reinstalando archivos del modpack... {safe:0}%";


                            if (IsSelected(
                                    instance.Id))
                            {
                                ShowOperationState(
                                    instance.Id,
                                    operation);
                            }
                        });


                InstalledInstance verified =
                    await _modpackInstallerService
                        .VerifyIntegrityAsync(
                            manifest,
                            instance.InstallCode,
                            instance,
                            archiveProgress);


                LauncherPreferences preferences =
                    await _launcherPreferencesService
                        .LoadAsync();


                string runtimeStage =
                    "Verificando Minecraft...";


                double runtimePercentage =
                    0;


                Progress<double> runtimeProgress =
                    new(
                        percentage =>
                        {
                            if (!IsValidProgressValue(
                                    percentage))
                            {
                                return;
                            }


                            runtimePercentage =
                                Math.Clamp(
                                    percentage,
                                    0,
                                    100);


                            operation.Progress =
                                55 +
                                (runtimePercentage *
                                 0.45);


                            operation.Message =
                                $"{runtimeStage} {runtimePercentage:0}%";


                            if (IsSelected(
                                    instance.Id))
                            {
                                ShowOperationState(
                                    instance.Id,
                                    operation);
                            }
                        });


                Progress<string> runtimeStatus =
                    new(
                        message =>
                        {
                            if (!string.IsNullOrWhiteSpace(
                                    message))
                            {
                                runtimeStage =
                                    message;
                            }


                            operation.Message =
                                $"{runtimeStage} {runtimePercentage:0}%";


                            if (IsSelected(
                                    instance.Id))
                            {
                                ShowOperationState(
                                    instance.Id,
                                    operation);
                            }
                        });


                string launchVersionName =
                    await _minecraftGameService
                        .PrepareAsync(
                            verified,
                            preferences,
                            runtimeProgress,
                            runtimeStatus);


                verified.RuntimePrepared =
                    true;


                verified.LaunchVersionName =
                    launchVersionName;


                await _instanceService
                    .SaveAsync(
                        verified);


                _instances[
                    verified.Id] =
                    verified;


                operation.Progress =
                    100;


                StatusText.Text =
                    "Integridad verificada correctamente.";


                MessageBox.Show(
                    "La instalación fue verificada y reparada.\n\n" +
                    "Los archivos extra se conservaron.",
                    "Verificación completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error de verificación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                operation.IsRunning =
                    false;


                _operations.Remove(
                    instance.Id);


                RefreshInstanceButtons();


                if (_instances.TryGetValue(
                        instance.Id,
                        out InstalledInstance?
                            current) &&
                    IsSelected(
                        instance.Id))
                {
                    await SelectInstanceAsync(
                        current);
                }
            }
        }


        private async Task DeleteSelectedInstanceAsync()
        {
            if (_selectedInstance == null)
            {
                return;
            }


            InstalledInstance instance =
                _selectedInstance;


            if (IsMinecraftRunning(
                    instance.Id))
            {
                MessageBox.Show(
                    "Cierra Minecraft antes de eliminar esta instalación.",
                    "Eliminar instalación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (_operations.TryGetValue(
                    instance.Id,
                    out InstanceOperationState?
                        operation) &&
                operation.IsRunning)
            {
                MessageBox.Show(
                    "Espera a que termine la operación actual antes de eliminar la instalación.",
                    "Eliminar instalación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            MessageBoxResult answer =
                MessageBox.Show(
                    $"¿Eliminar completamente {instance.Name} de este equipo?\n\n" +
                    "Esta acción elimina la carpeta de la instancia, incluidos " +
                    "mundos, capturas y archivos locales que haya dentro.",
                    "Eliminar instalación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);


            if (answer !=
                MessageBoxResult.Yes)
            {
                return;
            }


            try
            {
                string instanceDirectory =
                    _instanceService
                        .GetInstanceDirectory(
                            instance.Id);


                if (Directory.Exists(
                        instanceDirectory))
                {
                    await Task.Run(
                        () =>
                            Directory.Delete(
                                instanceDirectory,
                                recursive: true));
                }


                string cacheDirectory =
                    Path.Combine(
                        ImageCacheService.CacheRoot,
                        instance.Id);


                if (Directory.Exists(
                        cacheDirectory))
                {
                    try
                    {
                        Directory.Delete(
                            cacheDirectory,
                            recursive: true);
                    }
                    catch
                    {
                    }
                }


                _instances.Remove(
                    instance.Id);


                _remoteManifests.Remove(
                    instance.Id);


                _operations.Remove(
                    instance.Id);


                if (string.Equals(
                        _lastPlayedInstanceId,
                        instance.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _lastPlayedInstanceId =
                        null;


                    await _launcherStateService
                        .ClearLastPlayedInstanceAsync();
                }


                _selectedInstance =
                    null;


                RefreshInstanceButtons();


                ShowHome();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "No se pudo eliminar la instalación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =====================================================
        // CUENTA RÁPIDA EN INSTALACIONES
        // =====================================================

        private async void AccountQuickButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedInstance == null)
            {
                return;
            }


            await RefreshQuickAccountUiAsync();


            AccountQuickPopup.IsOpen =
                !AccountQuickPopup.IsOpen;
        }


        private async Task RefreshQuickAccountUiAsync()
        {
            List<MicrosoftAccountInfo> accounts =
                _microsoftAccountService
                    .GetAccounts();


            MicrosoftAccountInfo? selected =
                accounts.FirstOrDefault(
                    account =>
                        account.IsSelected);


            QuickCurrentAccountNameText.Text =
                selected?.Username ??
                "Sin cuenta";


            QuickCurrentHeadImage.Source =
                null;


            QuickCurrentHeadImage.Visibility =
                Visibility.Collapsed;


            QuickCurrentHeadFallback.Visibility =
                Visibility.Visible;


            if (selected != null &&
                !string.IsNullOrWhiteSpace(
                    selected.Uuid))
            {
                string? selectedHeadPath =
                    await _minecraftSkinService
                        .GetHeadPathAsync(
                            selected.Uuid);


                if (!string.IsNullOrWhiteSpace(
                        selectedHeadPath) &&
                    File.Exists(
                        selectedHeadPath))
                {
                    try
                    {
                        QuickCurrentHeadImage.Source =
                            LoadBitmap(
                                selectedHeadPath);


                        QuickCurrentHeadImage.Visibility =
                            Visibility.Visible;


                        QuickCurrentHeadFallback.Visibility =
                            Visibility.Collapsed;
                    }
                    catch
                    {
                    }
                }
            }


            QuickAccountsListPanel.Children.Clear();


            foreach (MicrosoftAccountInfo account in
                accounts)
            {
                Button accountButton =
                    new()
                    {
                        Tag =
                            account.Identifier,

                        Style =
                            (Style)FindResource(
                                "QuickMenuButtonStyle"),

                        ToolTip =
                            account.IsSelected
                                ? "Cuenta actual"
                                : "Usar esta cuenta"
                    };


                Grid contentGrid =
                    new();


                contentGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                38)
                    });


                contentGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                1,
                                GridUnitType.Star)
                    });


                contentGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width =
                            GridLength.Auto
                    });


                Grid headGrid =
                    new()
                    {
                        Width =
                            32,

                        Height =
                            32,

                        VerticalAlignment =
                            VerticalAlignment.Center
                    };


                Border fallback =
                    new()
                    {
                        Background =
                            new SolidColorBrush(
                                Color.FromRgb(
                                    40,
                                    49,
                                    58)),

                        CornerRadius =
                            new CornerRadius(
                                5)
                    };


                TextBlock fallbackText =
                    new()
                    {
                        Text =
                            "?",

                        Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(
                                    170,
                                    179,
                                    188)),

                        FontWeight =
                            FontWeights.Bold,

                        HorizontalAlignment =
                            HorizontalAlignment.Center,

                        VerticalAlignment =
                            VerticalAlignment.Center
                    };


                fallback.Child =
                    fallbackText;


                headGrid.Children.Add(
                    fallback);


                Image headImage =
                    new()
                    {
                        Width =
                            32,

                        Height =
                            32,

                        Stretch =
                            Stretch.Fill,

                        SnapsToDevicePixels =
                            true,

                        Visibility =
                            Visibility.Collapsed
                    };


                RenderOptions.SetBitmapScalingMode(
                    headImage,
                    BitmapScalingMode.NearestNeighbor);


                headGrid.Children.Add(
                    headImage);


                Grid.SetColumn(
                    headGrid,
                    0);


                contentGrid.Children.Add(
                    headGrid);


                TextBlock nameText =
                    new()
                    {
                        Text =
                            account.Username,

                        Foreground =
                            Brushes.White,

                        FontSize =
                            12,

                        FontWeight =
                            account.IsSelected
                                ? FontWeights.SemiBold
                                : FontWeights.Normal,

                        VerticalAlignment =
                            VerticalAlignment.Center,

                        Margin =
                            new Thickness(
                                7,
                                0,
                                8,
                                0)
                    };


                Grid.SetColumn(
                    nameText,
                    1);


                contentGrid.Children.Add(
                    nameText);


                if (account.IsSelected)
                {
                    TextBlock selectedMark =
                        new()
                        {
                            Text =
                                "✓",

                            Foreground =
                                AccentBrush,

                            FontSize =
                                13,

                            FontWeight =
                                FontWeights.Bold,

                            VerticalAlignment =
                                VerticalAlignment.Center
                        };


                    Grid.SetColumn(
                        selectedMark,
                        2);


                    contentGrid.Children.Add(
                        selectedMark);
                }


                accountButton.Content =
                    contentGrid;


                accountButton.Click +=
                    QuickAccountEntry_Click;


                QuickAccountsListPanel.Children.Add(
                    accountButton);


                if (!string.IsNullOrWhiteSpace(
                        account.Uuid))
                {
                    _ =
                        LoadQuickAccountHeadAsync(
                            account,
                            headImage,
                            fallback);
                }
            }


            bool canAdd =
                accounts.Count <
                MicrosoftAccountService.MaxAccounts;


            QuickAddAccountButton.IsEnabled =
                canAdd;


            QuickAddAccountButton.Content =
                canAdd
                    ? "+  Añadir otra cuenta"
                    : "Máximo de 3 cuentas";
        }


        private async Task LoadQuickAccountHeadAsync(
            MicrosoftAccountInfo account,
            Image image,
            Border fallback)
        {
            string? headPath =
                await _minecraftSkinService
                    .GetHeadPathAsync(
                        account.Uuid);


            if (string.IsNullOrWhiteSpace(
                    headPath) ||
                !File.Exists(
                    headPath))
            {
                return;
            }


            try
            {
                image.Source =
                    LoadBitmap(
                        headPath);


                image.Visibility =
                    Visibility.Visible;


                fallback.Visibility =
                    Visibility.Collapsed;
            }
            catch
            {
            }
        }


        private async void QuickAccountEntry_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not string identifier)
            {
                return;
            }


            AccountQuickPopup.IsOpen =
                false;


            bool restored =
                await _microsoftAccountService
                    .SelectAccountAsync(
                        identifier);


            if (!restored)
            {
                MessageBoxResult answer =
                    MessageBox.Show(
                        "La sesión guardada de esta cuenta ya no es válida.\n\n" +
                        "¿Quieres volver a iniciar sesión ahora?",
                        "Sesión expirada",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);


                if (answer ==
                    MessageBoxResult.Yes)
                {
                    try
                    {
                        await _microsoftAccountService
                            .ReauthenticateAccountAsync(
                                identifier);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            ex.Message,
                            "Microsoft",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }


            RefreshMicrosoftWarning();


            await RefreshQuickAccountUiAsync();
        }


        private async void QuickAddAccountButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_microsoftAccountService
                    .GetAccounts()
                    .Count >=
                MicrosoftAccountService.MaxAccounts)
            {
                return;
            }


            AccountQuickPopup.IsOpen =
                false;


            try
            {
                await _microsoftAccountService
                    .AddAccountInteractivelyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Microsoft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }


            RefreshMicrosoftWarning();


            await RefreshQuickAccountUiAsync();
        }


        // =====================================================
        // CUENTA / CONFIGURACIÓN
        // =====================================================

        private async void AccountSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SettingsWindow window =
                new SettingsWindow(
                    _microsoftAccountService,
                    _launcherPreferencesService)
                {
                    Owner =
                        this
                };


            window.ShowDialog();


            RefreshMicrosoftWarning();


            await RefreshQuickAccountUiAsync();
        }


        private void RefreshMicrosoftWarning()
        {
            MicrosoftWarningBorder.Visibility =
                _microsoftAccountService.IsSignedIn
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }


        // =====================================================
        // SELECCIÓN VISUAL
        // =====================================================

        private void UpdateSidebarSelection()
        {
            HomeButton.BorderBrush =
                _selectedInstance == null
                    ? AccentBrush
                    : NormalBorderBrush;

            foreach (
                KeyValuePair<string, Button> pair
                in _instanceButtons)
            {
                bool selected =
                    _selectedInstance != null &&
                    string.Equals(
                        pair.Key,
                        _selectedInstance.Id,
                        StringComparison.OrdinalIgnoreCase);

                pair.Value.BorderBrush =
                    selected
                        ? AccentBrush
                        : NormalBorderBrush;
            }
        }
    }
}
