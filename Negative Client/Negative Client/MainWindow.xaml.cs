using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
        private readonly ModpackCatalogService
            _modpackCatalogService;

        private readonly InstanceService
            _instanceService;

        private readonly ModpackInstallerService
            _modpackInstallerService;


        private readonly Dictionary<string, InstalledInstance>
            _instances =
                new(StringComparer.OrdinalIgnoreCase);


        private readonly Dictionary<string, Button>
            _instanceButtons =
                new(StringComparer.OrdinalIgnoreCase);


        private InstalledInstance?
            _selectedInstance;


        private ModpackManifest?
            _selectedRemoteManifest;


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
                await _instanceService
                    .LoadAllAsync();


            foreach (InstalledInstance instance in
                instances)
            {
                _instances[instance.Id] =
                    instance;
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

            _selectedRemoteManifest =
                null;


            MainTitleText.Text =
                "NEGATIVE STUDIOS";


            SelectedModpackText.Text =
                "Selecciona o instala una instancia";


            StatusText.Text =
                "Negative Client listo";


            PlayButton.Content =
                "PLAY";

            PlayButton.IsEnabled =
                false;


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
                _instances.Values
                    .OrderBy(instance =>
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


            await SelectInstanceAsync(
                instance);
        }


        private async System.Threading.Tasks.Task
            SelectInstanceAsync(
                InstalledInstance instance)
        {
            _selectedInstance =
                instance;


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


            PlayButton.Content =
                "PLAY";

            PlayButton.IsEnabled =
                true;


            StatusText.Text =
                $"Instalado: v{instance.InstalledVersion}";


            UpdateSidebarSelection();


            if (string.IsNullOrWhiteSpace(
                    instance.InstallCode))
            {
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


                _selectedRemoteManifest =
                    remote;


                if (remote == null)
                {
                    StatusText.Text =
                        $"Instalado: v{instance.InstalledVersion}";

                    return;
                }


                if (!string.Equals(
                        remote.Version,
                        instance.InstalledVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    PlayButton.Content =
                        "ACTUALIZAR";


                    StatusText.Text =
                        $"Actualización disponible: " +
                        $"v{instance.InstalledVersion} → " +
                        $"v{remote.Version}";
                }
                else
                {
                    PlayButton.Content =
                        "PLAY";


                    StatusText.Text =
                        $"Actualizado • v" +
                        instance.InstalledVersion;
                }
            }
            catch
            {
                // Si no hay internet, podemos seguir
                // mostrando la instalación local.

                _selectedRemoteManifest =
                    null;


                PlayButton.Content =
                    "PLAY";


                StatusText.Text =
                    $"Modo sin conexión • v" +
                    instance.InstalledVersion;
            }
        }


        // =====================================================
        // INSTALAR DESDE CÓDIGO
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
                AddModpackButton.IsEnabled =
                    false;

                PlayButton.IsEnabled =
                    false;


                StatusText.Text =
                    $"Buscando instalación {code}...";


                ModpackManifest? manifest =
                    await _modpackCatalogService
                        .FindByCodeAsync(code);


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


                // Si ya existe, simplemente la seleccionamos.

                if (_instances.TryGetValue(
                        manifest.Id,
                        out InstalledInstance?
                            existingInstance))
                {
                    await SelectInstanceAsync(
                        existingInstance);

                    return;
                }


                if (string.IsNullOrWhiteSpace(
                        manifest.ArchiveFileId))
                {
                    throw new InvalidOperationException(
                        "El manifest existe, pero todavía " +
                        "no tiene archiveFileId.");
                }


                Progress<double> progress =
                    new(
                        percentage =>
                        {
                            StatusText.Text =
                                $"Descargando " +
                                $"{manifest.Name}... " +
                                $"{percentage:0}%";
                        });


                InstalledInstance instance =
                    await _modpackInstallerService
                        .InstallOrUpdateAsync(
                            manifest,
                            code,
                            progress);


                _instances[instance.Id] =
                    instance;


                RefreshInstanceButtons();


                await SelectInstanceAsync(
                    instance);


                StatusText.Text =
                    $"{instance.Name} instalado correctamente.";
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    "No se pudo descargar el modpack.\n\n" +
                    ex.Message,
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);


                StatusText.Text =
                    "Error de descarga.";
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
                    "Error de instalación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);


                StatusText.Text =
                    "No se pudo instalar el modpack.";
            }
            finally
            {
                AddModpackButton.IsEnabled =
                    true;


                if (_selectedInstance != null)
                {
                    PlayButton.IsEnabled =
                        true;
                }
            }
        }


        // =====================================================
        // PLAY / ACTUALIZAR
        // =====================================================

        private async void PlayButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedInstance == null)
            {
                return;
            }


            // =============================================
            // ACTUALIZAR
            // =============================================

            if (_selectedRemoteManifest != null &&
                !string.Equals(
                    _selectedRemoteManifest.Version,
                    _selectedInstance.InstalledVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    PlayButton.IsEnabled =
                        false;

                    AddModpackButton.IsEnabled =
                        false;


                    Progress<double> progress =
                        new(
                            percentage =>
                            {
                                StatusText.Text =
                                    $"Actualizando... " +
                                    $"{percentage:0}%";
                            });


                    InstalledInstance updatedInstance =
                        await _modpackInstallerService
                            .InstallOrUpdateAsync(
                                _selectedRemoteManifest,
                                _selectedInstance.InstallCode,
                                progress);


                    _instances[
                        updatedInstance.Id] =
                        updatedInstance;


                    RefreshInstanceButtons();


                    await SelectInstanceAsync(
                        updatedInstance);


                    StatusText.Text =
                        "Actualización completada.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.Message,
                        "Error de actualización",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    AddModpackButton.IsEnabled =
                        true;

                    PlayButton.IsEnabled =
                        true;
                }


                return;
            }


            // =============================================
            // MINECRAFT
            // =============================================

            StatusText.Text =
                "Instancia preparada para Minecraft Java.";


            MessageBox.Show(
                "La instalación está lista.\n\n" +
                "El siguiente paso será conectar la " +
                "cuenta Microsoft e iniciar Minecraft " +
                "Java desde esta instancia.",
                "Negative Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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