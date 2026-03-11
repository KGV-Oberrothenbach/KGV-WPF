#if ANDROID
using Android.Content;
using Android.Nfc;
using Android.OS;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;

namespace KGV.Maui.Services;

public sealed class AndroidRfidScanService : Java.Lang.Object, IRfidScanService
{
    private NfcAdapter? _adapter;
    private bool _isListening;

    public event EventHandler<string>? TagScanned;

    public bool IsSupported
    {
        get
        {
            var activity = Platform.CurrentActivity;
            if (activity == null) return false;
            return NfcAdapter.GetDefaultAdapter(activity) != null;
        }
    }

    public bool IsEnabled
    {
        get
        {
            var activity = Platform.CurrentActivity;
            if (activity == null) return false;
            return NfcAdapter.GetDefaultAdapter(activity)?.IsEnabled == true;
        }
    }

    public void StartListening()
    {
        if (_isListening)
            return;

        var activity = Platform.CurrentActivity;
        if (activity == null)
            return;

        _adapter = NfcAdapter.GetDefaultAdapter(activity);
        if (_adapter == null || !_adapter.IsEnabled)
            return;

        var flags = NfcReaderFlags.NfcA
            | NfcReaderFlags.NfcB
            | NfcReaderFlags.NfcF
            | NfcReaderFlags.NfcV
            | NfcReaderFlags.SkipNdefCheck;

        _adapter.EnableReaderMode(activity, new ReaderCallback(OnTagDiscovered), flags, new Bundle());
        _isListening = true;
    }

    public void StopListening()
    {
        if (!_isListening)
            return;

        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            _isListening = false;
            return;
        }

        try
        {
            _adapter?.DisableReaderMode(activity);
        }
        catch
        {
            // ignore
        }
        finally
        {
            _isListening = false;
        }
    }

    public void OpenNfcSettings()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
            return;

        try
        {
            var intent = new Intent(Android.Provider.Settings.ActionNfcSettings);
            activity.StartActivity(intent);
        }
        catch
        {
            try
            {
                var intent = new Intent(Android.Provider.Settings.ActionWirelessSettings);
                activity.StartActivity(intent);
            }
            catch
            {
            }
        }
    }

    private void OnTagDiscovered(Tag tag)
    {
        try
        {
            var id = tag.GetId();
            if (id == null || id.Length == 0)
                return;

            var uid = BitConverter.ToString(id).Replace("-", string.Empty);
            MainThread.BeginInvokeOnMainThread(() => TagScanned?.Invoke(this, uid));
        }
        catch
        {
        }
    }

    private sealed class ReaderCallback : Java.Lang.Object, NfcAdapter.IReaderCallback
    {
        private readonly Action<Tag> _cb;

        public ReaderCallback(Action<Tag> cb) => _cb = cb;

        public void OnTagDiscovered(Tag? tag)
        {
            if (tag == null) return;
            _cb(tag);
        }
    }
}
#endif
