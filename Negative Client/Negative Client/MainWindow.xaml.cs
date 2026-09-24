using System.Windows;
using System.Windows.Input;

namespace Negative_Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private void AddModpackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AddModpackWindow window =
                new AddModpackWindow();

            window.Owner = this;


            bool? result =
                window.ShowDialog();


            if (result == true)
            {
                string code =
                    window.InstallCode;


                StatusText.Text =
                    $"Buscando instalación {code}...";


                /*
                 * Próximo paso:
                 *
                 * Buscar el código en el catálogo online.
                 *
                 * EJEMPLO:
                 *
                 * JADC32
                 *      ↓
                 * catálogo online
                 *      ↓
                 * OVERLAND
                 *      ↓
                 * manifest.json
                 *      ↓
                 * descargar archivos
                 */
            }
        }
    }
}