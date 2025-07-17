using CommandLine;
using RecipeBox.Data;
using RecipeBox.Import;

namespace RecipeBox.Console.Commands;

public enum ImportSource
{
    Url,
    Text,
    Image
}

[Verb("import", HelpText = "Import a recipe from an external source into the repository.")]
public class ImportOptions
{
    [Value(0, Required = true, HelpText = "Import source (url, text, image).")]
    public ImportSource Source { get; set; }

    [Value(1, Required = false, HelpText = "Input file, URL, or text string.")]
    public string Input { get; set; } = "";
}

public static class ImportCommand
{
    public static async Task<int> Handle(ImportOptions opts, Repository repository)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var importer = new Importer(apiKey);
        Recipe? recipe = null;

        switch (opts.Source)
        {
            case ImportSource.Url:
                if (string.IsNullOrWhiteSpace(opts.Input)) throw new InvalidOperationException("A URL must be provided for the 'url' import source.");
                recipe = await importer.FromUrlAsync(opts.Input);
                break;
            
            case ImportSource.Image:
                if (string.IsNullOrWhiteSpace(opts.Input)) throw new InvalidOperationException("An image file path must be provided for the 'image' import source.");
                recipe = await importer.FromImageAsync(opts.Input);
                break;

            case ImportSource.Text:
                var textToImport = string.IsNullOrWhiteSpace(opts.Input)
                    ? await System.Console.In.ReadToEndAsync()
                    : await File.ReadAllTextAsync(opts.Input);
                recipe = await importer.FromTextAsync(textToImport);
                break;
        }

        if (recipe == null)
        {
            throw new InvalidOperationException("Failed to import recipe.");
        }

        await repository.AddRecipe(recipe);
        return 0;
    }
}