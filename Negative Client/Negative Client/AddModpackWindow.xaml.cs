using System.Windows;
using System.Windows.Input;

namespace Negative_Client
{
    public partial class AddModpackWindow : Window
    {
        public string InstallCode { get; private set; } = string.Empty;


        public AddModpackWindow()
        {
            InitializeComponent();

            Loaded += AddModpackWindow_Loaded;
        }


        private void AddModpackWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            CodeTextBox.Focus();
        }


        // Permite arrastrar la ventana
        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        // Botón X
        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
        }


        // Cancelar
        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
        }


        // Instalar
        private void Install_Click(
            object sender,
            RoutedEventArgs e)
        {
            TryAcceptCode();
        }


        // También permite pulsar ENTER
        private void CodeTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryAcceptCode();
            }
        }


        private void TryAcceptCode()
        {
            string code = CodeTextBox.Text
                .Trim()
                .ToUpperInvariant();


            if (string.IsNullOrWhiteSpace(code))
            {
                ErrorText.Text =
                    "Introduce un código de instalación.";

                CodeTextBox.Focus();

                return;
            }


            if (code.Length < 4)
            {
                ErrorText.Text =
                    "El código introducido es demasiado corto.";

                CodeTextBox.Focus();

                return;
            }


            InstallCode = code;

            DialogResult = true;
        }
    }
}