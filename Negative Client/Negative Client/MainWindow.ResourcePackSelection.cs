using System;
using System.Windows.Input;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow
    {
        private ResourcePackSelectionService?
            _resourcePackSelectionService;

        private OfflineSkinService?
            _offlineSkinService;

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

            _offlineSkinService =
                new OfflineSkinService();

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
                _resourcePackSelectionService == null ||
                _offlineSkinService == null)
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

                string instanceDirectory =
                    _instanceService
                        .GetInstanceDirectory(
                            _selectedInstance.Id);

                if (_microsoftAccountService.IsOfflineModeActive &&
                    _microsoftAccountService.OfflineProfile != null)
                {
                    OfflineAccountProfile profile =
                        _microsoftAccountService.OfflineProfile!;

                    _offlineSkinService
                        .ApplyLocalSkin(
                            instanceDirectory,
                            _selectedInstance.MinecraftVersion,
                            profile);
                }
                else
                {
                    // Si se volvió a una cuenta Premium retiramos únicamente
                    // el pack de skin local de Negative Client.
                    _offlineSkinService
                        .DisableLocalSkin(
                            instanceDirectory);
                }

                if (result.MissingResourcePacks.Count > 0)
                {
                    StatusText.Text =
                        "Se restauró la selección de texture packs, pero faltan: " +
                        string.Join(
                            ", ",
                            result.MissingResourcePacks);
                }
            }
            catch (Exception ex)
            {
                // La reparación visual no bloquea el arranque del juego,
                // pero dejamos el motivo visible para poder depurarlo.
                StatusText.Text =
                    "No se pudo preparar la selección de texture packs: " +
                    ex.Message;
            }
        }
    }
}
