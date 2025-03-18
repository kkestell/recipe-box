using System.Diagnostics;

namespace RecipeBox;

public static class Pdf
{
    public record PdfResult(bool Success, string Base64Data, string ErrorMessage = "");
    
    public static async Task<PdfResult> GenerateFromRecipe(Recipe recipe)
    {
        var typstData = RecipeToTypst(recipe);

        // Create temporary Typst file
        var tempTypPath = Path.GetTempFileName();
        File.Move(tempTypPath, Path.ChangeExtension(tempTypPath, ".typ"));
        tempTypPath = Path.ChangeExtension(tempTypPath, ".typ");
        
        await File.WriteAllTextAsync(tempTypPath, typstData);

        // Create temporary PDF path
        var tempPdfPath = Path.ChangeExtension(Path.GetTempFileName(), ".pdf");
        
        try 
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "typst",
                    Arguments = $"compile \"{tempTypPath}\" \"{tempPdfPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var errorOutput = await process.StandardError.ReadToEndAsync();
                return new PdfResult(false, "", $"Failed to generate PDF: {errorOutput}");
            }

            // Read and encode PDF
            var pdfData = await File.ReadAllBytesAsync(tempPdfPath);
            var base64Pdf = Convert.ToBase64String(pdfData);

            return new PdfResult(true, base64Pdf);
        }
        catch (Exception ex)
        {
            return new PdfResult(false, "", $"Error in PDF generation: {ex.Message}");
        }
        finally
        {
            // Clean up temporary files
            try 
            {
                if (File.Exists(tempTypPath)) File.Delete(tempTypPath);
                if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath);
            }
            catch 
            {
                // Ignore cleanup errors
            }
        }
    }
    
    private static string RecipeToTypst(Recipe recipe)
    {
        var parts = new List<string>();

        parts.AddRange(new[]
        {
            "#set page(",
            "  paper: \"us-letter\"",
            ")",
            ""
        });

        parts.Add($"#align(center)[= {recipe.Title}]");
        parts.Add("");
        parts.Add("#v(1cm)");
        parts.Add("");

        var stepNumber = 1;
        foreach (var section in recipe.Sections)
        {
            if (!string.IsNullOrEmpty(section.Title))
            {
                parts.Add($"== {section.Title}\n");
            }

            foreach (var step in section.Steps)
            {
                // Join paragraphs with newlines
                var paragraphText = TextUtils.Pretty(string.Join("\n", step.Paragraphs));
                parts.Add($"{stepNumber}. {paragraphText}");
                stepNumber++;

                if (step.Ingredients.Count > 0)
                {
                    parts.Add("");
                    foreach (var ingredient in step.Ingredients)
                    {
                        parts.Add($"   - *{TextUtils.Pretty(ingredient)}*");
                    }
                }

                parts.Add("");
            }
        }

        return string.Join("\n", parts).Trim();
    }
}