using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private bool _uiPolishInitialized;
        private bool _changingProgressCaption;

        private static readonly Brush WindowCircleNormalBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    53,
                    55,
                    58));

        private static readonly Brush WindowCircleHoverBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    69,
                    72,
                    75));

        private static readonly Brush WindowCirclePressedBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    43,
                    45,
                    48));

        private static readonly Brush WindowGlyphBrush =
            new SolidColorBrush(
                Color.FromRgb(
                    214,
                    217,
                    219));


        // =====================================================
        // AJUSTES ANTES DEL PRIMER FRAME
        // =====================================================

        private void ApplyUiPolishBeforeFirstRender()
        {
            try
            {
                // El logo superior ya no forma parte de la interfaz.
                TopLeftLogoImage.Visibility =
                    Visibility.Collapsed;

                TopLeftLogoFallbackText.Visibility =
                    Visibility.Collapsed;

                TryApplyHomeIcon();
            }
            catch
            {
                // Un ajuste visual nunca debe impedir abrir el launcher.
            }
        }


        // =====================================================
        // INICIALIZACIÓN
        // =====================================================

        private void InitializeUiPolish()
        {
            if (_uiPolishInitialized)
            {
                return;
            }

            _uiPolishInitialized =
                true;


            ApplyUiPolishBeforeFirstRender();

            CenterTitleAfterRemovingLogo();

            ApplyCompactWindowButtons();

            TuneAllSidebarButtons();

            NudgeInstanceImagesToTheRight();


            DownloadProgressText.TextChanged +=
                DownloadProgressText_TextChanged;


            // Los iconos de las instancias se cargan de forma asíncrona y
            // también pueden aparecer después de instalar otra instancia.
            // LayoutUpdated nos permite corregir su posición cuando existan.
            ModpackList.LayoutUpdated +=
                ModpackList_UiPolishLayoutUpdated;
        }


        private void ModpackList_UiPolishLayoutUpdated(
            object? sender,
            EventArgs e)
        {
            TuneAllSidebarButtons();

            NudgeInstanceImagesToTheRight();
        }


        // =====================================================
        // BOTÓN INICIO -> Assets/home_icon.png
        // =====================================================

        private void TryApplyHomeIcon()
        {
            try
            {
                string imagePath =
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "Assets",
                        "home_icon.png");


                if (!File.Exists(
                        imagePath))
                {
                    // Si el usuario todavía no añadió el PNG, dejamos el
                    // icono actual para que el launcher siga funcionando.
                    return;
                }


                BitmapImage bitmap =
                    new BitmapImage();


                bitmap.BeginInit();

                bitmap.CacheOption =
                    BitmapCacheOption.OnLoad;

                bitmap.UriSource =
                    new Uri(
                        imagePath,
                        UriKind.Absolute);

                bitmap.EndInit();
                bitmap.Freeze();


                Image image =
                    new Image
                    {
                        Source =
                            bitmap,

                        Width =
                            24,

                        Height =
                            24,

                        Stretch =
                            Stretch.Uniform,

                        IsHitTestVisible =
                            false,

                        RenderTransform =
                            new TranslateTransform(
                                1.0,
                                0)
                    };


                RenderOptions.SetBitmapScalingMode(
                    image,
                    BitmapScalingMode.HighQuality);


                HomeButton.Content =
                    image;
            }
            catch
            {
                // Fallback: se conserva el icono anterior.
            }
        }


        // =====================================================
        // LOGO SUPERIOR FUERA + TÍTULO CENTRADO
        // =====================================================

        private void CenterTitleAfterRemovingLogo()
        {
            try
            {
                // Estructura actual:
                // TitleBar Grid
                //   ├─ columna 0: host del antiguo logo
                //   ├─ columna 1: texto de versión
                //   └─ columna 2: botones de ventana (126 px)
                //
                // Igualamos las columnas laterales para que el título quede
                // geométricamente centrado en la ventana.
                if (TopLeftLogoImage.Parent is Grid logoHost &&
                    logoHost.Parent is Grid titleBar &&
                    titleBar.ColumnDefinitions.Count >=
                    3)
                {
                    titleBar.ColumnDefinitions[0].Width =
                        new GridLength(
                            126);
                }
            }
            catch
            {
            }
        }


        // =====================================================
        // BOTONES MINIMIZAR / MAXIMIZAR / CERRAR
        // =====================================================

        private void ApplyCompactWindowButtons()
        {
            try
            {
                Style? windowButtonStyle =
                    TryFindResource(
                        "WindowButton") as Style;


                if (windowButtonStyle ==
                    null)
                {
                    return;
                }


                List<Button> buttons =
                    FindVisualChildren<Button>(
                            this)
                        .Where(
                            button =>
                                ReferenceEquals(
                                    button.Style,
                                    windowButtonStyle))
                        .Take(
                            3)
                        .ToList();


                if (buttons.Count <
                    3)
                {
                    return;
                }


                ConfigureWindowButton(
                    buttons[0],
                    WindowControlGlyph.Minimize);

                ConfigureWindowButton(
                    buttons[1],
                    WindowControlGlyph.Maximize);

                ConfigureWindowButton(
                    buttons[2],
                    WindowControlGlyph.Close);


                if (buttons[0].Parent is StackPanel panel)
                {
                    panel.VerticalAlignment =
                        VerticalAlignment.Center;

                    panel.Margin =
                        new Thickness(
                            0,
                            0,
                            8,
                            0);
                }
            }
            catch
            {
            }
        }


        private enum WindowControlGlyph
        {
            Minimize,
            Maximize,
            Close
        }


        private void ConfigureWindowButton(
            Button button,
            WindowControlGlyph glyph)
        {
            button.Width =
                24;

            button.Height =
                24;

            button.Margin =
                new Thickness(
                    2,
                    0,
                    2,
                    0);

            button.Padding =
                new Thickness(
                    0);

            button.Background =
                Brushes.Transparent;

            button.BorderThickness =
                new Thickness(
                    0);


            Border circle =
                new Border
                {
                    Width =
                        18,

                    Height =
                        18,

                    Background =
                        WindowCircleNormalBrush,

                    CornerRadius =
                        new CornerRadius(
                            9),

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    IsHitTestVisible =
                        false,

                    Child =
                        CreateWindowGlyph(
                            glyph)
                };


            button.Content =
                circle;


            button.MouseEnter +=
                (_, _) =>
                {
                    circle.Background =
                        WindowCircleHoverBrush;
                };


            button.MouseLeave +=
                (_, _) =>
                {
                    circle.Background =
                        WindowCircleNormalBrush;
                };


            button.PreviewMouseLeftButtonDown +=
                (_, _) =>
                {
                    circle.Background =
                        WindowCirclePressedBrush;
                };


            button.PreviewMouseLeftButtonUp +=
                (_, _) =>
                {
                    circle.Background =
                        button.IsMouseOver
                            ? WindowCircleHoverBrush
                            : WindowCircleNormalBrush;
                };
        }


        private static FrameworkElement CreateWindowGlyph(
            WindowControlGlyph glyph)
        {
            string geometryData =
                glyph switch
                {
                    WindowControlGlyph.Minimize =>
                        "M 5,10 L 13,10",

                    WindowControlGlyph.Maximize =>
                        "M 5.5,5.5 L 12.5,5.5 L 12.5,12.5 L 5.5,12.5 Z",

                    _ =>
                        "M 5.5,5.5 L 12.5,12.5 M 12.5,5.5 L 5.5,12.5"
                };


            System.Windows.Shapes.Path path =
                new System.Windows.Shapes.Path
                {
                    Data =
                        Geometry.Parse(
                            geometryData),

                    Stroke =
                        WindowGlyphBrush,

                    StrokeThickness =
                        glyph ==
                        WindowControlGlyph.Maximize
                            ? 1.15
                            : 1.35,

                    StrokeStartLineCap =
                        PenLineCap.Round,

                    StrokeEndLineCap =
                        PenLineCap.Round,

                    StrokeLineJoin =
                        PenLineJoin.Round,

                    Width =
                        18,

                    Height =
                        18,

                    Stretch =
                        Stretch.None,

                    HorizontalAlignment =
                        HorizontalAlignment.Center,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    IsHitTestVisible =
                        false
                };


            RenderOptions.SetEdgeMode(
                path,
                EdgeMode.Unspecified);


            return path;
        }


        // =====================================================
        // BOTONES LATERALES - RENDERIZADO / BRILLO
        // =====================================================

        private void TuneAllSidebarButtons()
        {
            try
            {
                Style? circleStyle =
                    TryFindResource(
                        "CircleButton") as Style;


                if (circleStyle ==
                    null)
                {
                    return;
                }


                foreach (Button button in
                    FindVisualChildren<Button>(
                        this))
                {
                    if (!ReferenceEquals(
                            button.Style,
                            circleStyle))
                    {
                        continue;
                    }


                    TuneSidebarButton(
                        button);
                }
            }
            catch
            {
            }
        }


        private static void TuneSidebarButton(
            Button button)
        {
            button.UseLayoutRounding =
                true;

            // En bordes circulares, forzar cada píxel al grid entero puede
            // producir escalones. Dejamos que WPF antialiasée la curva.
            button.SnapsToDevicePixels =
                false;


            if (button.CacheMode is not BitmapCache)
            {
                button.CacheMode =
                    new BitmapCache
                    {
                        RenderAtScale =
                            2.0,

                        EnableClearType =
                            true
                    };
            }


            RenderOptions.SetEdgeMode(
                button,
                EdgeMode.Unspecified);


            // Si el template actual o uno futuro usa DropShadowEffect para
            // el brillo, lo forzamos al modo de mayor calidad.
            foreach (FrameworkElement element in
                FindVisualChildren<FrameworkElement>(
                    button))
            {
                if (element.Effect is
                    DropShadowEffect shadow)
                {
                    shadow.RenderingBias =
                        RenderingBias.Quality;
                }
            }
        }


        // =====================================================
        // ICONOS DE INSTANCIAS - 1 px A LA DERECHA
        // =====================================================

        private void NudgeInstanceImagesToTheRight()
        {
            foreach (Button button in
                _instanceButtons.Values)
            {
                if (button.Content is not
                    Image image)
                {
                    continue;
                }


                RenderOptions.SetBitmapScalingMode(
                    image,
                    BitmapScalingMode.HighQuality);


                image.SnapsToDevicePixels =
                    false;


                if (image.RenderTransform is
                        TranslateTransform transform &&
                    Math.Abs(
                        transform.X -
                        1.0) <
                    0.01 &&
                    Math.Abs(
                        transform.Y) <
                    0.01)
                {
                    continue;
                }


                image.RenderTransform =
                    new TranslateTransform(
                        1.0,
                        0);
            }
        }


        // =====================================================
        // TEXTO DE PROGRESO: NO MOSTRAR NOMBRE DE CADA ARCHIVO ARRIBA
        // =====================================================

        private void DownloadProgressText_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_changingProgressCaption)
            {
                return;
            }


            string currentText =
                DownloadProgressText.Text ??
                string.Empty;


            if (!TryGetGenericRuntimeCaption(
                    currentText,
                    out string genericCaption))
            {
                return;
            }


            string percentage =
                ExtractPercentageText(
                    currentText);


            string replacement =
                string.IsNullOrWhiteSpace(
                    percentage)
                    ? genericCaption
                    : genericCaption +
                      " " +
                      percentage;


            if (string.Equals(
                    currentText,
                    replacement,
                    StringComparison.Ordinal))
            {
                return;
            }


            try
            {
                _changingProgressCaption =
                    true;


                // Solo se limpia el texto SOBRE la barra. StatusText no se
                // modifica, así que debajo del botón todavía se ve el asset,
                // librería o archivo exacto que CmlLib está procesando.
                DownloadProgressText.Text =
                    replacement;
            }
            finally
            {
                _changingProgressCaption =
                    false;
            }
        }


        private static bool TryGetGenericRuntimeCaption(
            string text,
            out string caption)
        {
            string trimmed =
                text.TrimStart();


            if (trimmed.StartsWith(
                    "Minecraft:",
                    StringComparison.OrdinalIgnoreCase))
            {
                caption =
                    "Descargando Minecraft...";

                return true;
            }


            if (trimmed.StartsWith(
                    "Forge:",
                    StringComparison.OrdinalIgnoreCase))
            {
                caption =
                    "Preparando Forge...";

                return true;
            }


            if (trimmed.StartsWith(
                    "Java:",
                    StringComparison.OrdinalIgnoreCase))
            {
                caption =
                    "Descargando Java...";

                return true;
            }


            caption =
                string.Empty;

            return false;
        }


        private static string ExtractPercentageText(
            string text)
        {
            Match match =
                Regex.Match(
                    text,
                    @"(?<!\d)(\d{1,3}(?:[\.,]\d+)?)\s*%",
                    RegexOptions.CultureInvariant);


            if (!match.Success)
            {
                return string.Empty;
            }


            string value =
                match.Groups[1]
                    .Value;


            if (double.TryParse(
                    value.Replace(
                        ',',
                        '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double numeric))
            {
                numeric =
                    Math.Clamp(
                        numeric,
                        0,
                        100);


                return
                    $"{numeric:0.#}%";
            }


            return
                match.Value.Trim();
        }


        // =====================================================
        // VISUAL TREE
        // =====================================================

        private static IEnumerable<T>
            FindVisualChildren<T>(
                DependencyObject root)
            where T : DependencyObject
        {
            if (root ==
                null)
            {
                yield break;
            }


            int childrenCount =
                VisualTreeHelper
                    .GetChildrenCount(
                        root);


            for (int index =
                     0;
                 index <
                 childrenCount;
                 index++)
            {
                DependencyObject child =
                    VisualTreeHelper
                        .GetChild(
                            root,
                            index);


                if (child is T typed)
                {
                    yield return typed;
                }


                foreach (T descendant in
                    FindVisualChildren<T>(
                        child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
