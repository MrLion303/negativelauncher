using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Negative_Client.Models;

namespace Negative_Client
{
    public partial class SettingsWindow
    {
        private CheckBox?
            _holidayLauncherThemesCheckBox;

        private bool
            _holidaySectionInitialized;

        private bool
            _holidayCheckBoxLoading;


        protected override async void OnContentRendered(
            EventArgs e)
        {
            base.OnContentRendered(e);


            if (_holidaySectionInitialized)
            {
                return;
            }


            _holidaySectionInitialized =
                true;


            AddHolidayAppearanceSection();

            await LoadHolidayAppearancePreferenceAsync();
        }


        private void AddHolidayAppearanceSection()
        {
            StackPanel? mainStackPanel =
                FindMainSettingsStackPanel();


            if (mainStackPanel ==
                null)
            {
                return;
            }


            TextBlock sectionTitle =
                new TextBlock
                {
                    Text =
                        "APARIENCIA",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                125,
                                135,
                                146)),

                    FontSize =
                        11,

                    FontWeight =
                        FontWeights.SemiBold,

                    Margin =
                        new Thickness(
                            0,
                            24,
                            0,
                            8)
                };


            _holidayLauncherThemesCheckBox =
                new CheckBox
                {
                    Content =
                        "Aspectos de días festivos del launcher",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                220,
                                224,
                                228)),

                    Margin =
                        new Thickness(
                            0,
                            0,
                            0,
                            0),

                    IsChecked =
                        true
                };


            if (TryFindResource(
                    "ThemedCheckBox") is
                Style checkBoxStyle)
            {
                _holidayLauncherThemesCheckBox.Style =
                    checkBoxStyle;
            }


            _holidayLauncherThemesCheckBox.Checked +=
                HolidayLauncherThemesCheckBox_Changed;

            _holidayLauncherThemesCheckBox.Unchecked +=
                HolidayLauncherThemesCheckBox_Changed;


            TextBlock description =
                new TextBlock
                {
                    Text =
                        "Cambia automáticamente los colores del launcher y activa efectos especiales en fechas señaladas. " +
                        "Los cambios de fecha se calculan a medianoche GMT-6 (Monterrey).",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                120,
                                131,
                                142)),

                    FontSize =
                        12,

                    TextWrapping =
                        TextWrapping.Wrap,

                    Margin =
                        new Thickness(
                            22,
                            6,
                            0,
                            0)
                };


            StackPanel contentPanel =
                new StackPanel();


            contentPanel.Children.Add(
                _holidayLauncherThemesCheckBox);

            contentPanel.Children.Add(
                description);


            Border card =
                new Border
                {
                    Background =
                        new SolidColorBrush(
                            Color.FromRgb(
                                21,
                                26,
                                32)),

                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(
                                41,
                                47,
                                54)),

                    BorderThickness =
                        new Thickness(1),

                    CornerRadius =
                        new CornerRadius(8),

                    Padding =
                        new Thickness(18),

                    Child =
                        contentPanel
                };


            int developerSectionIndex =
                Math.Max(
                    0,
                    mainStackPanel.Children.Count -
                    1);


            mainStackPanel.Children.Insert(
                developerSectionIndex,
                sectionTitle);

            mainStackPanel.Children.Insert(
                developerSectionIndex + 1,
                card);
        }


        private StackPanel? FindMainSettingsStackPanel()
        {
            if (Content is not Border rootBorder ||
                rootBorder.Child is not Grid rootGrid)
            {
                return null;
            }


            ScrollViewer? mainScrollViewer =
                rootGrid.Children
                    .OfType<ScrollViewer>()
                    .FirstOrDefault(
                        viewer =>
                            Grid.GetRow(viewer) ==
                            1);


            return mainScrollViewer?
                .Content as StackPanel;
        }


        private async Task LoadHolidayAppearancePreferenceAsync()
        {
            if (_holidayLauncherThemesCheckBox ==
                null)
            {
                return;
            }


            try
            {
                LauncherPreferences preferences =
                    await _preferencesService
                        .LoadAsync();


                _preferences.EnableHolidayLauncherThemes =
                    preferences.EnableHolidayLauncherThemes;


                _holidayCheckBoxLoading =
                    true;


                _holidayLauncherThemesCheckBox.IsChecked =
                    preferences.EnableHolidayLauncherThemes;
            }
            finally
            {
                _holidayCheckBoxLoading =
                    false;
            }
        }


        private async void HolidayLauncherThemesCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_holidayCheckBoxLoading ||
                _holidayLauncherThemesCheckBox ==
                    null)
            {
                return;
            }


            bool enabled =
                _holidayLauncherThemesCheckBox.IsChecked ==
                true;


            await _saveSemaphore
                .WaitAsync();


            try
            {
                _preferences.EnableHolidayLauncherThemes =
                    enabled;


                await _preferencesService
                    .SaveAsync(
                        _preferences);


                PreferencesStatusText.Text =
                    "Guardado automáticamente.";
            }
            catch
            {
                PreferencesStatusText.Text =
                    "No se pudo guardar el aspecto festivo.";
            }
            finally
            {
                _saveSemaphore.Release();
            }


            if (Owner is MainWindow mainWindow)
            {
                await mainWindow
                    .RefreshHolidayThemeFromPreferencesAsync();
            }
        }
    }
}
