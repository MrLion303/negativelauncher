using System;
using System.Windows;
using System.Windows.Input;

namespace Negative_Client
{
    public partial class DeveloperLoginWindow : Window
    {
        private const string RequiredUsername =
            "admin";


        private const string RequiredPassword =
            "050408Mm@";


        public bool Authenticated { get; private set; }


        public DeveloperLoginWindow()
        {
            InitializeComponent();


            Loaded +=
                (_, _) =>
                {
                    UsernameTextBox.Focus();
                };
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


        private void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            bool correct =
                string.Equals(
                    UsernameTextBox.Text,
                    RequiredUsername,
                    StringComparison.Ordinal) &&
                string.Equals(
                    PasswordInput.Password,
                    RequiredPassword,
                    StringComparison.Ordinal);


            if (!correct)
            {
                Authenticated =
                    false;


                DialogResult =
                    false;

                return;
            }


            Authenticated =
                true;


            DialogResult =
                true;
        }
    }
}
