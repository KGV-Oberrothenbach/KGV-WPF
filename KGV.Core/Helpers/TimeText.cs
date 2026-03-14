using System.Linq;

namespace KGV.Core.Helpers;

public static class TimeText
{
    public static bool TryNormalize(string? input, out string? normalized)
    {
        normalized = null;

        var raw = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return true; // empty is allowed -> null

        raw = raw.Replace(',', ':');

        // allow stray characters like "\\" etc. -> treat as invalid
        // keep only digits and ':' for parsing
        var hasColon = raw.Contains(':');

        if (hasColon)
        {
            var parts = raw.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Length > 2)
                return false;

            if (!int.TryParse(parts[0], out var h))
                return false;

            var m = 0;
            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[1], out m))
                    return false;
            }

            if (h < 0 || h > 23) return false;
            if (m < 0 || m > 59) return false;

            normalized = $"{h:00}:{m:00}";
            return true;
        }

        // digits-only formats: 9, 09, 930, 0930, 1330
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length != raw.Length)
            return false;

        int hours;
        int minutes;

        if (digits.Length is 1 or 2)
        {
            if (!int.TryParse(digits, out hours)) return false;
            minutes = 0;
        }
        else if (digits.Length == 3)
        {
            // HMM
            if (!int.TryParse(digits[..1], out hours)) return false;
            if (!int.TryParse(digits[1..], out minutes)) return false;
        }
        else if (digits.Length == 4)
        {
            // HHMM
            if (!int.TryParse(digits[..2], out hours)) return false;
            if (!int.TryParse(digits[2..], out minutes)) return false;
        }
        else
        {
            return false;
        }

        if (hours < 0 || hours > 23) return false;
        if (minutes < 0 || minutes > 59) return false;

        normalized = $"{hours:00}:{minutes:00}";
        return true;
    }

    public static IReadOnlyList<string> BuildHalfHourOptions()
    {
        var list = new List<string>(48);
        for (var h = 0; h < 24; h++)
        {
            list.Add($"{h:00}:00");
            list.Add($"{h:00}:30");
        }
        return list;
    }
}
