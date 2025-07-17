using CommandLine;
using RecipeBox.Data;
using RecipeBox.Export;

namespace RecipeBox.Console.Commands;

public enum ExportFormat
{
    Markdown,
    Json
}

[Verb("export", HelpText = "Export a recipe to a different format.")]
public class ExportOptions
{
    [Value(0, Required = true, HelpText = "Export format (md, text).")]
    public ExportFormat Format { get; set; }

    [Value(1, Required = true, HelpText = "Slug of the recipe to export.")]
    public string Slug { get; set; } = "";

    [Option('o', "output", HelpText = "Output file (default: stdout).")]
    public string? Output { get; set; }
}

public static class ExportCommand
{
    public static async Task<int> Handle(ExportOptions opts, Repository repository)
    {
        var recipe = await repository.GetRecipe(opts.Slug);
        if (recipe == null)
        {
            throw new FileNotFoundException($"Recipe with slug '{opts.Slug}' not found.");
        }

        var content = opts.Format switch
        {
            ExportFormat.Markdown => MarkdownExporter.Export(recipe),
            ExportFormat.Json => JsonExporter.Export(recipe),
            _ => throw new ArgumentOutOfRangeException(nameof(opts.Format), "Unsupported export format.")
        };

        if (string.IsNullOrWhiteSpace(opts.Output))
        {
            await System.Console.Out.WriteLineAsync(content);
        }
        else
        {
            await File.WriteAllTextAsync(opts.Output, content);
        }

        return 0;
    }
}