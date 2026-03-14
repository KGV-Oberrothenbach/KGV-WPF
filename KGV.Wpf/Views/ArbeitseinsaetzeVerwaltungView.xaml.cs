using System.Windows.Controls;
using System.Windows.Input;
using KGV.Core.Helpers;

namespace KGV.Views
{
    public partial class ArbeitseinsaetzeVerwaltungView : UserControl
    {
        public ArbeitseinsaetzeVerwaltungView()
        {
            InitializeComponent();
        }

        private void OnTimeLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is not TextBox tb)
                return;

            var raw = (tb.Text ?? string.Empty).Trim();
            if (!TimeText.TryNormalize(raw, out var norm))
                return;

            tb.Text = norm ?? string.Empty;
        }
    }
}
