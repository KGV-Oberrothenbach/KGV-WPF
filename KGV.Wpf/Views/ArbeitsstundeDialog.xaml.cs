using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using KGV.Core.Models;

namespace KGV.Views
{
    public partial class ArbeitsstundeDialog : Window
    {
        public bool DeleteRequested { get; private set; }

        public int? SelectedMitgliedId
        {
            get
            {
                if (MitgliedComboBox.SelectedValue is int i) return i;
                return null;
            }
        }

        public int? SelectedSaisonId
        {
            get
            {
                if (SaisonComboBox.SelectedValue is int i) return i;
                return null;
            }
        }

        public DateTime? Datum => DatumPicker.SelectedDate;

        public decimal? Stunden
        {
            get
            {
                if (decimal.TryParse(StundenTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v))
                    return v;
                return null;
            }
        }

        public string? Beschreibung => BeschreibungTextBox.Text;

        public bool Freigegeben => FreigegebenCheckBox.IsChecked == true;

        public ArbeitsstundeDialog()
        {
            InitializeComponent();
            DatumPicker.SelectedDate = DateTime.Today;
        }

        public void SetOptions(IReadOnlyList<MemberDTO> mitglieder, IReadOnlyList<SaisonRecord> saisons)
        {
            MitgliedComboBox.ItemsSource = mitglieder;
            SaisonComboBox.ItemsSource = saisons;
        }

        public void SetInitialValues(int? memberId, int? saisonId, DateTime? datum, decimal? stunden, string? beschreibung)
        {
            if (memberId.HasValue)
                MitgliedComboBox.SelectedValue = memberId.Value;

            if (saisonId.HasValue)
                SaisonComboBox.SelectedValue = saisonId.Value;

            DatumPicker.SelectedDate = (datum ?? DateTime.Today).Date;

            StundenTextBox.Text = stunden.HasValue
                ? stunden.Value.ToString(CultureInfo.CurrentCulture)
                : string.Empty;

            BeschreibungTextBox.Text = beschreibung ?? string.Empty;
        }

        public void SetFreigabeMode(bool canApprove, bool defaultFreigegeben)
        {
            FreigegebenCheckBox.IsChecked = defaultFreigegeben;
            FreigegebenCheckBox.IsEnabled = canApprove;
        }

        public void SetDeleteEnabled(bool enabled)
        {
            DeleteButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Datensatz wirklich löschen?", "Bestätigung", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes)
                return;

            DeleteRequested = true;
            DialogResult = true;
        }
    }
}
