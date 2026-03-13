using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public sealed class UserManagementPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly MemberSelectionState _memberSelection;

    private bool _isBusy;
    private Task? _initTask;

    private readonly ActivityIndicator _busy;
    private readonly Label _status;
    private readonly Entry _search;
    private readonly Button _reloadButton;
    private readonly CollectionView _list;

    private readonly List<AppUserAccountItem> _all = new();
    private readonly List<AppUserAccountItem> _filtered = new();

    public UserManagementPage(ISupabaseService supabaseService, MemberSelectionState memberSelection)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _memberSelection = memberSelection ?? throw new ArgumentNullException(nameof(memberSelection));

        Title = "Benutzerverwaltung";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Gray };

        _search = new Entry { Placeholder = "Suche (Name, Mail, Rolle, MitgliedId, UserId)" };
        _search.TextChanged += (_, __) => UpdateFilter();

        _reloadButton = new Button { Text = "Neu laden" };
        _reloadButton.Clicked += async (_, __) => await EnsureInitializedAsync();

        _list = new CollectionView
        {
            ItemsSource = _filtered,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(AppUserAccountItem.Title));

                var sub = new Label { FontSize = 12, TextColor = Colors.Gray };
                sub.SetBinding(Label.TextProperty, nameof(AppUserAccountItem.Subtitle));

                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 8),
                    Spacing = 2,
                    Children = { title, sub, new BoxView { HeightRequest = 1, Color = Colors.LightGray } }
                };
            })
        };
        _list.SelectionChanged += OnSelected;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    _reloadButton,
                    _busy,
                    _search,
                    _status,
                    _list
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
        SetStatus("Lade Nutzerkonten...", isError: false);

        try
        {
            _all.Clear();
            _filtered.Clear();

            var appUsers = await _supabaseService.GetAppUsersAsync();
            var members = await _supabaseService.GetMitgliederAsync();
            var memberById = members.ToDictionary(m => (long)m.Id, m => m);

            foreach (var u in appUsers
                         .OrderBy(x => x.Role ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(x => x.MitgliedId ?? long.MaxValue))
            {
                memberById.TryGetValue(u.MitgliedId ?? -1, out var m);

                _all.Add(new AppUserAccountItem(
                    u.UserId,
                    u.MitgliedId,
                    (u.Role ?? string.Empty).Trim().ToLowerInvariant(),
                    m?.Vorname ?? string.Empty,
                    m?.Name ?? string.Empty,
                    m?.Email ?? string.Empty));
            }

            UpdateFilter();
            SetStatus($"App-Nutzer: {_all.Count}", isError: false);
        }
        catch (Exception ex)
        {
            _all.Clear();
            _filtered.Clear();
            UpdateList();
            SetStatus($"Fehler: {ex.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_isBusy)
        {
            _list.SelectedItem = null;
            return;
        }

        if (e.CurrentSelection?.FirstOrDefault() is not AppUserAccountItem item)
            return;

        _list.SelectedItem = null;

        if (!item.MitgliedId.HasValue || item.MitgliedId.Value <= 0 || item.MitgliedId.Value > int.MaxValue)
        {
            await DisplayAlert("Hinweis", "Dieser app_user ist keinem Mitglied zugeordnet.", "OK");
            return;
        }

        _memberSelection.SelectedMitgliedId = (int)item.MitgliedId.Value;
        await Shell.Current.GoToAsync("//adminrole");
    }

    private void UpdateFilter()
    {
        _filtered.Clear();

        var text = (_search.Text ?? string.Empty).Trim();
        IEnumerable<AppUserAccountItem> source = _all;

        if (!string.IsNullOrWhiteSpace(text))
        {
            source = source.Where(m =>
                (m.MitgliedId?.ToString() ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase) ||
                m.UserId.ToString().Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(m.Role) && m.Role.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(m.Vorname) && m.Vorname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(m.Nachname) && m.Nachname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(m.Email) && m.Email.Contains(text, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var m in source
                     .OrderBy(x => x.Nachname, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(x => x.Vorname, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(x => x.Role, StringComparer.CurrentCultureIgnoreCase))
            _filtered.Add(m);

        UpdateList();

        if (!_isBusy && _filtered.Count == 0)
            SetStatus("Keine Treffer.", isError: false);
    }

    private void UpdateList()
    {
        _list.ItemsSource = null;
        _list.ItemsSource = _filtered;
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
        _reloadButton.IsEnabled = !_isBusy;
        _search.IsEnabled = !_isBusy;
        _list.IsEnabled = !_isBusy;
    }

    private void SetStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.TextColor = isError ? Colors.Red : Colors.Gray;
    }

    private sealed record AppUserAccountItem(Guid UserId, long? MitgliedId, string Role, string Vorname, string Nachname, string Email)
    {
        public string Title
        {
            get
            {
                var name = $"{Nachname} {Vorname}".Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    return MitgliedId.HasValue ? $"{name} (#{MitgliedId.Value})" : name;

                return MitgliedId.HasValue ? $"Mitglied #{MitgliedId.Value}" : "(Mitglied nicht verknüpft)";
            }
        }

        public string Subtitle
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Email)) parts.Add(Email);
                if (!string.IsNullOrWhiteSpace(Role)) parts.Add($"Rolle: {Role}");
                parts.Add($"UserId: {UserId}");
                return string.Join(" • ", parts);
            }
        }
    }
}
