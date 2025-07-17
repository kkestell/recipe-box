using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using RecipeBox.Data;

namespace RecipeBox.Import;

public record StructuredRecipeData(string? Title, List<string?>? Ingredients, List<string?>? Instructions);

public static class UrlImporter
{
    private static readonly HttpClient HttpClient;

    static UrlImporter()
    {
        HttpClient = new HttpClient();
        HttpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36");
        HttpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        HttpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public static async Task<Recipe?> FromStructuredDataAsync(string url)
    {
        try
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(url);

            var recipeJsonElement = ExtractRecipeJson(document);
            if (recipeJsonElement.HasValue)
            {
                var structuredData = TransformRecipeJson(recipeJsonElement.Value);
                var recipeText = FormatAsSmidge(structuredData);
                return Recipe.Parse(recipeText);
            }
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"Could not extract structured recipe from {url}: {e.Message}");
        }

        return null;
    }

    public static async Task<string?> ExtractTextAsync(string url)
    {
        try
        {
            var html = await HttpClient.GetStringAsync(url);
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html));
            return document.Body?.TextContent.Trim();
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"Could not extract text content from {url}: {e.Message}");
            return null;
        }
    }

    private static JsonElement? ExtractRecipeJson(IDocument document)
    {
        var scripts = document.QuerySelectorAll("script[type='application/ld+json']");
        foreach (var script in scripts)
        {
            if (string.IsNullOrWhiteSpace(script.TextContent))
            {
                continue;
            }

            try
            {
                var json = JsonDocument.Parse(script.TextContent).RootElement;
                var items = new List<JsonElement>();

                if (json.ValueKind == JsonValueKind.Array)
                {
                    items.AddRange(json.EnumerateArray());
                }
                else
                {
                    items.Add(json);
                }

                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("@graph", out var graph) &&
                    graph.ValueKind == JsonValueKind.Array)
                {
                    items.AddRange(graph.EnumerateArray());
                }

                foreach (var item in items)
                {
                    if (item.TryGetProperty("@type", out var type))
                    {
                        if ((type.ValueKind == JsonValueKind.String && type.GetString() == "Recipe") ||
                            (type.ValueKind == JsonValueKind.Array &&
                             type.EnumerateArray().Any(t => t.GetString() == "Recipe")))
                        {
                            return item;
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static StructuredRecipeData TransformRecipeJson(JsonElement recipe)
    {
        string? title = null;
        if (recipe.TryGetProperty("name", out var name))
        {
            title = CleanText(name.GetString());
        }

        List<string?>? ingredients = null;
        if (recipe.TryGetProperty("recipeIngredient", out var ingredientsProp) &&
            ingredientsProp.ValueKind == JsonValueKind.Array)
        {
            ingredients = ingredientsProp.EnumerateArray().Select(i => CleanText(i.GetString())).ToList();
        }

        List<string?>? instructions = null;
        if (recipe.TryGetProperty("recipeInstructions", out var instructionsProp) &&
            instructionsProp.ValueKind == JsonValueKind.Array)
        {
            instructions = instructionsProp.EnumerateArray().Select(i =>
                CleanText(i.TryGetProperty("text", out var text) ? text.GetString() : i.GetString())).ToList();
        }

        return new StructuredRecipeData(title, ingredients, instructions);
    }

    private static string? CleanText(string? text)
    {
        return text == null ? null : WebUtility.HtmlDecode(text).Trim();
    }

    private static string FormatAsSmidge(StructuredRecipeData data)
    {
        var output = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(data.Title))
        {
            output.AppendLine($"= {data.Title}");
            output.AppendLine();
        }

        if (data.Ingredients is { Count: > 0 } ingredientList)
        {
            output.AppendLine("# Gather ingredients");
            foreach (var ing in ingredientList.Where(i => !string.IsNullOrWhiteSpace(i)))
            {
                output.AppendLine($"  - {ing}");
            }

            output.AppendLine();
        }

        if (data.Instructions is { Count: > 0 } instructionList)
        {
            foreach (var inst in instructionList.Where(i => !string.IsNullOrWhiteSpace(i)))
            {
                output.AppendLine($"# {inst}");
            }
        }

        return output.ToString().Trim();
    }
}