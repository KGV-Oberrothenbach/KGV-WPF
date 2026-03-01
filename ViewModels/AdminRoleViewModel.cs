using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Helpers;

namespace KGV.ViewModels
{
    public sealed class AdminRoleViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly IAuthService _authService;

        private string? _lockUserId;

        public MemberDTO SelectedMember { get; }

        public ObservableCollection<string> Roles { get; } = new() { "admin", "vorstand", "user" };

        private string _selectedRole = "user";
        public string SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (SetProperty(ref _selectedRole, value ?? "user"))
                {
                    IsDirty = true;
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value);
        }

        public bool IsRoleEditable => SelectedMember.Id != 7;

        public RelayCommand<object?> SaveCommand { get; }

        public AdminRoleViewModel(ISupabaseService supabaseService, IAuthService authService, MemberDTO member)
        {
            _supabaseService = supabaseService;
            _authService = authService;
            SelectedMember = member;

            SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanSave());
        }

        public async Task OnNavigatedToAsync()
        {
            await LoadAsync();
            IsDirty = false;
            SaveCommand.RaiseCanExecuteChanged();
        }

        public async Task OnNavigatedFromAsync()
        {
            if (!string.IsNullOrEmpty(_lockUserId))
            {
                await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, _lockUserId, force: false);
                _lockUserId = null;
            }
        }

        private async Task LoadAsync()
        {
            var rec = await _supabaseService.GetMitgliedByIdAsync(SelectedMember.Id);
            if (rec == null)
                return;

            SelectedMember.Vorname = rec.Vorname ?? string.Empty;
            SelectedMember.Nachname = rec.Name ?? string.Empty;
            SelectedMember.Role = rec.Role ?? "user";

            SelectedRole = SelectedMember.Role;
            IsDirty = false;
        }

        private bool CanSave()
        {
            if (!_authService.IsAdmin)
                return false;

            if (!IsDirty)
                return false;

            if (!IsRoleEditable)
                return false;

            return true;
        }

        private async Task SaveAsync()
        {
            try
            {
                if (!IsRoleEditable)
                {
                    MessageBox.Show("Für dieses Mitglied ist die Rollenbearbeitung gesperrt.", "Gesperrt", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var userId = _authService.CurrentUserId;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    MessageBox.Show("Nicht angemeldet. Bitte erneut einloggen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // vollständiges DTO laden (damit UpdateMitgliedAsync nichts überschreibt)
                var rec = await _supabaseService.GetMitgliedByIdAsync(SelectedMember.Id);
                if (rec == null)
                {
                    MessageBox.Show("Mitglied konnte nicht geladen werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dto = new MemberDTO
                {
                    Id = rec.Id,
                    Vorname = rec.Vorname ?? string.Empty,
                    Nachname = rec.Name ?? string.Empty,
                    Email = rec.Email ?? string.Empty,
                    Role = SelectedRole,

                    Geburtsdatum = rec.Geburtsdatum,
                    Strasse = rec.Adresse ?? string.Empty,
                    PLZ = rec.Plz ?? string.Empty,
                    Ort = rec.Ort ?? string.Empty,
                    Telefon = rec.Telefon ?? string.Empty,
                    Bemerkungen = rec.Bemerkung ?? string.Empty,
                    WhatsappEinwilligung = rec.WhatsappEinwilligung,
                    MitgliedSeit = rec.MitgliedSeit,
                    MitgliedEnde = rec.MitgliedEnde
                };

                var ok = await _supabaseService.UpdateMitgliedAsync(dto, userId);
                if (!ok)
                {
                    MessageBox.Show("Speichern fehlgeschlagen (ggf. Lock verloren oder keine Berechtigung).", "Fehler",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                SelectedMember.Role = SelectedRole;
                IsDirty = false;
                SaveCommand.RaiseCanExecuteChanged();

                await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, userId, force: false);
                _lockUserId = null;

                MessageBox.Show("Rolle gespeichert.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
