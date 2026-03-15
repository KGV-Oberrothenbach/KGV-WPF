// Datei: KGV.Wpf/ViewModels/MemberDetailViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;
using KGV.Wpf.Messages;
using KGV.Views;

namespace KGV.Wpf.ViewModels
{
    public class MemberDetailViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly IAuthService _authService;

        private string? _lockUserId;

        public MemberDTO SelectedMember { get; }

        public ObservableCollection<string> ArbeitsstundenAltersregelTypOptions { get; } = new()
        {
            "keine",
            "frau75",
            "mann80"
        };

        private bool _isHauptmitglied;
        public bool IsHauptmitglied
        {
            get => _isHauptmitglied;
            private set => SetProperty(ref _isHauptmitglied, value);
        }

        public bool ShowGoToHauptmitgliedButton => false;
        public RelayCommand<object?> GoToHauptmitgliedCommand { get; } = new RelayCommand<object?>(_ => { }, _ => false);

        public bool ShowParzellenSection => true;

        private MitgliedRecord? _nebenmitgliedRecord;
        private bool _hasNebenmitglied;
        public bool HasNebenmitglied
        {
            get => _hasNebenmitglied;
            private set
            {
                if (SetProperty(ref _hasNebenmitglied, value))
                {
                    OnPropertyChanged(nameof(ShowNebenmitgliedButton));
                    OnPropertyChanged(nameof(NebenmitgliedButtonText));
                }
            }
        }

        public bool ShowNebenmitgliedButton => HasNebenmitglied || IsEditMode;
        public string NebenmitgliedButtonText => HasNebenmitglied ? "Nebenmitglied" : "Nebenmitglied anlegen";

        public bool ShowAdresseUebernehmenButton => false;

        private MemberDTO _originalSnapshot;

        public ObservableCollection<ParzellenBelegungDTO> ParzellenBelegungen { get; } = new();
        public ObservableCollection<ParzelleRecord> AvailableParzellen { get; } = new();

        private ParzellenBelegungDTO? _selectedBelegung;
        public ParzellenBelegungDTO? SelectedBelegung
        {
            get => _selectedBelegung;
            set
            {
                if (SetProperty(ref _selectedBelegung, value))
                {
                    InvalidateCommands();

                    // Nur Kontext setzen (Sidebar-Menü auf Garten aktivieren), ohne direkt zu navigieren.
                    // WICHTIG: Beim Navigieren weg von der Seite kann WPF `SelectedItem` auf null setzen.
                    // Das darf den globalen Garten-Kontext (Sidebar) nicht wieder "löschen".
                    if (_selectedBelegung != null)
                        WeakReferenceMessenger.Default.Send(new ParzelleContextChangedMessage(_selectedBelegung));
                }
            }
        }

        private ParzelleRecord? _selectedParzelleToAssign;
        public ParzelleRecord? SelectedParzelleToAssign
        {
            get => _selectedParzelleToAssign;
            set
            {
                if (SetProperty(ref _selectedParzelleToAssign, value))
                {
                    InvalidateCommands();
                }
            }
        }

        private DateTime? _assignVonDatum = DateTime.Today;
        public DateTime? AssignVonDatum
        {
            get => _assignVonDatum;
            set
            {
                if (SetProperty(ref _assignVonDatum, value?.Date))
                {
                    InvalidateCommands();
                }
            }
        }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            private set => SetProperty(ref _isEditMode, value);
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }

        private bool _parzellenChanged;

        // ✅ FIX: MemberDetailViewModel wurde beim Navigieren immer neu geladen (oder in einem ungünstigen Moment),
        // aber manchmal NICHT initialisiert, wenn Supabase noch nicht ready war oder wenn exceptions geschluckt wurden.
        // Daher: (1) initialisieren nur einmal pro VM-Instanz, (2) Fehler sichtbar machen, (3) Loading-State für UI.
        private bool _isInitialized;
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public RelayCommand<object?> ToggleEditCommand { get; }
        public RelayCommand<object?> SaveCommand { get; }
        public RelayCommand<object?> CancelCommand { get; }
        public RelayCommand<object?> NebenmitgliedCommand { get; }
        public RelayCommand<object?> CopyAddressFromHauptmitgliedCommand { get; }

        public RelayCommand<object?> AssignParzelleCommand { get; }
        public RelayCommand<object?> EndBelegungCommand { get; }
        public RelayCommand<object?> OpenSelectedParzelleCommand { get; }

        public MemberDetailViewModel(ISupabaseService supabaseService, IAuthService authService, MemberDTO member)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            SelectedMember = member ?? throw new ArgumentNullException(nameof(member));

            _originalSnapshot = SelectedMember.Clone();

            SelectedMember.PropertyChanged += (_, __) =>
            {
                if (!IsEditMode)
                    return;

                IsDirty = !SelectedMember.ValueEquals(_originalSnapshot);
                InvalidateCommands();
            };

            ToggleEditCommand = new RelayCommand<object?>(_ => _ = ToggleEditAsync());
            SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanSave());
            CancelCommand = new RelayCommand<object?>(_ => _ = CancelAsync(), _ => CanCancel());
            AssignParzelleCommand = new RelayCommand<object?>(_ => _ = AssignParzelleAsync(), _ => CanAssignParzelle());
            EndBelegungCommand = new RelayCommand<object?>(_ => _ = EndBelegungAsync(), _ => CanEndBelegung());
            OpenSelectedParzelleCommand = new RelayCommand<object?>(_ => OpenSelectedParzelle(), _ => SelectedBelegung != null);

            NebenmitgliedCommand = new RelayCommand<object?>(_ => _ = NebenmitgliedAsync(), _ => ShowNebenmitgliedButton);
            CopyAddressFromHauptmitgliedCommand = new RelayCommand<object?>(_ => { }, _ => false);
        }

        private void OpenSelectedParzelle()
        {
            if (SelectedBelegung == null)
                return;

            WeakReferenceMessenger.Default.Send(new ParzelleSelectedMessage(SelectedBelegung));
        }

        public async Task OnNavigatedToAsync()
        {
            // ✅ FIX: Nicht mehrfach parallel laden, wenn man schnell klickt / Navigation mehrfach feuert.
            if (_isInitialized)
            {
                // Trotzdem Commands/UI aktualisieren (z.B. wenn View erneut angezeigt wird)
                InvalidateCommands();
                return;
            }

            _isInitialized = true;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                // falls SelectedMember nur ein Placeholder ist (z.B. nur Id gesetzt), wird hier alles nachgeladen
                await LoadMemberAsync();
                await LoadParzellenAsync();
                await RefreshNebenmitgliedAsync();

                IsEditMode = false;
                IsDirty = false;
                InvalidateCommands();
            }
            catch (Exception ex)
            {
                // ✅ vorher: Exceptions wären "unsichtbar" und UI bleibt leer -> wirkt wie "lädt keine Daten"
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task OnNavigatedFromAsync()
        {
            if (IsEditMode && !string.IsNullOrEmpty(_lockUserId))
            {
                await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockUserId, force: false);
                _lockUserId = null;
            }

            IsEditMode = false;
            IsDirty = false;
        }

        private async Task RefreshNebenmitgliedAsync()
        {
            _nebenmitgliedRecord = await _supabaseService.GetNebenmitgliedByHauptmitgliedIdAsync(SelectedMember.Id);
            HasNebenmitglied = _nebenmitgliedRecord != null;
            NebenmitgliedCommand.RaiseCanExecuteChanged();
        }

        private static MemberDTO ToMemberDto(MitgliedRecord rec)
        {
            return new MemberDTO
            {
                Id = rec.Id,
                Vorname = rec.Vorname ?? string.Empty,
                Nachname = rec.Name ?? string.Empty,
                Geburtsdatum = rec.Geburtsdatum,
                Strasse = rec.Adresse ?? string.Empty,
                PLZ = rec.Plz ?? string.Empty,
                Ort = rec.Ort ?? string.Empty,
                Telefon = rec.Telefon ?? string.Empty,
                Mobilnummer = rec.Handy ?? string.Empty,
                Email = rec.Email ?? string.Empty,
                EmailInfoEinwilligung = rec.EmailInfoEinwilligung,
                EmailRechnungEinwilligung = rec.EmailRechnungEinwilligung,
                Bemerkungen = rec.Bemerkung ?? string.Empty,
                WhatsappEinwilligung = rec.WhatsappEinwilligung,
                MitgliedSeit = rec.MitgliedSeit,
                MitgliedEnde = rec.MitgliedEnde,
                Role = rec.Role ?? string.Empty
            };
        }

        private async Task NebenmitgliedAsync()
        {
            if (HasNebenmitglied)
            {
                if (_nebenmitgliedRecord == null)
                    _nebenmitgliedRecord = await _supabaseService.GetNebenmitgliedByHauptmitgliedIdAsync(SelectedMember.Id);

                if (_nebenmitgliedRecord == null)
                {
                    await RefreshNebenmitgliedAsync();
                    return;
                }

                var ctx = new NebenmitgliedContext(SelectedMember.Clone(), ToMemberDto(_nebenmitgliedRecord));
                WeakReferenceMessenger.Default.Send(new NebenmitgliedSelectedMessage(ctx));
                return;
            }

            if (!IsEditMode)
                return;

            var dlg = new NebenmitgliedDialog
            {
                Owner = Application.Current?.MainWindow
            };

            // Vorschlag: Nachname übernehmen
            dlg.SetInitialValues(vorname: string.Empty, nachname: SelectedMember.Nachname, adresseUebernehmen: true);

            if (dlg.ShowDialog() != true)
                return;

            if (string.IsNullOrWhiteSpace(dlg.Vorname) || string.IsNullOrWhiteSpace(dlg.Nachname))
            {
                MessageBox.Show("Bitte Vorname und Nachname angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MitgliedRecord? created;
            try
            {
                created = await _supabaseService.CreateNebenmitgliedAsync(SelectedMember.Id, dlg.Vorname.Trim(), dlg.Nachname.Trim(), dlg.AdresseUebernehmen);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nebenmitglied konnte nicht angelegt werden:\n\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (created == null)
            {
                MessageBox.Show("Nebenmitglied konnte nicht angelegt werden (kein Datensatz zurückgegeben).", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await RefreshNebenmitgliedAsync();

            var context = new NebenmitgliedContext(SelectedMember.Clone(), ToMemberDto(created));
            WeakReferenceMessenger.Default.Send(new NebenmitgliedSelectedMessage(context));
        }

        private async Task LoadMemberAsync()
        {
            ErrorMessage = null;

            var rec = await _supabaseService.GetMitgliedByIdAsync(SelectedMember.Id);
            if (rec == null)
            {
                ErrorMessage = $"Mitglied nicht gefunden (Id={SelectedMember.Id}).";
                return;
            }

            // ✅ Vollständig mappen (inkl. Mobilnummer) – vorher fehlte z.B. Mobilnummer komplett
            SelectedMember.Vorname = rec.Vorname ?? "";
            SelectedMember.Nachname = rec.Name ?? "";
            SelectedMember.Geburtsdatum = rec.Geburtsdatum;

            SelectedMember.Strasse = rec.Adresse ?? "";
            SelectedMember.PLZ = rec.Plz ?? "";
            SelectedMember.Ort = rec.Ort ?? "";

            SelectedMember.Telefon = rec.Telefon ?? "";
            SelectedMember.Mobilnummer = rec.Handy ?? "";   // ✅ FIX
            SelectedMember.Email = rec.Email ?? "";
            SelectedMember.EmailInfoEinwilligung = rec.EmailInfoEinwilligung;
            SelectedMember.EmailRechnungEinwilligung = rec.EmailRechnungEinwilligung;

            SelectedMember.Bemerkungen = rec.Bemerkung ?? "";
            SelectedMember.WhatsappEinwilligung = rec.WhatsappEinwilligung;

            SelectedMember.MitgliedSeit = rec.MitgliedSeit;
            SelectedMember.MitgliedEnde = rec.MitgliedEnde;

            SelectedMember.Role = rec.Role ?? "";
            SelectedMember.ArbeitsstundenAltersregelTyp = rec.ArbeitsstundenAltersregelTyp ?? "keine";

            IsHauptmitglied = rec.HauptmitgliedId == null;

            _originalSnapshot = SelectedMember.Clone();
        }

        private async Task LoadParzellenAsync()
        {
            ParzellenBelegungen.Clear();
            AvailableParzellen.Clear();
            SelectedBelegung = null;
            SelectedParzelleToAssign = null;
            AssignVonDatum = DateTime.Today;

            var parzellen = await _supabaseService.GetAllParzellenAsync();
            var memberBelegungen = await _supabaseService.GetBelegungenForMitgliedAsync(SelectedMember.Id);
            var allBelegungen = await _supabaseService.GetAllParzellenBelegungenAsync();

            var parzById = parzellen.ToDictionary(p => p.Id, p => p);

            foreach (var b in memberBelegungen
                         .OrderByDescending(x => x.BisDatum == null)
                         .ThenByDescending(x => x.VonDatum ?? DateTime.MinValue))
            {
                parzById.TryGetValue(b.ParzelleId, out var p);

                ParzellenBelegungen.Add(new ParzellenBelegungDTO
                {
                    BelegungId = b.Id,
                    ParzelleId = b.ParzelleId,
                    MitgliedId = b.MitgliedId,
                    GartenNr = p?.GartenNr ?? $"#{b.ParzelleId}",
                    Anlage = p?.Anlage ?? "",
                    VonDatum = b.VonDatum?.Date,
                    BisDatum = b.BisDatum?.Date
                });
            }

            var today = DateTime.Today;

            var activeToday = allBelegungen
                .GroupBy(b => b.ParzelleId)
                .Select(g => g.Where(x =>
                        (x.VonDatum ?? DateTime.MinValue).Date <= today &&
                        (x.BisDatum == null || x.BisDatum.Value.Date >= today))
                    .OrderByDescending(x => x.VonDatum ?? DateTime.MinValue)
                    .FirstOrDefault())
                .Where(x => x != null)
                .ToDictionary(x => x!.ParzelleId, x => x!);

            foreach (var p in parzellen
                         .OrderBy(x => GetGartenNrSortKey(x.GartenNr))
                         .ThenBy(x => x.GartenNr, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!activeToday.TryGetValue(p.Id, out _))
                {
                    AvailableParzellen.Add(p);
                }
            }

            InvalidateCommands();
        }

        private async Task ToggleEditAsync()
        {
            if (!IsEditMode)
            {
                var userId = _authService.CurrentUserId;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    MessageBox.Show("Nicht angemeldet. Bitte erneut einloggen.", "Fehler",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var locked = await _supabaseService.TryLockMitgliedAsync(SelectedMember.Id, userId);
                if (!locked)
                {
                    MessageBox.Show("Datensatz ist aktuell gesperrt. Bitte später erneut versuchen.", "Gesperrt",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _lockUserId = userId;

                IsEditMode = true;
                _originalSnapshot = SelectedMember.Clone();
                IsDirty = false;
                _parzellenChanged = false;

                OnPropertyChanged(nameof(ShowNebenmitgliedButton));
                NebenmitgliedCommand.RaiseCanExecuteChanged();
            }
            else
            {
                await CancelAsync();
            }

            InvalidateCommands();
        }

        private bool CanSave() => IsEditMode && (IsDirty || _parzellenChanged);
        private bool CanCancel() => IsEditMode;

        private async Task SaveAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_lockUserId))
                {
                    MessageBox.Show("Kein Lock aktiv. Bitte Bearbeiten erneut starten.", "Fehler",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (IsDirty)
                {
                    var ok = await _supabaseService.UpdateMitgliedAsync(SelectedMember, _lockUserId);
                    if (!ok)
                    {
                        MessageBox.Show("Speichern fehlgeschlagen (ggf. Lock verloren oder keine Berechtigung).", "Fehler",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    _originalSnapshot = SelectedMember.Clone();
                    IsDirty = false;

                    WeakReferenceMessenger.Default.Send(new MemberSavedMessage(SelectedMember.Clone()));
                }

                _parzellenChanged = false;

                if (!string.IsNullOrEmpty(_lockUserId))
                {
                    await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockUserId, force: false);
                    _lockUserId = null;
                }

                IsEditMode = false;
                InvalidateCommands();

                MessageBox.Show("Änderungen gespeichert.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CancelAsync()
        {
            try
            {
                if (IsDirty)
                {
                    var decision = MessageBox.Show(
                        "Ungespeicherte Änderungen verwerfen?",
                        "Abbrechen",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (decision != MessageBoxResult.Yes)
                        return;
                }
                else if (_parzellenChanged)
                {
                    var decision = MessageBox.Show(
                        "Parzellen-Zuordnungen wurden bereits gespeichert. Bearbeiten wirklich beenden?",
                        "Abbrechen",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (decision != MessageBoxResult.Yes)
                        return;
                }

                SelectedMember.CopyFrom(_originalSnapshot);

                if (!string.IsNullOrEmpty(_lockUserId))
                {
                    await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockUserId, force: false);
                    _lockUserId = null;
                }

                IsEditMode = false;
                IsDirty = false;
                _parzellenChanged = false;
                InvalidateCommands();

                OnPropertyChanged(nameof(ShowNebenmitgliedButton));
                NebenmitgliedCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Abbrechen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanAssignParzelle()
        {
            if (!IsEditMode)
                return false;

            if (SelectedParzelleToAssign == null)
                return false;

            if (!AssignVonDatum.HasValue)
                return false;

            return true;
        }

        private async Task AssignParzelleAsync()
        {
            if (SelectedParzelleToAssign == null)
                return;

            try
            {
                var start = (AssignVonDatum ?? DateTime.Today).Date;

                var ok = await _supabaseService.AssignParzelleToMitgliedAsync(
                    SelectedMember.Id,
                    SelectedParzelleToAssign.Id,
                    start);

                if (!ok)
                {
                    MessageBox.Show(
                        "Zuweisung fehlgeschlagen. Der Datensatz konnte nicht gespeichert werden (keine Details von der Datenbank).",
                        "Fehler",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                await LoadParzellenAsync();

                _parzellenChanged = true;
                InvalidateCommands();

                MessageBox.Show("Parzelle zugewiesen.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zuweisen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanEndBelegung()
        {
            if (!IsEditMode)
                return false;

            if (SelectedBelegung == null)
                return false;

            if (SelectedBelegung.BisDatum.HasValue)
                return false;

            return true;
        }

        private async Task EndBelegungAsync()
        {
            if (SelectedBelegung == null)
                return;

            try
            {
                var today = DateTime.Today;

                var ok = await _supabaseService.EndParzellenBelegungAsync(SelectedBelegung.BelegungId, today);
                if (!ok)
                {
                    MessageBox.Show("Belegung konnte nicht beendet werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await LoadParzellenAsync();

                _parzellenChanged = true;
                InvalidateCommands();

                MessageBox.Show("Belegung beendet.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Beenden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InvalidateCommands()
        {
            SaveCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            AssignParzelleCommand.RaiseCanExecuteChanged();
            EndBelegungCommand.RaiseCanExecuteChanged();
            OpenSelectedParzelleCommand.RaiseCanExecuteChanged();
            NebenmitgliedCommand.RaiseCanExecuteChanged();
        }

        private static int GetGartenNrSortKey(string? gartenNr)
        {
            if (string.IsNullOrWhiteSpace(gartenNr))
                return int.MaxValue;

            var digits = new string(gartenNr.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var n) ? n : int.MaxValue;
        }
    }
}