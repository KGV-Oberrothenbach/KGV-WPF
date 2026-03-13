using CommunityToolkit.Mvvm.Messaging;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using KGV.Wpf.Messages;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Input;

namespace KGV.Wpf.ViewModels
{
    public sealed class HomeViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;
        private readonly UserContext _userContext;

        private readonly SemaphoreSlim _opLock = new(1, 1);

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        private async Task ExecuteArbeitseinsatzActionAsync(ArbeitseinsatzItem? item)
        {
            if (item == null) return;
            if (!item.HasMitgliedContext) return;

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            bool ok = false;
            try
            {
                ok = item.IsSignedUp
                    ? await _supabaseService.SignOffFromArbeitseinsatzAsync(item.Id)
                    : await _supabaseService.SignUpForArbeitseinsatzAsync(item.Id);
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

            if (!ok)
            {
                if (string.IsNullOrWhiteSpace(StatusText))
                    StatusText = "Aktion konnte nicht ausgeführt werden.";
                return;
            }

            await LoadAsync();
        }

        private Task? _loadTask;

        public ICommand ArbeitseinsatzActionCommand { get; }

        public ICommand EditBekanntmachungenCommand { get; }
        public ICommand EditTermineCommand { get; }
        public ICommand EditArbeitseinsaetzeCommand { get; }

        public bool CanEditStartseite => _userContext.Role == UserRole.Admin || _userContext.Role == UserRole.Vorstand;

        public ObservableCollection<BekanntmachungItem> Bekanntmachungen { get; } = new();
        public ObservableCollection<TerminItem> Termine { get; } = new();
        public ObservableCollection<ArbeitseinsatzItem> Arbeitseinsaetze { get; } = new();

        private BekanntmachungItem? _selectedBekanntmachung;
        public BekanntmachungItem? SelectedBekanntmachung
        {
            get => _selectedBekanntmachung;
            set
            {
                if (SetProperty(ref _selectedBekanntmachung, value))
                {
                    OnPropertyChanged(nameof(HasSelectedBekanntmachung));
                }
            }
        }

        public bool HasSelectedBekanntmachung => SelectedBekanntmachung != null;

        private PflichtstundenTile? _pflichtstunden;
        public PflichtstundenTile? Pflichtstunden
        {
            get => _pflichtstunden;
            private set
            {
                if (SetProperty(ref _pflichtstunden, value))
                {
                    OnPropertyChanged(nameof(ShowPflichtstunden));
                }
            }
        }

        public bool ShowPflichtstunden => Pflichtstunden != null;

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        private string _bekanntmachungenEmptyText = string.Empty;
        public string BekanntmachungenEmptyText
        {
            get => _bekanntmachungenEmptyText;
            private set => SetProperty(ref _bekanntmachungenEmptyText, value);
        }

        private string _termineEmptyText = string.Empty;
        public string TermineEmptyText
        {
            get => _termineEmptyText;
            private set => SetProperty(ref _termineEmptyText, value);
        }

        private string _arbeitseinsaetzeEmptyText = string.Empty;
        public string ArbeitseinsaetzeEmptyText
        {
            get => _arbeitseinsaetzeEmptyText;
            private set => SetProperty(ref _arbeitseinsaetzeEmptyText, value);
        }

        public HomeViewModel(ISupabaseService supabaseService, UserContext userContext)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

            ArbeitseinsatzActionCommand = new RelayCommand<ArbeitseinsatzItem>(item => _ = ExecuteArbeitseinsatzActionAsync(item));

            EditBekanntmachungenCommand = new RelayCommand<object?>(_ =>
                WeakReferenceMessenger.Default.Send(new NavigateToViewModelMessage(typeof(BekanntmachungenVerwaltungViewModel))));

            EditTermineCommand = new RelayCommand<object?>(_ =>
                WeakReferenceMessenger.Default.Send(new NavigateToViewModelMessage(typeof(TermineVerwaltungViewModel))));

            EditArbeitseinsaetzeCommand = new RelayCommand<object?>(_ =>
                WeakReferenceMessenger.Default.Send(new NavigateToViewModelMessage(typeof(ArbeitseinsaetzeVerwaltungViewModel))));

            WeakReferenceMessenger.Default.Register<SeasonChangedMessage>(this, (_, msg) =>
            {
                // Pflichtstunden sollen zur aktuell gewählten Saison passen.
                _selectedJahr = msg.Jahr;
                _ = LoadPflichtstundenAsync();
            });
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        public Task OnNavigatedToAsync()
        {
            // Reload on each navigation to keep it current.
            if (_loadTask != null && !_loadTask.IsCompleted)
                return _loadTask;

            _loadTask = LoadAsync();
            return _loadTask;
        }

        private async Task LoadAsync()
        {
            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var bekTask = _supabaseService.GetStartseiteBekanntmachungenAsync();
                var terTask = _supabaseService.GetStartseiteTermineAsync();
                var arbTask = _supabaseService.GetStartseiteArbeitseinsaetzeAsync();
                var myTask = _supabaseService.GetMyArbeitseinsatzAnmeldungenAsync();

                await Task.WhenAll(bekTask, terTask, arbTask, myTask);

                var mySignups = myTask.Result ?? new HashSet<long>();

                UpdateBekanntmachungen(bekTask.Result ?? new List<StartseiteBekanntmachungRecord>());
                UpdateTermine(terTask.Result ?? new List<StartseiteTerminRecord>());
                UpdateArbeitseinsaetze(arbTask.Result ?? new List<StartseiteArbeitseinsatzRecord>(), mySignups);

                await LoadPflichtstundenAsync();
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
                UpdateBekanntmachungen(new List<StartseiteBekanntmachungRecord>());
                UpdateTermine(new List<StartseiteTerminRecord>());
                UpdateArbeitseinsaetze(new List<StartseiteArbeitseinsatzRecord>(), new HashSet<long>());

                Pflichtstunden = null;
                SelectedBekanntmachung = null;
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }

        private int _selectedJahr = DateTime.Today.Year;

        private async Task LoadPflichtstundenAsync()
        {
            try
            {
                if (!_userContext.MitgliedId.HasValue || _userContext.MitgliedId.Value <= 0 || _userContext.MitgliedId.Value > int.MaxValue)
                {
                    Pflichtstunden = null;
                    return;
                }

                var myMitgliedId = (int)_userContext.MitgliedId.Value;
                var member = await _supabaseService.GetMitgliedByIdAsync(myMitgliedId);
                if (member == null)
                {
                    Pflichtstunden = null;
                    return;
                }

                var hauptmitgliedId = member.HauptmitgliedId ?? member.Id;
                if (hauptmitgliedId <= 0)
                {
                    Pflichtstunden = null;
                    return;
                }

                var saisonen = await _supabaseService.GetSaisonRecordsAsync();
                var saison = saisonen?.FirstOrDefault(x => x.Jahr == _selectedJahr);
                if (saison == null)
                {
                    Pflichtstunden = null;
                    return;
                }

                var rec = await _supabaseService.GetPflichtstundenUebersichtAsync(hauptmitgliedId, saison.Id);
                if (rec == null)
                {
                    Pflichtstunden = null;
                    return;
                }

                Pflichtstunden = new PflichtstundenTile(rec);
            }
            catch
            {
                Pflichtstunden = null;
            }
        }

        private void UpdateBekanntmachungen(List<StartseiteBekanntmachungRecord> list)
        {
            var keepSelectedId = SelectedBekanntmachung?.Dto.Id;

            Bekanntmachungen.Clear();
            foreach (var b in list.Where(x => x != null))
                Bekanntmachungen.Add(new BekanntmachungItem(b));

            BekanntmachungenEmptyText = Bekanntmachungen.Count == 0 ? "Keine aktuellen Bekanntmachungen." : string.Empty;

            if (keepSelectedId.HasValue)
                SelectedBekanntmachung = Bekanntmachungen.FirstOrDefault(x => x.Dto.Id == keepSelectedId.Value);
            else
                SelectedBekanntmachung = null;
        }

        private void UpdateTermine(List<StartseiteTerminRecord> list)
        {
            Termine.Clear();
            foreach (var t in list.Where(x => x != null))
                Termine.Add(new TerminItem(t));

            TermineEmptyText = Termine.Count == 0 ? "Keine anstehenden Termine." : string.Empty;
        }

        private void UpdateArbeitseinsaetze(List<StartseiteArbeitseinsatzRecord> list, HashSet<long> mySignups)
        {
            Arbeitseinsaetze.Clear();

            var hasMitglied = _userContext.MitgliedId.HasValue && _userContext.MitgliedId.Value > 0;

            foreach (var a in list.Where(x => x != null))
            {
                var signedUp = mySignups.Contains(a.Id);
                Arbeitseinsaetze.Add(new ArbeitseinsatzItem(a, signedUp, hasMitglied));
            }

            ArbeitseinsaetzeEmptyText = Arbeitseinsaetze.Count == 0 ? "Keine aktuellen Arbeitseinsätze." : string.Empty;
        }

        private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

        private static string FormatDate(DateTime? date)
            => date.HasValue ? date.Value.ToString("ddd, dd.MM.yyyy", DeCulture) : string.Empty;

        private static string FormatTime(string? time)
        {
            time = (time ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(time)) return string.Empty;

            if (TimeSpan.TryParse(time, DeCulture, out var ts))
                return ts.ToString(@"hh\:mm", DeCulture);

            if (DateTime.TryParse(time, DeCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("HH:mm", DeCulture);

            return time;
        }

        private static string FormatTimeRange(string? start, string? end)
        {
            var s = FormatTime(start);
            var e = FormatTime(end);
            if (string.IsNullOrWhiteSpace(s) && string.IsNullOrWhiteSpace(e)) return string.Empty;
            if (string.IsNullOrWhiteSpace(e)) return s;
            if (string.IsNullOrWhiteSpace(s)) return e;
            return $"{s}–{e}";
        }

        public sealed class BekanntmachungItem
        {
            public BekanntmachungItem(StartseiteBekanntmachungRecord dto)
            {
                Dto = dto;
            }

            public StartseiteBekanntmachungRecord Dto { get; }
            public string Titel => Dto.Titel;
            public string InhaltHtml => Dto.InhaltHtml;
            public bool HasInhaltHtml => !string.IsNullOrWhiteSpace(InhaltHtml);
        }

        public sealed class TerminItem
        {
            public TerminItem(StartseiteTerminRecord dto)
            {
                Dto = dto;
            }

            public StartseiteTerminRecord Dto { get; }
            public string Titel => Dto.Titel;
            public string Beschreibung => Dto.Beschreibung;
            public bool HasBeschreibung => !string.IsNullOrWhiteSpace(Beschreibung);
            public string WhenText
            {
                get
                {
                    var d = FormatDate(Dto.Datum);
                    var t = FormatTimeRange(Dto.StartUhrzeit, Dto.EndUhrzeit);
                    if (string.IsNullOrWhiteSpace(d)) return t;
                    if (string.IsNullOrWhiteSpace(t)) return d;
                    return $"{d} • {t}";
                }
            }
        }

        public sealed class PflichtstundenTile
        {
            public PflichtstundenTile(PflichtstundenUebersichtRecord rec)
            {
                Rec = rec;
            }

            public PflichtstundenUebersichtRecord Rec { get; }

            public string SaisonText => Rec.Jahr.ToString(DeCulture);
            public string SollText => Rec.Sollstunden.ToString("0.##", DeCulture);
            public string GeleistetText => Rec.Geleistet.ToString("0.##", DeCulture);
            public string OffenText => Rec.Offen.ToString("0.##", DeCulture);
            public string BefreiungsgrundText => (Rec.Befreiungsgrund ?? string.Empty).Trim();
            public bool HasBefreiungsgrund => !string.IsNullOrWhiteSpace(BefreiungsgrundText);
        }

        public sealed class ArbeitseinsatzItem
        {
            public ArbeitseinsatzItem(StartseiteArbeitseinsatzRecord dto, bool isSignedUp, bool hasMitgliedContext)
            {
                Dto = dto;
                IsSignedUp = isSignedUp;
                HasMitgliedContext = hasMitgliedContext;
            }

            public StartseiteArbeitseinsatzRecord Dto { get; }
            public long Id => Dto.Id;

            public bool IsSignedUp { get; }
            public bool HasMitgliedContext { get; }
            public string Titel => Dto.Titel;
            public string Beschreibung => Dto.Beschreibung;
            public bool HasBeschreibung => !string.IsNullOrWhiteSpace(Beschreibung);

            public string Treffpunkt => Dto.Treffpunkt;
            public bool HasTreffpunkt => !string.IsNullOrWhiteSpace(Treffpunkt);
            public string TreffpunktText => HasTreffpunkt ? $"Treffpunkt: {Treffpunkt}" : string.Empty;

            public string WhenText
            {
                get
                {
                    var d = FormatDate(Dto.Datum);
                    var t = FormatTimeRange(Dto.StartUhrzeit, Dto.EndUhrzeit);
                    if (string.IsNullOrWhiteSpace(d)) return t;
                    if (string.IsNullOrWhiteSpace(t)) return d;
                    return $"{d} • {t}";
                }
            }

            public string StundenWertText
                => Dto.StundenWert.HasValue ? $"Stundenwert: {Dto.StundenWert.Value:0.##}h" : string.Empty;

            public bool HasStundenWert => !string.IsNullOrWhiteSpace(StundenWertText);

            public string TeilnehmerText
            {
                get
                {
                    if (!Dto.MaxTeilnehmer.HasValue) return string.Empty;
                    var freie = Dto.FreiePlaetze.HasValue ? $" • Frei: {Dto.FreiePlaetze.Value}" : string.Empty;
                    var angemeldet = Dto.AngemeldetCount ?? 0;
                    return $"Teilnehmer: {angemeldet}/{Dto.MaxTeilnehmer.Value}{freie}";
                }
            }

            public bool HasTeilnehmerInfo => !string.IsNullOrWhiteSpace(TeilnehmerText);

            public bool IsSignupWindowOpen
            {
                get
                {
                    var now = DateTime.Now;

                    if (Dto.AnmeldungBis.HasValue && Dto.AnmeldungBis.Value < now)
                        return false;

                    if (IsEinsatzInPast(Dto, now))
                        return false;

                    return true;
                }
            }

            public bool HasFreeCapacity
            {
                get
                {
                    if (!Dto.MaxTeilnehmer.HasValue)
                        return true;

                    if (!Dto.FreiePlaetze.HasValue)
                        return false;

                    return Dto.FreiePlaetze.Value > 0;
                }
            }

            public bool CanSignUp => HasMitgliedContext && !IsSignedUp && IsSignupWindowOpen && HasFreeCapacity;
            public bool CanSignOff => HasMitgliedContext && IsSignedUp && IsSignupWindowOpen;

            public bool ShowActionButton => CanSignUp || CanSignOff;
            public string ActionButtonText => IsSignedUp ? "Abmelden" : "Anmelden";

            public string SignupStatusText => IsSignedUp ? "Du bist angemeldet." : string.Empty;
            public bool HasSignupStatus => !string.IsNullOrWhiteSpace(SignupStatusText);

            public string HintText
            {
                get
                {
                    if (!HasMitgliedContext)
                        return string.Empty;

                    if (ShowActionButton)
                        return string.Empty;

                    if (IsSignedUp)
                        return "Abmeldung nicht mehr möglich.";

                    if (!IsSignupWindowOpen)
                        return "Anmeldung nicht mehr möglich.";

                    if (!HasFreeCapacity)
                        return "Teilnehmerzahl erreicht.";

                    return "Anmeldung nicht möglich.";
                }
            }

            public bool HasHint => !string.IsNullOrWhiteSpace(HintText);

            private static bool IsEinsatzInPast(StartseiteArbeitseinsatzRecord dto, DateTime now)
            {
                if (!dto.Datum.HasValue)
                    return false;

                var date = dto.Datum.Value.Date;
                if (date < now.Date)
                    return true;

                if (date > now.Date)
                    return false;

                // Same day: if an end time exists and is already passed, treat as past.
                if (TryParseTime(dto.EndUhrzeit, out var end))
                {
                    var endDt = date.Add(end);
                    return endDt < now;
                }

                return false;
            }

            private static bool TryParseTime(string? time, out TimeSpan ts)
            {
                ts = default;
                time = (time ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(time)) return false;

                return TimeSpan.TryParse(time, DeCulture, out ts);
            }
        }
    }
}
