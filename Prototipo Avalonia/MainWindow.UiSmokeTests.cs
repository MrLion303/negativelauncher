using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow
{
    private Window? _uiSmokeAddModpackDialog;
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
                    !GalleryDeleteSelectedButton.IsVisible,
                    "cancelar la selección limpia el estado sin borrar capturas");
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
            addModpackDialog!.Close(null);
            await WaitForUiSmokeConditionAsync(
                () => _uiSmokeAddModpackDialog is null &&
                    _activeDownloadController is null &&
                    _runtimeOperationController is null,
                TimeSpan.FromSeconds(5),
                "cancelar desde el botón real de Modpacks finaliza sin iniciar una operación");
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

            const string successMessage =
                "Prueba de interfaz Linux superada: acceso local, navegación, progreso, pausa y detención, galería completa, temas estacionales, selector de cuentas, OAuth y configuración.";
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
