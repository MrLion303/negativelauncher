using System;
using System.Diagnostics;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow
{
    private static void StartMinecraftOutputDrain(Process process)
    {
        try
        {
            if (process.HasExited)
                return;

            // La salida debe consumirse incluso cuando la consola está oculta.
            // De lo contrario, el búfer del pipe puede llenarse y bloquear Java.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException(
                "No se pudo iniciar el drenaje de la salida de Minecraft.",
                ex);
        }
    }
}
