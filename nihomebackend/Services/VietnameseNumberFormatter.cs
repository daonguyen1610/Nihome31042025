using System.Globalization;

namespace NihomeBackend.Services;

/// <summary>
/// Converts a monetary <see cref="decimal"/> into Vietnamese words —
/// used for the "tổng tiền viết bằng chữ" line on quote / contract PDFs
/// (spec NIH-84 AC #3, NIH-87). Handles values up to trillions and
/// rounds to whole VND (decimals discarded).
/// </summary>
public static class VietnameseNumberFormatter
{
    private static readonly string[] Digits =
    {
        "không", "một", "hai", "ba", "bốn",
        "năm", "sáu", "bảy", "tám", "chín",
    };

    /// <summary>Groups of 3 digits, from the smallest (units) upward.</summary>
    private static readonly string[] GroupSuffix =
    {
        string.Empty, "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ",
    };

    public static string ToWords(decimal amount, string currencySuffix = "đồng")
    {
        var value = (long)Math.Truncate(Math.Abs(amount));
        var sign = amount < 0 ? "âm " : string.Empty;
        var words = ConvertInteger(value);
        if (!string.IsNullOrWhiteSpace(currencySuffix))
        {
            words = $"{words} {currencySuffix}";
        }
        var result = sign + words;
        return char.ToUpper(result[0], CultureInfo.InvariantCulture) + result[1..];
    }

    private static string ConvertInteger(long value)
    {
        if (value == 0) return Digits[0];

        var groups = new List<int>();
        while (value > 0)
        {
            groups.Add((int)(value % 1000));
            value /= 1000;
        }

        var parts = new List<string>();
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            var g = groups[i];
            if (g == 0) continue;
            var text = ReadTriplet(g, isLeadingGroup: parts.Count == 0);
            if (!string.IsNullOrWhiteSpace(GroupSuffix[i]))
            {
                text = $"{text} {GroupSuffix[i]}";
            }
            parts.Add(text);
        }
        return string.Join(' ', parts).Trim();
    }

    private static string ReadTriplet(int n, bool isLeadingGroup)
    {
        int hundreds = n / 100;
        int tens = (n % 100) / 10;
        int units = n % 10;

        var sb = new System.Text.StringBuilder();

        if (hundreds > 0)
        {
            sb.Append($"{Digits[hundreds]} trăm");
        }
        else if (!isLeadingGroup && (tens > 0 || units > 0))
        {
            sb.Append("không trăm");
        }

        if (tens == 0)
        {
            if (units > 0)
            {
                if (sb.Length > 0) sb.Append(" lẻ ");
                sb.Append(Digits[units]);
            }
        }
        else if (tens == 1)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append("mười");
            if (units == 5) sb.Append(" lăm");
            else if (units > 0) sb.Append(' ').Append(Digits[units]);
        }
        else
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append($"{Digits[tens]} mươi");
            if (units == 1) sb.Append(" mốt");
            else if (units == 5) sb.Append(" lăm");
            else if (units > 0) sb.Append(' ').Append(Digits[units]);
        }

        return sb.ToString().Trim();
    }
}
