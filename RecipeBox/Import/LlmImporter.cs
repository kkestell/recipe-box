using System.Text;
using System.Text.Json;
using OpenAI.Chat;
using RecipeBox.Data;

namespace RecipeBox.Import;

public class LlmImporter
{
    private const string SystemPrompt = """
                                        You are an expert recipe assistant. Your task is to analyze the user's input, which could be unstructured text or an image of a recipe, and convert it into a structured JSON object that conforms to the provided schema.

                                        Key formatting rules to follow when populating the JSON:
                                        * Extract metadata like 'yield', 'prep_time', 'cook_time', 'category', 'cuisine', and 'source' as top-level string properties if they exist. If a value is not present, return null for that field.
                                        * All ingredients should be in a single flat list of strings.
                                        * All instructional steps should be in a single flat list of strings.
                                        * Format fractions and mixed numbers as ASCII, e.g. 1/2, 1 3/4, etc. Do not use unicode fractions.
                                        * Format temperatures using a unicode degree symbol, e.g. 350°F.
                                        * Do not modify the wording of step text, ingredient amounts, etc. Your job is merely to extract and structure the user's recipe.
                                        * The recipe must have a title, at least one ingredient, and at least one step.
                                        """;

    private const string RecipeJsonSchema = """
                                            {
                                                "type": "object",
                                                "properties": {
                                                    "title": { "type": "string" },
                                                    "yield": { "type": ["string", "null"] },
                                                    "prep_time": { "type": ["string", "null"] },
                                                    "cook_time": { "type": ["string", "null"] },
                                                    "category": { "type": ["string", "null"] },
                                                    "cuisine": { "type": ["string", "null"] },
                                                    "source": { "type": ["string", "null"] },
                                                    "ingredients": {
                                                        "type": "array",
                                                        "items": { "type": "string" }
                                                    },
                                                    "steps": {
                                                        "type": "array",
                                                        "items": { "type": "string" }
                                                    }
                                                },
                                                "required": ["title", "yield", "prep_time", "cook_time", "category", "cuisine", "source", "ingredients", "steps"],
                                                "additionalProperties": false
                                            }
                                            """;

    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _completionOptions;

    public LlmImporter(string apiKey, string model = "gpt-4.1-nano")
    {
        _chatClient = new ChatClient(model, apiKey);
        _completionOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "recipe_structure",
                BinaryData.FromString(RecipeJsonSchema),
                jsonSchemaIsStrict: true)
        };
    }

    public async Task<Recipe?> FromTextAsync(string text)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(text)
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, _completionOptions);
            var content = completion.Content.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var jsonDoc = JsonDocument.Parse(content);
            var recipeText = FormatAsSmidge(jsonDoc.RootElement);

            return Recipe.Parse(recipeText);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"Error processing recipe text with LLM: {e.Message}");
            return null;
        }
    }

    public async Task<Recipe?> FromImageAsync(string imagePath)
    {
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var imageData = BinaryData.FromBytes(imageBytes);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart("Reformat the recipe in this image."),
                    ChatMessageContentPart.CreateImagePart(imageData, "image/png")
                )
            };

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, _completionOptions);
            var content = completion.Content.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            using var jsonDoc = JsonDocument.Parse(content);
            var recipeText = FormatAsSmidge(jsonDoc.RootElement);

            return Recipe.Parse(recipeText);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"Error processing recipe image with LLM: {e.Message}");
            return null;
        }
    }

    private static string FormatAsSmidge(JsonElement root)
    {
        var sb = new StringBuilder();
        var metadataFields = new Dictionary<string, string>();

        Action<string> checkMeta = key =>
        {
            if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                metadataFields[key] = prop.GetString()!;
            }
        };

        checkMeta("yield");
        checkMeta("prep_time");
        checkMeta("cook_time");
        checkMeta("category");
        checkMeta("cuisine");
        checkMeta("source");

        if (metadataFields.Any())
        {
            sb.AppendLine("---");
            foreach (var (key, value) in metadataFields)
            {
                sb.AppendLine($"{key.Replace("_", " ")}: {value}");
            }

            sb.AppendLine("---");
        }

        if (root.TryGetProperty("title", out var title))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine($"= {title.GetString()}");
        }

        if (root.TryGetProperty("ingredients", out var ingredients) && ingredients.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine();
            sb.AppendLine("# Gather ingredients");
            sb.AppendLine();
            foreach (var ingredient in ingredients.EnumerateArray())
            {
                sb.AppendLine($"  - {ingredient.GetString()}");
            }
        }

        if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine();
            var stepList = steps.EnumerateArray().ToList();
            for (var i = 0; i < stepList.Count; i++)
            {
                sb.AppendLine($"# {stepList[i].GetString()}");
                if (i < stepList.Count - 1)
                {
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString().Trim();
    }
}