using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace KGV.Wpf.ViewModels;

public sealed class WartungsvertraegeVerwaltungViewModel : BaseViewModel, INavigationAware
{
    private readonly ISupabaseService _supabaseService;
    private readonly UserContext _userContext;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    public bool CanEdit => _userContext.Role == UserRole.Admin || _userContext.Role == UserRole.Vorstand;

    public ObservableCollection<WartungsvertragEditItem> Items { get; } = new();

    private WartungsvertragEditItem? _selectedItem;
    public WartungsvertragEditItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
                EditCommand.RaiseCanExecuteChanged();
        }
    }

    private WartungsvertragEditItem? _editItem;
    public WartungsvertragEditItem? EditItem
    {
        get => _editItem;
        private set
        {
            if (ReferenceEquals(_editItem, value))
                return;

            if (_editItem != null)
                _editItem.PropertyChanged -= EditItem_PropertyChanged;

            _editItem = value;

            if (_editItem != null)
                _editItem.PropertyChanged += EditItem_PropertyChanged;

            OnPropertyChanged();

            IsEditMode = _editItem != null;
            HasUnsavedChanges = false;

            SaveCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NewCommand.RaiseCanExecuteChanged();
                EditCommand.RaiseCanExecuteChanged();
                SaveCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        private set => SetProperty(ref _isEditMode, value);
    }

    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set
        {
            if (SetProperty(ref _hasUnsavedChanges, value))
                SaveCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _suppressDirtyTracking;

    public RelayCommand<object?> RefreshCommand { get; }
    public RelayCommand<object?> NewCommand { get; }
    public RelayCommand<object?> EditCommand { get; }
    public RelayCommand<object?> SaveCommand { get; }
    public RelayCommand<object?> CancelCommand { get; }

    public WartungsvertraegeVerwaltungViewModel(ISupabaseService supabaseService, UserContext userContext)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

        RefreshCommand = new RelayCommand<object?>(_ => _ = LoadAsync(), _ => !IsBusy);
        NewCommand = new RelayCommand<object?>(_ => _ = NewAsync(), _ => CanEdit && !IsBusy);
        EditCommand = new RelayCommand<object?>(_ => _ = BeginEditAsync(), _ => CanEdit && !IsBusy && SelectedItem != null);
        SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanEdit && !IsBusy && IsEditMode && EditItem != null && HasUnsavedChanges && IsSaveValid(EditItem));
        CancelCommand = new RelayCommand<object?>(_ => _ = CancelAsync(), _ => !IsBusy && IsEditMode);
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
            var list = await _supabaseService.GetWartungsvertraegeAsync();

            Items.Clear();
            foreach (var r in (list ?? new List<WartungsvertragRecord>()).Where(x => x != null))
                Items.Add(new WartungsvertragEditItem(r));

            SelectedItem = Items.FirstOrDefault();
            EditItem = null;
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

    private static bool IsSaveValid(WartungsvertragEditItem item)
    {
        if (item == null) return false;
        if (string.IsNullOrWhiteSpace(item.Titel)) return false;
        if (string.IsNullOrWhiteSpace(item.Bereich)) return false;

        if (!TryParseInt(item.MaxAktiveZuordnungenText, out var max) || max < 0)
            return false;

        return true;
    }

    private async Task NewAsync()
    {
        if (!CanEdit) return;
        if (IsEditMode && !ConfirmDiscardChangesIfNeeded())
            return;

        EditItem = new WartungsvertragEditItem(new WartungsvertragRecord
        {
            Titel = string.Empty,
            Bereich = "",
            Beschreibung = "",
            Aktiv = true,
            MaxAktiveZuordnungen = 1,
            BefreitVonPflichtstunden = false,
            Bemerkung = ""
        });

        HasUnsavedChanges = true;
        StatusText = string.Empty;
    }

    private async Task BeginEditAsync()
    {
        if (!CanEdit) return;
        if (SelectedItem == null) return;
        if (IsEditMode && !ConfirmDiscardChangesIfNeeded())
            return;

        EditItem = SelectedItem.Clone();
        HasUnsavedChanges = false;
        StatusText = string.Empty;
        await Task.CompletedTask;
    }

    private Task CancelAsync()
    {
        if (!IsEditMode) return Task.CompletedTask;

        EditItem = null;
        HasUnsavedChanges = false;
        StatusText = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        if (EditItem == null) return;

        if (!IsSaveValid(EditItem))
        {
            StatusText = "Bitte Pflichtfelder prüfen (Titel/Bereich/Max. aktive Zuordnungen).";
            return;
        }

        if (!await _opLock.WaitAsync(0))
            return;

        IsBusy = true;
        StatusText = string.Empty;

        try
        {
            var record = EditItem.ToRecord();
            var saved = await _supabaseService.SaveWartungsvertragAsync(record);
            if (saved == null)
            {
                StatusText = "Speichern fehlgeschlagen.";
                return;
            }

            await LoadAsync();

            // reselect
            SelectedItem = Items.FirstOrDefault(x => x.Id == saved.Id) ?? Items.FirstOrDefault();
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

    private void EditItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressDirtyTracking)
            return;

        HasUnsavedChanges = true;
    }

    private bool ConfirmDiscardChangesIfNeeded()
    {
        if (!IsEditMode || !HasUnsavedChanges)
            return true;

        var result = MessageBox.Show(
            "Es gibt ungespeicherte Änderungen. Änderungen verwerfen?",
            "Ungespeicherte Änderungen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    private static bool TryParseInt(string? text, out int value)
    {
        value = 0;
        var s = (text ?? string.Empty).Trim();
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public sealed class WartungsvertragEditItem : BaseViewModel
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

        private string _beschreibung = string.Empty;
        public string Beschreibung
        {
            get => _beschreibung;
            set => SetProperty(ref _beschreibung, value ?? string.Empty);
        }

        private string _bereich = string.Empty;
        public string Bereich
        {
            get => _bereich;
            set => SetProperty(ref _bereich, value ?? string.Empty);
        }

        private string _maxAktiveZuordnungenText = "1";
        public string MaxAktiveZuordnungenText
        {
            get => _maxAktiveZuordnungenText;
            set => SetProperty(ref _maxAktiveZuordnungenText, value ?? string.Empty);
        }

        private bool _befreitVonPflichtstunden;
        public bool BefreitVonPflichtstunden
        {
            get => _befreitVonPflichtstunden;
            set => SetProperty(ref _befreitVonPflichtstunden, value);
        }

        private bool _aktiv = true;
        public bool Aktiv
        {
            get => _aktiv;
            set => SetProperty(ref _aktiv, value);
        }

        private string _bemerkung = string.Empty;
        public string Bemerkung
        {
            get => _bemerkung;
            set => SetProperty(ref _bemerkung, value ?? string.Empty);
        }

        public WartungsvertragEditItem(WartungsvertragRecord rec)
        {
            ApplySaved(rec);
        }

        public void ApplySaved(WartungsvertragRecord rec)
        {
            Id = rec.Id;
            Titel = (rec.Titel ?? string.Empty).Trim();
            Beschreibung = rec.Beschreibung ?? string.Empty;
            Bereich = (rec.Bereich ?? string.Empty).Trim();
            MaxAktiveZuordnungenText = rec.MaxAktiveZuordnungen.ToString(CultureInfo.InvariantCulture);
            BefreitVonPflichtstunden = rec.BefreitVonPflichtstunden;
            Aktiv = rec.Aktiv;
            Bemerkung = rec.Bemerkung ?? string.Empty;
        }

        public WartungsvertragEditItem Clone()
        {
            return new WartungsvertragEditItem(ToRecord());
        }

        public WartungsvertragRecord ToRecord()
        {
            _ = TryParseInt(MaxAktiveZuordnungenText, out var max);

            return new WartungsvertragRecord
            {
                Id = Id,
                Titel = (Titel ?? string.Empty).Trim(),
                Beschreibung = Beschreibung ?? string.Empty,
                Bereich = (Bereich ?? string.Empty).Trim(),
                MaxAktiveZuordnungen = max,
                BefreitVonPflichtstunden = BefreitVonPflichtstunden,
                Aktiv = Aktiv,
                Bemerkung = Bemerkung ?? string.Empty
            };
        }
    }
}
