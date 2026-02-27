using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KGV.Core.Interfaces;
using System.Threading.Tasks;
using System;

namespace KGV.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        public event Action? LoginSucceeded;

        private readonly IAuthService _authService;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;

            LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
        }

        [ObservableProperty]
        private string email = "";

        [ObservableProperty]
        private string password = "";

        [ObservableProperty]
        private string statusMessage = "";

        public IAsyncRelayCommand LoginCommand { get; }

        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Email) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        private async Task LoginAsync()
        {
            StatusMessage = "";

            // Trim
            var emailTrim = Email.Trim();
            var pwdTrim = Password.Trim();

            if (string.IsNullOrEmpty(emailTrim) ||
                string.IsNullOrEmpty(pwdTrim))
            {
                StatusMessage = "E‑Mail oder Passwort leer.";
                return;
            }

            try
            {
                bool success = await _authService.LoginAsync(emailTrim, pwdTrim);

                if (success)
                {
                    // Email speichern
                    AppSettings.LastEmail = emailTrim;
                    AppSettings.Save();

                    StatusMessage = "Login erfolgreich!";
                    // Ereignis für erfolgreiche Anmeldung auslösen
                    LoginSucceeded?.Invoke();
                    return;
                }
                else
                {
                    StatusMessage = "Login fehlgeschlagen.";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Fehler: {ex.Message}";
            }
        }
    }
}