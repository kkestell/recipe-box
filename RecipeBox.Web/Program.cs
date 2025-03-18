using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;

namespace RecipeBox.Web;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var appLifetime = new CancellationTokenSource();

        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var recipesPath = Path.Combine(documentsPath, "Recipes");

        if (!Directory.Exists(recipesPath))
            Directory.CreateDirectory(recipesPath);

        var library = await Library.Create(recipesPath);
        
        var app = builder.Build();

        // Choose the appropriate file provider based on the build configuration
        var staticFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "static");
        IFileProvider fileProvider;

#if DEBUG
        // In debug mode, use physical files
        if (Directory.Exists(staticFilesPath))
        {
            fileProvider = new PhysicalFileProvider(staticFilesPath);
            Console.WriteLine("Using physical files from: " + staticFilesPath);
        }
        else
        {
            // Fallback to embedded resources if the directory doesn't exist
            fileProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
            Console.WriteLine("DEBUG: Static directory not found, falling back to embedded resources");
        }
#else
        // In release mode, use embedded resources
        fileProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
        Console.WriteLine("Using embedded resources for static files");
#endif

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = "/static"
        });

        app.MapGet("/", () => Results.Content(Pages.Index, "text/html"));

        // The rest of your endpoints remain unchanged
        app.MapPost("/recipes", async (HttpRequest request) =>
        {
            var content = "";
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }
            
            if (string.IsNullOrEmpty(content))
            {
                return Results.BadRequest("No content received.");
            }
            
            try
            {
                var recipe = RecipeParser.Parse(content);
                var slug = await library.CreateRecipe(recipe);
                return Results.Ok(new RecipeCreationResponse(slug, recipe.Title));
            }
            catch (SyntaxError ex)
            {
                return Results.BadRequest("Invalid recipe format.");
            }
        });

        app.MapGet("/recipes", () =>
        {
            var recipes = library.ListRecipes()
                .OrderBy(r => r.Slug)
                .ToDictionary(
                    r => r.Slug,
                    r => r.Recipe
                );
            
            return Results.Ok(recipes);
        });

        app.MapPost("/recipes/import", () =>
        {
            return Results.BadRequest("Not Implemented");
        });

        app.MapPost("/recipes/validate", async (HttpRequest request) =>
        {
            var content = "";
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }
    
            if (string.IsNullOrEmpty(content))
            {
                return Results.BadRequest("No content received.");
            }

            try
            {
                RecipeParser.Parse(content);
            }
            catch (SyntaxError ex)
            {
                return Results.BadRequest(new ValidationErrorResponse(ex.Message, ex.LineNumber));
            }

            return Results.NoContent();
        });

        app.MapGet("/recipes/{slug}", (string slug) =>
        {
            if (!library.RecipeExists(slug))
            {
                return Results.NotFound($"Recipe '{slug}' not found.");
            }

            var recipe = library.GetRecipe(slug);
            var content = Renderer.Render(recipe);
            return Results.Text(content);
        });

        app.MapPut("/recipes/{slug}", async (string slug, HttpRequest request) =>
        {
            var content = "";
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
            {
                content = reader.ReadToEndAsync().Result;
            }
            
            if (string.IsNullOrEmpty(content))
            {
                return Results.BadRequest("No content received.");
            }

            try
            {
                var recipe = RecipeParser.Parse(content);
                await library.UpdateRecipe(slug, recipe);
                return Results.NoContent();
            }
            catch (SyntaxError ex)
            {
                return Results.BadRequest("Invalid recipe format.");
            }
        });

        app.MapDelete("/recipes/{slug}", async (string slug) =>
        {
            if (!library.RecipeExists(slug))
            {
                return Results.NotFound($"Recipe '{slug}' not found.");
            }

            await library.DeleteRecipe(slug);
            return Results.NoContent();
        });

        app.MapPost("/recipes/{slug}/pdf", async (string slug) =>
        {
            if (!library.RecipeExists(slug))
            {
                return Results.NotFound($"Recipe '{slug}' not found.");
            }

            var recipe = library.GetRecipe(slug);
        
            var pdfResult = await Pdf.GenerateFromRecipe(recipe);
            if (!pdfResult.Success)
            {
                return Results.BadRequest(pdfResult.ErrorMessage);
            }
            
            return Results.Ok(new PdfGenerationResponse(true, pdfResult.Base64Data));
        });
        
        app.MapPost("/shutdown", () => 
        {
            appLifetime.Cancel();
            return Results.Ok();
        });
        
        Browser.Launch(5000);

        await app.RunAsync(appLifetime.Token);
    }

    // Response types
    public record RecipeCreationResponse(string Slug, string Title);
    public record ValidationErrorResponse(string Message, int Line);
    public record PdfGenerationResponse(bool Success, string PdfData);
}

[JsonSerializable(typeof(Dictionary<string, Recipe>))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(Program.RecipeCreationResponse))]
[JsonSerializable(typeof(Program.ValidationErrorResponse))]
[JsonSerializable(typeof(Program.PdfGenerationResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}