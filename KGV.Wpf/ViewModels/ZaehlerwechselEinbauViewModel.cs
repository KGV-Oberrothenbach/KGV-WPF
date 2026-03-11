using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;
using KGV.Wpf.Messages;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KGV.Wpf.ViewModels
{
    public sealed class ZaehlerwechselEinbauViewModel : BaseViewModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly SemaphoreSlim _opLock = new(1, 1);
        private readonly RelayCommand<object?> _saveCommand;

        private RfidScanContextRecord _context;
        public RfidScanContextRecord Context
        {
            get => _context;
            private set
            {
                if (SetProperty(ref _context, value))
                {
                    OnPropertyChanged(nameof(AnlageText));
                    OnPropertyChanged(nameof(GartenNrText));
                    OnPropertyChanged(nameof(MediumText));
                    OnPropertyChanged(nameof(RfidText));
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string AnlageText => (Context.Anlage ?? string.Empty).Trim();
        public string GartenNrText => Context.GartenNr.HasValue ? Context.GartenNr.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        public string MediumText => (Context.Medium ?? string.Empty).Trim();
        public string RfidText => (Context.RfidTagUid ?? string.Empty).Trim();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                    _saveCommand.RaiseCanExecuteChanged();
            }
        }

        private string _zaehlernummer = string.Empty;
        public string Zaehlernummer
        {
            get => _zaehlernummer;
            set
            {
                if (SetProperty(ref _zaehlernummer, value))
                    _saveCommand.RaiseCanExecuteChanged();
            }
        }

        private DateTime _eichdatum = DateTime.Today;
        public DateTime Eichdatum
        {
            get => _eichdatum;
            set => SetProperty(ref _eichdatum, value);
        }

        private DateTime _eingebautAm = DateTime.Today;
        public DateTime EingebautAm
        {
            get => _eingebautAm;
            set => SetProperty(ref _eingebautAm, value);
        }

        private string _errorText = string.Empty;
        public string ErrorText
        {
            get => _errorText;
            private set => SetProperty(ref _errorText, value);
        }

        private string _successText = string.Empty;
        public string SuccessText
        {
            get => _successText;
            private set => SetProperty(ref _successText, value);
        }

        public ICommand SaveCommand { get; }

        public ZaehlerwechselEinbauViewModel(ISupabaseService supabaseService, RfidScanContextRecord ctx)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _context = ctx ?? throw new ArgumentNullException(nameof(ctx));

            _saveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanSave());
            SaveCommand = _saveCommand;
        }

        private bool CanSave()
        {
            if (IsBusy) return false;
            if (Context.AktiverZaehlerId.HasValue) return false;
            if (!Context.ParzelleId.HasValue || Context.ParzelleId.Value <= 0 || Context.ParzelleId.Value > int.MaxValue) return false;
            if (!TryResolveZaehlerTyp(out _)) return false;
            return !string.IsNullOrWhiteSpace((Zaehlernummer ?? string.Empty).Trim());
        }

        private async Task SaveAsync()
        {
            if (Context.AktiverZaehlerId.HasValue)
            {
                ErrorText = "Es ist bereits ein aktiver Zähler vorhanden.";
                SuccessText = string.Empty;
                return;
            }

            var nummer = (Zaehlernummer ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nummer))
            {
                ErrorText = "Bitte eine Zählernummer eingeben.";
                SuccessText = string.Empty;
                return;
            }

            if (!TryResolveZaehlerTyp(out var typ))
            {
                ErrorText = "Medium konnte nicht zugeordnet werden.";
                SuccessText = string.Empty;
                return;
            }

            if (!Context.ParzelleId.HasValue || Context.ParzelleId.Value <= 0 || Context.ParzelleId.Value > int.MaxValue)
            {
                ErrorText = "Ungültige Parzellen-ID.";
                SuccessText = string.Empty;
                return;
            }

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            ErrorText = string.Empty;
            SuccessText = string.Empty;

            try
            {
                var parzelleId = (int)Context.ParzelleId.Value;
                var ok = typ switch
                {
                    1 => await _supabaseService.AddStromzaehlerAsync(parzelleId, nummer, Eichdatum.Date, EingebautAm.Date),
                    2 => await _supabaseService.AddWasserzaehlerAsync(parzelleId, nummer, Eichdatum.Date, EingebautAm.Date),
                    _ => false
                };

                if (!ok)
                {
                    ErrorText = "Einbau konnte nicht gespeichert werden.";
                    return;
                }

                SuccessText = "Zähler eingebaut.";
                MessageBox.Show("Zähler eingebaut.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                WeakReferenceMessenger.Default.Send(new NavigateToViewModelMessage(typeof(ZaehlerwechselScanViewModel)));
            }
            catch (Exception ex)
            {
                ErrorText = $"Fehler: {ex.Message}";
                SuccessText = string.Empty;
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }

        private bool TryResolveZaehlerTyp(out short typ)
        {
            typ = 0;
            var medium = (Context.Medium ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(medium)) return false;

            if (medium.Contains("strom", StringComparison.OrdinalIgnoreCase)) { typ = 1; return true; }
            if (medium.Contains("wasser", StringComparison.OrdinalIgnoreCase)) { typ = 2; return true; }

            return false;
        }
    }
}
