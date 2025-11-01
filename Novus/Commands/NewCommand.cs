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
            // Check if we're inside a solution (solution.toml exists in current directory)
            var currentDir = Directory.GetCurrentDirectory();
            var workspaceFile = Path.Combine(currentDir, "solution.toml");
            bool insideWorkspace = File.Exists(workspaceFile);

            if (insideWorkspace)
            {
                // We're inside a solution - create a new project within it
                return CreateProjectInWorkspace(currentDir, options);
            }
            else
            {
                // We're not in a solution - create a new solution
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

        Console.WriteLine($"Creating new solution: {workspaceName}");
        Console.WriteLine();

        // Create workspace structure
        Directory.CreateDirectory(workspaceDir);
        Console.WriteLine($"  ✓ Created solution directory: {workspaceName}/");

        // Create solution.toml (workspace file)
        var workspaceToml = GenerateWorkspaceToml(workspaceName, options);
        File.WriteAllText(Path.Combine(workspaceDir, "solution.toml"), workspaceToml);
        Console.WriteLine("  ✓ Created solution.toml (solution file)");

        // Create .gitignore
        var gitignorePath = Path.Combine(workspaceDir, ".gitignore");
        File.WriteAllText(gitignorePath, GenerateGitignore());
        Console.WriteLine("  ✓ Created .gitignore");

        // Create README
        var readmePath = Path.Combine(workspaceDir, "README.md");
        File.WriteAllText(readmePath, GenerateWorkspaceReadme(workspaceName, options));
        Console.WriteLine("  ✓ Created README.md");

        Console.WriteLine();
        Console.WriteLine("Your solution is ready!");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        if (options.ProjectName != ".")
        {
            Console.WriteLine($"  cd {workspaceName}");
        }
        Console.WriteLine("  novusc new my-app --type cli       # Add a CLI project");
        Console.WriteLine("  novusc new my-gui --type workbench # Add a Workbench project");
        Console.WriteLine();
        Console.WriteLine("Happy coding! 🚀");

        return 0;
    }

    private static int CreateProjectInWorkspace(string workspaceDir, NewCommandOptions options)
    {
        // Validate project type
        var validTypes = new[] { "cli", "workbench", "dual", "library", "device" };
        if (!validTypes.Contains(options.ProjectType.ToLower()))
        {
            Console.WriteLine($"Error: Invalid project type '{options.ProjectType}'");
            Console.WriteLine($"Valid types: {string.Join(", ", validTypes)}");
            return 1;
        }

        var projectName = options.ProjectName;
        var projectDir = Path.Combine(workspaceDir, projectName);

        // Check if project directory already exists
        if (Directory.Exists(projectDir))
        {
            Console.WriteLine($"Error: Project '{projectName}' already exists in this solution");
            return 1;
        }

        Console.WriteLine($"Adding new {options.ProjectType} project to solution: {projectName}");
        Console.WriteLine();

        CreateProjectStructure(projectDir, projectName, options);

        // Update solution.toml to add this project to members
        UpdateWorkspaceMembers(workspaceDir, projectName);

        Console.WriteLine();
        Console.WriteLine("Project added to solution!");
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

        // Create main source file
        var mainPath = Path.Combine(srcDir, "main.novus");
        File.WriteAllText(mainPath, GenerateMainFile(projectName, options));
        Console.WriteLine("  ✓ Created src/main.novus");

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

    private static string GenerateMainFile(string projectName, NewCommandOptions options)
    {
        return options.ProjectType.ToLower() switch
        {
            "cli" => GenerateCliTemplate(projectName),
            "workbench" => GenerateWorkbenchTemplate(projectName),
            "dual" => GenerateDualTemplate(projectName),
            "library" => GenerateLibraryTemplate(projectName),
            "device" => GenerateDeviceTemplate(projectName),
            _ => GenerateCliTemplate(projectName)
        };
    }

    private static string GenerateCliTemplate(string projectName)
    {
        return $@"// {projectName} - A command-line application for AmigaOS
//
// This template uses the VBCC C runtime which provides argc/argv
// just like standard C programs.

from std::io import println

pub fn main() -> i32 {{
    println(""Hello from {projectName}!"")

    // TODO: Parse command-line arguments
    // For simple args, use the C runtime's argc/argv (provided by VBCC)
    //
    // For AmigaOS-style argument parsing, use ReadArgs:
    // from std::ffi::dos import ReadArgs, FreeArgs
    // See: /tmp/AMIGA_CLI_ARGS_GUIDE.md for details

    return 0
}}
";
    }

    private static string GenerateWorkbenchTemplate(string projectName)
    {
        return $@"// {projectName} - A Workbench GUI application for AmigaOS
//
// This template handles WBStartup messages for Workbench launches.

from std::ffi::dos import Input, Output, Write

pub fn main() -> i32 {{
    // Check if launched from Workbench or CLI
    let input_fh = Input()

    if input_fh == 0 {{
        // Launched from Workbench
        return handle_workbench()
    }} else {{
        // Launched from CLI (fallback)
        return handle_cli()
    }}
}}

fn handle_workbench() -> i32 {{
    // TODO: Get WBStartup message from process message port
    // TODO: Process files from sm_ArgList
    // TODO: Do your Workbench app logic
    //
    // Example:
    // from std::ffi::amiga_structs import WBStartup, WBArg
    // from std::ffi::exec import ReplyMsg, Forbid
    //
    // let wbmsg: *WBStartup = get_workbench_msg()
    // for i in 0..wbmsg.sm_NumArgs {{
    //     let arg: *WBArg = &wbmsg.sm_ArgList[i]
    //     // Process arg.wa_Name file
    // }}
    //
    // IMPORTANT: Must reply to WBStartup message when done!
    // Forbid()
    // ReplyMsg(wbmsg as *Message)

    return 0
}}

fn handle_cli() -> i32 {{
    let stdout = Output()
    let msg: String = ""{projectName}: This is a Workbench application.\\n""
    Write(stdout, (i32)(msg.ptr), msg.len)
    return 5  // RETURN_WARN
}}
";
    }

    private static string GenerateDualTemplate(string projectName)
    {
        return $@"// {projectName} - Dual-mode application (CLI + Workbench)
//
// This application works from both the CLI and Workbench.

from std::ffi::dos import Input, Output, Write

pub fn main() -> i32 {{
    let input_fh = Input()

    if input_fh == 0 {{
        // Workbench launch
        return run_workbench()
    }} else {{
        // CLI launch
        return run_cli()
    }}
}}

fn run_cli() -> i32 {{
    // TODO: Parse command-line arguments using ReadArgs
    // from std::ffi::dos import ReadArgs, FreeArgs
    //
    // let template: String = ""FILES/M,VERBOSE/S""
    // var results: [2]i32 = {{0, 0}}
    // let args = ReadArgs(template.ptr, &results[0] as *u8, 0 as *RDArgs)

    let stdout = Output()
    let msg: String = ""Hello from {projectName} CLI mode!\\n""
    Write(stdout, (i32)(msg.ptr), msg.len)

    return 0
}}

fn run_workbench() -> i32 {{
    // TODO: Handle WBStartup message
    // See workbench template for details

    let stdout = Output()
    let msg: String = ""Hello from {projectName} Workbench mode!\\n""
    Write(stdout, (i32)(msg.ptr), msg.len)

    return 0
}}
";
    }

    private static string GenerateLibraryTemplate(string projectName)
    {
        return $@"// {projectName}.library - AmigaOS shared library
//
// This template provides the basic structure for an AmigaOS library.

// Library version
pub const VERSION: i32 = 1
pub const REVISION: i32 = 0

// Library initialization
// Called when library is first loaded
pub fn lib_init() -> i32 {{
    // TODO: Initialize library resources
    return 0
}}

// Library cleanup
// Called when library is expunged
pub fn lib_expunge() -> i32 {{
    // TODO: Clean up library resources
    return 0
}}

// Library open
// Called when a program opens the library
pub fn lib_open() -> i32 {{
    // TODO: Increment open count
    return 0
}}

// Library close
// Called when a program closes the library
pub fn lib_close() -> i32 {{
    // TODO: Decrement open count
    return 0
}}

// Example library function
pub fn hello() -> String {{
    return ""{projectName} library says hello!""
}}
";
    }

    private static string GenerateDeviceTemplate(string projectName)
    {
        return $@"// {projectName}.device - AmigaOS device driver
//
// Device drivers handle I/O requests through the exec device interface.

pub const VERSION: i32 = 1
pub const REVISION: i32 = 0

// Device initialization
pub fn dev_init() -> i32 {{
    // TODO: Initialize device hardware
    return 0
}}

// Device open
pub fn dev_open() -> i32 {{
    // TODO: Open device unit
    return 0
}}

// Device close
pub fn dev_close() -> i32 {{
    // TODO: Close device unit
    return 0
}}

// Begin I/O request
pub fn dev_begin_io(io_request: *i32) -> i32 {{
    // TODO: Process I/O command
    return 0
}}

// Abort I/O request
pub fn dev_abort_io(io_request: *i32) -> i32 {{
    // TODO: Abort pending I/O
    return 0
}}
";
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
        var workspaceFile = Path.Combine(workspaceDir, "solution.toml");
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
        Console.WriteLine($"  ✓ Updated solution.toml (added '{projectName}' to members)");
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

        sb.AppendLine("## Novus Solution");
        sb.AppendLine();
        sb.AppendLine("This is a Novus solution containing multiple projects.");
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
