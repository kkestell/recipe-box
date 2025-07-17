using CommandLine;
using RecipeBox.Data;

namespace RecipeBox.Console.Commands;

[Verb("transform", HelpText = "Transform a recipe using an LLM prompt.")]
public class TransformOptions
{
    [Value(0, Required = true, HelpText = "Slug of the recipe to transform.")]
    public string Slug { get; set; } = "";

    [Option('p', "prompt", Required = true, HelpText = "Transformation prompt.")]
    public string Prompt { get; set; } = "";

    [Option('o', "output", HelpText = "Output file (default: stdout).")]
    public string? Output { get; set; }

    [Option('i', "interactive", HelpText = "Interactive mode.")]
    public bool Interactive { get; set; }
}

public static class TransformCommand
{
    public static Task<int> Handle(TransformOptions opts, Repository repository)
    {
        return Task.FromResult(0);
    }
}