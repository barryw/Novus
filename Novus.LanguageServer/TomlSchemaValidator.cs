using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Tomlyn.Model;

namespace Novus.LanguageServer;

/// <summary>
/// Validates project.toml and workspace.toml files against the Novus schema.
/// Provides detailed diagnostics for invalid configurations.
/// </summary>
public class TomlSchemaValidator
{
    internal static readonly string[] ValidPackageTypes = { "cli", "workbench", "dual", "library", "device", "handler", "resource" };
    internal static readonly string[] ValidTargetCpus = { "68020", "68030", "68040", "68060", "68080", "auto" };
    internal static readonly string[] ValidFpuModes = { "soft", "none", "68881", "68882", "68040", "68060", "auto" };
    internal static readonly string[] ValidChipsets = { "OCS", "ECS", "AGA", "auto" };

    /// <summary>
    /// Validates a project.toml file and returns diagnostics.
    /// </summary>
    public static List<Diagnostic> ValidateProjectToml(TomlTable table, string text, string? documentPath = null)
    {
        var diagnostics = new List<Diagnostic>();
        var lines = text.Split('\n');
        var baseDirectory = documentPath != null ? System.IO.Path.GetDirectoryName(documentPath) : null;

        // Validate [package] section
        if (!table.ContainsKey("package"))
        {
            diagnostics.Add(CreateDiagnostic(0, 0, "Missing required [package] section", DiagnosticSeverity.Error));
        }
        else if (table["package"] is TomlTable packageTable)
        {
            ValidatePackageSection(packageTable, lines, diagnostics);
        }

        // Validate [build] section (optional but validate if present)
        if (table.ContainsKey("build") && table["build"] is TomlTable buildTable)
        {
            ValidateBuildSection(buildTable, lines, diagnostics, baseDirectory);
        }

        // Validate [paths] section (optional but validate if present)
        if (table.ContainsKey("paths") && table["paths"] is TomlTable pathsTable)
        {
            ValidatePathsSection(pathsTable, lines, diagnostics, baseDirectory);
        }

        // Validate [dependencies] section (optional but validate if present)
        if (table.ContainsKey("dependencies") && table["dependencies"] is TomlTable depsTable)
        {
            ValidateDependenciesSection(depsTable, lines, diagnostics);
        }

        // Check for unknown top-level sections
        var knownSections = new HashSet<string> { "package", "build", "paths", "dependencies" };
        foreach (var key in table.Keys)
        {
            if (!knownSections.Contains(key))
            {
                var (line, column, length) = FindKeyLocation(lines, key, isSection: true);
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    $"Unknown section '[{key}]'. Valid sections are: {string.Join(", ", knownSections.Select(s => $"[{s}]"))}",
                    DiagnosticSeverity.Warning
                ));
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates a workspace.toml file and returns diagnostics.
    /// </summary>
    public static List<Diagnostic> ValidateWorkspaceToml(TomlTable table, string text, string? documentPath = null)
    {
        var diagnostics = new List<Diagnostic>();
        var lines = text.Split('\n');
        var baseDirectory = documentPath != null ? System.IO.Path.GetDirectoryName(documentPath) : null;

        // Validate [workspace] section
        if (!table.ContainsKey("workspace"))
        {
            diagnostics.Add(CreateDiagnostic(0, 0, "Missing required [workspace] section", DiagnosticSeverity.Error));
        }
        else if (table["workspace"] is TomlTable workspaceTable)
        {
            // Find the [workspace] section header for missing key errors
            var workspaceLocation = FindKeyLocation(lines, "workspace", isSection: true);

            // Validate members array
            if (!workspaceTable.ContainsKey("members"))
            {
                diagnostics.Add(CreateDiagnostic(workspaceLocation.Line, workspaceLocation.Column, "Missing required 'members' key in [workspace] section", DiagnosticSeverity.Error, workspaceLocation.Length));
            }
            else if (workspaceTable["members"] is not TomlArray membersArray)
            {
                var (line, column, length) = FindKeyLocation(lines, "members");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'members' must be an array of strings",
                    DiagnosticSeverity.Error
                ));
            }
            else
            {
                // Validate that member directories exist
                ValidateDirectoryArray(membersArray, "members", lines, diagnostics, baseDirectory);
            }

            // Validate optional [workspace.build] subsection
            if (workspaceTable.ContainsKey("build") && workspaceTable["build"] is TomlTable workspaceBuildTable)
            {
                // Use the same validation as project build section
                ValidateBuildSection(workspaceBuildTable, lines, diagnostics, baseDirectory);
            }

            // Check for unknown keys in workspace section
            var knownKeys = new HashSet<string> { "name", "description", "version", "authors", "members", "build" };
            foreach (var key in workspaceTable.Keys)
            {
                if (!knownKeys.Contains(key))
                {
                    var (line, column, length) = FindKeyLocation(lines, key);
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"Unknown key '{key}' in [workspace] section. Valid keys are: {string.Join(", ", knownKeys)}",
                        DiagnosticSeverity.Warning,
                        length
                    ));
                }
            }
        }

        // Check for unknown top-level sections
        var knownSections = new HashSet<string> { "workspace" };
        foreach (var key in table.Keys)
        {
            if (!knownSections.Contains(key))
            {
                var (line, column, length) = FindKeyLocation(lines, key, isSection: true);
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    $"Unknown section '[{key}]'. workspace.toml should only have [workspace] section",
                    DiagnosticSeverity.Warning
                ));
            }
        }

        return diagnostics;
    }

    private static void ValidatePackageSection(TomlTable packageTable, string[] lines, List<Diagnostic> diagnostics)
    {
        // Find the [package] section header for missing key errors
        var packageLocation = FindKeyLocation(lines, "package", isSection: true);

        // Required: name
        if (!packageTable.ContainsKey("name"))
        {
            diagnostics.Add(CreateDiagnostic(packageLocation.Line, packageLocation.Column,
                "Missing required 'name' key in [package] section", DiagnosticSeverity.Error, packageLocation.Length));
        }
        else if (packageTable["name"] is not string || string.IsNullOrWhiteSpace(packageTable["name"] as string))
        {
            var (line, column, length) = FindKeyLocation(lines, "name");
            diagnostics.Add(CreateDiagnostic(
                line, column,
                "'name' must be a non-empty string",
                DiagnosticSeverity.Error,
                length
            ));
        }

        // Required: version
        if (!packageTable.ContainsKey("version"))
        {
            diagnostics.Add(CreateDiagnostic(packageLocation.Line, packageLocation.Column,
                "Missing required 'version' key in [package] section", DiagnosticSeverity.Error, packageLocation.Length));
        }
        else if (packageTable["version"] is not string)
        {
            var (line, column, length) = FindKeyLocation(lines, "version");
            diagnostics.Add(CreateDiagnostic(
                line, column,
                "'version' must be a string (e.g., \"1.0.0\")",
                DiagnosticSeverity.Error,
                length
            ));
        }

        // Required: type
        if (!packageTable.ContainsKey("type"))
        {
            diagnostics.Add(CreateDiagnostic(packageLocation.Line, packageLocation.Column,
                "Missing required 'type' key in [package] section", DiagnosticSeverity.Error, packageLocation.Length));
        }
        else if (packageTable["type"] is string typeValue)
        {
            if (!ValidPackageTypes.Contains(typeValue))
            {
                var (line, column, length) = FindKeyLocation(lines, "type");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    $"Invalid package type '{typeValue}'. Valid types are: {string.Join(", ", ValidPackageTypes)}",
                    DiagnosticSeverity.Error,
                    length
                ));
            }
        }
        else
        {
            var (line, column, length) = FindValueLocation(lines, "type");
            diagnostics.Add(CreateDiagnostic(
                line, column,
                "'type' must be a string",
                DiagnosticSeverity.Error,
                length
            ));
        }

        // Optional: description, entry, authors, license
        var knownKeys = new HashSet<string> { "name", "version", "type", "description", "entry", "authors", "license" };
        foreach (var key in packageTable.Keys)
        {
            if (!knownKeys.Contains(key))
            {
                var (line, column, length) = FindKeyLocation(lines, key);
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    $"Unknown key '{key}' in [package] section. Valid keys are: {string.Join(", ", knownKeys)}",
                    DiagnosticSeverity.Warning,
                    length
                ));
            }
        }
    }

    private static void ValidateBuildSection(TomlTable buildTable, string[] lines, List<Diagnostic> diagnostics, string? baseDirectory)
    {
        // Validate target_cpu
        if (buildTable.ContainsKey("target_cpu"))
        {
            if (buildTable["target_cpu"] is string targetCpu)
            {
                if (!ValidTargetCpus.Contains(targetCpu))
                {
                    // Highlight the value, not the key
                    var (line, column, length) = FindValueLocation(lines, "target_cpu");
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"Invalid target_cpu '{targetCpu}'. Valid values are: {string.Join(", ", ValidTargetCpus)}",
                        DiagnosticSeverity.Error,
                        length
                    ));
                }
            }
            else
            {
                var (line, column, length) = FindValueLocation(lines, "target_cpu");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'target_cpu' must be a string",
                    DiagnosticSeverity.Error
                ));
            }
        }

        // Validate fpu
        if (buildTable.ContainsKey("fpu"))
        {
            if (buildTable["fpu"] is string fpu)
            {
                if (!ValidFpuModes.Contains(fpu))
                {
                    var (line, column, length) = FindValueLocation(lines, "fpu");
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"Invalid fpu mode '{fpu}'. Valid values are: {string.Join(", ", ValidFpuModes)}",
                        DiagnosticSeverity.Error,
                        length
                    ));
                }
            }
            else
            {
                var (line, column, length) = FindValueLocation(lines, "fpu");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'fpu' must be a string",
                    DiagnosticSeverity.Error
                ));
            }
        }

        // Validate chipset
        if (buildTable.ContainsKey("chipset"))
        {
            if (buildTable["chipset"] is string chipset)
            {
                if (!ValidChipsets.Contains(chipset))
                {
                    var (line, column, length) = FindValueLocation(lines, "chipset");
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"Invalid chipset '{chipset}'. Valid values are: {string.Join(", ", ValidChipsets)}",
                        DiagnosticSeverity.Error,
                        length
                    ));
                }
            }
            else
            {
                var (line, column, length) = FindValueLocation(lines, "chipset");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'chipset' must be a string",
                    DiagnosticSeverity.Error
                ));
            }
        }

        // Validate optimization_level
        if (buildTable.ContainsKey("optimization_level"))
        {
            if (buildTable["optimization_level"] is long optLevel)
            {
                if (optLevel < 0 || optLevel > 3)
                {
                    var (line, column, length) = FindValueLocation(lines, "optimization_level");
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"optimization_level must be between 0 and 3 (got {optLevel})",
                        DiagnosticSeverity.Error,
                        length
                    ));
                }
            }
            else
            {
                var (line, column, length) = FindValueLocation(lines, "optimization_level");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'optimization_level' must be an integer (0-3)",
                    DiagnosticSeverity.Error
                ));
            }
        }

        // Validate emit_asm
        if (buildTable.ContainsKey("emit_asm") && buildTable["emit_asm"] is not bool)
        {
            var (line, column, length) = FindValueLocation(lines, "emit_asm");
            diagnostics.Add(CreateDiagnostic(
                line, column,
                "'emit_asm' must be a boolean (true or false)",
                DiagnosticSeverity.Error,
                length
            ));
        }

        // Validate asm_files
        if (buildTable.ContainsKey("asm_files"))
        {
            if (buildTable["asm_files"] is not TomlArray asmFilesArray)
            {
                var (line, column, length) = FindValueLocation(lines, "asm_files");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'asm_files' must be an array of strings",
                    DiagnosticSeverity.Error,
                    length
                ));
            }
            else
            {
                // Validate that assembly files exist
                ValidateFileArray(asmFilesArray, "asm_files", lines, diagnostics, baseDirectory);
            }
        }

        // Check for unknown keys
        var knownKeys = new HashSet<string> { "target_cpu", "fpu", "chipset", "output", "optimization_level", "emit_asm", "asm_files" };
        foreach (var key in buildTable.Keys)
        {
            if (!knownKeys.Contains(key))
            {
                var (line, column, length) = FindKeyLocation(lines, key);
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    $"Unknown key '{key}' in [build] section. Valid keys are: {string.Join(", ", knownKeys)}",
                    DiagnosticSeverity.Warning,
                    length
                ));
            }
        }
    }

    private static void ValidatePathsSection(TomlTable pathsTable, string[] lines, List<Diagnostic> diagnostics, string? baseDirectory)
    {
        // Validate src
        if (pathsTable.ContainsKey("src"))
        {
            if (pathsTable["src"] is not string srcPath)
            {
                var (line, column, length) = FindValueLocation(lines, "src");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'src' must be a string",
                    DiagnosticSeverity.Error,
                    length
                ));
            }
            else
            {
                // Validate that the src directory exists
                ValidateDirectoryPath(srcPath, "src", lines, diagnostics, baseDirectory);
            }
        }

        // Validate lib
        if (pathsTable.ContainsKey("lib"))
        {
            if (pathsTable["lib"] is not TomlArray libArray)
            {
                var (line, column, length) = FindValueLocation(lines, "lib");
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    "'lib' must be an array of strings",
                    DiagnosticSeverity.Error,
                    length
                ));
            }
            else
            {
                // Validate that library directories exist
                ValidateDirectoryArray(libArray, "lib", lines, diagnostics, baseDirectory);
            }
        }

        // Check for unknown keys
        var knownKeys = new HashSet<string> { "src", "lib" };
        foreach (var key in pathsTable.Keys)
        {
            if (!knownKeys.Contains(key))
            {
                var (line, column, length) = FindKeyLocation(lines, key);
                diagnostics.Add(CreateDiagnostic(
                    line, column,
                    $"Unknown key '{key}' in [paths] section. Valid keys are: {string.Join(", ", knownKeys)}",
                    DiagnosticSeverity.Error,
                    length
                ));
            }
        }
    }

    private static void ValidateDependenciesSection(TomlTable depsTable, string[] lines, List<Diagnostic> diagnostics)
    {
        foreach (var depName in depsTable.Keys)
        {
            if (depsTable[depName] is TomlTable depTable)
            {
                // Validate dependency keys
                var knownKeys = new HashSet<string> { "version", "path", "git", "branch", "tag" };
                foreach (var key in depTable.Keys)
                {
                    if (!knownKeys.Contains(key))
                    {
                        var (line, column, length) = FindKeyLocation(lines, key);
                        diagnostics.Add(CreateDiagnostic(
                            line, column,
                            $"Unknown key '{key}' in dependency '{depName}'. Valid keys are: {string.Join(", ", knownKeys)}",
                            DiagnosticSeverity.Warning
                        ));
                    }
                }

                // Validate that branch/tag require git
                if ((depTable.ContainsKey("branch") || depTable.ContainsKey("tag")) && !depTable.ContainsKey("git"))
                {
                    var (line, column, length) = FindKeyLocation(lines, depName);
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"Dependency '{depName}' specifies branch/tag but is missing 'git' URL",
                        DiagnosticSeverity.Error
                    ));
                }
            }
        }
    }

    /// <summary>
    /// Validates that a directory path exists.
    /// </summary>
    private static void ValidateDirectoryPath(string path, string keyName, string[] lines, List<Diagnostic> diagnostics, string? baseDirectory)
    {
        if (baseDirectory == null)
            return;

        var fullPath = System.IO.Path.Combine(baseDirectory, path);
        if (!System.IO.Directory.Exists(fullPath))
        {
            var (line, column, length) = FindValueLocation(lines, keyName);
            diagnostics.Add(CreateDiagnostic(
                line, column,
                $"Directory not found: {path}",
                DiagnosticSeverity.Error,
                length
            ));
        }
    }

    /// <summary>
    /// Validates that all paths in an array are existing directories.
    /// </summary>
    private static void ValidateDirectoryArray(TomlArray array, string keyName, string[] lines, List<Diagnostic> diagnostics, string? baseDirectory)
    {
        if (baseDirectory == null)
            return;

        foreach (var item in array)
        {
            if (item is string path)
            {
                var fullPath = System.IO.Path.Combine(baseDirectory, path);
                if (!System.IO.Directory.Exists(fullPath))
                {
                    // Find the specific array element to highlight just that directory name
                    var (line, column, length) = FindArrayElementLocation(lines, keyName, path);
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"Directory not found: {path}",
                        DiagnosticSeverity.Error,
                        length
                    ));
                }
            }
        }
    }

    /// <summary>
    /// Validates that all paths in an array are existing files.
    /// </summary>
    private static void ValidateFileArray(TomlArray array, string keyName, string[] lines, List<Diagnostic> diagnostics, string? baseDirectory)
    {
        if (baseDirectory == null)
            return;

        foreach (var item in array)
        {
            if (item is string path)
            {
                var fullPath = System.IO.Path.Combine(baseDirectory, path);
                if (!System.IO.File.Exists(fullPath))
                {
                    // Find the specific array element to highlight just that file name
                    var (line, column, length) = FindArrayElementLocation(lines, keyName, path);
                    diagnostics.Add(CreateDiagnostic(
                        line, column,
                        $"File not found: {path}",
                        DiagnosticSeverity.Error,
                        length
                    ));
                }
            }
        }
    }

    private static (int Line, int Column, int Length) FindKeyLocation(string[] lines, string key, bool isSection = false)
    {
        var searchPattern = isSection ? $"\\[{key}\\]" : $"{key}\\s*=";

        for (int i = 0; i < lines.Length; i++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], searchPattern);
            if (match.Success)
            {
                // For sections, return the whole match including brackets
                // For keys, return only the key name length (not including whitespace and '=')
                int length = isSection ? match.Length : key.Length;
                return (i, match.Index, length);
            }
        }

        return (0, 0, 1); // Fallback
    }

    private static (int Line, int Column, int Length) FindValueLocation(string[] lines, string key)
    {
        var searchPattern = $"{key}\\s*=\\s*";

        for (int i = 0; i < lines.Length; i++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], searchPattern);
            if (match.Success)
            {
                // Find the value after the '='
                var valueStart = match.Index + match.Length;
                var restOfLine = lines[i].Substring(valueStart);

                // Match quoted string - highlight content inside quotes, not the quotes themselves
                var quotedMatch = System.Text.RegularExpressions.Regex.Match(restOfLine, @"^(""([^""]*)""|'([^']*)')");
                if (quotedMatch.Success)
                {
                    // Skip the opening quote and highlight just the content
                    var content = quotedMatch.Groups[2].Success ? quotedMatch.Groups[2].Value : quotedMatch.Groups[3].Value;
                    return (i, valueStart + 1, content.Length);
                }

                // Match number, boolean, or array start (no quotes to skip)
                var otherMatch = System.Text.RegularExpressions.Regex.Match(restOfLine, @"^(true|false|\d+|\[)");
                if (otherMatch.Success)
                {
                    return (i, valueStart, otherMatch.Length);
                }

                // Fallback: highlight rest of line
                return (i, valueStart, restOfLine.TrimEnd().Length);
            }
        }

        return (0, 0, 1); // Fallback
    }

    /// <summary>
    /// Finds the location of a specific string value within an array.
    /// For example, in: members = ["cli", "other"]
    /// This will find "cli" or "other" and return its position without quotes.
    /// </summary>
    private static (int Line, int Column, int Length) FindArrayElementLocation(string[] lines, string key, string value)
    {
        var searchPattern = $"{key}\\s*=\\s*";

        for (int i = 0; i < lines.Length; i++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], searchPattern);
            if (match.Success)
            {
                // Find the array after the '='
                var arrayStart = match.Index + match.Length;
                var restOfLine = lines[i].Substring(arrayStart);

                // Look for the specific value within the array, possibly across multiple lines
                var currentLine = i;
                var searchText = restOfLine;

                // Keep searching until we find the closing bracket or run out of lines
                while (currentLine < lines.Length)
                {
                    // Escape special regex characters in the value
                    var escapedValue = System.Text.RegularExpressions.Regex.Escape(value);

                    // Match the value inside quotes: "value" or 'value'
                    var valuePattern = $@"[""']({escapedValue})[""']";
                    var valueMatch = System.Text.RegularExpressions.Regex.Match(searchText, valuePattern);

                    if (valueMatch.Success)
                    {
                        // Calculate the column position
                        int column;
                        if (currentLine == i)
                        {
                            // Same line as the key
                            column = arrayStart + valueMatch.Groups[1].Index;
                        }
                        else
                        {
                            // Different line - just use the position in the current line
                            column = valueMatch.Groups[1].Index;
                        }

                        return (currentLine, column, valueMatch.Groups[1].Length);
                    }

                    // Check if we've hit the closing bracket
                    if (searchText.Contains("]"))
                    {
                        break;
                    }

                    // Move to next line
                    currentLine++;
                    if (currentLine < lines.Length)
                    {
                        searchText = lines[currentLine];
                    }
                }

                // If we didn't find it, fall back to highlighting the array bracket
                return (i, arrayStart, 1);
            }
        }

        return (0, 0, 1); // Fallback
    }

    private static Diagnostic CreateDiagnostic(int line, int column, string message, DiagnosticSeverity severity, int length = 1)
    {
        return new Diagnostic
        {
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(line, column),
                new Position(line, column + Math.Max(1, length))
            ),
            Severity = severity,
            Source = "novus-toml",
            Message = message
        };
    }
}
