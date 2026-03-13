using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;

namespace KGV.Wpf.ViewModels;

public sealed class SaisonViewModel : BaseViewModel, INavigationAware
{
    private readonly ISupabaseService _supabaseService;

    public ObservableCollection<SaisonRecord> Saisons { get; } = new();

    private SaisonRecord? _selectedSaison;
    public SaisonRecord? SelectedSaison
    {
        get => _selectedSaison;
        set
        {
            if (!SetProperty(ref _selectedSaison, value))
                return;

            LoadFromSelected();
        }
    }

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
                InvalidateCommands();
        }
    }

    private bool _isNewDraft;

    private int _snapshotId;
    private string _snapshotJahr = string.Empty;
    private string _snapshotSoll = string.Empty;
    private string _snapshotEuro = string.Empty;
    private string _snapshotBemerkung = string.Empty;

    private string _jahrText = string.Empty;
    public string JahrText
    {
        get => _jahrText;
        set => SetProperty(ref _jahrText, value);
    }

    private string _pflichtstundenSollText = string.Empty;
    public string PflichtstundenSollText
    {
        get => _pflichtstundenSollText;
        set => SetProperty(ref _pflichtstundenSollText, value);
    }

    private string _euroProFehlstundeText = string.Empty;
    public string EuroProFehlstundeText
    {
        get => _euroProFehlstundeText;
        set => SetProperty(ref _euroProFehlstundeText, value);
    }

    private string _bemerkungText = string.Empty;
    public string BemerkungText
    {
        get => _bemerkungText;
        set => SetProperty(ref _bemerkungText, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand<object?> RefreshCommand { get; }
    public RelayCommand<object?> NewSaisonCommand { get; }
    public RelayCommand<object?> ToggleEditCommand { get; }
    public RelayCommand<object?> SaveCommand { get; }
    public RelayCommand<object?> CancelCommand { get; }

    public SaisonViewModel(ISupabaseService supabaseService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

        RefreshCommand = new RelayCommand<object?>(_ => _ = LoadAsync());
        NewSaisonCommand = new RelayCommand<object?>(_ => _ = NewSaisonAsync());
        ToggleEditCommand = new RelayCommand<object?>(_ => ToggleEdit());
        SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => IsEditMode);
        CancelCommand = new RelayCommand<object?>(_ => _ = CancelAsync(), _ => IsEditMode);
    }

    public async Task OnNavigatedToAsync()
    {
        await LoadAsync();
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private async Task LoadAsync()
    {
        StatusMessage = null;

        try
        {
            var list = await _supabaseService.GetSaisonRecordsAsync();
            Saisons.Clear();

            foreach (var s in (list ?? new()).OrderByDescending(x => x.Jahr))
                Saisons.Add(s);

            if (SelectedSaison == null)
            {
                var currentYear = DateTime.Today.Year;
                SelectedSaison = Saisons.FirstOrDefault(x => x.Jahr == currentYear) ?? Saisons.FirstOrDefault();
            }
            LoadFromSelected();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void LoadFromSelected()
    {
        if (IsEditMode)
            return;

        var s = SelectedSaison;
        if (s == null)
        {
            JahrText = string.Empty;
            PflichtstundenSollText = string.Empty;
            EuroProFehlstundeText = string.Empty;
            BemerkungText = string.Empty;
            Snapshot();
            return;
        }

        JahrText = s.Jahr.ToString(CultureInfo.InvariantCulture);
        PflichtstundenSollText = s.PflichtstundenSoll.ToString(CultureInfo.CurrentCulture);
        EuroProFehlstundeText = s.EuroProFehlstunde.ToString(CultureInfo.CurrentCulture);
        BemerkungText = s.Bemerkung ?? string.Empty;

        Snapshot();
    }

    private void Snapshot()
    {
        _snapshotId = SelectedSaison?.Id ?? 0;
        _snapshotJahr = JahrText;
        _snapshotSoll = PflichtstundenSollText;
        _snapshotEuro = EuroProFehlstundeText;
        _snapshotBemerkung = BemerkungText;
    }

    private void ToggleEdit()
    {
        StatusMessage = null;

        if (!IsEditMode)
        {
            if (SelectedSaison == null && !_isNewDraft)
            {
                StatusMessage = "Bitte zuerst eine Saison auswählen.";
                return;
            }

            IsEditMode = true;
            Snapshot();
            return;
        }

        _ = CancelAsync();
    }

    private async Task NewSaisonAsync()
    {
        StatusMessage = null;

        try
        {
            var latest = Saisons.OrderByDescending(x => x.Jahr).FirstOrDefault();
            var newYear = (latest?.Jahr ?? DateTime.Today.Year) + 1;

            JahrText = newYear.ToString(CultureInfo.InvariantCulture);
            PflichtstundenSollText = (latest?.PflichtstundenSoll ?? 0m).ToString(CultureInfo.CurrentCulture);
            EuroProFehlstundeText = (latest?.EuroProFehlstunde ?? 25m).ToString(CultureInfo.CurrentCulture);
            BemerkungText = latest?.Bemerkung ?? string.Empty;

            SelectedSaison = null;
            _isNewDraft = true;

            IsEditMode = false;
            ToggleEdit();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task SaveAsync()
    {
        StatusMessage = null;

        if (!IsEditMode)
            return;

        if (!int.TryParse((JahrText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var jahr) || jahr < 1900 || jahr > 2100)
        {
            StatusMessage = "Jahr ist ungültig.";
            return;
        }

        if (!TryParseDecimal(PflichtstundenSollText, out var soll))
        {
            StatusMessage = "Pflichtstunden Soll ist ungültig.";
            return;
        }

        if (!TryParseDecimal(EuroProFehlstundeText, out var euro))
        {
            StatusMessage = "€ pro Fehlstunde ist ungültig.";
            return;
        }

        var record = new SaisonRecord
        {
            Id = _isNewDraft ? 0 : (SelectedSaison?.Id ?? _snapshotId),
            Jahr = jahr,
            PflichtstundenSoll = soll,
            EuroProFehlstunde = euro,
            Bemerkung = string.IsNullOrWhiteSpace(BemerkungText) ? null : BemerkungText.Trim(),
        };

        try
        {
            var saved = await _supabaseService.SaveSaisonAsync(record);
            if (saved == null)
            {
                StatusMessage = "Speichern fehlgeschlagen.";
                return;
            }

            _isNewDraft = false;
            IsEditMode = false;

            await LoadAsync();
            SelectedSaison = Saisons.FirstOrDefault(x => x.Id == saved.Id) ?? Saisons.FirstOrDefault(x => x.Jahr == saved.Jahr);

            StatusMessage = "Gespeichert.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            InvalidateCommands();
        }
    }

    private async Task CancelAsync()
    {
        try
        {
            JahrText = _snapshotJahr;
            PflichtstundenSollText = _snapshotSoll;
            EuroProFehlstundeText = _snapshotEuro;
            BemerkungText = _snapshotBemerkung;

            if (_isNewDraft)
            {
                _isNewDraft = false;
                SelectedSaison = Saisons.FirstOrDefault();
                LoadFromSelected();
            }

            IsEditMode = false;
            StatusMessage = null;
        }
        catch
        {
        }
        finally
        {
            InvalidateCommands();
        }

        await Task.CompletedTask;
    }

    private void InvalidateCommands()
    {
        SaveCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0m;
            return true;
        }

        // erlauben sowohl "," als auch "." als Dezimaltrenner
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
            return true;

        if (decimal.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;

        return false;
    }
}
