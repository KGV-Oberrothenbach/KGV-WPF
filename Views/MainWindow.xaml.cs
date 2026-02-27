using System.Windows;
using KGV.ViewModels;

namespace KGV.Views
{
    public partial class MainWindow : Window
    {
        // Designer-Konstruktor
        public MainWindow()
        {
            InitializeComponent();
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = new MainWindowViewModel(null!, null!, null!);
            }
        }

        // DI-Konstruktor
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        }
    }
}