using System.Reflection;

namespace RecipeBox.Core;

public class Library
{
    private readonly DirectoryInfo _libraryDir;
    private readonly Dictionary<string, Recipe> _recipes = new();
    
    private Library(DirectoryInfo libraryPath)
    {
        _libraryDir = libraryPath;
    }
    
    public static async Task<Library> Create(DirectoryInfo libraryPath)
    {
        var library = new Library(libraryPath);
        
        if (!libraryPath.Exists)
        {
            Directory.CreateDirectory(libraryPath.FullName);
            InitializeWithExampleRecipe(libraryPath);
        }
        
        await library.LoadRecipes();
        return library;
    }
    
    private static void InitializeWithExampleRecipe(DirectoryInfo libraryDir)
    {
        const string exampleFilename = "banana-bread.rcp";
        
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"RecipeBox.Core.recipes.{exampleFilename}");

        if (stream == null) 
            return;
        
        using var reader = new StreamReader(stream);
        var recipeContent = reader.ReadToEnd();
            
        var recipePath = Path.Combine(libraryDir.FullName, exampleFilename);
            
        if (!File.Exists(recipePath))
            File.WriteAllText(recipePath, recipeContent);
    }
    
    private async Task LoadRecipes()
    {
        foreach (var recipeFile in _libraryDir.GetFiles("*.rcp"))
        {
            var slug = Path.GetFileNameWithoutExtension(recipeFile.Name);
            var content = await File.ReadAllTextAsync(recipeFile.FullName, System.Text.Encoding.UTF8);
            
            try
            {
                var recipe = RecipeParser.Parse(content);
                _recipes[slug] = recipe;
            }
            catch (SyntaxError ex)
            {
                Console.WriteLine($"Skipping {recipeFile.Name}: Syntax error ({ex.Message})");
            }
        }
    }
    
    public bool RecipeExists(string slug)
    {
        return _recipes.ContainsKey(slug);
    }
    
    public List<(string Slug, Recipe Recipe)> ListRecipes()
    {
        return _recipes
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }
    
    public Recipe GetRecipe(string slug)
    {
        return _recipes[slug];
    }
    
    public async Task<string> CreateRecipe(Recipe recipe)
    {
        var title = string.IsNullOrEmpty(recipe.Title) ? "Untitled" : recipe.Title;
        var slug = TextUtils.Slugify(title);
        var counter = 1;
        
        while (_recipes.ContainsKey(slug) || File.Exists(Path.Combine(_libraryDir.FullName, $"{slug}.rcp")))
        {
            slug = $"{slug}-{counter}";
            counter++;
        }
        
        var content = Renderer.Render(recipe);
        _recipes[slug] = recipe;
        var recipePath = Path.Combine(_libraryDir.FullName, $"{slug}.rcp");
        await File.WriteAllTextAsync(recipePath, content, System.Text.Encoding.UTF8);
        
        return slug;
    }
    
    public async Task UpdateRecipe(string slug, Recipe recipe)
    {
        if (_recipes.TryGetValue(slug, out var existingRecipe) && existingRecipe.Equals(recipe))
        {
            return;
        }
        
        var recipeText = Renderer.Render(recipe);
        _recipes[slug] = recipe;
        var recipePath = Path.Combine(_libraryDir.FullName, $"{slug}.rcp");
        await File.WriteAllTextAsync(recipePath, recipeText, System.Text.Encoding.UTF8);
    }
    
    public async Task DeleteRecipe(string slug)
    {
        if (!_recipes.ContainsKey(slug))
        {
            throw new ArgumentException($"Recipe '{slug}' not found", nameof(slug));
        }
        
        var recipePath = Path.Combine(_libraryDir.FullName, $"{slug}.rcp");
        if (File.Exists(recipePath))
        {
            await Task.Run(() => File.Delete(recipePath));
        }
        
        _recipes.Remove(slug);
    }
}