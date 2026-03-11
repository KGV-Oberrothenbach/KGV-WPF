using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace KGV.Maui.Pages;

public sealed class HomePage : ContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly KGV.Maui.Services.IAndroidUpdateService _androidUpdateService;

    private bool _isBusy;
    private Task? _loadTask;

    private readonly ActivityIndicator _busy;
    private readonly Label _status;

    private readonly ObservableCollection<BekanntmachungItem> _bekanntmachungen = new();
    private readonly ObservableCollection<TerminItem> _termine = new();
    private readonly ObservableCollection<ArbeitseinsatzItem> _arbeitseinsaetze = new();

    private readonly Label _bekanntmachungenEmpty;
    private readonly Label _termineEmpty;
    private readonly Label _arbeitseinsaetzeEmpty;

    public HomePage(ISupabaseService supabaseService, IUserContextAccessor userContextAccessor, KGV.Maui.Services.IAndroidUpdateService androidUpdateService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));
        _androidUpdateService = androidUpdateService ?? throw new ArgumentNullException(nameof(androidUpdateService));

        Title = "Start";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };

        _bekanntmachungenEmpty = new Label { Text = "Keine aktuellen Bekanntmachungen.", Opacity = 0.8, IsVisible = false, TextColor = Colors.Gray };
        _termineEmpty = new Label { Text = "Keine anstehenden Termine.", Opacity = 0.8, IsVisible = false, TextColor = Colors.Gray };
        _arbeitseinsaetzeEmpty = new Label { Text = "Keine aktuellen Arbeitseinsätze.", Opacity = 0.8, IsVisible = false, TextColor = Colors.Gray };

        object? cardStyleObj = null;
        if (Application.Current?.Resources != null)
            Application.Current.Resources.TryGetValue("Card", out cardStyleObj);
        var cardStyle = cardStyleObj as Style;

        Border WrapCard(View content)
            => cardStyle != null
                ? new Border { Style = cardStyle, Content = content }
                : new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = 12,
                    Content = content
                };

        var bekanntmachungenList = new VerticalStackLayout { Spacing = 12 };
        BindableLayout.SetItemsSource(bekanntmachungenList, _bekanntmachungen);
        BindableLayout.SetItemTemplate(bekanntmachungenList, new DataTemplate(() =>
        {
            var title = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
            title.SetBinding(Label.TextProperty, nameof(BekanntmachungItem.Titel));

            var html = new Label { TextType = TextType.Html, LineBreakMode = LineBreakMode.WordWrap };
            html.SetBinding(Label.TextProperty, nameof(BekanntmachungItem.InhaltHtml));
            html.SetBinding(IsVisibleProperty, nameof(BekanntmachungItem.HasInhaltHtml));

            return WrapCard(new VerticalStackLayout
            {
                Spacing = 6,
                Children = { title, html }
            });
        }));

        var termineList = new VerticalStackLayout { Spacing = 12 };
        BindableLayout.SetItemsSource(termineList, _termine);
        BindableLayout.SetItemTemplate(termineList, new DataTemplate(() =>
        {
            var title = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
            title.SetBinding(Label.TextProperty, nameof(TerminItem.Titel));

            var when = new Label { FontSize = 12, TextColor = Colors.Gray };
            when.SetBinding(Label.TextProperty, nameof(TerminItem.WhenText));

            var desc = new Label { LineBreakMode = LineBreakMode.WordWrap };
            desc.SetBinding(Label.TextProperty, nameof(TerminItem.Beschreibung));
            desc.SetBinding(IsVisibleProperty, nameof(TerminItem.HasBeschreibung));

            return WrapCard(new VerticalStackLayout
            {
                Spacing = 4,
                Children = { title, when, desc }
            });
        }));

        var arbeitList = new VerticalStackLayout { Spacing = 12 };
        BindableLayout.SetItemsSource(arbeitList, _arbeitseinsaetze);
        BindableLayout.SetItemTemplate(arbeitList, new DataTemplate(() =>
        {
            var title = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
            title.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.Titel));

            var when = new Label { FontSize = 12, TextColor = Colors.Gray };
            when.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.WhenText));

            var treff = new Label { LineBreakMode = LineBreakMode.WordWrap };
            treff.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.TreffpunktText));
            treff.SetBinding(IsVisibleProperty, nameof(ArbeitseinsatzItem.HasTreffpunkt));

            var stunden = new Label { FontSize = 12, TextColor = Colors.Gray };
            stunden.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.StundenWertText));
            stunden.SetBinding(IsVisibleProperty, nameof(ArbeitseinsatzItem.HasStundenWert));

            var teiln = new Label { FontSize = 12, TextColor = Colors.Gray };
            teiln.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.TeilnehmerText));
            teiln.SetBinding(IsVisibleProperty, nameof(ArbeitseinsatzItem.HasTeilnehmerInfo));

            var desc = new Label { LineBreakMode = LineBreakMode.WordWrap };
            desc.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.Beschreibung));
            desc.SetBinding(IsVisibleProperty, nameof(ArbeitseinsatzItem.HasBeschreibung));

            var status = new Label { FontSize = 12, TextColor = Colors.Gray };
            status.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.SignupStatusText));
            status.SetBinding(IsVisibleProperty, nameof(ArbeitseinsatzItem.HasSignupStatus));

            var hint = new Label { FontSize = 12, TextColor = Colors.Gray };
            hint.SetBinding(Label.TextProperty, nameof(ArbeitseinsatzItem.HintText));
            hint.SetBinding(IsVisibleProperty, nameof(ArbeitseinsatzItem.HasHint));

            var action = new Button { Margin = new Thickness(0, 8, 0, 0) };
            action.SetBinding(Button.TextProperty, nameof(ArbeitseinsatzItem.ActionButtonText));
            action.SetBinding(IsVisibleProperty, nameof(ArbeitseinsatzItem.ShowActionButton));
            action.Clicked += OnArbeitseinsatzActionClicked;

            return WrapCard(new VerticalStackLayout
            {
                Spacing = 4,
                Children = { title, when, treff, stunden, teiln, desc, status, action, hint }
            });
        }));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 16,
                Children =
                {
                    new Label
                    {
                        Text = "Herzlich willkommen im Kleingartenverein Oberrothenbach",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },

                    _busy,
                    _status,

                    new Label { Text = "Bekanntmachungen", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _bekanntmachungenEmpty,
                    bekanntmachungenList,

                    new Label { Text = "Termine", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _termineEmpty,
                    termineList,

                    new Label { Text = "Arbeitseinsätze", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _arbeitseinsaetzeEmpty,
                    arbeitList
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += (_, _) => _status.Text = string.Empty;

        UpdateUiState();
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        _ = PromptAndroidUpdateIfNeededAsync();
        await EnsureLoadedAsync();
    }

    private async Task PromptAndroidUpdateIfNeededAsync()
    {
        try
        {
            var info = await _androidUpdateService.TryGetUpdatePromptAsync();
            if (info == null)
                return;

            var download = await MainThread.InvokeOnMainThreadAsync(()
                => DisplayAlert("Update verfügbar", info.BuildMessage(), "Herunterladen", "Später"));

            if (!download)
                return;

            await Browser.OpenAsync(info.DownloadUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            // Update check/prompt must never break navigation or the start page.
        }
    }

    private Task EnsureLoadedAsync()
    {
        if (_loadTask != null && !_loadTask.IsCompleted)
            return _loadTask;

        _loadTask = LoadAsync();
        return _loadTask;
    }

    private async Task LoadAsync()
    {
        if (_isBusy)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var bekTask = _supabaseService.GetStartseiteBekanntmachungenAsync();
            var terTask = _supabaseService.GetStartseiteTermineAsync();
            var arbTask = _supabaseService.GetStartseiteArbeitseinsaetzeAsync();
            var myTask = _supabaseService.GetMyArbeitseinsatzAnmeldungenAsync();

            await Task.WhenAll(bekTask, terTask, arbTask, myTask);

            var hasMitglied = _userContextAccessor.CurrentUserContext?.MitgliedId is > 0;
            var mySignups = myTask.Result ?? new HashSet<long>();

            UpdateBekanntmachungen(bekTask.Result ?? new List<StartseiteBekanntmachungRecord>());
            UpdateTermine(terTask.Result ?? new List<StartseiteTerminRecord>());
            UpdateArbeitseinsaetze(arbTask.Result ?? new List<StartseiteArbeitseinsatzRecord>(), mySignups, hasMitglied);
        }
        catch (Exception ex)
        {
            _bekanntmachungen.Clear();
            _termine.Clear();
            _arbeitseinsaetze.Clear();
            _status.Text = ex.Message;
        }
        finally
        {
            UpdateEmptyTexts();
            SetBusy(false);
        }
    }

    private void UpdateBekanntmachungen(List<StartseiteBekanntmachungRecord> list)
    {
        _bekanntmachungen.Clear();
        foreach (var b in list.Where(x => x != null))
            _bekanntmachungen.Add(new BekanntmachungItem((b.Titel ?? string.Empty).Trim(), b.InhaltHtml ?? string.Empty));
    }

    private void UpdateTermine(List<StartseiteTerminRecord> list)
    {
        _termine.Clear();
        foreach (var t in list.Where(x => x != null))
            _termine.Add(new TerminItem((t.Titel ?? string.Empty).Trim(), t.Beschreibung ?? string.Empty, FormatWhen(t.Datum, t.StartUhrzeit, t.EndUhrzeit)));
    }

    private void UpdateArbeitseinsaetze(List<StartseiteArbeitseinsatzRecord> list, HashSet<long> mySignups, bool hasMitgliedContext)
    {
        _arbeitseinsaetze.Clear();
        foreach (var a in list.Where(x => x != null))
        {
            var signedUp = mySignups.Contains(a.Id);
            var teilnehmer = a.MaxTeilnehmer.HasValue
                ? $"Teilnehmer: {(a.AngemeldetCount ?? 0)}/{a.MaxTeilnehmer.Value}" + (a.FreiePlaetze.HasValue ? $" • Frei: {a.FreiePlaetze.Value}" : string.Empty)
                : string.Empty;

            _arbeitseinsaetze.Add(new ArbeitseinsatzItem(
                a.Id,
                (a.Titel ?? string.Empty).Trim(),
                a.Beschreibung ?? string.Empty,
                FormatWhen(a.Datum, a.StartUhrzeit, a.EndUhrzeit),
                (a.Treffpunkt ?? string.Empty).Trim(),
                a.StundenWert.HasValue ? $"Stundenwert: {a.StundenWert.Value:0.##}h" : string.Empty,
                teilnehmer,
                signedUp,
                hasMitgliedContext,
                a.Datum,
                a.EndUhrzeit,
                a.AnmeldungBis,
                a.MaxTeilnehmer,
                a.FreiePlaetze));
        }
    }

    private async void OnArbeitseinsatzActionClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;

        if (sender is not Button btn || btn.BindingContext is not ArbeitseinsatzItem item)
            return;

        if (!item.HasMitgliedContext || !item.ShowActionButton)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        bool ok = false;
        try
        {
            ok = item.IsSignedUp
                ? await _supabaseService.SignOffFromArbeitseinsatzAsync(item.Id)
                : await _supabaseService.SignUpForArbeitseinsatzAsync(item.Id);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }

        if (!ok)
        {
            if (string.IsNullOrWhiteSpace(_status.Text))
                await DisplayAlert("Fehler", "Aktion konnte nicht ausgeführt werden.", "OK");
            return;
        }

        _loadTask = null;
        await EnsureLoadedAsync();
    }

    private void UpdateEmptyTexts()
    {
        _bekanntmachungenEmpty.IsVisible = _bekanntmachungen.Count == 0;
        _termineEmpty.IsVisible = _termine.Count == 0;
        _arbeitseinsaetzeEmpty.IsVisible = _arbeitseinsaetze.Count == 0;
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
        // aktuell read-only, nur Sperre gegen paralleles Öffnen/Reload
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

    private static string FormatWhen(DateTime? date, string? start, string? end)
    {
        var d = FormatDate(date);
        var s = FormatTime(start);
        var e = FormatTime(end);

        var time = string.Empty;
        if (!string.IsNullOrWhiteSpace(s) && !string.IsNullOrWhiteSpace(e)) time = $"{s}–{e}";
        else if (!string.IsNullOrWhiteSpace(s)) time = s;
        else if (!string.IsNullOrWhiteSpace(e)) time = e;

        if (string.IsNullOrWhiteSpace(d)) return time;
        if (string.IsNullOrWhiteSpace(time)) return d;
        return $"{d} • {time}";
    }

    private sealed record BekanntmachungItem(string Titel, string InhaltHtml)
    {
        public bool HasInhaltHtml => !string.IsNullOrWhiteSpace(InhaltHtml);
    }

    private sealed record TerminItem(string Titel, string Beschreibung, string WhenText)
    {
        public bool HasBeschreibung => !string.IsNullOrWhiteSpace(Beschreibung);
    }

    private sealed record ArbeitseinsatzItem(
        long Id,
        string Titel,
        string Beschreibung,
        string WhenText,
        string Treffpunkt,
        string StundenWertText,
        string TeilnehmerText,
        bool IsSignedUp,
        bool HasMitgliedContext,
        DateTime? Datum,
        string? EndUhrzeit,
        DateTime? AnmeldungBis,
        int? MaxTeilnehmer,
        int? FreiePlaetze)
    {
        public bool HasBeschreibung => !string.IsNullOrWhiteSpace(Beschreibung);
        public bool HasTreffpunkt => !string.IsNullOrWhiteSpace(Treffpunkt);
        public bool HasStundenWert => !string.IsNullOrWhiteSpace(StundenWertText);
        public bool HasTeilnehmerInfo => !string.IsNullOrWhiteSpace(TeilnehmerText);
        public string TreffpunktText => string.IsNullOrWhiteSpace(Treffpunkt) ? string.Empty : $"Treffpunkt: {Treffpunkt}";

        public bool IsSignupWindowOpen
        {
            get
            {
                var now = DateTime.Now;

                if (AnmeldungBis.HasValue && AnmeldungBis.Value < now)
                    return false;

                if (IsEinsatzInPast(Datum, EndUhrzeit, now))
                    return false;

                return true;
            }
        }

        public bool HasFreeCapacity
        {
            get
            {
                if (!MaxTeilnehmer.HasValue)
                    return true;

                if (!FreiePlaetze.HasValue)
                    return false;

                return FreiePlaetze.Value > 0;
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

        private static bool IsEinsatzInPast(DateTime? datum, string? endUhrzeit, DateTime now)
        {
            if (!datum.HasValue)
                return false;

            var date = datum.Value.Date;
            if (date < now.Date)
                return true;

            if (date > now.Date)
                return false;

            endUhrzeit = (endUhrzeit ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(endUhrzeit))
                return false;

            if (TimeSpan.TryParse(endUhrzeit, DeCulture, out var end))
            {
                var endDt = date.Add(end);
                return endDt < now;
            }

            return false;
        }
    }
}
