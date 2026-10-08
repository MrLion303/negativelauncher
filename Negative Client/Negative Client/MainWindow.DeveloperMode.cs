using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private const string DeveloperInstanceId =
            "__developer_vanilla__";


        private bool _developerModeEnabled;
        private bool _developerPageActive;
        private bool _developerVersionsLoaded;
        private bool _developerVersionFiltersLoading;


        private Button? _developerInstanceButton;


        private List<MinecraftVersionOption>
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


            if (_operations.TryGetValue(
                    DeveloperInstanceId,
                    out InstanceOperationState? runningOperation) &&
                runningOperation.IsRunning)
            {
                ShowOperationState(
                    DeveloperInstanceId,
                    runningOperation);


                UpdateSidebarSelection();

                return;
            }


            HideProgress();


            LauncherPreferences preferences =
                await _launcherPreferencesService
                    .LoadAsync();


            _developerVersionFiltersLoading =
                true;


            DeveloperShowSnapshotsCheckBox.IsChecked =
                preferences.DeveloperShowSnapshots;


            DeveloperShowBetasCheckBox.IsChecked =
                preferences.DeveloperShowBetas;


            _developerVersionFiltersLoading =
                false;


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


                    _developerVersionsLoaded =
                        true;


                    ApplyDeveloperVersionFilters(
                        preferences.DeveloperMinecraftVersion,
                        catalog.LatestRelease);
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
            else
            {
                ApplyDeveloperVersionFilters(
                    preferences.DeveloperMinecraftVersion,
                    fallbackRelease: string.Empty);
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


        private void ApplyDeveloperVersionFilters(
            string preferredVersion,
            string fallbackRelease)
        {
            bool showSnapshots =
                DeveloperShowSnapshotsCheckBox.IsChecked ==
                true;


            bool showBetas =
                DeveloperShowBetasCheckBox.IsChecked ==
                true;


            List<string> visibleVersions =
                _developerMinecraftVersions
                    .Where(
                        version =>
                        {
                            string type =
                                version.Type
                                    .Trim()
                                    .ToLowerInvariant();


                            if (type ==
                                "release")
                            {
                                return true;
                            }


                            if (type ==
                                "snapshot")
                            {
                                return showSnapshots;
                            }


                            // CmlLib/Mojang usan old_beta y old_alpha
                            // para las ramas históricas previas a release.
                            if (type ==
                                    "old_beta" ||
                                type ==
                                    "old_alpha")
                            {
                                return showBetas;
                            }


                            return false;
                        })
                    .Select(
                        version =>
                            version.Name)
                    .ToList();


            DeveloperVersionComboBox.ItemsSource =
                visibleVersions;


            string desired =
                preferredVersion;


            if (string.IsNullOrWhiteSpace(
                    desired) ||
                !visibleVersions.Contains(
                    desired,
                    StringComparer.OrdinalIgnoreCase))
            {
                desired =
                    fallbackRelease;
            }


            if (string.IsNullOrWhiteSpace(
                    desired) ||
                !visibleVersions.Contains(
                    desired,
                    StringComparer.OrdinalIgnoreCase))
            {
                desired =
                    visibleVersions
                        .FirstOrDefault() ??
                    string.Empty;
            }


            DeveloperVersionComboBox.SelectedItem =
                visibleVersions
                    .FirstOrDefault(
                        version =>
                            string.Equals(
                                version,
                                desired,
                                StringComparison.OrdinalIgnoreCase));
        }


        private async void DeveloperVersionFilter_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_developerVersionFiltersLoading ||
                !_developerPageActive ||
                !_developerVersionsLoaded)
            {
                return;
            }


            string preferred =
                DeveloperVersionComboBox.SelectedItem
                    as string ??
                string.Empty;


            LauncherPreferences preferences =
                await _launcherPreferencesService
                    .LoadAsync();


            preferences.DeveloperShowSnapshots =
                DeveloperShowSnapshotsCheckBox.IsChecked ==
                true;


            preferences.DeveloperShowBetas =
                DeveloperShowBetasCheckBox.IsChecked ==
                true;


            await _launcherPreferencesService
                .SaveAsync(
                    preferences);


            ApplyDeveloperVersionFilters(
                preferred,
                fallbackRelease: string.Empty);
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
            if (_developerVersionFiltersLoading ||
                !_developerPageActive ||
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


            if (!IsMinecraftRunning() &&
                !_operations.ContainsKey(
                    DeveloperInstanceId))
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


            if (_operations.TryGetValue(
                    DeveloperInstanceId,
                    out InstanceOperationState? existingOperation) &&
                existingOperation.IsRunning)
            {
                ShowOperationState(
                    DeveloperInstanceId,
                    existingOperation);

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


            InstanceOperationState operation =
                new()
                {
                    IsRunning =
                        true,

                    IsUpdate =
                        false,

                    Progress =
                        0,

                    Message =
                        "Preparando Minecraft Vanilla... 0%",

                    StageMessage =
                        "Preparando Minecraft Vanilla...",

                    ButtonText =
                        "PREPARANDO..."
                };


            _operations[
                DeveloperInstanceId] =
                operation;


            ShowOperationState(
                DeveloperInstanceId,
                operation);


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


                        operation.Progress =
                            percentageValue;


                        operation.Message =
                            $"{stage} {percentageValue:0}%";


                        if (_developerPageActive)
                        {
                            ShowOperationState(
                                DeveloperInstanceId,
                                operation);
                        }
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


                        operation.Message =
                            $"{stage} {percentageValue:0}%";


                        if (_developerPageActive)
                        {
                            ShowOperationState(
                                DeveloperInstanceId,
                                operation);
                        }
                    });


            try
            {
                string launchVersionName =
                    await RunPausableRuntimePhaseAsync(
                        operation.Controller,
                        cancellationToken =>
                            _minecraftGameService
                                .PrepareAsync(
                                    developerInstance,
                                    preferences,
                                    progress,
                                    status,
                                    cancellationToken));


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
            catch (OperationCanceledException)
            {
                HideProgress();


                StatusText.Text =
                    "Preparación de Minecraft Vanilla detenida.";
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
            finally
            {
                operation.IsRunning =
                    false;


                _operations.Remove(
                    DeveloperInstanceId);


                operation.Controller.Dispose();


                if (_developerPageActive &&
                    !IsMinecraftRunning(
                        DeveloperInstanceId))
                {
                    HideProgress();


                    PlayButton.Content =
                        "JUGAR";


                    PlayButton.IsEnabled =
                        true;
                }
            }
        }
    }
}
