using CommandLine;
using RecipeBox.Data;

namespace RecipeBox.Console.Commands;

[Verb("show", HelpText = "Display a recipe from the repository.")]
public class ShowOptions
{
    [Value(0, Required = true, HelpText = "Slug of the recipe to show.")]
    public string Slug { get; set; } = "";
}

public static class ShowCommand
{
    public static async Task<int> Handle(ShowOptions opts, Repository repository)
    {
        var recipe = await repository.GetRecipe(opts.Slug);
        if (recipe == null)
        {
            throw new FileNotFoundException($"Recipe with slug '{opts.Slug}' not found.");
        }
        
        System.Console.WriteLine(recipe.Serialize());
        return 0;
    }
}