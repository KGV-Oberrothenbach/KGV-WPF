// Datei: KGV.Wpf/ViewModels/MemberSearchViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Messages;

namespace KGV.Wpf.ViewModels
{
    public class MemberSearchViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly MainWindowViewModel _mainVm;

        public ObservableCollection<MemberSearchResultRow> Results { get; } = new();
        public ObservableCollection<string> DebugMessages { get; } = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                UpdateFilter();
            }
        }

        private bool _searchByParzelle;
        public bool SearchByParzelle
        {
            get => _searchByParzelle;
            set
            {
                if (_searchByParzelle == value) return;
                _searchByParzelle = value;
                OnPropertyChanged(nameof(SearchByParzelle));
                OnPropertyChanged(nameof(Column1Header));
                OnPropertyChanged(nameof(Column2Header));
                OnPropertyChanged(nameof(Column3Header));
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
                if (_includeNebenmitglieder == value) return;
                _includeNebenmitglieder = value;
                OnPropertyChanged(nameof(IncludeNebenmitglieder));
                UpdateFilter();
            }
        }

        public string Column1Header => SearchByParzelle ? "Parzelle" : "Nachname";
        public string Column2Header => SearchByParzelle ? "Pächter" : "Vorname";
        public string Column3Header => SearchByParzelle ? "Anlage" : "E-Mail";

        public ICommand SearchCommand { get; }
        public ICommand SelectCommand { get; }

        private MemberSearchResultRow? _selectedResult;
        public MemberSearchResultRow? SelectedResult
        {
            get => _selectedResult;
            set
            {
                if (_selectedResult == value) return;
                _selectedResult = value;
                OnPropertyChanged(nameof(SelectedResult));
            }
        }

        private readonly System.Collections.Generic.List<MemberDTO> _allMembers;
        private readonly System.Collections.Generic.Dictionary<int, MemberDTO> _memberById;
        private readonly System.Collections.Generic.Dictionary<int, int?> _hauptmitgliedIdByMemberId;
        private readonly System.Collections.Generic.List<ParzelleRecord> _allParzellen;
        private readonly System.Collections.Generic.Dictionary<int, ParzellenBelegungRecord> _activeBelegungByParzelleId;

        private bool _isInitialized;

        public MemberSearchViewModel(ISupabaseService supabaseService, MainWindowViewModel mainVm)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

            SearchCommand = new KGV.Wpf.Helpers.RelayCommand<object?>(_ => UpdateFilter());
            SelectCommand = new KGV.Wpf.Helpers.RelayCommand<object?>(_ => _ = SelectResultAsync(SelectedResult));

            _allMembers = new System.Collections.Generic.List<MemberDTO>();
            _memberById = new System.Collections.Generic.Dictionary<int, MemberDTO>();
            _hauptmitgliedIdByMemberId = new System.Collections.Generic.Dictionary<int, int?>();
            _allParzellen = new System.Collections.Generic.List<ParzelleRecord>();
            _activeBelegungByParzelleId = new System.Collections.Generic.Dictionary<int, ParzellenBelegungRecord>();

            WeakReferenceMessenger.Default.Register<MemberSearchViewModel, MemberSavedMessage>(
                this,
                (r, m) => _ = r.HandleMemberSavedAsync(m));
        }

        public async Task OnNavigatedToAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            await InitializeAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            await EnsureDataLoadedAsync();
            UpdateFilter();
        }

        private async Task HandleMemberSavedAsync(MemberSavedMessage message)
        {
            if (message?.Member == null) return;

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(() => ApplySavedMember(message.Member));
                return;
            }

            ApplySavedMember(message.Member);
        }

        private void ApplySavedMember(MemberDTO saved)
        {
            if (SearchByParzelle) return;

            var existing = _allMembers.FirstOrDefault(m => m.Id == saved.Id);
            if (existing == null)
            {
                existing = saved.Clone();
                _allMembers.Add(existing);
            }
            else
            {
                existing.CopyFrom(saved);
            }

            _memberById[existing.Id] = existing;

            UpdateFilter();
        }

        private async Task EnsureDataLoadedAsync()
        {
            DebugMessages.Clear();
            DebugMessages.Add("⚡ Lade Mitglieder von Supabase...");

            try
            {
                if (_allMembers.Count == 0)
                {
                    var members = await _supabaseService.GetMitgliederAsync();
                    if (members == null)
                    {
                        DebugMessages.Add("❌ SupabaseService.GetMitgliederAsync() gab null zurück!");
                        return;
                    }

                    foreach (var m in members)
                    {
                        var dto = MapToDTO(m);
                        _allMembers.Add(dto);
                        _memberById[dto.Id] = dto;
                        _hauptmitgliedIdByMemberId[dto.Id] = m.HauptmitgliedId;
                    }

                    DebugMessages.Add($"✅ Mitglieder geladen: {_allMembers.Count}");
                }
                else
                {
                    DebugMessages.Add($"✅ Mitglieder bereits im Cache: {_allMembers.Count}");
                }
            }
            catch (Exception ex)
            {
                DebugMessages.Add($"❌ Fehler beim Laden der Mitglieder: {ex.Message}");
            }

            if (SearchByParzelle && _allParzellen.Count == 0)
            {
                try
                {
                    var pars = await _supabaseService.GetAllParzellenAsync();
                    if (pars != null)
                    {
                        _allParzellen.AddRange(pars);
                        DebugMessages.Add($"⚡ Parzellen geladen: {_allParzellen.Count}");
                    }
                }
                catch (Exception ex)
                {
                    DebugMessages.Add($"❌ Fehler beim Laden der Parzellen: {ex.Message}");
                }
            }

            if (SearchByParzelle && _activeBelegungByParzelleId.Count == 0)
            {
                try
                {
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

                    DebugMessages.Add($"⚡ Belegungen geladen: {_activeBelegungByParzelleId.Count} aktiv");
                }
                catch (Exception ex)
                {
                    DebugMessages.Add($"❌ Fehler beim Laden der Belegungen: {ex.Message}");
                }
            }

            UpdateFilter();
        }

        private void UpdateFilter()
        {
            Results.Clear();
            var text = (SearchText ?? string.Empty).Trim();

            if (SearchByParzelle)
            {
                var matches = string.IsNullOrEmpty(text)
                    ? _allParzellen
                    : _allParzellen.Where(p => !string.IsNullOrEmpty(p.GartenNr) &&
                                               p.GartenNr.Contains(text, StringComparison.OrdinalIgnoreCase));

                foreach (var p in matches
                             .OrderBy(p => GetGartenNrSortKey(p.GartenNr))
                             .ThenBy(p => p.GartenNr, StringComparer.CurrentCultureIgnoreCase))
                {
                    var pachterText = "(frei)";
                    int? pachtHauptmitgliedId = null;

                    if (_activeBelegungByParzelleId.TryGetValue(p.Id, out var beleg))
                    {
                        var belegMitgliedId = beleg.MitgliedId;
                        var hmId = _hauptmitgliedIdByMemberId.TryGetValue(belegMitgliedId, out var tmp) ? tmp : null;
                        pachtHauptmitgliedId = hmId ?? belegMitgliedId;

                        if (pachtHauptmitgliedId.HasValue && _memberById.TryGetValue(pachtHauptmitgliedId.Value, out var pachter))
                            pachterText = FormatMemberTitle(pachter);
                        else
                            pachterText = $"#{pachtHauptmitgliedId}";
                    }

                    Results.Add(new MemberSearchResultRow(
                        col1: p.GartenNr ?? $"#{p.Id}",
                        col2: pachterText,
                        col3: p.Anlage ?? string.Empty,
                        model: new ParzelleSearchResult(p, pachtHauptmitgliedId)));
                }

                return;
            }

            var members = _allMembers.AsEnumerable();
            if (!IncludeNebenmitglieder)
            {
                members = members.Where(m =>
                    !_hauptmitgliedIdByMemberId.TryGetValue(m.Id, out var hmId) || hmId == null);
            }

            var memberMatches = string.IsNullOrEmpty(text)
                ? members
                : members.Where(m => MatchesMemberSearch(m, text));

            foreach (var m in memberMatches
                         .OrderBy(m => m.Nachname, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(m => m.Vorname, StringComparer.CurrentCultureIgnoreCase))
            {
                Results.Add(new MemberSearchResultRow(
                    col1: m.Nachname,
                    col2: m.Vorname,
                    col3: m.Email,
                    model: m));
            }
        }

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
            if (string.IsNullOrWhiteSpace(text)) return true;

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
                Role = m.Role ?? string.Empty,
                    AuthUserId = m.AuthUserId,
                    ArbeitsstundenAltersregelTyp = m.ArbeitsstundenAltersregelTyp ?? "keine"
            };
        }

        private async Task SelectResultAsync(object? result)
        {
            if (result is not MemberSearchResultRow row)
                return;

            MemberDTO? selected = null;

            if (row.Model is MemberDTO md)
            {
                selected = md;
            }
            else if (row.Model is ParzelleSearchResult pr)
            {
                var hauptmitgliedId = pr.PachtHauptmitgliedId;
                if (hauptmitgliedId.HasValue && _memberById.TryGetValue(hauptmitgliedId.Value, out var dto))
                    selected = dto;
            }

            if (selected == null) return;

            var memberForDetail = selected.Clone();
            _mainVm.SelectedMember = memberForDetail;
        }

        public sealed record ParzelleSearchResult(ParzelleRecord Parzelle, int? PachtHauptmitgliedId);

        public sealed class MemberSearchResultRow
        {
            public MemberSearchResultRow(string col1, string col2, string col3, object model)
            {
                Col1 = col1;
                Col2 = col2;
                Col3 = col3;
                Model = model;
            }

            public string Col1 { get; }
            public string Col2 { get; }
            public string Col3 { get; }
            public object Model { get; }
        }
    }
}