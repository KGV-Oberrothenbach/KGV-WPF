using System.Windows.Controls;
using System.Windows.Input;
using KGV.Core.Helpers;

namespace KGV.Views
{
    public partial class TermineVerwaltungView : UserControl
    {
        public TermineVerwaltungView()
        {
            InitializeComponent();
        }

        private void OnTimeLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is not ComboBox cb)
                return;

            var raw = (cb.Text ?? string.Empty).Trim();
            if (!TimeText.TryNormalize(raw, out var norm))
                return;

            // empty is allowed -> null; keep UI empty, but normalize valid input
            cb.Text = norm ?? string.Empty;
        }
    }
}
