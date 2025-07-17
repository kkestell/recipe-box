# Recipe Box

A plain-text file format for structured recipes, and a .NET library and command-line tool to parse and manage them.

## Command-Line Application

The `rbx` command-line tool provides utilities for working with recipe files in a git-based repository.

### Installation

No installers yet. Sorry.

## Commands

### Import

```text
rbx import <source> [input]
```

Import a recipe from an external source into the repository.

**Arguments:**

`source`
: Import source type: `url`, `text`, or `image`

`input`
: Input file path, URL, or text string (optional for text - will read from stdin if not provided)

**Examples:**

```bash
# Import from URL
rbx import url "https://www.allrecipes.com/recipe/223042/best-brownies/"

# Import from image file
rbx import image /path/to/recipe-photo.jpg

# Import from text file
rbx import text /path/to/recipe.txt

# Import text from stdin
cat recipe.txt | rbx import text
echo "My recipe text..." | rbx import text
```

### Show

```text
rbx show <slug>
```

Display a recipe.

**Arguments:**

`slug`
: The slug identifier of the recipe to display

**Examples:**

```bash
rbx show best-brownies
```

### Edit

```text
rbx edit <slug>
```

Edit a recipe in your default editor.

**Arguments:**

`slug`
: The slug identifier of the recipe to edit

**Examples:**

```bash
rbx edit best-brownies
```

### Delete

```text
rbx delete <slug>
```

Delete a recipe.

**Arguments:**

`slug`
: The slug identifier of the recipe to delete

**Examples:**

```bash
rbx delete best-brownies
```

### List

```text
rbx list
```

List all recipe slugs.

**Examples:**

```bash
rbx list
```

### PDF

`rbx pdf [options]`

Export recipes to a PDF file.

#### Options

`--output <filename>`
: Output PDF file name (default: cookbook.pdf)

`--include <slug1> <slug2> ...`
: Only include these specific recipes by slug

`--exclude <slug1> <slug2> ...`
: Exclude these recipes by slug

`--include-drafts`
: Include draft recipes in the export (default: false)

**Examples:**

```bash
# Create a PDF of all non-draft recipes
rbx pdf

# Create a PDF with custom output name
rbx pdf --output my-cookbook.pdf

# Create a PDF of specific recipes only
rbx pdf --include best-brownies chocolate-cake

# Create a PDF excluding certain recipes
rbx pdf --exclude old-recipe deprecated-recipe

# Create a PDF including draft recipes
rbx pdf --include-drafts

# Combine PDF options
rbx pdf --include-drafts --exclude test-recipe --output complete-cookbook.pdf
```

### Export

```text
rbx export <format> <slug> [options]
```

Export a recipe to another format.

**Arguments:**

`format`
: Export format: `markdown` or `json`

`slug`
: The slug identifier of the recipe to export

#### Options

`--output <filename>`
: Output file (default: stdout)

**Examples:**

```bash
# Export to Markdown (stdout)
rbx export markdown best-brownies

# Export to JSON (stdout)
rbx export json best-brownies

# Export to file
rbx export markdown best-brownies --output my-brownies.md
rbx export json best-brownies --output recipe.json
```


## Recipe Format

The RecipeBox format is a simple, structured way to write recipes in plain text files that's easy for both humans and computers to read and write. A recipe consists of a title, optional metadata, and a series of steps, which can be grouped into named components.

```smidge
= Classic Brownies

# Melt 1 cup butter and mix with 2 cups sugar.

- 1 cup butter
- 2 cups sugar

# Beat in 4 eggs, one at a time.

- 4 eggs

# Fold in 3/4 cup cocoa powder and 1 cup flour.

- 3/4 cup cocoa powder
- 1 cup flour

# Bake 25 minutes at 350°F.
```

### Ingredients

Ingredients are prefixed with a dash (`-`) and are listed immediately following the step they belong to.

### Steps

Steps are marked with a hash symbol (`#`).

### Multiple Components

For more complex recipes, you can use a plus (`+`) prefix to create named components.

```smidge
= Chicken Parmesan

+ Chicken

# Pound 4 chicken breasts thin.

- 4 chicken breasts

# Dip in 2 eggs, then 1 cup breadcrumbs.

- 2 eggs
- 1 cup breadcrumbs

# Pan fry until golden.

+ Sauce

# Sauté 3 cloves garlic in 1 tbsp olive oil until fragrant.

- 3 cloves garlic
- 1 tbsp olive oil
  
# Add 1 can crushed tomatoes and 1 tsp oregano, then simmer.

- 1 can crushed tomatoes
- 1 tsp oregano
```

### Metadata

Recipe metadata uses YAML frontmatter format with `lower_snake_case` keys.

```
---
prep_time: 20 minutes
cook_time: 45 minutes
servings: 6
---
```

Metadata must appear at the very beginning of your recipe file.

## .NET Library

The `RecipeBox` library provides classes and functions for parsing recipe files into structured C# objects.

### Recipe.Parse()

The static `Recipe.Parse()` method parses a recipe from text and returns a `Recipe` object.

```csharp
using RecipeBox;

var recipeText = """
---
prep_time: 10 minutes
---
= Simple Salad

# Wash greens, add tomatoes, and drizzle with oil.
- Mixed greens
- Cherry tomatoes
- Olive oil
""";

var recipe = Recipe.Parse(recipeText);
Console.WriteLine(recipe.Title); // "Simple Salad"
```

The method will throw an `ArgumentException` if the recipe cannot be parsed.

### Models

```csharp
public class Step
{
    public string Text { get; set; }
    public List<string> Ingredients { get; set; }
}

public class Component
{
    public string? Name { get; set; }
    public List<Step> Steps { get; set; }
}

public class Recipe
{
    public string Title { get; set; }
    public List<Component> Components { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public string Content { get; set; } // Original text
}
```
