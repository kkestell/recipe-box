using System.Diagnostics;
using System.Runtime.InteropServices;
using CommandLine;
using RecipeBox.Data;

namespace RecipeBox.Console.Commands;

[Verb("edit", HelpText = "Edit a recipe in the repository.")]
public class EditOptions
{
    [Value(0, Required = true, HelpText = "Slug of the recipe to edit.")]
    public string Slug { get; set; } = "";
}

public static class EditCommand
{
    public static async Task<int> Handle(EditOptions opts, Repository repository)
    {
        var originalRecipe = await repository.GetRecipe(opts.Slug);
        if (originalRecipe?.RepositoryFile == null)
        {
            throw new FileNotFoundException($"Recipe with slug '{opts.Slug}' not found.");
        }

        var tempFile = Path.GetTempFileName();
        File.Copy(originalRecipe.RepositoryFile.FullName, tempFile, true);

        while (true)
        {
            await LaunchEditorAndWaitAsync(tempFile);
            var content = await File.ReadAllTextAsync(tempFile);

            try
            {
                var newRecipe = Recipe.Parse(content, originalRecipe);
                await repository.UpdateRecipe(newRecipe);
                break;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error: \nError parsing recipe: {ex.Message}");
                System.Console.Error.Write("Would you like to (r)e-edit or (a)bort? ");
                var choice = System.Console.ReadKey().KeyChar.ToString().ToLower();
                System.Console.Error.WriteLine();

                if (choice == "r") continue;
                
                throw new InvalidOperationException($"Edit aborted. Your changes are preserved in '{tempFile}'");
            }
        }
        
        File.Delete(tempFile);
        return 0;
    }
    
    private static async Task LaunchEditorAndWaitAsync(string filePath)
    {
        var editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            editor = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "notepad" : "vim";
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = editor,
                Arguments = filePath,
                UseShellExecute = true,
                CreateNoWindow = false,
            }
        };

        process.Start();
        await process.WaitForExitAsync();
    }
}