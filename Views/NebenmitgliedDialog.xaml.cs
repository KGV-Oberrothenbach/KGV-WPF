using System.Windows;

namespace KGV.Views
{
    public partial class NebenmitgliedDialog : Window
    {
        public string Vorname => VornameTextBox.Text;
        public string Nachname => NachnameTextBox.Text;
        public bool AdresseUebernehmen => AdresseUebernehmenCheckBox.IsChecked == true;

        public NebenmitgliedDialog()
        {
            InitializeComponent();
        }

        public void SetInitialValues(string? vorname, string? nachname, bool adresseUebernehmen = true)
        {
            VornameTextBox.Text = vorname ?? string.Empty;
            NachnameTextBox.Text = nachname ?? string.Empty;
            AdresseUebernehmenCheckBox.IsChecked = adresseUebernehmen;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
