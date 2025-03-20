using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;

using RecipeBox.Core;
using RecipeBox.Scrapers;

namespace RecipeBox;

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

        var configuration = builder.Configuration;
        var libraryPath = configuration["LibraryPath"];
        
        if (string.IsNullOrEmpty(libraryPath))
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            libraryPath = Path.Combine(documentsPath, "Recipes");
        }

        var recipesPath = new DirectoryInfo(libraryPath);

        var library = await Library.Create(recipesPath);
        
        var app = builder.Build();

        var staticFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "static");
        IFileProvider fileProvider;

        fileProvider = new PhysicalFileProvider(staticFilesPath);
        Console.WriteLine("Using physical files from: " + staticFilesPath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = "/static"
        });

        app.MapGet("/", () => Results.Content(Pages.Index, "text/html"));

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

        app.MapGet("/recipes/import", async (HttpRequest request) =>
        {
            var url = request.Query["url"].ToString();
            
            if (string.IsNullOrEmpty(url))
                return Results.BadRequest("No URL provided.");

            // try
            // {
                var recipe = await RecipeScraper.ScrapeRecipe(url);
                return Results.Text(Renderer.Render(recipe));
            // }
            // catch (Exception ex)
            // {
            //     return Results.BadRequest("Failed to scrape recipe.");
            // }
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
            // appLifetime.Cancel();
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