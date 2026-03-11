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

        public ObservableCollection<object> Results { get; } = new();
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

        private bool _isInitialized;

        public MemberSearchViewModel(ISupabaseService supabaseService, MainWindowViewModel mainVm)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

            SearchCommand = new KGV.Wpf.Helpers.RelayCommand<object?>(_ => UpdateFilter());
            SelectCommand = new KGV.Wpf.Helpers.RelayCommand<object?>(_ => _ = SelectResultAsync(SelectedResult));

            _allMembers = new System.Collections.Generic.List<MemberDTO>();
            _allParzellen = new System.Collections.Generic.List<ParzelleRecord>();

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
                    Results.Add(p);

                return;
            }

            var memberMatches = string.IsNullOrEmpty(text)
                ? _allMembers
                : _allMembers.Where(m => MatchesMemberSearch(m, text));

            foreach (var m in memberMatches
                         .OrderBy(m => m.Nachname, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(m => m.Vorname, StringComparer.CurrentCultureIgnoreCase))
                Results.Add(m);
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
                AuthUserId = m.AuthUserId
            };
        }

        private async Task SelectResultAsync(object? result)
        {
            if (result == null) return;

            MemberDTO? selected = null;

            if (result is MemberDTO md)
            {
                selected = md;
            }
            else if (result is ParzelleRecord pr)
            {
                var beleg = await _supabaseService.GetCurrentBelegungForParzelleAsync(pr.Id);
                if (beleg != null)
                {
                    var members = await _supabaseService.GetMitgliederAsync();
                    var mit = members.FirstOrDefault(m => m.Id == beleg.MitgliedId);
                    if (mit != null)
                        selected = MapToDTO(mit);
                }
            }

            if (selected == null) return;

            var memberForDetail = selected.Clone();
            _mainVm.SelectedMember = memberForDetail;
        }
    }
}