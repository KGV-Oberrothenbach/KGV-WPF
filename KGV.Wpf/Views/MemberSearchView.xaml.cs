// Datei: KGV.Wpf/Views/MemberSearchView.xaml.cs

using System.Windows.Controls;
using System.Windows.Input;
using KGV.Wpf.ViewModels;

namespace KGV.Views
{
    public partial class MemberSearchView : UserControl
    {
        public MemberSearchView()
        {
            InitializeComponent();
        }

        // Muss exakt so heißen wie in XAML: MouseDoubleClick="ResultsListView_MouseDoubleClick"
        private void ResultsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelectCommand();
        }

        // Muss exakt so heißen wie in XAML: KeyDown="ResultsListView_KeyDown"
        private void ResultsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteSelectCommand();
                e.Handled = true;
            }
        }

        private void ExecuteSelectCommand()
        {
            if (DataContext is not MemberSearchViewModel vm) return;

            var param = vm.SelectedResult;
            if (param == null) return;

            if (vm.SelectCommand.CanExecute(param))
                vm.SelectCommand.Execute(param);
        }
    }
}