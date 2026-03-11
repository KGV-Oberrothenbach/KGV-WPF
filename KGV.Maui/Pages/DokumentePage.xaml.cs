using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public sealed class DokumentePage : ContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly UserContextState _state;

    private bool _isBusy;
    private Task? _initTask;

    private readonly ActivityIndicator _busy;
    private readonly Label _status;
    private readonly CollectionView _list;

    private readonly List<DocumentInfo> _docs = new();

    public DokumentePage(ISupabaseService supabaseService, UserContextState state)
    {
        _supabaseService = supabaseService;
        _state = state;

        Title = "Meine Dokumente";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };

        _status = new Label { TextColor = Colors.Gray };

        _list = new CollectionView
        {
            ItemsSource = _docs,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontAttributes = FontAttributes.Bold };
                name.SetBinding(Label.TextProperty, nameof(DocumentInfo.Name));

                var sub = new Label { FontSize = 12, TextColor = Colors.Gray };
                sub.SetBinding(Label.TextProperty, new Binding(path: ".", converter: new DocSubConverter()));

                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 8),
                    Children = { name, sub, new BoxView { HeightRequest = 1, Color = Colors.LightGray } }
                };
            })
        };

        _list.SelectionChanged += OnSelectionChanged;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    _busy,
                    _status,
                    _list
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += (_, _) => _status.Text = string.Empty;

        UpdateUiState();
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureInitializedAsync();
    }

    private Task EnsureInitializedAsync()
    {
        // Guard gegen parallele Initialisierung (schnelles Navigieren / mehrfaches Appearing)
        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = InitializeAsync();
        return _initTask;
    }

    private async Task InitializeAsync()
    {
        if (_isBusy)
            return;

        SetBusy(true);
        SetStatus("Lädt…", isError: false);

        try
        {
            var memberId = await TryGetMemberIdAsync();
            if (!memberId.HasValue)
            {
                ClearUi("Mitgliedskontext fehlt.");
                return;
            }

            _docs.Clear();
            var list = await _supabaseService.GetMitgliedDokumenteAsync(memberId.Value);
            if (list != null)
            {
                foreach (var d in list.Where(x => x != null)
                             .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                             .ThenBy(x => x.Name))
                    _docs.Add(d);
            }

            _list.ItemsSource = null;
            _list.ItemsSource = _docs;

            if (_docs.Count == 0)
                SetStatus("Keine Dokumente vorhanden.", isError: false);
            else
                SetStatus(string.Empty, isError: false);
        }
        catch (Exception ex)
        {
            ClearUi(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<int?> TryGetMemberIdAsync()
    {
        if (_state.CurrentMitgliedId.HasValue && _state.CurrentMitgliedId.Value > 0 && _state.CurrentMitgliedId.Value <= int.MaxValue)
            return (int)_state.CurrentMitgliedId.Value;

        // defensiver Fallback: falls der State inkonsistent ist, versuchen wir über CurrentUserId zu ermitteln.
        if (_state.CurrentUserId == null)
            return null;

        var member = await _supabaseService.GetMitgliedByAuthUserIdAsync(_state.CurrentUserId.Value);
        if (member == null)
            return null;

        _state.CurrentMitgliedId = member.Id;
        return member.Id;
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var doc = e.CurrentSelection?.FirstOrDefault() as DocumentInfo;
        if (sender is CollectionView cv)
            cv.SelectedItem = null;

        if (doc == null)
            return;

        await OpenAsync(doc);
    }

    private async Task OpenAsync(DocumentInfo doc)
    {
        if (_isBusy)
            return;

        if (doc == null || string.IsNullOrWhiteSpace(doc.StoragePath))
        {
            await DisplayAlert("Fehler", "Dokument ist ungültig.", "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var url = await _supabaseService.CreateDokumentSignedUrlAsync(doc.StoragePath, 3600);
            if (string.IsNullOrWhiteSpace(url))
            {
                await DisplayAlert("Fehler", "Dokument konnte nicht geöffnet werden (kein URL).", "OK");
                return;
            }

            await Launcher.Default.OpenAsync(url);
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

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        _list.IsEnabled = !_isBusy;
    }

    private void SetStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.TextColor = isError ? Colors.Red : Colors.Gray;
    }

    private void ClearUi(string message)
    {
        _docs.Clear();
        _list.ItemsSource = null;
        _list.ItemsSource = _docs;
        SetStatus(message, isError: true);
    }

    private sealed class DocSubConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not DocumentInfo d)
                return string.Empty;

            var parts = new List<string>();
            if (d.UpdatedAt.HasValue)
                parts.Add($"Aktualisiert: {d.UpdatedAt.Value:dd.MM.yyyy HH:mm}");

            if (d.Size.HasValue && d.Size.Value > 0)
                parts.Add($"Größe: {FormatBytes(d.Size.Value)}");

            return parts.Count == 0 ? string.Empty : string.Join(" • ", parts);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = 1024 * KB;

            if (bytes >= MB) return $"{bytes / (double)MB:0.#} MB";
            if (bytes >= KB) return $"{bytes / (double)KB:0.#} KB";
            return $"{bytes} B";
        }
    }
}
