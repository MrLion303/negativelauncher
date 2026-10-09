using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class GameConsoleWindow : Window
{
    private readonly Process _process;

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

    private async void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string log = ConsoleTextBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(log))
        {
            ConsoleStatusText.Text = "No hay registros que guardar.";
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar registro de Minecraft",
                SuggestedFileName = $"negative-launcher-minecraft-{DateTime.Now:yyyyMMdd-HHmmss}.log",
                DefaultExtension = "log",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Registro de Minecraft")
                    {
                        Patterns = new[] { "*.log", "*.txt" }
                    },
                    new FilePickerFileType("Todos los archivos")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (file is null)
            {
                ConsoleStatusText.Text = "Guardado cancelado.";
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(log);
            ConsoleStatusText.Text = "Registro guardado correctamente.";
        }
        catch (Exception ex)
        {
            ConsoleStatusText.Text = "No se pudo guardar el registro: " + ex.Message;
        }
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
