using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KGV.Wpf.ViewModels;

namespace KGV.Views
{
    public partial class ResetPasswordWindow : Window
    {
        public ResetPasswordWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => WireCloseRequest();
        }

        private void WireCloseRequest()
        {
            if (DataContext is ResetPasswordViewModel vm)
            {
                vm.CloseRequested += ok =>
                {
                    DialogResult = ok;
                };
            }
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ResetPasswordViewModel vm)
                vm.NewPassword = ((PasswordBox)sender).Password;
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ResetPasswordViewModel vm)
                vm.ConfirmPassword = ((PasswordBox)sender).Password;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ResetPasswordViewModel vm)
            {
                if (vm.SaveCommand.CanExecute(null))
                    vm.SaveCommand.Execute(null);
            }
        }
    }
}
