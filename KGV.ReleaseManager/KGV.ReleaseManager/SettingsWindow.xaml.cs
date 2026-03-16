using System.Windows;
using KGV.ReleaseManager.Models;

namespace KGV.ReleaseManager;

public partial class SettingsWindow : Window
{
    public ReleaseManagerSettings Settings { get; }

    public SettingsWindow(ReleaseManagerSettings settings)
    {
        InitializeComponent();
        Settings = settings ?? ReleaseManagerSettings.CreateDefaults();
        DataContext = Settings;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
