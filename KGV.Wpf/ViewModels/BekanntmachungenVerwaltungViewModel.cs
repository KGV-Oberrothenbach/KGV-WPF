using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KGV.Wpf.ViewModels
{
    public sealed class BekanntmachungenVerwaltungViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly UserContext _userContext;
        private readonly SemaphoreSlim _opLock = new(1, 1);

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    NewCommand.RaiseCanExecuteChanged();
                    SaveCommand.RaiseCanExecuteChanged();
                    DeactivateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public bool CanEdit => _userContext.Role == UserRole.Admin || _userContext.Role == UserRole.Vorstand;

        public ObservableCollection<BekanntmachungEditItem> Items { get; } = new();

        private BekanntmachungEditItem? _selectedItem;
        public BekanntmachungEditItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                    DeactivateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public RelayCommand<object?> NewCommand { get; }
        public RelayCommand<object?> SaveCommand { get; }
        public RelayCommand<object?> DeactivateCommand { get; }

        public BekanntmachungenVerwaltungViewModel(ISupabaseService supabaseService, UserContext userContext)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

            NewCommand = new RelayCommand<object?>(_ => _ = NewAsync(), _ => CanEdit && !IsBusy);
            SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanEdit && !IsBusy && SelectedItem != null);
            DeactivateCommand = new RelayCommand<object?>(_ => _ = DeactivateAsync(), _ => CanEdit && !IsBusy && SelectedItem != null);
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        public Task OnNavigatedToAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var list = await _supabaseService.GetStartseiteBekanntmachungenAsync();

                Items.Clear();
                foreach (var r in (list ?? new List<StartseiteBekanntmachungRecord>()).Where(x => x != null))
                    Items.Add(new BekanntmachungEditItem(r));

                SelectedItem = Items.FirstOrDefault();

                if (!CanEdit)
                    StatusText = "Keine Berechtigung (Admin/Vorstand erforderlich).";
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
                Items.Clear();
                SelectedItem = null;
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }

        private async Task NewAsync()
        {
            if (!CanEdit) return;

            var rec = new StartseiteBekanntmachungRecord
            {
                Titel = string.Empty,
                InhaltHtml = string.Empty,
                SichtbarAb = DateTime.Today,
                SichtbarBis = null,
                SortOrder = Items.Count == 0 ? 0 : (Items.Max(x => x.SortOrderValue) + 1)
            };

            var vm = new BekanntmachungEditItem(rec);
            Items.Insert(0, vm);
            SelectedItem = vm;
        }

        private async Task SaveAsync()
        {
            if (!CanEdit) return;
            if (SelectedItem == null) return;

            if (string.IsNullOrWhiteSpace((SelectedItem.Titel ?? string.Empty).Trim()))
            {
                StatusText = "Bitte Titel ausfüllen.";
                return;
            }

            if (string.IsNullOrWhiteSpace((SelectedItem.InhaltHtml ?? string.Empty).Trim()))
            {
                StatusText = "Bitte Inhalt ausfüllen.";
                return;
            }

            if (!SelectedItem.SichtbarAb.HasValue)
            {
                StatusText = "Bitte 'Sichtbar ab' auswählen.";
                return;
            }

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var saved = await _supabaseService.SaveStartseiteBekanntmachungAsync(SelectedItem.ToRecord());
                if (saved == null)
                {
                    StatusText = "Speichern fehlgeschlagen.";
                    return;
                }

                SelectedItem.ApplySaved(saved);
                StatusText = "Gespeichert.";
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }

        private async Task DeactivateAsync()
        {
            if (!CanEdit) return;
            if (SelectedItem == null) return;

            SelectedItem.SichtbarBis = DateTime.Today;
            await SaveAsync();
        }

        public sealed class BekanntmachungEditItem : BaseViewModel
        {
            private long _id;
            public long Id
            {
                get => _id;
                private set => SetProperty(ref _id, value);
            }

            private string _titel = string.Empty;
            public string Titel
            {
                get => _titel;
                set => SetProperty(ref _titel, value ?? string.Empty);
            }

            private string _inhaltHtml = string.Empty;
            public string InhaltHtml
            {
                get => _inhaltHtml;
                set => SetProperty(ref _inhaltHtml, value ?? string.Empty);
            }

            private DateTime? _sichtbarAb;
            public DateTime? SichtbarAb
            {
                get => _sichtbarAb;
                set => SetProperty(ref _sichtbarAb, value);
            }

            private DateTime? _sichtbarBis;
            public DateTime? SichtbarBis
            {
                get => _sichtbarBis;
                set => SetProperty(ref _sichtbarBis, value);
            }

            private int _sortOrder;
            public int SortOrderValue
            {
                get => _sortOrder;
                set => SetProperty(ref _sortOrder, value);
            }

            public string SortOrderText
            {
                get => SortOrderValue.ToString();
                set
                {
                    if (int.TryParse((value ?? string.Empty).Trim(), out var v))
                        SortOrderValue = v;
                }
            }

            public BekanntmachungEditItem(StartseiteBekanntmachungRecord rec)
            {
                ApplySaved(rec);
            }

            public StartseiteBekanntmachungRecord ToRecord()
            {
                return new StartseiteBekanntmachungRecord
                {
                    Id = Id,
                    Titel = (Titel ?? string.Empty).Trim(),
                    InhaltHtml = InhaltHtml ?? string.Empty,
                    SichtbarAb = SichtbarAb,
                    SichtbarBis = SichtbarBis,
                    SortOrder = SortOrderValue
                };
            }

            public void ApplySaved(StartseiteBekanntmachungRecord rec)
            {
                Id = rec.Id;
                Titel = (rec.Titel ?? string.Empty).Trim();
                InhaltHtml = rec.InhaltHtml ?? string.Empty;
                SichtbarAb = rec.SichtbarAb;
                SichtbarBis = rec.SichtbarBis;
                SortOrderValue = rec.SortOrder ?? 0;
            }
        }
    }
}
