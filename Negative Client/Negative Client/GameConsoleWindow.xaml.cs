using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Negative_Client
{
    public partial class GameConsoleWindow : Window
    {
        private const int MaximumConsoleCharacters =
            120000;


        private readonly Process _process;


        private bool _allowClose;

        private bool _readingStarted;


        public GameConsoleWindow(
            Process process,
            string instanceName)
        {
            InitializeComponent();


            _process =
                process;


            ConsoleTitleText.Text =
                $"Consola de Minecraft — {instanceName}";


            Loaded +=
                GameConsoleWindow_Loaded;


            _process.OutputDataReceived +=
                Process_OutputDataReceived;


            _process.ErrorDataReceived +=
                Process_ErrorDataReceived;


            _process.EnableRaisingEvents =
                true;


            _process.Exited +=
                Process_Exited;
        }


        private void GameConsoleWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            StartReading();
        }


        private void StartReading()
        {
            if (_readingStarted)
            {
                return;
            }


            _readingStarted =
                true;


            try
            {
                _process.BeginOutputReadLine();
            }
            catch (Exception ex)
            {
                AppendLine(
                    $"[Negative Client] No se pudo leer stdout: {ex.Message}");
            }


            try
            {
                _process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                AppendLine(
                    $"[Negative Client] No se pudo leer stderr: {ex.Message}");
            }
        }


        private void Process_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                AppendLine(
                    e.Data);
            }
        }


        private void Process_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                AppendLine(
                    e.Data);
            }
        }


        private void AppendLine(
            string text)
        {
            Dispatcher.BeginInvoke(
                () =>
                {
                    if (ConsoleTextBox.Text.Length >
                        MaximumConsoleCharacters)
                    {
                        int removeLength =
                            ConsoleTextBox.Text.Length -
                            MaximumConsoleCharacters /
                            2;


                        ConsoleTextBox.Text =
                            ConsoleTextBox.Text[
                                removeLength..];
                    }


                    ConsoleTextBox.AppendText(
                        text +
                        Environment.NewLine);


                    ConsoleTextBox.ScrollToEnd();
                });
        }


        private void Process_Exited(
            object? sender,
            EventArgs e)
        {
            Dispatcher.BeginInvoke(
                () =>
                {
                    ConsoleStatusText.Text =
                        "Minecraft se cerró.";


                    _allowClose =
                        true;


                    Close();
                });
        }


        public void CloseForProcessExit()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    CloseForProcessExit);

                return;
            }


            _allowClose =
                true;


            Close();
        }


        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState =
                    WindowState ==
                    WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;

                return;
            }


            if (e.LeftButton ==
                MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState =
                WindowState.Minimized;
        }


        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Mientras Minecraft siga abierto, este botón minimiza.
            // Así stdout/stderr se siguen leyendo y Java no se bloquea.
            WindowState =
                WindowState.Minimized;
        }


        private void Clear_Click(
            object sender,
            RoutedEventArgs e)
        {
            ConsoleTextBox.Clear();
        }


        private void Window_Closing(
            object? sender,
            CancelEventArgs e)
        {
            if (_allowClose)
            {
                DetachProcessEvents();

                return;
            }


            try
            {
                if (!_process.HasExited)
                {
                    e.Cancel =
                        true;


                    WindowState =
                        WindowState.Minimized;
                }
            }
            catch
            {
            }
        }


        private void DetachProcessEvents()
        {
            try
            {
                _process.OutputDataReceived -=
                    Process_OutputDataReceived;


                _process.ErrorDataReceived -=
                    Process_ErrorDataReceived;


                _process.Exited -=
                    Process_Exited;
            }
            catch
            {
            }
        }
    }
}
