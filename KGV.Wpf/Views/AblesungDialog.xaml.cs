using System;
using System.Globalization;
using System.Windows;

namespace KGV.Views
{
    public partial class AblesungDialog : Window
    {
        public DateTime? Ablesedatum => AblesedatumPicker.SelectedDate;
        public string FotoPfad => FotoTextBox.Text;

        public decimal? Stand
        {
            get
            {
                if (decimal.TryParse(StandTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v))
                    return v;
                return null;
            }
        }

        public AblesungDialog()
        {
            InitializeComponent();
            AblesedatumPicker.SelectedDate = DateTime.Today;
        }

        public void SetInitialValues(DateTime ablesedatum, decimal stand, string? fotoPfad)
        {
            AblesedatumPicker.SelectedDate = ablesedatum.Date;
            StandTextBox.Text = stand.ToString(CultureInfo.CurrentCulture);
            FotoTextBox.Text = fotoPfad ?? string.Empty;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
