using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Negative_Client.Models;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private const string DeveloperInstanceId =
            "__developer_vanilla__";


        private bool
            _developerModeEnabled;


        private bool
            _developerPageActive;


        private bool
            _developerVersionsLoaded;


        private Button?
            _developerInstanceButton;


        private List<string>
            _developerMinecraftVersions =
                new();


        private async Task RefreshDeveloperModeStateAsync()
        {
            LauncherPreferences preferences =
                await _launcherPreferencesService
                    .LoadAsync();


            bool previousState =
                _developerModeEnabled;


            _developerModeEnabled =
                preferences.DeveloperMode;


            if (previousState &&
                !_developerModeEnabled)
            {
                _developerVersionsLoaded =
                    false;


                _developerMinecraftVersions.Clear();


                DeveloperVersionComboBox.ItemsSource =
                    null;
            }
        }


        private void AppendDeveloperInstanceButtonIfEnabled()
        {
            _developerInstanceButton =
                null;


            if (!_developerModeEnabled)
            {
                return;
            }


            TextBlock text =
                new()
                {
                    Text =
                        "DEV",

                    Foreground =
                        Brushes.White,

                    FontSize =
                        11,

                    FontWeight =
                        FontWeights.Bold,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };


            Button button =
                new()
                {
                    Content =
                        text,

                    ToolTip =
                        "Minecraft Vanilla — Modo desarrollador",

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
                        _developerPageActive
                            ? AccentBrush
                            : NormalBorderBrush
                };


            button.Click +=
                DeveloperInstanceButton_Click;


            ModpackList.Children.Add(
                button);


            _developerInstanceButton =
                button;
        }


        private async void DeveloperInstanceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await ShowDeveloperVanillaPageAsync(
                reloadVersions: false);
        }


        private async Task ShowDeveloperVanillaPageAsync(
            bool reloadVersions)
        {
            if (!_developerModeEnabled)
            {
                ShowHome();

                return;
            }


            ExitGalleryMode();


            _developerPageActive =
                true;


            _selectedInstance =
                null;


            ClearHomeBackground();

            ClearInstanceBackground();


            AccountQuickPopup.IsOpen =
                false;


            AccountQuickRoot.Visibility =
                Visibility.Visible;


            InstanceOptionsButton.Visibility =
                Visibility.Collapsed;


            MainCenterPanel.Visibility =
                Visibility.Visible;


            MainTitleText.Text =
                "MINECRAFT VANILLA";


            SelectedModpackText.Text =
                "Modo desarrollador";


            DeveloperVersionPanel.Visibility =
                Visibility.Visible;


            PlayButton.Visibility =
                Visibility.Visible;


            StatusText.Visibility =
                Visibility.Visible;


            RefreshMicrosoftWarning();


            _ =
                RefreshQuickAccountUiAsync();


            HideProgress();


            if (reloadVersions ||
                !_developerVersionsLoaded)
            {
                PlayButton.IsEnabled =
                    false;


                PlayButton.Content =
                    "CARGANDO...";


                StatusText.Text =
                    "Cargando versiones oficiales de Minecraft...";


                try
                {
                    var catalog =
                        await _minecraftGameService
                            .GetAvailableVanillaVersionsAsync(
                                DeveloperInstanceId);


                    _developerMinecraftVersions =
                        catalog.Versions
                            .ToList();


                    DeveloperVersionComboBox.ItemsSource =
                        _developerMinecraftVersions;


                    LauncherPreferences preferences =
                        await _launcherPreferencesService
                            .LoadAsync();


                    string desired =
                        preferences.DeveloperMinecraftVersion;


                    if (string.IsNullOrWhiteSpace(
                            desired) ||
                        !_developerMinecraftVersions.Contains(
                            desired,
                            StringComparer.OrdinalIgnoreCase))
                    {
                        desired =
                            catalog.LatestRelease;
                    }


                    if (string.IsNullOrWhiteSpace(
                            desired))
                    {
                        desired =
                            _developerMinecraftVersions
                                .FirstOrDefault() ??
                            string.Empty;
                    }


                    DeveloperVersionComboBox.SelectedItem =
                        _developerMinecraftVersions
                            .FirstOrDefault(
                                version =>
                                    string.Equals(
                                        version,
                                        desired,
                                        StringComparison.OrdinalIgnoreCase));


                    _developerVersionsLoaded =
                        true;
                }
                catch (Exception ex)
                {
                    _developerVersionsLoaded =
                        false;


                    PlayButton.Content =
                        "JUGAR";


                    PlayButton.IsEnabled =
                        false;


                    StatusText.Text =
                        "No se pudieron cargar las versiones de Minecraft.";


                    MessageBox.Show(
                        ex.Message,
                        "Modo desarrollador",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);


                    UpdateSidebarSelection();

                    return;
                }
            }


            if (IsMinecraftRunning(
                    DeveloperInstanceId))
            {
                PlayButton.Content =
                    "CERRAR";


                PlayButton.IsEnabled =
                    true;


                StatusText.Text =
                    "Minecraft Vanilla está abierto.";
            }
            else if (IsMinecraftRunning())
            {
                PlayButton.Content =
                    "JUEGO ABIERTO";


                PlayButton.IsEnabled =
                    false;


                StatusText.Text =
                    "Cierra la otra instancia antes de iniciar Minecraft Vanilla.";
            }
            else
            {
                string? selectedVersion =
                    DeveloperVersionComboBox
                        .SelectedItem
                        as string;


                PlayButton.Content =
                    "JUGAR";


                PlayButton.IsEnabled =
                    !string.IsNullOrWhiteSpace(
                        selectedVersion);


                StatusText.Text =
                    !string.IsNullOrWhiteSpace(
                        selectedVersion)
                        ? $"Vanilla {selectedVersion} listo para preparar."
                        : "Selecciona una versión de Minecraft.";
            }


            UpdateSidebarSelection();
        }


        private void DeactivateDeveloperPageVisuals()
        {
            if (!_developerPageActive &&
                DeveloperVersionPanel.Visibility ==
                Visibility.Collapsed)
            {
                return;
            }


            _developerPageActive =
                false;


            DeveloperVersionPanel.Visibility =
                Visibility.Collapsed;


            if (_developerInstanceButton !=
                null)
            {
                _developerInstanceButton.BorderBrush =
                    NormalBorderBrush;
            }
        }


        private async void DeveloperVersionComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!_developerPageActive ||
                DeveloperVersionComboBox.SelectedItem
                    is not string selectedVersion)
            {
                return;
            }


            LauncherPreferences preferences =
                await _launcherPreferencesService
                    .LoadAsync();


            preferences.DeveloperMinecraftVersion =
                selectedVersion;


            await _launcherPreferencesService
                .SaveAsync(
                    preferences);


            if (!IsMinecraftRunning())
            {
                PlayButton.Content =
                    "JUGAR";


                PlayButton.IsEnabled =
                    true;


                StatusText.Text =
                    $"Vanilla {selectedVersion} listo para preparar.";
            }
        }


        private async Task HandleDeveloperPlayButtonAsync()
        {
            if (!_developerModeEnabled)
            {
                ShowHome();

                return;
            }


            if (IsMinecraftRunning(
                    DeveloperInstanceId))
            {
                await CloseRunningMinecraftAsync();


                await ShowDeveloperVanillaPageAsync(
                    reloadVersions: false);

                return;
            }


            if (IsMinecraftRunning())
            {
                MessageBox.Show(
                    "Ya hay otra instancia de Minecraft abierta.",
                    "Minecraft ya está abierto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            if (DeveloperVersionComboBox.SelectedItem
                is not string version ||
                string.IsNullOrWhiteSpace(
                    version))
            {
                MessageBox.Show(
                    "Selecciona una versión de Minecraft.",
                    "Modo desarrollador",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            var validSession =
                await _microsoftAccountService
                    .GetValidSessionAsync();


            if (validSession ==
                null)
            {
                RefreshMicrosoftWarning();


                MessageBox.Show(
                    "Debes seleccionar una cuenta Microsoft válida para iniciar Minecraft.",
                    "Cuenta Microsoft requerida",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }


            LauncherPreferences preferences =
                await _launcherPreferencesService
                    .LoadAsync();


            preferences.DeveloperMinecraftVersion =
                version;


            await _launcherPreferencesService
                .SaveAsync(
                    preferences);


            InstalledInstance developerInstance =
                new()
                {
                    Id =
                        DeveloperInstanceId,

                    Name =
                        $"Minecraft {version} Vanilla",

                    InstallCode =
                        string.Empty,

                    IsInstalled =
                        true,

                    RuntimePrepared =
                        false,

                    LaunchVersionName =
                        string.Empty,

                    InstalledVersion =
                        version,

                    MinecraftVersion =
                        version,

                    Loader =
                        "vanilla",

                    LoaderVersion =
                        string.Empty,

                    IconFileId =
                        string.Empty,

                    BackgroundFileId =
                        string.Empty
                };


            DownloadProgressPanel.Visibility =
                Visibility.Visible;


            DownloadProgressBar.Value =
                0;


            DownloadProgressText.Text =
                "Preparando Minecraft Vanilla... 0%";


            PlayButton.Content =
                "PREPARANDO...";


            PlayButton.IsEnabled =
                false;


            string stage =
                "Preparando Minecraft Vanilla...";


            double percentageValue =
                0;


            Progress<double> progress =
                new(
                    percentage =>
                    {
                        if (!IsValidProgressValue(
                                percentage))
                        {
                            return;
                        }


                        percentageValue =
                            Math.Clamp(
                                percentage,
                                0,
                                100);


                        DownloadProgressBar.Value =
                            percentageValue;


                        DownloadProgressText.Text =
                            $"{stage} {percentageValue:0}%";


                        StatusText.Text =
                            DownloadProgressText.Text;
                    });


            Progress<string> status =
                new(
                    message =>
                    {
                        if (!string.IsNullOrWhiteSpace(
                                message))
                        {
                            stage =
                                message;
                        }


                        DownloadProgressText.Text =
                            $"{stage} {percentageValue:0}%";


                        StatusText.Text =
                            DownloadProgressText.Text;
                    });


            try
            {
                string launchVersionName =
                    await _minecraftGameService
                        .PrepareAsync(
                            developerInstance,
                            preferences,
                            progress,
                            status);


                developerInstance.RuntimePrepared =
                    true;


                developerInstance.LaunchVersionName =
                    launchVersionName;


                PlayButton.Content =
                    "INICIANDO...";


                StatusText.Text =
                    $"Iniciando Vanilla {version} como {validSession.Username}...";


                Process process =
                    await _minecraftGameService
                        .LaunchAsync(
                            developerInstance,
                            validSession,
                            preferences);


                _runningMinecraftProcess =
                    process;


                _runningMinecraftInstanceId =
                    DeveloperInstanceId;


                if (preferences.ShowGameConsole)
                {
                    if (_gameConsoleWindow !=
                        null)
                    {
                        _gameConsoleWindow
                            .CloseForProcessExit();


                        _gameConsoleWindow =
                            null;
                    }


                    _gameConsoleWindow =
                        new GameConsoleWindow(
                            process,
                            developerInstance.Name);


                    _gameConsoleWindow.Show();
                }


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
                                    DeveloperInstanceId);
                            });
                    };


                HideProgress();


                PlayButton.Content =
                    "CERRAR";


                PlayButton.IsEnabled =
                    true;


                StatusText.Text =
                    $"Minecraft Vanilla {version} abierto como {validSession.Username}.";


                if (preferences.CloseLauncherOnGameStart)
                {
                    Close();

                    return;
                }
            }
            catch (Exception ex)
            {
                HideProgress();


                PlayButton.Content =
                    "JUGAR";


                PlayButton.IsEnabled =
                    true;


                StatusText.Text =
                    "No se pudo iniciar Minecraft Vanilla.";


                MessageBox.Show(
                    ex.Message,
                    "Modo desarrollador",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
