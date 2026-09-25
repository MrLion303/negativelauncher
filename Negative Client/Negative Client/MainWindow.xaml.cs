using System;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Negative_Client.Models;
using Negative_Client.Services;

namespace Negative_Client
{
    public partial class MainWindow : Window
    {
        private readonly ModpackCatalogService
            _modpackCatalogService;


        private ModpackManifest? _selectedModpack;


        public MainWindow()
        {
            InitializeComponent();

            _modpackCatalogService =
                new ModpackCatalogService();
        }


        // =====================================================
        // VENTANA
        // =====================================================

        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }


            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }


        private void Maximize_Click(
            object sender,
            RoutedEventArgs e)
        {
            ToggleMaximize();
        }


        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }


        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }


        // =====================================================
        // MODPACKS
        // =====================================================

        private async void AddModpackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddModpackWindow window =
                new AddModpackWindow
                {
                    Owner = this
                };


            bool? result =
                window.ShowDialog();


            if (result != true)
            {
                return;
            }


            string code =
                window.InstallCode;


            try
            {
                AddModpackButton.IsEnabled = false;

                StatusText.Text =
                    $"Buscando instalación {code}...";


                _selectedModpack =
                    await _modpackCatalogService
                        .FindByCodeAsync(code);


                // ==========================================
                // CÓDIGO NO ENCONTRADO
                // ==========================================

                if (_selectedModpack == null)
                {
                    SelectedModpackText.Text =
                        "Ningún modpack seleccionado";


                    StatusText.Text =
                        "Código de instalación no encontrado.";


                    MessageBox.Show(
                        $"No existe ninguna instalación " +
                        $"asociada al código {code}.",
                        "Código no encontrado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);


                    return;
                }


                // ==========================================
                // MODPACK ENCONTRADO
                // ==========================================

                string loaderText =
                    _selectedModpack.Loader;


                if (!string.IsNullOrWhiteSpace(
                        _selectedModpack.LoaderVersion))
                {
                    loaderText +=
                        $" {_selectedModpack.LoaderVersion}";
                }


                SelectedModpackText.Text =
                    $"{_selectedModpack.Name}  •  " +
                    $"Minecraft " +
                    $"{_selectedModpack.MinecraftVersion}  •  " +
                    $"{loaderText}";


                StatusText.Text =
                    $"Encontrado: " +
                    $"{_selectedModpack.Name} " +
                    $"v{_selectedModpack.Version}";


                /*
                 * Todavía NO habilitamos PLAY.
                 *
                 * El próximo paso será instalar
                 * realmente el modpack.
                 */
                PlayButton.IsEnabled = false;
            }
            catch (HttpRequestException ex)
            {
                StatusText.Text =
                    "No se pudo conectar con el servidor.";


                MessageBox.Show(
                    "No se pudo descargar el catálogo.\n\n" +
                    ex.Message,
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (JsonException ex)
            {
                StatusText.Text =
                    "El catálogo contiene datos inválidos.";


                MessageBox.Show(
                    "No se pudo interpretar el archivo JSON.\n\n" +
                    ex.Message,
                    "JSON inválido",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Ocurrió un error al buscar el modpack.";


                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                AddModpackButton.IsEnabled = true;
            }
        }
    }
}