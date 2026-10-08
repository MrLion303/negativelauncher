using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private SettingsPage? _embeddedSettingsPage;


        /*
         * Este es el ÚNICO override de OnSourceInitialized que debe existir
         * en todos los archivos partial de MainWindow.
         */
        protected override void OnSourceInitialized(
            EventArgs e)
        {
            base.OnSourceInitialized(
                e);

            InitializeEmbeddedSettingsPage();

            InitializeDeveloperVanillaBackgroundHook();

            /*
             * Inicializamos las cuentas regresivas desde el mismo lifecycle
             * hook ya existente, pero después de que WPF termine de construir
             * el contenido visual.
             */
            Dispatcher.BeginInvoke(
                new Action(
                    InitializeGlobalCountdowns),
                DispatcherPriority.Loaded);
        }


        private void InitializeEmbeddedSettingsPage()
        {
            if (_embeddedSettingsPage !=
                null)
            {
                return;
            }

            if (GalleryViewRoot.Parent is not
                Grid contentHost)
            {
                return;
            }

            _embeddedSettingsPage =
                new SettingsPage(
                    _microsoftAccountService,
                    _launcherPreferencesService,
                    _instanceService)
                {
                    Visibility =
                        Visibility.Collapsed
                };

            Grid.SetRowSpan(
                _embeddedSettingsPage,
                2);

            Panel.SetZIndex(
                _embeddedSettingsPage,
                400);

            contentHost.Children.Add(
                _embeddedSettingsPage);

            _embeddedSettingsPage.AccountsChanged +=
                EmbeddedSettingsPage_AccountsChanged;

            _embeddedSettingsPage.HolidayAppearanceChanged +=
                EmbeddedSettingsPage_HolidayAppearanceChanged;

            _embeddedSettingsPage.DeveloperModeChanged +=
                EmbeddedSettingsPage_DeveloperModeChanged;


            /*
             * El XAML ya enlazó AccountSettingsButton_Click. Lo retiramos
             * para que el engrane deje de abrir SettingsWindow y pase a
             * mostrar la página dentro del propio launcher.
             */
            AccountSettingsButton.Click -=
                AccountSettingsButton_Click;

            AccountSettingsButton.Click +=
                AccountSettingsButton_Embedded_Click;


            HomeButton.Click +=
                SidebarNavigationAwayFromSettings_Click;

            GalleryButton.Click +=
                SidebarNavigationAwayFromSettings_Click;

            AddModpackButton.Click +=
                SidebarNavigationAwayFromSettings_Click;

            ModpackList.AddHandler(
                Button.ClickEvent,
                new RoutedEventHandler(
                    SidebarNavigationAwayFromSettings_Click),
                handledEventsToo:
                    true);
        }


        private async void AccountSettingsButton_Embedded_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_embeddedSettingsPage ==
                null)
            {
                return;
            }

            if (_embeddedSettingsPage.Visibility ==
                Visibility.Visible)
            {
                HideEmbeddedSettingsPage();

                return;
            }

            await ShowEmbeddedSettingsPageAsync();
        }


        private async Task ShowEmbeddedSettingsPageAsync()
        {
            if (_embeddedSettingsPage ==
                null)
            {
                return;
            }

            AccountQuickPopup.IsOpen =
                false;

            AccountQuickRoot.Visibility =
                Visibility.Collapsed;

            InstanceOptionsButton.Visibility =
                Visibility.Collapsed;

            ExitGalleryMode();

            _embeddedSettingsPage.Visibility =
                Visibility.Visible;

            await _embeddedSettingsPage
                .RefreshPageAsync();

            ClearSidebarSelectionForSettings();

            AccountSettingsButton.BorderBrush =
                AccentBrush;
        }


        private void HideEmbeddedSettingsPage()
        {
            if (_embeddedSettingsPage ==
                null)
            {
                return;
            }

            _embeddedSettingsPage.Visibility =
                Visibility.Collapsed;

            AccountSettingsButton.BorderBrush =
                NormalBorderBrush;

            UpdateSidebarSelection();

            if (_selectedInstance !=
                null)
            {
                AccountQuickRoot.Visibility =
                    Visibility.Visible;

                InstanceOptionsButton.Visibility =
                    Visibility.Visible;
            }
        }


        private void SidebarNavigationAwayFromSettings_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_embeddedSettingsPage ==
                    null ||
                _embeddedSettingsPage.Visibility !=
                    Visibility.Visible)
            {
                return;
            }

            HideEmbeddedSettingsPage();
        }


        private void ClearSidebarSelectionForSettings()
        {
            HomeButton.BorderBrush =
                NormalBorderBrush;

            GalleryButton.BorderBrush =
                NormalBorderBrush;

            foreach (Button instanceButton in
                _instanceButtons.Values)
            {
                instanceButton.BorderBrush =
                    NormalBorderBrush;
            }

            if (_developerInstanceButton !=
                null)
            {
                _developerInstanceButton.BorderBrush =
                    NormalBorderBrush;
            }
        }


        private async void EmbeddedSettingsPage_AccountsChanged(
            object? sender,
            EventArgs e)
        {
            RefreshMicrosoftWarning();

            await RefreshQuickAccountUiAsync();
        }


        private async void EmbeddedSettingsPage_HolidayAppearanceChanged(
            object? sender,
            EventArgs e)
        {
            await RefreshHolidayThemeFromPreferencesAsync();
        }


        private async void EmbeddedSettingsPage_DeveloperModeChanged(
            object? sender,
            EventArgs e)
        {
            await RefreshDeveloperModeStateAsync();

            RefreshInstanceButtons();

            if (_embeddedSettingsPage !=
                    null &&
                _embeddedSettingsPage.Visibility ==
                    Visibility.Visible)
            {
                ClearSidebarSelectionForSettings();

                AccountSettingsButton.BorderBrush =
                    AccentBrush;
            }
        }
    }
}
