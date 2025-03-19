using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RecipeBox.Core;

public static partial class TextUtils
{
    private const string MULTIPLICATION_SIGN = "\u00D7";
    private const string DOUBLE_PRIME = "\u2033";
    private const string FRACTION_SLASH = "\u2044";
    private const string DEGREE = "\u00B0";
    private const string EN_DASH = "\u2013";
    private const string ELLIPSIS = "\u2026";
    private const string NARROW_NBSP = "\u202F";
    private const string APOSTROPHE = "\u2019";

    [GeneratedRegex(@"\d+""(?:\s*x\s*\d+"")+")]
    private static partial Regex DimensionPattern();

    [GeneratedRegex(@"\s*x\s*")]
    private static partial Regex DimensionSplitPattern();

    [GeneratedRegex(@"(\d+)""")]
    private static partial Regex InchPattern();

    [GeneratedRegex(@"(\d+)/(\d+)")]
    private static partial Regex FractionPattern();

    [GeneratedRegex(@"(\d+)\s+(\d+\u2044\d+)")]
    private static partial Regex MixedNumberPattern();

    [GeneratedRegex(@"(\d+)\s*\u00B0F")]
    private static partial Regex TemperaturePattern();

    [GeneratedRegex(@"\d+(?:-\d+)+")]
    private static partial Regex NumberRangePattern();

    public static string Pretty(object text)
    {
        var result = text.ToString();

        // Replace dimension notation (e.g., 24" x 36")
        result = DimensionPattern().Replace(result, match =>
        {
            var chunk = match.Value;
            var parts = DimensionSplitPattern().Split(chunk);
            parts = parts.Select(p => InchPattern().Replace(p, $"$1{DOUBLE_PRIME}")).ToArray();
            return string.Join($"{NARROW_NBSP}{MULTIPLICATION_SIGN}{NARROW_NBSP}", parts);
        });

        // Replace inch marks with double primes
        result = InchPattern().Replace(result, $"$1{DOUBLE_PRIME}");

        // Replace ASCII fraction slashes with proper fraction slashes
        result = FractionPattern().Replace(result, $"$1{FRACTION_SLASH}$2");

        // Replace spaces in mixed numbers with narrow non-breaking spaces
        result = MixedNumberPattern().Replace(result, $"$1{NARROW_NBSP}$2");

        // Ensure proper spacing in temperature
        result = TemperaturePattern().Replace(result, $"$1{NARROW_NBSP}{DEGREE}F");

        // Replace hyphens between numbers with en dashes
        result = NumberRangePattern().Replace(result, m => m.Value.Replace("-", EN_DASH));

        // Replace ASCII ellipsis with single character ellipsis
        result = result.Replace("...", ELLIPSIS);

        // Replace single quotes with apostrophes
        result = result.Replace("'", APOSTROPHE);

        return result;
    }
    
    [GeneratedRegex(@"[^\w\-]")]
    private static partial Regex NonAlphanumericPattern();
    
    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphenPattern();
    
    public static string Slugify(string text)
    {
        // Convert to lowercase
        text = text.ToLowerInvariant();
        
        // Convert accented characters to ASCII equivalents
        text = text.Normalize(NormalizationForm.FormKD);
        var chars = text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        text = new string(chars.ToArray());
        
        // Replace spaces with hyphens
        text = text.Replace(' ', '-');
        
        // Remove all non-alphanumeric characters (except hyphens)
        text = NonAlphanumericPattern().Replace(text, "");
        
        // Replace multiple consecutive hyphens with a single hyphen
        text = MultipleHyphenPattern().Replace(text, "-");
        
        // Remove leading and trailing hyphens
        text = text.Trim('-');
        
        return text;
    }    
}