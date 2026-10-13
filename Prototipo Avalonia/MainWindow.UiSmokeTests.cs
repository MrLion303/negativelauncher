using System;
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
            EndRuntimeOperation(progressSmokeController);
            RequireUiSmokeCondition(
                !DownloadProgressPanel.IsVisible &&
                !RuntimeGlobalOperationPanel.IsVisible &&
                HomePlayButton.IsVisible,
                "terminar la operación devuelve la interfaz al estado normal");

            InstancesNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => InstancesPage.IsVisible,
                TimeSpan.FromSeconds(5),
                "navegación real a Instancias");
            RequireUiSmokeCondition(InstancesPage.IsVisible, "el botón Instancias muestra la página correcta");

            GalleryNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitForUiSmokeConditionAsync(
                () => GalleryPage.IsVisible,
                TimeSpan.FromSeconds(8),
                "navegación real a Galería");
            RequireUiSmokeCondition(GalleryPage.IsVisible, "el botón Galería muestra la página correcta");

            bool previousGallerySelectionMode = _gallerySelectionMode;
            if (previousGallerySelectionMode)
                GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            RequireUiSmokeCondition(
                !GalleryDeleteSelectedButton.IsVisible &&
                string.Equals(GallerySelectionButton.Content?.ToString(), "SELECCIONAR", StringComparison.Ordinal),
                "Galería inicia en modo normal sin mostrar acciones de borrado");
            GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                _gallerySelectionMode &&
                GalleryDeleteSelectedButton.IsVisible &&
                string.Equals(GallerySelectionButton.Content?.ToString(), "CANCELAR SELECCIÓN", StringComparison.Ordinal),
                "el botón real activa el modo de selección y muestra las acciones correspondientes");
            GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RequireUiSmokeCondition(
                !_gallerySelectionMode &&
                !GalleryDeleteSelectedButton.IsVisible &&
                string.Equals(GallerySelectionButton.Content?.ToString(), "SELECCIONAR", StringComparison.Ordinal),
                "cancelar la selección restaura la Galería sin ejecutar borrados");
            if (previousGallerySelectionMode)
                GallerySelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
                "Prueba de interfaz Linux superada: acceso local, botones de navegación, selector de cuentas, OAuth y ventana de configuración.";
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
