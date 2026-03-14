using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace KGV.Wpf.ViewModels
{
    public sealed class BekanntmachungenVerwaltungViewModel : BaseViewModel, INavigationAware
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

        public ObservableCollection<BekanntmachungEditItem> Items { get; } = new();

        private BekanntmachungEditItem? _selectedItem;
        public BekanntmachungEditItem? SelectedItem
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

        private BekanntmachungEditItem? _editItem;
        public BekanntmachungEditItem? EditItem
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

        public BekanntmachungenVerwaltungViewModel(ISupabaseService supabaseService, UserContext userContext)
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
                var list = await _supabaseService.GetStartseiteBekanntmachungenAsync();

                Items.Clear();
                foreach (var r in (list ?? new List<StartseiteBekanntmachungRecord>()).Where(x => x != null))
                    Items.Add(new BekanntmachungEditItem(r));

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

        private static bool IsSaveValid(BekanntmachungEditItem item)
        {
            if (item == null) return false;

            if (string.IsNullOrWhiteSpace((item.Titel ?? string.Empty).Trim()))
                return false;

            if (string.IsNullOrWhiteSpace((item.InhaltText ?? string.Empty).Trim()))
                return false;

            var sortText = (item.SortOrderText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(sortText) && !int.TryParse(sortText, out _))
                return false;

            return true;
        }

        private async Task NewAsync()
        {
            if (!CanEdit) return;

            if (!ConfirmDiscardChangesIfNeeded())
                return;

            var rec = new StartseiteBekanntmachungRecord
            {
                Titel = string.Empty,
                InhaltHtml = string.Empty,
                SichtbarAb = DateTime.Today,
                SichtbarBis = null,
                SortOrder = null
            };

            _suppressDirtyTracking = true;
            try
            {
                EditItem = new BekanntmachungEditItem(rec);
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
                EditItem = new BekanntmachungEditItem(SelectedItem.ToRecord());
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

            if (string.IsNullOrWhiteSpace((EditItem.Titel ?? string.Empty).Trim()))
            {
                StatusText = "Bitte Titel ausfüllen.";
                return;
            }

            if (string.IsNullOrWhiteSpace((EditItem.InhaltText ?? string.Empty).Trim()))
            {
                StatusText = "Bitte Inhalt ausfüllen.";
                return;
            }

            var sortText = (EditItem.SortOrderText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(sortText) && !int.TryParse(sortText, out _))
            {
                StatusText = "Sortierung muss eine ganze Zahl sein.";
                return;
            }

            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            StatusText = string.Empty;

            try
            {
                var saved = await _supabaseService.SaveStartseiteBekanntmachungAsync(EditItem.ToRecord());
                if (saved == null)
                {
                    StatusText = "Speichern fehlgeschlagen.";
                    return;
                }

                var existing = Items.FirstOrDefault(x => x.Id == saved.Id);
                if (existing != null)
                {
                    existing.ApplySaved(saved);
                    SelectedItem = existing;
                }
                else
                {
                    var inserted = new BekanntmachungEditItem(saved);
                    Items.Insert(0, inserted);
                    SelectedItem = inserted;
                }

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
                var ok = await _supabaseService.DeleteStartseiteBekanntmachungAsync(EditItem.Id);
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

        public sealed class BekanntmachungEditItem : BaseViewModel
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

            private string _inhaltText = string.Empty;
            public string InhaltText
            {
                get => _inhaltText;
                set => SetProperty(ref _inhaltText, value ?? string.Empty);
            }

            private int _fontSize = 14;
            public int FontSize
            {
                get => _fontSize;
                set => SetProperty(ref _fontSize, value <= 0 ? 14 : value);
            }

            private bool _isBold;
            public bool IsBold
            {
                get => _isBold;
                set => SetProperty(ref _isBold, value);
            }

            private bool _isItalic;
            public bool IsItalic
            {
                get => _isItalic;
                set => SetProperty(ref _isItalic, value);
            }

            // Intern weiterhin HTML (DB-Feld: inhalt_html)
            private string _inhaltHtml = string.Empty;
            public string InhaltHtml
            {
                get => _inhaltHtml;
                private set => SetProperty(ref _inhaltHtml, value ?? string.Empty);
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

            private string _sortOrderText = string.Empty;
            public string SortOrderText
            {
                get => _sortOrderText;
                set => SetProperty(ref _sortOrderText, value ?? string.Empty);
            }

            public BekanntmachungEditItem(StartseiteBekanntmachungRecord rec)
            {
                ApplySaved(rec);
            }

            public StartseiteBekanntmachungRecord ToRecord()
            {
                int? sortOrder = null;
                var sortText = (SortOrderText ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(sortText) && int.TryParse(sortText, out var so))
                    sortOrder = so;

                return new StartseiteBekanntmachungRecord
                {
                    Id = Id,
                    Titel = (Titel ?? string.Empty).Trim(),
                    InhaltHtml = BuildHtml(InhaltText, FontSize, IsBold, IsItalic),
                    SichtbarAb = SichtbarAb,
                    SichtbarBis = SichtbarBis,
                    SortOrder = sortOrder
                };
            }

            public void ApplySaved(StartseiteBekanntmachungRecord rec)
            {
                Id = rec.Id;
                Titel = (rec.Titel ?? string.Empty).Trim();
                InhaltHtml = rec.InhaltHtml ?? string.Empty;
                InhaltText = ExtractPlainText(InhaltHtml);
                TryExtractEditorStyle(InhaltHtml, out var fs, out var bold, out var italic);
                FontSize = fs;
                IsBold = bold;
                IsItalic = italic;
                SichtbarAb = rec.SichtbarAb;
                SichtbarBis = rec.SichtbarBis;
                SortOrderText = rec.SortOrder?.ToString() ?? string.Empty;
            }

            private static string BuildHtml(string? text, int fontSize, bool bold, bool italic)
            {
                text = (text ?? string.Empty).Trim();
                var encoded = System.Net.WebUtility.HtmlEncode(text)
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\r", "\n", StringComparison.Ordinal)
                    .Replace("\n", "<br/>", StringComparison.Ordinal);

                var styles = new List<string> { $"font-size:{fontSize}px" };
                if (bold) styles.Add("font-weight:bold");
                if (italic) styles.Add("font-style:italic");

                return $"<p style=\"{string.Join(";", styles)}\">{encoded}</p>";
            }

            private static string ExtractPlainText(string? html)
            {
                html = html ?? string.Empty;
                // Minimaler Fallback: Tags entfernen + <br> als Zeilenumbruch.
                var s = html
                    .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
                    .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
                    .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);

                s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", string.Empty);
                return System.Net.WebUtility.HtmlDecode(s).Trim();
            }

            private static void TryExtractEditorStyle(string? html, out int fontSize, out bool bold, out bool italic)
            {
                fontSize = 14;
                bold = false;
                italic = false;

                html = html ?? string.Empty;
                var m = System.Text.RegularExpressions.Regex.Match(html, "style\\s*=\\s*\\\"(?<style>[^\\\"]+)\\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success) return;

                var style = m.Groups["style"].Value;
                if (style.Contains("font-weight:bold", StringComparison.OrdinalIgnoreCase)) bold = true;
                if (style.Contains("font-style:italic", StringComparison.OrdinalIgnoreCase)) italic = true;

                var m2 = System.Text.RegularExpressions.Regex.Match(style, "font-size\\s*:\\s*(?<n>\\d+)px", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m2.Success && int.TryParse(m2.Groups["n"].Value, out var fs) && fs > 0)
                    fontSize = fs;
            }
        }
    }
}
