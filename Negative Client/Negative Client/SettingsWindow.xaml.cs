using System;
using System.Linq;
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
    public partial class SettingsWindow : Window
    {
        private readonly MicrosoftAccountService
            _accountService;


        private readonly LauncherPreferencesService
            _preferencesService;


        private LauncherPreferences
            _preferences =
                new LauncherPreferences();


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


        public SettingsWindow(
            MicrosoftAccountService accountService,
            LauncherPreferencesService preferencesService)
        {
            InitializeComponent();

            _accountService =
                accountService;

            _preferencesService =
                preferencesService;

            Loaded +=
                SettingsWindow_Loaded;
        }


        private async void SettingsWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            _preferences =
                await _preferencesService
                    .LoadAsync();


            LoadPreferencesIntoUi();

            RefreshAccountsUi();
        }


        // =====================================================
        // VENTANA
        // =====================================================

        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.LeftButton ==
                MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }


        // =====================================================
        // CUENTAS
        // =====================================================

        private void RefreshAccountsUi(
            string? selectIdentifier = null)
        {
            var accounts =
                _accountService
                    .GetAccounts();


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
                selected.Identifier;


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
                enabled;

            UseAccountButton.IsEnabled =
                enabled &&
                AccountsListBox.SelectedItem != null;

            ReauthenticateButton.IsEnabled =
                enabled &&
                AccountsListBox.SelectedItem != null;

            SignOutButton.IsEnabled =
                enabled &&
                AccountsListBox.SelectedItem != null;
        }


        // =====================================================
        // PREFERENCIAS DE MINECRAFT
        // =====================================================

        private void LoadPreferencesIntoUi()
        {
            int wantedRam =
                _preferences
                    .MaximumRamMb;


            ComboBoxItem? matching =
                RamComboBox
                    .Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(
                        item =>
                            int.TryParse(
                                item.Tag?.ToString(),
                                out int value) &&
                            value ==
                            wantedRam);


            RamComboBox.SelectedItem =
                matching ??
                RamComboBox.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(
                        item =>
                            item.Tag?.ToString() ==
                            "4096");


            AutomaticJavaCheckBox.IsChecked =
                _preferences
                    .UseAutomaticJava;


            CustomJavaPathTextBox.Text =
                _preferences
                    .CustomJavaPath;


            RefreshJavaUi();
        }


        private void AutomaticJavaCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            RefreshJavaUi();
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


            if (dialog.ShowDialog(this) ==
                true)
            {
                CustomJavaPathTextBox.Text =
                    dialog.FileName;
            }
        }


        private async void SavePreferencesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (RamComboBox.SelectedItem
                    is not ComboBoxItem ramItem ||
                    !int.TryParse(
                        ramItem.Tag?.ToString(),
                        out int ramMb))
                {
                    throw new InvalidOperationException(
                        "Selecciona una cantidad de RAM válida.");
                }


                bool automaticJava =
                    AutomaticJavaCheckBox
                        .IsChecked ==
                    true;


                if (!automaticJava &&
                    string.IsNullOrWhiteSpace(
                        CustomJavaPathTextBox.Text))
                {
                    throw new InvalidOperationException(
                        "Selecciona java.exe o javaw.exe, " +
                        "o activa Java automático.");
                }


                _preferences =
                    new LauncherPreferences
                    {
                        MaximumRamMb =
                            ramMb,

                        UseAutomaticJava =
                            automaticJava,

                        CustomJavaPath =
                            CustomJavaPathTextBox.Text
                                .Trim()
                    };


                await _preferencesService
                    .SaveAsync(
                        _preferences);


                PreferencesStatusText.Text =
                    "Configuración guardada.";
            }
            catch (Exception ex)
            {
                PreferencesStatusText.Text =
                    "No se pudo guardar.";


                MessageBox.Show(
                    ex.Message,
                    "Configuración",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
