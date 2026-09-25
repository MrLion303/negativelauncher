using System;
using System.Windows;
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
            if (!PrepareBundledResourcePackSelectionForLaunch())
            {
                e.Handled =
                    true;
            }
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

            if (!PrepareBundledResourcePackSelectionForLaunch())
            {
                e.Handled =
                    true;
            }
        }


        private bool PrepareBundledResourcePackSelectionForLaunch()
        {
            if (_selectedInstance == null ||
                _resourcePackSelectionService == null ||
                _offlineSkinService == null)
            {
                return true;
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
                return true;
            }


            // =================================================
            // TEXTURE PACKS DEL MODPACK
            // =================================================

            try
            {
                ResourcePackSelectionResult result =
                    _resourcePackSelectionService
                        .ApplyBundledSelectionIfNeeded(
                            _selectedInstance);

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
                StatusText.Text =
                    "No se pudo preparar la selección de texture packs: " +
                    ex.Message;

                /*
                 * No bloqueamos el juego por una reparación del preset:
                 * el log de Minecraft nos permitirá diagnosticar por qué
                 * un resource pack fue rechazado o ignorado.
                 */
            }


            // =================================================
            // SKIN LOCAL DEL PERFIL NO PREMIUM
            // =================================================

            string instanceDirectory =
                _instanceService
                    .GetInstanceDirectory(
                        _selectedInstance.Id);

            try
            {
                if (_microsoftAccountService.IsOfflineModeActive &&
                    _microsoftAccountService.OfflineProfile != null)
                {
                    OfflineAccountProfile profile =
                        _microsoftAccountService
                            .OfflineProfile!;

                    if (!string.IsNullOrWhiteSpace(
                            profile.SkinFilePath))
                    {
                        StatusText.Text =
                            "Preparando skin local...";
                    }

                    _offlineSkinService
                        .ApplyLocalSkin(
                            instanceDirectory,
                            _selectedInstance.MinecraftVersion,
                            _selectedInstance.Loader,
                            profile);
                }
                else
                {
                    _offlineSkinService
                        .DisableLocalSkin(
                            instanceDirectory);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "No se pudo preparar la skin local.";

                MessageBox.Show(
                    "Negative Client no pudo preparar la skin del perfil " +
                    "no premium.\n\n" +
                    ex.Message +
                    "\n\nEl juego no se iniciará todavía para evitar abrirlo " +
                    "sin la skin seleccionada.",
                    "Skin no premium",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }


            return true;
        }
    }
}
