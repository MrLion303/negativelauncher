using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow : Window
    {
        private readonly ModpackCatalogService _modpackCatalogService;
        private readonly InstanceService _instanceService;
        private readonly ModpackInstallerService _modpackInstallerService;

        private readonly Dictionary<string, InstalledInstance> _instances =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Button> _instanceButtons =
            new(StringComparer.OrdinalIgnoreCase);

        private InstalledInstance? _selectedInstance;
        private ModpackManifest? _selectedRemoteManifest;

        private static readonly Brush AccentBrush =
            new SolidColorBrush(Color.FromRgb(79, 195, 215));

        private static readonly Brush NormalBorderBrush =
            new SolidColorBrush(Color.FromRgb(70, 81, 92));


        public MainWindow()
        {
            InitializeComponent();

            _modpackCatalogService =
                new ModpackCatalogService();

            _instanceService =
                new InstanceService();

            _modpackInstallerService =
                new ModpackInstallerService(
                    new GoogleDriveService(),
                    _instanceService);

            Loaded += MainWindow_Loaded;
        }


        // =====================================================
        // INICIO
        // =====================================================

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            List<InstalledInstance> instances =
                await _instanceService.LoadAllAsync();

            foreach (InstalledInstance instance in instances)
            {
                _instances[instance.Id] = instance;
            }

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

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
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
                WindowState == WindowState.Maximized
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
            _selectedInstance = null;
            _selectedRemoteManifest = null;

            MainTitleText.Text = "NEGATIVE STUDIOS";

            SelectedModpackText.Text =
                "Selecciona o instala una instancia";

            StatusText.Text =
                "Negative Client listo";

            PlayButton.Content =
                "JUGAR";

            PlayButton.IsEnabled =
                false;

            HideProgress();

            UpdateSidebarSelection();
        }


        // =====================================================
        // INSTANCIAS
        // =====================================================

        private void RefreshInstanceButtons()
        {
            ModpackList.Children.Clear();
            _instanceButtons.Clear();

            foreach (InstalledInstance instance in
                _instances.Values.OrderBy(instance => instance.Name))
            {
                string letter =
                    string.IsNullOrWhiteSpace(instance.Name)
                        ? "?"
                        : instance.Name[..1].ToUpperInvariant();

                Button button =
                    new()
                    {
                        Content = letter,
                        Tag = instance.Id,
                        ToolTip = instance.Name,
                        Style = (Style)FindResource("CircleButton"),
                        Margin = new Thickness(0, 0, 0, 10),
                        BorderBrush = NormalBorderBrush
                    };

                button.Click += InstanceButton_Click;

                ModpackList.Children.Add(button);
                _instanceButtons[instance.Id] = button;
            }

            UpdateSidebarSelection();
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

            await SelectInstanceAsync(instance);
        }


        private async Task SelectInstanceAsync(
            InstalledInstance instance)
        {
            _selectedInstance = instance;
            _selectedRemoteManifest = null;

            HideProgress();

            MainTitleText.Text =
                instance.Name.ToUpperInvariant();

            string loaderText =
                instance.Loader;

            if (!string.IsNullOrWhiteSpace(instance.LoaderVersion))
            {
                loaderText += " " + instance.LoaderVersion;
            }

            SelectedModpackText.Text =
                $"Minecraft {instance.MinecraftVersion}  •  {loaderText}";

            UpdateSidebarSelection();

            if (string.IsNullOrWhiteSpace(instance.InstallCode))
            {
                if (instance.IsInstalled)
                {
                    PlayButton.Content = "JUGAR";
                    PlayButton.IsEnabled = true;

                    StatusText.Text =
                        $"Instalado • v{instance.InstalledVersion}";
                }
                else
                {
                    PlayButton.Content = "DESCARGAR";
                    PlayButton.IsEnabled = false;

                    StatusText.Text =
                        "La instancia no tiene código de instalación.";
                }

                return;
            }

            try
            {
                StatusText.Text =
                    "Buscando actualizaciones...";

                ModpackManifest? remote =
                    await _modpackCatalogService.FindByCodeAsync(
                        instance.InstallCode);

                _selectedRemoteManifest = remote;

                if (remote == null)
                {
                    if (instance.IsInstalled)
                    {
                        PlayButton.Content = "JUGAR";
                        PlayButton.IsEnabled = true;

                        StatusText.Text =
                            $"Instalado • v{instance.InstalledVersion}";
                    }
                    else
                    {
                        PlayButton.Content = "DESCARGAR";
                        PlayButton.IsEnabled = false;

                        StatusText.Text =
                            "No se encontró la instalación remota.";
                    }

                    return;
                }

                if (!instance.IsInstalled)
                {
                    PlayButton.Content = "DESCARGAR";
                    PlayButton.IsEnabled = true;

                    StatusText.Text =
                        $"Disponible • v{remote.Version} • sin descargar";

                    return;
                }

                if (!string.Equals(
                        remote.Version,
                        instance.InstalledVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    PlayButton.Content = "ACTUALIZAR";
                    PlayButton.IsEnabled = true;

                    StatusText.Text =
                        $"Actualización disponible: " +
                        $"v{instance.InstalledVersion} → " +
                        $"v{remote.Version}";
                }
                else
                {
                    PlayButton.Content = "JUGAR";
                    PlayButton.IsEnabled = true;

                    StatusText.Text =
                        $"Actualizado • v{instance.InstalledVersion}";
                }
            }
            catch
            {
                _selectedRemoteManifest = null;

                if (instance.IsInstalled)
                {
                    PlayButton.Content = "JUGAR";
                    PlayButton.IsEnabled = true;

                    StatusText.Text =
                        $"Modo sin conexión • v{instance.InstalledVersion}";
                }
                else
                {
                    PlayButton.Content = "DESCARGAR";
                    PlayButton.IsEnabled = false;

                    StatusText.Text =
                        "Sin conexión. No se puede descargar esta instancia.";
                }
            }
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
                    Owner = this
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
                AddModpackButton.IsEnabled = false;
                PlayButton.IsEnabled = false;

                StatusText.Text =
                    $"Buscando instalación {code}...";

                ModpackManifest? manifest =
                    await _modpackCatalogService.FindByCodeAsync(code);

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

                // Si ya existe, no se vuelve a crear.
                if (_instances.TryGetValue(
                        manifest.Id,
                        out InstalledInstance? existingInstance))
                {
                    await SelectInstanceAsync(existingInstance);
                    return;
                }

                // IMPORTANTE:
                // aquí solo registramos la instancia.
                // NO descargamos todavía el modpack.
                InstalledInstance instance =
                    new()
                    {
                        Id = manifest.Id,
                        Name = manifest.Name,
                        InstallCode = code,

                        IsInstalled = false,
                        InstalledVersion = string.Empty,

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

                await _instanceService.SaveAsync(instance);

                _instances[instance.Id] = instance;

                RefreshInstanceButtons();

                await SelectInstanceAsync(instance);

                StatusText.Text =
                    $"{instance.Name} añadido. Pulsa DESCARGAR para instalarlo.";
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
                AddModpackButton.IsEnabled = true;

                if (_selectedInstance != null)
                {
                    PlayButton.IsEnabled =
                        _selectedInstance.IsInstalled ||
                        _selectedRemoteManifest != null;
                }
            }
        }


        // =====================================================
        // DESCARGAR / ACTUALIZAR / JUGAR
        // =====================================================

        private async void PlayButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedInstance == null)
            {
                return;
            }

            if (!_selectedInstance.IsInstalled)
            {
                await DownloadOrUpdateSelectedInstanceAsync(
                    isUpdate: false);

                return;
            }

            if (_selectedRemoteManifest != null &&
                !string.Equals(
                    _selectedRemoteManifest.Version,
                    _selectedInstance.InstalledVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                await DownloadOrUpdateSelectedInstanceAsync(
                    isUpdate: true);

                return;
            }

            // Minecraft real se conectará en la siguiente fase.
            StatusText.Text =
                "Instancia lista para iniciar Minecraft Java.";

            MessageBox.Show(
                "La instancia está descargada y actualizada.\n\n" +
                "El siguiente paso será conectar Microsoft " +
                "y lanzar Minecraft Java.",
                "Negative Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        private async Task DownloadOrUpdateSelectedInstanceAsync(
            bool isUpdate)
        {
            if (_selectedInstance == null)
            {
                return;
            }

            try
            {
                ModpackManifest? manifest =
                    _selectedRemoteManifest;

                if (manifest == null)
                {
                    StatusText.Text =
                        "Consultando instalación...";

                    manifest =
                        await _modpackCatalogService.FindByCodeAsync(
                            _selectedInstance.InstallCode);

                    _selectedRemoteManifest =
                        manifest;
                }

                if (manifest == null)
                {
                    throw new InvalidOperationException(
                        "No se encontró el manifest remoto de esta instalación.");
                }

                if (string.IsNullOrWhiteSpace(manifest.ArchiveFileId))
                {
                    throw new InvalidOperationException(
                        "El manifest no tiene archiveFileId.");
                }

                AddModpackButton.IsEnabled = false;
                PlayButton.IsEnabled = false;

                DownloadProgressPanel.Visibility =
                    Visibility.Visible;

                DownloadProgressBar.Value =
                    0;

                PlayButton.Content =
                    isUpdate
                        ? "ACTUALIZANDO..."
                        : "DESCARGANDO...";

                DownloadProgressText.Text =
                    isUpdate
                        ? "Actualizando... 0%"
                        : "Descargando... 0%";

                Progress<double> progress =
                    new(
                        percentage =>
                        {
                            double safePercentage =
                                Math.Clamp(percentage, 0, 100);

                            DownloadProgressBar.Value =
                                safePercentage;

                            DownloadProgressText.Text =
                                isUpdate
                                    ? $"Actualizando... {safePercentage:0}%"
                                    : $"Descargando... {safePercentage:0}%";
                        });

                InstalledInstance installedInstance =
                    await _modpackInstallerService
                        .InstallOrUpdateAsync(
                            manifest,
                            _selectedInstance.InstallCode,
                            progress);

                _instances[installedInstance.Id] =
                    installedInstance;

                _selectedInstance =
                    installedInstance;

                RefreshInstanceButtons();

                await SelectInstanceAsync(
                    installedInstance);

                StatusText.Text =
                    isUpdate
                        ? $"{installedInstance.Name} actualizado correctamente."
                        : $"{installedInstance.Name} descargado correctamente.";
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

                StatusText.Text =
                    isUpdate
                        ? "No se pudo actualizar la instalación."
                        : "No se pudo descargar la instalación.";
            }
            finally
            {
                HideProgress();

                AddModpackButton.IsEnabled = true;

                if (_selectedInstance != null)
                {
                    PlayButton.IsEnabled =
                        _selectedInstance.IsInstalled ||
                        _selectedRemoteManifest != null;

                    if (_selectedInstance.IsInstalled &&
                        _selectedRemoteManifest != null &&
                        !string.Equals(
                            _selectedRemoteManifest.Version,
                            _selectedInstance.InstalledVersion,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        PlayButton.Content =
                            "ACTUALIZAR";
                    }
                    else if (_selectedInstance.IsInstalled)
                    {
                        PlayButton.Content =
                            "JUGAR";
                    }
                    else
                    {
                        PlayButton.Content =
                            "DESCARGAR";
                    }
                }
            }
        }


        // =====================================================
        // CUENTA / CONFIGURACIÓN
        // =====================================================

        private void AccountSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "Aquí irá la cuenta Microsoft, RAM, " +
                "Java y otras opciones del launcher.",
                "Cuenta y configuración",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        // =====================================================
        // UI AUXILIAR
        // =====================================================

        private void HideProgress()
        {
            DownloadProgressPanel.Visibility =
                Visibility.Collapsed;

            DownloadProgressBar.Value =
                0;

            DownloadProgressText.Text =
                string.Empty;
        }


        private void UpdateSidebarSelection()
        {
            HomeButton.BorderBrush =
                _selectedInstance == null
                    ? AccentBrush
                    : NormalBorderBrush;

            foreach (KeyValuePair<string, Button> pair
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
