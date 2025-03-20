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
        
        // Replace number + degF/degC with proper symbols
        result = Regex.Replace(result, @"(\d+)\s*degF", $"$1{NARROW_NBSP}{DEGREE}F");
        result = Regex.Replace(result, @"(\d+)\s*degC", $"$1{NARROW_NBSP}{DEGREE}C");

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
    
    [GeneratedRegex(@"\b\d+\.\d+\b")]
    private static partial Regex DecimalNumberPattern();

    public static string ConvertToCookingFractions(string input)
    {
        return DecimalNumberPattern().Replace(input, match => 
        {
            if (double.TryParse(match.Value, out double number))
            {
                return DecimalToCookingFraction(number);
            }
            return match.Value;
        });
    }

    private static string DecimalToCookingFraction(double number)
    {
        // Extract whole number part
        var wholeNumber = (int)Math.Floor(number);
        var fraction = number - wholeNumber;
    
        // If fraction is very small, just return the whole number
        if (fraction < 0.0625) // 1/16
            return wholeNumber > 0 ? wholeNumber.ToString() : "0";
    
        // Common cooking fractions denominators: 2, 3, 4, 8
        // Mapping of decimal ranges to fractions
        var fractionMap = new Dictionary<(double, double), string>
        {
            // Close to 1/8
            { (0.0625, 0.1875), "1/8" },
            // Close to 1/4
            { (0.1875, 0.3125), "1/4" },
            // Close to 1/3
            { (0.3125, 0.375), "1/3" },
            // Close to 3/8
            { (0.375, 0.4375), "3/8" },
            // Close to 1/2
            { (0.4375, 0.5625), "1/2" },
            // Close to 5/8
            { (0.5625, 0.6251), "5/8" },  // Slightly adjusted upper bound
            // Close to 2/3
            { (0.6251, 0.6875), "2/3" },  // Adjusted lower bound
            // Close to 3/4
            { (0.6875, 0.8125), "3/4" },
            // Close to 7/8
            { (0.8125, 0.9375), "7/8" },
            // Close to 1
            { (0.9375, 1.0), "1" }
        };
    
        var fractionString = "";
        foreach (var range in fractionMap)
        {
            if (!(fraction >= range.Key.Item1) || !(fraction < range.Key.Item2)) 
                continue;
            
            fractionString = range.Value;
            break;
        }
    
        // Return the appropriate format based on whole number and fraction
        
        if (wholeNumber > 0 && fractionString != "0" && fractionString != "1")
            return $"{wholeNumber} {fractionString}";
        
        if (wholeNumber > 0 || fractionString == "1")
            return (wholeNumber + (fractionString == "1" ? 1 : 0)).ToString();

        return fractionString;
    }

    public static string NormalizeUnits(string input)
    {
        var units = new Dictionary<List<string>, string>
        {
            { ["tsp"], "teaspoons" },
            { ["tbsp", "Tbs"], "tablespoons" },
            { ["oz"], "ounces" },
            { ["lb"], "pound" },
            { ["kg"], "kilograms" },
            { ["ml"], "milliliters" },
        };

        foreach (var (needles, replacement) in units)
        {
            input = needles.Aggregate(input, (current, needle) => current.Replace(needle, replacement));
        }
        
        return input;
    }

    public static string UnicodeToAsciiFractions(string input)
    {
        input = input.Replace("½", "1/2");
        input = input.Replace("⅓", "1/3");
        input = input.Replace("⅔", "2/3");
        input = input.Replace("¼", "1/4");
        input = input.Replace("¾", "3/4");
        input = input.Replace("⅕", "1/5");
        input = input.Replace("⅖", "2/5");
        input = input.Replace("⅗", "3/5");
        input = input.Replace("⅘", "4/5");
        input = input.Replace("⅙", "1/6");
        input = input.Replace("⅚", "5/6");
        input = input.Replace("⅐", "1/7");
        input = input.Replace("⅛", "1/8");
        input = input.Replace("⅜", "3/8");
        input = input.Replace("⅝", "5/8");
        input = input.Replace("⅞", "7/8");
        input = input.Replace("⅑", "1/9");
        input = input.Replace("⅒", "1/10");
        
        return input;
    }
}