using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core.Auth;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class SettingsWindow : Window
    {
        private readonly MicrosoftAccountService
            _accountService;


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
            MicrosoftAccountService accountService)
        {
            InitializeComponent();

            _accountService =
                accountService;

            Loaded +=
                SettingsWindow_Loaded;
        }


        private void SettingsWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            RefreshAccountUi();
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
        // LOGIN
        // =====================================================

        private async void SignInButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetAccountButtonsEnabled(
                false);

            MicrosoftActionStatusText.Text =
                "Abriendo inicio de sesión de Microsoft...";


            try
            {
                MSession session =
                    await _accountService
                        .SignInInteractivelyAsync();


                MicrosoftActionStatusText.Text =
                    $"Sesión iniciada como {session.Username}.";


                RefreshAccountUi();
            }
            catch (Exception ex)
            {
                MicrosoftActionStatusText.Text =
                    "No se pudo iniciar sesión.";


                MessageBox.Show(
                    "No se pudo iniciar sesión con Microsoft.\n\n" +
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


        // =====================================================
        // LOGOUT
        // =====================================================

        private async void SignOutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetAccountButtonsEnabled(
                false);

            MicrosoftActionStatusText.Text =
                "Cerrando sesión...";


            try
            {
                await _accountService
                    .SignOutAsync();


                MicrosoftActionStatusText.Text =
                    "Sesión cerrada.";


                RefreshAccountUi();
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


        // =====================================================
        // UI
        // =====================================================

        private void RefreshAccountUi()
        {
            bool connected =
                _accountService.IsSignedIn;


            AccountStateBadge.Background =
                connected
                    ? ConnectedBrush
                    : DisconnectedBrush;


            AccountStateText.Text =
                connected
                    ? "CONECTADO"
                    : "NO CONECTADO";


            MicrosoftUsernameText.Text =
                connected
                    ? _accountService.Username
                    : "—";


            MicrosoftUuidText.Text =
                connected
                    ? _accountService.Uuid
                    : "—";


            SignInButton.Visibility =
                connected
                    ? Visibility.Collapsed
                    : Visibility.Visible;


            SignOutButton.Visibility =
                connected
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }


        private void SetAccountButtonsEnabled(
            bool enabled)
        {
            SignInButton.IsEnabled =
                enabled;

            SignOutButton.IsEnabled =
                enabled;
        }
    }
}
