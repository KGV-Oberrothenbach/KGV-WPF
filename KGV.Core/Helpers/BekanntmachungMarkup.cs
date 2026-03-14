using System;
using System.Net;
using System.Text.RegularExpressions;

namespace KGV.Core.Helpers;

public static class BekanntmachungMarkup
{
    public const string BoldOpen = "{{b}}";
    public const string BoldClose = "{{/b}}";
    public const string ItalicOpen = "{{i}}";
    public const string ItalicClose = "{{/i}}";
    public const string FontSizeClose = "{{/fs}}";

    public static string WrapSelection(string text, int selectionStart, int selectionLength, string open, string close)
    {
        text ??= string.Empty;
        if (selectionStart < 0 || selectionStart > text.Length) return text;
        if (selectionLength <= 0) return text;
        if (selectionStart + selectionLength > text.Length) return text;

        return text.Substring(0, selectionStart)
               + open
               + text.Substring(selectionStart, selectionLength)
               + close
               + text.Substring(selectionStart + selectionLength);
    }

    public static string WrapSelectionFontSize(string text, int selectionStart, int selectionLength, int fontSize)
        => WrapSelection(text, selectionStart, selectionLength, $"{{{{fs:{fontSize}}}}}", FontSizeClose);

    public static string ToHtml(string? editorTextWithMarkers, int defaultFontSizePx)
    {
        var text = (editorTextWithMarkers ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        var encoded = WebUtility.HtmlEncode(text);

        // Newlines -> <br/>
        encoded = encoded.Replace("\n", "<br/>", StringComparison.Ordinal);

        // Marker -> Tags (encoded contains markers unchanged)
        encoded = encoded
            .Replace(BoldOpen, "<b>", StringComparison.Ordinal)
            .Replace(BoldClose, "</b>", StringComparison.Ordinal)
            .Replace(ItalicOpen, "<i>", StringComparison.Ordinal)
            .Replace(ItalicClose, "</i>", StringComparison.Ordinal)
            .Replace(FontSizeClose, "</span>", StringComparison.Ordinal);

        // {{fs:18}} -> <span style="font-size:18px">
        encoded = Regex.Replace(
            encoded,
            "\\{\\{fs:(\\d+)\\}\\}",
            m => $"<span style=\"font-size:{m.Groups[1].Value}px\">",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var fs = defaultFontSizePx <= 0 ? 14 : defaultFontSizePx;
        return $"<p style=\"font-size:{fs}px\">{encoded}</p>";
    }

    public static string ToEditorTextWithMarkers(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var s = html;

        // Normalize line breaks
        s = Regex.Replace(s, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Font size spans
        s = Regex.Replace(
            s,
            "<span[^>]*font-size\\s*:\\s*(\\d+)px[^>]*>",
            m => $"{{{{fs:{m.Groups[1].Value}}}}}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, "</span>", FontSizeClose, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Bold/Italic
        s = Regex.Replace(s, "<(b|strong)[^>]*>", BoldOpen, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, "</(b|strong)>", BoldClose, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, "<(i|em)[^>]*>", ItalicOpen, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, "</(i|em)>", ItalicClose, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Remove container tags but keep markers
        s = Regex.Replace(s, "</?(p|div)[^>]*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Strip any remaining tags
        s = Regex.Replace(s, "<[^>]+>", string.Empty, RegexOptions.CultureInvariant);

        // Decode entities
        s = WebUtility.HtmlDecode(s);

        return s;
    }
}
