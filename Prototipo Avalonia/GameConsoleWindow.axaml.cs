using System;
using System.Diagnostics;
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

        Opened += (_, _) => StartReading();
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
            AppendLine(e.Data);
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
            AppendLine(e.Data);
    }

    private void AppendLine(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible)
                return;

            string current = ConsoleTextBox.Text ?? string.Empty;
            if (current.Length > MaximumConsoleCharacters)
                current = current[Math.Max(0, current.Length - MaximumConsoleCharacters / 2)..];

            ConsoleTextBox.Text = current + line + Environment.NewLine;
            ConsoleTextBox.CaretIndex = ConsoleTextBox.Text.Length;
        });
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
