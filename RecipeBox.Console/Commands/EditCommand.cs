using CommandLine;
using RecipeBox.Data;
using RecipeBox.Editor;

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
        await File.WriteAllTextAsync(tempFile, originalRecipe.Content);

        var editor = new TextEditor(tempFile);

        editor.Validator = text =>
        {
            try
            {
                Recipe.Parse(text, originalRecipe);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        };

        var saved = editor.Run();

        if (saved)
        {
            var newRecipe = Recipe.Parse(editor.Text, originalRecipe);
            await repository.UpdateRecipe(newRecipe);
            await System.Console.Out.WriteLineAsync("Recipe updated successfully.");
        }
        else
        {
            await System.Console.Out.WriteLineAsync("Edit cancelled. Your changes were not saved.");
        }

        File.Delete(tempFile);
        return 0;
    }
}