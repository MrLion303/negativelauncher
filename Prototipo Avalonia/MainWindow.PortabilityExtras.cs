using System;
using Avalonia;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using System.Collections.Generic;
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
    private readonly List<HolidayParticle> _holidayParticles = new();
    private readonly Random _holidayRandom = new();
    private DispatcherTimer? _holidayEffectsTimer;
    private TimeSpan _holidayEffectsElapsed = TimeSpan.Zero;
    private string _holidayEffectsMode = "none";
    private int _holidayPreviewIndex;

    private sealed class HolidayParticle
    {
        public required Ellipse Shape { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; init; }
        public double Gravity { get; init; }
    }

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
        UpdateGlobalCountdownOpacity();
    }

    private void UpdateGlobalCountdownOpacity()
    {
        double opacity = GalleryPage.IsVisible || SettingsPage.IsVisible ? 0.22 : 1.0;
        foreach (Border banner in GlobalCountdownHost.Children.OfType<Border>())
            banner.Opacity = opacity;
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
        _holidayEffectsMode = "none";
        DeveloperHolidayThemeButton.IsVisible = _developerModeEnabled && _preferences.EnableHolidayLauncherThemes;
        DeveloperHolidayThemeButton.Content = _holidayPreviewIndex == 0
            ? "TEMA DEV: AUTO"
            : "TEMA DEV: " + new[] { "AUTO", "NAVIDAD", "HALLOWEEN", "SAN VALENTÍN", "DÍA DE LA MUJER", "ANIVERSARIO", "AÑO NUEVO" }[_holidayPreviewIndex];
        if (_preferences.EnableHolidayLauncherThemes != true)
        {
            HolidayTintOverlay.Background = Brushes.Transparent;
            HolidayTopAccentStrip.IsVisible = false;
            HolidayBottomAccentStrip.IsVisible = false;
            HomePlayButton.Background = new SolidColorBrush(Color.Parse("#38899A"));
            HomeSelectedInstanceText.Foreground = new SolidColorBrush(Color.Parse("#B8BEC6"));
            StopHolidayParticles();
            return;
        }

        LauncherHolidaySchedule schedule = _holidayPreviewIndex == 0
            ? LauncherHolidayCalendar.GetAutomaticState(monterreyDate)
            : LauncherHolidayCalendar.GetPreviewState((LauncherHolidayTheme)_holidayPreviewIndex);

        if (_holidayPreviewIndex == 0 && schedule.Theme == LauncherHolidayTheme.Default)
        {
            HolidayTintOverlay.Background = Brushes.Transparent;
            HolidayTopAccentStrip.IsVisible = false;
            HolidayBottomAccentStrip.IsVisible = false;
            HomePlayButton.Background = new SolidColorBrush(Color.Parse("#38899A"));
            HomeSelectedInstanceText.Foreground = new SolidColorBrush(Color.Parse("#B8BEC6"));
            StopHolidayParticles();
            return;
        }

        Color primary = Color.Parse(schedule.PrimaryHex);
        Color secondary = Color.Parse(schedule.SecondaryHex);
        _holidayEffectsMode = schedule.ShowFireworks
            ? "fireworks"
            : schedule.ShowBalloons
                ? "balloons"
                : "none";

        HolidayTintOverlay.Background = Brushes.Transparent;
            HolidayTopAccentStrip.IsVisible = false;
            HolidayBottomAccentStrip.IsVisible = false;
            HomePlayButton.Background = new SolidColorBrush(Color.Parse("#38899A"));
            HomeSelectedInstanceText.Foreground = new SolidColorBrush(Color.Parse("#B8BEC6"));
            StopHolidayParticles();
            return;
        }

        HolidayTintOverlay.Background = new SolidColorBrush(Color.FromArgb(34, primary.R, primary.G, primary.B));
        HolidayTopAccentStrip.Background = new SolidColorBrush(primary);
        HolidayBottomAccentStrip.Background = new SolidColorBrush(secondary);
        HolidayTopAccentStrip.IsVisible = true;
        HolidayBottomAccentStrip.IsVisible = true;
        HolidayEffectsCanvas.IsHitTestVisible = _holidayEffectsMode == "balloons";
        HomePlayButton.Background = new SolidColorBrush(primary);
        HomeSelectedInstanceText.Foreground = new SolidColorBrush(secondary);
        if (_holidayEffectsMode == "none")
            StopHolidayParticles();
        else
            StartHolidayParticles();
    }

    private void StartHolidayParticles()
    {
        if (_holidayEffectsTimer is not null)
            return;

        _holidayEffectsElapsed = TimeSpan.Zero;
        _holidayEffectsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _holidayEffectsTimer.Tick += (_, _) => TickHolidayParticles();
        _holidayEffectsTimer.Start();
    }

    private void StopHolidayParticles()
    {
        _holidayEffectsTimer?.Stop();
        _holidayEffectsTimer = null;
        HolidayEffectsCanvas.IsHitTestVisible = false;
        _holidayParticles.Clear();
        HolidayEffectsCanvas.Children.Clear();
        _holidayEffectsElapsed = TimeSpan.Zero;
    }

    private void TickHolidayParticles()
    {
        if (_holidayEffectsMode == "none" || HolidayEffectsCanvas.Bounds.Width < 10 || HolidayEffectsCanvas.Bounds.Height < 10)
            return;

        const double delta = 0.04;
        _holidayEffectsElapsed += TimeSpan.FromSeconds(delta);
        if (_holidayEffectsMode == "fireworks" && _holidayEffectsElapsed.TotalSeconds >= 1.35)
        {
            _holidayEffectsElapsed = TimeSpan.Zero;
            SpawnFirework();
        }
        else if (_holidayEffectsMode == "balloons" && _holidayEffectsElapsed.TotalSeconds >= 2.2)
        {
            _holidayEffectsElapsed = TimeSpan.Zero;
            SpawnBalloon();
        }

        for (int i = _holidayParticles.Count - 1; i >= 0; i--)
        {
            HolidayParticle particle = _holidayParticles[i];
            particle.Age += delta;
            particle.X += particle.VelocityX * delta;
            particle.Y += particle.VelocityY * delta;
            particle.VelocityY += particle.Gravity * delta;
            particle.Shape.Opacity = Math.Clamp(1.0 - particle.Age / particle.Lifetime, 0, 1);
            Canvas.SetLeft(particle.Shape, particle.X);
            Canvas.SetTop(particle.Shape, particle.Y);
            if (particle.Age >= particle.Lifetime || particle.Y < -80 || particle.Y > HolidayEffectsCanvas.Bounds.Height + 80)
            {
                HolidayEffectsCanvas.Children.Remove(particle.Shape);
                _holidayParticles.RemoveAt(i);
            }
        }
    }

    private void SpawnFirework()
    {
        double width = HolidayEffectsCanvas.Bounds.Width;
        double height = HolidayEffectsCanvas.Bounds.Height;
        if (width < 10 || height < 10) return;

        double centerX = width * (0.18 + _holidayRandom.NextDouble() * 0.64);
        double centerY = height * (0.12 + _holidayRandom.NextDouble() * 0.38);
        string[] palette = { "#F6C945", "#4ECAF6", "#FF4391", "#E83646", "#FFFFFF" };
        Color color = Color.Parse(palette[_holidayRandom.Next(palette.Length)]);
        int count = 26;
        for (int i = 0; i < count; i++)
        {
            double angle = Math.PI * 2 * i / count + _holidayRandom.NextDouble() * 0.12;
            double speed = 55 + _holidayRandom.NextDouble() * 105;
            AddHolidayParticle(centerX, centerY, color, 4 + _holidayRandom.Next(3),
                Math.Cos(angle) * speed, Math.Sin(angle) * speed, 0.85 + _holidayRandom.NextDouble() * 0.45, 68);
        }
    }

    private void SpawnBalloon()
    {
        double width = HolidayEffectsCanvas.Bounds.Width;
        double height = HolidayEffectsCanvas.Bounds.Height;
        if (width < 10 || height < 10) return;

        string[] palette = { "#FF4391", "#4ECAF6", "#F6C945", "#E83646", "#34BC5E", "#B58BFF" };
        Color color = Color.Parse(palette[_holidayRandom.Next(palette.Length)]);
        double x = width * (0.08 + _holidayRandom.NextDouble() * 0.84);
        double y = height + 28;
        AddHolidayParticle(x, y, color, 18, (_holidayRandom.NextDouble() - 0.5) * 15,
            -(32 + _holidayRandom.NextDouble() * 22), 6.5, 0, popOnClick: true);
    }

    private void AddHolidayParticle(double x, double y, Color color, double size,
        double velocityX, double velocityY, double lifetime, double gravity, bool popOnClick = false)
    {
        var shape = new Ellipse
        {
            Width = popOnClick ? size * 0.82 : size,
            Height = popOnClick ? size * 1.12 : size,
            Fill = new SolidColorBrush(color),
            Stroke = popOnClick
                ? new SolidColorBrush(Color.FromArgb(190, 255, 255, 255))
                : null,
            StrokeThickness = popOnClick ? 0.8 : 0,
            Opacity = 0.95
        };
        var particle = new HolidayParticle
        {
            Shape = shape,
            X = x,
            Y = y,
            VelocityX = velocityX,
            VelocityY = velocityY,
            Lifetime = lifetime,
            Gravity = gravity
        };

        if (popOnClick)
        {
            shape.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                double centerX = particle.X + shape.Width / 2;
                double centerY = particle.Y + shape.Height / 2;
                HolidayEffectsCanvas.Children.Remove(shape);
                _holidayParticles.Remove(particle);
                SpawnConfettiBurst(centerX, centerY);
            };
        }

        HolidayEffectsCanvas.Children.Add(shape);
        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
        _holidayParticles.Add(particle);
    }

    private void SpawnConfettiBurst(double centerX, double centerY)
    {
        string[] palette = { "#FF4391", "#4ECAF6", "#F6C945", "#E83646", "#34BC5E", "#B58BFF", "#FFFFFF" };
        const int count = 22;
        for (int i = 0; i < count; i++)
        {
            double angle = Math.PI * 2 * i / count + _holidayRandom.NextDouble() * 0.16;
            double speed = 42 + _holidayRandom.NextDouble() * 92;
            Color color = Color.Parse(palette[_holidayRandom.Next(palette.Length)]);
            AddHolidayParticle(
                centerX,
                centerY,
                color,
                3 + _holidayRandom.Next(3),
                Math.Cos(angle) * speed,
                Math.Sin(angle) * speed,
                0.65 + _holidayRandom.NextDouble() * 0.5,
                72);
        }
    }

    private void DeveloperHolidayThemeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_developerModeEnabled || !_preferences.EnableHolidayLauncherThemes)
            return;

        _holidayPreviewIndex = (_holidayPreviewIndex + 1) % 7;
        ApplySeasonalTheme(force: true);
    }

    private void StopGlobalCountdowns()
    {
        _holidayThemeTimer?.Stop();
        StopHolidayParticles();
        _globalCountdownTickTimer?.Stop();
        _globalCountdownRefreshTimer?.Stop();
        _globalCountdownCancellation.Cancel();
        _globalCountdownCancellation.Dispose();
    }
}
