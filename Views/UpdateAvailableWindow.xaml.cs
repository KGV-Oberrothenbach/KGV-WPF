using System;
using System.Windows;

namespace KGV.Views
{
    public partial class UpdateAvailableWindow : Window
    {
        public UpdateAvailableWindow(string currentVersion, string onlineVersion, string? notes)
        {
            InitializeComponent();

            CurrentVersionRun.Text = currentVersion;
            OnlineVersionRun.Text = onlineVersion;

            NotesText.Text = string.IsNullOrWhiteSpace(notes)
                ? ""
                : notes;

            NotesText.Visibility = string.IsNullOrWhiteSpace(NotesText.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public UpdateAvailableWindow(Version currentVersion, Version onlineVersion, string? notes)
        {
            InitializeComponent();

            CurrentVersionRun.Text = currentVersion.ToString();
            OnlineVersionRun.Text = onlineVersion.ToString();

            NotesText.Text = string.IsNullOrWhiteSpace(notes)
                ? ""
                : notes;

            NotesText.Visibility = string.IsNullOrWhiteSpace(NotesText.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
