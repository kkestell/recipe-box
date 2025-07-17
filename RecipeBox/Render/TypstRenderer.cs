using System.Text.RegularExpressions;
using RecipeBox.Data;

namespace RecipeBox.Render;

public partial class TypstRenderer
{
    [GeneratedRegex(@"(?<=\d)/(?=\d)")]
    private static partial Regex FractionSlashRegex();

    [GeneratedRegex(@"(?<=\d)x(?=\d)")]
    private static partial Regex MultiplicationSignRegex();
    
    [GeneratedRegex(@"(?<=\d)-(?=\d)")]
    private static partial Regex EnDashRegex();
    
    public static string Render(List<Recipe> recipes, string? title = null, string? subtitle = null)
    {
        var typst = new List<string> { TypstHeader() };

        if (recipes.Count == 1)
        {
            typst.Add(RenderSingleRecipe(recipes.First()));
        }
        else
        {
            typst.AddRange(RenderCookbook(recipes, title, subtitle));
        }

        return string.Join("\n\n", typst);
    }

    private static bool IsPresent(string? s)
    {
        return !string.IsNullOrWhiteSpace(s);
    }

    private static string? Presence(string? s)
    {
        return IsPresent(s) ? s : null;
    }

    private static List<string> RenderCookbook(List<Recipe> recipes, string? title, string? subtitle)
    {
        var typst = new List<string>();

        if (IsPresent(title) || IsPresent(subtitle))
        {
            typst.Add("#v(5cm)");
            typst.Add($"#align(center)[#text(size: 22pt)[#heading(level: 1, outlined: false)[{title}]]]");
            if (IsPresent(subtitle))
            {
                typst.Add("#v(1cm)");
                typst.Add($"#align(center)[#heading(level: 2, outlined: false)[{subtitle}]]");
            }

            typst.Add("#pagebreak()");
        }

        typst.Add("#align(center)[#heading(level: 1, outlined: false)[Contents]]");
        typst.Add("#v(1cm)");
        typst.Add("#outline(title: none, depth: 2)");
        typst.Add("#pagebreak()");
        typst.Add("#counter(page).update(1)");

        var recipesByCategory = recipes
            .GroupBy(recipe => Presence(recipe.Metadata.GetValueOrDefault("category")) ?? "Uncategorized")
            .OrderBy(g => g.Key)
            .ToList();

        for (var catIdx = 0; catIdx < recipesByCategory.Count; catIdx++)
        {
            var categoryGroup = recipesByCategory[catIdx];
            var category = categoryGroup.Key;
            var catRecipes = categoryGroup.ToList();

            typst.Add("#v(5cm)");
            typst.Add($"#align(center)[#heading(level: 1)[{category}]]");
            typst.Add("#pagebreak()");

            for (var recIdx = 0; recIdx < catRecipes.Count; recIdx++)
            {
                var recipe = catRecipes[recIdx];
                typst.Add(RenderSingleRecipe(recipe, 2));
                if (recIdx < catRecipes.Count - 1 || catIdx < recipesByCategory.Count - 1)
                {
                    typst.Add("#pagebreak()");
                }
            }
        }

        return typst;
    }

    private static string RenderSingleRecipe(Recipe recipe, int titleHeadingLevel = 1)
    {
        var typst = new List<string>();
        var metadata = recipe.Metadata;

        var source = metadata.GetValueOrDefault("source");
        var footerContent = IsPresent(source)
            ? $"#text(8pt)[{Fancy(source)}] #h(1fr) #text(8pt, [#counter(page).display() / #counter(page).final().at(0)])"
            : "#h(1fr) #text(8pt, [#counter(page).display() / #counter(page).final().at(0)]) #h(1fr)";
        typst.Add($"#set page(footer: context [{footerContent}])");

        var title = Presence(recipe.Title) ?? "Untitled Recipe";

        var metadataKeys = new[] { "yield", "prep_time", "cook_time", "category", "cuisine" };
        var hasMetadata = metadataKeys.Any(key => IsPresent(metadata.GetValueOrDefault(key)));

        typst.Add(hasMetadata
            ? RenderTitleWithMetadataGrid(title, metadata, titleHeadingLevel)
            : $"#heading(level: {titleHeadingLevel})[{title}]");

        typst.Add("#v(1.5em)\n#line(length: 100%, stroke: 0.5pt)\n#v(1.5em)");

        for (var i = 0; i < recipe.Components.Count; i++)
        {
            var component = recipe.Components[i];
            if (IsPresent(component.Name))
            {
                typst.Add($"=== {component.Name}\n#v(1em)");
            }

            for (var j = 0; j < component.Steps.Count; j++)
            {
                var step = component.Steps[j];
                typst.Add(RenderStep(step, j));
                if (j < component.Steps.Count - 1)
                {
                    typst.Add("#v(1em)");
                }
            }

            if (i < recipe.Components.Count - 1)
            {
                typst.Add("#v(3em)");
            }
        }

        return string.Join("\n\n", typst);
    }

    private static string RenderTitleWithMetadataGrid(string title, Dictionary<string, string> metadata,
        int titleHeadingLevel)
    {
        return $$"""
                 #grid(
                   columns: (1fr, auto),
                   gutter: 2em,
                   align: horizon,
                   [#heading(level: {{titleHeadingLevel}})[{{title}}]],
                   [
                     #align(right)[
                       #block[
                         #set text(size: 9pt)
                         {{RenderMetadataGrid(metadata)}}
                       ]
                     ]
                   ]
                 )
                 """;
    }

    private static string RenderMetadataGrid(Dictionary<string, string> metadata)
    {
        string GetValue(string key)
        {
            return metadata.GetValueOrDefault(key, "")?.Replace("\"", "\\\"") ?? "";
        }

        var yieldVal = GetValue("yield");
        var prepTimeVal = GetValue("prep_time");
        var cookTimeVal = GetValue("cook_time");
        var categoryVal = GetValue("category");
        var cuisineVal = GetValue("cuisine");

        return $$"""
                 #grid(
                   columns: (auto, auto, auto, auto, auto),
                   column-gutter: 1.5em,
                   row-gutter: 0.75em,
                   [#align(center)[#text(weight: "bold")[Yield]]],
                   [#align(center)[#text(weight: "bold")[Prep Time]]],
                   [#align(center)[#text(weight: "bold")[Cook Time]]],
                   [#align(center)[#text(weight: "bold")[Category]]],
                   [#align(center)[#text(weight: "bold")[Cuisine]]],
                   [#align(center)[{{yieldVal}}]],
                   [#align(center)[{{prepTimeVal}}]],
                   [#align(center)[{{cookTimeVal}}]],
                   [#align(center)[{{categoryVal}}]],
                   [#align(center)[{{cuisineVal}}]]
                 )
                 """;
    }

    private static string RenderStep(Step step, int index)
    {
        var ingredientList = string.Join(", ", step.Ingredients.Select(i => $"[{Fancy(i)}]"));
        var hasIngredients = step.Ingredients.Any();
        var hasIngredientsStr = hasIngredients.ToString().ToLower();

        return $$"""
                 #grid(
                   columns: (2fr, 1fr),
                   gutter: 3em,
                   [
                     #enum.item({{index + 1}})[{{Fancy(step.Text)}}]
                   ],
                   [
                     #if {{hasIngredientsStr}} {
                       block(
                         breakable: false,
                         list(
                           spacing: 1em,
                           {{ingredientList}}
                         )
                       )
                     }
                   ]
                 )
                 """;
    }

    private static string Fancy(string? text)
    {
        const string narrowNbsp = "\u202F";
        const string fractionSlash = "\u2044";
        const string multiplicationSign = "\u00D7";
        const string enDash = "\u2013";
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var processedText = text.Replace("°F", $"{narrowNbsp}°F");
        processedText = FractionSlashRegex().Replace(processedText, fractionSlash);
        processedText = MultiplicationSignRegex().Replace(processedText, multiplicationSign);
        processedText = EnDashRegex().Replace(processedText, enDash);
        
        return processedText;
    }

    private static string TypstHeader()
    {
        return """
               #set list(spacing: 0.65em)
               #set text(font: "Libertinus Serif", size: 11pt)
               #set page("us-letter", margin: (top: 0.75in, bottom: 1in, left: 0.75in, right: 0.75in))
               #set enum(spacing: 1.5em)
               """;
    }
}
