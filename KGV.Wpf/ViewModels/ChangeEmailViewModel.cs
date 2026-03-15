using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KGV.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace KGV.Wpf.ViewModels
{
    public partial class ChangeEmailViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        public event Action? ChangeSucceeded;
        public event Action? CancelRequested;

        public ChangeEmailViewModel(IAuthService authService, string? currentEmail)
        {
            _authService = authService;

            NewEmail = string.Empty;
            CurrentEmail = currentEmail ?? string.Empty;

            SendCodeCommand = new AsyncRelayCommand(SendCodeAsync, CanSendCode);
            VerifyCommand = new AsyncRelayCommand(VerifyAsync, CanVerify);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
        }

        [ObservableProperty]
        private string currentEmail = string.Empty;

        [ObservableProperty]
        private string newEmail = string.Empty;

        [ObservableProperty]
        private string otpCode = string.Empty;

        [ObservableProperty]
        private bool isCodeSent;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public IAsyncRelayCommand SendCodeCommand { get; }
        public IAsyncRelayCommand VerifyCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        private bool CanSendCode()
        {
            return !string.IsNullOrWhiteSpace(NewEmail);
        }

        private bool CanVerify()
        {
            return IsCodeSent &&
                   !string.IsNullOrWhiteSpace(NewEmail) &&
                   !string.IsNullOrWhiteSpace(OtpCode);
        }

        private async Task SendCodeAsync()
        {
            StatusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(NewEmail))
            {
                StatusMessage = "Bitte neue E-Mail-Adresse angeben.";
                return;
            }

            var ok = await _authService.RequestEmailChangeOtpAsync(NewEmail);
            if (!ok)
            {
                StatusMessage = "Code konnte nicht angefordert werden.";
                return;
            }

            IsCodeSent = true;
            StatusMessage = "Code wurde gesendet. Bitte prüfen Sie Ihre neue E-Mail-Adresse.";
        }

        private async Task VerifyAsync()
        {
            StatusMessage = string.Empty;

            if (!CanVerify())
            {
                StatusMessage = "Bitte Code und neue E-Mail-Adresse angeben.";
                return;
            }

            var ok = await _authService.VerifyEmailChangeOtpAsync(NewEmail, OtpCode);
            if (!ok)
            {
                StatusMessage = "Code ist ungültig oder abgelaufen.";
                return;
            }

            ChangeSucceeded?.Invoke();
        }

        private Task CancelAsync()
        {
            CancelRequested?.Invoke();
            return Task.CompletedTask;
        }

        partial void OnNewEmailChanged(string value)
        {
            SendCodeCommand.NotifyCanExecuteChanged();
            VerifyCommand.NotifyCanExecuteChanged();
        }

        partial void OnOtpCodeChanged(string value)
        {
            VerifyCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsCodeSentChanged(bool value)
        {
            VerifyCommand.NotifyCanExecuteChanged();
        }
    }
}
