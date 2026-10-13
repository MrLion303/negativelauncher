using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow
{
    private Window? _uiSmokeAddModpackDialog;
    private Window? _uiSmokeGalleryConfirmationDialog;
    private TaskCompletionSource<string?>? _uiSmokeAddModpackDialogCompletion;

    private async Task RunLinuxUiSmokeChecksAsync()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("NEGATIVE_LAUNCHER_UI_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (LoginGate.IsVisible)
            {
                RequireUiSmokeCondition(
                    LoginMicrosoftButton.IsVisible &&
                    LoginOptionsPanel.IsVisible &&
                    ChooseOfflineLoginButton.IsVisible &&
                    !OfflineLoginPanel.IsVisible,
                    "la pantalla de acceso inicia con las opciones de cuenta visibles y el formulario sin conexión cerrado");
                await CaptureLinuxUiSmokeScreenshotAsync(this, "login.png");

                if (!OperatingSystem.IsWindows() &&
                    string.IsNullOrWhiteSpace(EffectiveMicrosoftOAuthClientId))
                {
                    string? previousClientIdInput = MicrosoftOAuthClientIdLoginInput.Text;
                    bool previousClientPanelVisibility = MicrosoftOAuthClientPanel.IsVisible;
                    string previousLoginStatus = LoginStatusText.Text;

                    MicrosoftOAuthClientPanel.IsVisible = true;
                    MicrosoftOAuthClientIdLoginInput.Text = "esto-no-es-un-guid";
                    SaveMicrosoftOAuthClientIdLogin_Click(
                        null,
                        new RoutedEventArgs(Button.ClickEvent));

                    RequireUiSmokeCondition(
                        LoginStatusText.Text.Contains("Client ID válido", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrWhiteSpace(EffectiveMicrosoftOAuthClientId),
                        "rechazar un Client ID mal formado sin guardarlo ni iniciar OAuth");

                    MicrosoftOAuthClientIdLoginInput.Text = previousClientIdInput;
                    MicrosoftOAuthClientPanel.IsVisible = previousClientPanelVisibility;
                    LoginStatusText.Text = previousLoginStatus;

                    LoginMicrosoftButton_Click(
                        LoginMicrosoftButton,
                        new RoutedEventArgs(Button.ClickEvent));
                    RequireUiSmokeCondition(
                        MicrosoftOAuthClientPanel.IsVisible &&
                        LoginStatusText.Text.Contains("Configura y guarda el Client ID", StringComparison.OrdinalIgnoreCase),
                        "bloquear el login Microsoft cuando falta un Client ID y mostrar instrucciones");
                    LoginStatusText.Text = previousLoginStatus;
                }

                OfflineUsernameBox.Text = "UiSmokeUser";
                ChooseOfflineLoginButton_Click(
                    ChooseOfflineLoginButton,
                    new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    !LoginOptionsPanel.IsVisible &&
                    OfflineLoginPanel.IsVisible &&
                    OfflineUsernameBox.IsVisible &&
                    LoginOfflineButton.IsVisible,
                    "elegir el acceso sin conexión cambia al formulario correcto");

                BackToLoginOptionsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    LoginOptionsPanel.IsVisible && !OfflineLoginPanel.IsVisible,
                    "Volver restaura las opciones de cuenta y oculta el formulario sin conexión");

                ChooseOfflineLoginButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    !LoginOptionsPanel.IsVisible && OfflineLoginPanel.IsVisible,
                    "el formulario sin conexión puede abrirse de nuevo tras volver a las opciones");

                LoginOfflineButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                await WaitForUiSmokeConditionAsync(
                    () => !LoginGate.IsVisible,
                    TimeSpan.FromSeconds(12),
                    "acceso con perfil sin conexión");

                RequireUiSmokeCondition(
                    _accountService.IsOfflineModeActive,
                    "el perfil sin conexión queda activo");
            }

            HomeNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => HomePage.IsVisible,
                TimeSpan.FromSeconds(5),
                "navegación real a Inicio");
            RequireUiSmokeCondition(HomePage.IsVisible, "el botón Inicio muestra la página correcta");
            await CaptureLinuxUiSmokeScreenshotAsync(this, "home.png");

            var progressSmokeController = new DownloadOperationController();
            BeginRuntimeOperation(progressSmokeController, "Prueba de progreso de interfaz");
            RequireUiSmokeCondition(
                DownloadProgressPanel.IsVisible &&
                DownloadProgressTrack.IsVisible &&
                RuntimeGlobalOperationProgress.IsVisible &&
                RuntimeGlobalOperationPanel.IsVisible &&
                !HomePlayButton.IsVisible &&
                RuntimeGlobalOperationMessage.Text == "Prueba de progreso de interfaz" &&
                RuntimeGlobalOperationProgress.Value == 0,
                "la preparación muestra mensaje, barra de progreso y acciones mientras oculta Jugar");
            RuntimeGlobalOperationProgress.Value = 42;
            RequireUiSmokeCondition(
                RuntimeGlobalOperationProgress.Value == 42,
                "la barra global acepta y refleja valores de progreso");

            RuntimeGlobalOperationPauseResumeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                progressSmokeController.IsPaused &&
                RuntimeGlobalOperationPauseResumeButton.Content?.ToString() == "REANUDAR" &&
                RuntimeGlobalOperationMessage.Text == "Preparación pausada. Pulsa Reanudar para continuar.",
                "el control real pausa la operación y ofrece reanudarla");

            RuntimeGlobalOperationPauseResumeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                !progressSmokeController.IsPaused &&
                RuntimeGlobalOperationPauseResumeButton.Content?.ToString() == "PAUSAR" &&
                RuntimeGlobalOperationMessage.Text == "Reanudando la preparación de Minecraft…",
                "el control real reanuda la operación pausada");

            RuntimeGlobalOperationStopButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                progressSmokeController.IsStopped &&
                !RuntimeGlobalOperationStopButton.IsEnabled &&
                !RuntimeGlobalOperationPauseResumeButton.IsEnabled &&
                RuntimeGlobalOperationMessage.Text == "Deteniendo la preparación de Minecraft…",
                "el control real detiene la operación y bloquea acciones repetidas");

            EndRuntimeOperation(progressSmokeController);
            RequireUiSmokeCondition(
                !DownloadProgressPanel.IsVisible &&
                !RuntimeGlobalOperationPanel.IsVisible &&
                HomePlayButton.IsVisible &&
                RuntimeGlobalOperationStopButton.IsEnabled &&
                RuntimeGlobalOperationPauseResumeButton.Content?.ToString() == "PAUSAR",
                "terminar la operación devuelve la interfaz al estado normal");

            InstancesNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => InstancesPage.IsVisible,
                TimeSpan.FromSeconds(5),
                "navegación real a Instancias");
            await RefreshInstancesAsync();
            RequireUiSmokeCondition(
                InstancesPage.IsVisible &&
                HomeInstanceCount.Text == _instances.Count.ToString() &&
                InstancesEmpty.IsVisible == (_instances.Count == 0),
                "el contador y el estado vacío reflejan las instalaciones cargadas");
            await CaptureLinuxUiSmokeScreenshotAsync(this, "instances.png");

            GalleryNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => GalleryPage.IsVisible,
                TimeSpan.FromSeconds(8),
                "navegación real a Galería");
            RequireUiSmokeCondition(GalleryPage.IsVisible, "el botón Galería muestra la página correcta");

            await RefreshGalleryAsync();
            var visibleGalleryItems = GetVisibleGalleryItems();
            RequireUiSmokeCondition(
                GalleryCountText.Text == $"{visibleGalleryItems.Count} capturas" &&
                GalleryEmpty.IsVisible == (visibleGalleryItems.Count == 0) &&
                (visibleGalleryItems.Count != 0 ||
                    GalleryEmpty.Text == (_galleryItems.Count == 0
                        ? "Todavía no hay capturas."
                        : "No hay capturas que coincidan con estos filtros.")),
                "el contador, los filtros y el estado vacío de Galería coinciden con las capturas cargadas");

            GalleryInstanceFilter.SelectedIndex = 0;
            GallerySortFilter.SelectedIndex = 0;
            visibleGalleryItems = GetVisibleGalleryItems();
            bool previousGallerySelectionMode = _gallerySelectionMode;
            if (previousGallerySelectionMode)
                GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            RequireUiSmokeCondition(
                !GalleryDeleteSelectedButton.IsVisible &&
                string.Equals(GallerySelectionButton.Content?.ToString(), "SELECCIONAR", StringComparison.Ordinal),
                "Galería inicia en modo normal sin mostrar acciones de borrado");

            var smokeGalleryInstance = new Negative_Client.Models.InstalledInstance
            {
                Id = "linux-ui-smoke-" + Guid.NewGuid().ToString("N"),
                Name = "Captura de prueba Linux"
            };
            string smokeInstanceDirectory = _instanceService.GetInstanceDirectory(smokeGalleryInstance.Id);
            string smokeScreenshotDirectory = Path.Combine(smokeInstanceDirectory, "screenshots", "ui-smoke");
            string[] smokeScreenshotPaths =
            {
                Path.Combine(smokeScreenshotDirectory, "captura-antigua.png"),
                Path.Combine(smokeScreenshotDirectory, "captura-reciente.png")
            };
            _instances.Add(smokeGalleryInstance);

            try
            {
                if (Directory.Exists(smokeInstanceDirectory))
                    throw new InvalidOperationException("El ID temporal de la prueba ya existe.");

                Directory.CreateDirectory(smokeScreenshotDirectory);
                string screenshotSource = Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "negativeclient_logo.png");
                RequireUiSmokeCondition(
                    File.Exists(screenshotSource),
                    "el paquete Linux contiene el logo que se usará como captura de prueba");

                File.Copy(screenshotSource, smokeScreenshotPaths[0]);
                File.Copy(screenshotSource, smokeScreenshotPaths[1]);
                File.SetLastWriteTime(smokeScreenshotPaths[0], DateTime.Now.AddMinutes(-10));
                File.SetLastWriteTime(smokeScreenshotPaths[1], DateTime.Now.AddMinutes(-5));

                await RefreshGalleryAsync();
                var allGalleryItemsWithFixture = GetVisibleGalleryItems();
                RequireUiSmokeCondition(
                    allGalleryItemsWithFixture.Count == visibleGalleryItems.Count + 2 &&
                    GalleryCountText.Text == $"{allGalleryItemsWithFixture.Count} capturas" &&
                    !GalleryEmpty.IsVisible,
                    "Galería encuentra capturas PNG anidadas y actualiza el contador");

                Avalonia.Controls.ComboBoxItem? smokeInstanceFilter = null;
                foreach (object? item in GalleryInstanceFilter.Items)
                {
                    if (item is Avalonia.Controls.ComboBoxItem comboBoxItem &&
                        string.Equals(comboBoxItem.Tag?.ToString(), smokeGalleryInstance.Id, StringComparison.Ordinal))
                    {
                        smokeInstanceFilter = comboBoxItem;
                        break;
                    }
                }

                RequireUiSmokeCondition(
                    smokeInstanceFilter is not null,
                    "el filtro de instalaciones incluye la instancia con capturas");
                GalleryInstanceFilter.SelectedItem = smokeInstanceFilter;
                GallerySortFilter.SelectedIndex = 2;
                var filteredOldestFirst = GetVisibleGalleryItems();
                RequireUiSmokeCondition(
                    filteredOldestFirst.Count == 2 &&
                    GalleryCountText.Text == "2 capturas" &&
                    LauncherPathRules.PathsEqual(filteredOldestFirst[0].FilePath, smokeScreenshotPaths[0]) &&
                    LauncherPathRules.PathsEqual(filteredOldestFirst[1].FilePath, smokeScreenshotPaths[1]),
                    "el filtro de instalación y el orden de más antiguas a más nuevas funcionan");
                await CaptureLinuxUiSmokeScreenshotAsync(this, "gallery.png");

                RequireUiSmokeCondition(
                    GalleryImagesPanel.Children.Count == 2 &&
                    GalleryImagesPanel.Children[0] is Button,
                    "la galería construye tarjetas para las capturas cargadas");
                ((Button)GalleryImagesPanel.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    GalleryViewerOverlay.IsVisible &&
                    GalleryViewerTitle.Text == smokeGalleryInstance.Name &&
                    GalleryViewerImage.Source is not null &&
                    GalleryViewerPreviousButton.IsEnabled &&
                    GalleryViewerNextButton.IsEnabled &&
                    GalleryViewerDetails.Text.Contains("captura-antigua.png", StringComparison.Ordinal),
                    "abrir una tarjeta muestra la imagen y metadatos en el visor");
                await CaptureLinuxUiSmokeScreenshotAsync(this, "gallery-viewer.png");

                GalleryViewerNextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    GalleryViewerDetails.Text.Contains("captura-reciente.png", StringComparison.Ordinal),
                    "el visor avanza a la captura siguiente");
                GalleryViewerPreviousButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    GalleryViewerDetails.Text.Contains("captura-antigua.png", StringComparison.Ordinal),
                    "el visor regresa a la captura anterior");
                GalleryViewerCloseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    !GalleryViewerOverlay.IsVisible && GalleryViewerImage.Source is null,
                    "cerrar el visor libera la imagen y devuelve la galería");

                GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    _gallerySelectionMode && GalleryDeleteSelectedButton.IsVisible,
                    "la selección múltiple puede activarse con capturas cargadas");
                ((Button)GalleryImagesPanel.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    GalleryDeleteSelectedButton.IsEnabled &&
                    GalleryDeleteSelectedButton.Content?.ToString() == "ELIMINAR (1)",
                    "seleccionar una tarjeta habilita la eliminación y actualiza el contador");
                GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    !_gallerySelectionMode &&
                    _gallerySelection.Count == 0 &&
                    !GalleryDeleteSelectedButton.IsVisible &&
                    File.Exists(smokeScreenshotPaths[0]) && File.Exists(smokeScreenshotPaths[1]),
                    "cancelar la selección limpia el estado sin borrar capturas");

                GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (int cardIndex = 0; cardIndex < GalleryImagesPanel.Children.Count; cardIndex++)
                {
                    if (GalleryImagesPanel.Children[cardIndex] is Button screenshotCard)
                        screenshotCard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }

                RequireUiSmokeCondition(
                    _gallerySelectionMode &&
                    _gallerySelection.Count == smokeScreenshotPaths.Length &&
                    GalleryDeleteSelectedButton.IsEnabled &&
                    GalleryDeleteSelectedButton.Content?.ToString() == "ELIMINAR (2)",
                    "seleccionar las capturas temporales prepara el borrado múltiple");

                GalleryDeleteSelectedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => _uiSmokeGalleryConfirmationDialog?.IsVisible == true,
                    TimeSpan.FromSeconds(5),
                    "confirmación real del borrado de capturas");

                Window cancelGalleryDeletionDialog = _uiSmokeGalleryConfirmationDialog!;
                Button? cancelGalleryDeletionButton = null;
                bool confirmationCountIsCorrect = false;
                foreach (var visual in cancelGalleryDeletionDialog.GetVisualDescendants())
                {
                    if (visual is TextBlock textBlock &&
                        textBlock.Text == "¿Eliminar definitivamente 2 captura(s)?")
                        confirmationCountIsCorrect = true;
                    if (visual is Button button && button.Content?.ToString() == "Cancelar")
                        cancelGalleryDeletionButton = button;
                }

                RequireUiSmokeCondition(
                    confirmationCountIsCorrect && cancelGalleryDeletionButton is not null,
                    "el diálogo de borrado muestra la cantidad exacta y permite cancelar");
                cancelGalleryDeletionButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => _uiSmokeGalleryConfirmationDialog is null,
                    TimeSpan.FromSeconds(5),
                    "cancelación del borrado de capturas");
                RequireUiSmokeCondition(
                    _gallerySelectionMode &&
                    _gallerySelection.Count == smokeScreenshotPaths.Length &&
                    File.Exists(smokeScreenshotPaths[0]) && File.Exists(smokeScreenshotPaths[1]),
                    "cancelar la confirmación conserva selección y archivos");

                GalleryDeleteSelectedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => _uiSmokeGalleryConfirmationDialog?.IsVisible == true,
                    TimeSpan.FromSeconds(5),
                    "segunda confirmación real del borrado de capturas");

                Window confirmGalleryDeletionDialog = _uiSmokeGalleryConfirmationDialog!;
                Button? confirmGalleryDeletionButton = null;
                foreach (var visual in confirmGalleryDeletionDialog.GetVisualDescendants())
                {
                    if (visual is Button button && button.Content?.ToString() == "Eliminar")
                    {
                        confirmGalleryDeletionButton = button;
                        break;
                    }
                }

                RequireUiSmokeCondition(
                    confirmGalleryDeletionButton is not null,
                    "el diálogo real ofrece confirmar el borrado");
                confirmGalleryDeletionButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => _uiSmokeGalleryConfirmationDialog is null &&
                        !File.Exists(smokeScreenshotPaths[0]) && !File.Exists(smokeScreenshotPaths[1]) &&
                        _galleryItems.Count == 0 &&
                        !_gallerySelectionMode &&
                        GalleryMessage.Text == "Se eliminaron 2 capturas.",
                    TimeSpan.FromSeconds(8),
                    "borrado confirmado de capturas temporales");
                RequireUiSmokeCondition(
                    GalleryEmpty.IsVisible &&
                    GalleryCountText.Text == "0 capturas" &&
                    GalleryMessage.IsVisible,
                    "el borrado confirmado actualiza el estado vacío, el contador y el mensaje de Galería");

                File.Copy(screenshotSource, smokeScreenshotPaths[1]);
                await RefreshGalleryAsync();
                RequireUiSmokeCondition(
                    _galleryItems.Count == 1 &&
                    GalleryCountText.Text == "1 capturas" &&
                    GalleryImagesPanel.Children.Count == 1,
                    "una captura sintética vuelve a cargarse para probar el borrado desde el visor");

                ((Button)GalleryImagesPanel.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                RequireUiSmokeCondition(
                    GalleryViewerOverlay.IsVisible &&
                    GalleryViewerDeleteButton.IsEnabled,
                    "el visor expone la acción de borrar para la captura seleccionada");

                GalleryViewerDeleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => _uiSmokeGalleryConfirmationDialog?.IsVisible == true,
                    TimeSpan.FromSeconds(5),
                    "confirmación del borrado desde el visor");

                Window confirmViewerDeletionDialog = _uiSmokeGalleryConfirmationDialog!;
                Button? confirmViewerDeletionButton = null;
                bool viewerConfirmationNamesCapture = false;
                foreach (var visual in confirmViewerDeletionDialog.GetVisualDescendants())
                {
                    if (visual is TextBlock textBlock &&
                        textBlock.Text == "¿Eliminar definitivamente la captura captura-reciente.png?")
                        viewerConfirmationNamesCapture = true;
                    if (visual is Button button && button.Content?.ToString() == "Eliminar")
                    {
                        confirmViewerDeletionButton = button;
                        break;
                    }
                }

                RequireUiSmokeCondition(
                    viewerConfirmationNamesCapture && confirmViewerDeletionButton is not null,
                    "la confirmación del visor identifica el archivo seleccionado");
                confirmViewerDeletionButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => _uiSmokeGalleryConfirmationDialog is null &&
                        !File.Exists(smokeScreenshotPaths[1]) &&
                        _galleryItems.Count == 0 &&
                        !GalleryViewerOverlay.IsVisible,
                    TimeSpan.FromSeconds(8),
                    "borrado confirmado desde el visor");
                RequireUiSmokeCondition(
                    GalleryEmpty.IsVisible &&
                    GalleryCountText.Text == "0 capturas",
                    "el borrado desde el visor cierra el visor y refresca la galería");
            }
            finally
            {
                if (GalleryViewerOverlay.IsVisible)
                    GalleryViewerCloseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (_gallerySelectionMode)
                    GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                GalleryInstanceFilter.SelectedIndex = 0;
                GallerySortFilter.SelectedIndex = 0;
                _instances.Remove(smokeGalleryInstance);
                await RefreshGalleryAsync();

                if (Directory.Exists(smokeInstanceDirectory))
                    Directory.Delete(smokeInstanceDirectory, recursive: true);

                if (previousGallerySelectionMode)
                    GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            RequireUiSmokeCondition(
                ModpacksNav.IsVisible && ModpacksNav.IsEnabled,
                "el botón de instalación de modpacks está disponible en la navegación");

            ModpacksNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => _uiSmokeAddModpackDialog is not null &&
                    _uiSmokeAddModpackDialogCompletion is not null,
                TimeSpan.FromSeconds(5),
                "el botón de Modpacks abre el diálogo real de instalación");
            Window? addModpackDialog = _uiSmokeAddModpackDialog;
            RequireUiSmokeCondition(
                addModpackDialog is not null,
                "el manejador real de Modpacks construye el diálogo de instalación");
            await WaitForUiSmokeConditionAsync(
                () => addModpackDialog!.IsVisible,
                TimeSpan.FromSeconds(5),
                "el diálogo de instalación queda visible antes de probar sus controles");
            await CaptureLinuxUiSmokeScreenshotAsync(addModpackDialog!, "modpack-dialog.png");

            Avalonia.Controls.TextBox? modpackCodeInput = null;
            Button? installModpackButton = null;
            Button? cancelModpackButton = null;
            foreach (var visual in addModpackDialog!.GetVisualDescendants())
            {
                if (visual is Avalonia.Controls.TextBox textBox &&
                    textBox.Watermark == "Código de instalación")
                    modpackCodeInput = textBox;
                if (visual is Button button && button.Content?.ToString() == "INSTALAR")
                    installModpackButton = button;
                if (visual is Button cancelButton && cancelButton.Content?.ToString() == "CANCELAR")
                    cancelModpackButton = cancelButton;
            }

            RequireUiSmokeCondition(
                modpackCodeInput is not null &&
                installModpackButton is not null &&
                cancelModpackButton is not null,
                "el diálogo ofrece entrada, validación e instalación cancelable");

            bool DialogShowsText(string expected)
            {
                foreach (var visual in addModpackDialog.GetVisualDescendants())
                {
                    if (visual is TextBlock textBlock &&
                        string.Equals(textBlock.Text, expected, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }

            installModpackButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                addModpackDialog.IsVisible &&
                DialogShowsText("Introduce un código de instalación."),
                "el diálogo rechaza el código vacío y muestra su mensaje de validación");

            modpackCodeInput!.Text = "abc";
            await WaitForUiSmokeConditionAsync(
                () => string.Equals(modpackCodeInput.Text, "ABC", StringComparison.Ordinal),
                TimeSpan.FromSeconds(3),
                "el código de modpack se normaliza a mayúsculas");
            RequireUiSmokeCondition(
                modpackCodeInput.Text == "ABC",
                "el código de modpack se normaliza a mayúsculas");

            installModpackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                addModpackDialog.IsVisible &&
                DialogShowsText("El código introducido es demasiado corto."),
                "el diálogo rechaza códigos demasiado cortos sin consultar el catálogo");

            cancelModpackButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => _uiSmokeAddModpackDialog is null &&
                    _activeDownloadController is null &&
                    _runtimeOperationController is null,
                TimeSpan.FromSeconds(5),
                "cancelar desde el diálogo finaliza sin iniciar una operación");
            RequireUiSmokeCondition(
                _activeDownloadController is null && _runtimeOperationController is null,
                "cancelar desde Modpacks no inicia descargas ni modifica el catálogo");

            RequireUiSmokeCondition(
                !IsOutsideQuickAccountTrigger(QuickAccountButton),
                "pulsar el botón del selector de cuenta no se clasifica como clic externo");
            RequireUiSmokeCondition(
                IsOutsideQuickAccountTrigger(SettingsNav),
                "un clic sobre el resto de la interfaz se clasifica como externo al selector");

            bool previousDeveloperMode = _developerModeEnabled;
            try
            {
                _developerModeEnabled = true;
                DeveloperNav.IsVisible = true;
                DeveloperNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => DeveloperPage.IsVisible && DeveloperVersionPanel.IsVisible,
                    TimeSpan.FromSeconds(5),
                    "el botón de desarrollador abre la página Minecraft Vanilla");
                RequireUiSmokeCondition(
                    DeveloperPage.IsVisible && DeveloperVersionPanel.IsVisible,
                    "el botón real de desarrollador muestra Minecraft Vanilla");
                await CaptureLinuxUiSmokeScreenshotAsync(this, "developer.png");

                bool previousHolidayThemesEnabled = _preferences.EnableHolidayLauncherThemes;
                int previousHolidayPreviewIndex = _holidayPreviewIndex;
                try
                {
                    _preferences.EnableHolidayLauncherThemes = true;
                    _holidayPreviewIndex = 0;
                    ApplySeasonalTheme(force: true);
                    RequireUiSmokeCondition(
                        DeveloperHolidayThemeButton.IsVisible &&
                        DeveloperHolidayThemeButton.Content?.ToString() == "TEMA DEV: AUTO",
                        "el modo desarrollador muestra la vista previa automática de temas");

                    string[] seasonalThemes =
                    {
                        "NAVIDAD",
                        "HALLOWEEN",
                        "SAN VALENTÍN",
                        "DÍA DE LA MUJER",
                        "ANIVERSARIO",
                        "AÑO NUEVO"
                    };

                    foreach (string themeName in seasonalThemes)
                    {
                        DeveloperHolidayThemeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        RequireUiSmokeCondition(
                            DeveloperHolidayThemeButton.Content?.ToString() == "TEMA DEV: " + themeName &&
                            HolidayTintOverlay.IsVisible &&
                            HolidayTopAccentStrip.IsVisible &&
                            HolidayBottomAccentStrip.IsVisible &&
                            HolidayEffectsCanvas.IsHitTestVisible == (themeName == "ANIVERSARIO") &&
                            MainTitleText.Effect is not null,
                            "la vista previa " + themeName + " aplica sus efectos y acentos");
                    }

                    DeveloperHolidayThemeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    RequireUiSmokeCondition(
                        DeveloperHolidayThemeButton.Content?.ToString() == "TEMA DEV: AUTO",
                        "el ciclo de vistas previas vuelve al modo automático");

                    _preferences.EnableHolidayLauncherThemes = false;
                    ApplySeasonalTheme(force: true);
                    RequireUiSmokeCondition(
                        !DeveloperHolidayThemeButton.IsVisible &&
                        !HolidayTintOverlay.IsVisible &&
                        !HolidayTopAccentStrip.IsVisible &&
                        !HolidayBottomAccentStrip.IsVisible &&
                        MainTitleText.Effect is null,
                        "desactivar los temas festivos oculta el control y restaura la apariencia base");

                    _preferences.EnableHolidayLauncherThemes = true;
                    ApplySeasonalTheme(force: true);
                    RequireUiSmokeCondition(
                        DeveloperHolidayThemeButton.IsVisible,
                        "reactivar los temas festivos restaura el control de vista previa");
                }
                finally
                {
                    _preferences.EnableHolidayLauncherThemes = previousHolidayThemesEnabled;
                    _holidayPreviewIndex = previousHolidayPreviewIndex;
                    ApplySeasonalTheme(force: true);
                }

                await RefreshQuickAccountUiAsync();
                RequireUiSmokeCondition(
                    AccountQuickRoot.IsVisible,
                    "el selector rápido de cuentas aparece en la página Vanilla");
                QuickAccountButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitForUiSmokeConditionAsync(
                    () => AccountQuickPopup.IsOpen,
                    TimeSpan.FromSeconds(3),
                    "apertura del selector rápido de cuentas");
                RequireUiSmokeCondition(
                    QuickAddAccountButton.IsVisible && QuickAccountsListPanel is not null,
                    "el selector muestra sus acciones sin exigir una cuenta Microsoft real");
                AccountQuickPopup.IsOpen = false;
            }
            finally
            {
                _developerModeEnabled = previousDeveloperMode;
                DeveloperNav.IsVisible = previousDeveloperMode;
                ApplySeasonalTheme(force: true);
                OpenPage("Inicio");
            }

            SettingsNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => _settingsWindow is not null && SettingsPage.IsVisible,
                TimeSpan.FromSeconds(15),
                "apertura de Configuración mediante su botón real");

            Window? settingsWindow = _settingsWindow;
            RequireUiSmokeCondition(settingsWindow is not null, "el botón Configuración abre una ventana independiente");

            RequireUiSmokeCondition(
                SettingsPage.IsVisible,
                "el contenido completo de Configuración se adjunta a la ventana");
            RequireUiSmokeCondition(
                MicrosoftOAuthClientSettingsPanel.IsVisible &&
                MicrosoftOAuthClientIdInput is not null,
                "Ajustes expone la configuración OAuth necesaria en Linux");
            await CaptureLinuxUiSmokeScreenshotAsync(settingsWindow!, "settings.png");

            bool originalAutomaticJava = AutomaticJavaInput.IsChecked == true;
            bool originalCustomArguments = EnableCustomJavaArgumentsInput.IsChecked == true;
            double originalRamValue = RamSlider.Value;

            AutomaticJavaInput.IsChecked = false;
            SettingsAutoSave_Changed(AutomaticJavaInput, new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                JavaPathInput.IsEnabled,
                "desactivar Java automático habilita la ruta personalizada");

            AutomaticJavaInput.IsChecked = true;
            SettingsAutoSave_Changed(AutomaticJavaInput, new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                !JavaPathInput.IsEnabled,
                "activar Java automático deshabilita la ruta personalizada");

            EnableCustomJavaArgumentsInput.IsChecked = false;
            SettingsAutoSave_Changed(EnableCustomJavaArgumentsInput, new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                !CustomJavaArgumentsInput.IsEnabled,
                "desactivar argumentos personalizados bloquea el campo de argumentos");

            EnableCustomJavaArgumentsInput.IsChecked = true;
            SettingsAutoSave_Changed(EnableCustomJavaArgumentsInput, new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                CustomJavaArgumentsInput.IsEnabled,
                "activar argumentos personalizados habilita el campo de argumentos");

            double testRamValue = originalRamValue + 256 <= RamSlider.Maximum
                ? originalRamValue + 256
                : Math.Max(RamSlider.Minimum, originalRamValue - 256);
            RamSlider.Value = testRamValue;
            RequireUiSmokeCondition(
                RamValueText.Text == $"{(int)Math.Round(testRamValue)} MB",
                "el control de RAM actualiza la etiqueta con el valor seleccionado");

            AutomaticJavaInput.IsChecked = originalAutomaticJava;
            SettingsAutoSave_Changed(AutomaticJavaInput, new RoutedEventArgs(Button.ClickEvent));
            EnableCustomJavaArgumentsInput.IsChecked = originalCustomArguments;
            SettingsAutoSave_Changed(EnableCustomJavaArgumentsInput, new RoutedEventArgs(Button.ClickEvent));
            RamSlider.Value = originalRamValue;
            RequireUiSmokeCondition(
                JavaPathInput.IsEnabled == !originalAutomaticJava &&
                CustomJavaArgumentsInput.IsEnabled == originalCustomArguments &&
                RamValueText.Text == $"{(int)Math.Round(originalRamValue)} MB",
                "la prueba restaura el estado original de Java, argumentos y RAM");

            settingsWindow!.Close();
            await WaitForUiSmokeConditionAsync(
                () => SettingsPage.Parent is Panel && _settingsWindow is null,
                TimeSpan.FromSeconds(10),
                "devolución del contenido de Configuración al contenedor original");

            RequireUiSmokeCondition(
                SettingsPage.Parent is Panel && _settingsWindow is null,
                "Configuración se devuelve al contenedor original después de cerrarse");

            OpenPage("Inicio");
            RequireUiSmokeCondition(HomePage.IsVisible, "la navegación vuelve a Inicio al terminar la prueba");
            await RunLinuxConsoleSmokeChecksAsync();

            const string successMessage =
                "Prueba de interfaz Linux superada: acceso local, navegación, progreso, pausa, reanudación y detención, galería completa, consola de proceso, temas estacionales, selector de cuentas, OAuth y configuración.";
            LauncherDiagnostics.WriteInfo(successMessage);
            Console.WriteLine("NEGATIVE_LAUNCHER_UI_SMOKE_OK");
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException(
                "La prueba automatizada de interfaz Linux no se completó.",
                ex);
            Console.Error.WriteLine("NEGATIVE_LAUNCHER_UI_SMOKE_FAILED: " + ex);
        }
    }

    private async Task RunLinuxConsoleSmokeChecksAsync()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(
            "printf 'UI_SMOKE_STDOUT_MARKER\\n'; printf 'UI_SMOKE_STDERR_MARKER\\n' >&2; sleep 30");

        Process? process = null;
        GameConsoleWindow? consoleWindow = null;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("No se pudo iniciar el proceso de prueba de consola.");
            consoleWindow = new GameConsoleWindow(process, "Prueba de consola Linux");
            consoleWindow.Show();

            Avalonia.Controls.TextBox? consoleText = consoleWindow.FindControl<Avalonia.Controls.TextBox>("ConsoleTextBox");
            TextBlock? consoleStatus = consoleWindow.FindControl<TextBlock>("ConsoleStatusText");
            Button? clearConsoleButton = null;
            foreach (var visual in consoleWindow.GetVisualDescendants())
            {
                if (visual is Button button && button.Content?.ToString() == "LIMPIAR")
                {
                    clearConsoleButton = button;
                    break;
                }
            }

            RequireUiSmokeCondition(
                consoleText is not null &&
                consoleStatus is not null &&
                clearConsoleButton is not null &&
                consoleText.IsReadOnly,
                "la consola de proceso es de solo lectura y expone la acción para limpiar");

            await WaitForUiSmokeConditionAsync(
                () => consoleText!.Text?.Contains("UI_SMOKE_STDOUT_MARKER", StringComparison.Ordinal) == true &&
                    consoleText.Text.Contains("UI_SMOKE_STDERR_MARKER", StringComparison.Ordinal),
                TimeSpan.FromSeconds(8),
                "lectura de stdout y stderr del proceso de juego");

            RequireUiSmokeCondition(
                consoleText!.Text!.Contains("UI_SMOKE_STDOUT_MARKER", StringComparison.Ordinal) &&
                consoleText.Text.Contains("UI_SMOKE_STDERR_MARKER", StringComparison.Ordinal),
                "la consola muestra salida estándar y errores del proceso");

            clearConsoleButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                consoleText.Text == string.Empty,
                "el botón LIMPIAR vacía la salida visible");

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            await WaitForUiSmokeConditionAsync(
                () => !consoleWindow.IsVisible,
                TimeSpan.FromSeconds(5),
                "cierre automático de la consola cuando termina el proceso");
            RequireUiSmokeCondition(
                consoleStatus!.Text == "Minecraft se cerró.",
                "la consola informa el cierre del proceso");
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
                catch
                {
                }
            }

            if (consoleWindow?.IsVisible == true)
                consoleWindow.Close();
            process?.Dispose();
        }
    }

    private static async Task CaptureLinuxUiSmokeScreenshotAsync(Window window, string fileName)
    {
        if (!OperatingSystem.IsLinux())
            return;

        string? screenshotDirectory = Environment.GetEnvironmentVariable(
            "NEGATIVE_LAUNCHER_UI_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(screenshotDirectory))
            return;

        await Task.Delay(TimeSpan.FromMilliseconds(120));

        int pixelWidth = (int)Math.Ceiling(window.Bounds.Width);
        int pixelHeight = (int)Math.Ceiling(window.Bounds.Height);
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new InvalidOperationException(
                $"No se pudo capturar {fileName}: la ventana no tiene un tamaño renderizable.");
        }

        string fullDirectory = Path.GetFullPath(screenshotDirectory);
        Directory.CreateDirectory(fullDirectory);
        string screenshotPath = Path.Combine(fullDirectory, fileName);

        using var bitmap = new RenderTargetBitmap(
            new PixelSize(pixelWidth, pixelHeight),
            new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(screenshotPath);

        if (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length <= 8)
            throw new IOException($"La captura visual quedó vacía: {fileName}.");

        Console.WriteLine($"NEGATIVE_LAUNCHER_UI_SCREENSHOT_SAVED: {fileName}");
    }

    private static async Task WaitForUiSmokeConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string operation)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        if (!condition())
            throw new TimeoutException($"No se completó la operación de interfaz: {operation}.");
    }

    private static void RequireUiSmokeCondition(bool condition, string operation)
    {
        if (!condition)
            throw new InvalidOperationException($"Falló la comprobación de interfaz: {operation}.");

        LauncherDiagnostics.WriteInfo("Interfaz comprobada: " + operation + ".");
    }
}
