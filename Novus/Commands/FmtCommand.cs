using System;
using System.IO;
using System.Collections.Generic;
using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Formatting;
using Novus.Parser;
using Tomlyn;

namespace Novus.Commands;

/// <summary>
/// Handles the 'novusc fmt' command for formatting Novus source code
/// </summary>
public static class FmtCommand
{
    public static int Run(FmtOptions options)
    {
        try
        {
            // Determine the path to format
            string targetPath;

            if (string.IsNullOrEmpty(options.Path))
            {
                // No path specified - use current directory
                targetPath = Directory.GetCurrentDirectory();
            }
            else
            {
                targetPath = Path.GetFullPath(options.Path);
            }

            // Determine if it's a file or directory
            if (File.Exists(targetPath))
            {
                // Single file
                return FormatFile(targetPath, options);
            }
            else if (Directory.Exists(targetPath))
            {
                // Directory - check for workspace or project
                return FormatDirectory(targetPath, options);
            }
            else
            {
                Console.WriteLine($"Error: Path not found: {targetPath}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (options.Verbose)
            {
                Console.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static int FormatFile(string filePath, FmtOptions options)
    {
        // Check if it's a Novus file
        if (!filePath.EndsWith(".novus", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Skipping non-Novus file: {filePath}");
            return 0;
        }

        // Read source file
        string source = File.ReadAllText(filePath);

        // Parse using ANTLR directly so we can access the token stream for comment preservation
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokenStream);

        // Remove default console error listener and add our own
        var diagnostics = new DiagnosticBag();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new NovusErrorListener(diagnostics, filePath, source));

        // Parse the compilation unit
        var tree = parser.compilationUnit();

        // Check for parse errors
        if (diagnostics.HasErrors)
        {
            Console.WriteLine($"Parse errors in {filePath}:");
            Console.WriteLine(diagnostics.FormatDiagnostics());
            return 1;
        }

        // Format the tree with the token stream to preserve comments
        var formatter = new NovusFormatter(tokenStream, options.IndentSize, options.MaxWidth);
        string formattedCode = formatter.Format(tree);

        // Normalize line endings for comparison
        var normalizedSource = source.Replace("\r\n", "\n").TrimEnd() + "\n";
        var normalizedFormatted = formattedCode.Replace("\r\n", "\n").TrimEnd() + "\n";

        if (options.Check)
        {
            // Check mode - don't write, just report if changes needed
            if (normalizedFormatted != normalizedSource)
            {
                Console.WriteLine($"Would reformat: {filePath}");
                return 1;  // Exit code 1 when changes needed
            }
            else if (options.Verbose)
            {
                Console.WriteLine($"Already formatted: {filePath}");
            }
        }
        else
        {
            // Actually format the file
            if (normalizedFormatted != normalizedSource)
            {
                File.WriteAllText(filePath, formattedCode);
                Console.WriteLine($"Formatted: {filePath}");
            }
            else if (options.Verbose)
            {
                Console.WriteLine($"Unchanged: {filePath}");
            }
        }

        return 0;
    }

    private static int FormatDirectory(string dirPath, FmtOptions options)
    {
        // Check for workspace.toml
        var workspaceFile = Path.Combine(dirPath, "workspace.toml");
        if (File.Exists(workspaceFile))
        {
            return FormatWorkspace(dirPath, workspaceFile, options);
        }

        // Check for project.toml
        var projectFile = Path.Combine(dirPath, "project.toml");
        if (File.Exists(projectFile))
        {
            return FormatProject(dirPath, projectFile, options);
        }

        // No workspace or project - format all .novus files recursively
        return FormatAllNovusFiles(dirPath, options);
    }

    private static int FormatWorkspace(string workspaceDir, string workspaceFile, FmtOptions options)
    {
        Console.WriteLine($"Formatting workspace: {Path.GetFileName(workspaceDir)}");

        // Parse workspace.toml to get member projects
        var tomlContent = File.ReadAllText(workspaceFile);
        var workspace = Toml.ToModel<WorkspaceToml>(tomlContent);

        var members = workspace?.Workspace?.Members ?? new List<string>();

        if (members.Count == 0)
        {
            Console.WriteLine("Warning: No member projects found in workspace.toml");
            return 0;
        }

        int totalFormatted = 0;
        int totalUnchanged = 0;
        int totalErrors = 0;

        foreach (var member in members)
        {
            var projectDir = Path.Combine(workspaceDir, member);
            var projectFile = Path.Combine(projectDir, "project.toml");

            if (Directory.Exists(projectDir) && File.Exists(projectFile))
            {
                Console.WriteLine($"\n  Project: {member}");
                var (formatted, unchanged, errors) = FormatProjectFiles(projectDir, projectFile, options);
                totalFormatted += formatted;
                totalUnchanged += unchanged;
                totalErrors += errors;
            }
            else
            {
                Console.WriteLine($"  Warning: Project not found: {member}");
            }
        }

        // Print summary
        Console.WriteLine();
        PrintSummary(totalFormatted, totalUnchanged, totalErrors, options.Check);

        return totalErrors > 0 ? 1 : 0;
    }

    private static int FormatProject(string projectDir, string projectFile, FmtOptions options)
    {
        Console.WriteLine($"Formatting project: {Path.GetFileName(projectDir)}");

        var (formatted, unchanged, errors) = FormatProjectFiles(projectDir, projectFile, options);

        // Print summary
        Console.WriteLine();
        PrintSummary(formatted, unchanged, errors, options.Check);

        return errors > 0 ? 1 : 0;
    }

    private static (int formatted, int unchanged, int errors) FormatProjectFiles(string projectDir, string projectFile, FmtOptions options)
    {
        int formatted = 0;
        int unchanged = 0;
        int errors = 0;

        // Parse project.toml to get source directory
        var tomlContent = File.ReadAllText(projectFile);
        var project = Toml.ToModel<ProjectToml>(tomlContent);

        var srcPath = project?.Paths?.Src ?? "src";
        var srcDir = Path.Combine(projectDir, srcPath);

        if (!Directory.Exists(srcDir))
        {
            Console.WriteLine($"    Warning: Source directory not found: {srcDir}");
            return (0, 0, 0);
        }

        // Format all .novus files in src directory
        var novusFiles = Directory.EnumerateFiles(srcDir, "*.novus", SearchOption.AllDirectories);

        foreach (var file in novusFiles)
        {
            var relativePath = Path.GetRelativePath(projectDir, file);

            var result = FormatSingleFile(file, relativePath, options);
            switch (result)
            {
                case FormatResult.Formatted:
                    formatted++;
                    break;
                case FormatResult.Unchanged:
                    unchanged++;
                    break;
                case FormatResult.Error:
                    errors++;
                    break;
            }
        }

        return (formatted, unchanged, errors);
    }

    private static int FormatAllNovusFiles(string dirPath, FmtOptions options)
    {
        Console.WriteLine($"Formatting all Novus files in: {dirPath}");

        var novusFiles = Directory.EnumerateFiles(dirPath, "*.novus", SearchOption.AllDirectories);

        int formatted = 0;
        int unchanged = 0;
        int errors = 0;

        foreach (var file in novusFiles)
        {
            var relativePath = Path.GetRelativePath(dirPath, file);

            var result = FormatSingleFile(file, relativePath, options);
            switch (result)
            {
                case FormatResult.Formatted:
                    formatted++;
                    break;
                case FormatResult.Unchanged:
                    unchanged++;
                    break;
                case FormatResult.Error:
                    errors++;
                    break;
            }
        }

        // Print summary
        Console.WriteLine();
        PrintSummary(formatted, unchanged, errors, options.Check);

        return errors > 0 ? 1 : 0;
    }

    private enum FormatResult
    {
        Formatted,
        Unchanged,
        Error
    }

    private static FormatResult FormatSingleFile(string filePath, string displayPath, FmtOptions options)
    {
        try
        {
            // Read source file
            string source = File.ReadAllText(filePath);

            // Parse using ANTLR directly so we can access the token stream
            var inputStream = new AntlrInputStream(source);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            // Remove default console error listener and add our own
            var diagnostics = new DiagnosticBag();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new NovusErrorListener(diagnostics, filePath, source));

            // Parse the compilation unit
            var tree = parser.compilationUnit();

            // Check for parse errors
            if (diagnostics.HasErrors)
            {
                Console.WriteLine($"    Error: {displayPath} - parse errors");
                if (options.Verbose)
                {
                    Console.WriteLine(diagnostics.FormatDiagnostics());
                }
                return FormatResult.Error;
            }

            // Format the tree with the token stream to preserve comments
            var formatter = new NovusFormatter(tokenStream, options.IndentSize, options.MaxWidth);
            string formattedCode = formatter.Format(tree);

            // Normalize line endings for comparison
            var normalizedSource = source.Replace("\r\n", "\n").TrimEnd() + "\n";
            var normalizedFormatted = formattedCode.Replace("\r\n", "\n").TrimEnd() + "\n";

            if (options.Check)
            {
                // Check mode
                if (normalizedFormatted != normalizedSource)
                {
                    Console.WriteLine($"    Would reformat: {displayPath}");
                    return FormatResult.Formatted;  // Count as "would be formatted"
                }
                else if (options.Verbose)
                {
                    Console.WriteLine($"    OK: {displayPath}");
                }
                return FormatResult.Unchanged;
            }
            else
            {
                // Format mode
                if (normalizedFormatted != normalizedSource)
                {
                    File.WriteAllText(filePath, formattedCode);
                    Console.WriteLine($"    Formatted: {displayPath}");
                    return FormatResult.Formatted;
                }
                else if (options.Verbose)
                {
                    Console.WriteLine($"    Unchanged: {displayPath}");
                }
                return FormatResult.Unchanged;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Error: {displayPath} - {ex.Message}");
            return FormatResult.Error;
        }
    }

    private static void PrintSummary(int formatted, int unchanged, int errors, bool checkMode)
    {
        if (checkMode)
        {
            if (formatted > 0)
            {
                Console.WriteLine($"{formatted} file(s) would be reformatted");
            }
            if (errors > 0)
            {
                Console.WriteLine($"{errors} file(s) had errors");
            }
            if (formatted == 0 && errors == 0)
            {
                Console.WriteLine("All files are properly formatted");
            }
        }
        else
        {
            if (formatted > 0)
            {
                Console.WriteLine($"{formatted} file(s) formatted");
            }
            if (errors > 0)
            {
                Console.WriteLine($"{errors} file(s) had errors");
            }
            if (formatted == 0 && errors == 0)
            {
                Console.WriteLine("All files were already formatted");
            }
        }
    }

    // TOML model classes for parsing workspace.toml and project.toml
    private class WorkspaceToml
    {
        public WorkspaceSection? Workspace { get; set; }
    }

    private class WorkspaceSection
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public List<string> Members { get; set; } = new();
    }

    private class ProjectToml
    {
        public PackageSection? Package { get; set; }
        public PathsSection? Paths { get; set; }
    }

    private class PackageSection
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Type { get; set; }
    }

    private class PathsSection
    {
        public string? Src { get; set; }
    }
}
