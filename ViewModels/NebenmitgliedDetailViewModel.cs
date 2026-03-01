using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Helpers;

namespace KGV.ViewModels
{
    public sealed class NebenmitgliedDetailViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly IAuthService _authService;

        private string? _lockUserId;

        public MemberDTO SelectedMember { get; }
        public MemberDTO Hauptmitglied { get; }

        private MemberDTO _originalSnapshot;

        public bool ShowParzellenSection => false;
        public bool ShowNewContractButton => false;
        public bool ShowNebenmitgliedButton => false;

        public string NebenmitgliedButtonText => "Nebenmitglied";

        public bool ShowAdresseUebernehmenButton => IsEditMode;

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

        public RelayCommand<object?> ToggleEditCommand { get; }
        public RelayCommand<object?> SaveCommand { get; }
        public RelayCommand<object?> CancelCommand { get; }
        public RelayCommand<object?> CopyAddressFromHauptmitgliedCommand { get; }

        public RelayCommand<object?> NebenmitgliedCommand { get; }

        // noch nicht implementiert (Binding existiert in View)
        public RelayCommand<object?> NewContractCommand { get; }
        public RelayCommand<object?> CancelMembershipCommand { get; }

        public NebenmitgliedDetailViewModel(ISupabaseService supabaseService, IAuthService authService, NebenmitgliedContext context)
        {
            _supabaseService = supabaseService;
            _authService = authService;

            Hauptmitglied = context.Hauptmitglied;
            SelectedMember = context.Nebenmitglied;

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
            CopyAddressFromHauptmitgliedCommand = new RelayCommand<object?>(_ => CopyAddressFromHauptmitglied(), _ => IsEditMode);

            NebenmitgliedCommand = new RelayCommand<object?>(_ => { }, _ => false);

            NewContractCommand = new RelayCommand<object?>(_ => MessageBox.Show("Noch nicht implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information));
            CancelMembershipCommand = new RelayCommand<object?>(_ => MessageBox.Show("Noch nicht implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information));
        }

        public async Task OnNavigatedToAsync()
        {
            await LoadMemberAsync();

            IsEditMode = false;
            IsDirty = false;
            InvalidateCommands();
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

        private async Task LoadMemberAsync()
        {
            var rec = await _supabaseService.GetMitgliedByIdAsync(SelectedMember.Id);
            if (rec == null)
                return;

            SelectedMember.Vorname = rec.Vorname ?? "";
            SelectedMember.Nachname = rec.Name ?? "";
            SelectedMember.Geburtsdatum = rec.Geburtsdatum;

            SelectedMember.Strasse = rec.Adresse ?? "";
            SelectedMember.PLZ = rec.Plz ?? "";
            SelectedMember.Ort = rec.Ort ?? "";

            SelectedMember.Telefon = rec.Telefon ?? "";
            SelectedMember.Email = rec.Email ?? "";

            SelectedMember.Bemerkungen = rec.Bemerkung ?? "";
            SelectedMember.WhatsappEinwilligung = rec.WhatsappEinwilligung;

            SelectedMember.MitgliedSeit = rec.MitgliedSeit;
            SelectedMember.MitgliedEnde = rec.MitgliedEnde;

            SelectedMember.Role = rec.Role ?? "";
            _originalSnapshot = SelectedMember.Clone();
        }

        private void CopyAddressFromHauptmitglied()
        {
            SelectedMember.Strasse = Hauptmitglied.Strasse;
            SelectedMember.PLZ = Hauptmitglied.PLZ;
            SelectedMember.Ort = Hauptmitglied.Ort;
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
            }
            else
            {
                await CancelAsync();
            }

            InvalidateCommands();
        }

        private bool CanSave() => IsEditMode && IsDirty;
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

                var ok = await _supabaseService.UpdateMitgliedAsync(SelectedMember, _lockUserId);
                if (!ok)
                {
                    MessageBox.Show("Speichern fehlgeschlagen (ggf. Lock verloren oder keine Berechtigung).", "Fehler",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _originalSnapshot = SelectedMember.Clone();
                IsDirty = false;

                if (!string.IsNullOrEmpty(_lockUserId))
                {
                    await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockUserId, force: false);
                    _lockUserId = null;
                }

                IsEditMode = false;
                InvalidateCommands();

                MessageBox.Show("Nebenmitglied gespeichert.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
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
                SelectedMember.CopyFrom(_originalSnapshot);

                if (!string.IsNullOrEmpty(_lockUserId))
                {
                    await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockUserId, force: false);
                    _lockUserId = null;
                }

                IsEditMode = false;
                IsDirty = false;
                InvalidateCommands();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Abbrechen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InvalidateCommands()
        {
            SaveCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            CopyAddressFromHauptmitgliedCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(ShowAdresseUebernehmenButton));
        }
    }
}
