using System.Text.Json;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Parser;

namespace Novus.Commands;

/// <summary>
/// Handles the 'novus verify-docs' command for verifying code snippets in documentation
/// </summary>
public static class VerifyDocsCommand
{
    // Regex to match ```novus code blocks in markdown
    private static readonly Regex NovusCodeBlockRegex = new(
        @"```novus\s*\n(.*?)```",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // Regex to match \begin{lstlisting}...\end{lstlisting} in LaTeX (excluding language=bash)
    private static readonly Regex LatexNovusCodeBlockRegex = new(
        @"\\begin\{lstlisting\}(?!\[language=bash\])\s*\n(.*?)\\end\{lstlisting\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // Regex to detect incomplete/illustrative markers
    private static readonly Regex IncompleteMarkerRegex = new(
        @"//\s*(\.\.\.|\[?\.\.\.\]?|incomplete|illustrative|pseudo|example only|not yet implemented|future syntax)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static int Run(VerifyDocsOptions options)
    {
        try
        {
            var targetPath = string.IsNullOrEmpty(options.Path)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(options.Path);

            if (!Directory.Exists(targetPath))
            {
                Console.WriteLine($"Error: Directory not found: {targetPath}");
                return 1;
            }

            var results = new List<SnippetResult>();
            var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Find all markdown, mdx, and LaTeX files
            var mdFiles = Directory.EnumerateFiles(targetPath, "*.md", searchOption)
                .Concat(Directory.EnumerateFiles(targetPath, "*.mdx", searchOption))
                .Concat(Directory.EnumerateFiles(targetPath, "*.tex", searchOption))
                .Where(f => !f.Contains("node_modules"))
                .OrderBy(f => f)
                .ToList();

            foreach (var file in mdFiles)
            {
                var fileResults = VerifyFile(file, targetPath, options);
                results.AddRange(fileResults);
            }

            // Output results
            if (options.JsonOutput)
            {
                OutputJson(results);
            }
            else
            {
                OutputText(results, options.Verbose);
            }

            // Return exit code based on results
            var failCount = results.Count(r => r.Status == SnippetStatus.Fail);
            return failCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static List<SnippetResult> VerifyFile(string filePath, string basePath, VerifyDocsOptions options)
    {
        var results = new List<SnippetResult>();
        var content = File.ReadAllText(filePath);
        var relativePath = Path.GetRelativePath(basePath, filePath);

        // Use appropriate regex based on file extension
        var isLatex = filePath.EndsWith(".tex", StringComparison.OrdinalIgnoreCase);
        var matches = isLatex
            ? LatexNovusCodeBlockRegex.Matches(content)
            : NovusCodeBlockRegex.Matches(content);


        foreach (Match match in matches)
        {
            var snippet = match.Groups[1].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            // Check if snippet is marked as incomplete
            if (!options.IncludeIncomplete && IsIncompleteSnippet(snippet))
            {
                results.Add(new SnippetResult
                {
                    FilePath = relativePath,
                    LineNumber = lineNumber,
                    Snippet = TruncateSnippet(snippet),
                    Status = SnippetStatus.Skip,
                    Message = "Marked as incomplete/illustrative"
                });
                continue;
            }

            // Verify the snippet
            var result = VerifySnippet(snippet, relativePath, lineNumber, options);
            results.Add(result);
        }

        return results;
    }

    private static SnippetResult VerifySnippet(string snippet, string filePath, int lineNumber, VerifyDocsOptions options)
    {
        var result = new SnippetResult
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            Snippet = TruncateSnippet(snippet)
        };

        // Prepare the snippet - wrap if needed
        var wrappedSnippet = WrapSnippetIfNeeded(snippet);

        // Debug: show wrapped snippet
        if (Environment.GetEnvironmentVariable("NOVUS_DEBUG_VERIFY") == "1")
        {
            Console.WriteLine($"=== Original snippet ===\n{snippet}\n=== Wrapped ===\n{wrappedSnippet}\n===");
        }

        // Parse the snippet
        var diagnostics = new DiagnosticBag();

        try
        {
            var inputStream = new AntlrInputStream(wrappedSnippet);
            var lexer = new NovusLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new NovusParser(tokenStream);

            // Remove default console error listener and add our own
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new NovusErrorListener(diagnostics, $"doc:{filePath}:{lineNumber}", wrappedSnippet));

            // Parse the compilation unit
            var tree = parser.compilationUnit();

            // Check for parse errors
            if (diagnostics.HasErrors)
            {
                result.Status = SnippetStatus.Fail;
                var firstError = diagnostics.Diagnostics.FirstOrDefault(d => d.IsError);
                result.Message = $"Parse error: {firstError?.Message ?? "Unknown error"}";
                return result;
            }

            result.Status = SnippetStatus.Pass;
            result.Message = "Syntax OK";
            return result;
        }
        catch (Exception ex)
        {
            result.Status = SnippetStatus.Fail;
            result.Message = $"Parse exception: {ex.Message}";
            return result;
        }
    }

    private static string WrapSnippetIfNeeded(string snippet)
    {
        var trimmed = snippet.Trim();

        // If it's empty, don't wrap
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return snippet;
        }

        // If it's ONLY comments (all lines start with //), don't wrap
        var allLines = trimmed.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (allLines.Count > 0 && allLines.All(l => l.TrimStart().StartsWith("//")))
        {
            return snippet;
        }

        // Check each line to see if any starts with a top-level declaration
        var lines = trimmed.Split('\n').Select(l => l.TrimStart()).ToList();
        var firstNonComment = lines.FirstOrDefault(l => !l.StartsWith("//") && !string.IsNullOrWhiteSpace(l)) ?? "";

        // If it already has a top-level declaration, use as-is
        if (firstNonComment.StartsWith("fn ") ||
            firstNonComment.StartsWith("pub fn ") ||
            firstNonComment.StartsWith("extern fn ") ||
            firstNonComment.StartsWith("extern pub fn ") ||
            firstNonComment.StartsWith("pub extern fn ") ||
            firstNonComment.StartsWith("struct ") ||
            firstNonComment.StartsWith("pub struct ") ||
            firstNonComment.StartsWith("enum ") ||
            firstNonComment.StartsWith("pub enum ") ||
            firstNonComment.StartsWith("trait ") ||
            firstNonComment.StartsWith("pub trait ") ||
            firstNonComment.StartsWith("impl ") ||
            firstNonComment.StartsWith("from ") ||
            firstNonComment.StartsWith("import ") ||
            firstNonComment.StartsWith("module ") ||
            firstNonComment.StartsWith("const ") ||
            firstNonComment.StartsWith("pub const ") ||
            firstNonComment.StartsWith("static ") ||
            firstNonComment.StartsWith("pub static ") ||
            firstNonComment.StartsWith("#[") ||
            firstNonComment.StartsWith("@"))
        {
            return snippet;
        }

        // Check if any line looks like a statement that needs wrapping
        // Use the original lines (not trimmed) but trim for pattern matching
        var originalLines = snippet.Split('\n');
        bool needsWrapping = originalLines.Any(l =>
        {
            var lt = l.TrimStart();
            return lt.StartsWith("let ") ||
                   lt.StartsWith("var ") ||
                   lt.StartsWith("if ") ||
                   lt.StartsWith("while ") ||
                   lt.StartsWith("loop ") ||
                   lt.StartsWith("loop{") ||
                   lt.StartsWith("for ") ||
                   lt.StartsWith("match ") ||
                   lt.StartsWith("return ") ||
                   lt.StartsWith("defer ") ||
                   lt.StartsWith("using ") ||
                   lt.StartsWith("unsafe ") ||
                   lt.StartsWith("unsafe{") ||
                   lt.StartsWith("asm ") ||
                   lt.StartsWith("asm{") ||
                   Regex.IsMatch(lt, @"^\w+\s*=") ||  // Assignment like x = 5
                   Regex.IsMatch(lt, @"^\w+\s*\(");   // Function call like print("hello")
        });

        if (needsWrapping)
        {
            // Wrap in a main function
            return $"fn main() {{\n{snippet}\n}}";
        }

        // Check if it's a block
        if (trimmed.StartsWith("{"))
        {
            return $"fn main() {snippet}";
        }

        // Default: try to wrap in main
        return $"fn main() {{\n{snippet}\n}}";
    }

    private static bool IsIncompleteSnippet(string snippet)
    {
        // Check for common incomplete markers
        if (IncompleteMarkerRegex.IsMatch(snippet))
            return true;

        // Check for standalone ellipsis (but not range syntax ..=)
        if (Regex.IsMatch(snippet, @"(?<!\.)\.\.\.(?!\.)") && !snippet.Contains("..="))
            return true;

        // Check for placeholder comments
        if (snippet.Contains("// TODO") || snippet.Contains("/* ... */"))
            return true;

        // Check if it's a commented-out block (all lines start with //)
        var lines = snippet.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count > 0 && lines.All(l => l.TrimStart().StartsWith("//")))
            return true;

        // Check if it's a keyword/operator reference table (many space-separated words on one line)
        var trimmed = snippet.Trim();
        if (!trimmed.Contains('\n') || trimmed.Split('\n').Length <= 3)
        {
            // Single line or few lines - check for reference table patterns
            var wordCount = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var hasComment = trimmed.Contains("//");

            // If it's a single line with many words and possibly a comment, likely a reference
            if (wordCount >= 5 && hasComment)
                return true;

            // Operator-only lines (just operators with comments)
            if (Regex.IsMatch(trimmed, @"^[\+\-\*/%&\|^~!=<>\.\?:]+\s+//"))
                return true;
        }

        // Check if it looks like a list of keywords (multiple bare identifiers)
        if (Regex.IsMatch(trimmed, @"^[a-z_]+(\s+[a-z_]+){4,}$", RegexOptions.Multiline))
            return true;

        // Check if it's an operator reference (operators with comments)
        // Matches lines like: "+   // addition" or "==  // equal" or "&   // bitwise AND"
        if (Regex.IsMatch(trimmed, @"^[\+\-\*/%&\|^~!=<>\.\?:]+\s+//", RegexOptions.Multiline))
            return true;

        // Check if it's a single operator with a comment on its own
        if (Regex.IsMatch(trimmed, @"^([\+\-\*/%&\|^~!=<>\.\?:]+|\.\.|\.\.=|&&|\|\|)\s*//"))
            return true;

        // Check if the snippet is just showing syntax without actual code
        // e.g., "let var x = ..." or placeholder identifiers
        if (trimmed.Contains("condition") && !trimmed.Contains("==") && !trimmed.Contains("!="))
            return true;

        return false;
    }

    private static int GetLineNumber(string content, int charIndex)
    {
        return content.Take(charIndex).Count(c => c == '\n') + 1;
    }

    private static string TruncateSnippet(string snippet, int maxLength = 60)
    {
        var firstLine = snippet.Split('\n').FirstOrDefault()?.Trim() ?? "";
        if (firstLine.Length > maxLength)
            return firstLine.Substring(0, maxLength) + "...";
        return firstLine;
    }

    private static void OutputText(List<SnippetResult> results, bool verbose)
    {
        var passCount = results.Count(r => r.Status == SnippetStatus.Pass);
        var failCount = results.Count(r => r.Status == SnippetStatus.Fail);
        var skipCount = results.Count(r => r.Status == SnippetStatus.Skip);

        Console.WriteLine();
        Console.WriteLine("Documentation Snippet Verification Results");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        // Group by file
        var byFile = results.GroupBy(r => r.FilePath).OrderBy(g => g.Key);

        foreach (var fileGroup in byFile)
        {
            var fileResults = fileGroup.ToList();
            var filePasses = fileResults.Count(r => r.Status == SnippetStatus.Pass);
            var fileFails = fileResults.Count(r => r.Status == SnippetStatus.Fail);
            var fileSkips = fileResults.Count(r => r.Status == SnippetStatus.Skip);

            // Show file header
            var statusIcon = fileFails > 0 ? "X" : "OK";
            Console.WriteLine($"[{statusIcon}] {fileGroup.Key} ({filePasses} pass, {fileFails} fail, {fileSkips} skip)");

            if (verbose || fileFails > 0)
            {
                foreach (var result in fileResults)
                {
                    var icon = result.Status switch
                    {
                        SnippetStatus.Pass => "  [OK]",
                        SnippetStatus.Fail => "  [FAIL]",
                        SnippetStatus.Skip => "  [SKIP]",
                        _ => "  [?]"
                    };

                    if (verbose || result.Status == SnippetStatus.Fail)
                    {
                        Console.WriteLine($"{icon} Line {result.LineNumber}: {result.Snippet}");
                        if (result.Status != SnippetStatus.Pass)
                        {
                            Console.WriteLine($"        {result.Message}");
                        }
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Summary");
        Console.WriteLine("-------");
        Console.WriteLine($"  Total snippets: {results.Count}");
        Console.WriteLine($"  Passed: {passCount}");
        Console.WriteLine($"  Failed: {failCount}");
        Console.WriteLine($"  Skipped: {skipCount}");
        Console.WriteLine();

        if (failCount == 0)
        {
            Console.WriteLine("All documentation snippets are syntactically valid!");
        }
        else
        {
            Console.WriteLine($"{failCount} snippet(s) have syntax errors.");
        }
    }

    private static void OutputJson(List<SnippetResult> results)
    {
        var output = new VerifyDocsJsonOutput
        {
            Total = results.Count,
            Passed = results.Count(r => r.Status == SnippetStatus.Pass),
            Failed = results.Count(r => r.Status == SnippetStatus.Fail),
            Skipped = results.Count(r => r.Status == SnippetStatus.Skip),
            Results = results.Select(r => new VerifyDocsJsonResult
            {
                File = r.FilePath,
                Line = r.LineNumber,
                Status = r.Status.ToString().ToLower(),
                Snippet = r.Snippet,
                Message = r.Message
            }).ToList()
        };

        Console.WriteLine(JsonSerializer.Serialize(output, VerifyDocsJsonContext.Default.VerifyDocsJsonOutput));
    }
}

public enum SnippetStatus
{
    Pass,
    Fail,
    Skip
}

public class SnippetResult
{
    public string FilePath { get; set; } = "";
    public int LineNumber { get; set; }
    public string Snippet { get; set; } = "";
    public SnippetStatus Status { get; set; }
    public string Message { get; set; } = "";
}

// AOT-compatible JSON output classes
public class VerifyDocsJsonOutput
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<VerifyDocsJsonResult> Results { get; set; } = new();
}

public class VerifyDocsJsonResult
{
    public string File { get; set; } = "";
    public int Line { get; set; }
    public string Status { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string Message { get; set; } = "";
}

// JSON source generator for AOT compatibility
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(VerifyDocsJsonOutput))]
internal partial class VerifyDocsJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
