using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow
{
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

            OpenPage("Inicio");
            RequireUiSmokeCondition(HomePage.IsVisible, "la página Inicio se muestra");

            OpenPage("Instancias");
            RequireUiSmokeCondition(InstancesPage.IsVisible, "la página Instancias se muestra");

            OpenPage("Modpacks");
            RequireUiSmokeCondition(ModpacksPage.IsVisible, "la página Modpacks se muestra");

            OpenPage("Galería");
            RequireUiSmokeCondition(GalleryPage.IsVisible, "la página Galería se muestra");

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
            }
            finally
            {
                _developerModeEnabled = previousDeveloperMode;
                DeveloperNav.IsVisible = previousDeveloperMode;
                OpenPage("Inicio");
            }

            Task settingsTask = OpenSettingsWindowAsync(showAccounts: true);
            for (int attempt = 0; attempt < 160 && _settingsWindow is null; attempt++)
            {
                if (settingsTask.IsCompleted)
                {
                    await settingsTask;
                    break;
                }

                await Task.Delay(50);
            }

            Window? settingsWindow = _settingsWindow;
            RequireUiSmokeCondition(settingsWindow is not null, "Configuración se abre como ventana independiente");

            RequireUiSmokeCondition(
                SettingsPage.IsVisible,
                "el contenido completo de Configuración se adjunta a la ventana");

            settingsWindow!.Close();
            await settingsTask.WaitAsync(TimeSpan.FromSeconds(10));

            RequireUiSmokeCondition(
                SettingsPage.Parent is Panel && _settingsWindow is null,
                "Configuración se devuelve al contenedor original después de cerrarse");

            OpenPage("Inicio");
            RequireUiSmokeCondition(HomePage.IsVisible, "la navegación vuelve a Inicio al terminar la prueba");

            const string successMessage =
                "Prueba de interfaz Linux superada: acceso local, navegación y ventana de configuración.";
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
