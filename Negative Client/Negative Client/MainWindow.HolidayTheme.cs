using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private static readonly Color DefaultAccentColor =
            Color.FromRgb(79, 195, 215);

        private static readonly Color DefaultLogoColor =
            Color.FromRgb(106, 216, 232);

        private static readonly Color DefaultSecondaryTextColor =
            Color.FromRgb(184, 190, 198);

        private static readonly Color DefaultMainTitleColor =
            Colors.White;

        private static readonly Color DefaultPlayButtonColor =
            Color.FromRgb(41, 51, 62);

        private static readonly Color DefaultDeveloperThemeBorderColor =
            Color.FromRgb(72, 84, 96);

        private const int MaximumBirthdayBalloons =
            8;

        private const string BirthdayBalloonTag =
            "NegativeClientHolidayBalloon";


        private readonly Random _holidayRandom =
            new Random();


        private bool _holidayVisualsInitialized;
        private bool _holidayVisualsEnabled =
            true;

        private HolidayVisualTheme? _developerHolidayOverride;

        private DispatcherTimer? _holidayMidnightTimer;
        private DispatcherTimer? _holidayFireworksTimer;
        private DispatcherTimer? _holidayBalloonTimer;

        private Border? _holidayTintOverlay;
        private Border? _holidayTopAccentStrip;
        private Border? _holidayBottomAccentStrip;
        private Canvas? _holidayEffectsCanvas;
        private Button? _developerHolidayThemeButton;


        protected override void OnInitialized(
            EventArgs e)
        {
            base.OnInitialized(e);

            // Ajustes visuales que conviene aplicar antes del primer frame
            // (ocultar el logo/N y preparar el icono de Inicio).
            ApplyUiPolishBeforeFirstRender();
        }


        protected override async void OnContentRendered(
            EventArgs e)
        {
            base.OnContentRendered(e);


            if (_holidayVisualsInitialized)
            {
                return;
            }


            _holidayVisualsInitialized =
                true;


            InitializeHolidayVisualLayer();
            InitializeResourcePackSelectionHook();
            InitializeUiPolish();


            AddHandler(
                Button.ClickEvent,
                new RoutedEventHandler(
                    HolidayAnyButton_Click),
                handledEventsToo: true);


            Activated +=
                MainWindow_HolidayActivated;


            Closed +=
                MainWindow_HolidayClosed;


            await RefreshHolidayThemeFromPreferencesAsync();

            ScheduleNextHolidayMidnightRefresh();

            UpdateDeveloperHolidayButtonVisibility();
        }


        internal async System.Threading.Tasks.Task
            RefreshHolidayThemeFromPreferencesAsync()
        {
            try
            {
                LauncherPreferences preferences =
                    await _launcherPreferencesService
                        .LoadAsync();


                _holidayVisualsEnabled =
                    preferences.EnableHolidayLauncherThemes;


                ApplyCurrentHolidayVisualState();

                UpdateDeveloperHolidayButtonVisibility();
            }
            catch
            {
                _holidayVisualsEnabled =
                    true;


                ApplyCurrentHolidayVisualState();
            }
        }


        private void InitializeHolidayVisualLayer()
        {
            if (Content is not Border rootBorder ||
                rootBorder.Child is not Grid rootGrid)
            {
                return;
            }


            _holidayTintOverlay =
                new Border
                {
                    IsHitTestVisible =
                        false,

                    Visibility =
                        Visibility.Collapsed,

                    Opacity =
                        0.12
                };


            Grid.SetRowSpan(
                _holidayTintOverlay,
                2);

            Panel.SetZIndex(
                _holidayTintOverlay,
                150);


            _holidayTopAccentStrip =
                new Border
                {
                    Height =
                        5,

                    VerticalAlignment =
                        VerticalAlignment.Top,

                    IsHitTestVisible =
                        false,

                    Visibility =
                        Visibility.Collapsed
                };


            Grid.SetRow(
                _holidayTopAccentStrip,
                0);

            Panel.SetZIndex(
                _holidayTopAccentStrip,
                151);


            _holidayBottomAccentStrip =
                new Border
                {
                    Height =
                        5,

                    VerticalAlignment =
                        VerticalAlignment.Bottom,

                    IsHitTestVisible =
                        false,

                    Visibility =
                        Visibility.Collapsed
                };


            Grid.SetRow(
                _holidayBottomAccentStrip,
                1);

            Panel.SetZIndex(
                _holidayBottomAccentStrip,
                151);


            _holidayEffectsCanvas =
                new Canvas
                {
                    Background =
                        null,

                    ClipToBounds =
                        true
                };


            Grid.SetRowSpan(
                _holidayEffectsCanvas,
                2);

            Panel.SetZIndex(
                _holidayEffectsCanvas,
                210);


            _developerHolidayThemeButton =
                CreateDeveloperHolidayThemeButton();


            Grid.SetRow(
                _developerHolidayThemeButton,
                1);

            Panel.SetZIndex(
                _developerHolidayThemeButton,
                220);


            rootGrid.Children.Add(
                _holidayTintOverlay);

            rootGrid.Children.Add(
                _holidayTopAccentStrip);

            rootGrid.Children.Add(
                _holidayBottomAccentStrip);

            rootGrid.Children.Add(
                _holidayEffectsCanvas);

            rootGrid.Children.Add(
                _developerHolidayThemeButton);
        }


        private Button CreateDeveloperHolidayThemeButton()
        {
            Button button =
                new Button
                {
                    Content =
                        "TEMA DEV: AUTO",

                    Height =
                        34,

                    MinWidth =
                        145,

                    Padding =
                        new Thickness(
                            12,
                            0,
                            12,
                            0),

                    HorizontalAlignment =
                        HorizontalAlignment.Right,

                    VerticalAlignment =
                        VerticalAlignment.Bottom,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            22,
                            20),

                    Background =
                        new SolidColorBrush(
                            Color.FromArgb(
                                225,
                                24,
                                30,
                                36)),

                    Foreground =
                        Brushes.White,

                    BorderBrush =
                        new SolidColorBrush(
                            Color.FromRgb(
                                72,
                                84,
                                96)),

                    BorderThickness =
                        new Thickness(1),

                    Cursor =
                        Cursors.Hand,

                    FontSize =
                        11,

                    FontWeight =
                        FontWeights.SemiBold,

                    Visibility =
                        Visibility.Collapsed,

                    ToolTip =
                        "Solo modo desarrollador: cambia el aspecto festivo sin depender de la fecha."
                };


            button.Click +=
                DeveloperHolidayThemeButton_Click;


            return button;
        }


        private void DeveloperHolidayThemeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HolidayVisualTheme?[] previewCycle =
            {
                null,
                HolidayVisualTheme.Christmas,
                HolidayVisualTheme.Halloween,
                HolidayVisualTheme.Valentine,
                HolidayVisualTheme.WomensDay,
                HolidayVisualTheme.Birthday,
                HolidayVisualTheme.NewYear
            };


            int currentIndex =
                Array.IndexOf(
                    previewCycle,
                    _developerHolidayOverride);


            int nextIndex =
                (currentIndex + 1) %
                previewCycle.Length;


            _developerHolidayOverride =
                previewCycle[nextIndex];


            ApplyCurrentHolidayVisualState();

            RefreshDeveloperHolidayButtonText();
        }


        private void RefreshDeveloperHolidayButtonText()
        {
            if (_developerHolidayThemeButton ==
                null)
            {
                return;
            }


            if (_developerHolidayOverride ==
                null)
            {
                _developerHolidayThemeButton.Content =
                    "TEMA DEV: AUTO";

                return;
            }


            HolidayVisualState state =
                HolidayThemeService
                    .GetPreviewState(
                        _developerHolidayOverride.Value);


            _developerHolidayThemeButton.Content =
                "TEMA DEV: " +
                state.DisplayName.ToUpperInvariant();
        }


        private void HolidayAnyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                new Action(
                    UpdateDeveloperHolidayButtonVisibility),
                DispatcherPriority.Background);
        }


        private async void MainWindow_HolidayActivated(
            object? sender,
            EventArgs e)
        {
            await RefreshHolidayThemeFromPreferencesAsync();
        }


        private void MainWindow_HolidayClosed(
            object? sender,
            EventArgs e)
        {
            _holidayMidnightTimer?.Stop();
            _holidayFireworksTimer?.Stop();
            _holidayBalloonTimer?.Stop();
        }


        private void UpdateDeveloperHolidayButtonVisibility()
        {
            if (_developerHolidayThemeButton ==
                null)
            {
                return;
            }


            bool homeVisible =
                _selectedInstance ==
                    null &&
                !_developerPageActive &&
                GalleryViewRoot.Visibility !=
                    Visibility.Visible;


            _developerHolidayThemeButton.Visibility =
                _developerModeEnabled &&
                homeVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }


        private void ApplyCurrentHolidayVisualState()
        {
            HolidayVisualState state;


            // El selector DEV sirve específicamente para probar los aspectos.
            // Por eso una selección manual de desarrollador puede mostrarlos
            // aunque los aspectos automáticos estén desactivados.
            if (_developerHolidayOverride !=
                null)
            {
                state =
                    HolidayThemeService
                        .GetPreviewState(
                            _developerHolidayOverride.Value);
            }
            else if (_holidayVisualsEnabled)
            {
                state =
                    HolidayThemeService
                        .GetAutomaticState();
            }
            else
            {
                state =
                    HolidayThemeService
                        .GetPreviewState(
                            HolidayVisualTheme.Default);
            }


            ApplyHolidayVisualState(
                state);
        }


        private void ApplyHolidayVisualState(
            HolidayVisualState state)
        {
            StopHolidayEffects();


            Color primary =
                state.PrimaryColor;

            Color secondary =
                state.SecondaryColor;


            if (AccentBrush is SolidColorBrush accentBrush &&
                !accentBrush.IsFrozen)
            {
                accentBrush.Color =
                    primary;
            }


            if (state.Theme ==
                HolidayVisualTheme.Default)
            {
                TopLeftLogoFallbackText.Foreground =
                    new SolidColorBrush(
                        DefaultLogoColor);


                SelectedModpackText.Foreground =
                    new SolidColorBrush(
                        DefaultSecondaryTextColor);


                MainTitleText.Foreground =
                    new SolidColorBrush(
                        DefaultMainTitleColor);

                MainTitleText.Effect =
                    null;


                PlayButton.Background =
                    new SolidColorBrush(
                        DefaultPlayButtonColor);


                if (_developerHolidayThemeButton !=
                    null)
                {
                    _developerHolidayThemeButton.BorderBrush =
                        new SolidColorBrush(
                            DefaultDeveloperThemeBorderColor);
                }


                if (_holidayTintOverlay !=
                    null)
                {
                    _holidayTintOverlay.Visibility =
                        Visibility.Collapsed;
                }


                if (_holidayTopAccentStrip !=
                    null)
                {
                    _holidayTopAccentStrip.Visibility =
                        Visibility.Collapsed;
                }


                if (_holidayBottomAccentStrip !=
                    null)
                {
                    _holidayBottomAccentStrip.Visibility =
                        Visibility.Collapsed;
                }
            }
            else
            {
                LinearGradientBrush gradient =
                    new LinearGradientBrush(
                        primary,
                        secondary,
                        0);


                TopLeftLogoFallbackText.Foreground =
                    new SolidColorBrush(
                        primary);


                SelectedModpackText.Foreground =
                    new SolidColorBrush(
                        secondary);


                // El título principal ahora toma directamente el degradado
                // del tema, además del brillo. Esto hace que el cambio
                // festivo sea visible sin depender solo de un tinte tenue.
                MainTitleText.Foreground =
                    new LinearGradientBrush(
                        primary,
                        secondary,
                        0);

                MainTitleText.Effect =
                    new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color =
                            primary,

                        BlurRadius =
                            22,

                        ShadowDepth =
                            0,

                        Opacity =
                            0.58
                    };


                PlayButton.Background =
                    new SolidColorBrush(
                        BlendHolidayColor(
                            DefaultPlayButtonColor,
                            primary,
                            0.38));


                if (_developerHolidayThemeButton !=
                    null)
                {
                    _developerHolidayThemeButton.BorderBrush =
                        new SolidColorBrush(
                            primary);
                }


                if (_holidayTintOverlay !=
                    null)
                {
                    _holidayTintOverlay.Background =
                        gradient;

                    _holidayTintOverlay.Visibility =
                        Visibility.Visible;
                }


                if (_holidayTopAccentStrip !=
                    null)
                {
                    _holidayTopAccentStrip.Background =
                        new LinearGradientBrush(
                            primary,
                            secondary,
                            0);

                    _holidayTopAccentStrip.Visibility =
                        Visibility.Visible;
                }


                if (_holidayBottomAccentStrip !=
                    null)
                {
                    _holidayBottomAccentStrip.Background =
                        new LinearGradientBrush(
                            secondary,
                            primary,
                            0);

                    _holidayBottomAccentStrip.Visibility =
                        Visibility.Visible;
                }
            }


            UpdateSidebarSelection();


            if (state.ShowBalloons)
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        StartBirthdayBalloons),
                    DispatcherPriority.Loaded);
            }


            if (state.ShowFireworks)
            {
                StartFireworks();
            }
        }


        private static Color BlendHolidayColor(
            Color baseColor,
            Color accentColor,
            double accentAmount)
        {
            double amount =
                Math.Clamp(
                    accentAmount,
                    0.0,
                    1.0);


            byte red =
                (byte)Math.Round(
                    baseColor.R +
                    (accentColor.R -
                     baseColor.R) *
                    amount);

            byte green =
                (byte)Math.Round(
                    baseColor.G +
                    (accentColor.G -
                     baseColor.G) *
                    amount);

            byte blue =
                (byte)Math.Round(
                    baseColor.B +
                    (accentColor.B -
                     baseColor.B) *
                    amount);


            return
                Color.FromRgb(
                    red,
                    green,
                    blue);
        }


        private void StopHolidayEffects()
        {
            _holidayFireworksTimer?.Stop();
            _holidayBalloonTimer?.Stop();


            if (_holidayEffectsCanvas !=
                null)
            {
                _holidayEffectsCanvas.Children.Clear();
            }
        }


        private void StartBirthdayBalloons()
        {
            if (_holidayEffectsCanvas ==
                null)
            {
                return;
            }


            _holidayBalloonTimer?.Stop();

            _holidayEffectsCanvas.Children.Clear();


            // Un poco más que antes: aparecen 7 de entrada.
            // Después el timer añade más poco a poco, sin superar 8.
            AddBirthdayBalloons(
                7);


            _holidayBalloonTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(
                            3.8)
                };


            _holidayBalloonTimer.Tick +=
                (_, _) =>
                {
                    int currentCount =
                        CountBirthdayBalloons();


                    if (currentCount >=
                        MaximumBirthdayBalloons)
                    {
                        return;
                    }


                    AddBirthdayBalloons(
                        1);
                };


            _holidayBalloonTimer.Start();
        }


        private void AddBirthdayBalloons(
            int requestedCount)
        {
            if (_holidayEffectsCanvas ==
                null ||
                requestedCount <=
                0)
            {
                return;
            }


            int currentCount =
                CountBirthdayBalloons();

            int availableSlots =
                Math.Max(
                    0,
                    MaximumBirthdayBalloons -
                    currentCount);

            int amountToAdd =
                Math.Min(
                    requestedCount,
                    availableSlots);


            if (amountToAdd <=
                0)
            {
                return;
            }


            double width =
                _holidayEffectsCanvas.ActualWidth;

            double height =
                _holidayEffectsCanvas.ActualHeight;


            if (width <=
                    0 ||
                height <=
                    0)
            {
                width =
                    ActualWidth;

                height =
                    ActualHeight;
            }


            for (int index =
                     0;
                 index <
                     amountToAdd;
                 index++)
            {
                Color color =
                    GetBirthdayBalloonColor();


                Canvas balloon =
                    CreateBalloon(
                        color);


                double left =
                    95 +
                    _holidayRandom.NextDouble() *
                    Math.Max(
                        1,
                        width -
                        185);

                double top =
                    65 +
                    _holidayRandom.NextDouble() *
                    Math.Max(
                        85,
                        height *
                        0.40);


                Canvas.SetLeft(
                    balloon,
                    left);

                Canvas.SetTop(
                    balloon,
                    top);


                _holidayEffectsCanvas.Children.Add(
                    balloon);


                DoubleAnimation floatingAnimation =
                    new DoubleAnimation
                    {
                        From =
                            top -
                            7,

                        To =
                            top +
                            7,

                        Duration =
                            TimeSpan.FromSeconds(
                                1.7 +
                                _holidayRandom.NextDouble() *
                                1.1),

                        AutoReverse =
                            true,

                        RepeatBehavior =
                            RepeatBehavior.Forever,

                        EasingFunction =
                            new SineEase
                            {
                                EasingMode =
                                    EasingMode.EaseInOut
                            }
                    };


                balloon.BeginAnimation(
                    Canvas.TopProperty,
                    floatingAnimation);
            }
        }


        private int CountBirthdayBalloons()
        {
            if (_holidayEffectsCanvas ==
                null)
            {
                return 0;
            }


            int count =
                0;


            foreach (UIElement child in
                _holidayEffectsCanvas.Children)
            {
                if (child is
                        Canvas balloon &&
                    string.Equals(
                        balloon.Tag as string,
                        BirthdayBalloonTag,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }


            return count;
        }


        private Color GetBirthdayBalloonColor()
        {
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


            return
                colors[
                    _holidayRandom.Next(
                        colors.Length)];
        }


        private Canvas CreateBalloon(
            Color color)
        {
            Canvas balloon =
                new Canvas
                {
                    Width =
                        58,

                    Height =
                        92,

                    Tag =
                        BirthdayBalloonTag,

                    Cursor =
                        Cursors.Hand,

                    RenderTransformOrigin =
                        new Point(
                            0.5,
                            0.45),

                    RenderTransform =
                        new ScaleTransform(
                            1,
                            1)
                };


            Ellipse body =
                new Ellipse
                {
                    Width =
                        48,

                    Height =
                        62,

                    Fill =
                        new SolidColorBrush(
                            color),

                    Stroke =
                        new SolidColorBrush(
                            Color.FromArgb(
                                180,
                                255,
                                255,
                                255)),

                    StrokeThickness =
                        1.2
                };


            Canvas.SetLeft(
                body,
                5);

            Canvas.SetTop(
                body,
                0);


            Polygon knot =
                new Polygon
                {
                    Points =
                        new PointCollection
                        {
                            new Point(24, 60),
                            new Point(34, 60),
                            new Point(29, 68)
                        },

                    Fill =
                        new SolidColorBrush(
                            color)
                };


            Line stringLine =
                new Line
                {
                    X1 =
                        29,

                    Y1 =
                        67,

                    X2 =
                        27,

                    Y2 =
                        91,

                    Stroke =
                        new SolidColorBrush(
                            Color.FromArgb(
                                180,
                                230,
                                230,
                                230)),

                    StrokeThickness =
                        1
                };


            balloon.Children.Add(
                stringLine);

            balloon.Children.Add(
                body);

            balloon.Children.Add(
                knot);


            balloon.MouseLeftButtonUp +=
                BirthdayBalloon_MouseLeftButtonUp;


            return balloon;
        }


        private void BirthdayBalloon_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is not Canvas balloon ||
                _holidayEffectsCanvas ==
                    null)
            {
                return;
            }


            e.Handled =
                true;


            balloon.BeginAnimation(
                Canvas.TopProperty,
                null);


            double left =
                Canvas.GetLeft(
                    balloon);

            double top =
                Canvas.GetTop(
                    balloon);


            CreateConfettiBurst(
                left + 29,
                top + 31);


            if (balloon.RenderTransform is
                ScaleTransform scale)
            {
                DoubleAnimation scaleAnimation =
                    new DoubleAnimation(
                        1,
                        1.7,
                        TimeSpan.FromMilliseconds(
                            150));


                scale.BeginAnimation(
                    ScaleTransform.ScaleXProperty,
                    scaleAnimation);

                scale.BeginAnimation(
                    ScaleTransform.ScaleYProperty,
                    scaleAnimation);
            }


            DoubleAnimation fade =
                new DoubleAnimation(
                    1,
                    0,
                    TimeSpan.FromMilliseconds(
                        150));


            fade.Completed +=
                (_, _) =>
                {
                    _holidayEffectsCanvas?
                        .Children
                        .Remove(
                            balloon);
                };


            balloon.BeginAnimation(
                OpacityProperty,
                fade);
        }


        private void CreateConfettiBurst(
            double centerX,
            double centerY)
        {
            if (_holidayEffectsCanvas ==
                null)
            {
                return;
            }


            Color[] colors =
            {
                Color.FromRgb(255, 82, 82),
                Color.FromRgb(255, 213, 79),
                Color.FromRgb(105, 240, 174),
                Color.FromRgb(64, 196, 255),
                Color.FromRgb(224, 64, 251)
            };


            for (int index = 0;
                 index < 16;
                 index++)
            {
                Rectangle particle =
                    new Rectangle
                    {
                        Width =
                            5,

                        Height =
                            8,

                        RadiusX =
                            1,

                        RadiusY =
                            1,

                        Fill =
                            new SolidColorBrush(
                                colors[
                                    _holidayRandom.Next(
                                        colors.Length)]),

                        IsHitTestVisible =
                            false
                    };


                Canvas.SetLeft(
                    particle,
                    centerX);

                Canvas.SetTop(
                    particle,
                    centerY);


                _holidayEffectsCanvas.Children.Add(
                    particle);


                double angle =
                    _holidayRandom.NextDouble() *
                    Math.PI *
                    2;

                double distance =
                    35 +
                    _holidayRandom.NextDouble() *
                    55;


                DoubleAnimation leftAnimation =
                    new DoubleAnimation(
                        centerX,
                        centerX +
                        Math.Cos(angle) *
                        distance,
                        TimeSpan.FromMilliseconds(
                            520));


                DoubleAnimation topAnimation =
                    new DoubleAnimation(
                        centerY,
                        centerY +
                        Math.Sin(angle) *
                        distance +
                        25,
                        TimeSpan.FromMilliseconds(
                            520));


                DoubleAnimation opacityAnimation =
                    new DoubleAnimation(
                        1,
                        0,
                        TimeSpan.FromMilliseconds(
                            520));


                opacityAnimation.Completed +=
                    (_, _) =>
                    {
                        _holidayEffectsCanvas?
                            .Children
                            .Remove(
                                particle);
                    };


                particle.BeginAnimation(
                    Canvas.LeftProperty,
                    leftAnimation);

                particle.BeginAnimation(
                    Canvas.TopProperty,
                    topAnimation);

                particle.BeginAnimation(
                    OpacityProperty,
                    opacityAnimation);
            }
        }


        private void StartFireworks()
        {
            if (_holidayEffectsCanvas ==
                null)
            {
                return;
            }


            CreateFireworkBurst();


            _holidayFireworksTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(
                            1.35)
                };


            _holidayFireworksTimer.Tick +=
                (_, _) =>
                {
                    CreateFireworkBurst();
                };


            _holidayFireworksTimer.Start();
        }


        private void CreateFireworkBurst()
        {
            if (_holidayEffectsCanvas ==
                null)
            {
                return;
            }


            double width =
                _holidayEffectsCanvas.ActualWidth > 0
                    ? _holidayEffectsCanvas.ActualWidth
                    : ActualWidth;

            double height =
                _holidayEffectsCanvas.ActualHeight > 0
                    ? _holidayEffectsCanvas.ActualHeight
                    : ActualHeight;


            if (width <= 100 ||
                height <= 100)
            {
                return;
            }


            double centerX =
                width *
                (0.25 +
                 _holidayRandom.NextDouble() *
                 0.60);

            double centerY =
                height *
                (0.16 +
                 _holidayRandom.NextDouble() *
                 0.42);


            Color[] colors =
            {
                Color.FromRgb(255, 215, 95),
                Color.FromRgb(93, 206, 255),
                Color.FromRgb(255, 108, 145),
                Color.FromRgb(113, 225, 151),
                Color.FromRgb(214, 145, 255)
            };


            Color burstColor =
                colors[
                    _holidayRandom.Next(
                        colors.Length)];


            int particleCount =
                18;


            for (int index = 0;
                 index < particleCount;
                 index++)
            {
                Ellipse particle =
                    new Ellipse
                    {
                        Width =
                            5,

                        Height =
                            5,

                        Fill =
                            new SolidColorBrush(
                                burstColor),

                        IsHitTestVisible =
                            false,

                        Effect =
                            new System.Windows.Media.Effects.DropShadowEffect
                            {
                                Color =
                                    burstColor,

                                BlurRadius =
                                    8,

                                ShadowDepth =
                                    0,

                                Opacity =
                                    0.85
                            }
                    };


                Canvas.SetLeft(
                    particle,
                    centerX);

                Canvas.SetTop(
                    particle,
                    centerY);


                _holidayEffectsCanvas.Children.Add(
                    particle);


                double angle =
                    (Math.PI *
                     2 *
                     index /
                     particleCount) +
                    (_holidayRandom.NextDouble() *
                     0.15);

                double distance =
                    50 +
                    _holidayRandom.NextDouble() *
                    65;

                TimeSpan duration =
                    TimeSpan.FromMilliseconds(
                        720 +
                        _holidayRandom.Next(0, 250));


                DoubleAnimation leftAnimation =
                    new DoubleAnimation(
                        centerX,
                        centerX +
                        Math.Cos(angle) *
                        distance,
                        duration)
                    {
                        EasingFunction =
                            new QuadraticEase
                            {
                                EasingMode =
                                    EasingMode.EaseOut
                            }
                    };


                DoubleAnimation topAnimation =
                    new DoubleAnimation(
                        centerY,
                        centerY +
                        Math.Sin(angle) *
                        distance +
                        18,
                        duration)
                    {
                        EasingFunction =
                            new QuadraticEase
                            {
                                EasingMode =
                                    EasingMode.EaseOut
                            }
                    };


                DoubleAnimation opacityAnimation =
                    new DoubleAnimation(
                        1,
                        0,
                        duration);


                opacityAnimation.Completed +=
                    (_, _) =>
                    {
                        _holidayEffectsCanvas?
                            .Children
                            .Remove(
                                particle);
                    };


                particle.BeginAnimation(
                    Canvas.LeftProperty,
                    leftAnimation);

                particle.BeginAnimation(
                    Canvas.TopProperty,
                    topAnimation);

                particle.BeginAnimation(
                    OpacityProperty,
                    opacityAnimation);
            }
        }


        private void ScheduleNextHolidayMidnightRefresh()
        {
            _holidayMidnightTimer?.Stop();


            _holidayMidnightTimer =
                new DispatcherTimer
                {
                    Interval =
                        HolidayThemeService
                            .GetDelayUntilNextMonterreyMidnight()
                };


            _holidayMidnightTimer.Tick +=
                HolidayMidnightTimer_Tick;


            _holidayMidnightTimer.Start();
        }


        private void HolidayMidnightTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _holidayMidnightTimer?.Stop();


            if (_developerHolidayOverride ==
                null)
            {
                ApplyCurrentHolidayVisualState();
            }


            ScheduleNextHolidayMidnightRefresh();
        }
    }
}
