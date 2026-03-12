using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KGV.Core.Interfaces;
using KGV.Core.Models;

namespace KGV.Maui.ViewModels;

public sealed class MemberSearchViewModel : INotifyPropertyChanged
{
    private readonly ISupabaseService _supabaseService;

    private readonly List<MemberDTO> _allMembers = new();
    private readonly Dictionary<int, MemberDTO> _memberById = new();
    private readonly Dictionary<int, int?> _hauptmitgliedIdByMemberId = new();
    private readonly List<ParzelleRecord> _allParzellen = new();
    private readonly Dictionary<int, ParzellenBelegungRecord> _activeBelegungByParzelleId = new();

    public ObservableCollection<MemberSearchResultItem> Results { get; } = new();
    public ObservableCollection<string> DebugMessages { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
                return;

            _searchText = value;
            OnPropertyChanged();
            UpdateFilter();
        }
    }

    private bool _searchByParzelle;
    public bool SearchByParzelle
    {
        get => _searchByParzelle;
        set
        {
            if (_searchByParzelle == value)
                return;

            _searchByParzelle = value;
            OnPropertyChanged();

            _ = EnsureDataLoadedAsync();
            UpdateFilter();
        }
    }

    private bool _includeNebenmitglieder;
    public bool IncludeNebenmitglieder
    {
        get => _includeNebenmitglieder;
        set
        {
            if (_includeNebenmitglieder == value)
                return;

            _includeNebenmitglieder = value;
            OnPropertyChanged();
            UpdateFilter();
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public Command SearchCommand { get; }

    public MemberSearchViewModel(ISupabaseService supabaseService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        SearchCommand = new Command(UpdateFilter);
    }

    public async Task InitializeAsync()
    {
        await EnsureDataLoadedAsync();
        UpdateFilter();
    }

    public async Task<MemberDTO?> SelectResultAsync(MemberSearchResultItem? item)
    {
        if (item?.Model == null)
            return null;

        if (item.Model is MemberDTO md)
            return md;

        if (item.Model is ParzelleRecord pr)
        {
            try
            {
                IsBusy = true;
                var beleg = await _supabaseService.GetCurrentBelegungForParzelleAsync(pr.Id);
                if (beleg == null)
                    return null;

                var member = _allMembers.FirstOrDefault(m => m.Id == beleg.MitgliedId);
                if (member != null)
                    return member;

                // fallback (falls Members noch nicht geladen waren)
                await EnsureMembersLoadedAsync();
                return _allMembers.FirstOrDefault(m => m.Id == beleg.MitgliedId);
            }
            catch (Exception ex)
            {
                DebugMessages.Add($"❌ Fehler beim Laden der Belegung: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        return null;
    }

    private async Task EnsureDataLoadedAsync()
    {
        try
        {
            await EnsureMembersLoadedAsync();

            if (SearchByParzelle)
            {
                await EnsureParzellenLoadedAsync();
                await EnsureActiveBelegungenLoadedAsync();
            }
        }
        catch (Exception ex)
        {
            DebugMessages.Add($"❌ Fehler beim Laden: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            UpdateFilter();
        }
    }

    private async Task EnsureMembersLoadedAsync()
    {
        if (_allMembers.Count > 0)
            return;

        IsBusy = true;
        DebugMessages.Clear();
        DebugMessages.Add("⚡ Lade Mitglieder von Supabase...");

        var members = await _supabaseService.GetMitgliederAsync();
        foreach (var m in members)
        {
            var dto = MapToDTO(m);
            _allMembers.Add(dto);
            _memberById[dto.Id] = dto;
            _hauptmitgliedIdByMemberId[dto.Id] = m.HauptmitgliedId;
        }

        DebugMessages.Add($"✅ {_allMembers.Count} Mitglieder geladen.");
    }

    private async Task EnsureParzellenLoadedAsync()
    {
        if (_allParzellen.Count > 0)
            return;

        IsBusy = true;
        DebugMessages.Add("⚡ Lade Parzellen von Supabase...");

        var pars = await _supabaseService.GetAllParzellenAsync();
        _allParzellen.AddRange(pars);

        DebugMessages.Add($"✅ {_allParzellen.Count} Parzellen geladen.");
    }

    private async Task EnsureActiveBelegungenLoadedAsync()
    {
        if (_activeBelegungByParzelleId.Count > 0)
            return;

        IsBusy = true;
        DebugMessages.Add("⚡ Lade Belegungen von Supabase...");

        var allBelegungen = await _supabaseService.GetAllParzellenBelegungenAsync();
        var today = DateTime.Today;

        _activeBelegungByParzelleId.Clear();

        foreach (var grp in allBelegungen.GroupBy(b => b.ParzelleId))
        {
            var active = grp
                .Where(x => (x.VonDatum ?? DateTime.MinValue).Date <= today && (x.BisDatum == null || x.BisDatum.Value.Date >= today))
                .OrderByDescending(x => x.VonDatum ?? DateTime.MinValue)
                .FirstOrDefault();

            if (active != null)
                _activeBelegungByParzelleId[active.ParzelleId] = active;
        }

        DebugMessages.Add($"✅ {_activeBelegungByParzelleId.Count} aktive Belegungen geladen.");
    }

    private void UpdateFilter()
    {
        Results.Clear();

        var text = (SearchText ?? string.Empty).Trim();

        if (SearchByParzelle)
        {
            var matches = string.IsNullOrEmpty(text)
                ? _allParzellen
                : _allParzellen.Where(p =>
                    !string.IsNullOrWhiteSpace(p.GartenNr) &&
                    p.GartenNr.Contains(text, StringComparison.OrdinalIgnoreCase));

            foreach (var p in matches
                         .OrderBy(p => GetGartenNrSortKey(p.GartenNr))
                         .ThenBy(p => p.GartenNr, StringComparer.CurrentCultureIgnoreCase))
            {
                var pachterText = "(frei)";
                int? pachtHauptmitgliedId = null;

                if (_activeBelegungByParzelleId.TryGetValue(p.Id, out var beleg))
                {
                    var hmId = _hauptmitgliedIdByMemberId.TryGetValue(beleg.MitgliedId, out var tmp) ? tmp : null;
                    pachtHauptmitgliedId = hmId ?? beleg.MitgliedId;

                    if (pachtHauptmitgliedId.HasValue && _memberById.TryGetValue(pachtHauptmitgliedId.Value, out var pachter))
                        pachterText = FormatMemberTitle(pachter);
                    else
                        pachterText = $"#{pachtHauptmitgliedId}";
                }

                var subtitle = string.IsNullOrWhiteSpace(p.Anlage)
                    ? $"Pächter: {pachterText}"
                    : $"{p.Anlage} – Pächter: {pachterText}";

                Results.Add(new MemberSearchResultItem($"Garten {p.GartenNr}", subtitle, new ParzelleSearchResult(p, pachtHauptmitgliedId)));
            }

            return;
        }

        var memberMatches = string.IsNullOrEmpty(text)
            ? _allMembers.AsEnumerable()
            : _allMembers.Where(m => MatchesMemberSearch(m, text));

        if (!IncludeNebenmitglieder)
        {
            memberMatches = memberMatches.Where(m =>
                !_hauptmitgliedIdByMemberId.TryGetValue(m.Id, out var hmId) || hmId == null);
        }

        foreach (var m in memberMatches
                     .OrderBy(m => m.Nachname, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(m => m.Vorname, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(m => m.Email, StringComparer.CurrentCultureIgnoreCase))
        {
            var subtitle = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email;
            Results.Add(new MemberSearchResultItem(FormatMemberTitle(m), subtitle, m));
        }

        return;
    }

    public sealed record ParzelleSearchResult(ParzelleRecord Parzelle, int? PachtHauptmitgliedId);

    private static string FormatMemberTitle(MemberDTO m)
    {
        var nachname = (m.Nachname ?? string.Empty).Trim();
        var vorname = (m.Vorname ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(nachname) && !string.IsNullOrWhiteSpace(vorname))
            return $"{nachname}, {vorname}";

        if (!string.IsNullOrWhiteSpace(nachname))
            return nachname;

        if (!string.IsNullOrWhiteSpace(vorname))
            return vorname;

        return (m.Email ?? string.Empty).Trim();
    }

    private static bool MatchesMemberSearch(MemberDTO m, string text)
    {
        return
            (!string.IsNullOrEmpty(m.Vorname) && m.Vorname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(m.Nachname) && m.Nachname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(m.Email) && m.Email.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetGartenNrSortKey(string? gartenNr)
    {
        if (string.IsNullOrWhiteSpace(gartenNr))
            return int.MaxValue;

        var digits = new string(gartenNr.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : int.MaxValue;
    }

    private static MemberDTO MapToDTO(MitgliedRecord m)
    {
        return new MemberDTO
        {
            Id = m.Id,
            Vorname = m.Vorname ?? string.Empty,
            Nachname = m.Name ?? string.Empty,
            Email = m.Email ?? string.Empty,
            Role = m.Role ?? string.Empty
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class MemberSearchResultItem
{
    public MemberSearchResultItem(string title, string? subtitle, object model)
    {
        Title = title;
        Subtitle = subtitle;
        Model = model;
    }

    public string Title { get; }
    public string? Subtitle { get; }

    public object Model { get; }
}
