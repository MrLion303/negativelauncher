using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private ScreenshotGalleryService?
            _screenshotGalleryService;


        private List<ScreenshotGalleryItem>
            _galleryAllItems =
                new();


        private List<ScreenshotGalleryItem>
            _galleryVisibleItems =
                new();


        private bool
            _gallerySelectionMode;


        private int
            _galleryViewerIndex =
                -1;


        // =====================================================
        // ENTRAR / SALIR DE LA GALERÍA
        // =====================================================

        private async void GalleryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await EnterGalleryModeAsync();
        }


        private async Task EnterGalleryModeAsync()
        {
            AccountQuickPopup.IsOpen =
                false;


            AccountQuickRoot.Visibility =
                Visibility.Collapsed;


            InstanceOptionsButton.Visibility =
                Visibility.Collapsed;


            DeveloperVersionPanel.Visibility =
                Visibility.Collapsed;


            _developerPageActive =
                false;


            MainCenterPanel.Visibility =
                Visibility.Collapsed;


            DownloadProgressPanel.Visibility =
                Visibility.Collapsed;


            PlayButton.Visibility =
                Visibility.Collapsed;


            StatusText.Visibility =
                Visibility.Collapsed;


            MicrosoftWarningBorder.Visibility =
                Visibility.Collapsed;


            HomeBackgroundImage.Visibility =
                Visibility.Collapsed;


            HomeBackgroundOverlay.Visibility =
                Visibility.Collapsed;


            InstanceBackgroundImage.Visibility =
                Visibility.Collapsed;


            InstanceBackgroundOverlay.Visibility =
                Visibility.Collapsed;


            GalleryViewerOverlay.Visibility =
                Visibility.Collapsed;


            GalleryViewRoot.Visibility =
                Visibility.Visible;


            HomeButton.BorderBrush =
                NormalBorderBrush;


            foreach (Button button in
                _instanceButtons.Values)
            {
                button.BorderBrush =
                    NormalBorderBrush;
            }


            GalleryButton.BorderBrush =
                AccentBrush;


            PopulateGalleryInstanceFilter();


            await RefreshGalleryAsync();
        }


        /*
         * LLAMA ESTE MÉTODO desde ShowHome() y SelectInstanceAsync()
         * para que cualquier navegación normal cierre la galería.
         */
        private void ExitGalleryMode()
        {
            if (GalleryViewRoot ==
                null)
            {
                return;
            }


            GalleryViewRoot.Visibility =
                Visibility.Collapsed;


            GalleryViewerOverlay.Visibility =
                Visibility.Collapsed;


            GalleryButton.BorderBrush =
                NormalBorderBrush;


            PlayButton.Visibility =
                Visibility.Visible;


            StatusText.Visibility =
                Visibility.Visible;


            SetGallerySelectionMode(
                false);
        }


        // =====================================================
        // CARGAR Y FILTRAR
        // =====================================================

        private async void GalleryRefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RefreshGalleryAsync();
        }


        private async Task RefreshGalleryAsync()
        {
            _screenshotGalleryService ??=
                new ScreenshotGalleryService(
                    _instanceService);


            GalleryStatusText.Text =
                "Buscando capturas...";


            GalleryRefreshButton.IsEnabled =
                false;


            try
            {
                _galleryAllItems =
                    await _screenshotGalleryService
                        .LoadAllAsync(
                            _instances.Values);


                ApplyGalleryFilters();
            }
            catch (Exception ex)
            {
                GalleryStatusText.Text =
                    "No se pudo cargar la galería.";


                MessageBox.Show(
                    ex.Message,
                    "Galería",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                GalleryRefreshButton.IsEnabled =
                    true;
            }
        }


        private void PopulateGalleryInstanceFilter()
        {
            string previousId =
                GetSelectedGalleryInstanceId();


            GalleryInstanceFilterComboBox.Items.Clear();


            ComboBoxItem allItem =
                new()
                {
                    Content =
                        "Todas las instalaciones",

                    Tag =
                        string.Empty
                };


            GalleryInstanceFilterComboBox.Items.Add(
                allItem);


            foreach (InstalledInstance instance in
                _instances.Values
                    .OrderBy(
                        instance =>
                            instance.Name,
                        StringComparer.OrdinalIgnoreCase))
            {
                GalleryInstanceFilterComboBox.Items.Add(
                    new ComboBoxItem
                    {
                        Content =
                            instance.Name,

                        Tag =
                            instance.Id
                    });
            }


            ComboBoxItem? target =
                GalleryInstanceFilterComboBox
                    .Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(
                        item =>
                            string.Equals(
                                item.Tag?.ToString() ??
                                string.Empty,
                                previousId,
                                StringComparison.OrdinalIgnoreCase));


            GalleryInstanceFilterComboBox.SelectedItem =
                target ??
                allItem;
        }


        private string GetSelectedGalleryInstanceId()
        {
            if (GalleryInstanceFilterComboBox
                    .SelectedItem
                is ComboBoxItem selectedItem)
            {
                return
                    selectedItem.Tag?.ToString() ??
                    string.Empty;
            }


            return
                string.Empty;
        }


        private void GalleryFilters_Changed(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded ||
                GalleryViewRoot.Visibility !=
                Visibility.Visible)
            {
                return;
            }


            ApplyGalleryFilters();
        }


        private void GalleryDateFilterPicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded ||
                GalleryViewRoot.Visibility !=
                Visibility.Visible)
            {
                return;
            }


            ApplyGalleryFilters();
        }


        private void GalleryClearDateButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            GalleryDateFilterPicker.SelectedDate =
                null;


            ApplyGalleryFilters();
        }


        private void ApplyGalleryFilters()
        {
            string instanceId =
                GetSelectedGalleryInstanceId();


            DateTime? selectedDate =
                GalleryDateFilterPicker.SelectedDate;


            IEnumerable<ScreenshotGalleryItem> query =
                _galleryAllItems;


            if (!string.IsNullOrWhiteSpace(
                    instanceId))
            {
                query =
                    query.Where(
                        item =>
                            string.Equals(
                                item.InstanceId,
                                instanceId,
                                StringComparison.OrdinalIgnoreCase));
            }


            if (selectedDate.HasValue)
            {
                DateTime wantedDate =
                    selectedDate.Value.Date;


                query =
                    query.Where(
                        item =>
                            item.CapturedAt.Date ==
                            wantedDate);
            }


            _galleryVisibleItems =
                query
                    .OrderByDescending(
                        item =>
                            item.CapturedAt)
                    .ToList();


            foreach (ScreenshotGalleryItem item in
                _galleryVisibleItems)
            {
                item.ShowSelection =
                    _gallerySelectionMode;
            }


            GalleryItemsControl.ItemsSource =
                null;


            GalleryItemsControl.ItemsSource =
                _galleryVisibleItems;


            GalleryEmptyPanel.Visibility =
                _galleryVisibleItems.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;


            GalleryStatusText.Text =
                _galleryVisibleItems.Count ==
                1
                    ? "1 captura"
                    : $"{_galleryVisibleItems.Count} capturas";


            UpdateGalleryDeleteSelectedButton();
        }


        // =====================================================
        // SELECCIÓN MÚLTIPLE
        // =====================================================

        private void GallerySelectionModeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetGallerySelectionMode(
                !_gallerySelectionMode);
        }


        private void SetGallerySelectionMode(
            bool enabled)
        {
            _gallerySelectionMode =
                enabled;


            foreach (ScreenshotGalleryItem item in
                _galleryAllItems)
            {
                item.ShowSelection =
                    enabled;


                if (!enabled)
                {
                    item.IsSelected =
                        false;
                }
            }


            GallerySelectionModeButton.Content =
                enabled
                    ? "CANCELAR"
                    : "SELECCIONAR";


            GalleryDeleteSelectedButton.Visibility =
                enabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;


            UpdateGalleryDeleteSelectedButton();
        }


        private void ScreenshotCard_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not ScreenshotGalleryItem item)
            {
                return;
            }


            if (_gallerySelectionMode)
            {
                item.IsSelected =
                    !item.IsSelected;


                UpdateGalleryDeleteSelectedButton();

                return;
            }


            OpenGalleryViewer(
                item);
        }


        private void UpdateGalleryDeleteSelectedButton()
        {
            int selectedCount =
                _galleryAllItems.Count(
                    item =>
                        item.IsSelected);


            GalleryDeleteSelectedButton.IsEnabled =
                selectedCount > 0;


            GalleryDeleteSelectedText.Text =
                selectedCount > 0
                    ? $"Eliminar ({selectedCount})"
                    : "Eliminar seleccionadas";
        }


        private async void GalleryDeleteSelectedButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            List<ScreenshotGalleryItem> selected =
                _galleryAllItems
                    .Where(
                        item =>
                            item.IsSelected)
                    .ToList();


            if (selected.Count == 0)
            {
                return;
            }


            MessageBoxResult result =
                MessageBox.Show(
                    $"¿Eliminar definitivamente {selected.Count} captura(s)?",
                    "Eliminar capturas",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);


            if (result !=
                MessageBoxResult.Yes)
            {
                return;
            }


            _screenshotGalleryService ??=
                new ScreenshotGalleryService(
                    _instanceService);


            int deleted =
                0;


            foreach (ScreenshotGalleryItem item in
                selected)
            {
                if (_screenshotGalleryService
                    .Delete(
                        item.FilePath))
                {
                    deleted++;
                }
            }


            SetGallerySelectionMode(
                false);


            await RefreshGalleryAsync();


            GalleryStatusText.Text =
                deleted ==
                1
                    ? "1 captura eliminada."
                    : $"{deleted} capturas eliminadas.";
        }


        // =====================================================
        // CLIC DERECHO -> PORTAPAPELES
        // =====================================================

        private void ScreenshotCard_RightClick(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not ScreenshotGalleryItem item)
            {
                return;
            }


            try
            {
                _screenshotGalleryService ??=
                    new ScreenshotGalleryService(
                        _instanceService);


                Clipboard.SetImage(
                    _screenshotGalleryService
                        .LoadFullImage(
                            item.FilePath));


                GalleryStatusText.Text =
                    $"Copiada al portapapeles: {item.FileName}";


                e.Handled =
                    true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "No se pudo copiar la captura",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =====================================================
        // VISOR GRANDE
        // =====================================================

        private void OpenGalleryViewer(
            ScreenshotGalleryItem item)
        {
            _galleryViewerIndex =
                _galleryVisibleItems.FindIndex(
                    candidate =>
                        string.Equals(
                            candidate.FilePath,
                            item.FilePath,
                            StringComparison.OrdinalIgnoreCase));


            if (_galleryViewerIndex <
                0)
            {
                return;
            }


            GalleryViewerOverlay.Visibility =
                Visibility.Visible;


            ShowCurrentGalleryViewerImage();
        }


        private void ShowCurrentGalleryViewerImage()
        {
            if (_galleryVisibleItems.Count ==
                0)
            {
                GalleryViewerOverlay.Visibility =
                    Visibility.Collapsed;

                return;
            }


            _galleryViewerIndex =
                Math.Clamp(
                    _galleryViewerIndex,
                    0,
                    _galleryVisibleItems.Count - 1);


            ScreenshotGalleryItem item =
                _galleryVisibleItems[
                    _galleryViewerIndex];


            try
            {
                _screenshotGalleryService ??=
                    new ScreenshotGalleryService(
                        _instanceService);


                GalleryViewerImage.Source =
                    _screenshotGalleryService
                        .LoadFullImage(
                            item.FilePath);


                GalleryViewerTitleText.Text =
                    item.FileName;


                GalleryViewerInfoText.Text =
                    $"{item.InstanceName}  •  " +
                    $"{item.CapturedAt:dd/MM/yyyy HH:mm}  •  " +
                    $"{_galleryViewerIndex + 1} / {_galleryVisibleItems.Count}";
            }
            catch
            {
                GalleryViewerImage.Source =
                    null;


                GalleryViewerInfoText.Text =
                    "No se pudo cargar esta captura.";
            }
        }


        private void GalleryPreviousButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_galleryVisibleItems.Count ==
                0)
            {
                return;
            }


            _galleryViewerIndex--;


            if (_galleryViewerIndex <
                0)
            {
                _galleryViewerIndex =
                    _galleryVisibleItems.Count -
                    1;
            }


            ShowCurrentGalleryViewerImage();
        }


        private void GalleryNextButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_galleryVisibleItems.Count ==
                0)
            {
                return;
            }


            _galleryViewerIndex++;


            if (_galleryViewerIndex >=
                _galleryVisibleItems.Count)
            {
                _galleryViewerIndex =
                    0;
            }


            ShowCurrentGalleryViewerImage();
        }


        private void GalleryCloseViewerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            GalleryViewerOverlay.Visibility =
                Visibility.Collapsed;


            GalleryViewerImage.Source =
                null;
        }


        private async void GalleryViewerDeleteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_galleryViewerIndex <
                    0 ||
                _galleryViewerIndex >=
                    _galleryVisibleItems.Count)
            {
                return;
            }


            ScreenshotGalleryItem item =
                _galleryVisibleItems[
                    _galleryViewerIndex];


            MessageBoxResult result =
                MessageBox.Show(
                    $"¿Eliminar definitivamente esta captura?\n\n{item.FileName}",
                    "Eliminar captura",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);


            if (result !=
                MessageBoxResult.Yes)
            {
                return;
            }


            _screenshotGalleryService ??=
                new ScreenshotGalleryService(
                    _instanceService);


            if (!_screenshotGalleryService
                .Delete(
                    item.FilePath))
            {
                MessageBox.Show(
                    "No se pudo eliminar el archivo.",
                    "Galería",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }


            GalleryViewerImage.Source =
                null;


            await RefreshGalleryAsync();


            if (_galleryVisibleItems.Count ==
                0)
            {
                GalleryViewerOverlay.Visibility =
                    Visibility.Collapsed;

                return;
            }


            if (_galleryViewerIndex >=
                _galleryVisibleItems.Count)
            {
                _galleryViewerIndex =
                    _galleryVisibleItems.Count -
                    1;
            }


            ShowCurrentGalleryViewerImage();
        }
    }
}
