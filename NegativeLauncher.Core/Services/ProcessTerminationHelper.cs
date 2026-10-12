using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Negative_Client.Services;

/// <summary>
/// Solicita el cierre normal de un proceso usando el mecanismo adecuado para cada sistema.
/// </summary>
public static class ProcessTerminationHelper
{
    private const int SigTerm = 15;

    public static bool TryRequestGracefulExit(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            if (process.HasExited)
                return true;

            if (OperatingSystem.IsWindows())
                return process.CloseMainWindow();

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                if (SendSignal(process.Id, SigTerm) == 0)
                    return true;

                // El proceso puede terminar entre HasExited y kill(2).
                // ESRCH significa que ya no existe; no es un fallo de cierre.
                return Marshal.GetLastPInvokeError() == 3;
            }

            return false;
        }
        catch (InvalidOperationException)
        {
            // El proceso pudo terminar entre la comprobación y el envío de la señal.
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int processId, int signal);
}
