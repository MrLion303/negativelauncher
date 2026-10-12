using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class GameConsoleWindow : Window
{
    private const int MaximumConsoleCharacters = 120000;
    private readonly Process _process;
    private readonly ConcurrentQueue<string> _pendingLines = new();
    private int _pendingLineCount;
    private int _flushScheduled;
    private bool _allowClose;
    private bool _readingStarted;

    public GameConsoleWindow(Process process, string instanceName)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        InitializeComponent();
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "negativeclient.ico");
        try
        {
            if (System.IO.File.Exists(iconPath))
                Icon = new WindowIcon(iconPath);
        }
        catch
        {
            // La consola debe seguir funcionando aunque falte el icono del cliente.
        }
        ConsoleTitleText.Text = $"Consola de Minecraft — {instanceName}";

        _process.OutputDataReceived += Process_OutputDataReceived;
        _process.ErrorDataReceived += Process_ErrorDataReceived;
        _process.Exited += Process_Exited;
        _process.EnableRaisingEvents = true;

        Opened += (_, _) =>
        {
            ScheduleConsoleFlush();
            StartReading();
        };
        Closing += ConsoleWindow_Closing;
        Closed += (_, _) => DetachProcessEvents();
        if (_process.HasExited)
            Process_Exited(this, EventArgs.Empty);
    }

    private void StartReading()
    {
        if (_readingStarted || _process.HasExited)
            return;

        _readingStarted = true;
        try
        {
            _process.BeginOutputReadLine();
        }
        catch (Exception ex)
        {
            AppendLine($"[Negative Client] No se pudo leer stdout: {ex.Message}");
        }

        try
        {
            _process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            AppendLine($"[Negative Client] No se pudo leer stderr: {ex.Message}");
        }
    }

    private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
            QueueConsoleLine(e.Data);
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
            QueueConsoleLine(e.Data);
    }

    private void AppendLine(string line) => QueueConsoleLine(line);

    private void QueueConsoleLine(string line)
    {
        _pendingLines.Enqueue(line);
        int pending = Interlocked.Increment(ref _pendingLineCount);

        // Limitar memoria si Minecraft produce más registros de los que la interfaz puede pintar.
        while (pending > 5000 && _pendingLines.TryDequeue(out _))
            pending = Interlocked.Decrement(ref _pendingLineCount);

        ScheduleConsoleFlush();
    }

    private void ScheduleConsoleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) != 0)
            return;

        Dispatcher.UIThread.Post(FlushPendingLines);
    }

    private void FlushPendingLines()
    {
        try
        {
            // No perder los primeros registros si el proceso empieza a escribir antes de que
            // el sistema de ventanas haya terminado de mostrar esta consola.
            if (!IsVisible)
                return;

            var batch = new StringBuilder();
            while (_pendingLines.TryDequeue(out string? line))
            {
                Interlocked.Decrement(ref _pendingLineCount);
                batch.AppendLine(line);
            }

            if (batch.Length == 0)
                return;

            if (batch.Length > MaximumConsoleCharacters)
                batch.Remove(0, batch.Length - MaximumConsoleCharacters);

            string current = ConsoleTextBox.Text ?? string.Empty;
            string combined = current + batch;
            if (combined.Length > MaximumConsoleCharacters)
                combined = combined[(combined.Length - MaximumConsoleCharacters)..];

            ConsoleTextBox.Text = combined;
            ConsoleTextBox.CaretIndex = combined.Length;
        }
        finally
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
            if (!_pendingLines.IsEmpty && IsVisible)
                ScheduleConsoleFlush();
        }
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                ConsoleStatusText.Text = "Minecraft se cerró.";
            }
            catch
            {
            }

            _allowClose = true;
            Close();
        });
    }

    private void ConsoleWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
            return;

        try
        {
            if (!_process.HasExited)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
            }
        }
        catch
        {
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control control && control.FindAncestorOfType<Button>() is not null)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (TryBeginWindowResizeDrag(e.GetPosition(this), e))
            return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        if (WindowState != WindowState.Maximized)
            BeginMoveDrag(e);
        e.Handled = true;
    }

    private void Root_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        TryBeginWindowResizeDrag(e.GetPosition(this), e);
    }

    private bool TryBeginWindowResizeDrag(Point point, PointerPressedEventArgs e)
    {
        if (!CanResize || WindowState != WindowState.Normal)
            return false;

        const double border = 6;
        bool left = point.X <= border;
        bool right = point.X >= Bounds.Width - border;
        bool top = point.Y <= border;
        bool bottom = point.Y >= Bounds.Height - border;
        if (!left && !right && !top && !bottom)
            return false;

        WindowEdge edge = top
            ? left ? WindowEdge.NorthWest : right ? WindowEdge.NorthEast : WindowEdge.North
            : bottom
                ? left ? WindowEdge.SouthWest : right ? WindowEdge.SouthEast : WindowEdge.South
                : left ? WindowEdge.West : WindowEdge.East;

        BeginResizeDrag(edge, e);
        e.Handled = true;
        return true;
    }

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Clear_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConsoleTextBox.Text = string.Empty;
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // En el launcher original, cerrar la consola la minimiza mientras Minecraft sigue abierto.
        WindowState = WindowState.Minimized;
    }

    private void DetachProcessEvents()
    {
        try
        {
            _process.OutputDataReceived -= Process_OutputDataReceived;
            _process.ErrorDataReceived -= Process_ErrorDataReceived;
            _process.Exited -= Process_Exited;
        }
        catch
        {
        }
    }
}
