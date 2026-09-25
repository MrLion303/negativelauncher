using System;
using System.Windows.Input;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private ResourcePackSelectionService?
            _resourcePackSelectionService;

        private bool
            _resourcePackSelectionHookInitialized;


        private void InitializeResourcePackSelectionHook()
        {
            if (_resourcePackSelectionHookInitialized)
            {
                return;
            }


            _resourcePackSelectionHookInitialized =
                true;


            _resourcePackSelectionService =
                new ResourcePackSelectionService(
                    _instanceService);


            PlayButton.PreviewMouseLeftButtonDown +=
                PlayButton_ResourcePackPreviewMouseLeftButtonDown;


            PlayButton.PreviewKeyDown +=
                PlayButton_ResourcePackPreviewKeyDown;
        }


        private void PlayButton_ResourcePackPreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            PrepareBundledResourcePackSelectionForLaunch();
        }


        private void PlayButton_ResourcePackPreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter &&
                e.Key != Key.Space)
            {
                return;
            }


            PrepareBundledResourcePackSelectionForLaunch();
        }


        private void PrepareBundledResourcePackSelectionForLaunch()
        {
            if (_selectedInstance == null ||
                _resourcePackSelectionService == null)
            {
                return;
            }


            string action =
                PlayButton.Content?
                    .ToString() ??
                string.Empty;


            if (!string.Equals(
                    action,
                    "JUGAR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }


            try
            {
                ResourcePackSelectionResult result =
                    _resourcePackSelectionService
                        .ApplyBundledSelectionIfNeeded(
                            _selectedInstance);


                if (result.MissingResourcePacks.Count > 0)
                {
                    StatusText.Text =
                        "El preset de texture packs fue restaurado, " +
                        "pero faltan algunos archivos en resourcepacks.";
                }
            }
            catch
            {
                // Nunca se bloquea el arranque del juego por una reparación
                // automática de options.txt. Minecraft seguirá iniciando.
            }
        }
    }
}
