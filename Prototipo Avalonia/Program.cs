using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LauncherDiagnostics.Initialize();
        LauncherDiagnostics.WriteInfo(
            $"Inicio de Negative Launcher {typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "desconocido"}; " +
            $"sistema={RuntimeInformation.OSDescription}; arquitectura={RuntimeInformation.ProcessArchitecture}; " +
            $".NET={Environment.Version}; base={AppContext.BaseDirectory}");

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            LauncherDiagnostics.WriteFatal(eventArgs.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            LauncherDiagnostics.WriteException("Excepción de una tarea no observada.", eventArgs.Exception);
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
