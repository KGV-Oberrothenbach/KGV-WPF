using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;
using KGV.Wpf.Messages;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KGV.Wpf.ViewModels
{
    public sealed class ZaehlerwechselAusbauViewModel : BaseViewModel
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
                    OnPropertyChanged(nameof(HasAktiverZaehler));
                    OnPropertyChanged(nameof(AnlageText));
                    OnPropertyChanged(nameof(GartenNrText));
                    OnPropertyChanged(nameof(MediumText));
                    OnPropertyChanged(nameof(RfidText));
                    OnPropertyChanged(nameof(ZaehlernummerText));
                    OnPropertyChanged(nameof(EichfaelligText));
                    OnPropertyChanged(nameof(EingebautText));
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasAktiverZaehler => Context.AktiverZaehlerId.HasValue;

        public string AnlageText => (Context.Anlage ?? string.Empty).Trim();
        public string GartenNrText => Context.GartenNr.HasValue ? Context.GartenNr.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        public string MediumText => (Context.Medium ?? string.Empty).Trim();
        public string RfidText => (Context.RfidTagUid ?? string.Empty).Trim();
        public string ZaehlernummerText => (Context.Zaehlernummer ?? string.Empty).Trim();
        public bool HasZaehlernummer => !string.IsNullOrWhiteSpace(ZaehlernummerText);

        public string EichfaelligText => FormatDate(Context.EichfaelligAm);
        public string EingebautText => FormatDate(Context.EingebautAm);

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

        private DateTime _ausgebautAm = DateTime.Today;
        public DateTime AusgebautAm
        {
            get => _ausgebautAm;
            set => SetProperty(ref _ausgebautAm, value);
        }

        private string _endstandText = string.Empty;
        public string EndstandText
        {
            get => _endstandText;
            set
            {
                if (SetProperty(ref _endstandText, value))
                    _saveCommand.RaiseCanExecuteChanged();
            }
        }

        private string _fotoPfad = string.Empty;
        public string FotoPfad
        {
            get => _fotoPfad;
            private set => SetProperty(ref _fotoPfad, value);
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

        public ICommand ChooseFotoCommand { get; }
        public ICommand SaveCommand { get; }

        public ZaehlerwechselAusbauViewModel(ISupabaseService supabaseService, RfidScanContextRecord ctx)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _context = ctx ?? throw new ArgumentNullException(nameof(ctx));

            ChooseFotoCommand = new RelayCommand<object?>(_ => ChooseFoto());
            _saveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanSave());
            SaveCommand = _saveCommand;
        }

        private void ChooseFoto()
        {
            ErrorText = string.Empty;
            SuccessText = string.Empty;

            var dlg = new OpenFileDialog
            {
                Title = "Foto auswählen",
                Filter = "Bilder (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|Alle Dateien (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
                FotoPfad = dlg.FileName;
        }

        private bool CanSave()
        {
            if (IsBusy) return false;
            if (!HasAktiverZaehler) return false;
            if (!TryResolveZaehlerTyp(out _)) return false;
            return TryParseStand(out var stand) && stand >= 0;
        }

        private async Task SaveAsync()
        {
            if (!HasAktiverZaehler)
                return;

            if (!TryResolveZaehlerTyp(out var typ))
            {
                ErrorText = "Medium konnte nicht zugeordnet werden.";
                SuccessText = string.Empty;
                return;
            }

            if (!TryParseStand(out var stand) || stand < 0)
            {
                ErrorText = "Bitte einen gültigen, nicht-negativen Endstand eingeben.";
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
                var zaehlerId = Context.AktiverZaehlerId!.Value;
                var datum = AusgebautAm.Date;
                var foto = string.IsNullOrWhiteSpace(FotoPfad) ? null : FotoPfad;

                // 1) Endstand als Ablesung speichern
                var res = await _supabaseService.AddAblesungResultAsync(typ, zaehlerId, datum, stand, foto);
                if (!res.Ok)
                {
                    ErrorText = string.IsNullOrWhiteSpace(res.Message) ? "Endstand konnte nicht gespeichert werden." : res.Message;
                    return;
                }

                // 2) aktiven Zähler als ausgebaut markieren
                var okAusbau = typ switch
                {
                    1 => await _supabaseService.SetStromzaehlerAusgebautAmAsync(zaehlerId, datum),
                    2 => await _supabaseService.SetWasserzaehlerAusgebautAmAsync(zaehlerId, datum),
                    _ => false
                };

                if (!okAusbau)
                {
                    ErrorText = "Zähler konnte nicht als ausgebaut markiert werden.";
                    return;
                }

                SuccessText = "Zähler ausgebaut.";

                MessageBox.Show("Zähler ausgebaut.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private bool TryParseStand(out decimal stand)
        {
            stand = 0;
            var s = (EndstandText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(s)) return false;

            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out stand);
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

        private static string FormatDate(DateTime? dt)
            => dt.HasValue ? dt.Value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) : string.Empty;
    }
}
