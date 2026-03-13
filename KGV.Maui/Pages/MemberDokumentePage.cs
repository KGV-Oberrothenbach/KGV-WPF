using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public sealed class MemberDokumentePage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly MemberSelectionState _memberSelection;

    private bool _isBusy;
    private Task? _initTask;

    private readonly ActivityIndicator _busy;
    private readonly Label _subHeader;
    private readonly Label _status;
    private readonly CollectionView _list;

    private readonly List<DocumentInfo> _docs = new();

    public MemberDokumentePage(ISupabaseService supabaseService, MemberSelectionState memberSelection)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _memberSelection = memberSelection ?? throw new ArgumentNullException(nameof(memberSelection));

        Title = "Mitgliedsdokumente";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _subHeader = new Label { Text = string.Empty, Opacity = 0.8 };
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
                    new Label { Text = "Mitgliedsdokumente", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    _subHeader,
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
            var memberId = _memberSelection.SelectedMitgliedId;
            if (!memberId.HasValue)
            {
                ClearUi("Bitte erst ein Mitglied wählen (Mitgliedersuche).", clearList: true);
                _subHeader.Text = string.Empty;
                return;
            }

            _subHeader.Text = $"Mitglied #{memberId.Value}";

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
            ClearUi(ex.Message, clearList: true);
        }
        finally
        {
            SetBusy(false);
        }
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

    private void ClearUi(string message, bool clearList)
    {
        if (clearList)
        {
            _docs.Clear();
            _list.ItemsSource = null;
            _list.ItemsSource = _docs;
        }

        SetStatus(message, isError: true);
        UpdateUiState();
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
