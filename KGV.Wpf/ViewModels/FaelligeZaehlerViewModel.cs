using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace KGV.Wpf.ViewModels
{
    public sealed class FaelligeZaehlerViewModel : BaseViewModel
    {
        private readonly ISupabaseService _supabaseService;
        private readonly SemaphoreSlim _opLock = new(1, 1);

        private readonly RelayCommand<object?> _reloadCommand;

        public ObservableCollection<ZaehlerEichstatusRecord> Items { get; } = new();
        public ICollectionView ItemsView { get; }

        public ObservableCollection<string> FilterOptions { get; } = new()
        {
            "Alle",
            "Bereits fällig",
            "Bald fällig",
            "Unkritisch"
        };

        private string _selectedFilter = "Alle";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                    ItemsView.Refresh();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                    _reloadCommand.RaiseCanExecuteChanged();
            }
        }

        private string _errorText = string.Empty;
        public string ErrorText
        {
            get => _errorText;
            private set => SetProperty(ref _errorText, value);
        }

        public ICommand ReloadCommand => _reloadCommand;

        public FaelligeZaehlerViewModel(ISupabaseService supabaseService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

            ItemsView = CollectionViewSource.GetDefaultView(Items);
            ItemsView.Filter = Filter;

            ApplyDefaultSorting();

            _reloadCommand = new RelayCommand<object?>(_ => _ = LoadAsync(), _ => !IsBusy);

            _ = LoadAsync();
        }

        private bool Filter(object obj)
        {
            if (obj is not ZaehlerEichstatusRecord r)
                return false;

            var days = r.TageBisFaellig ?? int.MaxValue;

            return SelectedFilter switch
            {
                "Bereits fällig" => days <= 0,
                "Bald fällig" => days > 0 && days <= 30,
                "Unkritisch" => days > 30,
                _ => true
            };
        }

        private void ApplyDefaultSorting()
        {
            ItemsView.SortDescriptions.Clear();
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(ZaehlerEichstatusRecord.TageBisFaellig), ListSortDirection.Ascending));
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(ZaehlerEichstatusRecord.Anlage), ListSortDirection.Ascending));
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(ZaehlerEichstatusRecord.GartenNr), ListSortDirection.Ascending));
        }

        private async Task LoadAsync()
        {
            if (!await _opLock.WaitAsync(0))
                return;

            IsBusy = true;
            ErrorText = string.Empty;

            try
            {
                Items.Clear();

                var list = await _supabaseService.GetZaehlerEichstatusAsync();

                foreach (var r in list
                    .OrderBy(x => x.TageBisFaellig ?? int.MaxValue)
                    .ThenBy(x => (x.Anlage ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.GartenNr ?? int.MaxValue)
                    .ThenBy(x => (x.Medium ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => (x.Zaehlernummer ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    Items.Add(r);
                }

                ItemsView.Refresh();
            }
            catch (Exception ex)
            {
                ErrorText = $"Fehler: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                _opLock.Release();
            }
        }
    }
}
