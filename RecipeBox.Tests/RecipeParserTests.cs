namespace RecipeBox.Tests;

[TestFixture]
public class RecipeParserTests
{
    #region Valid Recipes
    
    [Test]
    public void Parse_BasicRecipe_ReturnsCorrectRecipe()
    {
        // Arrange
        const string input = 
            """
            = Basic Recipe
            > This is a description
            # First step
            * 1 cup sugar
            """;

        // Act
        var recipe = RecipeParser.Parse(input);

        // Assert
        Assert.That(recipe.Title, Is.EqualTo("Basic Recipe"));
        Assert.That(recipe.Description, Is.EqualTo("This is a description"));
        Assert.That(recipe.Sections.Count, Is.EqualTo(1));
        Assert.That(recipe.Sections[0].Steps.Count, Is.EqualTo(1));
        Assert.That(recipe.Sections[0].Steps[0].Paragraphs[0], Is.EqualTo("First step"));
        Assert.That(recipe.Sections[0].Steps[0].Ingredients.Count, Is.EqualTo(1));
        Assert.That(recipe.Sections[0].Steps[0].Ingredients[0].Amount.Unit, Is.EqualTo("cup"));
        Assert.That(recipe.Sections[0].Steps[0].Ingredients[0].Name, Is.EqualTo("sugar"));
    }

    [Test]
    public void Parse_RecipeWithMetadata_ParsesMetadataCorrectly()
    {
        // Arrange
        const string input = 
            """
            ---
            yield: 4 servings
            category: Pasta
            subcategory: Italian
            ---
            = Pasta Recipe
            """;

        // Act
        var recipe = RecipeParser.Parse(input);

        // Assert
        Assert.That(recipe.Meta.Count, Is.EqualTo(3));
        Assert.That(recipe.Meta["yield"], Is.EqualTo("4 servings"));
        Assert.That(recipe.Meta["category"], Is.EqualTo("Pasta"));
        Assert.That(recipe.Meta["subcategory"], Is.EqualTo("Italian"));
    }

    [Test]
    public void Parse_RecipeWithMultipleStepParagraphs_ParsesParagraphsCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # First step
            Second paragraph
            Third paragraph
            """;

        // Act
        var recipe = RecipeParser.Parse(input);

        // Assert
        Assert.That(recipe.Sections[0].Steps[0].Paragraphs.Count, Is.EqualTo(3));
        Assert.That(recipe.Sections[0].Steps[0].Paragraphs[0], Is.EqualTo("First step"));
        Assert.That(recipe.Sections[0].Steps[0].Paragraphs[1], Is.EqualTo("Second paragraph"));
        Assert.That(recipe.Sections[0].Steps[0].Paragraphs[2], Is.EqualTo("Third paragraph"));
    }

    [Test]
    public void Parse_RecipeWithSections_ParsesSectionsCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            + First Section
            # Step in section one
            + Second Section
            # Step in section two
            """;

        // Act
        var recipe = RecipeParser.Parse(input);

        // Assert
        Assert.That(recipe.Sections.Count, Is.EqualTo(2));
        Assert.That(recipe.Sections[0].Title, Is.EqualTo("First Section"));
        Assert.That(recipe.Sections[1].Title, Is.EqualTo("Second Section"));
        Assert.That(recipe.Sections[0].Steps[0].Paragraphs[0], Is.EqualTo("Step in section one"));
        Assert.That(recipe.Sections[1].Steps[0].Paragraphs[0], Is.EqualTo("Step in section two"));
    }

    [Test]
    public void Parse_IngredientWithWholeNumber_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 2 cups flour
            """;

        // Act
        var recipe = RecipeParser.Parse(input);
        var ingredient = recipe.Sections[0].Steps[0].Ingredients[0];

        // Assert
        Assert.That(ingredient.Name, Is.EqualTo("flour"));
        Assert.That(ingredient.Amount.Unit, Is.EqualTo("cups"));
        var quantity = ingredient.Amount.Quantity as ExactQuantity;
        Assert.That(quantity.WholeNumber, Is.EqualTo(2));
        Assert.That(quantity.Numerator, Is.EqualTo(0));
        Assert.That(quantity.Denominator, Is.EqualTo(1));
    }

    [Test]
    public void Parse_IngredientWithFraction_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1/2 cup sugar
            """;

        // Act
        var recipe = RecipeParser.Parse(input);
        var ingredient = recipe.Sections[0].Steps[0].Ingredients[0];

        // Assert
        Assert.That(ingredient.Name, Is.EqualTo("sugar"));
        Assert.That(ingredient.Amount.Unit, Is.EqualTo("cup"));
        var quantity = ingredient.Amount.Quantity as ExactQuantity;
        Assert.That(quantity.WholeNumber, Is.EqualTo(0));
        Assert.That(quantity.Numerator, Is.EqualTo(1));
        Assert.That(quantity.Denominator, Is.EqualTo(2));
    }

    [Test]
    public void Parse_IngredientWithMixedNumber_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1 1/2 cups milk
            """;

        // Act
        var recipe = RecipeParser.Parse(input);
        var ingredient = recipe.Sections[0].Steps[0].Ingredients[0];

        // Assert
        Assert.That(ingredient.Name, Is.EqualTo("milk"));
        Assert.That(ingredient.Amount.Unit, Is.EqualTo("cups"));
        var quantity = ingredient.Amount.Quantity as ExactQuantity;
        Assert.That(quantity.WholeNumber, Is.EqualTo(1));
        Assert.That(quantity.Numerator, Is.EqualTo(1));
        Assert.That(quantity.Denominator, Is.EqualTo(2));
    }

    [Test]
    public void Parse_IngredientWithRange_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1-2 tablespoons oil
            """;

        // Act
        var recipe = RecipeParser.Parse(input);
        var ingredient = recipe.Sections[0].Steps[0].Ingredients[0];

        // Assert
        Assert.That(ingredient.Name, Is.EqualTo("oil"));
        Assert.That(ingredient.Amount.Unit, Is.EqualTo("tablespoons"));
        var range = ingredient.Amount.Quantity as RangeQuantity;
        Assert.That(range, Is.Not.Null);
        var min = range.Min as ExactQuantity;
        var max = range.Max as ExactQuantity;
        Assert.That(min.WholeNumber, Is.EqualTo(1));
        Assert.That(max.WholeNumber, Is.EqualTo(2));
    }

    [Test]
    public void Parse_IngredientWithAlternateAmount_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 100 grams (1/2 cup) sugar
            """;

        // Act
        var recipe = RecipeParser.Parse(input);
        var ingredient = recipe.Sections[0].Steps[0].Ingredients[0];

        // Assert
        Assert.That(ingredient.Name, Is.EqualTo("sugar"));
        Assert.That(ingredient.Amount.Unit, Is.EqualTo("grams"));
        Assert.That(((ExactQuantity)ingredient.Amount.Quantity).WholeNumber, Is.EqualTo(100));
        Assert.That(ingredient.AltAmount.Unit, Is.EqualTo("cup"));
        var altQuantity = ingredient.AltAmount.Quantity as ExactQuantity;
        Assert.That(altQuantity.WholeNumber, Is.EqualTo(0));
        Assert.That(altQuantity.Numerator, Is.EqualTo(1));
        Assert.That(altQuantity.Denominator, Is.EqualTo(2));
    }

    [Test]
    public void Parse_IngredientWithNote_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1 cup sugar, granulated
            """;

        // Act
        var recipe = RecipeParser.Parse(input);
        var ingredient = recipe.Sections[0].Steps[0].Ingredients[0];

        // Assert
        Assert.That(ingredient.Name, Is.EqualTo("sugar"));
        Assert.That(ingredient.Note, Is.EqualTo("granulated"));
    }

    [Test]
    public void Parse_IngredientWithoutAmount_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * Salt
            """;

        // Act
        var recipe = RecipeParser.Parse(input);
        var ingredient = recipe.Sections[0].Steps[0].Ingredients[0];

        // Assert
        Assert.That(ingredient.Name, Is.EqualTo("Salt"));
        Assert.That(ingredient.Amount, Is.Null);
    }

    [Test]
    public void Parse_IngredientOutsideStep_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            * 1 cup sugar
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(2));
    }

    [Test]
    public void Parse_ParagraphOutsideStep_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            This is a paragraph outside a step
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(2));
    }

    [Test]
    public void Parse_CompleteRecipe_ParsesCorrectly()
    {
        // Arrange
        const string input = 
            """
            ---
            yield: 4 servings
            prep_time: 10 minutes
            cook_time: 45 minutes
            ---

            = Red Lentil Soup

            > A delicious Middle Eastern soup

            + Prepare Aromatics

            # Heat olive oil in a large pot over medium heat. Add onion and cook until translucent.

            * 2 tablespoons olive oil
            * 1 large onion, diced

            # Add garlic and cook for another minute.

            * 2 cloves garlic, minced

            + Cook Soup

            # Add lentils, broth, and spices. Bring to a boil.

            * 1 cup red lentils
            * 4 cups vegetable broth
            * 1 teaspoon cumin
            * 1/2 teaspoon turmeric
            * Salt
            * Black pepper

            # Reduce heat and simmer for 20-30 minutes until lentils are tender.

            # Stir in lemon juice before serving.

            * 1-2 tablespoons lemon juice
            """;

        // Act
        var recipe = RecipeParser.Parse(input);

        // Assert
        Assert.That(recipe.Title, Is.EqualTo("Red Lentil Soup"));
        Assert.That(recipe.Description, Is.EqualTo("A delicious Middle Eastern soup"));
        Assert.That(recipe.Meta.Count, Is.EqualTo(3));
        Assert.That(recipe.Meta["yield"], Is.EqualTo("4 servings"));
        Assert.That(recipe.Sections.Count, Is.EqualTo(2));
        Assert.That(recipe.Sections[0].Title, Is.EqualTo("Prepare Aromatics"));
        Assert.That(recipe.Sections[1].Title, Is.EqualTo("Cook Soup"));
        Assert.That(recipe.Sections[0].Steps.Count, Is.EqualTo(2));
        Assert.That(recipe.Sections[1].Steps.Count, Is.EqualTo(3));
        Assert.That(recipe.Sections[1].Steps[2].Ingredients[0].Amount.Quantity, Is.TypeOf<RangeQuantity>());
    }
    
    #endregion
    
    #region Invalid Recipes
    
    [Test]
    public void Parse_UnclosedMetadataSection_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            ---
            yield: 4 servings
            category: Dessert
            = Cheesecake Recipe
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(4)); // Should fail at the title line
    }
    
    [Test]
    public void Parse_InvalidMetadataFormat_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            ---
            yield: 4 servings
            this is not a key-value pair
            category: Dessert
            ---
            = Cheesecake Recipe
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(3)); // The line with the invalid format
    }

    [Test]
    public void Parse_InvalidMetadataKey_StartWithNonLetter_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            ---
            yield: 4 servings
            1invalid: This key starts with a number
            ---
            = Cheesecake Recipe
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(3)); // The line with the invalid key
    }

    [Test]
    public void Parse_InvalidMetadataKey_ContainsInvalidCharacters_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            ---
            yield: 4 servings
            invalid-key: This key contains a hyphen
            ---
            = Cheesecake Recipe
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(3)); // The line with the invalid key
    }

    [Test]
    public void Parse_EmptyMetadataKey_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            ---
            : Empty key is not allowed
            ---
            = Cheesecake Recipe
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(2)); // The line with the empty key
    }    

    [Test]
    public void Parse_IngredientWithInvalidUnit_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1 sugar
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(3));
    }
    
    [Test]
    public void Parse_InvalidAlternateAmount_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1 cup (1bc cup) sugar
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(3));
    }
    
    [Test]
    public void Parse_InvalidMixedNumberIngredient_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1 x/2 cups flour
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(3));
    }    

    [Test]
    public void Parse_RecipeWithInvalidIngredientQuantity_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            = Recipe
            # Step
            * 1bc cups flour
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(3)); // The line with the invalid ingredient
    }
    
    [Test]
    public void Parse_MissingTitle_ThrowsSyntaxError()
    {
        // Arrange
        const string input = 
            """
            > This is a description without a title
            # First step
            """;

        // Act & Assert
        var ex = Assert.Throws<SyntaxError>(() => RecipeParser.Parse(input));
        Assert.That(ex.LineNumber, Is.EqualTo(1)); // The first line, which isn't a title
    }
    
    #endregion
}