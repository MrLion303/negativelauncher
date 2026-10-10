using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Negative_Client.Models;
using Negative_Client.Services;

namespace NegativeLauncher.AvaloniaPrototype;

public partial class MainWindow
{
    private readonly GlobalCountdownService _globalCountdownService = new();
    private readonly System.Collections.Generic.List<GlobalCountdown> _globalCountdowns = new();
    private readonly System.Collections.Generic.Dictionary<string, TextBlock> _globalCountdownTextBlocks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _globalCountdownCancellation = new();
    private DispatcherTimer? _globalCountdownTickTimer;
    private DispatcherTimer? _globalCountdownRefreshTimer;
    private bool _globalCountdownRefreshInProgress;
    private bool _globalCountdownsStarted;
    private DispatcherTimer? _holidayThemeTimer;
    private DateTime _lastHolidayThemeDate = DateTime.MinValue;

    private void StartGlobalCountdowns()
    {
        ApplySeasonalTheme(force: true);
        _holidayThemeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _holidayThemeTimer.Tick += (_, _) => ApplySeasonalTheme();
        _holidayThemeTimer.Start();

        if (_globalCountdownsStarted)
            return;

        _globalCountdownsStarted = true;
        _globalCountdownTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _globalCountdownTickTimer.Tick += (_, _) => UpdateGlobalCountdownText(DateTimeOffset.UtcNow);
        _globalCountdownTickTimer.Start();

        _globalCountdownRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _globalCountdownRefreshTimer.Tick += async (_, _) => await RefreshGlobalCountdownsAsync();
        _globalCountdownRefreshTimer.Start();
        _ = RefreshGlobalCountdownsAsync();
    }

    private async Task RefreshGlobalCountdownsAsync()
    {
        if (_globalCountdownRefreshInProgress || _globalCountdownCancellation.IsCancellationRequested)
            return;

        _globalCountdownRefreshInProgress = true;
        try
        {
            GlobalCountdownFeed feed = await _globalCountdownService.GetFeedAsync(_globalCountdownCancellation.Token);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            _globalCountdowns.Clear();
            _globalCountdowns.AddRange(feed.Countdowns
                .Where(x => x.Active && !string.IsNullOrWhiteSpace(x.Name) && x.EndAtUtc != default && x.EndAtUtc > now)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Id) ? x.Name : x.Id, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Last())
                .OrderBy(x => x.EndAtUtc));
            RenderGlobalCountdowns();
        }
        catch (OperationCanceledException)
        {
            // La ventana se está cerrando.
        }
        catch (Exception ex)
        {
            HeaderStatus.Text = "No se pudieron actualizar los contadores";
            System.Diagnostics.Debug.WriteLine("Global countdown refresh: " + ex.Message);
        }
        finally
        {
            _globalCountdownRefreshInProgress = false;
        }
    }

    private void RenderGlobalCountdowns()
    {
        GlobalCountdownHost.Children.Clear();
        _globalCountdownTextBlocks.Clear();

        foreach (GlobalCountdown countdown in _globalCountdowns)
        {
            string key = string.IsNullOrWhiteSpace(countdown.Id) ? countdown.Name : countdown.Id;
            var label = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#EEF4F7")),
                FontSize = 24,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 930
            };
            var dot = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.Parse("#58D0E3")),
                Margin = new Avalonia.Thickness(0, 0, 16, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 0 };
            row.Children.Add(dot);
            row.Children.Add(label);
            var banner = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#EB161C23")),
                BorderBrush = new SolidColorBrush(Color.Parse("#363F4B")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Avalonia.Thickness(20, 15, 22, 15),
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Child = row
            };
            _globalCountdownTextBlocks[key] = label;
            GlobalCountdownHost.Children.Add(banner);
        }

        GlobalCountdownHost.IsVisible = _globalCountdowns.Count > 0;
        UpdateGlobalCountdownText(DateTimeOffset.UtcNow);
    }

    private void UpdateGlobalCountdownText(DateTimeOffset now)
    {
        bool removed = _globalCountdowns.RemoveAll(x => !x.Active || x.EndAtUtc <= now) > 0;
        if (removed)
        {
            RenderGlobalCountdowns();
            return;
        }

        foreach (GlobalCountdown countdown in _globalCountdowns)
        {
            string key = string.IsNullOrWhiteSpace(countdown.Id) ? countdown.Name : countdown.Id;
            if (!_globalCountdownTextBlocks.TryGetValue(key, out TextBlock? label))
                continue;

            TimeSpan remaining = countdown.EndAtUtc - now;
            int days = Math.Max(0, (int)Math.Floor(remaining.TotalDays));
            label.Text = $"{countdown.Name} en {days:00}:{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }
    }

    private void ApplySeasonalTheme(bool force = false)
    {
        DateTime monterreyDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
        if (!force && monterreyDate == _lastHolidayThemeDate)
            return;

        _lastHolidayThemeDate = monterreyDate;
        if (_preferences.EnableHolidayLauncherThemes != true)
        {
            HolidayTintOverlay.Background = Brushes.Transparent;
            HomePlayButton.Background = new SolidColorBrush(Color.Parse("#38899A"));
            return;
        }

        Color primary;
        Color secondary;
        int month = monterreyDate.Month;
        int day = monterreyDate.Day;
        if ((month == 12 && day >= 1) || (month == 1 && day <= 15))
        {
            primary = Color.Parse("#E83646");
            secondary = Color.Parse("#34BC5E");
        }
        else if ((month == 10 && day >= 20) || (month == 11 && day <= 4))
        {
            primary = Color.Parse("#FF8018");
            secondary = Color.Parse("#FFBE4A");
        }
        else if (month == 2 && day >= 10 && day <= 15)
        {
            primary = Color.Parse("#FF4391");
            secondary = Color.Parse("#FF8BBF");
        }
        else if (month == 3 && day == 8)
        {
            primary = Color.Parse("#EE4690");
            secondary = Color.Parse("#FF97CA");
        }
        else if (month == 3 && day == 27)
        {
            primary = Color.Parse("#4FC3D7");
            secondary = Color.Parse("#91E7F5");
        }
        else
        {
            HolidayTintOverlay.Background = Brushes.Transparent;
            HomePlayButton.Background = new SolidColorBrush(Color.Parse("#38899A"));
            return;
        }

        HolidayTintOverlay.Background = new SolidColorBrush(Color.FromArgb(34, primary.R, primary.G, primary.B));
        HomePlayButton.Background = new SolidColorBrush(primary);
        HomeSelectedInstanceText.Foreground = new SolidColorBrush(secondary);
    }

    private void StopGlobalCountdowns()
    {
        _holidayThemeTimer?.Stop();
        _globalCountdownTickTimer?.Stop();
        _globalCountdownRefreshTimer?.Stop();
        _globalCountdownCancellation.Cancel();
        _globalCountdownCancellation.Dispose();
    }
}
