using System;
using System.Windows;

namespace KGV.Views
{
    public partial class DatumDialog : Window
    {
        public DateTime? Datum => DatumPicker.SelectedDate;

        public DatumDialog()
        {
            InitializeComponent();
            DatumPicker.SelectedDate = DateTime.Today;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
