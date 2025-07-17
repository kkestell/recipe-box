using RecipeBox.Data;

namespace RecipeBox.Import;

public class Importer
{
    private readonly LlmImporter? _llmImporter;

    public Importer(string? apiKey = null)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _llmImporter = new LlmImporter(apiKey);
        }
    }

    public Task<Recipe?> FromTextAsync(string text)
    {
        RaiseApiKeyErrorIfMissing();
        return _llmImporter!.FromTextAsync(text);
    }

    public Task<Recipe?> FromImageAsync(string path)
    {
        RaiseApiKeyErrorIfMissing();
        return _llmImporter!.FromImageAsync(path);
    }

    public async Task<Recipe?> FromUrlAsync(string url)
    {
        var recipe = await UrlImporter.FromStructuredDataAsync(url);
        if (recipe != null)
        {
            return recipe;
        }

        RaiseApiKeyErrorIfMissing();

        var textContent = await UrlImporter.ExtractTextAsync(url);
        if (string.IsNullOrWhiteSpace(textContent))
        {
            return null;
        }

        return await _llmImporter!.FromTextAsync(textContent);
    }

    private void RaiseApiKeyErrorIfMissing()
    {
        if (_llmImporter == null)
        {
            throw new ArgumentException("An API key is required for this import method.");
        }
    }
}