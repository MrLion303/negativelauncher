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

            // Solo se puede leer un stream que Process haya redirigido al launcher.
            // Si la consola está desactivada, el runtime deja ambos streams heredados
            // para evitar crear pipes que nadie necesita consumir.
            if (!process.StartInfo.RedirectStandardOutput &&
                !process.StartInfo.RedirectStandardError)
            {
                return;
            }

            if (process.StartInfo.RedirectStandardOutput)
            {
                process.OutputDataReceived += (_, _) => { };
                process.BeginOutputReadLine();
            }

            if (process.StartInfo.RedirectStandardError)
            {
                process.ErrorDataReceived += (_, _) => { };
                process.BeginErrorReadLine();
            }
        }
        catch (Exception ex)
        {
            LauncherDiagnostics.WriteException(
                "No se pudo iniciar el drenaje de la salida de Minecraft.",
                ex);
        }
    }
}
