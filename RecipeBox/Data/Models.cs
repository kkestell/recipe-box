using System.Text.RegularExpressions;

namespace RecipeBox.Data;

public class ParsingError(string message, int lineNumber) : Exception($"{message} (Line: {lineNumber})")
{
    public int LineNumber { get; } = lineNumber;
}

public enum BlockType
{
    MetadataDelimiter,
    MetadataPair,
    Title,
    Component,
    Step,
    Ingredient,
    Blank,
    Unknown
}

public readonly struct Block(BlockType type, string? value, int lineNumber)
{
    public BlockType Type { get; } = type;
    public string? Value { get; } = value;
    public int LineNumber { get; } = lineNumber;
}

public class Step(string text)
{
    public string Text { get; set; } = text;
    public List<string> Ingredients { get; set; } = [];
}

public class Component(string? name = null)
{
    public string? Name { get; set; } = name;
    public List<Step> Steps { get; set; } = [];

    public bool IsEmpty => Name == null && Steps.Count == 0;
}

public class Recipe
{
    private enum ParserState
    {
        Start,
        InMetadata,
        InBody
    }

    private Recipe(string content)
    {
        Content = content;
        Components = [];
        Metadata = new Dictionary<string, string>();
        Parse();
    }
    
    private Recipe(string newContent, Recipe originalRecipe) : this(newContent)
    {
        RepositoryFile = originalRecipe.RepositoryFile;
        Slug = originalRecipe.Slug;
        OriginalTitle = originalRecipe.OriginalTitle;
    }

    public static Recipe Parse(string content)
    {
        return new Recipe(content);
    }

    public static Recipe Parse(string newContent, Recipe originalRecipe)
    {
        return new Recipe(newContent, originalRecipe);
    }

    public string Title { get; set; } = "";
    public List<Component> Components { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public string Content { get; set; }
    internal string? OriginalTitle { get; set; }
    public FileInfo? RepositoryFile { get; set; }
    public string? Slug { get; internal set; }
    
    public bool IsDraft => Metadata.ContainsKey("draft") &&  Metadata["draft"] == "true";

    public string Serialize()
    {
        var output = new List<string>();

        void AddSpacer()
        {
            if (output.Count > 0 && !string.IsNullOrEmpty(output.LastOrDefault()))
            {
                output.Add("");
            }
        }

        if (Metadata.Count != 0)
        {
            output.Add("---");
            foreach (var (key, value) in Metadata)
            {
                output.Add($"{key}: {value}");
            }

            output.Add("---");
        }

        AddSpacer();
        output.Add($"= {Title}");

        foreach (var component in Components)
        {
            var isFirstSubstantiveBlock = !component.IsEmpty || Components.Count > 1;
            if (isFirstSubstantiveBlock)
            {
                AddSpacer();
            }

            if (component.Name != null)
            {
                output.Add($"+ {component.Name}");
            }

            if (component.Name != null && component.Steps.Any())
            {
                AddSpacer();
            }

            foreach (var step in component.Steps)
            {
                if (output.Count > 0 && !string.IsNullOrEmpty(output.Last()) && !output.Last().StartsWith('+'))
                {
                    AddSpacer();
                }

                output.Add($"# {step.Text}");
                if (step.Ingredients.Count != 0)
                {
                    AddSpacer();
                    output.AddRange(step.Ingredients.Select(ingredient => $"- {ingredient}"));
                }
            }
        }

        return string.Join("\n", output);
    }

    private static List<Block> Lex(string content)
    {
        var blocks = new List<Block>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var lineNumber = i + 1;

            if (string.IsNullOrWhiteSpace(line))
            {
                blocks.Add(new Block(BlockType.Blank, null, lineNumber));
                continue;
            }

            if (line.Trim() == "---")
            {
                blocks.Add(new Block(BlockType.MetadataDelimiter, null, lineNumber));
            }
            else if (line.StartsWith("= "))
            {
                blocks.Add(new Block(BlockType.Title, line[2..].Trim(), lineNumber));
            }
            else if (line.StartsWith("+ "))
            {
                blocks.Add(new Block(BlockType.Component, line[2..].Trim(), lineNumber));
            }
            else if (line.StartsWith("# "))
            {
                blocks.Add(new Block(BlockType.Step, line[2..].Trim(), lineNumber));
            }
            else if (line.Trim().StartsWith("- "))
            {
                blocks.Add(new Block(BlockType.Ingredient, line.TrimStart(' ', '-').Trim(), lineNumber));
            }
            else if (Regex.IsMatch(line, @"^([\w\s-]+):\s*(.*)$"))
            {
                blocks.Add(new Block(BlockType.MetadataPair, line, lineNumber));
            }
            else
            {
                blocks.Add(new Block(BlockType.Unknown, line, lineNumber));
            }
        }
        return blocks;
    }

    private void Parse()
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new ParsingError("Recipe text cannot be empty", 1);
        }

        var blocks = Lex(Content);
        var state = ParserState.Start;
        
        Component? currentComponent = null;
        Step? currentStep = null;

        foreach (var block in blocks)
        {
            if (block.Type == BlockType.Blank) continue;

            switch (state)
            {
                case ParserState.Start:
                    if (block.Type == BlockType.MetadataDelimiter)
                    {
                        state = ParserState.InMetadata;
                    }
                    else if (block.Type == BlockType.Title)
                    {
                        Title = block.Value!;
                        currentComponent = new Component();
                        Components.Add(currentComponent);
                        state = ParserState.InBody;
                    }
                    else if (block.Type is BlockType.Component or BlockType.Step or BlockType.Ingredient)
                    {
                        throw new ParsingError($"Recipe body cannot start with '{block.Type}'. Must start with a title.", block.LineNumber);
                    }
                    else
                    {
                        throw new ParsingError($"Unexpected block '{block.Type}'. Expected metadata or title.", block.LineNumber);
                    }
                    break;

                case ParserState.InMetadata:
                    if (block.Type == BlockType.MetadataPair)
                    {
                        var match = Regex.Match(block.Value!, @"^([\w\s-]+):\s*(.*)$");
                        var key = match.Groups[1].Value.Trim().ToLower().Replace(" ", "_").Replace("-", "_");
                        Metadata[key] = match.Groups[2].Value.Trim();
                    }
                    else if (block.Type == BlockType.MetadataDelimiter)
                    {
                        state = ParserState.InBody;
                    }
                    else
                    {
                        throw new ParsingError($"Unexpected block '{block.Type}' in metadata.", block.LineNumber);
                    }
                    break;

                case ParserState.InBody:
                    switch (block.Type)
                    {
                        case BlockType.Title:
                            throw new ParsingError("Cannot define a new title.", block.LineNumber);
                        case BlockType.Component:
                            if (currentComponent?.IsEmpty ?? false) Components.Remove(currentComponent);
                            currentComponent = new Component(block.Value);
                            Components.Add(currentComponent);
                            currentStep = null;
                            break;
                        case BlockType.Step:
                            if (currentComponent == null)
                            {
                                currentComponent = new Component();
                                Components.Add(currentComponent);
                            }
                            currentStep = new Step(block.Value!);
                            currentComponent.Steps.Add(currentStep);
                            break;
                        case BlockType.Ingredient:
                            if (currentStep == null) throw new ParsingError("Ingredient found outside a step.", block.LineNumber);
                            currentStep.Ingredients.Add(block.Value!);
                            break;
                        case BlockType.MetadataDelimiter:
                        case BlockType.MetadataPair:
                            throw new ParsingError("Metadata is not allowed in the recipe body.", block.LineNumber);
                        case BlockType.Unknown:
                            throw new ParsingError($"Unknown content: '{block.Value}'", block.LineNumber);
                    }
                    break;
            }
        }
        
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ParsingError("Recipe must have a title.", 1);
        }
        
        if (!Components.SelectMany(c => c.Steps).SelectMany(s => s.Ingredients).Any())
        {
            throw new ParsingError("Recipe must contain at least one ingredient.", blocks.LastOrDefault().LineNumber);
        }

        foreach (var comp in Components)
        {
            if (comp is { Name: not null, Steps.Count: 0 })
            {
                var errorBlock = blocks.First(b => b.Type == BlockType.Component && b.Value == comp.Name);
                throw new ParsingError($"Component '{comp.Name}' must have at least one step.", errorBlock.LineNumber);
            }
        }
    }
}
