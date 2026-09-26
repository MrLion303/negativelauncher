using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private const int GlobalCountdownRefreshSeconds = 2;

        private readonly GlobalCountdownService _globalCountdownService = new();

        private readonly Dictionary<string, TextBlock> _globalCountdownTextBlocks =
            new(StringComparer.OrdinalIgnoreCase);

        private List<GlobalCountdown> _globalCountdowns = new();

        private bool _globalCountdownsInitialized;
        private bool _globalCountdownRefreshInProgress;
        private int _globalCountdownInitializationRetries;

        private StackPanel? _globalCountdownHost;
        private DispatcherTimer? _globalCountdownTickTimer;
        private DispatcherTimer? _globalCountdownRefreshTimer;
        private CancellationTokenSource? _globalCountdownCancellation;


        protected override void OnSourceInitialized(
            EventArgs e)
        {
            base.OnSourceInitialized(e);

            Dispatcher.BeginInvoke(
                new Action(
                    InitializeGlobalCountdowns),
                DispatcherPriority.Loaded);
        }


        protected override void OnActivated(
            EventArgs e)
        {
            base.OnActivated(e);

            if (!_globalCountdownsInitialized)
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        InitializeGlobalCountdowns),
                    DispatcherPriority.Loaded);

                return;
            }

            _ = RefreshGlobalCountdownsAsync();
        }


        private void InitializeGlobalCountdowns()
        {
            if (_globalCountdownsInitialized)
            {
                return;
            }

            if (HomeBackgroundImage.Parent is not Grid mainContentGrid)
            {
                _globalCountdownInitializationRetries++;

                GlobalCountdownService.WriteDiagnostic(
                    $"INIT: HomeBackgroundImage.Parent todavía no es Grid. " +
                    $"Intento {_globalCountdownInitializationRetries}.");

                if (_globalCountdownInitializationRetries <= 8)
                {
                    Dispatcher.BeginInvoke(
                        new Action(
                            InitializeGlobalCountdowns),
                        DispatcherPriority.ContextIdle);
                }

                return;
            }

            _globalCountdownHost =
                new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(18, 18, 18, 0),
                    MaxWidth = 1040,
                    Visibility = Visibility.Collapsed,
                    IsHitTestVisible = false
                };

            Grid.SetRow(_globalCountdownHost, 0);
            Grid.SetRowSpan(_globalCountdownHost, 2);
            Panel.SetZIndex(_globalCountdownHost, 10000);

            mainContentGrid.Children.Add(_globalCountdownHost);

            _globalCountdownCancellation = new CancellationTokenSource();

            _globalCountdownTickTimer =
                new DispatcherTimer(DispatcherPriority.Normal)
                {
                    Interval = TimeSpan.FromSeconds(1)
                };

            _globalCountdownTickTimer.Tick +=
                GlobalCountdownTickTimer_Tick;

            _globalCountdownTickTimer.Start();

            _globalCountdownRefreshTimer =
                new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromSeconds(
                        GlobalCountdownRefreshSeconds)
                };

            _globalCountdownRefreshTimer.Tick +=
                GlobalCountdownRefreshTimer_Tick;

            _globalCountdownRefreshTimer.Start();

            Closed += MainWindow_GlobalCountdownClosed;

            _globalCountdownsInitialized = true;

            GlobalCountdownService.WriteDiagnostic(
                "INIT OK: host visual creado y timers iniciados.");

            _ = RefreshGlobalCountdownsAsync();
        }


        private async void GlobalCountdownRefreshTimer_Tick(
            object? sender,
            EventArgs e)
        {
            await RefreshGlobalCountdownsAsync();
        }


        private void GlobalCountdownTickTimer_Tick(
            object? sender,
            EventArgs e)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            bool removedExpired =
                _globalCountdowns.RemoveAll(
                    countdown =>
                        !countdown.Active ||
                        countdown.EndAtUtc <= now) > 0;

            if (removedExpired)
            {
                RenderGlobalCountdowns();
                return;
            }

            UpdateGlobalCountdownText(now);
        }


        private async Task RefreshGlobalCountdownsAsync()
        {
            if (_globalCountdownRefreshInProgress ||
                _globalCountdownCancellation == null)
            {
                return;
            }

            _globalCountdownRefreshInProgress = true;

            try
            {
                GlobalCountdownFeed feed =
                    await _globalCountdownService.GetFeedAsync(
                        _globalCountdownCancellation.Token);

                DateTimeOffset now = DateTimeOffset.UtcNow;

                _globalCountdowns =
                    feed.Countdowns
                        .Where(
                            countdown =>
                                countdown.Active &&
                                !string.IsNullOrWhiteSpace(countdown.Name) &&
                                countdown.EndAtUtc != default &&
                                countdown.EndAtUtc > now)
                        .GroupBy(
                            GetGlobalCountdownKey,
                            StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.Last())
                        .OrderBy(countdown => countdown.EndAtUtc)
                        .ToList();

                RenderGlobalCountdowns();
            }
            catch (OperationCanceledException)
            {
                // La ventana se está cerrando.
            }
            catch (Exception ex)
            {
                GlobalCountdownService.WriteDiagnostic(
                    $"REFRESH ERROR: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _globalCountdownRefreshInProgress = false;
            }
        }


        private void RenderGlobalCountdowns()
        {
            if (_globalCountdownHost == null)
            {
                return;
            }

            _globalCountdownHost.Children.Clear();
            _globalCountdownTextBlocks.Clear();

            if (_globalCountdowns.Count == 0)
            {
                _globalCountdownHost.Visibility = Visibility.Collapsed;
                return;
            }

            foreach (GlobalCountdown countdown in _globalCountdowns)
            {
                string key = GetGlobalCountdownKey(countdown);

                TextBlock messageText =
                    new()
                    {
                        Foreground = new SolidColorBrush(
                            Color.FromRgb(238, 244, 247)),
                        FontSize = 24,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 930
                    };

                Grid contentGrid = new();

                contentGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = GridLength.Auto
                    });

                contentGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });

                Ellipse dot =
                    new()
                    {
                        Width = 14,
                        Height = 14,
                        Fill = new SolidColorBrush(
                            Color.FromRgb(88, 208, 227)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 16, 0)
                    };

                Grid.SetColumn(dot, 0);
                Grid.SetColumn(messageText, 1);

                contentGrid.Children.Add(dot);
                contentGrid.Children.Add(messageText);

                Border banner =
                    new()
                    {
                        Background = new SolidColorBrush(
                            Color.FromArgb(235, 22, 28, 35)),
                        BorderBrush = new SolidColorBrush(
                            Color.FromRgb(54, 67, 79)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(20, 15, 22, 15),
                        Margin = new Thickness(0, 0, 0, 10),
                        Child = contentGrid,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                _globalCountdownTextBlocks[key] = messageText;
                _globalCountdownHost.Children.Add(banner);
            }

            _globalCountdownHost.Visibility = Visibility.Visible;
            UpdateGlobalCountdownText(DateTimeOffset.UtcNow);
        }


        private void UpdateGlobalCountdownText(
            DateTimeOffset now)
        {
            foreach (GlobalCountdown countdown in _globalCountdowns)
            {
                string key = GetGlobalCountdownKey(countdown);

                if (!_globalCountdownTextBlocks.TryGetValue(
                        key,
                        out TextBlock? textBlock))
                {
                    continue;
                }

                TimeSpan remaining = countdown.EndAtUtc - now;

                textBlock.Text =
                    $"{countdown.Name} en {FormatGlobalCountdown(remaining)}";
            }
        }


        private static string FormatGlobalCountdown(
            TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                return "00:00:00:00";
            }

            int days = Math.Max(
                0,
                (int)Math.Floor(remaining.TotalDays));

            return
                $"{days:00}:{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }


        private static string GetGlobalCountdownKey(
            GlobalCountdown countdown)
        {
            if (!string.IsNullOrWhiteSpace(countdown.Id))
            {
                return countdown.Id;
            }

            return $"{countdown.Name}|{countdown.EndAtUtc.UtcTicks}";
        }


        private void MainWindow_GlobalCountdownClosed(
            object? sender,
            EventArgs e)
        {
            if (_globalCountdownTickTimer != null)
            {
                _globalCountdownTickTimer.Stop();
                _globalCountdownTickTimer.Tick -=
                    GlobalCountdownTickTimer_Tick;
            }

            if (_globalCountdownRefreshTimer != null)
            {
                _globalCountdownRefreshTimer.Stop();
                _globalCountdownRefreshTimer.Tick -=
                    GlobalCountdownRefreshTimer_Tick;
            }

            _globalCountdownCancellation?.Cancel();
            _globalCountdownCancellation?.Dispose();
            _globalCountdownCancellation = null;
        }
    }
}
