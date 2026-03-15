using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KGV.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace KGV.Wpf.ViewModels
{
    public partial class ResetPasswordViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        public event Action<bool>? CloseRequested;

        public ResetPasswordViewModel(IAuthService authService)
        {
            _authService = authService;

            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
        }

        [ObservableProperty]
        private string newPassword = "";

        [ObservableProperty]
        private string confirmPassword = "";

        [ObservableProperty]
        private string statusMessage = "";

        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(NewPassword) &&
                   !string.IsNullOrWhiteSpace(ConfirmPassword) &&
                   string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal);
        }

        private async Task SaveAsync()
        {
            StatusMessage = "";

            if (!CanSave())
            {
                StatusMessage = "Passwörter sind leer oder stimmen nicht überein.";
                return;
            }

            try
            {
                var ok = await _authService.CompletePasswordResetAsync(NewPassword);
                if (!ok)
                {
                    StatusMessage = "Passwort konnte nicht gesetzt werden.";
                    return;
                }

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler: {ex.Message}";
            }
        }

        private async Task CancelAsync()
        {
            try
            {
                await _authService.CancelPasswordResetSessionAsync();
            }
            catch
            {
            }

            CloseRequested?.Invoke(false);
        }

        partial void OnNewPasswordChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnConfirmPasswordChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
