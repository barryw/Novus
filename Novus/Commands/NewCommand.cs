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
        var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", options.ProjectType ?? "cli");

        if (!Directory.Exists(templateDir))
        {
            Console.WriteLine($"Error: Template '{options.ProjectType}' not found");
            Console.WriteLine($"Available templates:");
            var templatesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
            if (Directory.Exists(templatesRoot))
            {
                foreach (var dir in Directory.GetDirectories(templatesRoot))
                {
                    Console.WriteLine($"  - {Path.GetFileName(dir)}");
                }
            }
            return 1;
        }

        // Copy entire template directory
        CopyDirectory(templateDir, workspaceDir, true);
        Console.WriteLine($"  ✓ Created workspace from '{options.ProjectType}' template");

        // Replace placeholder names in files
        ReplaceTemplatePlaceholders(workspaceDir, workspaceName);

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

    private static void CopyDirectory(string sourceDir, string destDir, bool recursive)
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
            string targetFilePath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        // Copy subdirectories
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
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
        var validTypes = new[] { "cli", "workbench", "dual", "library", "device" };
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
        return (options.ProjectType ?? "cli").ToLower() switch
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
        // Convert project name to a valid identifier (capitalize first letter, remove special chars)
        var structName = char.ToUpper(projectName[0]) + projectName.Substring(1).Replace("-", "").Replace("_", "");

        return $@"// {projectName}.device - AmigaOS device driver
//
// Device drivers handle I/O requests through the exec device interface.
// Unlike libraries (which expose functions), devices process commands
// via BeginIO/AbortIO using IORequest structures.
//
// Build with: novusc build (in workspace) or novusc compile --project-type device

from std::ffi::amiga_structs import IORequest, Unit
from std::ffi::amiga_consts import IOERR_NOCMD

// ============================================================================
// Device Base Structure
// ============================================================================
// The @device attribute marks this struct as the device base.
// The compiler will generate:
//   - ROMTag for exec.library scanning
//   - DevInit/DevOpen/DevClose/DevExpunge lifecycle functions
//   - BeginIO command dispatcher
//   - AbortIO handler
//   - A6 calling convention wrappers

@device(name = ""{projectName}.device"", units = 4)
pub struct {structName} {{
    // Add custom device state here
    // These fields are available in command handlers via the base pointer
    initialized: bool,
    custom_data: u32,
}}

// ============================================================================
// Device Commands
// ============================================================================
// Standard Exec commands (CMD_*) are:
//   0 = CMD_INVALID
//   1 = CMD_RESET
//   2 = CMD_READ
//   3 = CMD_WRITE
//   4 = CMD_UPDATE
//   5 = CMD_CLEAR
//   6 = CMD_STOP
//   7 = CMD_START
//   8 = CMD_FLUSH
//   9+ = Device-specific commands (CMD_NONSTD and above)

// Custom command constant
pub const CMD_{structName.ToUpper()}_HELLO: u16 = 9

// ============================================================================
// Command Handlers
// ============================================================================
// Mark functions with @devicecmd to register them as command handlers.
// The dispatcher calls these based on ioReq->io_Command.

impl {structName} {{
    // CMD_RESET handler - reset device to initial state
    @devicecmd(cmd = ""CMD_RESET"")
    pub fn cmd_reset(ioReq: *IORequest, base: *{structName}) -> i8 {{
        // Reset device state
        unsafe {{
            (*base).initialized = false
            (*base).custom_data = 0
        }}
        return 0  // Success
    }}

    // Custom command handler
    @devicecmd(cmd = 9, quick = true)  // quick = can complete without blocking
    pub fn cmd_hello(ioReq: *IORequest, base: *{structName}) -> i8 {{
        // Example: just increment a counter
        unsafe {{
            (*base).custom_data = (*base).custom_data + 1
        }}
        return 0  // Success
    }}

    // CMD_READ handler - read data from device
    @devicecmd(cmd = ""CMD_READ"")
    pub fn cmd_read(ioReq: *IORequest, base: *{structName}) -> i8 {{
        // Access IORequest fields:
        //   ioReq->io_Data   - buffer pointer
        //   ioReq->io_Length - requested length
        //   ioReq->io_Actual - set to actual bytes transferred

        // TODO: Implement read logic
        // For now, return ""not implemented""
        return (i8)IOERR_NOCMD
    }}

    // CMD_WRITE handler - write data to device
    @devicecmd(cmd = ""CMD_WRITE"")
    pub fn cmd_write(ioReq: *IORequest, base: *{structName}) -> i8 {{
        // Access IORequest fields:
        //   ioReq->io_Data   - buffer pointer
        //   ioReq->io_Length - data length
        //   ioReq->io_Actual - set to actual bytes transferred

        // TODO: Implement write logic
        return (i8)IOERR_NOCMD
    }}
}}

// ============================================================================
// Usage Example (from another program)
// ============================================================================
// To use this device from a Novus program:
//
//   from std::ffi::exec import OpenDevice, CloseDevice, DoIO
//   from std::ffi::exec import CreateMsgPort, DeleteMsgPort
//   from std::ffi::exec import CreateIORequest, DeleteIORequest
//
//   pub fn main() -> i32 {{
//       let port = CreateMsgPort()
//       let req = CreateIORequest(port, @sizeof(IORequest))
//
//       // Open device unit 0
//       let error = OpenDevice(""{projectName}.device"", 0, req, 0)
//       if error != 0 {{
//           println(""Failed to open device"")
//           return 1
//       }}
//
//       // Send custom command
//       req.io_Command = CMD_{structName.ToUpper()}_HELLO
//       DoIO(req)
//
//       // Clean up
//       CloseDevice(req)
//       DeleteIORequest(req)
//       DeleteMsgPort(port)
//       return 0
//   }}
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
