using KGV.Core.Interfaces;
using KGV.Core.Helpers;
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
                    EditCommand.RaiseCanExecuteChanged();
                    SaveCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
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

        public IReadOnlyList<string> TimeOptions { get; } = TimeText.BuildHalfHourOptions();

        public ObservableCollection<TerminEditItem> Items { get; } = new();

        private TerminEditItem? _selectedItem;
        public TerminEditItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    EditCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private TerminEditItem? _editItem;
        public TerminEditItem? EditItem
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
                DeactivateCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            private set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                    DeactivateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set => SetProperty(ref _hasUnsavedChanges, value);
        }

        private bool _suppressDirtyTracking;

        public RelayCommand<object?> NewCommand { get; }
        public RelayCommand<object?> EditCommand { get; }
        public RelayCommand<object?> SaveCommand { get; }
        public RelayCommand<object?> CancelCommand { get; }
        public RelayCommand<object?> DeactivateCommand { get; }
        public RelayCommand<object?> DeleteCommand { get; }

        public TermineVerwaltungViewModel(ISupabaseService supabaseService, UserContext userContext)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

            NewCommand = new RelayCommand<object?>(_ => _ = NewAsync(), _ => CanEdit && !IsBusy);
            EditCommand = new RelayCommand<object?>(_ => _ = BeginEditAsync(), _ => CanEdit && !IsBusy && SelectedItem != null);
            SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => CanEdit && !IsBusy && IsEditMode && EditItem != null && HasUnsavedChanges && IsSaveValid(EditItem));
            CancelCommand = new RelayCommand<object?>(_ => _ = CancelAsync(), _ => !IsBusy && IsEditMode);
            DeactivateCommand = new RelayCommand<object?>(_ => _ = DeactivateAsync(), _ => CanEdit && !IsBusy && IsEditMode && EditItem != null && EditItem.Id > 0);
            DeleteCommand = new RelayCommand<object?>(_ => _ = DeleteAsync(), _ => CanEdit && !IsBusy && IsEditMode && EditItem != null && EditItem.Id > 0);
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
                EditItem = null;

                if (!CanEdit)
                    StatusText = "Keine Berechtigung (Admin/Vorstand erforderlich).";
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
                Items.Clear();
                SelectedItem = null;
                EditItem = null;
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }

        private static bool IsSaveValid(TerminEditItem item)
        {
            if (item == null) return false;

            if (string.IsNullOrWhiteSpace((item.Titel ?? string.Empty).Trim()))
                return false;

            if (!item.Datum.HasValue)
                return false;

            var startRaw = (item.StartUhrzeit ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(startRaw) && !TimeText.TryNormalize(startRaw, out _))
                return false;

            var endRaw = (item.EndUhrzeit ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(endRaw) && !TimeText.TryNormalize(endRaw, out _))
                return false;

            return true;
        }

        private async Task NewAsync()
        {
            if (!CanEdit) return;

            if (!ConfirmDiscardChangesIfNeeded())
                return;

            var rec = new StartseiteTerminRecord
            {
                Titel = string.Empty,
                Beschreibung = string.Empty,
                Datum = DateTime.Today,
                StartUhrzeit = "10:00",
                EndUhrzeit = "13:00",
                SichtbarAb = DateTime.Today,
                SichtbarBis = null
            };

            _suppressDirtyTracking = true;
            try
            {
                EditItem = new TerminEditItem(rec);
            }
            finally
            {
                _suppressDirtyTracking = false;
            }
        }

        private async Task BeginEditAsync()
        {
            if (!CanEdit) return;
            if (SelectedItem == null) return;

            if (!ConfirmDiscardChangesIfNeeded())
                return;

            _suppressDirtyTracking = true;
            try
            {
                EditItem = new TerminEditItem(SelectedItem.ToRecord());
            }
            finally
            {
                _suppressDirtyTracking = false;
            }

            await Task.CompletedTask;
        }

        private Task CancelAsync()
        {
            if (!ConfirmDiscardChangesIfNeeded())
                return Task.CompletedTask;

            EditItem = null;
            StatusText = string.Empty;
            return Task.CompletedTask;
        }

        private async Task SaveAsync()
        {
            if (!CanEdit) return;
            if (EditItem == null) return;

            long? reselectId = null;
            var reloadAfterSave = false;

            if (string.IsNullOrWhiteSpace((EditItem.Titel ?? string.Empty).Trim()))
            {
                StatusText = "Bitte Titel ausfüllen.";
                return;
            }

            if (!EditItem.Datum.HasValue)
            {
                StatusText = "Bitte Datum auswählen.";
                return;
            }

            if (!TryNormalizeTimes(EditItem, out var error))
            {
                StatusText = error;
                return;
            }

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var saved = await _supabaseService.SaveStartseiteTerminAsync(EditItem.ToRecord());
                if (saved == null)
                {
                    StatusText = "Speichern fehlgeschlagen.";
                    return;
                }

                reselectId = saved.Id;
                reloadAfterSave = true;
                EditItem = null;
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

            if (reloadAfterSave)
            {
                await LoadAsync();
                if (reselectId.HasValue)
                    SelectedItem = Items.FirstOrDefault(x => x.Id == reselectId.Value) ?? SelectedItem;
            }
        }

        private static bool TryNormalizeTimes(TerminEditItem item, out string error)
        {
            error = string.Empty;

            var startRaw = (item.StartUhrzeit ?? string.Empty).Trim();
            if (!TimeText.TryNormalize(startRaw, out var start))
            {
                error = "Startzeit ist ungültig. Bitte HH:mm verwenden (z.B. 09:30).";
                return false;
            }

            var endRaw = (item.EndUhrzeit ?? string.Empty).Trim();
            if (!TimeText.TryNormalize(endRaw, out var end))
            {
                error = "Endzeit ist ungültig. Bitte HH:mm verwenden (z.B. 13:00).";
                return false;
            }

            // Normalisieren (Doppelpunkt wird hier ergänzt)
            item.StartUhrzeit = start;
            item.EndUhrzeit = end;

            return true;
        }

        private async Task DeactivateAsync()
        {
            if (!CanEdit) return;
            if (EditItem == null) return;

            EditItem.SichtbarBis = DateTime.Today;
            await SaveAsync();
        }

        private async Task DeleteAsync()
        {
            if (!CanEdit) return;
            if (EditItem == null) return;
            if (EditItem.Id <= 0) return;

            var result = MessageBox.Show(
                "Eintrag wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.",
                "Löschen bestätigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var ok = await _supabaseService.DeleteStartseiteTerminAsync(EditItem.Id);
                if (!ok)
                {
                    StatusText = "Löschen fehlgeschlagen.";
                    return;
                }

                var existing = Items.FirstOrDefault(x => x.Id == EditItem.Id);
                if (existing != null)
                    Items.Remove(existing);

                SelectedItem = Items.FirstOrDefault();
                EditItem = null;
                StatusText = "Gelöscht.";
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
            SaveCommand.RaiseCanExecuteChanged();
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
