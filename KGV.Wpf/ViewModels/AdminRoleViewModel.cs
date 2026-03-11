using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using KGV.Wpf.Messages;

namespace KGV.Wpf.ViewModels
{
    public sealed class AdminRoleViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly IAuthService _authService;

        private string? _lockUserId;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                    AddUserCommand.RaiseCanExecuteChanged();
                    DeleteUserCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _hasAppUser;
        public bool HasAppUser
        {
            get => _hasAppUser;
            private set
            {
                if (SetProperty(ref _hasAppUser, value))
                {
                    OnPropertyChanged(nameof(UserAccountStatusText));
                    OnPropertyChanged(nameof(EffectiveRoleText));
                    OnPropertyChanged(nameof(PreparedRoleText));
                    OnPropertyChanged(nameof(HasPreparedRole));
                    AddUserCommand.RaiseCanExecuteChanged();
                    DeleteUserCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _hasAuthUserLink;
        public bool HasAuthUserLink
        {
            get => _hasAuthUserLink;
            private set
            {
                if (SetProperty(ref _hasAuthUserLink, value))
                {
                    OnPropertyChanged(nameof(UserAccountStatusText));
                    AddUserCommand.RaiseCanExecuteChanged();
                    DeleteUserCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public MemberDTO SelectedMember { get; }

        public ObservableCollection<string> Roles { get; } = new(UserRoles.All);

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
                    AddUserCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(EffectiveRoleText));
                    OnPropertyChanged(nameof(PreparedRoleText));
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

        public bool CanManageUsers => _authService.IsAdmin || _authService.IsVorstand;

        public string EmailStatusText => string.IsNullOrWhiteSpace(SelectedMember.Email) ? "fehlt" : "vorhanden";

        public string UserAccountStatusText => HasAppUser
            ? "vorhanden (app_user)"
            : HasAuthUserLink
                ? "eingeladen/verknüpft (mitglied.auth_user_id)"
                : "nicht vorhanden";

        public string EffectiveRoleText => HasAppUser
            ? $"{SelectedRole} (app_user.role)"
            : $"{SelectedRole} (mitglied.role)";

        public bool HasPreparedRole => !HasAppUser;

        public string PreparedRoleText => HasAppUser ? string.Empty : SelectedRole;

        public RelayCommand<object?> SaveCommand { get; }
        public RelayCommand<object?> OpenUserManagementCommand { get; }
        public RelayCommand<object?> AddUserCommand { get; }
        public RelayCommand<object?> DeleteUserCommand { get; }

        public AdminRoleViewModel(ISupabaseService supabaseService, IAuthService authService, MemberDTO member)
        {
            _supabaseService = supabaseService;
            _authService = authService;
            SelectedMember = member;

            SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanSave());

            OpenUserManagementCommand = new RelayCommand<object?>(
                _ => WeakReferenceMessenger.Default.Send(new NavigateToViewModelMessage(typeof(UserManagementViewModel))),
                _ => CanManageUsers);

            AddUserCommand = new RelayCommand<object?>(_ => _ = AddUserAsync(), _ => CanAddUser());

            DeleteUserCommand = new RelayCommand<object?>(_ => _ = DeleteUserAsync(), _ => CanDeleteUser());
        }

        public async Task OnNavigatedToAsync()
        {
            await LoadAsync();
            IsDirty = false;
            SaveCommand.RaiseCanExecuteChanged();
            AddUserCommand.RaiseCanExecuteChanged();
            DeleteUserCommand.RaiseCanExecuteChanged();
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
            SelectedMember.Email = rec.Email ?? string.Empty;
            SelectedMember.AuthUserId = rec.AuthUserId;
            // `rec.Role` wird in SupabaseService aus `app_user.role` überschrieben.
            SelectedMember.Role = rec.Role ?? string.Empty;

            SelectedRole = string.IsNullOrWhiteSpace(SelectedMember.Role) ? UserRoles.User : SelectedMember.Role;

            HasAppUser = await _supabaseService.HasAppUserForMitgliedAsync(SelectedMember.Id);
            HasAuthUserLink = SelectedMember.AuthUserId.HasValue;

            OnPropertyChanged(nameof(EmailStatusText));
            IsDirty = false;

            DeleteUserCommand.RaiseCanExecuteChanged();
        }

        private bool CanDeleteUser()
        {
            if (!CanManageUsers) return false;
            if (IsBusy) return false;

            if (SelectedMember == null) return false;
            if (SelectedMember.Id <= 0) return false;

            // Löschen nur möglich, wenn überhaupt ein Nutzerkonto/Link existiert.
            if (!HasAppUser && !HasAuthUserLink) return false;

            return true;
        }

        private bool CanAddUser()
        {
            if (!CanManageUsers) return false;
            if (IsBusy) return false;

            if (SelectedMember == null) return false;
            if (HasAppUser) return false;
            if (HasAuthUserLink) return false;

            if (string.IsNullOrWhiteSpace(SelectedMember.Email)) return false;

            var role = (SelectedRole ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(role)) return false;
            if (!UserRoles.All.Contains(role)) return false;

            return true;
        }

        private bool CanSave()
        {
            if (IsBusy)
                return false;

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
                IsBusy = true;

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

                bool ok;
                var hasAppUser = await _supabaseService.HasAppUserForMitgliedAsync(SelectedMember.Id);

                if (hasAppUser)
                {
                    ok = await _supabaseService.UpdateAppUserRoleForMitgliedAsync(SelectedMember.Id, SelectedRole);
                }
                else
                {
                    // Übergangs-Phase: Mitglied hat noch keinen app_user (noch kein Konto/Invite).
                    // Die vorbereitete Zielrolle bleibt in mitglied.role.
                    ok = await _supabaseService.UpdateMitgliedRoleForMitgliedAsync(SelectedMember.Id, SelectedRole, userId);
                }

                if (!ok)
                {
                    MessageBox.Show(
                        "Rolle konnte nicht gespeichert werden (ggf. Lock verloren oder keine Berechtigung).",
                        "Fehler",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                SelectedMember.Role = SelectedRole;
                IsDirty = false;
                SaveCommand.RaiseCanExecuteChanged();

                await _supabaseService.ReleaseLockMitgliedAsync(SelectedMember.Id, userId, force: false);
                _lockUserId = null;

                MessageBox.Show("Rolle gespeichert.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);

                // Status-Texte aktualisieren
                OnPropertyChanged(nameof(EffectiveRoleText));
                OnPropertyChanged(nameof(PreparedRoleText));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                SaveCommand.RaiseCanExecuteChanged();
                AddUserCommand.RaiseCanExecuteChanged();
                DeleteUserCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task DeleteUserAsync()
        {
            try
            {
                IsBusy = true;

                if (!CanManageUsers)
                {
                    MessageBox.Show("Keine Berechtigung für diese Aktion.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (SelectedMember == null || SelectedMember.Id <= 0)
                {
                    MessageBox.Show("Mitglied nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!HasAppUser && !HasAuthUserLink)
                {
                    MessageBox.Show("Für dieses Mitglied existiert kein Nutzerkonto.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirm = MessageBox.Show(
                    "Soll das Nutzerkonto für dieses Mitglied wirklich gelöscht werden?",
                    "Nutzer löschen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;

                var result = await _supabaseService.DeleteUserAccountForMitgliedAsync(SelectedMember.Id);

                switch (result.Outcome)
                {
                    case DeleteUserAccountOutcome.Deleted:
                        MessageBox.Show("Nutzerkonto wurde gelöscht.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;

                    case DeleteUserAccountOutcome.NoUserAccount:
                        MessageBox.Show("Für dieses Mitglied existiert kein Nutzerkonto.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;

                    case DeleteUserAccountOutcome.NotFound:
                        MessageBox.Show("Mitglied nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case DeleteUserAccountOutcome.Unauthorized:
                        MessageBox.Show("Keine Berechtigung für diese Aktion.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    default:
                        MessageBox.Show(result.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }

                // Status nach Erfolg/Fehlern neu laden (insb. app_user + mitglied.auth_user_id)
                await LoadAsync();

                // Schutz/Diagnose: Der Client löscht kein `mitglied`. Wenn der Datensatz danach fehlt,
                // wurde er sehr wahrscheinlich serverseitig entfernt (z.B. FK-CASCADE bei Auth-User-Delete).
                if (result.Outcome == DeleteUserAccountOutcome.Deleted)
                {
                    var stillThere = await _supabaseService.GetMitgliedByIdAsync(SelectedMember.Id);
                    if (stillThere == null)
                    {
                        MessageBox.Show(
                            "Warnung: Das Mitglied ist nach dem Löschen des Nutzerkontos nicht mehr auffindbar. " +
                            "Clientseitig wird kein Mitglied gelöscht – Ursache ist sehr wahrscheinlich serverseitig (z.B. Auth-User-Delete mit ON DELETE CASCADE auf `mitglied.auth_user_id`).",
                            "Warnung",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                AddUserCommand.RaiseCanExecuteChanged();
                DeleteUserCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task AddUserAsync()
        {
            try
            {
                IsBusy = true;

                // Doppelte Absicherung: auch bei deaktiviertem Button (z.B. via Tastatur) sauber prüfen.
                if (SelectedMember == null)
                {
                    MessageBox.Show("Kein Mitglied ausgewählt.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = await _supabaseService.PrepareAddUserForMitgliedAsync(SelectedMember.Id, SelectedRole);

                switch (result.Outcome)
                {
                    case PrepareAddUserOutcome.Ready:
                        var invite = await _supabaseService.InviteUserAccountForMitgliedAsync(result.MitgliedId, result.Role);

                        switch (invite.Outcome)
                        {
                            case InviteUserAccountOutcome.Invited:
                                MessageBox.Show("Einladungs-Mail wurde versendet.", "Nutzer hinzufügen", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                                break;

                            case InviteUserAccountOutcome.AlreadyLinked:
                            case InviteUserAccountOutcome.UserAlreadyExists:
                                MessageBox.Show("Für dieses Mitglied existiert bereits ein Nutzerkonto.", "Hinweis",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                break;

                            case InviteUserAccountOutcome.MissingEmail:
                                MessageBox.Show("Keine E-Mail-Adresse vorhanden.", "Hinweis", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                                break;

                            case InviteUserAccountOutcome.InvalidRole:
                                MessageBox.Show("Ungültige Rolle. Erlaubt: admin, vorstand, user.", "Hinweis", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                                break;

                            case InviteUserAccountOutcome.NotFound:
                                MessageBox.Show("Mitglied nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                                break;

                            case InviteUserAccountOutcome.Unauthorized:
                                MessageBox.Show("Keine Berechtigung für diese Aktion.", "Fehler", MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                break;

                            default:
                                MessageBox.Show(invite.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                                break;
                        }

                        // Nach einem Invite/Fehlern den Status neu laden (insb. mitglied.auth_user_id).
                        await LoadAsync();
                        break;

                    case PrepareAddUserOutcome.MissingEmail:
                        MessageBox.Show("Keine E-Mail-Adresse vorhanden.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;

                    case PrepareAddUserOutcome.UserAlreadyExists:
                        await LoadAsync();
                        MessageBox.Show("Für dieses Mitglied existiert bereits ein Nutzerkonto.", "Hinweis", MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        break;

                    case PrepareAddUserOutcome.InvalidRole:
                        MessageBox.Show("Ungültige Rolle. Erlaubt: admin, vorstand, user.", "Hinweis", MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        break;

                    case PrepareAddUserOutcome.NotFound:
                        MessageBox.Show("Mitglied nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    default:
                        MessageBox.Show(result.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                AddUserCommand.RaiseCanExecuteChanged();
                DeleteUserCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
