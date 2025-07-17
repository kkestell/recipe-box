using CommandLine;
using RecipeBox.Data;

namespace RecipeBox.Console.Commands;

[Verb("list", HelpText = "List all recipes in the repository.")]
public class ListOptions
{
}

public static class ListCommand
{
    public static Task<int> Handle(ListOptions opts, Repository repository)
    {
        var recipes = repository.ListRecipes();
        foreach (var slug in recipes)
        {
            System.Console.WriteLine(slug);
        }
        return Task.FromResult(0);
    }
}