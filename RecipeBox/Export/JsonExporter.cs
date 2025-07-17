using System.Text.Json;
using RecipeBox.Data;

namespace RecipeBox.Export;

public static class JsonExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    public static string Export(Recipe recipe)
    {
        var recipeObject = new
        {
            title = recipe.Title,
            metadata = recipe.Metadata.Count != 0 ? recipe.Metadata : null,
            components = recipe.Components.Select(c => new
            {
                name = c.Name,
                steps = c.Steps.Select(s => new
                {
                    text = s.Text,
                    ingredients = s.Ingredients.Count != 0 ? s.Ingredients : null
                })
            })
        };

        return JsonSerializer.Serialize(recipeObject, JsonOptions);
    }
}