using System.Text;
using RecipeBox.Data;

namespace RecipeBox.Export;

public static class MarkdownExporter
{
    public static string Export(Recipe recipe)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {recipe.Title}");
        sb.AppendLine();

        var stepCounter = 1;
        foreach (var component in recipe.Components)
        {
            if (!string.IsNullOrWhiteSpace(component.Name))
            {
                sb.AppendLine($"## {component.Name}");
                sb.AppendLine();
            }

            foreach (var step in component.Steps)
            {
                sb.AppendLine($"{stepCounter++}. {step.Text}");
                if (step.Ingredients.Count != 0)
                {
                    sb.AppendLine();
                    foreach (var ingredient in step.Ingredients)
                    {
                        sb.AppendLine($"- {ingredient}");
                    }
                }
                sb.AppendLine();
            }
        }

        if (recipe.Metadata.Count != 0)
        {
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var (key, value) in recipe.Metadata)
            {
                var formattedKey = string.Join(" ", key.Split('_').Select(w => char.ToUpper(w[0]) + w.Substring(1)));
                sb.AppendLine(formattedKey);
                sb.AppendLine($": {value}");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
