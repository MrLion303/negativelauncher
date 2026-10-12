using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow
{
    private Window? _uiSmokeAddModpackDialog;

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
                OfflineUsernameBox.Text = "UiSmokeUser";
                ChooseOfflineLoginButton_Click(
                    ChooseOfflineLoginButton,
                    new RoutedEventArgs(Button.ClickEvent));
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

            Task<string?> addModpackDialogTask = ShowAddModpackDialogAsync();
            await WaitForUiSmokeConditionAsync(
                () => _uiSmokeAddModpackDialog is not null,
                TimeSpan.FromSeconds(3),
                "diálogo de instalación de modpack");
            Window? addModpackDialog = _uiSmokeAddModpackDialog;
            RequireUiSmokeCondition(
                addModpackDialog is not null,
                "el diálogo de instalación de modpack se construye");
            addModpackDialog!.Close(null);
            string? cancelledCode = await addModpackDialogTask.WaitAsync(TimeSpan.FromSeconds(8));
            RequireUiSmokeCondition(
                cancelledCode is null,
                "cancelar la instalación no inicia descargas ni modifica el catálogo");

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
                OpenPage("Minecraft Vanilla");
                RequireUiSmokeCondition(
                    DeveloperPage.IsVisible && DeveloperVersionPanel.IsVisible,
                    "la página de Minecraft Vanilla se muestra en modo desarrollador");

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
