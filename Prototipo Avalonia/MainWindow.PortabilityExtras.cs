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
    private Color _currentHolidayAccentColor = Color.Parse("#4FC3D7");

    private sealed class HolidayParticle
    {
        public required Control Shape { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; init; }
        public double Gravity { get; init; }
        public bool IsBalloon { get; init; }
        public double BaseX { get; init; }
        public double BaseY { get; init; }
        public double FloatPhase { get; init; }
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

        // El reloj visible se actualiza localmente cada segundo; consultar GitHub cada 30 s es suficiente.
        _globalCountdownRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
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
        // En Windows solo se atenúan en la galería; Configuración es una ventana independiente.
        double opacity = GalleryPage.IsVisible ? 0.22 : 1.0;
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
        DateTime monterreyDate = LauncherHolidayCalendar.GetMonterreyNow().Date;
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
            ApplyDefaultHolidayVisuals();
            StopHolidayParticles();
            return;
        }

        LauncherHolidaySchedule schedule = _holidayPreviewIndex == 0
            ? LauncherHolidayCalendar.GetAutomaticState(monterreyDate)
            : LauncherHolidayCalendar.GetPreviewState((LauncherHolidayTheme)_holidayPreviewIndex);

        if (_holidayPreviewIndex == 0 && schedule.Theme == LauncherHolidayTheme.Default)
        {
            ApplyDefaultHolidayVisuals();
            StopHolidayParticles();
            return;
        }

        Color primary = Color.Parse(schedule.PrimaryHex);
        Color secondary = Color.Parse(schedule.SecondaryHex);
        SetHolidayAccent(primary);
        _holidayEffectsMode = schedule.ShowFireworks
            ? "fireworks"
            : schedule.ShowBalloons
                ? "balloons"
                : "none";

        var gradient = CreateHolidayGradientBrush(primary, secondary);
        MainTitleText.Foreground = gradient;
        MainTitleText.Effect = new DropShadowEffect
        {
            Color = primary,
            BlurRadius = 22,
            OffsetX = 0,
            OffsetY = 0,
            Opacity = 0.58
        };
        HomeSelectedInstanceText.Foreground = new SolidColorBrush(secondary);
        HomePlayButton.Background = new SolidColorBrush(
            BlendHolidayColor(Color.Parse("#29333E"), primary, 0.38));

        HolidayTintOverlay.Background = CreateHolidayGradientBrush(primary, secondary);
        HolidayTintOverlay.Opacity = 0.12;
        HolidayTintOverlay.IsVisible = true;
        HolidayTopAccentStrip.Background = CreateHolidayGradientBrush(primary, secondary);
        HolidayBottomAccentStrip.Background = CreateHolidayGradientBrush(secondary, primary);
        HolidayTopAccentStrip.IsVisible = true;
        HolidayBottomAccentStrip.IsVisible = true;
        DeveloperHolidayThemeButton.BorderBrush = new SolidColorBrush(primary);
        HolidayEffectsCanvas.IsHitTestVisible = _holidayEffectsMode == "balloons";

        if (_holidayEffectsMode == "none")
            StopHolidayParticles();
        else
            StartHolidayParticles();
    }

    private void ApplyDefaultHolidayVisuals()
    {
        SetHolidayAccent(Color.Parse("#4FC3D7"));
        MainTitleText.Foreground = Brushes.White;
        MainTitleText.Effect = null;
        HomeSelectedInstanceText.Foreground = new SolidColorBrush(Color.Parse("#B8BEC6"));
        HomePlayButton.Background = new SolidColorBrush(Color.Parse("#29333E"));
        DeveloperHolidayThemeButton.BorderBrush = new SolidColorBrush(Color.Parse("#485460"));
        HolidayTintOverlay.Background = Brushes.Transparent;
        HolidayTintOverlay.Opacity = 0.12;
        HolidayTintOverlay.IsVisible = false;
        HolidayTopAccentStrip.IsVisible = false;
        HolidayBottomAccentStrip.IsVisible = false;
        HolidayEffectsCanvas.IsHitTestVisible = false;
    }

    private void SetHolidayAccent(Color color)
    {
        _currentHolidayAccentColor = color;
        Resources["LauncherAccentBrush"] = new SolidColorBrush(color);
        UpdateSidebarSelection();
    }

    private static LinearGradientBrush CreateHolidayGradientBrush(Color startColor, Color endColor)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(startColor, 0));
        brush.GradientStops.Add(new GradientStop(endColor, 1));
        return brush;
    }

    private static Color BlendHolidayColor(Color baseColor, Color accentColor, double accentAmount)
    {
        double amount = Math.Clamp(accentAmount, 0.0, 1.0);
        byte red = (byte)Math.Round(baseColor.R + (accentColor.R - baseColor.R) * amount);
        byte green = (byte)Math.Round(baseColor.G + (accentColor.G - baseColor.G) * amount);
        byte blue = (byte)Math.Round(baseColor.B + (accentColor.B - baseColor.B) * amount);
        return Color.FromRgb(red, green, blue);
    }

    private void StartHolidayParticles()
    {
        if (_holidayEffectsTimer is not null)
            return;

        _holidayEffectsElapsed = _holidayEffectsMode == "fireworks"
            ? TimeSpan.FromSeconds(1.31)
            : TimeSpan.Zero;
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
        else if (_holidayEffectsMode == "balloons")
        {
            int balloonCount = _holidayParticles.Count(particle => particle.IsBalloon);
            if (balloonCount < 7)
            {
                for (int index = balloonCount; index < 7; index++)
                    SpawnBalloon();
                _holidayEffectsElapsed = TimeSpan.Zero;
            }
            else if (_holidayEffectsElapsed.TotalSeconds >= 3.8)
            {
                _holidayEffectsElapsed = TimeSpan.Zero;
                if (balloonCount < 8)
                    SpawnBalloon();
            }
        }

        for (int i = _holidayParticles.Count - 1; i >= 0; i--)
        {
            HolidayParticle particle = _holidayParticles[i];
            particle.Age += delta;
            if (particle.IsBalloon)
            {
                particle.X = particle.BaseX + Math.Sin(particle.Age * 0.95 + particle.FloatPhase) * 3.0;
                particle.Y = particle.BaseY + Math.Sin(particle.Age * 1.15 + particle.FloatPhase) * 7.0;
                Canvas.SetLeft(particle.Shape, particle.X);
                Canvas.SetTop(particle.Shape, particle.Y);
                continue;
            }

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
        if (width <= 100 || height <= 100) return;

        double centerX = width * (0.25 + _holidayRandom.NextDouble() * 0.60);
        double centerY = height * (0.16 + _holidayRandom.NextDouble() * 0.42);
        string[] palette = { "#FFD75F", "#5DCEFF", "#FF6C91", "#71E197", "#D691FF" };
        Color color = Color.Parse(palette[_holidayRandom.Next(palette.Length)]);
        const int count = 18;
        for (int i = 0; i < count; i++)
        {
            double angle = Math.PI * 2 * i / count + _holidayRandom.NextDouble() * 0.15;
            double distance = 50 + _holidayRandom.NextDouble() * 65;
            double lifetime = 0.72 + _holidayRandom.NextDouble() * 0.25;
            double speed = distance / lifetime;
            AddHolidayParticle(centerX, centerY, color, 5,
                Math.Cos(angle) * speed, Math.Sin(angle) * speed, lifetime, 36);
        }
    }

    private void SpawnBalloon()
    {
        double width = HolidayEffectsCanvas.Bounds.Width;
        double height = HolidayEffectsCanvas.Bounds.Height;
        if (width <= 100 || height <= 100 || _holidayParticles.Count(particle => particle.IsBalloon) >= 8)
            return;

        Color[] colors =
        {
            Color.FromRgb(239, 83, 80),
            Color.FromRgb(66, 165, 245),
            Color.FromRgb(102, 187, 106),
            Color.FromRgb(255, 202, 40),
            Color.FromRgb(171, 71, 188),
            Color.FromRgb(255, 112, 67),
            Color.FromRgb(38, 198, 218),
            Color.FromRgb(236, 64, 122)
        };
        Color color = colors[_holidayRandom.Next(colors.Length)];
        double left = 95 + _holidayRandom.NextDouble() * Math.Max(1, width - 185);
        double top = 65 + _holidayRandom.NextDouble() * Math.Max(85, height * 0.40);

        var balloon = new Canvas
        {
            Width = 58,
            Height = 92,
            RenderTransformOrigin = new RelativePoint(0.5, 0.45, RelativeUnit.Relative)
        };
        var stringPath = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M 29,67 L 27,91"),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 230, 230, 230)),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        var body = new Ellipse
        {
            Width = 48,
            Height = 62,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            StrokeThickness = 1.2,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(body, 5);
        Canvas.SetTop(body, 0);
        var knot = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M 24,60 L 34,60 L 29,68 Z"),
            Fill = new SolidColorBrush(color),
            IsHitTestVisible = false
        };
        balloon.Children.Add(stringPath);
        balloon.Children.Add(body);
        balloon.Children.Add(knot);
        Canvas.SetLeft(balloon, left);
        Canvas.SetTop(balloon, top);

        var particle = new HolidayParticle
        {
            Shape = balloon,
            X = left,
            Y = top,
            BaseX = left,
            BaseY = top,
            FloatPhase = _holidayRandom.NextDouble() * Math.PI * 2,
            Age = 0,
            Lifetime = double.MaxValue,
            Gravity = 0,
            IsBalloon = true
        };

        balloon.PointerReleased += (_, e) =>
        {
            if (e.GetCurrentPoint(balloon).Properties.PointerUpdateKind != Avalonia.Input.PointerUpdateKind.LeftButtonReleased)
                return;
            e.Handled = true;
            double centerX = particle.X + 29;
            double centerY = particle.Y + 31;
            HolidayEffectsCanvas.Children.Remove(balloon);
            _holidayParticles.Remove(particle);
            SpawnConfettiBurst(centerX, centerY);
        };

        HolidayEffectsCanvas.Children.Add(balloon);
        _holidayParticles.Add(particle);
    }

    private void AddHolidayParticle(double x, double y, Color color, double size,
        double velocityX, double velocityY, double lifetime, double gravity, bool popOnClick = false, bool rectangle = false)
    {
        Control shape;
        if (rectangle)
        {
            shape = new Rectangle
            {
                Width = size,
                Height = size * 1.6,
                Fill = new SolidColorBrush(color),
                IsHitTestVisible = false,
                Opacity = 0.95
            };
        }
        else
        {
            shape = new Ellipse
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
        }

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

        if (popOnClick && shape is Ellipse ellipse)
        {
            ellipse.PointerPressed += (_, e) =>
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
        Color[] colors =
        {
            Color.FromRgb(255, 82, 82),
            Color.FromRgb(255, 213, 79),
            Color.FromRgb(105, 240, 174),
            Color.FromRgb(64, 196, 255),
            Color.FromRgb(224, 64, 251)
        };
        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            double angle = _holidayRandom.NextDouble() * Math.PI * 2;
            double distance = 35 + _holidayRandom.NextDouble() * 55;
            double lifetime = 0.52;
            double speed = distance / lifetime;
            Color color = colors[_holidayRandom.Next(colors.Length)];
            AddHolidayParticle(
                centerX,
                centerY,
                color,
                5,
                Math.Cos(angle) * speed,
                Math.Sin(angle) * speed + 25 / lifetime,
                lifetime,
                0,
                rectangle: true);
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
