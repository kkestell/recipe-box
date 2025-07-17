using System.ComponentModel;
using System.Diagnostics;
using CommandLine;
using RecipeBox.Data;
using RecipeBox.Render;

namespace RecipeBox.Console.Commands;

[Verb("pdf", HelpText = "Export recipes to a PDF file.")]
public class PdfOptions
{
    [Option('o', "output", HelpText = "Output PDF file name.")]
    public string? Output { get; set; }

    [Option("include", HelpText = "Slugs of recipes to include. If used, only these recipes will be exported.")]
    public IEnumerable<string> Include { get; set; } = Enumerable.Empty<string>();

    [Option("exclude", HelpText = "Slugs of recipes to exclude from the export.")]
    public IEnumerable<string> Exclude { get; set; } = Enumerable.Empty<string>();

    [Option("include-drafts", Default = false, HelpText = "Include draft recipes in the export.")]
    public bool IncludeDrafts { get; set; }
}

public static class PdfCommand
{
    public static async Task<int> Handle(PdfOptions opts, Repository repository)
    {
        List<Recipe> recipes;

        if (opts.Include.Any())
        {
            // If --include is used, ignore other filters and just get the specified recipes.
            var includedRecipes = new List<Recipe>();
            foreach (var slug in opts.Include)
            {
                var recipe = await repository.GetRecipe(slug);
                if (recipe == null) throw new FileNotFoundException($"Recipe with slug '{slug}' not found.");
                includedRecipes.Add(recipe);
            }
            recipes = includedRecipes;
        }
        else
        {
            // Get all recipes and apply filters.
            var allRecipes = await repository.GetRecipes();
            
            var query = allRecipes.AsEnumerable();

            if (!opts.IncludeDrafts)
            {
                query = query.Where(r => !r.IsDraft);
            }

            if (opts.Exclude.Any())
            {
                var exclusions = new HashSet<string>(opts.Exclude);
                query = query.Where(r => !exclusions.Contains(r.Slug));
            }
            
            recipes = query.ToList();
        }

        if (!recipes.Any())
        {
            await System.Console.Out.WriteLineAsync("No recipes match the criteria. PDF not generated.");
            return 0;
        }

        var outputFile = string.IsNullOrWhiteSpace(opts.Output) ? "cookbook.pdf" : opts.Output;

        var typstContent = TypstRenderer.Render(recipes);
        var tempTypstFile = Path.ChangeExtension(Path.GetTempFileName(), ".typ");

        try
        {
            await File.WriteAllTextAsync(tempTypstFile, typstContent);
            await CompileTypstAsync(tempTypstFile, outputFile);
            await System.Console.Out.WriteLineAsync($"Successfully created {outputFile}");
        }
        finally
        {
            if (File.Exists(tempTypstFile))
            {
                File.Delete(tempTypstFile);
            }
        }

        return 0;
    }

    private static async Task CompileTypstAsync(string inputFile, string outputFile)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "typst",
                Arguments = $"compile \"{inputFile}\" \"{outputFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException)
        {
            throw new InvalidOperationException("Could not find the 'typst' executable. Please ensure it is installed and in your PATH.", ex);
        }

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Typst compilation failed with exit code {process.ExitCode}:\n{error}");
        }
    }
}
