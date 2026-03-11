using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KGV.Wpf.ViewModels
{
    public sealed class RfidScanContextViewModel : BaseViewModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly SemaphoreSlim _opLock = new(1, 1);

        private readonly RelayCommand<object?> _saveCommand;

        public RfidScanContextViewModel(ISupabaseService supabaseService, RfidScanContextRecord ctx)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            Context = ctx ?? throw new ArgumentNullException(nameof(ctx));

            ChooseFotoCommand = new RelayCommand<object?>(_ => ChooseFoto());
            _saveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanSave());
            SaveCommand = _saveCommand;
        }

        public RfidScanContextRecord Context { get; }

        public bool HasAktiverZaehler => Context.AktiverZaehlerId.HasValue;

        public string TitleText => HasAktiverZaehler ? "Ablesung erfassen" : "Kein aktiver Zähler";

        public string MessageText
        {
            get
            {
                var garten = Context.GartenNr.HasValue ? $"Garten {Context.GartenNr.Value}" : "(Garten unbekannt)";
                var medium = string.IsNullOrWhiteSpace(Context.Medium) ? "(Medium unbekannt)" : Context.Medium;

                if (HasAktiverZaehler)
                    return $"RFID erkannt: {garten} • {medium}. Aktiver Zähler gefunden.";

                return $"RFID erkannt: {garten} • {medium}, aber aktuell ist kein aktiver Zähler vorhanden.";
            }
        }

        public string AnlageText => (Context.Anlage ?? string.Empty).Trim();
        public string GartenNrText => Context.GartenNr.HasValue ? Context.GartenNr.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        public string MediumText => (Context.Medium ?? string.Empty).Trim();
        public string RfidText => (Context.RfidTagUid ?? string.Empty).Trim();
        public string ZaehlernummerText => (Context.Zaehlernummer ?? string.Empty).Trim();

        public string EichfaelligText => FormatDate(Context.EichfaelligAm);
        public string EingebautText => FormatDate(Context.EingebautAm);

        public string StatusText => (Context.Status ?? string.Empty).Trim();

        public bool HasZaehlernummer => !string.IsNullOrWhiteSpace(ZaehlernummerText);

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

        private string _standText = string.Empty;
        public string StandText
        {
            get => _standText;
            set
            {
                if (SetProperty(ref _standText, value))
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
            if (!HasAktiverZaehler) return;
            if (!TryResolveZaehlerTyp(out var typ))
            {
                ErrorText = "Medium konnte nicht zugeordnet werden.";
                SuccessText = string.Empty;
                return;
            }

            if (!TryParseStand(out var stand) || stand < 0)
            {
                ErrorText = "Bitte einen gültigen, nicht-negativen Zählerstand eingeben.";
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
                var foto = string.IsNullOrWhiteSpace(FotoPfad) ? null : FotoPfad;

                var res = await _supabaseService.AddAblesungResultAsync(typ, zaehlerId, DateTime.Now, stand, foto);
                if (res.Ok)
                {
                    SuccessText = res.Message;
                    StandText = string.Empty;
                }
                else
                {
                    ErrorText = string.IsNullOrWhiteSpace(res.Message) ? "Speichern fehlgeschlagen." : res.Message;
                }
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
            var s = (StandText ?? string.Empty).Trim();
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
