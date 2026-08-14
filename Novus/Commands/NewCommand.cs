using System;
using System.IO;
using System.Text;

namespace Novus.Commands;

/// <summary>
/// Handles the 'novusc new' command for creating new projects from templates
/// </summary>
public static class NewCommand
{
    public static int Run(NewCommandOptions options)
    {
        try
        {
            // Check if we're inside a workspace (workspace.toml exists in current directory)
            var currentDir = Directory.GetCurrentDirectory();
            var workspaceFile = Path.Combine(currentDir, "workspace.toml");
            bool insideWorkspace = File.Exists(workspaceFile);

            if (insideWorkspace)
            {
                // We're inside a workspace - create a new project within it
                return CreateProjectInWorkspace(currentDir, options);
            }
            else
            {
                // We're not in a workspace - create a new workspace
                return CreateNewWorkspace(options);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int CreateNewWorkspace(NewCommandOptions options)
    {
        string workspaceDir;
        string workspaceName;

        if (options.ProjectName == ".")
        {
            workspaceDir = Directory.GetCurrentDirectory();
            workspaceName = Path.GetFileName(workspaceDir);
        }
        else
        {
            workspaceName = options.ProjectName;
            workspaceDir = Path.Combine(Directory.GetCurrentDirectory(), workspaceName);
        }

        // Check if directory already exists
        if (Directory.Exists(workspaceDir) && Directory.GetFiles(workspaceDir).Length > 0)
        {
            Console.WriteLine($"Error: Directory '{workspaceDir}' already exists and is not empty");
            return 1;
        }

        Console.WriteLine($"Creating new workspace: {workspaceName}");
        Console.WriteLine();

        // If a template type is specified, copy from templates directory
        if (!string.IsNullOrEmpty(options.ProjectType))
        {
            return CreateWorkspaceFromTemplate(workspaceName, workspaceDir, options);
        }

        // Otherwise, create empty workspace
        return CreateEmptyWorkspace(workspaceName, workspaceDir, options);
    }

    private static int CreateEmptyWorkspace(string workspaceName, string workspaceDir, NewCommandOptions options)
    {
        // Create workspace structure
        Directory.CreateDirectory(workspaceDir);
        Console.WriteLine($"  ✓ Created workspace directory: {workspaceName}/");

        // Create workspace.toml (workspace file)
        var workspaceToml = GenerateWorkspaceToml(workspaceName, options);
        File.WriteAllText(Path.Combine(workspaceDir, "workspace.toml"), workspaceToml);
        Console.WriteLine("  ✓ Created workspace.toml (workspace file)");

        // Create .gitignore
        var gitignorePath = Path.Combine(workspaceDir, ".gitignore");
        File.WriteAllText(gitignorePath, GenerateGitignore());
        Console.WriteLine("  ✓ Created .gitignore");

        // Create README
        var readmePath = Path.Combine(workspaceDir, "README.md");
        File.WriteAllText(readmePath, GenerateWorkspaceReadme(workspaceName, options));
        Console.WriteLine("  ✓ Created README.md");

        Console.WriteLine();
        Console.WriteLine("Empty workspace is ready!");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        if (options.ProjectName != ".")
        {
            Console.WriteLine($"  cd {workspaceName}");
        }
        Console.WriteLine("  novusc new my-app --type cli       # Add a CLI project");
        Console.WriteLine("  novusc new my-lib --type library   # Add a library project");
        Console.WriteLine("  novusc new my-gui --type workbench # Add a Workbench project");
        Console.WriteLine();
        Console.WriteLine("Happy coding! 🚀");

        return 0;
    }

    private static int CreateWorkspaceFromTemplate(string workspaceName, string workspaceDir, NewCommandOptions options)
    {
        // Find template directory
        var requestedType = (options.ProjectType ?? "cli").ToLowerInvariant();
        var bundledType = requestedType switch
        {
            "workbench" => "gui",
            "dual" => "cli",
            _ => requestedType
        };
        var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", bundledType);

        if (!Directory.Exists(templateDir))
        {
            Console.WriteLine($"Error: Template '{options.ProjectType}' not found");
            Console.WriteLine($"Available templates:");
            Console.WriteLine("  - cli");
            Console.WriteLine("  - workbench");
            Console.WriteLine("  - dual");
            Console.WriteLine("  - library");
            Console.WriteLine("  - device");
            Console.WriteLine("  - resource");
            Console.WriteLine("  - handler");
            Console.WriteLine("  - gui");
            return 1;
        }

        // Copy entire template directory
        CopyDirectory(templateDir, workspaceDir, true);
        Console.WriteLine($"  ✓ Created workspace from '{options.ProjectType}' template");

        // Replace placeholder names in files
        ReplaceTemplatePlaceholders(workspaceDir, workspaceName);
        if (requestedType is "workbench" or "dual")
        {
            foreach (var projectFile in Directory.GetFiles(workspaceDir, "project.toml", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(projectFile);
                content = requestedType == "workbench"
                    ? content.Replace("type = \"executable\"", "type = \"workbench\"")
                    : content.Replace("type = \"cli\"", "type = \"dual\"");
                File.WriteAllText(projectFile, content);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Workspace '{workspaceName}' created from template!");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        if (options.ProjectName != ".")
        {
            Console.WriteLine($"  cd {workspaceName}");
        }
        Console.WriteLine("  novusc build              # Build all projects");
        Console.WriteLine("  novusc new my-gui --type workbench # Add more projects");
        Console.WriteLine();
        Console.WriteLine("Happy coding! 🚀");

        return 0;
    }

    /// <summary>
    /// Build output that must never be copied out of a template.
    ///
    /// Anyone who runs a build inside templates/ leaves these behind. They are
    /// gitignored, so a clean checkout looks fine, but the directory on disk is what
    /// gets copied - so a stale tree silently ships its leftovers into every new
    /// project. That is how a 468-byte skeleton executable ended up in the bins/
    /// directory of freshly created gui and workbench workspaces, alongside the real
    /// ones and indistinguishable from them at a glance.
    /// </summary>
    internal static readonly HashSet<string> BuildOutputDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "target", "build", ".novus-cache", "usercache", "bin", "obj"
    };

    internal static void CopyDirectory(string sourceDir, string destDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        // Create destination directory
        Directory.CreateDirectory(destDir);

        // Copy files
        foreach (FileInfo file in dir.GetFiles())
        {
            if (file.Name == ".DS_Store") continue;
            string targetFilePath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // Copy subdirectories
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                if (BuildOutputDirectories.Contains(subDir.Name)) continue;
                string newDestinationDir = Path.Combine(destDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    private static void ReplaceTemplatePlaceholders(string directory, string projectName)
    {
        // Replace {{PROJECT_NAME}} placeholder in all text files
        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            // Skip binary files
            var ext = Path.GetExtension(file).ToLower();
            if (ext == ".exe" || ext == ".dll" || ext == ".o" || ext == ".a")
                continue;

            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("{{PROJECT_NAME}}"))
                {
                    content = content.Replace("{{PROJECT_NAME}}", projectName);
                    File.WriteAllText(file, content);
                }
            }
            catch
            {
                // Skip files that can't be read as text
            }
        }
    }

    private static int CreateProjectInWorkspace(string workspaceDir, NewCommandOptions options)
    {
        // Default to "cli" when adding projects to a workspace
        string projectType = options.ProjectType ?? "cli";

        // Validate project type
        var validTypes = new[] { "cli", "workbench", "dual", "library", "device", "resource", "handler" };
        if (!validTypes.Contains(projectType.ToLower()))
        {
            Console.WriteLine($"Error: Invalid project type '{projectType}'");
            Console.WriteLine($"Valid types: {string.Join(", ", validTypes)}");
            return 1;
        }

        // Update options with normalized type for use below
        options.ProjectType = projectType;

        var projectName = options.ProjectName;
        var projectDir = Path.Combine(workspaceDir, projectName);

        // Check if project directory already exists
        if (Directory.Exists(projectDir))
        {
            Console.WriteLine($"Error: Project '{projectName}' already exists in this workspace");
            return 1;
        }

        Console.WriteLine($"Adding new {options.ProjectType} project to workspace: {projectName}");
        Console.WriteLine();

        CreateProjectStructure(projectDir, projectName, options);

        // Update workspace.toml to add this project to members
        UpdateWorkspaceMembers(workspaceDir, projectName);

        Console.WriteLine();
        Console.WriteLine("Project added to workspace!");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  cd {projectName}");
        Console.WriteLine("  # Edit src/main.novus");
        Console.WriteLine("  cd ..");
        Console.WriteLine("  novusc build              # Build all projects");
        Console.WriteLine($"  novusc build {projectName} # Build specific project");
        Console.WriteLine();
        Console.WriteLine("Happy coding! 🚀");

        return 0;
    }

    private static void CreateProjectStructure(string projectDir, string projectName, NewCommandOptions options)
    {
        // Create directories
        Directory.CreateDirectory(projectDir);
        Console.WriteLine($"  ✓ Created directory: {projectName}/");

        var srcDir = Path.Combine(projectDir, "src");
        Directory.CreateDirectory(srcDir);

        // Create project.toml
        var tomlPath = Path.Combine(projectDir, "project.toml");
        File.WriteAllText(tomlPath, GenerateToml(projectName, options));
        Console.WriteLine("  ✓ Created project.toml");

        // Reuse the same source files as top-level workspace templates so the two
        // `new` paths cannot drift apart.
        var (sourceFileName, source) = GenerateSourceFile(projectName, options);
        var mainPath = Path.Combine(srcDir, sourceFileName);
        File.WriteAllText(mainPath, source);
        Console.WriteLine($"  ✓ Created src/{sourceFileName}");

        // Create .gitignore
        var gitignorePath = Path.Combine(projectDir, ".gitignore");
        File.WriteAllText(gitignorePath, GenerateGitignore());
        Console.WriteLine("  ✓ Created .gitignore");

        // Create README for workbench apps
        if (options.ProjectType == "workbench" || options.ProjectType == "dual")
        {
            var readmePath = Path.Combine(projectDir, "README.md");
            File.WriteAllText(readmePath, GenerateReadme(projectName, options));
            Console.WriteLine("  ✓ Created README.md");
        }
    }

    private static string GenerateToml(string projectName, NewCommandOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[package]");
        sb.AppendLine($"name = \"{projectName}\"");
        sb.AppendLine("version = \"0.1.0\"");
        sb.AppendLine($"type = \"{options.ProjectType}\"");

        if (!string.IsNullOrEmpty(options.Description))
        {
            sb.AppendLine($"description = \"{options.Description}\"");
        }

        if (!string.IsNullOrEmpty(options.Author))
        {
            sb.AppendLine($"authors = [\"{options.Author}\"]");
        }

        if (!string.IsNullOrEmpty(options.License))
        {
            sb.AppendLine($"license = \"{options.License}\"");
        }

        sb.AppendLine();
        sb.AppendLine("[build]");
        sb.AppendLine("target_cpu = \"68020\"");
        sb.AppendLine("fpu = \"auto\"");
        sb.AppendLine("output = \"build\"");
        sb.AppendLine("optimization_level = 0");
        sb.AppendLine();
        sb.AppendLine("[paths]");
        sb.AppendLine("src = \"src\"");

        return sb.ToString();
    }

    private static (string FileName, string Source) GenerateSourceFile(string projectName, NewCommandOptions options)
    {
        return (options.ProjectType ?? "cli").ToLower() switch
        {
            "cli" => ("main.novus", LoadBundledSource("cli", "cli", "main.novus", projectName)),
            "workbench" => ("main.novus", LoadBundledSource("gui", "traditional", "main.novus", projectName)),
            "dual" => ("main.novus", LoadBundledSource("cli", "cli", "main.novus", projectName)),
            "library" => ("lib.novus", LoadBundledSource("library", "library", "lib.novus", projectName)),
            "device" => ("dev.novus", LoadBundledSource("device", "device", "dev.novus", projectName)),
            "resource" => ("resource.novus", LoadBundledSource("resource", "resource", "resource.novus", projectName)),
            "handler" => ("main.novus", LoadBundledSource("handler", "handler", "main.novus", projectName)),
            _ => ("main.novus", LoadBundledSource("cli", "cli", "main.novus", projectName))
        };
    }

    private static string LoadBundledSource(string template, string project, string fileName, string projectName)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", template, project, "src", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Bundled project template not found: {path}");

        return File.ReadAllText(path).Replace("{{PROJECT_NAME}}", projectName);
    }

    private static string GenerateWorkspaceToml(string workspaceName, NewCommandOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[workspace]");
        sb.AppendLine($"name = \"{workspaceName}\"");
        sb.AppendLine("version = \"0.1.0\"");

        if (!string.IsNullOrEmpty(options.Description))
        {
            sb.AppendLine($"description = \"{options.Description}\"");
        }

        if (!string.IsNullOrEmpty(options.Author))
        {
            sb.AppendLine($"authors = [\"{options.Author}\"]");
        }

        sb.AppendLine("members = []  # Projects will be added here");
        sb.AppendLine();
        sb.AppendLine("[workspace.build]");
        sb.AppendLine("target_cpu = \"68020\"");
        sb.AppendLine("fpu = \"auto\"");
        sb.AppendLine("optimization_level = 0");

        return sb.ToString();
    }

    private static void UpdateWorkspaceMembers(string workspaceDir, string projectName)
    {
        var workspaceFile = Path.Combine(workspaceDir, "workspace.toml");
        var content = File.ReadAllText(workspaceFile);

        // Simple regex to update members array
        // This is a basic implementation - could use TOML parser for robustness
        if (content.Contains("members = []"))
        {
            content = content.Replace("members = []", $"members = [\"{projectName}\"]");
        }
        else if (content.Contains("members = ["))
        {
            // Find the closing bracket and insert before it
            var membersStart = content.IndexOf("members = [");
            var closingBracket = content.IndexOf(']', membersStart);
            var beforeBracket = content.Substring(0, closingBracket).TrimEnd();

            // Check if there are existing members
            if (beforeBracket.EndsWith("["))
            {
                content = content.Insert(closingBracket, $"\"{projectName}\"");
            }
            else
            {
                content = content.Insert(closingBracket, $", \"{projectName}\"");
            }
        }

        File.WriteAllText(workspaceFile, content);
        Console.WriteLine($"  ✓ Updated workspace.toml (added '{projectName}' to members)");
    }

    private static string GenerateWorkspaceReadme(string workspaceName, NewCommandOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {workspaceName}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(options.Description))
        {
            sb.AppendLine(options.Description);
            sb.AppendLine();
        }

        sb.AppendLine("## Novus Workspace");
        sb.AppendLine();
        sb.AppendLine("This is a Novus workspace containing multiple projects.");
        sb.AppendLine();
        sb.AppendLine("### Adding Projects");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Add a CLI application");
        sb.AppendLine("novusc new my-tool --type cli");
        sb.AppendLine();
        sb.AppendLine("# Add a Workbench GUI application");
        sb.AppendLine("novusc new my-gui --type workbench");
        sb.AppendLine();
        sb.AppendLine("# Add a shared library");
        sb.AppendLine("novusc new mylib --type library");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Building");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Build all projects");
        sb.AppendLine("novusc build");
        sb.AppendLine();
        sb.AppendLine("# Build specific project");
        sb.AppendLine("novusc build my-tool");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Projects");
        sb.AppendLine();
        sb.AppendLine("(Projects will be listed here as you add them)");
        sb.AppendLine();
        sb.AppendLine("## License");
        sb.AppendLine();
        sb.AppendLine(options.License ?? "All rights reserved");

        return sb.ToString();
    }

    private static string GenerateGitignore()
    {
        return @"# Novus build outputs
build/
*.o
*.s
*.c
*.h
*.lnk

# VBCC outputs
*.asm

# AmigaOS binaries
*.exe
*.library
*.device

# Temporary files
*.tmp
*.bak
*~

# IDE files
.vs/
.vscode/
.idea/
*.suo
*.user

# OS files
.DS_Store
Thumbs.db
";
    }

    private static string GenerateReadme(string projectName, NewCommandOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {projectName}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(options.Description))
        {
            sb.AppendLine(options.Description);
            sb.AppendLine();
        }

        sb.AppendLine("## Building");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("novusc build");
        sb.AppendLine("```");
        sb.AppendLine();

        if (options.ProjectType == "workbench" || options.ProjectType == "dual")
        {
            sb.AppendLine("## Running from Workbench");
            sb.AppendLine();
            sb.AppendLine("After building, copy the executable to your Amiga and double-click the icon.");
            sb.AppendLine();
        }

        sb.AppendLine("## License");
        sb.AppendLine();
        sb.AppendLine(options.License ?? "All rights reserved");

        return sb.ToString();
    }
}
