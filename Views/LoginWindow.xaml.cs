using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KGV.Views
{
    public partial class LoginWindow : Window
    {
        private bool _passwordVisible = false;

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.LoginViewModel vm)
            {
                if (vm.LoginCommand.CanExecute(null))
                    vm.LoginCommand.Execute(null);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LoginViewModel vm)
                vm.Password = PasswordBox.Password;
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is ViewModels.LoginViewModel vm)
                vm.Password = PasswordTextBox.Text;
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (!_passwordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordTextBox.Focus();
                _passwordVisible = true;
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordBox.Focus();
                _passwordVisible = false;
            }
        }
    }
}