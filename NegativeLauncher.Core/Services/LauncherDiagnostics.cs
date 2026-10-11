using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Negative_Client.Services;

/// <summary>
/// Guarda diagnósticos locales para que los errores de portabilidad puedan revisarse
/// sin depender de una terminal ni enviar datos a un servicio remoto.
/// </summary>
public static class LauncherDiagnostics
{
    private const long MaximumLogSizeBytes = 2 * 1024 * 1024;
    private static readonly object Sync = new();
    private static string? _logPath;

    public static string LogPath
    {
        get
        {
            Initialize();
            lock (Sync)
                return _logPath!;
        }
    }

    public static string LogDirectory => Path.GetDirectoryName(LogPath)!;

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_logPath is not null)
                return;

            string preferredDirectory = Path.Combine(LauncherPaths.DefaultLauncherRoot, "logs");
            try
            {
                Directory.CreateDirectory(preferredDirectory);
                _logPath = Path.Combine(preferredDirectory, "negative-launcher.log");
                RotateIfNeeded(_logPath);
                AppendCore("INFO", "Registro de diagnóstico iniciado.");
                return;
            }
            catch
            {
                // Si la carpeta de datos no admite escritura, conservar un registro local temporal.
            }

            try
            {
                string fallbackDirectory = Path.Combine(Path.GetTempPath(), "NegativeLauncher", "logs");
                Directory.CreateDirectory(fallbackDirectory);
                _logPath = Path.Combine(fallbackDirectory, "negative-launcher.log");
                RotateIfNeeded(_logPath);
                AppendCore("WARN", "No se pudo escribir en la carpeta de datos predeterminada; se usa el registro temporal.");
            }
            catch
            {
                _logPath = Path.Combine(Path.GetTempPath(), "negative-launcher.log");
            }
        }
    }

    public static void WriteInfo(string message) => Write("INFO", message);
    public static void WriteWarning(string message) => Write("WARN", message);

    public static void WriteException(string context, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write("ERROR", context + Environment.NewLine + exception);
    }

    public static void WriteFatal(object? exceptionObject)
    {
        if (exceptionObject is Exception exception)
        {
            WriteException("Excepción no controlada; puede que el launcher deba cerrarse.", exception);
            return;
        }

        Write("FATAL", "Excepción no controlada: " + (exceptionObject?.ToString() ?? "sin información"));
    }

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            Initialize();
            try
            {
                RotateIfNeeded(_logPath!);
                AppendCore(level, message);
            }
            catch
            {
                // El registro es auxiliar y nunca debe provocar un segundo error.
            }
        }
    }

    private static void AppendCore(string level, string message)
    {
        if (_logPath is null)
            return;

        string line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {message}{Environment.NewLine}";
        File.AppendAllText(_logPath, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void RotateIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < MaximumLogSizeBytes)
            return;

        string previousPath = path + ".previous";
        if (File.Exists(previousPath))
            File.Delete(previousPath);
        File.Move(path, previousPath);
    }
}
