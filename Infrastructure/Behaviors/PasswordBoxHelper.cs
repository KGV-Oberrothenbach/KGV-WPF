using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KGV.Infrastructure.Behaviors
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
                new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        public static readonly DependencyProperty EnterCommandProperty =
            DependencyProperty.RegisterAttached("EnterCommand", typeof(ICommand), typeof(PasswordBoxHelper),
                new PropertyMetadata(null));

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(PasswordBoxHelper));

        public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);
        public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);

        public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPasswordProperty);
        public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPasswordProperty, value);

        public static ICommand? GetEnterCommand(DependencyObject dp) => (ICommand?)dp.GetValue(EnterCommandProperty);
        public static void SetEnterCommand(DependencyObject dp, ICommand? value) => dp.SetValue(EnterCommandProperty, value);

        private static bool GetIsUpdating(DependencyObject dp) => (bool)dp.GetValue(IsUpdatingProperty);
        private static void SetIsUpdating(DependencyObject dp, bool value) => dp.SetValue(IsUpdatingProperty, value);

        private static void OnBoundPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            if (dp is PasswordBox passwordBox && !GetIsUpdating(passwordBox))
            {
                passwordBox.Password = e.NewValue as string ?? string.Empty;
            }
        }

        private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            if (dp is PasswordBox passwordBox)
            {
                bool oldVal = (bool)e.OldValue;
                bool newVal = (bool)e.NewValue;

                if (oldVal)
                {
                    passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                    passwordBox.PreviewKeyDown -= PasswordBox_PreviewKeyDown;
                }

                if (newVal)
                {
                    passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
                    passwordBox.PreviewKeyDown += PasswordBox_PreviewKeyDown;
                }
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetIsUpdating(passwordBox, true);
                SetBoundPassword(passwordBox, passwordBox.Password);
                SetIsUpdating(passwordBox, false);
            }
        }

        private static void PasswordBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is PasswordBox passwordBox)
            {
                var command = GetEnterCommand(passwordBox);
                if (command != null && command.CanExecute(null))
                {
                    command.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
