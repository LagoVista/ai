using System;
using System.Text.RegularExpressions;

public static class DdrIdExtensions
{
    private static readonly Regex DdrRegex =
        new Regex(@"^([A-Z]{3})-(\d{1,6})$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string NormalizeDdrid(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Identifier cannot be empty.", nameof(input));

        input = input.Trim();

        var match = DdrRegex.Match(input);
        if (!match.Success)
            throw new FormatException($"Invalid identifier format: '{input}'.");

        var prefix = match.Groups[1].Value.ToUpperInvariant();
        var number = int.Parse(match.Groups[2].Value); // safe due to regex

        return $"{prefix}-{number:D6}";
    }
}
