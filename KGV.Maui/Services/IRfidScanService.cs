namespace KGV.Maui.Services;

public interface IRfidScanService
{
    bool IsSupported { get; }
    bool IsEnabled { get; }

    event EventHandler<string>? TagScanned;

    void StartListening();
    void StopListening();

    void OpenNfcSettings();
}
