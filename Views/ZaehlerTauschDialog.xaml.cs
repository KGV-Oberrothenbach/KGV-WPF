using System;
using System.Windows;

namespace KGV.Views
{
    public partial class ZaehlerTauschDialog : Window
    {
        public string Zaehlernummer => ZaehlernummerTextBox.Text;
        public DateTime? Eichdatum => EichdatumPicker.SelectedDate;
        public DateTime? EingebautAm => EinbauAmPicker.SelectedDate;

        public ZaehlerTauschDialog()
        {
            InitializeComponent();
            EinbauAmPicker.SelectedDate = DateTime.Today;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
