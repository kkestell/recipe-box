using CliWrap;
using System.Text;
using System.Text.RegularExpressions;

namespace RecipeBox.Data;

public class Repository
{
    private readonly string _repositoryPath;

    public Repository(string repositoryPath)
    {
        _repositoryPath = Path.GetFullPath(repositoryPath);

        if (!Directory.Exists(_repositoryPath))
        {
            Directory.CreateDirectory(_repositoryPath);
        }

        if (!Directory.Exists(Path.Combine(_repositoryPath, ".git")))
        {
            ExecuteGitCommandAsync("init").GetAwaiter().GetResult();
            // ExecuteGitCommandAsync("config", "user.name", "Test User").GetAwaiter().GetResult();
            // ExecuteGitCommandAsync("config", "user.email", "test@example.com").GetAwaiter().GetResult();
            // ExecuteGitCommandAsync("config", "commit.gpgsign", "false").GetAwaiter().GetResult();
        }
    }

    public async Task AddRecipe(Recipe recipe)
    {
        var filePath = GetUniqueFilePath(recipe.Title);

        recipe.RepositoryFile = new FileInfo(filePath);
        recipe.Slug = Path.GetFileNameWithoutExtension(filePath);
        recipe.OriginalTitle = recipe.Title;

        await File.WriteAllTextAsync(filePath, recipe.Serialize());
        await GitAdd(filePath);
        await GitCommit($"Added \"{recipe.Title}\"");
    }

    public async Task UpdateRecipe(Recipe recipe)
    {
        if (recipe.RepositoryFile is null || recipe.OriginalTitle is null)
        {
            throw new InvalidOperationException("This recipe has not been saved to the repository yet. Use AddRecipe instead.");
        }

        var oldFilePath = recipe.RepositoryFile.FullName;
        var oldTitle = recipe.OriginalTitle;
        var newTitle = recipe.Title;

        // First, always write the updated content to the file.
        await File.WriteAllTextAsync(oldFilePath, recipe.Serialize());

        // Next, determine if the filename needs to change based on the new title.
        var newFilePath = GetUniqueFilePath(newTitle, oldFilePath);
        var slugChanged = newFilePath != oldFilePath;

        string commitMessage;

        if (slugChanged)
        {
            // If the slug changed, move the file in git.
            await GitMove(oldFilePath, newFilePath);
            commitMessage = $"Renamed \"{oldTitle}\" to \"{newTitle}\"";
            
            // Update the recipe object's state to reflect the move.
            recipe.RepositoryFile = new FileInfo(newFilePath);
            recipe.Slug = Path.GetFileNameWithoutExtension(newFilePath);
        }
        else
        {
            // If the slug is the same, just add the modified file to staging.
            await GitAdd(oldFilePath);
            commitMessage = $"Updated \"{newTitle}\"";
        }

        // Commit the staged changes with the appropriate message.
        await GitCommit(commitMessage);

        // Finally, update the original title to match the new state for future edits.
        recipe.OriginalTitle = newTitle;
    }

    public async Task DeleteRecipe(Recipe recipe)
    {
        if (recipe.RepositoryFile is null || !File.Exists(recipe.RepositoryFile.FullName))
        {
            throw new InvalidOperationException("Cannot delete a recipe that does not exist in the repository.");
        }

        await GitRemove(recipe.RepositoryFile.FullName);
        await GitCommit($"Deleted \"{recipe.Title}\"");
    }

    public async Task<Recipe?> GetRecipe(string slug)
    {
        var filePath = Path.Combine(_repositoryPath, $"{slug}.txt");
        if (!File.Exists(filePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(filePath);
        var recipe = Recipe.Parse(content);
        recipe.RepositoryFile = new FileInfo(filePath);
        recipe.Slug = Path.GetFileNameWithoutExtension(filePath);
        recipe.OriginalTitle = recipe.Title;

        return recipe;
    }

    public async Task<IReadOnlyList<Recipe>> GetRecipes()
    {
        var slugs = ListRecipes();
        var recipeTasks = slugs.Select(GetRecipe);
        var results = await Task.WhenAll(recipeTasks);
        return results.Where(r => r is not null).ToList()!;
    }

    public IReadOnlyList<string> ListRecipes()
    {
        return Directory.EnumerateFiles(_repositoryPath, "*.txt")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(s => s is not null)
            .ToList()!;
    }

    private static string GenerateSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "untitled-recipe";

        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');
        slug = slug.Length > 60 ? slug.Substring(0, 60).Trim('-') : slug;

        return string.IsNullOrWhiteSpace(slug) ? "recipe" : slug;
    }

    private string GetUniqueFilePath(string title, string? originalPathToExclude = null)
    {
        var baseSlug = GenerateSlug(title);
        var filePath = Path.Combine(_repositoryPath, $"{baseSlug}.txt");
        var counter = 1;

        while (File.Exists(filePath) && (originalPathToExclude == null || !filePath.Equals(originalPathToExclude, StringComparison.OrdinalIgnoreCase)))
        {
            filePath = Path.Combine(_repositoryPath, $"{baseSlug}-{counter++}.txt");
        }
        return filePath;
    }

    private async Task ExecuteGitCommandAsync(params string[] args)
    {
        var stdOutBuffer = new StringBuilder();
        var stdErrBuffer = new StringBuilder();

        var cmd = Cli.Wrap("git")
            .WithArguments(args)
            .WithWorkingDirectory(_repositoryPath)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
            .WithValidation(CommandResultValidation.None);

        var result = await cmd.ExecuteAsync();

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {result.ExitCode}.\n" +
                $"Arguments: git {string.Join(" ", args)}\n" +
                $"Standard Error: {stdErrBuffer}\n" +
                $"Standard Output: {stdOutBuffer}");
        }
    }

    private Task GitAdd(string filePath) => ExecuteGitCommandAsync("add", filePath);

    private Task GitRemove(string filePath) => ExecuteGitCommandAsync("rm", filePath);

    private Task GitMove(string oldPath, string newPath) => ExecuteGitCommandAsync("mv", oldPath, newPath);

    private Task GitCommit(string message) => ExecuteGitCommandAsync("commit", "-m", message);
}
