using System.Text.RegularExpressions;

namespace UsaepaySupportTestbench.Services;

public sealed partial class RedactionService
{
    private const string Redacted = "***REDACTED***";

    public string Redact(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var redacted = JsonSensitiveKeyRegex().Replace(input, match =>
        {
            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            return $"\"{key}\": \"{MaskValue(value)}\"";
        });

        redacted = XmlSensitiveKeyRegex().Replace(redacted, match =>
        {
            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            return $"<{key}>{MaskValue(value)}</{key}>";
        });

        redacted = PanRegex().Replace(redacted, match => MaskPan(match.Value));

        return redacted;
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Redacted;
        }

        if (value.Any(char.IsDigit))
        {
            return MaskPan(value);
        }

        return Redacted;
    }

    private static string MaskPan(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 6)
        {
            return Redacted;
        }

        var last4 = digits[^4..];
        return new string('*', Math.Max(0, digits.Length - 4)) + last4;
    }

    [GeneratedRegex("\"(?<key>cardNumber|pan|cvv|cvc|securityCode|trackData|track1|track2|authorization|apiKey|apiSecret|sourceKey|pin|accessToken|paymentKey|token|softwareKey)\"\\s*:\\s*\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex JsonSensitiveKeyRegex();

    [GeneratedRegex("<(?<key>cardNumber|pan|cvv|cvc|securityCode|trackData|track1|track2|authorization|apiKey|apiSecret|sourceKey|pin|accessToken|paymentKey|token|softwareKey)>(?<value>[^<]*)</\\k<key>>", RegexOptions.IgnoreCase)]
    private static partial Regex XmlSensitiveKeyRegex();

    [GeneratedRegex("\\b\\d{12,19}\\b")]
    private static partial Regex PanRegex();
}
