using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;

namespace KGV.Wpf.ViewModels
{
    public sealed class UserManagementViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly IAuthService _authService;
        private readonly MainWindowViewModel _mainVm;

        private bool _isInitialized;

        public ObservableCollection<AppUserAccountItem> Results { get; } = new();
        private readonly List<AppUserAccountItem> _all = new();

        public bool CanManageUsers => _mainVm.UserContext.Has(PermissionFlags.CanManageUsers);

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                    UpdateFilter();
            }
        }

        private AppUserAccountItem? _selected;
        public AppUserAccountItem? Selected
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value))
                {
                    UpdateSelectedUserStatus();
                }
            }
        }

        private string _contextMemberTitle = "Admin-Kontext: kein Mitglied ausgewählt.";
        public string ContextMemberTitle
        {
            get => _contextMemberTitle;
            private set => SetProperty(ref _contextMemberTitle, value ?? string.Empty);
        }

        private string _contextMemberEmail = string.Empty;
        public string ContextMemberEmail
        {
            get => _contextMemberEmail;
            private set => SetProperty(ref _contextMemberEmail, value ?? string.Empty);
        }

        private string _contextMemberAccountStatus = string.Empty;
        public string ContextMemberAccountStatus
        {
            get => _contextMemberAccountStatus;
            private set => SetProperty(ref _contextMemberAccountStatus, value ?? string.Empty);
        }

        private string _selectedUserTitle = "Kein Nutzerkonto ausgewählt.";
        public string SelectedUserTitle
        {
            get => _selectedUserTitle;
            private set => SetProperty(ref _selectedUserTitle, value ?? string.Empty);
        }

        private string _selectedUserEmail = string.Empty;
        public string SelectedUserEmail
        {
            get => _selectedUserEmail;
            private set => SetProperty(ref _selectedUserEmail, value ?? string.Empty);
        }

        private string _selectedUserId = string.Empty;
        public string SelectedUserId
        {
            get => _selectedUserId;
            private set => SetProperty(ref _selectedUserId, value ?? string.Empty);
        }

        private string _selectedUserRole = string.Empty;
        public string SelectedUserRole
        {
            get => _selectedUserRole;
            private set => SetProperty(ref _selectedUserRole, value ?? string.Empty);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                    RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value ?? string.Empty);
        }

        public RelayCommand<object?> RefreshCommand { get; }

        public UserManagementViewModel(ISupabaseService supabaseService, IAuthService authService, MainWindowViewModel mainVm)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

            RefreshCommand = new RelayCommand<object?>(_ => _ = RefreshAsync(), _ => !IsBusy);
        }

        public async Task OnNavigatedToAsync()
        {
            _mainVm.PropertyChanged -= OnMainVmPropertyChanged;
            _mainVm.PropertyChanged += OnMainVmPropertyChanged;

            if (_isInitialized) return;
            _isInitialized = true;

            await RefreshAsync();
        }

        public Task OnNavigatedFromAsync()
        {
            _mainVm.PropertyChanged -= OnMainVmPropertyChanged;
            return Task.CompletedTask;
        }

        private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedMember))
                UpdateContextMemberStatus();
        }

        private void UpdateSelectedUserStatus()
        {
            if (Selected == null)
            {
                SelectedUserTitle = "Kein Nutzerkonto ausgewählt.";
                SelectedUserEmail = string.Empty;
                SelectedUserId = string.Empty;
                SelectedUserRole = string.Empty;
                return;
            }

            SelectedUserTitle = $"Nutzerkonto für {Selected.MitgliedDisplay}";
            SelectedUserEmail = Selected.Email;
            SelectedUserId = Selected.UserId.ToString();
            SelectedUserRole = Selected.Role;
        }

        private void UpdateContextMemberStatus()
        {
            var m = _mainVm.SelectedMember;
            if (m == null)
            {
                ContextMemberTitle = "Admin-Kontext: kein Mitglied ausgewählt.";
                ContextMemberEmail = string.Empty;
                ContextMemberAccountStatus = string.Empty;
                return;
            }

            ContextMemberTitle = $"Admin-Kontext: {m.DisplayName} (#{m.Id})";
            ContextMemberEmail = m.Email ?? string.Empty;

            var item = _all.FirstOrDefault(x => x.MitgliedId == m.Id);
            ContextMemberAccountStatus = item == null
                ? "Kein Nutzerkonto (kein Eintrag in app_user)"
                : $"Nutzerkonto vorhanden (Rolle: {item.Role})";
        }

        private async Task RefreshAsync()
        {
            if (!CanManageUsers)
            {
                StatusMessage = "Keine Berechtigung.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Lade Nutzerkonten...";

            try
            {
                Results.Clear();
                _all.Clear();

                var appUsers = await _supabaseService.GetAppUsersAsync();

                // Mitgliedsdaten ergänzend laden (Name/E-Mail), damit die Liste verständlich bleibt.
                var members = await _supabaseService.GetMitgliederAsync();
                var memberById = members.ToDictionary(m => (long)m.Id, m => m);

                foreach (var u in appUsers
                             .OrderBy(x => x.Role ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(x => x.MitgliedId ?? long.MaxValue))
                {
                    memberById.TryGetValue(u.MitgliedId ?? -1, out var m);

                    _all.Add(new AppUserAccountItem
                    {
                        UserId = u.UserId,
                        MitgliedId = u.MitgliedId,
                        Role = (u.Role ?? string.Empty).Trim().ToLowerInvariant(),
                        Vorname = m?.Vorname ?? string.Empty,
                        Nachname = m?.Name ?? string.Empty,
                        Email = m?.Email ?? string.Empty
                    });
                }

                UpdateFilter();
                StatusMessage = $"App-Nutzer: {_all.Count}";
                UpdateContextMemberStatus();
                UpdateSelectedUserStatus();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateFilter()
        {
            Results.Clear();

            var text = (SearchText ?? string.Empty).Trim();
            var filtered = string.IsNullOrWhiteSpace(text)
                ? _all
                : _all.Where(m =>
                    (!string.IsNullOrWhiteSpace(m.Nachname) && m.Nachname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(m.Vorname) && m.Vorname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(m.Email) && m.Email.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (m.MitgliedId?.ToString() ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    m.UserId.ToString().Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(m.Role) && m.Role.Contains(text, StringComparison.OrdinalIgnoreCase)));

            foreach (var m in filtered
                         .OrderBy(x => x.Nachname, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(x => x.Vorname, StringComparer.CurrentCultureIgnoreCase))
                Results.Add(m);
        }

        public sealed class AppUserAccountItem : BaseViewModel
        {
            private Guid _userId;
            public Guid UserId
            {
                get => _userId;
                set => SetProperty(ref _userId, value);
            }

            private long? _mitgliedId;
            public long? MitgliedId
            {
                get => _mitgliedId;
                set => SetProperty(ref _mitgliedId, value);
            }

            public string MitgliedDisplay
            {
                get
                {
                    var name = $"{Nachname} {Vorname}".Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        return MitgliedId.HasValue ? $"{name} (#{MitgliedId.Value})" : name;

                    return MitgliedId.HasValue ? $"Mitglied #{MitgliedId.Value}" : "(Mitglied nicht verknüpft)";
                }
            }

            private string _vorname = string.Empty;
            public string Vorname
            {
                get => _vorname;
                set
                {
                    if (SetProperty(ref _vorname, value ?? string.Empty))
                        OnPropertyChanged(nameof(MitgliedDisplay));
                }
            }

            private string _nachname = string.Empty;
            public string Nachname
            {
                get => _nachname;
                set
                {
                    if (SetProperty(ref _nachname, value ?? string.Empty))
                        OnPropertyChanged(nameof(MitgliedDisplay));
                }
            }

            private string _email = string.Empty;
            public string Email
            {
                get => _email;
                set
                {
                    SetProperty(ref _email, value ?? string.Empty);
                }
            }

            private string _role = UserRoles.User;
            public string Role
            {
                get => _role;
                set => SetProperty(ref _role, (value ?? UserRoles.User).Trim().ToLowerInvariant());
            }
        }
    }
}
