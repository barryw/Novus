using Novus.Project;
using Tomlyn;

namespace Novus.Commands;

/// <summary>
/// Handles building projects and workspaces.
/// Smart detection: builds workspace if in workspace dir, project if in project dir.
/// </summary>
public static class BuildCommand
{
    public static async Task<int> Run(BuildOptions buildOptions)
    {
        // First, check if we're in a workspace by looking at the ACTUAL current directory
        // (not the --project option, which might be a project name, not a path)
        var actualCurrentDir = Directory.GetCurrentDirectory();
        var workspaceFile = Path.Combine(actualCurrentDir, "solution.toml");
        bool inWorkspace = File.Exists(workspaceFile);

        // Now determine the target directory
        var currentDir = buildOptions.ProjectPath ?? actualCurrentDir;
        if (File.Exists(currentDir) && currentDir.EndsWith(".toml"))
        {
            // Project path is a toml file directly
            currentDir = Path.GetDirectoryName(currentDir) ?? actualCurrentDir;
        }

        // If --project was specified and we're in a workspace, treat it as a project name
        if (inWorkspace && !string.IsNullOrEmpty(buildOptions.ProjectPath) &&
            !Path.IsPathRooted(buildOptions.ProjectPath) &&
            !buildOptions.ProjectPath.EndsWith(".toml"))
        {
            // User specified a project name - build that project in the workspace
            return await BuildWorkspace(actualCurrentDir, buildOptions);
        }

        currentDir = Path.GetFullPath(currentDir);

        // Check for solution.toml (workspace file) and project.toml (project file)
        var targetWorkspaceFile = Path.Combine(currentDir, "solution.toml");
        var projectFile = Path.Combine(currentDir, "project.toml");

        bool hasWorkspace = File.Exists(targetWorkspaceFile);
        bool hasProject = File.Exists(projectFile);

        // Decision logic based on context
        if (hasWorkspace && hasProject)
        {
            // Both exist - this is weird, but treat as workspace
            Console.WriteLine("Warning: Both solution.toml and project.toml found. Treating as workspace.");
            return await BuildWorkspace(currentDir, buildOptions);
        }
        else if (hasWorkspace)
        {
            // We're in a workspace directory
            return await BuildWorkspace(currentDir, buildOptions);
        }
        else if (hasProject)
        {
            // We're in a project directory
            return await BuildProject(currentDir, buildOptions);
        }
        else
        {
            Console.WriteLine($"Error: No solution.toml or project.toml found in {currentDir}");
            Console.WriteLine("Run 'novusc new <name>' to create a new project or workspace");
            return 1;
        }
    }

    /// <summary>
    /// Build an entire workspace or a specific project within it
    /// </summary>
    private static async Task<int> BuildWorkspace(string workspaceDir, BuildOptions buildOptions)
    {
        var workspaceFile = Path.Combine(workspaceDir, "solution.toml");

        Console.WriteLine($"Loading workspace: {workspaceFile}\n");

        // Load workspace
        NovusWorkspace workspace;
        try
        {
            var tomlContent = await File.ReadAllTextAsync(workspaceFile);
            workspace = Toml.ToModel<NovusWorkspace>(tomlContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading workspace file: {ex.Message}");
            return 1;
        }

        if (workspace.Workspace.Members.Length == 0)
        {
            Console.WriteLine("Warning: Workspace has no member projects");
            Console.WriteLine("Use 'novusc new <name> --type <type>' to add projects to this workspace");
            return 0;
        }

        Console.WriteLine($"Workspace: {workspace.Workspace.Name} v{workspace.Workspace.Version}");
        if (!string.IsNullOrEmpty(workspace.Workspace.Description))
        {
            Console.WriteLine($"Description: {workspace.Workspace.Description}");
        }
        Console.WriteLine($"Projects: {string.Join(", ", workspace.Workspace.Members)}");
        Console.WriteLine();

        // Check if a specific project was requested via the --project option
        if (!string.IsNullOrEmpty(buildOptions.ProjectPath) &&
            !Path.IsPathRooted(buildOptions.ProjectPath) &&
            !buildOptions.ProjectPath.EndsWith(".toml"))
        {
            // User specified a project name (not a path)
            var requestedProject = buildOptions.ProjectPath;
            if (!workspace.Workspace.Members.Contains(requestedProject))
            {
                Console.WriteLine($"Error: Project '{requestedProject}' not found in workspace");
                Console.WriteLine($"Available projects: {string.Join(", ", workspace.Workspace.Members)}");
                return 1;
            }

            // Build just this one project
            var projectDir = Path.Combine(workspaceDir, requestedProject);
            return await BuildProject(projectDir, buildOptions, workspace);
        }

        // Build all projects in the workspace
        int failedCount = 0;
        int successCount = 0;

        for (int i = 0; i < workspace.Workspace.Members.Length; i++)
        {
            var projectName = workspace.Workspace.Members[i];
            var projectDir = Path.Combine(workspaceDir, projectName);

            Console.WriteLine($"[{i + 1}/{workspace.Workspace.Members.Length}] Building {projectName}...");
            Console.WriteLine(new string('─', 60));

            if (!Directory.Exists(projectDir))
            {
                Console.WriteLine($"  ✗ Project directory not found: {projectDir}\n");
                failedCount++;
                continue;
            }

            var result = await BuildProject(projectDir, buildOptions, workspace);
            if (result == 0)
            {
                Console.WriteLine($"  ✓ {projectName} built successfully\n");
                successCount++;
            }
            else
            {
                Console.WriteLine($"  ✗ {projectName} build failed\n");
                failedCount++;
            }
        }

        // Summary
        Console.WriteLine(new string('═', 60));
        Console.WriteLine($"Workspace build complete: {successCount} succeeded, {failedCount} failed");
        Console.WriteLine(new string('═', 60));

        return failedCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Build a single project
    /// </summary>
    private static async Task<int> BuildProject(
        string projectDir,
        BuildOptions buildOptions,
        NovusWorkspace? workspace = null)
    {
        var projectFile = Path.Combine(projectDir, "project.toml");
        if (!File.Exists(projectFile))
        {
            Console.WriteLine($"Error: No project.toml found in {projectDir}");
            return 1;
        }

        if (workspace == null)
        {
            // Not part of a workspace, show standalone project info
            Console.WriteLine($"Building project: {projectFile}\n");
        }

        NovusProject project;
        try
        {
            project = ProjectLoader.LoadFromFile(projectFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading project file: {ex.Message}");
            return 1;
        }

        // Validate project
        if (string.IsNullOrEmpty(project.Package.Name))
        {
            Console.WriteLine("Error: [package] section must specify 'name'");
            return 1;
        }

        // Determine entry point based on project type
        var projectType = project.Package.Type ?? "cli";
        var entryFile = project.Package.Entry;
        if (string.IsNullOrEmpty(entryFile))
        {
            // Auto-detect based on project type
            switch (projectType.ToLowerInvariant())
            {
                case "library":
                    // Libraries typically have lib.novus
                    var libPath = Path.Combine(projectDir, project.Paths.Src, "lib.novus");
                    if (File.Exists(libPath))
                    {
                        entryFile = Path.Combine(project.Paths.Src, "lib.novus");
                    }
                    break;

                case "device":
                    // Devices typically have device.novus
                    var devicePath = Path.Combine(projectDir, project.Paths.Src, "device.novus");
                    if (File.Exists(devicePath))
                    {
                        entryFile = Path.Combine(project.Paths.Src, "device.novus");
                    }
                    break;

                default:
                    // CLI, Workbench, Dual - all use main.novus
                    var mainPath = Path.Combine(projectDir, project.Paths.Src, "main.novus");
                    if (File.Exists(mainPath))
                    {
                        entryFile = Path.Combine(project.Paths.Src, "main.novus");
                    }
                    break;
            }

            // Fallback to package name
            if (string.IsNullOrEmpty(entryFile))
            {
                var packagePath = Path.Combine(projectDir, project.Paths.Src, $"{project.Package.Name}.novus");
                if (File.Exists(packagePath))
                {
                    entryFile = Path.Combine(project.Paths.Src, $"{project.Package.Name}.novus");
                }
            }

            if (string.IsNullOrEmpty(entryFile))
            {
                Console.WriteLine("Error: No entry point found");
                Console.WriteLine($"  Expected one of:");
                Console.WriteLine($"    - {project.Paths.Src}/main.novus");
                Console.WriteLine($"    - {project.Paths.Src}/lib.novus (for libraries)");
                Console.WriteLine($"    - {project.Paths.Src}/device.novus (for devices)");
                Console.WriteLine($"    - {project.Paths.Src}/{project.Package.Name}.novus");
                Console.WriteLine("  Or specify 'entry' in [package] section");
                return 1;
            }
        }

        var inputFile = Path.Combine(projectDir, entryFile);
        if (!File.Exists(inputFile))
        {
            Console.WriteLine($"Error: Entry file not found: {inputFile}");
            return 1;
        }

        // Create output directory
        var outputDir = Path.Combine(projectDir, project.Build.Output);
        Directory.CreateDirectory(outputDir);

        // Determine build mode (Debug or Release)
        // Default to Debug unless --release is specified
        var buildMode = buildOptions.Release ? BuildMode.Release :
                       buildOptions.Debug ? BuildMode.Debug :
                       BuildMode.Debug;  // Default to debug

        // Merge workspace build settings with project settings
        // Project settings override workspace settings
        var targetCpu = buildOptions.Cpu
            ?? project.Build.TargetCpu
            ?? workspace?.Workspace.Build?.TargetCpu
            ?? "68020";

        var fpu = buildOptions.Fpu
            ?? project.Build.Fpu
            ?? workspace?.Workspace.Build?.Fpu
            ?? "auto";

        // Optimization level: explicit > release default > project > workspace > 0
        int optimizationLevel;
        if (buildOptions.OptimizationLevel.HasValue)
        {
            optimizationLevel = buildOptions.OptimizationLevel.Value;
        }
        else if (buildMode == BuildMode.Release)
        {
            optimizationLevel = 2;  // Release default
        }
        else
        {
            optimizationLevel = project.Build.OptimizationLevel;
        }

        // Use workspace optimization level as fallback
        if (buildMode == BuildMode.Debug && workspace?.Workspace.Build != null)
        {
            optimizationLevel = project.Build.OptimizationLevel != 0
                ? project.Build.OptimizationLevel
                : workspace.Workspace.Build.OptimizationLevel;
        }

        // Convert to CompilerOptions
        var compilerOptions = new CompilerOptions
        {
            InputFile = inputFile,
            OutputFile = Path.Combine(outputDir, project.Package.Name),
            Cpu = targetCpu,
            Fpu = fpu,
            OptimizationLevel = optimizationLevel,
            BuildMode = buildMode,
            EmitAsmOnly = buildOptions.EmitAsmOnly || project.Build.EmitAsm,
            VbccPath = buildOptions.VbccPath ?? "/Users/barry/amiga-cc/vbcc",
            NdkPath = buildOptions.NdkPath ?? "/Users/barry/amiga-cc/NDK3.9",
            Verbose = buildOptions.Verbose,
            ProjectType = projectType
        };

        if (workspace == null)
        {
            // Only show detailed info when building standalone project
            Console.WriteLine($"Package: {project.Package.Name} v{project.Package.Version}");
            if (!string.IsNullOrEmpty(project.Package.Type))
            {
                Console.WriteLine($"Type: {project.Package.Type}");
            }
            if (!string.IsNullOrEmpty(project.Package.Description))
            {
                Console.WriteLine($"Description: {project.Package.Description}");
            }
            Console.WriteLine($"Entry: {entryFile}");
            Console.WriteLine($"Output: {project.Build.Output}/{project.Package.Name}");
            Console.WriteLine();
        }
        else
        {
            // In workspace context, show minimal info
            Console.WriteLine($"  Package: {project.Package.Name} v{project.Package.Version} ({project.Package.Type ?? "cli"})");
            Console.WriteLine($"  Entry: {entryFile}");
        }

        // Run the compiler (delegate to Program.RunCompiler)
        // We need to make RunCompiler public for this
        return await Program.RunCompiler(compilerOptions);
    }
}
