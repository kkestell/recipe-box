namespace RecipeBox;

public abstract record Quantity;

public record ExactQuantity(int WholeNumber, int Numerator, int Denominator) : Quantity
{
    public ExactQuantity(int value) : this(value, 0, 1) { }

    public override string ToString()
    {
        if (Numerator == 0)
            return WholeNumber.ToString();
        if (WholeNumber == 0)
            return $"{Numerator}/{Denominator}";
        return $"{WholeNumber} {Numerator}/{Denominator}";
    }
}

public record RangeQuantity(Quantity Min, Quantity Max) : Quantity
{
    public override string ToString() => $"{Min}-{Max}";
}

public record Amount(Quantity Quantity, string Unit)
{
    public override string ToString() => $"{Quantity} {Unit}";
}

public record Ingredient(Amount? Amount, Amount? AltAmount, string Name, string? Note)
{
    public override string ToString()
    {
        var parts = new List<string>();
        
        if (Amount != null)
            parts.Add(Amount.ToString());
            
        if (AltAmount != null)
            parts.Add($"({AltAmount})");
            
        parts.Add(Name);
        
        if (Note != null)
            parts.Add($", {Note}");
            
        return string.Join(" ", parts);
    }
}

public record Step
{
    public required List<string> Paragraphs { get; init; } = [];
    public required List<Ingredient> Ingredients { get; init; } = [];
}

public record Section
{
    public string? Title { get; init; }
    public required List<Step> Steps { get; init; } = [];
}

public record Recipe
{
    public required string Title { get; init; } = string.Empty;
    public required string? Description { get; init; } = string.Empty;
    public required Dictionary<string, string> Meta { get; init; } = new();
    public required List<Section> Sections { get; init; } = [];
}

public class SyntaxError(string message, int lineNumber) : Exception(message)
{
    public int LineNumber { get; } = lineNumber;
}