using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KGV.Maui.State;

public sealed class AppStatusState : INotifyPropertyChanged
{
    private string _updateStatusText = string.Empty;

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set
        {
            if (_updateStatusText == value) return;
            _updateStatusText = value;
            OnPropertyChanged();
        }
    }

    public string VersionText { get; }

    public AppStatusState()
    {
        var v = (AppInfo.Current?.VersionString ?? string.Empty).Trim();
        VersionText = string.IsNullOrWhiteSpace(v) ? "Version ?" : $"Version {v}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
