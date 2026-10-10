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

    private void StartGlobalCountdowns()
    {
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

    private void StopGlobalCountdowns()
    {
        _globalCountdownTickTimer?.Stop();
        _globalCountdownRefreshTimer?.Stop();
        _globalCountdownCancellation.Cancel();
        _globalCountdownCancellation.Dispose();
    }
}
