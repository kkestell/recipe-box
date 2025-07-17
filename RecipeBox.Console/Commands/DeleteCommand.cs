using CommandLine;
using RecipeBox.Data;

namespace RecipeBox.Console.Commands;

[Verb("delete", HelpText = "Delete a recipe from the repository.")]
public class DeleteOptions
{
    [Value(0, Required = true, HelpText = "Slug of the recipe to delete.")]
    public string Slug { get; set; } = "";
}

public static class DeleteCommand
{
    public static async Task<int> Handle(DeleteOptions opts, Repository repository)
    {
        var recipe = await repository.GetRecipe(opts.Slug);
        if (recipe == null)
        {
            throw new FileNotFoundException($"Recipe with slug '{opts.Slug}' not found.");
        }

        await repository.DeleteRecipe(recipe);
        return 0;
    }
}