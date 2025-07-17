using CommandLine;
using System.Diagnostics;
using RecipeBox.Console.Commands;
using RecipeBox.Data;

namespace RecipeBox.Console;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var defaultRepoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Recipes");
        var repository = new Repository(defaultRepoPath);
        
        var parser = new Parser(with =>
        {
            with.EnableDashDash = true;
            with.CaseInsensitiveEnumValues = true;
            with.HelpWriter = System.Console.Error;
        });

        return await parser.ParseArguments<
            ImportOptions,
            ExportOptions,
            PdfOptions,
            ShowOptions,
            EditOptions,
            DeleteOptions,
            ListOptions,
            TransformOptions
        >(args)
        .MapResult(
            (ImportOptions opts) => HandleCommand(opts, (o) => ImportCommand.Handle(o, repository)),
            (ExportOptions opts) => HandleCommand(opts, (o) => ExportCommand.Handle(o, repository)),
            (PdfOptions opts) => HandleCommand(opts, (o) => PdfCommand.Handle(o, repository)),
            (ShowOptions opts) => HandleCommand(opts, (o) => ShowCommand.Handle(o, repository)),
            (EditOptions opts) => HandleCommand(opts, (o) => EditCommand.Handle(o, repository)),
            (DeleteOptions opts) => HandleCommand(opts, (o) => DeleteCommand.Handle(o, repository)),
            (ListOptions opts) => HandleCommand(opts, (o) => ListCommand.Handle(o, repository)),
            (TransformOptions opts) => HandleCommand(opts, (o) => TransformCommand.Handle(o, repository)),
            errs => Task.FromResult(1)
        );
    }

    private static async Task<int> HandleCommand<T>(T opts, Func<T, Task<int>> handler)
    {
        if (Debugger.IsAttached)
        {
            return await handler(opts);
        }
        
        try
        {
            return await handler(opts);
        }
        catch (Exception ex)
        {
            await System.Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }
}
