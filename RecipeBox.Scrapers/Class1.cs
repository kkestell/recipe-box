using System.Text.Json.Nodes;
using HtmlAgilityPack;

namespace RecipeBox.Scrapers;

internal class Program
{
    private static async Task ScrapeRecipe(string url)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        var html = await client.GetStringAsync(url);
        
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        var jsonScripts = doc.DocumentNode
            .SelectNodes("//script[@type='application/ld+json']")
            ?.Select(node => node.InnerText.Trim())
            .ToList() ?? [];
        
        var recipeObj = ExtractRecipeObject(jsonScripts);
        
        if (recipeObj is null)
        {
            Console.WriteLine("No recipe found.");
            return;
        }
        
        var ingredients = ParseIngredients(recipeObj);
        
        if (ingredients is not null && ingredients.Count > 0)
        {
            Console.WriteLine("Ingredients:");
            foreach (var item in ingredients)
            {
                Console.WriteLine($"- {item}");
            }
            Console.WriteLine();
        }
        
        var instructions = ParseInstructions(recipeObj);
        
        if (instructions is not null && instructions.Count > 0)
        {
            Console.WriteLine("Instructions:");
            foreach (var section in instructions)
            {
                if (!string.IsNullOrEmpty(section.Key))
                {
                    Console.WriteLine($"Section: {section.Key}");
                }
                
                for (int i = 0; i < section.Value.Count; i++)
                {
                    Console.WriteLine($"{i+1}. {section.Value[i]}");
                }
                
                Console.WriteLine();
            }
        }
        
        var tip = ParseTip(recipeObj);
        if (!string.IsNullOrEmpty(tip))
        {
            Console.WriteLine($"Tip: {tip}");
        }
    }
    
    private static async Task Main()
    {
        var urls = new List<string> {
            "https://www.allrecipes.com/recipe/216981/deluxe-corned-beef-hash/",
            "https://www.americastestkitchen.com/recipes/10543-couscous-risotto-with-chicken-and-spinach",
            "https://www.bbcgoodfood.com/recipes/singapore-noodles-0",
            "https://www.bettycrocker.com/recipes/chicken-parmesan/fea4063a-e494-4d75-bf8e-e88393fbfe5e",
            "https://www.bonappetit.com/recipe/bas-best-bolognese",
            "https://www.budgetbytes.com/extra-cheesy-homemade-mac-and-cheese/",
            "https://www.chowhound.com/1808149/korean-fried-popcorn-chicken-recipe/",
            "https://www.countryliving.com/food-drinks/a26434198/seared-salmon-watercress-potato-salad-olive-dressing-recipe/",
            "https://damndelicious.net/2019/04/21/korean-beef-bulgogi/",
            "https://www.davidlebovitz.com/french-tomato-tart-recipe/",
            "https://www.delish.com/cooking/recipe-ideas/a58245/easy-baked-eggplant-parmesan-recipe/",
            "https://www.foodnetwork.com/recipes/moms-garlic-spread-11805239",
            "https://www.eatingwell.com/recipe/277766/teriyaki-tofu-rice-bowls/",
            "https://www.epicurious.com/recipes/food/views/oven-grilled-honey-mustard-chicken",
            "https://www.food.com/recipe/barbs-gumbo-82288",
            "https://cooking.nytimes.com/recipes/1025331-slow-roasted-salmon-with-salsa-verde"
        };
        
        foreach (var url in urls)
        {
            await ScrapeRecipe(url);
        }
    }
    
    private static JsonObject? ExtractRecipeObject(List<string> scripts)
    {
        foreach (var script in scripts)
        {
            try
            {
                var node = JsonNode.Parse(script);
            
                // Check for @graph format (array of objects in a graph)
                if (node is JsonObject rootObj && rootObj.ContainsKey("@graph"))
                {
                    var graph = rootObj["@graph"];
                    if (graph is JsonArray graphArray)
                    {
                        // Search through the graph for a Recipe object
                        foreach (var item in graphArray)
                        {
                            if (item is JsonObject graphObj && IsRecipeObject(graphObj))
                                return graphObj;
                        }
                    }
                }
            
                // Existing array handling
                if (node is JsonArray array)
                {
                    // Try to find recipe in array
                    foreach (var item in array)
                    {
                        if (item is JsonObject recipeCandidate && IsRecipeObject(recipeCandidate))
                            return recipeCandidate;
                    }
                
                    // Fallback to first item
                    node = array[0];
                }

                // Check if this is a direct recipe object
                if (node is JsonObject jsonObj)
                {
                    if (IsRecipeObject(jsonObj))
                        return jsonObj;
                
                    // Check if there's a recipe object nested within
                    if (jsonObj.ContainsKey("recipe") && jsonObj["recipe"] is JsonObject recipeObj)
                        return recipeObj;
                }
            }
            catch
            {
                // Skip invalid JSON
                continue;
            }
        }
    
        return null;
    }

    private static bool IsRecipeObject(JsonObject jsonObj)
    {
        if (!jsonObj.ContainsKey("@type"))
            return false;
        
        var type = jsonObj["@type"];

        return type switch
        {
            JsonValue value when value.ToString() == "Recipe" => true,
            JsonArray array => array.Any(item => item?.ToString() == "Recipe"),
            _ => false
        };
    }

    private static List<string>? ParseIngredients(JsonObject recipeObj)
    {
        if (!recipeObj.ContainsKey("recipeIngredient"))
            return null;

        var ingredientNode = recipeObj["recipeIngredient"];

        return ingredientNode switch
        {
            // Format 1: Array of strings
            JsonArray array => array.Select(item => item?.ToString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList(),
            
            // Format 2: Single string (possibly comma-separated)
            JsonValue value => value.ToString()
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList(),
            
            _ => null
        };
    }
        
    private static Dictionary<string, List<string>>? ParseInstructions(JsonObject recipeObj)
    {
        if (!recipeObj.ContainsKey("recipeInstructions"))
            return null;

        var instructionsNode = recipeObj["recipeInstructions"];
        var result = new Dictionary<string, List<string>>();
        const string defaultSection = "";
        
        // Single string format
        if (instructionsNode is JsonValue value)
        {
            var steps = value.ToString()
                .Split(new[] { ". ", ", " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            
            result[defaultSection] = steps;
            return result;
        }
        
        // Not an array format
        if (instructionsNode is not JsonArray instructionsArray)
            return null;
        
        // First, find all sections and regular steps
        foreach (var node in instructionsArray)
        {
            if (node is not JsonObject stepObj)
                continue;
                
            // Check if this is a section
            if (stepObj.ContainsKey("@type") && stepObj["@type"]?.ToString() == "HowToSection")
            {
                var sectionName = stepObj.ContainsKey("name") ? 
                    stepObj["name"]?.ToString() ?? defaultSection : 
                    defaultSection;
                
                var sectionSteps = new List<string>();
                
                if (stepObj.ContainsKey("itemListElement") && stepObj["itemListElement"] is JsonArray items)
                {
                    foreach (var item in items)
                    {
                        if (item is JsonObject itemObj && itemObj.ContainsKey("text"))
                        {
                            var text = itemObj["text"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(text))
                                sectionSteps.Add(text);
                        }
                    }
                }
                
                if (sectionSteps.Count > 0)
                    result[sectionName] = sectionSteps;
            }
            // Regular step
            else if (stepObj.ContainsKey("text"))
            {
                var text = stepObj["text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (!result.ContainsKey(defaultSection))
                        result[defaultSection] = new List<string>();
                        
                    result[defaultSection].Add(text);
                }
            }
        }
        
        // If we didn't find any structured instructions but have array items, treat them as simple steps
        if (result.Count == 0)
        {
            var steps = new List<string>();
            
            foreach (var node in instructionsArray)
            {
                // Plain string
                if (node != null)
                {
                    var text = node.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        steps.Add(text);
                }
            }
            
            if (steps.Count > 0)
                result[defaultSection] = steps;
        }
        
        return result.Count > 0 ? result : null;
    }
    
    private static string? ParseTip(JsonObject recipeObj)
    {
        // Check for HowtoTip object
        if (recipeObj.ContainsKey("HowtoTip"))
        {
            if (recipeObj["HowtoTip"] is JsonObject tipObj && tipObj.ContainsKey("text"))
                return tipObj["text"]?.ToString();
                
            if (recipeObj["HowtoTip"] is JsonValue tipValue)
                return tipValue.ToString();
        }
        
        // Check for notes fields
        foreach (var notesField in new[] { "notes", "recipeNotes" })
        {
            if (!recipeObj.ContainsKey(notesField))
                continue;
                
            var notes = recipeObj[notesField];
            
            if (notes is JsonValue notesValue)
                return notesValue.ToString();
                
            if (notes is JsonArray notesArray && notesArray.Count > 0)
            {
                var noteTexts = notesArray
                    .Select(n => n?.ToString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                    
                return string.Join(" ", noteTexts);
            }
        }
        
        return null;
    }
}