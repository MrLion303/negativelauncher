using System;
using System.Collections.Generic;
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

            RefreshMicrosoftWarning();

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

            ClearInstanceBackground();

            MainTitleText.Text =
                "NEGATIVE STUDIOS";

            SelectedModpackText.Text =
                "Selecciona o instala una instancia";

            HideProgress();

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

            ClearInstanceBackground();

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
                !File.Exists(imagePath))
            {
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
            }
            catch
            {
                ClearInstanceBackground();
            }
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
            // En HOME abre la última instancia jugada.
            if (_selectedInstance == null)
            {
                await OpenLastPlayedInstanceAsync();

                return;
            }


            string instanceId =
                _selectedInstance.Id;


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


                var process =
                    await _minecraftGameService
                        .LaunchAsync(
                            _selectedInstance,
                            validSession,
                            preferences);


                _lastPlayedInstanceId =
                    _selectedInstance.Id;


                await _launcherStateService
                    .SetLastPlayedInstanceIdAsync(
                        _selectedInstance.Id);


                StatusText.Text =
                    $"Minecraft iniciado como {validSession.Username}.";


                PlayButton.Content =
                    "JUGAR";

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
                                if (IsSelected(instanceId))
                                {
                                    StatusText.Text =
                                        "Minecraft se cerró.";

                                    PlayButton.Content =
                                        "JUGAR";

                                    PlayButton.IsEnabled =
                                        true;
                                }
                            });
                    };
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
                            : "Preparando descarga..."
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
                                double mapped =
                                    Math.Clamp(
                                        percentage,
                                        0,
                                        100) *
                                    0.60;


                                operation.Progress =
                                    mapped;


                                operation.Message =
                                    isUpdate
                                        ? $"Actualizando modpack... {percentage:0}%"
                                        : $"Descargando modpack... {percentage:0}%";


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


                Progress<double> runtimeProgress =
                    new(
                        percentage =>
                        {
                            double mapped =
                                runtimeStart +
                                ((100 - runtimeStart) *
                                 Math.Clamp(
                                     percentage,
                                     0,
                                     100) /
                                 100.0);


                            operation.Progress =
                                mapped;


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
                            operation.Message =
                                message;


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

            DownloadProgressBar.Value =
                operation.Progress;

            DownloadProgressText.Text =
                operation.Message;

            StatusText.Text =
                operation.Message;

            PlayButton.Content =
                operation.IsUpdate
                    ? "ACTUALIZANDO..."
                    : "DESCARGANDO...";

            PlayButton.IsEnabled =
                false;
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
        // CUENTA / CONFIGURACIÓN
        // =====================================================

        private void AccountSettingsButton_Click(
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
