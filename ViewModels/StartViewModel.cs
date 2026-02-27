using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace KGV.ViewModels
{
    public partial class StartViewModel : ObservableObject
    {
        // Bindable Property für Email
        [ObservableProperty]
        private string email = "";

        // Bindable Property für Password
        [ObservableProperty]
        private string password = "";

        // Login Command
        [RelayCommand]
        private void Login()
        {
            // Prüfung der Eingaben
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Bitte E-Mail und Passwort eingeben.");
                return;
            }

            // TODO: Login via SupabaseService implementieren
            bool loginErfolg = true; // Platzhalter

            if (loginErfolg)
            {
                MessageBox.Show($"Erfolgreich eingeloggt als {Email}");
            }
            else
            {
                MessageBox.Show("Login fehlgeschlagen!");
            }
        }
    }
}
