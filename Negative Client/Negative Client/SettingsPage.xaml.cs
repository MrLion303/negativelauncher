using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class SettingsPage : UserControl
    {
        private readonly MicrosoftAccountService _accountService;
        private readonly LauncherPreferencesService _preferencesService;
        private readonly InstanceService _instanceService;
        private readonly MinecraftSkinService _skinService;

        private LauncherPreferences _preferences =
            new LauncherPreferences();

        private bool _loadedOnce;
        private bool _isLoadingPreferences = true;
        private int _developerModeClickCount;

        private CancellationTokenSource? _autoSaveDelayCts;

        private readonly SemaphoreSlim _saveSemaphore =
            new SemaphoreSlim(1, 1);

        private static readonly Brush ConnectedBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    42,
                    112,
                    73));

        private static readonly Brush DisconnectedBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    128,
                    58,
                    58));


        public event EventHandler? AccountsChanged;
        public event EventHandler? HolidayAppearanceChanged;
        public event EventHandler? DeveloperModeChanged;


        public SettingsPage(
            MicrosoftAccountService accountService,
            LauncherPreferencesService preferencesService,
            InstanceService instanceService)
        {
            InitializeComponent();

            _accountService =
                accountService;

            _preferencesService =
                preferencesService;

            _instanceService =
                instanceService;

            _skinService =
                new MinecraftSkinService();

            Loaded +=
                SettingsPage_Loaded;
        }


        private async void SettingsPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadedOnce)
            {
                RefreshAccountsUi();

                await RefreshStorageUsageAsync();

                return;
            }

            _loadedOnce =
                true;

            _preferences =
                await _preferencesService
                    .LoadAsync();

            ConfigureRamSlider();

            LoadPreferencesIntoUi();

            InstallationsLocationTextBox.Text =
                _instanceService
                    .GetStorageRoot();

            _isLoadingPreferences =
                false;

            RefreshAccountsUi();

            UpdateDeveloperModeTextVisual();

            PreferencesStatusText.Text =
                "Los cambios se guardan automáticamente.";

            await RefreshStorageUsageAsync();
        }


        public async Task RefreshPageAsync()
        {
            _preferences =
                await _preferencesService
                    .LoadAsync();

            _isLoadingPreferences =
                true;

            ConfigureRamSlider();

            LoadPreferencesIntoUi();

            InstallationsLocationTextBox.Text =
                _instanceService
                    .GetStorageRoot();

            _isLoadingPreferences =
                false;

            RefreshAccountsUi();

            UpdateDeveloperModeTextVisual();

            await RefreshStorageUsageAsync();
        }


        // =====================================================
        // PESTAÑAS
        // =====================================================

        private void NormalSettingsTabButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NormalSettingsView.Visibility =
                Visibility.Visible;

            AccountsView.Visibility =
                Visibility.Collapsed;

            NormalSettingsTabButton.Tag =
                "selected";

            AccountsTabButton.Tag =
                null;
        }


        private void AccountsTabButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NormalSettingsView.Visibility =
                Visibility.Collapsed;

            AccountsView.Visibility =
                Visibility.Visible;

            NormalSettingsTabButton.Tag =
                null;

            AccountsTabButton.Tag =
                "selected";

            RefreshAccountsUi();
        }


        // =====================================================
        // CUENTAS MICROSOFT
        // =====================================================

        private void RefreshAccountsUi(
            string? selectIdentifier = null)
        {
            List<MicrosoftAccountInfo> accounts =
                _accountService
                    .GetAccounts();

            bool canAddAccount =
                accounts.Count <
                MicrosoftAccountService.MaxAccounts;

            AddAccountButton.IsEnabled =
                canAddAccount;

            AddAccountButton.Content =
                canAddAccount
                    ? "AÑADIR CUENTA"
                    : "MÁXIMO 3 CUENTAS";

            string identifierToSelect =
                selectIdentifier ??
                _accountService
                    .SelectedAccountIdentifier;

            AccountsListBox.ItemsSource =
                accounts;

            MicrosoftAccountInfo? selected =
                accounts.FirstOrDefault(
                    account =>
                        string.Equals(
                            account.Identifier,
                            identifierToSelect,
                            StringComparison.OrdinalIgnoreCase));

            if (selected != null)
            {
                AccountsListBox.SelectedItem =
                    selected;
            }
            else if (accounts.Count > 0)
            {
                AccountsListBox.SelectedIndex =
                    0;
            }

            RefreshAccountBadge();

            RefreshSelectedAccountDetails();

            _ =
                LoadAccountHeadsAsync(
                    accounts);
        }


        private async Task LoadAccountHeadsAsync(
            IEnumerable<MicrosoftAccountInfo> accounts)
        {
            bool changed =
                false;

            foreach (MicrosoftAccountInfo account in
                accounts)
            {
                if (string.IsNullOrWhiteSpace(
                        account.Uuid))
                {
                    continue;
                }

                string? headPath =
                    await _skinService
                        .GetHeadPathAsync(
                            account.Uuid);

                if (!string.Equals(
                        account.SkinHeadPath,
                        headPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    account.SkinHeadPath =
                        headPath ??
                        string.Empty;

                    changed =
                        true;
                }
            }

            if (changed &&
                IsLoaded)
            {
                AccountsListBox.Items.Refresh();
            }
        }


        private void AccountsListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            RefreshSelectedAccountDetails();
        }


        private void RefreshSelectedAccountDetails()
        {
            MicrosoftAccountInfo? selected =
                AccountsListBox.SelectedItem
                    as MicrosoftAccountInfo;

            if (selected == null)
            {
                MicrosoftUsernameText.Text =
                    "—";

                MicrosoftUuidText.Text =
                    "—";

                UseAccountButton.IsEnabled =
                    false;

                ReauthenticateButton.IsEnabled =
                    false;

                SignOutButton.IsEnabled =
                    false;

                return;
            }

            MicrosoftUsernameText.Text =
                selected.Username;

            MicrosoftUuidText.Text =
                string.IsNullOrWhiteSpace(
                    selected.Uuid)
                    ? "—"
                    : selected.Uuid;

            UseAccountButton.IsEnabled =
                true;

            ReauthenticateButton.IsEnabled =
                true;

            SignOutButton.IsEnabled =
                true;
        }


        private void RefreshAccountBadge()
        {
            bool connected =
                _accountService
                    .IsSignedIn;

            AccountStateBadge.Background =
                connected
                    ? ConnectedBrush
                    : DisconnectedBrush;

            AccountStateText.Text =
                connected
                    ? "CONECTADO"
                    : "NO CONECTADO";
        }


        private async void AddAccountButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_accountService
                    .GetAccounts()
                    .Count >=
                MicrosoftAccountService.MaxAccounts)
            {
                MessageBox.Show(
                    "Negative Client permite un máximo de 3 cuentas.",
                    "Límite de cuentas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            SetAccountButtonsEnabled(
                false);

            MicrosoftActionStatusText.Text =
                "Abriendo inicio de sesión de Microsoft...";

            try
            {
                var session =
                    await _accountService
                        .AddAccountInteractivelyAsync();

                MicrosoftActionStatusText.Text =
                    $"Cuenta añadida: {session.Username}";

                RefreshAccountsUi(
                    _accountService
                        .SelectedAccountIdentifier);

                AccountsChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MicrosoftActionStatusText.Text =
                    "No se pudo añadir la cuenta.";

                MessageBox.Show(
                    ex.Message,
                    "Error de Microsoft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetAccountButtonsEnabled(
                    true);
            }
        }


        private async void UseAccountButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MicrosoftAccountInfo? selected =
                AccountsListBox.SelectedItem
                    as MicrosoftAccountInfo;

            if (selected == null)
            {
                return;
            }

            SetAccountButtonsEnabled(
                false);

            MicrosoftActionStatusText.Text =
                $"Cambiando a {selected.Username}...";

            try
            {
                bool restored =
                    await _accountService
                        .SelectAccountAsync(
                            selected.Identifier);

                if (!restored)
                {
                    MessageBoxResult result =
                        MessageBox.Show(
                            "La sesión guardada de esta cuenta ya no es válida.\n\n" +
                            "¿Quieres volver a iniciar sesión ahora?",
                            "Sesión expirada",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                    if (result ==
                        MessageBoxResult.Yes)
                    {
                        await _accountService
                            .ReauthenticateAccountAsync(
                                selected.Identifier);
                    }
                }

                MicrosoftActionStatusText.Text =
                    _accountService.IsSignedIn
                        ? $"Ahora se usará {_accountService.Username} para jugar."
                        : "La cuenta quedó seleccionada, pero necesita iniciar sesión.";

                RefreshAccountsUi(
                    selected.Identifier);

                AccountsChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MicrosoftActionStatusText.Text =
                    "No se pudo cambiar de cuenta.";

                MessageBox.Show(
                    ex.Message,
                    "Error de cuenta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetAccountButtonsEnabled(
                    true);
            }
        }


        private async void ReauthenticateButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MicrosoftAccountInfo? selected =
                AccountsListBox.SelectedItem
                    as MicrosoftAccountInfo;

            if (selected == null)
            {
                return;
            }

            SetAccountButtonsEnabled(
                false);

            MicrosoftActionStatusText.Text =
                "Abriendo Microsoft...";

            try
            {
                var session =
                    await _accountService
                        .ReauthenticateAccountAsync(
                            selected.Identifier);

                MicrosoftActionStatusText.Text =
                    $"Sesión iniciada como {session.Username}.";

                RefreshAccountsUi(
                    _accountService
                        .SelectedAccountIdentifier);

                AccountsChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MicrosoftActionStatusText.Text =
                    "No se pudo iniciar sesión.";

                MessageBox.Show(
                    ex.Message,
                    "Error de Microsoft",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetAccountButtonsEnabled(
                    true);
            }
        }


        private async void SignOutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MicrosoftAccountInfo? selected =
                AccountsListBox.SelectedItem
                    as MicrosoftAccountInfo;

            if (selected == null)
            {
                return;
            }

            SetAccountButtonsEnabled(
                false);

            MicrosoftActionStatusText.Text =
                "Cerrando sesión...";

            try
            {
                await _accountService
                    .SignOutAccountAsync(
                        selected.Identifier);

                MicrosoftActionStatusText.Text =
                    "Sesión cerrada.";

                RefreshAccountsUi();

                AccountsChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MicrosoftActionStatusText.Text =
                    "No se pudo cerrar sesión.";

                MessageBox.Show(
                    ex.Message,
                    "Error al cerrar sesión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetAccountButtonsEnabled(
                    true);
            }
        }


        private void SetAccountButtonsEnabled(
            bool enabled)
        {
            AddAccountButton.IsEnabled =
                enabled &&
                _accountService
                    .GetAccounts()
                    .Count <
                MicrosoftAccountService.MaxAccounts;

            UseAccountButton.IsEnabled =
                enabled &&
                AccountsListBox.SelectedItem !=
                null;

            ReauthenticateButton.IsEnabled =
                enabled &&
                AccountsListBox.SelectedItem !=
                null;

            SignOutButton.IsEnabled =
                enabled &&
                AccountsListBox.SelectedItem !=
                null;
        }


        // =====================================================
        // PREFERENCIAS
        // =====================================================

        private void ConfigureRamSlider()
        {
            long availableBytes =
                GC.GetGCMemoryInfo()
                    .TotalAvailableMemoryBytes;

            int availableMb =
                availableBytes > 0
                    ? (int)Math.Min(
                        availableBytes /
                        (1024L * 1024L),
                        32768L)
                    : 8192;

            int maximumMb =
                Math.Max(
                    2048,
                    availableMb -
                    1024);

            maximumMb =
                Math.Min(
                    maximumMb,
                    32768);

            maximumMb =
                Math.Max(
                    1024,
                    maximumMb /
                    256 *
                    256);

            RamSlider.Minimum =
                1024;

            RamSlider.Maximum =
                maximumMb;

            RamSlider.TickFrequency =
                256;

            RamSlider.SmallChange =
                256;

            RamSlider.LargeChange =
                1024;
        }


        private void LoadPreferencesIntoUi()
        {
            double ramValue =
                Math.Clamp(
                    _preferences.MaximumRamMb,
                    (int)RamSlider.Minimum,
                    (int)RamSlider.Maximum);

            ramValue =
                Math.Round(
                    ramValue /
                    256.0) *
                256.0;

            RamSlider.Value =
                ramValue;

            RefreshRamText();

            AutomaticJavaCheckBox.IsChecked =
                _preferences
                    .UseAutomaticJava;

            CustomJavaPathTextBox.Text =
                _preferences
                    .CustomJavaPath;

            CloseLauncherOnGameStartCheckBox.IsChecked =
                _preferences
                    .CloseLauncherOnGameStart;

            CustomJavaArgumentsEnabledCheckBox.IsChecked =
                _preferences
                    .EnableCustomJavaArguments;

            CustomJavaArgumentsTextBox.Text =
                _preferences
                    .CustomJavaArguments;

            ShowGameConsoleCheckBox.IsChecked =
                _preferences
                    .ShowGameConsole;

            HolidayThemesCheckBox.IsChecked =
                _preferences
                    .EnableHolidayLauncherThemes;

            RefreshJavaUi();

            RefreshCustomJavaArgumentsUi();
        }


        private void RamSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            RefreshRamText();

            QueueAutoSave();
        }


        private void RefreshRamText()
        {
            if (RamValueText ==
                    null ||
                RamSlider ==
                    null)
            {
                return;
            }

            int ramMb =
                (int)(
                    Math.Round(
                        RamSlider.Value /
                        256.0) *
                    256.0);

            RamValueText.Text =
                $"{ramMb} MB";
        }


        private void AutomaticJavaCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            RefreshJavaUi();

            QueueAutoSave();
        }


        private void RefreshJavaUi()
        {
            bool automatic =
                AutomaticJavaCheckBox
                    .IsChecked ==
                true;

            CustomJavaPathTextBox.IsEnabled =
                !automatic;
        }


        private void BrowseJavaButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title =
                        "Seleccionar Java",

                    Filter =
                        "Java (javaw.exe;java.exe)|javaw.exe;java.exe|" +
                        "Ejecutables (*.exe)|*.exe",

                    CheckFileExists =
                        true
                };

            Window? owner =
                Window.GetWindow(
                    this);

            bool? result =
                owner != null
                    ? dialog.ShowDialog(
                        owner)
                    : dialog.ShowDialog();

            if (result ==
                true)
            {
                CustomJavaPathTextBox.Text =
                    dialog.FileName;
            }
        }


        private void CustomJavaArgumentsEnabledCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            RefreshCustomJavaArgumentsUi();

            QueueAutoSave();
        }


        private void RefreshCustomJavaArgumentsUi()
        {
            CustomJavaArgumentsTextBox.IsEnabled =
                CustomJavaArgumentsEnabledCheckBox
                    .IsChecked ==
                true;
        }


        private void AutoSaveToggle_Changed(
            object sender,
            RoutedEventArgs e)
        {
            QueueAutoSave();
        }


        private void AutoSaveText_Changed(
            object sender,
            TextChangedEventArgs e)
        {
            QueueAutoSave(
                500);
        }


        private async void HolidayThemesCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_isLoadingPreferences)
            {
                return;
            }

            await SavePreferencesFromUiAsync(
                showStatus:
                    true);

            HolidayAppearanceChanged?.Invoke(
                this,
                EventArgs.Empty);
        }


        private void QueueAutoSave(
            int delayMilliseconds = 300)
        {
            if (_isLoadingPreferences)
            {
                return;
            }

            _autoSaveDelayCts?
                .Cancel();

            _autoSaveDelayCts?
                .Dispose();

            CancellationTokenSource cts =
                new CancellationTokenSource();

            _autoSaveDelayCts =
                cts;

            _ =
                SaveAfterDelayAsync(
                    delayMilliseconds,
                    cts.Token);
        }


        private async Task SaveAfterDelayAsync(
            int delayMilliseconds,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(
                    delayMilliseconds,
                    cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                await SavePreferencesFromUiAsync(
                    showStatus:
                        true);
            }
            catch (OperationCanceledException)
            {
            }
        }


        private async Task FlushAutoSaveAsync()
        {
            _autoSaveDelayCts?
                .Cancel();

            try
            {
                await SavePreferencesFromUiAsync(
                    showStatus:
                        false);
            }
            catch
            {
            }
        }


        private async Task SavePreferencesFromUiAsync(
            bool showStatus)
        {
            if (_isLoadingPreferences)
            {
                return;
            }

            await _saveSemaphore
                .WaitAsync();

            try
            {
                int ramMb =
                    (int)(
                        Math.Round(
                            RamSlider.Value /
                            256.0) *
                        256.0);

                ramMb =
                    Math.Clamp(
                        ramMb,
                        (int)RamSlider.Minimum,
                        (int)RamSlider.Maximum);

                _preferences.MaximumRamMb =
                    ramMb;

                _preferences.UseAutomaticJava =
                    AutomaticJavaCheckBox
                        .IsChecked ==
                    true;

                _preferences.CustomJavaPath =
                    CustomJavaPathTextBox.Text
                        .Trim();

                _preferences.CloseLauncherOnGameStart =
                    CloseLauncherOnGameStartCheckBox
                        .IsChecked ==
                    true;

                _preferences.EnableCustomJavaArguments =
                    CustomJavaArgumentsEnabledCheckBox
                        .IsChecked ==
                    true;

                _preferences.CustomJavaArguments =
                    CustomJavaArgumentsTextBox.Text
                        .Trim();

                _preferences.ShowGameConsole =
                    ShowGameConsoleCheckBox
                        .IsChecked ==
                    true;

                _preferences.EnableHolidayLauncherThemes =
                    HolidayThemesCheckBox
                        .IsChecked ==
                    true;

                _preferences.StorageRootPath =
                    _instanceService
                        .GetStorageRoot();

                await _preferencesService
                    .SaveAsync(
                        _preferences);

                if (showStatus)
                {
                    PreferencesStatusText.Text =
                        "Guardado automáticamente.";
                }
            }
            catch
            {
                if (showStatus)
                {
                    PreferencesStatusText.Text =
                        "No se pudo guardar un cambio.";
                }
            }
            finally
            {
                _saveSemaphore
                    .Release();
            }
        }


        // =====================================================
        // ALMACENAMIENTO
        // =====================================================

        private async void BrowseInstallationsLocationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFolderDialog dialog =
                new OpenFolderDialog
                {
                    Title =
                        "Elegir carpeta de Negative Client",

                    InitialDirectory =
                        _instanceService
                            .GetStorageRoot(),

                    Multiselect =
                        false
                };

            Window? owner =
                Window.GetWindow(
                    this);

            bool? result =
                owner != null
                    ? dialog.ShowDialog(
                        owner)
                    : dialog.ShowDialog();

            if (result !=
                true)
            {
                return;
            }

            string selectedRoot =
                dialog.FolderName;

            if (string.IsNullOrWhiteSpace(
                    selectedRoot))
            {
                return;
            }

            BrowseInstallationsLocationButton.IsEnabled =
                false;

            PreferencesStatusText.Text =
                "Moviendo instalaciones al nuevo disco...";

            try
            {
                await FlushAutoSaveAsync();

                await _instanceService
                    .ChangeStorageRootAsync(
                        selectedRoot);

                _preferences.StorageRootPath =
                    _instanceService
                        .GetStorageRoot();

                await _preferencesService
                    .SaveAsync(
                        _preferences);

                InstallationsLocationTextBox.Text =
                    _instanceService
                        .GetStorageRoot();

                PreferencesStatusText.Text =
                    "Ubicación cambiada y guardada.";

                await RefreshStorageUsageAsync();
            }
            catch (Exception ex)
            {
                PreferencesStatusText.Text =
                    "No se pudo cambiar la ubicación.";

                MessageBox.Show(
                    ex.Message,
                    "Ubicación de instalaciones",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BrowseInstallationsLocationButton.IsEnabled =
                    true;
            }
        }


        private async Task RefreshStorageUsageAsync()
        {
            try
            {
                InstanceStorageText.Text =
                    "Calculando espacio usado...";

                long instanceBytes =
                    await Task.Run(
                        CalculateInstancesSizeBytes);

                string? root =
                    Path.GetPathRoot(
                        InstanceService.InstancesRoot);

                if (string.IsNullOrWhiteSpace(
                        root))
                {
                    throw new InvalidOperationException(
                        "No se pudo determinar la unidad de almacenamiento.");
                }

                DriveInfo drive =
                    new DriveInfo(
                        root);

                long freeBytes =
                    drive.AvailableFreeSpace;

                InstanceStorageText.Text =
                    $"Instancias: {FormatBytes(instanceBytes)}  •  " +
                    $"Libre en {drive.Name}: {FormatBytes(freeBytes)}";
            }
            catch
            {
                InstanceStorageText.Text =
                    "No se pudo calcular el espacio usado.";
            }
        }


        private static long CalculateInstancesSizeBytes()
        {
            if (!Directory.Exists(
                    InstanceService.InstancesRoot))
            {
                return 0;
            }

            long total =
                0;

            foreach (string filePath in
                Directory.EnumerateFiles(
                    InstanceService.InstancesRoot,
                    "*",
                    SearchOption.AllDirectories))
            {
                try
                {
                    total +=
                        new FileInfo(
                            filePath)
                            .Length;
                }
                catch
                {
                }
            }

            return total;
        }


        private static string FormatBytes(
            long bytes)
        {
            string[] units =
            {
                "B",
                "KB",
                "MB",
                "GB",
                "TB"
            };

            double value =
                bytes;

            int unitIndex =
                0;

            while (value >= 1024 &&
                   unitIndex <
                   units.Length -
                   1)
            {
                value /=
                    1024;

                unitIndex++;
            }

            return
                $"{value:0.##} {units[unitIndex]}";
        }


        // =====================================================
        // LIMPIEZA
        // =====================================================

        private async void CleanupUnusedFilesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CleanupUnusedFilesButton.IsEnabled =
                false;

            CleanupUnusedFilesStatusText.Text =
                "Analizando archivos no utilizados...";

            try
            {
                await FlushAutoSaveAsync();

                UnusedFilesCleanupService cleanupService =
                    new UnusedFilesCleanupService(
                        _instanceService);

                UnusedFilesCleanupPlan plan =
                    await cleanupService
                        .AnalyzeAsync(
                            _preferences);

                if (plan.Entries.Count ==
                    0)
                {
                    CleanupUnusedFilesStatusText.Text =
                        "No se encontraron archivos seguros que limpiar.";

                    return;
                }

                string reclaimable =
                    FormatBytes(
                        plan.TotalBytes);

                MessageBoxResult answer =
                    MessageBox.Show(
                        $"Se pueden liberar {reclaimable} en {plan.Entries.Count} carpeta(s).\n\n" +
                        "No se tocarán mods, configuraciones, mundos, capturas, resource packs, libraries ni assets compartidos.\n\n" +
                        "¿Quieres continuar?",
                        "Limpiar archivos no utilizados",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                if (answer !=
                    MessageBoxResult.Yes)
                {
                    CleanupUnusedFilesStatusText.Text =
                        "Limpieza cancelada.";

                    return;
                }

                CleanupUnusedFilesStatusText.Text =
                    "Eliminando archivos no utilizados...";

                UnusedFilesCleanupResult result =
                    await cleanupService
                        .ExecuteAsync(
                            plan);

                string freed =
                    FormatBytes(
                        result.FreedBytes);

                CleanupUnusedFilesStatusText.Text =
                    result.FailedEntries ==
                    0
                        ? $"Limpieza completada • {freed} liberados."
                        : $"Limpieza parcial • {freed} liberados • {result.FailedEntries} error(es).";

                await RefreshStorageUsageAsync();
            }
            catch (Exception ex)
            {
                CleanupUnusedFilesStatusText.Text =
                    "No se pudo completar la limpieza.";

                MessageBox.Show(
                    ex.Message,
                    "Limpieza de archivos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                CleanupUnusedFilesButton.IsEnabled =
                    true;
            }
        }


        // =====================================================
        // MODO DESARROLLADOR
        // =====================================================

        private async void DeveloperModeText_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            _developerModeClickCount++;

            if (_developerModeClickCount <
                5)
            {
                return;
            }

            _developerModeClickCount =
                0;

            if (_preferences.DeveloperMode)
            {
                _preferences.DeveloperMode =
                    false;

                await _preferencesService
                    .SaveAsync(
                        _preferences);

                UpdateDeveloperModeTextVisual();

                DeveloperModeChanged?.Invoke(
                    this,
                    EventArgs.Empty);

                return;
            }

            DeveloperLoginWindow loginWindow =
                new DeveloperLoginWindow();

            Window? owner =
                Window.GetWindow(
                    this);

            if (owner !=
                null)
            {
                loginWindow.Owner =
                    owner;
            }

            bool? result =
                loginWindow.ShowDialog();

            if (result !=
                    true ||
                !loginWindow.Authenticated)
            {
                return;
            }

            _preferences.DeveloperMode =
                true;

            await _preferencesService
                .SaveAsync(
                    _preferences);

            UpdateDeveloperModeTextVisual();

            DeveloperModeChanged?.Invoke(
                this,
                EventArgs.Empty);
        }


        private void UpdateDeveloperModeTextVisual()
        {
            DeveloperModeText.Foreground =
                _preferences.DeveloperMode
                    ? new SolidColorBrush(
                        Color.FromRgb(
                            94,
                            224,
                            243))
                    : new SolidColorBrush(
                        Color.FromRgb(
                            125,
                            135,
                            146));
        }
    }
}
