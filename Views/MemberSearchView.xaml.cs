using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KGV.ViewModels;

namespace KGV.Views
{
    public partial class MemberSearchView : UserControl
    {
        private bool _initialized;
        private string _lastSortProperty = string.Empty;
        private System.ComponentModel.ListSortDirection _lastSortDirection = System.ComponentModel.ListSortDirection.Ascending;

        public MemberSearchView()
        {
            InitializeComponent();
            Loaded += MemberSearchView_Loaded;
        }

        private void MemberSearchView_Loaded(object? sender, RoutedEventArgs e)
        {
            // WICHTIG:
            // DataContext kommt aus ViewModel-first Navigation (DataTemplate in App.xaml).
            // Wir erzeugen hier KEIN ViewModel mehr.
            if (_initialized) return;

            if (DataContext is MemberSearchViewModel vm)
            {
                _initialized = true;
                _ = vm.InitializeAsync();
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not GridViewColumnHeader header) return;

            var prop = header.Tag as string;
            if (string.IsNullOrEmpty(prop)) return;

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(ResultsListView.ItemsSource);
            if (view == null) return;

            // Toggle direction if same column
            var direction = System.ComponentModel.ListSortDirection.Ascending;
            if (string.Equals(_lastSortProperty, prop, StringComparison.OrdinalIgnoreCase))
            {
                direction = _lastSortDirection == System.ComponentModel.ListSortDirection.Ascending
                    ? System.ComponentModel.ListSortDirection.Descending
                    : System.ComponentModel.ListSortDirection.Ascending;
            }

            view.SortDescriptions.Clear();

            // Für Name sortieren: Nachname + Vorname
            if (string.Equals(prop, "Nachname", StringComparison.OrdinalIgnoreCase))
            {
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("Nachname", direction));
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("Vorname", direction));
            }
            else
            {
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription(prop, direction));
            }

            _lastSortProperty = prop;
            _lastSortDirection = direction;
        }

        private void ResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // MVVM-Trigger: Command ausführen
            if (DataContext is not MemberSearchViewModel vm) return;

            if (vm.SelectCommand.CanExecute(vm.SelectedResult))
                vm.SelectCommand.Execute(vm.SelectedResult);
        }
    }
}