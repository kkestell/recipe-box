namespace RecipeBox;

public static class Renderer
{
    public static string Render(Recipe recipe)
    {
        var parts = new List<string>();

        if (recipe.Meta.Count > 0)
        {
            parts.Add("---");
            foreach (var kvp in recipe.Meta)
            {
                parts.Add($"{kvp.Key}: {kvp.Value}");
            }
            parts.Add("---");
            parts.Add("");
        }

        parts.Add($"= {recipe.Title}");
        parts.Add("");

        if (!string.IsNullOrEmpty(recipe.Description))
        {
            parts.Add($"> {recipe.Description}");
            parts.Add("");
        }

        foreach (var section in recipe.Sections)
        {
            if (!string.IsNullOrEmpty(section.Title))
            {
                parts.Add($"+ {section.Title}");
                parts.Add("");
            }

            foreach (var step in section.Steps)
            {
                parts.Add($"# {step.Paragraphs[0]}");
                parts.Add("");
                
                if (step.Paragraphs.Count > 1)
                {
                    for (var i = 1; i < step.Paragraphs.Count; i++)
                    {
                        parts.Add(step.Paragraphs[i]);
                        parts.Add("");
                    }
                }
                
                if (step.Ingredients.Count > 0)
                {
                    foreach (var ingredient in step.Ingredients)
                    {
                        parts.Add($"* {ingredient}");
                    }
                    parts.Add("");
                }
            }
        }

        if (parts.Count > 0 && parts[^1] == "")
        {
            parts.RemoveAt(parts.Count - 1);
        }

        return string.Join("\n", parts);
    }
}