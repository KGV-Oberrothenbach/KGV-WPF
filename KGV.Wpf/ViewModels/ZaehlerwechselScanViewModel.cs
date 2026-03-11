using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Wpf.Helpers;
using KGV.Wpf.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KGV.Wpf.ViewModels
{
    public sealed class ZaehlerwechselScanViewModel : BaseViewModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly SemaphoreSlim _opLock = new(1, 1);

        private string _rfidTagUid = string.Empty;
        public string RfidTagUid
        {
            get => _rfidTagUid;
            set
            {
                if (SetProperty(ref _rfidTagUid, value))
                    _checkCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                    _checkCommand.RaiseCanExecuteChanged();
            }
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        private readonly RelayCommand<object?> _checkCommand;
        public ICommand CheckCommand => _checkCommand;

        public ZaehlerwechselScanViewModel(ISupabaseService supabaseService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

            _checkCommand = new RelayCommand<object?>(
                _ => _ = CheckAsync(),
                _ => !IsBusy && !string.IsNullOrWhiteSpace((RfidTagUid ?? string.Empty).Trim()));
        }

        private async Task CheckAsync()
        {
            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var uid = (RfidTagUid ?? string.Empty).Trim();
                var ctx = await _supabaseService.GetRfidScanContextAsync(uid);

                if (ctx == null)
                {
                    StatusText = "UID ist keiner Parzelle zugeordnet.";
                    return;
                }

                if (ctx.AktiverZaehlerId.HasValue)
                {
                    WeakReferenceMessenger.Default.Send(
                        new NavigateToViewModelMessage(typeof(ZaehlerwechselAusbauViewModel), ctx));
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(
                        new NavigateToViewModelMessage(typeof(ZaehlerwechselEinbauViewModel), ctx));
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Fehler: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }
    }
}
