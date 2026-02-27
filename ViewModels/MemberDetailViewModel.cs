using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KGV.Core.Interfaces;
using KGV.Core.Models;

namespace KGV.ViewModels
{
    public class MemberDetailViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private string? _lockedByUserId;

        // Das gebundene DTO (wird in der View gebunden)
        public MemberDTO SelectedMember { get; }

        // Snapshot für Cancel/Dirty
        private readonly MemberDTO _originalSnapshot;

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (_isEditMode == value) return;
                _isEditMode = value;
                OnPropertyChanged(nameof(IsEditMode));

                // Buttons neu bewerten
                InvalidateCommands();
            }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value) return;
                _isDirty = value;
                OnPropertyChanged(nameof(IsDirty));

                // Buttons neu bewerten
                InvalidateCommands();
            }
        }

        // Buttons
        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand NewContractCommand { get; }
        public ICommand CancelMembershipCommand { get; }

        public MemberDetailViewModel(ISupabaseService supabaseService, MemberDTO member)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            SelectedMember = member ?? throw new ArgumentNullException(nameof(member));

            // Originalzustand merken (für Cancel + Dirty Compare)
            _originalSnapshot = member.Clone();

            EditCommand = new KGV.Helpers.RelayCommand<object?>(async _ => await EditAsync(), _ => !IsEditMode);
            SaveCommand = new KGV.Helpers.RelayCommand<object?>(async _ => await SaveAsync(), _ => IsEditMode && IsDirty);
            CancelCommand = new KGV.Helpers.RelayCommand<object?>(async _ => await CancelAsync(), _ => IsEditMode);

            NewContractCommand = new KGV.Helpers.RelayCommand<object?>(_ => NewContract(), _ => true);

            CancelMembershipCommand = new KGV.Helpers.RelayCommand<object?>(
                _ => CancelMembership(),
                _ => IsEditMode && SelectedMember.Aktiv);

            // Dirty Tracking aktivieren: sobald Properties verändert werden -> IsDirty setzen
            SelectedMember.Changed += SelectedMember_Changed;
        }

        private void SelectedMember_Changed(object? sender, EventArgs e)
        {
            // Dirty nur im EditMode aktiv werten (sonst würde schon reines Anzeigen dirty machen)
            if (!IsEditMode) return;

            IsDirty = !SelectedMember.ValueEquals(_originalSnapshot);
        }

        // =============================
        // NAVIGATION LIFECYCLE
        // =============================

        public Task OnNavigatedToAsync()
        {
            return Task.CompletedTask;
        }

        public async Task OnNavigatedFromAsync()
        {
            // Wenn User weg navigiert während Edit aktiv:
            // - lock freigeben
            // - EditMode beenden
            // - dirty verwerfen (optional)
            if (IsEditMode && !string.IsNullOrEmpty(_lockedByUserId))
            {
                await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockedByUserId, force: false);
                _lockedByUserId = null;
            }

            IsEditMode = false;
            IsDirty = false;
        }

        // =============================
        // EDIT LOGIK
        // =============================

        private async Task EditAsync()
        {
            var userId = _supabaseService.Client.Auth.CurrentUser?.Id;
            if (string.IsNullOrEmpty(userId))
                return;

            var success = await _supabaseService.TryLockMitgliedAsync(SelectedMember.Id, userId);
            if (!success)
            {
                MessageBox.Show("Datensatz ist bereits gesperrt.", "Hinweis");
                return;
            }

            _lockedByUserId = userId;

            // Snapshot neu setzen beim Start vom Edit (wichtig, falls man mehrfach rein/raus geht)
            _originalSnapshot.CopyFrom(SelectedMember);

            IsEditMode = true;
            IsDirty = false;

            InvalidateCommands();
        }

        private async Task SaveAsync()
        {
            if (!IsEditMode) return;
            if (!IsDirty) return;

            try
            {
                // TODO: hier später echte Save-Logik rein (Supabase Update)
                // Beispiel (wenn du so etwas hast):
                // await _supabaseService.UpdateMitgliedAsync(SelectedMember);

                // Nach Save: Snapshot aktualisieren
                _originalSnapshot.CopyFrom(SelectedMember);
                IsDirty = false;

                // EditMode beenden und Lock lösen
                await UnlockIfNeededAsync();
                IsEditMode = false;

                MessageBox.Show("Änderungen gespeichert.", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler");
            }
            finally
            {
                InvalidateCommands();
            }
        }

        private async Task CancelAsync()
        {
            if (!IsEditMode) return;

            // Wenn dirty, kurz nachfragen
            if (IsDirty)
            {
                var result = MessageBox.Show(
                    "Änderungen verwerfen?",
                    "Abbrechen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            // Werte zurücksetzen
            SelectedMember.SuppressChangedEvents = true;
            try
            {
                SelectedMember.CopyFrom(_originalSnapshot);
            }
            finally
            {
                SelectedMember.SuppressChangedEvents = false;
            }

            // Zustände zurück
            IsDirty = false;

            // Lock lösen + EditMode aus
            await UnlockIfNeededAsync();
            IsEditMode = false;

            InvalidateCommands();
        }

        private async Task UnlockIfNeededAsync()
        {
            if (!string.IsNullOrEmpty(_lockedByUserId))
            {
                await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockedByUserId, force: false);
                _lockedByUserId = null;
            }
        }

        private void CancelMembership()
        {
            if (!IsEditMode)
                return;

            var result = MessageBox.Show(
                "Mitgliedschaft wirklich beenden?",
                "Bestätigung",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            SelectedMember.MitgliedEnde = DateTime.Today;
            // Dirty wird durch Changed-Event getriggert
        }

        private void NewContract()
        {
            // später
        }

        private void InvalidateCommands()
        {
            // Da RelayCommand<object?> evtl. kein RaiseCanExecuteChanged hat:
            CommandManager.InvalidateRequerySuggested();
        }
    }
}