using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KGV.Core.Interfaces;
using System.Threading.Tasks;
using System;

namespace KGV.Wpf.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        public event Action? LoginSucceeded;
        public event Func<Task<bool>>? PasswordResetRequired;
        public event Func<Task<bool>>? GoogleLoginRequired;

        private readonly IAuthService _authService;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;

            LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
            StartOtpFlowCommand = new AsyncRelayCommand(StartOtpFlowAsync, CanStartFlow);
            StartRecoveryFlowCommand = new AsyncRelayCommand(StartRecoveryFlowAsync, CanStartFlow);
            ResendCodeCommand = new AsyncRelayCommand(ResendCodeAsync, CanResendCode);
            VerifyCodeCommand = new AsyncRelayCommand(VerifyCodeAsync, CanVerifyCode);
            CancelAssistanceFlowCommand = new RelayCommand(CancelAssistanceFlow);

            GoogleLoginCommand = new AsyncRelayCommand(GoogleLoginAsync, CanGoogleLogin);
        }

        [ObservableProperty]
        private string email = "";

        [ObservableProperty]
        private string password = "";

        [ObservableProperty]
        private string statusMessage = "";

        [ObservableProperty]
        private string otpCode = "";

        public enum AssistanceFlow
        {
            None,
            Otp,
            Recovery
        }

        [ObservableProperty]
        private AssistanceFlow activeAssistanceFlow = AssistanceFlow.None;

        public bool IsPasswordLoginVisible => ActiveAssistanceFlow == AssistanceFlow.None;
        public bool IsAssistanceFlowVisible => ActiveAssistanceFlow != AssistanceFlow.None;

        public string AssistanceHeader => ActiveAssistanceFlow switch
        {
            AssistanceFlow.Recovery => "Passwort vergessen – Code eingeben",
            AssistanceFlow.Otp => "OTP – Code eingeben",
            _ => string.Empty
        };

        public IAsyncRelayCommand LoginCommand { get; }
        public IAsyncRelayCommand StartOtpFlowCommand { get; }
        public IAsyncRelayCommand StartRecoveryFlowCommand { get; }
        public IAsyncRelayCommand ResendCodeCommand { get; }
        public IAsyncRelayCommand VerifyCodeCommand { get; }
        public IRelayCommand CancelAssistanceFlowCommand { get; }
        public IAsyncRelayCommand GoogleLoginCommand { get; }

        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Email) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        private bool CanStartFlow()
        {
            return !string.IsNullOrWhiteSpace(Email);
        }

        private bool CanResendCode()
        {
            return ActiveAssistanceFlow != AssistanceFlow.None &&
                   !string.IsNullOrWhiteSpace(Email);
        }

        private bool CanVerifyCode()
        {
            return ActiveAssistanceFlow != AssistanceFlow.None &&
                   !string.IsNullOrWhiteSpace(Email) &&
                   !string.IsNullOrWhiteSpace(OtpCode);
        }

        private bool CanGoogleLogin()
        {
            return ActiveAssistanceFlow == AssistanceFlow.None;
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

        private async Task StartOtpFlowAsync()
        {
            StatusMessage = "";
            ActiveAssistanceFlow = AssistanceFlow.Otp;
            OtpCode = "";

            var emailTrim = Email.Trim();
            var ok = await _authService.RequestLoginOtpAsync(emailTrim);
            StatusMessage = ok
                ? "OTP wurde per E-Mail gesendet. Bitte Code eingeben."
                : "OTP konnte nicht gesendet werden.";
        }

        private async Task StartRecoveryFlowAsync()
        {
            StatusMessage = "";
            ActiveAssistanceFlow = AssistanceFlow.Recovery;
            OtpCode = "";

            var emailTrim = Email.Trim();
            var ok = await _authService.RequestRecoveryOtpAsync(emailTrim);
            StatusMessage = ok
                ? "Recovery-Code wurde per E-Mail gesendet. Bitte Code eingeben."
                : "Recovery-Code konnte nicht gesendet werden.";
        }

        private async Task ResendCodeAsync()
        {
            if (ActiveAssistanceFlow == AssistanceFlow.None)
                return;

            StatusMessage = "";
            var emailTrim = Email.Trim();

            var ok = ActiveAssistanceFlow == AssistanceFlow.Recovery
                ? await _authService.RequestRecoveryOtpAsync(emailTrim)
                : await _authService.RequestLoginOtpAsync(emailTrim);

            StatusMessage = ok ? "Code wurde erneut gesendet." : "Code konnte nicht erneut gesendet werden.";
        }

        private async Task VerifyCodeAsync()
        {
            StatusMessage = "";

            var emailTrim = Email.Trim();
            var otpTrim = OtpCode.Trim();

            if (string.IsNullOrWhiteSpace(emailTrim) || string.IsNullOrWhiteSpace(otpTrim))
            {
                StatusMessage = "E‑Mail oder Code leer.";
                return;
            }

            bool verified;
            try
            {
                verified = ActiveAssistanceFlow == AssistanceFlow.Recovery
                    ? await _authService.BeginPasswordResetFromRecoveryOtpAsync(emailTrim, otpTrim)
                    : await _authService.BeginPasswordResetFromLoginOtpAsync(emailTrim, otpTrim);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler: {ex.Message}";
                return;
            }

            if (!verified)
            {
                StatusMessage = "Code ist ungültig oder abgelaufen.";
                return;
            }

            var handler = PasswordResetRequired;
            if (handler == null)
            {
                await _authService.CancelPasswordResetSessionAsync();
                StatusMessage = "Reset-Dialog ist nicht verfügbar.";
                return;
            }

            bool resetOk;
            try
            {
                resetOk = await handler();
            }
            finally
            {
                // In jedem Fall sicherstellen, dass keine temporäre Session „liegen bleibt“.
                await _authService.CancelPasswordResetSessionAsync();
            }

            OtpCode = "";
            ActiveAssistanceFlow = AssistanceFlow.None;

            StatusMessage = resetOk
                ? "Passwort wurde gesetzt. Bitte mit neuem Passwort anmelden."
                : "Passwort-Reset abgebrochen.";
        }

        private void CancelAssistanceFlow()
        {
            OtpCode = "";
            ActiveAssistanceFlow = AssistanceFlow.None;
            StatusMessage = "";
        }

        private async Task GoogleLoginAsync()
        {
            StatusMessage = "";

            var handler = GoogleLoginRequired;
            if (handler == null)
            {
                StatusMessage = "Google-Login ist nicht verfügbar.";
                return;
            }

            bool ok;
            try
            {
                ok = await handler();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler: {ex.Message}";
                return;
            }

            if (!ok)
            {
                StatusMessage = "Google-Login fehlgeschlagen oder abgebrochen.";
                return;
            }

            StatusMessage = "Login erfolgreich!";
            LoginSucceeded?.Invoke();
        }

        partial void OnEmailChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
            StartOtpFlowCommand.NotifyCanExecuteChanged();
            StartRecoveryFlowCommand.NotifyCanExecuteChanged();
            ResendCodeCommand.NotifyCanExecuteChanged();
            VerifyCodeCommand.NotifyCanExecuteChanged();
        }

        partial void OnPasswordChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }

        partial void OnOtpCodeChanged(string value)
        {
            VerifyCodeCommand.NotifyCanExecuteChanged();
        }

        partial void OnActiveAssistanceFlowChanged(AssistanceFlow value)
        {
            OnPropertyChanged(nameof(IsPasswordLoginVisible));
            OnPropertyChanged(nameof(IsAssistanceFlowVisible));
            OnPropertyChanged(nameof(AssistanceHeader));
            ResendCodeCommand.NotifyCanExecuteChanged();
            VerifyCodeCommand.NotifyCanExecuteChanged();
            GoogleLoginCommand.NotifyCanExecuteChanged();
        }
    }
}