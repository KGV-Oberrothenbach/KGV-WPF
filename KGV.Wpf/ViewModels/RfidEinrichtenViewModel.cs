using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KGV.Wpf.ViewModels
{
    public sealed class RfidEinrichtenViewModel : BaseViewModel
    {
        private static readonly Regex UidRegex = new("^[0-9A-Fa-f]+$", RegexOptions.Compiled);

        private readonly ISupabaseService _supabaseService;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _opLock = new(1, 1);

        private readonly RelayCommand<object?> _checkCommand;
        private readonly RelayCommand<object?> _saveCommand;

        public ObservableCollection<ParzelleRecord> Parzellen { get; } = new();
        public ObservableCollection<string> MediumOptions { get; } = new() { "Wasser", "Strom" };

        private ParzelleRecord? _selectedParzelle;
        public ParzelleRecord? SelectedParzelle
        {
            get => _selectedParzelle;
            set
            {
                if (SetProperty(ref _selectedParzelle, value))
                {
                    ResetCheckState();
                    OnPropertyChanged(nameof(SelectedParzelleRfidWasser));
                    OnPropertyChanged(nameof(SelectedParzelleRfidStrom));
                    _checkCommand.RaiseCanExecuteChanged();
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _selectedMedium = "Wasser";
        public string SelectedMedium
        {
            get => _selectedMedium;
            set
            {
                if (SetProperty(ref _selectedMedium, value))
                {
                    ResetCheckState();
                    _checkCommand.RaiseCanExecuteChanged();
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _uidText = string.Empty;
        public string UidText
        {
            get => _uidText;
            set
            {
                if (SetProperty(ref _uidText, value))
                {
                    ResetCheckState();
                    _checkCommand.RaiseCanExecuteChanged();
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedParzelleRfidWasser => (SelectedParzelle?.RfidWasser ?? string.Empty).Trim();
        public string SelectedParzelleRfidStrom => (SelectedParzelle?.RfidStrom ?? string.Empty).Trim();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    _checkCommand.RaiseCanExecuteChanged();
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _checkOk;
        public bool CheckOk
        {
            get => _checkOk;
            private set
            {
                if (SetProperty(ref _checkOk, value))
                    _saveCommand.RaiseCanExecuteChanged();
            }
        }

        private string _errorText = string.Empty;
        public string ErrorText
        {
            get => _errorText;
            private set => SetProperty(ref _errorText, value);
        }

        private string _infoText = string.Empty;
        public string InfoText
        {
            get => _infoText;
            private set => SetProperty(ref _infoText, value);
        }

        private string _successText = string.Empty;
        public string SuccessText
        {
            get => _successText;
            private set => SetProperty(ref _successText, value);
        }

        public ICommand CheckCommand { get; }
        public ICommand SaveCommand { get; }

        public RfidEinrichtenViewModel(ISupabaseService supabaseService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

            _checkCommand = new RelayCommand<object?>(_ => _ = CheckAsync(), _ => CanCheck());
            _saveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanSave());

            CheckCommand = _checkCommand;
            SaveCommand = _saveCommand;

            _ = LoadParzellenAsync();
        }

        private async Task LoadParzellenAsync()
        {
            if (!await _loadLock.WaitAsync(0))
                return;

            IsBusy = true;
            try
            {
                Parzellen.Clear();
                var list = await _supabaseService.GetParzellenForRfidSetupAsync();
                foreach (var p in list)
                    Parzellen.Add(p);
            }
            catch (Exception ex)
            {
                ErrorText = $"Fehler: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                _loadLock.Release();
            }
        }

        private bool CanCheck()
        {
            if (IsBusy) return false;
            return SelectedParzelle != null;
        }

        private bool CanSave()
        {
            if (IsBusy) return false;
            if (!CheckOk) return false;
            return SelectedParzelle != null;
        }

        private void ResetCheckState()
        {
            CheckOk = false;
            ErrorText = string.Empty;
            InfoText = string.Empty;
            SuccessText = string.Empty;
        }

        private static bool IsUidValid(string uid)
        {
            uid = (uid ?? string.Empty).Trim();
            if (uid.Length < 4 || uid.Length > 64) return false;
            return UidRegex.IsMatch(uid);
        }

        private bool TryResolveZaehlerTyp(out short typ)
        {
            typ = 0;
            var m = (SelectedMedium ?? string.Empty).Trim();
            if (m.Equals("strom", StringComparison.OrdinalIgnoreCase)) { typ = 1; return true; }
            if (m.Equals("wasser", StringComparison.OrdinalIgnoreCase)) { typ = 2; return true; }
            return false;
        }

        private string GetExistingRfid(short typ)
        {
            var p = SelectedParzelle;
            if (p == null) return string.Empty;

            return typ switch
            {
                1 => (p.RfidStrom ?? string.Empty).Trim(),
                2 => (p.RfidWasser ?? string.Empty).Trim(),
                _ => string.Empty
            };
        }

        private async Task CheckAsync()
        {
            if (SelectedParzelle == null)
                return;

            if (!TryResolveZaehlerTyp(out var typ))
            {
                ErrorText = "Bitte Medium auswählen.";
                return;
            }

            var uid = (UidText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(uid))
            {
                ErrorText = "Bitte eine UID eingeben.";
                return;
            }

            if (!IsUidValid(uid))
            {
                ErrorText = "UID ist ungültig.";
                return;
            }

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            ErrorText = string.Empty;
            InfoText = string.Empty;
            SuccessText = string.Empty;

            try
            {
                var conflict = await _supabaseService.FindRfidUidAssignmentAsync(uid);
                if (conflict != null)
                {
                    var medium = conflict.ZaehlerTyp == 1 ? "Strom" : "Wasser";
                    ErrorText = $"UID ist bereits vergeben: Anlage '{conflict.Anlage}', Garten '{conflict.GartenNr}', Medium {medium}.";
                    CheckOk = false;
                    return;
                }

                var existing = GetExistingRfid(typ);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    var medium = typ == 1 ? "Strom" : "Wasser";
                    ErrorText = $"Diese Parzelle hat für {medium} bereits eine RFID. Speichern ist blockiert.";
                    CheckOk = false;
                    return;
                }

                InfoText = "OK – UID ist frei und kann gespeichert werden.";
                CheckOk = true;
            }
            catch (Exception ex)
            {
                ErrorText = $"Fehler: {ex.Message}";
                CheckOk = false;
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }

        private async Task SaveAsync()
        {
            if (SelectedParzelle == null)
                return;

            if (!TryResolveZaehlerTyp(out var typ))
            {
                ErrorText = "Bitte Medium auswählen.";
                return;
            }

            var uid = (UidText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(uid))
            {
                ErrorText = "Bitte eine UID eingeben.";
                return;
            }

            if (!IsUidValid(uid))
            {
                ErrorText = "UID ist ungültig.";
                return;
            }

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            ErrorText = string.Empty;
            InfoText = string.Empty;
            SuccessText = string.Empty;

            try
            {
                var conflict = await _supabaseService.FindRfidUidAssignmentAsync(uid);
                if (conflict != null)
                {
                    var medium = conflict.ZaehlerTyp == 1 ? "Strom" : "Wasser";
                    ErrorText = $"UID ist bereits vergeben: Anlage '{conflict.Anlage}', Garten '{conflict.GartenNr}', Medium {medium}.";
                    CheckOk = false;
                    return;
                }

                var existing = GetExistingRfid(typ);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    var medium = typ == 1 ? "Strom" : "Wasser";
                    ErrorText = $"Diese Parzelle hat für {medium} bereits eine RFID. Speichern ist blockiert.";
                    CheckOk = false;
                    return;
                }

                var ok = await _supabaseService.SetParzelleRfidAsync(SelectedParzelle.Id, typ, uid);
                if (!ok)
                {
                    ErrorText = "Speichern fehlgeschlagen.";
                    CheckOk = false;
                    return;
                }

                var mediumText = typ == 1 ? "Strom" : "Wasser";
                SuccessText = $"RFID gespeichert: Garten '{SelectedParzelle.GartenNr}' ({SelectedParzelle.Anlage}), Medium {mediumText}.";

                UidText = string.Empty;

                // Liste neu laden, damit die aktuellen RFID-Felder sichtbar sind.
                await LoadParzellenAsync();
                SelectedParzelle = Parzellen.FirstOrDefault(p => p.Id == SelectedParzelle.Id);
                OnPropertyChanged(nameof(SelectedParzelleRfidWasser));
                OnPropertyChanged(nameof(SelectedParzelleRfidStrom));
                CheckOk = false;
            }
            catch (Exception ex)
            {
                ErrorText = $"Fehler: {ex.Message}";
                CheckOk = false;
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }
    }
}
