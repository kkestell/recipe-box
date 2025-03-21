﻿namespace RecipeBox.Core;

public static class RecipeParser
{
    private static readonly List<string> AllowedUnits = new()
    {
        "cup", "cups",
        "teaspoon", "teaspoons",
        "tablespoon", "tablespoons",
        "pint", "pints",
        "quart", "quarts",
        "gallon", "gallons",
        "milliliter", "milliliters",
        "liter", "liters",
        "dash", "dashes",
        "drop", "drops",
        "ounce", "ounces",
        "pound", "pounds",
        "gram", "grams",
        "kilogram", "kilograms",
        "milligram", "milligrams",
        "can", "cans",
        "package", "packages",
        "container", "containers",
        "jar", "jars",
        "bottle", "bottles",
        "box", "boxes",
        "slice", "slices",
        "sheet", "sheets",
        "each",
        "piece", "pieces",
        "handful", "handfuls",
        "clove", "cloves",
        "head", "heads",
        "bulb", "bulbs",
        "leaf", "leaves",
        "stalk", "stalks",
        "stem", "stems",
        "stick", "sticks",
        "bunch", "bunches",
        "sprig", "sprigs",
        "bag", "bags",
        "pinch", "pinches",
        "stick", "sticks",
        "splash", "splashes",
        "drizzle", "drizzles",
        "dollop", "dollops",
        "scoop", "scoops"
    };

    public static Recipe Parse(string input)
    {
        var lines = input.Split('\n').Select(line => line.Trim()).ToList();
        
        // Recipe properties to build
        string? title = null;
        string? description = null;
        var meta = new Dictionary<string, string>();
        var sections = new List<Section>();
        Section? currentSection = null;
        Step? currentStep = null;
        string? notes = null;
        
        // Process each line
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            i++;
            
            // Check for notes marker
            if (line == "~~~")
            {
                // Collect all remaining lines as notes
                var notesLines = new List<string>();
                while (i < lines.Count)
                {
                    notesLines.Add(lines[i]);
                    i++;
                }
                notes = string.Join("\n", notesLines).Trim();
                break;
            }
            
            if (string.IsNullOrEmpty(line))
                continue;
            
            // Check for metadata section
            if (line == "---")
            {
                i = ParseMetadata(lines, i, lineNumber, meta);
                continue;
            }
            
            // Process line based on its first character
            if (line.StartsWith('='))
            {
                title = line.Trim('=').Trim();
            }
            else if (line.StartsWith('>'))
            {
                description = line.Trim('>').Trim();
            }
            else if (line.StartsWith('+'))
            {
                // New section with title
                if (currentSection != null && currentSection.Steps.Count > 0)
                {
                    sections.Add(currentSection);
                }
                currentSection = new Section { Title = line.Trim('+').Trim(), Steps = [] };
            }
            else if (line.StartsWith('#'))
            {
                // Ensure we have a section
                currentSection ??= new Section { Title = null, Steps = [] };
                
                // New step
                currentStep = new Step { Paragraphs = [line.Trim('#').Trim()], Ingredients = [] };
                currentSection.Steps.Add(currentStep);
            }
            else if (line.StartsWith('*'))
            {
                // Ingredient for current step
                if (currentStep == null)
                    throw new SyntaxError("Ingredient found outside of a step", lineNumber);
                
                var ingredientText = line.Trim('*').Trim();
                var ingredient = ParseIngredient(ingredientText, lineNumber);
                currentStep.Ingredients.Add(ingredient);
            }
            else
            {
                // Regular paragraph - add to current step
                if (currentStep == null)
                    throw new SyntaxError("Paragraph found outside of a step", lineNumber);

                currentStep.Paragraphs.Add(line);
            }
        }
        
        // Validate the recipe has a title
        if (string.IsNullOrEmpty(title))
            throw new SyntaxError("Recipe must have a title", 1);
        
        // Add the last section if we have one
        if (currentSection != null && currentSection.Steps.Count > 0)
        {
            sections.Add(currentSection);
        }
        
        return new Recipe
        {
            Title = title,
            Description = description,
            Sections = sections,
            Meta = meta,
            Notes = notes
        };
    }
    
    private static int ParseMetadata(List<string> lines, int startIndex, int startLineNumber, Dictionary<string, string> meta)
    {
        var i = startIndex;
        
        while (i < lines.Count)
        {
            var line = lines[i];
            var currentLineNumber = startLineNumber + (i - startIndex + 1);
            i++;
        
            if (line == "---")
            {
                return i;
            }
        
            // Parse key-value pair
            var parts = line.Split(':', 2);
        
            if (parts.Length != 2)
            {
                throw new SyntaxError($"Invalid metadata format, expected 'key: value' but got '{line}'", currentLineNumber);
            }
        
            var key = parts[0].Trim();
            var value = parts[1].Trim();
        
            // Validate key format (must begin with letter, contain only letters, numbers, underscores)
            if (string.IsNullOrEmpty(key) || !char.IsLetter(key[0]) || key.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            {
                throw new SyntaxError($"Invalid metadata key: '{key}', must begin with a letter and contain only letters, numbers, and underscores", currentLineNumber);
            }
        
            meta[key] = value;
        }
    
        // If we got here, we never found the closing "---"
        throw new SyntaxError("Unclosed metadata section", startLineNumber);
    }
        
private static Ingredient ParseIngredient(string input, int lineNumber)
{
    // Check if there's a note (after a comma)
    string? note = null;
    var commaIndex = input.IndexOf(',');
    if (commaIndex >= 0)
    {
        note = input[(commaIndex + 1)..].Trim();
        input = input[..commaIndex].Trim();
    }
    
    // Extract alternative amount in parentheses if present
    Amount? altAmount = null;
    if (input.Contains('(') && input.Contains(')'))
    {
        var openParenIndex = input.IndexOf('(');
        var closeParenIndex = input.IndexOf(')', openParenIndex);

        if (openParenIndex > 0 && closeParenIndex > openParenIndex)
        {
            var altAmountText = input.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
            
            // Check for mixed numbers in the alternative amount
            var altParts = altAmountText.Split(' ');
            
            // Handle mixed fraction case (e.g. "14 1/2 ounces")
            if (altParts.Length >= 3 && altParts[1].Contains('/')) 
            {
                var mixedNumberStr = $"{altParts[0]} {altParts[1]}";
                if (TryParseQuantity(mixedNumberStr, out var altQuantity) && altQuantity != null)
                {
                    var unit = altParts[2];
                    
                    // Validate alternative unit
                    if (!AllowedUnits.Contains(unit))
                    {
                        throw new SyntaxError($"Invalid unit: '{unit}'. Unit must be one of the allowed units. In ingredient {input}.", lineNumber);
                    }
                    
                    altAmount = new Amount(altQuantity, unit);

                    // Remove the alternative amount from the input
                    var beforeAlt = input[..openParenIndex].Trim();
                    var afterAlt = input[(closeParenIndex + 1)..].Trim();
                    input = $"{beforeAlt} {afterAlt}".Trim();
                }
            }
            // Handle regular case (quantity unit)
            else if (altParts.Length >= 2)
            {
                if (!TryParseQuantity(altParts[0], out var altQuantity) || altQuantity == null)
                {
                    throw new SyntaxError($"Invalid alternate quantity: {altParts[0]}", lineNumber);
                }
                
                // Validate alternative unit
                if (!AllowedUnits.Contains(altParts[1]))
                {
                    throw new SyntaxError($"Invalid unit: '{altParts[1]}'. Unit must be one of the allowed units. In ingredient {input}", lineNumber);
                }
                
                altAmount = new Amount(altQuantity, altParts[1]);

                // Remove the alternative amount from the input
                var beforeAlt = input[..openParenIndex].Trim();
                var afterAlt = input[(closeParenIndex + 1)..].Trim();
                input = $"{beforeAlt} {afterAlt}".Trim();
            }
        }
    }

    // Special case for mixed number ranges like "1 1/4-1 1/2 pounds Pasta"
    var dashIndex = input.IndexOf('-');
    if (dashIndex > 0)
    {
        var beforeDash = input[..dashIndex].Trim();
        var afterDash = input[(dashIndex + 1)..].Trim();
        
        // Check if we have a pattern that looks like a mixed number on both sides
        if (beforeDash.Count(c => c == ' ') >= 1 && beforeDash.Contains('/') &&
            afterDash.Count(c => c == ' ') >= 1 && afterDash.Contains('/'))
        {
            var beforeParts = beforeDash.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var afterParts = afterDash.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (beforeParts.Length >= 2 && afterParts.Length >= 2 &&
                char.IsDigit(beforeParts[0][0]) && beforeParts[1].Contains('/') &&
                char.IsDigit(afterParts[0][0]) && afterParts[1].Contains('/'))
            {
                // Extract the min and max mixed numbers
                var minStr = $"{beforeParts[0]} {beforeParts[1]}";
                var maxStr = $"{afterParts[0]} {afterParts[1]}";
                var rangeStr = $"{minStr}-{maxStr}";
                
                if (TryParseQuantity(rangeStr, out var rangeQuantity) && rangeQuantity != null)
                {
                    var unit = afterParts[2];
                    
                    // Validate unit
                    if (!AllowedUnits.Contains(unit))
                    {
                        throw new SyntaxError($"Invalid unit: '{unit}'. Unit must be one of the allowed units. In ingredient {input}.", lineNumber);
                    }
                    
                    var name = string.Join(' ', afterParts.Skip(3));
                    var amount = new Amount(rangeQuantity, unit);
                    return new Ingredient(amount, altAmount, name, note);
                }
            }
        }
    }

    // Get parts after processing alt amount and notes
    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    
    // Check if the ingredient starts with a digit - expected to have an amount
    if (parts.Length > 0 && parts[0].Length > 0 && char.IsDigit(parts[0][0]))
    {
        // Special handling for mixed numbers like "1 1/2 cups milk"
        if (parts.Length >= 3 && parts[1].Contains('/'))
        {
            var mixedNumberStr = $"{parts[0]} {parts[1]}";
            if (TryParseQuantity(mixedNumberStr, out var mixedQuantity) && mixedQuantity != null)
            {
                var unit = parts[2];
                
                // Validate unit
                if (!AllowedUnits.Contains(unit))
                {
                    throw new SyntaxError($"Invalid unit: '{unit}'. Unit must be one of the allowed units. In ingredient {input}.", lineNumber);
                }
                
                var name = string.Join(' ', parts.Skip(3));
                var amount = new Amount(mixedQuantity, unit);
                return new Ingredient(amount, altAmount, name, note);
            }
            else
            {
                throw new SyntaxError($"Invalid mixed number quantity: {parts[0]} {parts[1]}", lineNumber);
            }
        }
        
        // Regular quantity case
        if (parts.Length >= 2)
        {
            if (!TryParseQuantity(parts[0], out var quantity) || quantity == null)
            {
                throw new SyntaxError($"Invalid ingredient quantity: {parts[0]}", lineNumber);
            }
            
            var unit = parts[1];
            
            // Validate unit
            if (!AllowedUnits.Contains(unit))
            {
                throw new SyntaxError($"Invalid unit: '{unit}'. Unit must be one of the allowed units. In ingredient {input}.", lineNumber);
            }
            
            var name = string.Join(' ', parts.Skip(2));
            var amount = new Amount(quantity, unit);
            return new Ingredient(amount, altAmount, name, note);
        }

        throw new SyntaxError("Ingredient with quantity must have a unit", lineNumber);
    }
    
    // No quantity - just a name
    return new Ingredient(null, altAmount, input, note);
}
    
private static bool TryParseQuantity(string input, out Quantity? quantity)
{
    quantity = null;
    
    // Check for range "2-3" or mixed number ranges like "1 1/4-1 1/2"
    if (input.Contains('-'))
    {
        var rangeParts = input.Split('-');
        if (rangeParts.Length != 2)
            return false;

        // Handle mixed numbers in ranges
        var minStr = rangeParts[0].Trim();
        var maxStr = rangeParts[1].Trim();
        
        if (!TryParseExactQuantity(minStr, out var min) || min == null)
            return false;
            
        if (!TryParseExactQuantity(maxStr, out var max) || max == null)
            return false;

        quantity = new RangeQuantity(min, max);
        return true;
    }
    
    return TryParseExactQuantity(input, out quantity);
}
    
    private static bool TryParseExactQuantity(string input, out Quantity? quantity)
    {
        quantity = null;
        
        // Check for mixed number like "1 1/2"
        if (input.Contains(' ') && input.Contains('/'))
        {
            var parts = input.Split(' ');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var whole) || !parts[1].Contains('/'))
                return false;
            
            var fractionParts = parts[1].Split('/');
            if (fractionParts.Length != 2 || !int.TryParse(fractionParts[0], out var num) || !int.TryParse(fractionParts[1], out var denom))
                return false;

            quantity = new ExactQuantity(whole, num, denom);
            return true;
        }
        
        // Check for fraction like "1/2"
        if (input.Contains('/'))
        {
            var parts = input.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var num) || !int.TryParse(parts[1], out var denom))
                return false;

            quantity = new ExactQuantity(0, num, denom);
            return true;
        }
        
        // Check for whole number
        if (!int.TryParse(input, out var value))
            return false;
        
        quantity = new ExactQuantity(value);
        return true;
    }
}