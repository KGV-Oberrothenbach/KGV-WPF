using KGV.Core.Helpers;
using System;
using System.Windows.Controls;

namespace KGV.Views
{
    public partial class BekanntmachungenVerwaltungView : UserControl
    {
        public BekanntmachungenVerwaltungView()
        {
            InitializeComponent();
        }

        private void OnApplyBoldToSelection(object sender, System.Windows.RoutedEventArgs e)
            => ApplyWrapToSelection(BekanntmachungMarkup.BoldOpen, BekanntmachungMarkup.BoldClose);

        private void OnApplyItalicToSelection(object sender, System.Windows.RoutedEventArgs e)
            => ApplyWrapToSelection(BekanntmachungMarkup.ItalicOpen, BekanntmachungMarkup.ItalicClose);

        private void OnApplyFontSizeToSelection(object sender, System.Windows.RoutedEventArgs e)
        {
            if (InhaltTextBox == null || FontSizeCombo == null)
                return;

            var fs = 14;
            if (FontSizeCombo.SelectedItem is ComboBoxItem cbi
                && int.TryParse(cbi.Content?.ToString(), out var parsed)
                && parsed > 0)
            {
                fs = parsed;
            }

            ApplyWrapToSelection($"{{{{fs:{fs}}}}}", BekanntmachungMarkup.FontSizeClose);
        }

        private void ApplyWrapToSelection(string open, string close)
        {
            if (InhaltTextBox == null)
                return;

            var start = InhaltTextBox.SelectionStart;
            var len = InhaltTextBox.SelectionLength;
            if (len <= 0)
                return;

            var text = InhaltTextBox.Text ?? string.Empty;
            var updated = BekanntmachungMarkup.WrapSelection(text, start, len, open, close);
            if (ReferenceEquals(updated, text) || string.Equals(updated, text, StringComparison.Ordinal))
                return;

            InhaltTextBox.Text = updated;
            InhaltTextBox.SelectionStart = start + open.Length;
            InhaltTextBox.SelectionLength = len;
            InhaltTextBox.Focus();
        }
    }
}
