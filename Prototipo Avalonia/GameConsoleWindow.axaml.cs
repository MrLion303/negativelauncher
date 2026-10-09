using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class GameConsoleWindow : Window
{
    private readonly Process _process;
    private bool _allowClose;

    public GameConsoleWindow(Process process, string instanceName)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        InitializeComponent();
        ConsoleTitleText.Text = $"Consola de Minecraft — {instanceName}";

        _process.OutputDataReceived += Process_OutputDataReceived;
        _process.ErrorDataReceived += Process_ErrorDataReceived;
        _process.Exited += Process_Exited;
        _process.EnableRaisingEvents = true;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        Closing += ConsoleWindow_Closing;
        Closed += (_, _) => DetachProcessEvents();
        if (_process.HasExited)
            Process_Exited(this, EventArgs.Empty);
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
            if (current.Length > 250_000)
                current = current[^150_000..];

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
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        if (WindowState == WindowState.Maximized)
            return;

        BeginMoveDrag(e);
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
