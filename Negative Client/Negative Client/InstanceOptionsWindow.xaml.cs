using System.Windows;
using System.Windows.Input;

namespace Negative_Client
{
    public enum InstanceOptionsAction
    {
        None,
        CheckUpdates,
        VerifyIntegrity,
        Delete
    }


    public partial class InstanceOptionsWindow : Window
    {
        public InstanceOptionsAction RequestedAction { get; private set; } =
            InstanceOptionsAction.None;


        public InstanceOptionsWindow(
            string instanceName)
        {
            InitializeComponent();


            WindowTitleText.Text =
                string.IsNullOrWhiteSpace(
                    instanceName)
                    ? "Opciones de instalación"
                    : $"Opciones de {instanceName}";
        }


        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.LeftButton ==
                MouseButtonState.Pressed)
            {
                DragMove();
            }
        }


        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult =
                false;
        }


        private void CheckUpdates_Click(
            object sender,
            RoutedEventArgs e)
        {
            RequestedAction =
                InstanceOptionsAction.CheckUpdates;


            DialogResult =
                true;
        }


        private void VerifyIntegrity_Click(
            object sender,
            RoutedEventArgs e)
        {
            RequestedAction =
                InstanceOptionsAction.VerifyIntegrity;


            DialogResult =
                true;
        }


        private void Delete_Click(
            object sender,
            RoutedEventArgs e)
        {
            RequestedAction =
                InstanceOptionsAction.Delete;


            DialogResult =
                true;
        }
    }
}
