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
    public sealed class ArbeitseinsaetzeVerwaltungViewModel : BaseViewModel, INavigationAware
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

        public ObservableCollection<ArbeitseinsatzEditItem> Items { get; } = new();

        private ArbeitseinsatzEditItem? _selectedItem;
        public ArbeitseinsatzEditItem? SelectedItem
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

        public ArbeitseinsaetzeVerwaltungViewModel(ISupabaseService supabaseService, UserContext userContext)
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
                var list = await _supabaseService.GetStartseiteArbeitseinsaetzeAsync();

                Items.Clear();
                foreach (var r in (list ?? new List<StartseiteArbeitseinsatzRecord>()).Where(x => x != null))
                    Items.Add(new ArbeitseinsatzEditItem(r));

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

            var rec = new StartseiteArbeitseinsatzRecord
            {
                Titel = string.Empty,
                Beschreibung = string.Empty,
                Datum = DateTime.Today,
                StartUhrzeit = string.Empty,
                EndUhrzeit = string.Empty,
                Treffpunkt = string.Empty,
                StundenWert = null,
                MaxTeilnehmer = null,
                AnmeldungBis = null,
                SichtbarAb = DateTime.Today,
                SichtbarBis = null,
                AngemeldetCount = 0,
                FreiePlaetze = null
            };

            var vm = new ArbeitseinsatzEditItem(rec);
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

            var deCulture = CultureInfo.GetCultureInfo("de-DE");

            var stundenText = (SelectedItem.StundenWertText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(stundenText)
                && !decimal.TryParse(stundenText, NumberStyles.Number, deCulture, out _))
            {
                StatusText = "Stundenwert muss eine Zahl sein (z.B. 2 oder 2,5).";
                return;
            }

            var maxTeilnehmerText = (SelectedItem.MaxTeilnehmerText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(maxTeilnehmerText)
                && !int.TryParse(maxTeilnehmerText, NumberStyles.Integer, deCulture, out _))
            {
                StatusText = "Max. Teilnehmer muss eine ganze Zahl sein.";
                return;
            }

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var saved = await _supabaseService.SaveStartseiteArbeitseinsatzAsync(SelectedItem.ToRecord());
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

        public sealed class ArbeitseinsatzEditItem : BaseViewModel
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
                set
                {
                    if (SetProperty(ref _titel, value ?? string.Empty))
                        OnPropertyChanged(nameof(DisplayText));
                }
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

            private string _treffpunkt = string.Empty;
            public string Treffpunkt
            {
                get => _treffpunkt;
                set => SetProperty(ref _treffpunkt, value ?? string.Empty);
            }

            private string _stundenWertText = string.Empty;
            public string StundenWertText
            {
                get => _stundenWertText;
                set => SetProperty(ref _stundenWertText, value ?? string.Empty);
            }

            private string _maxTeilnehmerText = string.Empty;
            public string MaxTeilnehmerText
            {
                get => _maxTeilnehmerText;
                set => SetProperty(ref _maxTeilnehmerText, value ?? string.Empty);
            }

            private DateTime? _anmeldungBis;
            public DateTime? AnmeldungBis
            {
                get => _anmeldungBis;
                set => SetProperty(ref _anmeldungBis, value);
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

            private int _angemeldetCount;
            public int AngemeldetCount
            {
                get => _angemeldetCount;
                private set => SetProperty(ref _angemeldetCount, value);
            }

            private int? _freiePlaetze;
            public int? FreiePlaetze
            {
                get => _freiePlaetze;
                private set => SetProperty(ref _freiePlaetze, value);
            }

            public string DisplayText
            {
                get
                {
                    var t = (Titel ?? string.Empty).Trim();
                    var d = Datum.HasValue ? Datum.Value.ToString("dd.MM.yyyy", DeCulture) : string.Empty;
                    if (string.IsNullOrWhiteSpace(d)) return t;
                    return string.IsNullOrWhiteSpace(t) ? d : $"{t} ({d})";
                }
            }

            public ArbeitseinsatzEditItem(StartseiteArbeitseinsatzRecord rec)
            {
                ApplySaved(rec);
            }

            public StartseiteArbeitseinsatzRecord ToRecord()
            {
                decimal? stunden = null;
                if (decimal.TryParse((StundenWertText ?? string.Empty).Trim(), NumberStyles.Number, DeCulture, out var st))
                    stunden = st;

                int? maxTeilnehmer = null;
                if (int.TryParse((MaxTeilnehmerText ?? string.Empty).Trim(), NumberStyles.Integer, DeCulture, out var mt))
                    maxTeilnehmer = mt;

                return new StartseiteArbeitseinsatzRecord
                {
                    Id = Id,
                    Titel = (Titel ?? string.Empty).Trim(),
                    Beschreibung = Beschreibung ?? string.Empty,
                    Datum = Datum,
                    StartUhrzeit = (StartUhrzeit ?? string.Empty).Trim(),
                    EndUhrzeit = (EndUhrzeit ?? string.Empty).Trim(),
                    Treffpunkt = (Treffpunkt ?? string.Empty).Trim(),
                    MaxTeilnehmer = maxTeilnehmer,
                    StundenWert = stunden,
                    SichtbarAb = SichtbarAb,
                    SichtbarBis = SichtbarBis,
                    AnmeldungBis = AnmeldungBis,
                    AngemeldetCount = AngemeldetCount,
                    FreiePlaetze = FreiePlaetze
                };
            }

            public void ApplySaved(StartseiteArbeitseinsatzRecord rec)
            {
                Id = rec.Id;
                Titel = (rec.Titel ?? string.Empty).Trim();
                Beschreibung = rec.Beschreibung ?? string.Empty;
                Datum = rec.Datum;
                StartUhrzeit = rec.StartUhrzeit ?? string.Empty;
                EndUhrzeit = rec.EndUhrzeit ?? string.Empty;
                Treffpunkt = rec.Treffpunkt ?? string.Empty;

                StundenWertText = rec.StundenWert.HasValue ? rec.StundenWert.Value.ToString("0.##", DeCulture) : string.Empty;
                MaxTeilnehmerText = rec.MaxTeilnehmer.HasValue ? rec.MaxTeilnehmer.Value.ToString(DeCulture) : string.Empty;

                SichtbarAb = rec.SichtbarAb;
                SichtbarBis = rec.SichtbarBis;
                AnmeldungBis = rec.AnmeldungBis;
                AngemeldetCount = rec.AngemeldetCount ?? 0;
                FreiePlaetze = rec.FreiePlaetze;

                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }
}
