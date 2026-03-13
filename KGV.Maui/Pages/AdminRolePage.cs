using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Maui.State;
using System.Linq;

namespace KGV.Maui.Pages;

public sealed class AdminRolePage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IAuthService _authService;
    private readonly MemberSelectionState _memberSelection;

    private bool _isBusy;
    private Task? _initTask;

    private int? _loadedMemberId;
    private bool _hasAppUser;
    private bool _hasAuthUserLink;

    private MitgliedRecord? _member;

    private readonly ActivityIndicator _busy;
    private readonly Label _title;
    private readonly Label _memberInfo;
    private readonly Label _accountStatus;
    private readonly Label _effectiveRole;
    private readonly Label _status;

    private readonly Picker _rolePicker;
    private readonly Button _saveRoleButton;
    private readonly Button _inviteButton;
    private readonly Button _deleteButton;
    private readonly Button _userManagementButton;
    private readonly Button _goToSearchButton;

    public AdminRolePage(ISupabaseService supabaseService, IAuthService authService, MemberSelectionState memberSelection)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _memberSelection = memberSelection ?? throw new ArgumentNullException(nameof(memberSelection));

        Title = "Rollen / Benutzer";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };

        _title = new Label { Text = "Rollen / Benutzer", FontSize = 22, FontAttributes = FontAttributes.Bold };
        _memberInfo = new Label { Opacity = 0.8 };
        _accountStatus = new Label { Opacity = 0.8 };
        _effectiveRole = new Label { Opacity = 0.8 };

        _status = new Label { TextColor = Colors.Red };

        _rolePicker = new Picker { Title = "Rolle" };
        _rolePicker.ItemsSource = UserRoles.All.ToList();
        _rolePicker.SelectedIndexChanged += (_, __) => UpdateUiState();

        _saveRoleButton = new Button { Text = "Rolle speichern" };
        _saveRoleButton.Clicked += async (_, __) => await SaveRoleAsync();

        _inviteButton = new Button { Text = "Nutzerkonto einladen" };
        _inviteButton.Clicked += async (_, __) => await InviteAsync();

        _deleteButton = new Button { Text = "Nutzerkonto löschen" };
        _deleteButton.Clicked += async (_, __) => await DeleteAsync();

        _userManagementButton = new Button { Text = "Benutzerverwaltung" };
        _userManagementButton.Clicked += async (_, __) => await Shell.Current.GoToAsync("//usermanagement");

        _goToSearchButton = new Button { Text = "Zur Mitgliedersuche" };
        _goToSearchButton.Clicked += async (_, __) => await Shell.Current.GoToAsync("//membersearch");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    _title,
                    _busy,
                    _memberInfo,
                    _accountStatus,
                    _effectiveRole,
                    _rolePicker,
                    new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { _saveRoleButton, _inviteButton, _deleteButton, _userManagementButton }
                    },
                    _goToSearchButton,
                    _status
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += (_, _) => _status.Text = string.Empty;

        UpdateUiState();
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureInitializedAsync();
    }

    private Task EnsureInitializedAsync()
    {
        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = LoadAsync();
        return _initTask;
    }

    private async Task LoadAsync()
    {
        if (_isBusy)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var memberId = _memberSelection.SelectedMitgliedId;
            if (!memberId.HasValue)
            {
                ClearUi("Bitte erst ein Mitglied auswählen.");
                return;
            }

            // defensiv: bei erneutem Öffnen immer reloaden, um Account-/Role-Status aktuell zu halten
            _loadedMemberId = memberId.Value;

            var rec = await _supabaseService.GetMitgliedByIdAsync(memberId.Value);
            if (rec == null)
            {
                _member = null;
                ClearUi($"Mitglied nicht gefunden (Id={memberId.Value}).");
                return;
            }

            _member = rec;
            _hasAuthUserLink = rec.AuthUserId.HasValue;
            _hasAppUser = await _supabaseService.HasAppUserForMitgliedAsync(rec.Id);

            var role = (rec.Role ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(role))
                role = UserRoles.User;

            _rolePicker.SelectedItem = UserRoles.All.Contains(role) ? role : UserRoles.User;

            var displayName = $"{rec.Vorname} {rec.Name}".Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = $"Mitglied #{rec.Id}";

            _memberInfo.Text = $"{displayName}  (#{rec.Id})\n{(rec.Email ?? string.Empty)}";
            _accountStatus.Text = "Konto: " + (_hasAppUser
                ? "vorhanden (app_user)"
                : _hasAuthUserLink
                    ? "eingeladen/verknüpft (mitglied.auth_user_id)"
                    : "nicht vorhanden");

            _effectiveRole.Text = "Rolle: " + (_hasAppUser ? $"{_rolePicker.SelectedItem} (app_user.role)" : $"{_rolePicker.SelectedItem} (mitglied.role)");
        }
        catch (Exception ex)
        {
            ClearUi(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SaveRoleAsync()
    {
        if (_isBusy)
            return;

        if (!_authService.IsAdmin)
        {
            _status.Text = "Keine Berechtigung (Admin erforderlich).";
            return;
        }

        if (_member == null)
        {
            _status.Text = "Kein Mitglied geladen.";
            return;
        }

        if (_member.Id == 7)
        {
            _status.Text = "Für dieses Mitglied ist die Rollenbearbeitung gesperrt.";
            return;
        }

        var userId = _authService.CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            _status.Text = "Nicht angemeldet.";
            return;
        }

        var role = (_rolePicker.SelectedItem as string ?? UserRoles.User).Trim().ToLowerInvariant();
        if (!UserRoles.All.Contains(role))
        {
            _status.Text = "Rolle ist ungültig.";
            return;
        }

        SetBusy(true);
        try
        {
            var locked = await _supabaseService.TryLockMitgliedAsync(_member.Id, userId);
            if (!locked)
            {
                _status.Text = "Datensatz ist gesperrt (wird gerade bearbeitet).";
                return;
            }

            bool ok;
            var hasAppUser = await _supabaseService.HasAppUserForMitgliedAsync(_member.Id);
            if (hasAppUser)
            {
                ok = await _supabaseService.UpdateAppUserRoleForMitgliedAsync(_member.Id, role);
            }
            else
            {
                ok = await _supabaseService.UpdateMitgliedRoleForMitgliedAsync(_member.Id, role, userId);
            }

            if (!ok)
            {
                _status.Text = "Rolle konnte nicht gespeichert werden.";
                return;
            }

            await DisplayAlert("OK", "Rolle gespeichert.", "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            try
            {
                await _supabaseService.ReleaseLockMitgliedAsync(_member.Id, userId, force: false);
            }
            catch
            {
            }

            SetBusy(false);
        }
    }

    private async Task InviteAsync()
    {
        if (_isBusy)
            return;

        if (!_authService.IsAdmin && !_authService.IsVorstand)
        {
            _status.Text = "Keine Berechtigung.";
            return;
        }

        if (_member == null)
        {
            _status.Text = "Kein Mitglied geladen.";
            return;
        }

        var role = (_rolePicker.SelectedItem as string ?? UserRoles.User).Trim().ToLowerInvariant();
        if (!UserRoles.All.Contains(role))
        {
            _status.Text = "Rolle ist ungültig.";
            return;
        }

        SetBusy(true);
        try
        {
            var prep = await _supabaseService.PrepareAddUserForMitgliedAsync(_member.Id, role);
            if (prep.Outcome != PrepareAddUserOutcome.Ready)
            {
                await DisplayAlert("Hinweis", prep.Message, "OK");
                return;
            }

            var res = await _supabaseService.InviteUserAccountForMitgliedAsync(_member.Id, role);
            if (!res.Success)
            {
                await DisplayAlert("Fehler", res.Message, "OK");
                return;
            }

            await DisplayAlert("OK", res.Message, "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DeleteAsync()
    {
        if (_isBusy)
            return;

        if (!_authService.IsAdmin && !_authService.IsVorstand)
        {
            _status.Text = "Keine Berechtigung.";
            return;
        }

        if (_member == null)
        {
            _status.Text = "Kein Mitglied geladen.";
            return;
        }

        var okConfirm = await DisplayAlert("Bestätigung", "Nutzerkonto wirklich löschen?", "Ja", "Nein");
        if (!okConfirm)
            return;

        SetBusy(true);
        try
        {
            var res = await _supabaseService.DeleteUserAccountForMitgliedAsync(_member.Id);
            if (!res.Success)
            {
                await DisplayAlert("Fehler", res.Message, "OK");
                return;
            }

            await DisplayAlert("OK", res.Message, "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var hasMember = _member != null;
        var canUse = !_isBusy && hasMember;

        _rolePicker.IsEnabled = canUse;
        _saveRoleButton.IsEnabled = canUse && _authService.IsAdmin && _member?.Id != 7;

        // Einladen nur, wenn noch kein Konto/Link existiert und eine E-Mail vorhanden ist
        var canInvite = canUse
            && (_authService.IsAdmin || _authService.IsVorstand)
            && !_hasAppUser
            && !_hasAuthUserLink
            && !string.IsNullOrWhiteSpace(_member?.Email);

        _inviteButton.IsEnabled = canInvite;

        var canDelete = canUse
            && (_authService.IsAdmin || _authService.IsVorstand)
            && (_hasAppUser || _hasAuthUserLink);

        _deleteButton.IsEnabled = canDelete;

        _goToSearchButton.IsEnabled = !_isBusy;

        _effectiveRole.Text = hasMember
            ? "Rolle: " + (_hasAppUser ? $"{_rolePicker.SelectedItem} (app_user.role)" : $"{_rolePicker.SelectedItem} (mitglied.role)")
            : string.Empty;
    }

    private void ClearUi(string message)
    {
        _member = null;
        _loadedMemberId = null;
        _hasAppUser = false;
        _hasAuthUserLink = false;

        _memberInfo.Text = string.Empty;
        _accountStatus.Text = string.Empty;
        _effectiveRole.Text = string.Empty;

        _rolePicker.SelectedItem = UserRoles.User;
        _status.Text = message;
        UpdateUiState();
    }
}
