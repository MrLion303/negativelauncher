using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private bool
            _developerVanillaBackgroundHookInitialized;

        private bool
            _developerVanillaBackgroundActive;


        private void InitializeDeveloperVanillaBackgroundHook()
        {
            if (_developerVanillaBackgroundHookInitialized)
            {
                return;
            }

            _developerVanillaBackgroundHookInitialized =
                true;

            DeveloperVersionPanel.IsVisibleChanged +=
                DeveloperVersionPanel_BackgroundVisibilityChanged;
        }


        private void DeveloperVersionPanel_BackgroundVisibilityChanged(
            object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if (_developerPageActive &&
                DeveloperVersionPanel.Visibility ==
                    Visibility.Visible)
            {
                ShowDeveloperVanillaBackground();

                return;
            }

            if (_developerVanillaBackgroundActive)
            {
                ClearDeveloperVanillaBackground();
            }
        }


        private void ShowDeveloperVanillaBackground()
        {
            try
            {
                string imagePath =
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "Assets",
                        "minecraft_vanilla.png");

                if (!File.Exists(
                        imagePath))
                {
                    _developerVanillaBackgroundActive =
                        false;

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

                InstanceBackgroundImage.Source =
                    bitmap;

                InstanceBackgroundImage.Stretch =
                    Stretch.UniformToFill;

                RenderOptions.SetBitmapScalingMode(
                    InstanceBackgroundImage,
                    BitmapScalingMode.HighQuality);

                InstanceBackgroundImage.Visibility =
                    Visibility.Visible;

                /*
                 * Conservamos el oscurecimiento que usan las instalaciones
                 * para que el selector de versión, JUGAR y los textos sigan
                 * siendo legibles encima de minecraft_vanilla.png.
                 */
                InstanceBackgroundOverlay.Visibility =
                    Visibility.Visible;

                _developerVanillaBackgroundActive =
                    true;
            }
            catch
            {
                _developerVanillaBackgroundActive =
                    false;
            }
        }


        private void ClearDeveloperVanillaBackground()
        {
            if (!_developerVanillaBackgroundActive)
            {
                return;
            }

            InstanceBackgroundImage.Source =
                null;

            InstanceBackgroundImage.Visibility =
                Visibility.Collapsed;

            InstanceBackgroundOverlay.Visibility =
                Visibility.Collapsed;

            _developerVanillaBackgroundActive =
                false;
        }
    }
}
