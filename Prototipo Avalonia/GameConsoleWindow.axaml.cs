using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class GameConsoleWindow : Window
{
    private readonly Process _process;
    private bool _readingStarted;

    public GameConsoleWindow(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        InitializeComponent();

        _process.OutputDataReceived += Process_OutputDataReceived;
        _process.ErrorDataReceived += Process_ErrorDataReceived;
        _process.Exited += Process_Exited;
        _process.EnableRaisingEvents = true;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        _readingStarted = true;

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
                ConsoleStatusText.Text = $"Minecraft se cerró (código {_process.ExitCode}).";
            }
            catch
            {
                ConsoleStatusText.Text = "Minecraft se cerró.";
            }
        });
    }

    private void Clear_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConsoleTextBox.Text = string.Empty;
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
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
