using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KGV.Wpf.ViewModels
{
    public sealed class TermineVerwaltungViewModel : BaseViewModel, INavigationAware
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

        public ObservableCollection<TerminEditItem> Items { get; } = new();

        private TerminEditItem? _selectedItem;
        public TerminEditItem? SelectedItem
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

        public TermineVerwaltungViewModel(ISupabaseService supabaseService, UserContext userContext)
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
                var list = await _supabaseService.GetStartseiteTermineAsync();

                Items.Clear();
                foreach (var r in (list ?? new List<StartseiteTerminRecord>()).Where(x => x != null))
                    Items.Add(new TerminEditItem(r));

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

            var rec = new StartseiteTerminRecord
            {
                Titel = string.Empty,
                Beschreibung = string.Empty,
                Datum = DateTime.Today,
                StartUhrzeit = string.Empty,
                EndUhrzeit = string.Empty,
                SichtbarAb = DateTime.Today,
                SichtbarBis = null
            };

            var vm = new TerminEditItem(rec);
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

            if (!SelectedItem.Datum.HasValue)
            {
                StatusText = "Bitte Datum auswählen.";
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
                var saved = await _supabaseService.SaveStartseiteTerminAsync(SelectedItem.ToRecord());
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

        public sealed class TerminEditItem : BaseViewModel
        {
            private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

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

            private string _beschreibung = string.Empty;
            public string Beschreibung
            {
                get => _beschreibung;
                set => SetProperty(ref _beschreibung, value ?? string.Empty);
            }

            private DateTime? _datum;
            public DateTime? Datum
            {
                get => _datum;
                set
                {
                    if (SetProperty(ref _datum, value))
                        OnPropertyChanged(nameof(DisplayText));
                }
            }

            private string _start = string.Empty;
            public string StartUhrzeit
            {
                get => _start;
                set
                {
                    if (SetProperty(ref _start, value ?? string.Empty))
                        OnPropertyChanged(nameof(DisplayText));
                }
            }

            private string _end = string.Empty;
            public string EndUhrzeit
            {
                get => _end;
                set
                {
                    if (SetProperty(ref _end, value ?? string.Empty))
                        OnPropertyChanged(nameof(DisplayText));
                }
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

            public string DisplayText
            {
                get
                {
                    var d = Datum.HasValue ? Datum.Value.ToString("dd.MM.yyyy", DeCulture) : string.Empty;
                    var s = (StartUhrzeit ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(d)) return (Titel ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(s)) return $"{Titel} ({d})";
                    return $"{Titel} ({d} {s})";
                }
            }

            public TerminEditItem(StartseiteTerminRecord rec)
            {
                ApplySaved(rec);
            }

            public StartseiteTerminRecord ToRecord()
            {
                return new StartseiteTerminRecord
                {
                    Id = Id,
                    Titel = (Titel ?? string.Empty).Trim(),
                    Beschreibung = Beschreibung ?? string.Empty,
                    Datum = Datum,
                    StartUhrzeit = (StartUhrzeit ?? string.Empty).Trim(),
                    EndUhrzeit = (EndUhrzeit ?? string.Empty).Trim(),
                    SichtbarAb = SichtbarAb,
                    SichtbarBis = SichtbarBis
                };
            }

            public void ApplySaved(StartseiteTerminRecord rec)
            {
                Id = rec.Id;
                Titel = (rec.Titel ?? string.Empty).Trim();
                Beschreibung = rec.Beschreibung ?? string.Empty;
                Datum = rec.Datum;
                StartUhrzeit = rec.StartUhrzeit ?? string.Empty;
                EndUhrzeit = rec.EndUhrzeit ?? string.Empty;
                SichtbarAb = rec.SichtbarAb;
                SichtbarBis = rec.SichtbarBis;

                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }
}
