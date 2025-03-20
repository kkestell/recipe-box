using RecipeBox.Core;

namespace RecipeBox.Tests;

[TestFixture]
public class TextUtilsTests
{
    private const string MULTIPLICATION_SIGN = "\u00D7";  // ×
    private const string DOUBLE_PRIME = "\u2033";         // ″
    private const string FRACTION_SLASH = "\u2044";       // ⁄
    private const string DEGREE = "\u00B0";               // °
    private const string EN_DASH = "\u2013";              // –
    private const string ELLIPSIS = "\u2026";             // …
    private const string NARROW_NBSP = "\u202F";          // narrow non-breaking space
    private const string APOSTROPHE = "\u2019";           // '

    #region Pretty Tests

    [Test]
    public void Pretty_NullInput_ThrowsException()
    {
        Assert.Throws<NullReferenceException>(() => TextUtils.Pretty(null));
    }

    [Test]
    public void Pretty_EmptyString_ReturnsEmptyString()
    {
        var result = TextUtils.Pretty("");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void Pretty_DimensionNotation_ReturnsFormattedDimensions()
    {
        var result = TextUtils.Pretty("24\" x 36\"");
        Assert.That(result, Contains.Substring("24" + DOUBLE_PRIME));
        Assert.That(result, Contains.Substring("36" + DOUBLE_PRIME));
        Assert.That(result, Contains.Substring(NARROW_NBSP + MULTIPLICATION_SIGN + NARROW_NBSP));
    }

    [Test]
    public void Pretty_TemperatureNotation_ReturnsFormattedTemperature()
    {
        var result = TextUtils.Pretty("350 degF");
        Assert.That(result, Is.EqualTo("350" + NARROW_NBSP + DEGREE + "F"));
        
        result = TextUtils.Pretty("175 degC");
        Assert.That(result, Is.EqualTo("175" + NARROW_NBSP + DEGREE + "C"));
    }

    [Test]
    public void Pretty_InchMarks_ReturnsDoubleprimes()
    {
        var result = TextUtils.Pretty("12\"");
        Assert.That(result, Is.EqualTo("12" + DOUBLE_PRIME));
    }

    [Test]
    public void Pretty_Fractions_ReturnsProperFractionSlash()
    {
        var result = TextUtils.Pretty("1/2");
        Assert.That(result, Is.EqualTo("1" + FRACTION_SLASH + "2"));
    }

    [Test]
    public void Pretty_MixedNumbers_ReturnsProperSpacing()
    {
        var result = TextUtils.Pretty("2 1" + FRACTION_SLASH + "2");
        Assert.That(result, Is.EqualTo("2" + NARROW_NBSP + "1" + FRACTION_SLASH + "2"));
    }

    [Test]
    public void Pretty_NumberRange_ReturnsEnDash()
    {
        var result = TextUtils.Pretty("10-20");
        Assert.That(result, Is.EqualTo("10" + EN_DASH + "20"));
    }

    [Test]
    public void Pretty_Ellipsis_ReturnsSingleCharacter()
    {
        var result = TextUtils.Pretty("and so on...");
        Assert.That(result, Is.EqualTo("and so on" + ELLIPSIS));
    }

    [Test]
    public void Pretty_Apostrophe_ReturnsProperApostrophe()
    {
        var result = TextUtils.Pretty("It's time");
        Assert.That(result, Is.EqualTo("It" + APOSTROPHE + "s time"));
    }

    [Test]
    public void Pretty_ComplexInput_ReturnsFullyFormattedText()
    {
        var input = "Cook at 350 degF for 10-15 minutes. Use a 9\" x 13\" pan and add 1 1/2 cups of flour...";
        var expected = "Cook at 350" + NARROW_NBSP + DEGREE + "F for 10" + EN_DASH + "15 minutes. Use a 9" + 
                      DOUBLE_PRIME + NARROW_NBSP + MULTIPLICATION_SIGN + NARROW_NBSP + "13" + DOUBLE_PRIME +
                      " pan and add 1" + NARROW_NBSP + "1" + FRACTION_SLASH + "2 cups of flour" + ELLIPSIS;
        
        var result = TextUtils.Pretty(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Slugify Tests

    [Test]
    public void Slugify_Null_ThrowsException()
    {
        Assert.Throws<NullReferenceException>(() => TextUtils.Slugify(null));
    }

    [Test]
    public void Slugify_EmptyString_ReturnsEmptyString()
    {
        var result = TextUtils.Slugify("");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void Slugify_ConvertToLowercase_ReturnsLowercaseString()
    {
        var result = TextUtils.Slugify("UPPERCASE TEXT");
        Assert.That(result, Is.EqualTo("uppercase-text"));
    }

    [Test]
    public void Slugify_AccentedCharacters_ReturnsNormalizedString()
    {
        var result = TextUtils.Slugify("Café Crème");
        Assert.That(result, Is.EqualTo("cafe-creme"));
    }

    [Test]
    public void Slugify_SpacesAndPunctuation_ReturnsHyphenatedString()
    {
        var result = TextUtils.Slugify("Hello, World! How are you?");
        Assert.That(result, Is.EqualTo("hello-world-how-are-you"));
    }

    [Test]
    public void Slugify_MultipleHyphens_ReturnsSingleHyphens()
    {
        var result = TextUtils.Slugify("multiple---hyphens");
        Assert.That(result, Is.EqualTo("multiple-hyphens"));
    }

    [Test]
    public void Slugify_LeadingAndTrailingHyphens_ReturnsTrimmedString()
    {
        var result = TextUtils.Slugify("-leading-and-trailing-");
        Assert.That(result, Is.EqualTo("leading-and-trailing"));
    }

    [Test]
    public void Slugify_ComplexInput_ReturnsCleanSlug()
    {
        var result = TextUtils.Slugify("  The BEST Chocolate Chip Cookies! (Grandma's Recipe) #delicious  ");
        Assert.That(result, Is.EqualTo("the-best-chocolate-chip-cookies-grandmas-recipe-delicious"));
    }

    #endregion

    #region ConvertToCookingFractions Tests

    [Test]
    public void ConvertToCookingFractions_EmptyString_ReturnsEmptyString()
    {
        var result = TextUtils.ConvertToCookingFractions("");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void ConvertToCookingFractions_NoDecimals_ReturnsOriginalString()
    {
        var result = TextUtils.ConvertToCookingFractions("This has no decimals");
        Assert.That(result, Is.EqualTo("This has no decimals"));
    }

    [Test]
    public void ConvertToCookingFractions_WholeNumbers_ReturnsWholeNumbers()
    {
        var result = TextUtils.ConvertToCookingFractions("Use 2.0 cups");
        Assert.That(result, Is.EqualTo("Use 2 cups"));
    }

    [Test]
    public void ConvertToCookingFractions_SmallFractions_HandlesApproximation()
    {
        var result = TextUtils.ConvertToCookingFractions("0.05");
        Assert.That(result, Is.EqualTo("0"));
    }

    [TestCase("0.125", "1/8")]
    [TestCase("0.25", "1/4")]
    [TestCase("0.333", "1/3")]
    [TestCase("0.375", "3/8")]
    [TestCase("0.5", "1/2")]
    [TestCase("0.625", "5/8")]
    [TestCase("0.667", "2/3")]
    [TestCase("0.75", "3/4")]
    [TestCase("0.875", "7/8")]
    public void ConvertToCookingFractions_CommonFractions_ReturnsExpectedFraction(string input, string expected)
    {
        var result = TextUtils.ConvertToCookingFractions(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("1.125", "1 1/8")]
    [TestCase("1.25", "1 1/4")]
    [TestCase("1.5", "1 1/2")]
    [TestCase("1.75", "1 3/4")]
    [TestCase("2.333", "2 1/3")]
    [TestCase("3.667", "3 2/3")]
    public void ConvertToCookingFractions_MixedNumbers_ReturnsExpectedMixedNumber(string input, string expected)
    {
        var result = TextUtils.ConvertToCookingFractions(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ConvertToCookingFractions_ComplexInput_ConvertAllDecimals()
    {
        var result = TextUtils.ConvertToCookingFractions("Use 2.5 cups of flour and 0.75 cups of sugar");
        Assert.That(result, Is.EqualTo("Use 2 1/2 cups of flour and 3/4 cups of sugar"));
    }

    [Test]
    public void ConvertToCookingFractions_NearlyWholeNumber_ReturnsWholeNumber()
    {
        var result = TextUtils.ConvertToCookingFractions("3.99");
        Assert.That(result, Is.EqualTo("4"));
    }

    #endregion

    #region NormalizeUnits Tests

    [Test]
    public void NormalizeUnits_Null_ThrowsException()
    {
        Assert.Throws<NullReferenceException>(() => TextUtils.NormalizeUnits(null));
    }

    [Test]
    public void NormalizeUnits_EmptyString_ReturnsEmptyString()
    {
        var result = TextUtils.NormalizeUnits("");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void NormalizeUnits_NoUnitMatches_ReturnsOriginalString()
    {
        var result = TextUtils.NormalizeUnits("This has no unit abbreviations");
        Assert.That(result, Is.EqualTo("This has no unit abbreviations"));
    }

    [Test]
    public void NormalizeUnits_TeaspoonAbbreviation_ReturnsFullUnitName()
    {
        var result = TextUtils.NormalizeUnits("1 tsp salt");
        Assert.That(result, Is.EqualTo("1 teaspoons salt"));
    }

    [Test]
    public void NormalizeUnits_TablespoonAbbreviations_ReturnsFullUnitName()
    {
        var result = TextUtils.NormalizeUnits("2 tbsp oil");
        Assert.That(result, Is.EqualTo("2 tablespoons oil"));
        
        result = TextUtils.NormalizeUnits("2 Tbs oil");
        Assert.That(result, Is.EqualTo("2 tablespoons oil"));
    }

    [Test]
    public void NormalizeUnits_OunceAbbreviation_ReturnsFullUnitName()
    {
        var result = TextUtils.NormalizeUnits("8 oz water");
        Assert.That(result, Is.EqualTo("8 ounces water"));
    }

    [Test]
    public void NormalizeUnits_PoundAbbreviation_ReturnsFullUnitName()
    {
        var result = TextUtils.NormalizeUnits("1 lb ground beef");
        Assert.That(result, Is.EqualTo("1 pound ground beef"));
    }

    [Test]
    public void NormalizeUnits_KilogramAbbreviation_ReturnsFullUnitName()
    {
        var result = TextUtils.NormalizeUnits("2 kg flour");
        Assert.That(result, Is.EqualTo("2 kilograms flour"));
    }

    [Test]
    public void NormalizeUnits_MilliliterAbbreviation_ReturnsFullUnitName()
    {
        var result = TextUtils.NormalizeUnits("250 ml milk");
        Assert.That(result, Is.EqualTo("250 milliliters milk"));
    }

    [Test]
    public void NormalizeUnits_MultipleUnits_NormalizesAllUnits()
    {
        var result = TextUtils.NormalizeUnits("Mix 2 tbsp oil with 1 tsp salt and 8 oz water");
        Assert.That(result, Is.EqualTo("Mix 2 tablespoons oil with 1 teaspoons salt and 8 ounces water"));
    }

    [Test]
    public void NormalizeUnits_MultipleInstancesOfSameUnit_NormalizesAll()
    {
        var result = TextUtils.NormalizeUnits("2 tbsp sugar, 1 tbsp vanilla, and 3 tbsp butter");
        Assert.That(result, Is.EqualTo("2 tablespoons sugar, 1 tablespoons vanilla, and 3 tablespoons butter"));
    }

    #endregion

    #region UnicodeToAsciiFractions Tests

    [Test]
    public void UnicodeToAsciiFractions_Null_ThrowsException()
    {
        Assert.Throws<NullReferenceException>(() => TextUtils.UnicodeToAsciiFractions(null));
    }

    [Test]
    public void UnicodeToAsciiFractions_EmptyString_ReturnsEmptyString()
    {
        var result = TextUtils.UnicodeToAsciiFractions("");
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void UnicodeToAsciiFractions_NoFractions_ReturnsOriginalString()
    {
        var result = TextUtils.UnicodeToAsciiFractions("This has no fractions");
        Assert.That(result, Is.EqualTo("This has no fractions"));
    }

    [TestCase("½", "1/2")]
    [TestCase("⅓", "1/3")]
    [TestCase("⅔", "2/3")]
    [TestCase("¼", "1/4")]
    [TestCase("¾", "3/4")]
    [TestCase("⅕", "1/5")]
    [TestCase("⅖", "2/5")]
    [TestCase("⅗", "3/5")]
    [TestCase("⅘", "4/5")]
    [TestCase("⅙", "1/6")]
    [TestCase("⅚", "5/6")]
    [TestCase("⅐", "1/7")]
    [TestCase("⅛", "1/8")]
    [TestCase("⅜", "3/8")]
    [TestCase("⅝", "5/8")]
    [TestCase("⅞", "7/8")]
    [TestCase("⅑", "1/9")]
    [TestCase("⅒", "1/10")]
    public void UnicodeToAsciiFractions_SingleFraction_ReturnsAsciiEquivalent(string input, string expected)
    {
        var result = TextUtils.UnicodeToAsciiFractions(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void UnicodeToAsciiFractions_MultipleFractions_ConvertAllFractions()
    {
        var result = TextUtils.UnicodeToAsciiFractions("Mix ½ cup flour with ¼ cup sugar and ⅛ teaspoon salt");
        Assert.That(result, Is.EqualTo("Mix 1/2 cup flour with 1/4 cup sugar and 1/8 teaspoon salt"));
    }

    [Test]
    public void UnicodeToAsciiFractions_MixedWithText_OnlyConvertsFractions()
    {
        var result = TextUtils.UnicodeToAsciiFractions("This recipe needs ¾ cup of butter and ⅔ cup of sugar.");
        Assert.That(result, Is.EqualTo("This recipe needs 3/4 cup of butter and 2/3 cup of sugar."));
    }

    #endregion
}
