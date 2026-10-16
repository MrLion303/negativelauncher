using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Negative_Client
{
    internal sealed class LauncherUpdateProgressWindow : Window
    {
        private readonly TextBlock _statusText;
        private readonly ProgressBar _progressBar;

        public LauncherUpdateProgressWindow()
        {
            Title = "Actualizando Negative Client";
            Width = 430;
            Height = 155;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(24, 28, 34));
            Foreground = Brushes.White;

            StackPanel panel = new()
            {
                Margin = new Thickness(22),
                VerticalAlignment = VerticalAlignment.Center
            };

            _statusText = new TextBlock
            {
                Text = "Preparando la actualización...",
                FontSize = 13,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };

            _progressBar = new ProgressBar
            {
                Height = 16,
                Minimum = 0,
                Maximum = 100,
                IsIndeterminate = true
            };

            panel.Children.Add(_statusText);
            panel.Children.Add(_progressBar);
            Content = panel;
        }

        public void ReportProgress(int percentage)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(() => ReportProgress(percentage)));
                return;
            }

            int safePercentage = Math.Clamp(percentage, 0, 100);
            _progressBar.IsIndeterminate = false;
            _progressBar.Value = safePercentage;
            _statusText.Text = $"Descargando actualización... {safePercentage}%";
        }

        public void SetMessage(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => SetMessage(message)));
                return;
            }

            _progressBar.IsIndeterminate = true;
            _statusText.Text = message;
        }
    }
}
