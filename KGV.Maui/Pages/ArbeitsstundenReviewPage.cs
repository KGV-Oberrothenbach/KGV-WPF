using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public sealed class ArbeitsstundenReviewPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly UserContextState _state;

    private readonly List<ArbeitsstundeDTO> _items = new();
    private readonly CollectionView _list;
    private readonly Label _status;

    private bool _isBusy;
    private Task? _initTask;

    private readonly ActivityIndicator _busy;
    private readonly Button _reloadButton;

    public ArbeitsstundenReviewPage(ISupabaseService supabaseService, UserContextState state)
    {
        _supabaseService = supabaseService;
        _state = state;

        Title = "Arbeitsstunden prüfen";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Gray };

        _list = new CollectionView
        {
            ItemsSource = _items,
            ItemTemplate = new DataTemplate(() =>
            {
                var header = new Label { FontAttributes = FontAttributes.Bold };
                header.SetBinding(Label.TextProperty, new Binding(path: ".", converter: new HeaderConverter()));

                var desc = new Label { FontSize = 12, TextColor = Colors.Gray };
                desc.SetBinding(Label.TextProperty, nameof(ArbeitsstundeDTO.Beschreibung));

                var approve = new Button { Text = "Genehmigen", BackgroundColor = Colors.LightGreen };
                approve.Clicked += OnApproveClicked;
                approve.SetBinding(Button.CommandParameterProperty, new Binding(path: "."));

                var reject = new Button { Text = "Ablehnen", BackgroundColor = Colors.LightPink };
                reject.Clicked += OnRejectClicked;
                reject.SetBinding(Button.CommandParameterProperty, new Binding(path: "."));

                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 8),
                    Spacing = 6,
                    Children =
                    {
                        header,
                        desc,
                        new HorizontalStackLayout { Spacing = 12, Children = { approve, reject } },
                        new BoxView { HeightRequest = 1, Color = Colors.LightGray }
                    }
                };
            })
        };

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                (_reloadButton = new Button { Text = "Neu laden" }),
                _busy,
                _status,
                _list
            }
        };

        _reloadButton.Clicked += async (_, __) => await EnsureInitializedAsync();

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureInitializedAsync();
        _ = (Shell.Current as AdminShell)?.RefreshWorkhoursBadgeAsync();
    }

    private Task EnsureInitializedAsync()
    {
        // Guard gegen paralleles Laden (Appearing + Button)
        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = LoadAsync();
        return _initTask;
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        SetStatus(string.Empty, isError: false);
        _items.Clear();

        try
        {
            var groups = await _supabaseService.GetUnapprovedArbeitsstundenByMitgliedAsync();

            var ids = groups
                .Select(g => g.MitgliedId)
                .Distinct()
                .ToArray();

            if (ids.Length == 0)
            {
                _list.ItemsSource = null;
                _list.ItemsSource = _items;
                SetStatus("Keine offenen Arbeitsstunden.", isError: false);
                return;
            }

            var list = await _supabaseService.GetArbeitsstundenAsync(ids);
            foreach (var a in list
                         .Where(a => a != null)
                         .OrderByDescending(a => a.Datum)
                         .ThenByDescending(a => a.Id))
            {
                if (a.Freigegeben) continue;

                var status = (a.Status ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(status) && !status.Equals("offen", StringComparison.OrdinalIgnoreCase))
                    continue;

                _items.Add(a);
            }

            _list.ItemsSource = null;
            _list.ItemsSource = _items;

            if (_items.Count == 0)
                SetStatus("Keine offenen Arbeitsstunden.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnApproveClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
            return;

        if ((sender as Button)?.CommandParameter is not ArbeitsstundeDTO dto)
            return;

        if (!TryGetApproverId(out var approverId))
            return;

        await UpdateStatusAsync(dto, approverId, approved: true);
    }

    private async void OnRejectClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
            return;

        if ((sender as Button)?.CommandParameter is not ArbeitsstundeDTO dto)
            return;

        if (!TryGetApproverId(out var approverId))
            return;

        await UpdateStatusAsync(dto, approverId, approved: false);
    }

    private bool TryGetApproverId(out int approverId)
    {
        approverId = 0;
        if (_state.CurrentMitgliedId == null || _state.CurrentMitgliedId.Value > int.MaxValue)
        {
            _ = DisplayAlert("Fehler", "Genehmiger-MitgliedId fehlt.", "OK");
            return false;
        }

        approverId = (int)_state.CurrentMitgliedId.Value;
        return true;
    }

    private async Task UpdateStatusAsync(ArbeitsstundeDTO dto, int approverId, bool approved)
    {
        if (_isBusy)
            return;

        SetBusy(true);
        try
        {
            var now = DateTime.UtcNow;
            var record = new ArbeitsstundeRecord
            {
                Id = dto.Id,
                MitgliedId = dto.MitgliedId,
                SaisonId = dto.SaisonId,
                Datum = dto.Datum.Date,
                Stunden = dto.Stunden,
                ArtDerArbeit = dto.Beschreibung,
                Status = approved ? "genehmigt" : "abgelehnt",
                Freigegeben = approved,
                GenehmigtAm = now,
                GenehmigtVon = approverId
            };

            var ok = await _supabaseService.UpdateArbeitsstundeAsync(record);
            if (!ok)
            {
                await DisplayAlert("Fehler", "Update fehlgeschlagen.", "OK");
                return;
            }

            await EnsureInitializedAsync();
            _ = (Shell.Current as AdminShell)?.RefreshWorkhoursBadgeAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        _busy.IsVisible = value;
        _busy.IsRunning = value;
        _reloadButton.IsEnabled = !value;
        _list.IsEnabled = !value;
    }

    private void SetStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.TextColor = isError ? Colors.Red : Colors.Gray;
    }

    private sealed class HeaderConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not ArbeitsstundeDTO a) return string.Empty;
            var who = $"{a.Nachname} {a.Vorname}".Trim();
            return $"{who} – {a.Datum:dd.MM.yyyy} – {a.Stunden:0.##}h";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}
