using System.Text.RegularExpressions;

namespace RecipeBox.Data;

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

    public string Title { get; set; }
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

    private void Parse()
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new ArgumentException("Recipe text cannot be empty");
        }

        var lines = new Queue<string>(Content.Split('\n').Select(l => l.TrimEnd()));

        if (lines.Peek().Trim() == "---")
        {
            lines.Dequeue();
            while (lines.Count > 0 && lines.Peek().Trim() != "---")
            {
                var line = lines.Dequeue();
                var match = Regex.Match(line, @"^([\w\s-]+):\s*(.*)$");
                if (match.Success)
                {
                    var key = match.Groups[1].Value.Trim().ToLower().Replace(" ", "_").Replace("-", "_");
                    Metadata[key] = match.Groups[2].Value.Trim();
                }
            }

            if (lines.Count > 0)
            {
                lines.Dequeue();
            }
        }

        Component? currentComponent = null;
        Step? currentStep = null;

        while (lines.Count > 0)
        {
            var line = lines.Dequeue();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("= "))
            {
                Title = line[2..].Trim();
                if (currentComponent == null)
                {
                    currentComponent = new Component();
                    Components.Add(currentComponent);
                }

                currentStep = null;
            }
            else if (line.StartsWith("+ "))
            {
                if (currentComponent?.IsEmpty ?? false)
                {
                    Components.RemoveAt(Components.Count - 1);
                }

                currentStep = null;
                currentComponent = new Component(line[2..].Trim());
                Components.Add(currentComponent);
            }
            else if (line.StartsWith("# "))
            {
                if (currentComponent == null)
                {
                    currentComponent = new Component();
                    Components.Add(currentComponent);
                }

                currentStep = new Step(line[2..].Trim());
                currentComponent.Steps.Add(currentStep);
            }
            else if (line.Trim().StartsWith("- "))
            {
                currentStep?.Ingredients.Add(line.TrimStart(' ', '-').Trim());
            }
        }

        if (string.IsNullOrWhiteSpace(Title) && Components.Count == 0)
        {
            throw new ArgumentException("Invalid recipe format: No content found.");
        }

        if (Components.Count == 0)
        {
            Components.Add(new Component());
        }

        var hasIngredients = Components.Any(c => c.Steps.Any(s => s.Ingredients.Count != 0));
        if (!hasIngredients)
        {
            throw new ArgumentException("Recipe must contain at least one ingredient.");
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Recipe must have a title.");
        }
    }
}