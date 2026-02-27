using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using KGV.Core.Interfaces;
using KGV.Core.Models;

namespace KGV.ViewModels
{
    public class MemberSearchViewModel : BaseViewModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly MainWindowViewModel _mainVm;

        public ObservableCollection<object> Results { get; } = new();
        public ObservableCollection<string> DebugMessages { get; } = new(); // ⚡ Feedback

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

        private bool _searchByParzelle = false;
        public bool SearchByParzelle
        {
            get => _searchByParzelle;
            set
            {
                if (_searchByParzelle == value) return;
                _searchByParzelle = value;
                OnPropertyChanged(nameof(SearchByParzelle));
                _ = EnsureDataLoadedAsync();
                UpdateFilter();
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand SelectCommand { get; }

        private object? _selectedResult;
        public object? SelectedResult
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
        private readonly System.Collections.Generic.List<ParzelleRecord> _allParzellen;

        public MemberSearchViewModel(ISupabaseService supabaseService, MainWindowViewModel mainVm)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

            SearchCommand = new KGV.Helpers.RelayCommand<object?>(_ => UpdateFilter());

            // Wichtig: kein async void mehr -> wir feuern eine Task sauber "fire-and-forget" ab.
            SelectCommand = new KGV.Helpers.RelayCommand<object?>(_ => _ = SelectResultAsync(SelectedResult));

            _allMembers = new System.Collections.Generic.List<MemberDTO>();
            _allParzellen = new System.Collections.Generic.List<ParzelleRecord>();
        }

        public async Task InitializeAsync()
        {
            await EnsureDataLoadedAsync();
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

                    if (members.Count == 0)
                        DebugMessages.Add("⚠️ SupabaseService liefert 0 Mitglieder.");

                    foreach (var m in members)
                    {
                        var dto = MapToDTO(m);
                        _allMembers.Add(dto);
                        DebugMessages.Add($"✅ Mitglied geladen: {dto.DisplayName}");
                    }
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
                        DebugMessages.Add($"⚡ {_allParzellen.Count} Parzellen geladen.");
                    }
                }
                catch (Exception ex)
                {
                    DebugMessages.Add($"❌ Fehler beim Laden der Parzellen: {ex.Message}");
                }
            }
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
                        !string.IsNullOrEmpty(p.GartenNr) &&
                        p.GartenNr.Contains(text, StringComparison.OrdinalIgnoreCase));

                foreach (var p in matches)
                    Results.Add(p);
            }
            else
            {
                var matches = string.IsNullOrEmpty(text)
                    ? _allMembers
                    : _allMembers.Where(m =>
                        (!string.IsNullOrEmpty(m.Vorname) && m.Vorname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(m.Nachname) && m.Nachname.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(m.Email) && m.Email.Contains(text, StringComparison.OrdinalIgnoreCase)));

                foreach (var m in matches)
                    Results.Add(m);
            }
        }

        private MemberDTO MapToDTO(MitgliedRecord m)
        {
            return new MemberDTO
            {
                Id = m.Id,
                Email = m.Email ?? string.Empty,
                Role = m.Role ?? string.Empty,
                Vorname = m.Vorname ?? string.Empty,
                Nachname = m.Name ?? string.Empty
            };
        }

        private async Task SelectResultAsync(object? result)
        {
            if (result == null) return;

            MemberDTO? member = null;

            if (result is MemberDTO md)
            {
                member = md;
            }
            else if (result is ParzelleRecord pr)
            {
                var beleg = await _supabaseService.GetCurrentBelegungForParzelleAsync(pr.Id);
                if (beleg != null)
                {
                    var members = await _supabaseService.GetMitgliederAsync();
                    var mit = members.FirstOrDefault(m => m.Id == beleg.MitgliedId);
                    if (mit != null)
                        member = MapToDTO(mit);
                }
            }

            if (member == null) return;

            // SelectedMember setzen (für Sidebar/Member-Menü etc.)
            _mainVm.SelectedMember = member;

            // UND: sauber navigieren auf die Detailseite (Lifecycle wird von MainWindowViewModel handled)
            var detailVm = new MemberDetailViewModel(_supabaseService, member);
            await _mainVm.NavigateToAsync(detailVm);
        }
    }
}