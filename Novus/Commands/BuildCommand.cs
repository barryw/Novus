using Novus.Project;
using Tomlyn;

namespace Novus.Commands;

/// <summary>
/// Handles building projects and workspaces.
/// Smart detection: builds workspace if in workspace dir, project if in project dir.
/// </summary>
public static class BuildCommand
{
    /// <summary>
    /// Valid CPU values that VBCC/VASM accept
    /// </summary>
    private static readonly string[] ValidCpuValues = { "68000", "68010", "68020", "68030", "68040", "68060", "68080", "auto" };

    /// <summary>
    /// Valid FPU values
    /// </summary>
    private static readonly string[] ValidFpuValues = { "auto", "soft", "68881", "68040" };

    /// <summary>
    /// Validate CPU value and return error message if invalid
    /// </summary>
    private static string? ValidateCpu(string? cpu)
    {
        if (cpu == null) return null;
        if (!ValidCpuValues.Contains(cpu))
        {
            return $"Invalid CPU value '{cpu}'. Valid values are: {string.Join(", ", ValidCpuValues)}";
        }
        return null;
    }

    /// <summary>
    /// Validate FPU value and return error message if invalid
    /// </summary>
    private static string? ValidateFpu(string? fpu)
    {
        if (fpu == null) return null;
        if (!ValidFpuValues.Contains(fpu))
        {
            return $"Invalid FPU value '{fpu}'. Valid values are: {string.Join(", ", ValidFpuValues)}";
        }
        return null;
    }

    public static async Task<int> Run(BuildOptions buildOptions)
    {
        // First, check if we're in a workspace by looking at the ACTUAL current directory
        // (not the --project option, which might be a project name, not a path)
        var actualCurrentDir = Directory.GetCurrentDirectory();
        var workspaceFile = Path.Combine(actualCurrentDir, "workspace.toml");
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

        // Check for workspace.toml (workspace file) and project.toml (project file)
        var targetWorkspaceFile = Path.Combine(currentDir, "workspace.toml");
        var projectFile = Path.Combine(currentDir, "project.toml");

        bool hasWorkspace = File.Exists(targetWorkspaceFile);
        bool hasProject = File.Exists(projectFile);

        // Decision logic based on context
        if (hasWorkspace && hasProject)
        {
            // Both exist - this is weird, but treat as workspace
            Console.WriteLine("Warning: Both workspace.toml and project.toml found. Treating as workspace.");
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
            Console.WriteLine($"Error: No workspace.toml or project.toml found in {currentDir}");
            Console.WriteLine("Run 'novusc new <name>' to create a new project or workspace");
            return 1;
        }
    }

    /// <summary>
    /// Build an entire workspace or a specific project within it
    /// </summary>
    private static async Task<int> BuildWorkspace(string workspaceDir, BuildOptions buildOptions)
    {
        var workspaceFile = Path.Combine(workspaceDir, "workspace.toml");

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

        if (workspace.Workspace.Members is [])
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

        // Load all projects to build dependency graph
        var projects = new Dictionary<string, NovusProject>();
        foreach (var projectName in workspace.Workspace.Members)
        {
            var projectDir = Path.Combine(workspaceDir, projectName);
            var projectFile = Path.Combine(projectDir, "project.toml");

            if (!File.Exists(projectFile))
            {
                Console.WriteLine($"Warning: Project file not found: {projectFile}");
                continue;
            }

            try
            {
                var project = ProjectLoader.LoadFromFile(projectFile);
                projects[projectName] = project;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load project {projectName}: {ex.Message}");
            }
        }

        // Create build context for tracking outputs
        var buildContext = new BuildContext(workspaceDir, projects);

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

            // Build just this one project (with dependencies)
            var projectDir = Path.Combine(workspaceDir, requestedProject);
            return await BuildProject(projectDir, buildOptions, workspace, buildContext, workspaceDir);
        }

        // Compute build order based on dependencies
        var buildOrder = ComputeBuildOrder(projects);
        if (buildOrder == null)
        {
            Console.WriteLine("Error: Circular dependency detected in workspace projects");
            return 1;
        }

        Console.WriteLine($"Build order: {string.Join(" → ", buildOrder)}\n");

        // Build all projects in dependency order
        int failedCount = 0;
        int successCount = 0;

        for (int i = 0; i < buildOrder.Count; i++)
        {
            var projectName = buildOrder[i];
            var projectDir = Path.Combine(workspaceDir, projectName);

            Console.WriteLine($"[{i + 1}/{buildOrder.Count}] Building {projectName}...");
            Console.WriteLine(new string('─', 60));

            if (!Directory.Exists(projectDir))
            {
                Console.WriteLine($"  ✗ Project directory not found: {projectDir}\n");
                failedCount++;
                continue;
            }

            var result = await BuildProject(projectDir, buildOptions, workspace, buildContext, workspaceDir);
            if (result == 0)
            {
                Console.WriteLine($"  ✓ {projectName} built successfully\n");
                successCount++;

                // Record the output path for this project
                // For workspace builds, output goes to centralized target directory
                var project = projects[projectName];
                var buildModeStr = buildOptions.Release ? "release" : "debug";
                var typeSubdir = (project.Package.Type ?? "cli").ToLowerInvariant() switch
                {
                    "library" => "libs",
                    "device" => "libs",
                    "handler" => "libs",
                    "test" => "tests",
                    _ => "bins"
                };
                var outputDir = Path.Combine(workspaceDir, "target", buildModeStr, typeSubdir);
                buildContext.RecordBuildOutput(projectName, outputDir);
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
    /// Compute build order using topological sort
    /// Returns null if circular dependency detected
    /// </summary>
    private static List<string>? ComputeBuildOrder(Dictionary<string, NovusProject> projects)
    {
        var graph = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();

        // Initialize graph
        foreach (var (projectName, project) in projects)
        {
            graph[projectName] = new List<string>();
            inDegree[projectName] = 0;
        }

        // Build dependency graph
        foreach (var (projectName, project) in projects)
        {
            foreach (var (depName, depInfo) in project.Dependencies)
            {
                // Only consider workspace-internal dependencies (path-based)
                if (depInfo.Path != null && projects.ContainsKey(depName))
                {
                    // projectName depends on depName, so depName must be built first
                    graph[depName].Add(projectName);
                    inDegree[projectName]++;
                }
            }
        }

        // Topological sort using Kahn's algorithm
        var queue = new Queue<string>();
        var result = new List<string>();

        // Start with projects that have no dependencies
        foreach (var (projectName, degree) in inDegree)
        {
            if (degree == 0)
            {
                queue.Enqueue(projectName);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dependent in graph[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        // Check if all projects were processed (no cycles)
        if (result.Count != projects.Count)
        {
            return null;  // Circular dependency
        }

        return result;
    }

    /// <summary>
    /// Context for building projects in a workspace
    /// Tracks build outputs for linking dependencies
    /// </summary>
    private class BuildContext
    {
        private readonly string _workspaceDir;
        private readonly Dictionary<string, NovusProject> _projects;
        private readonly Dictionary<string, string> _buildOutputs = new();

        public BuildContext(string workspaceDir, Dictionary<string, NovusProject> projects)
        {
            _workspaceDir = workspaceDir;
            _projects = projects;
        }

        public void RecordBuildOutput(string projectName, string outputDir)
        {
            _buildOutputs[projectName] = outputDir;
        }

        public List<string> GetDependencyLibraryPaths(NovusProject project)
        {
            var libraryPaths = new List<string>();

            foreach (var (depName, depInfo) in project.Dependencies)
            {
                // Only handle workspace-internal dependencies
                if (depInfo.Path != null && _projects.ContainsKey(depName))
                {
                    if (_buildOutputs.TryGetValue(depName, out var outputDir))
                    {
                        var depProject = _projects[depName];

                        // For library projects, add path to the library artifact
                        if (depProject.Package.Type == "library")
                        {
                            // The outputDir for libraries already points to the .library directory
                            // (e.g., "/path/to/project/greeting.library")
                            var libName = depProject.Package.Name;
                            var libFile = Path.Combine(outputDir, $"{libName}_lib.o");

                            Console.WriteLine($"  DEBUG: Checking for lib stub: {libFile}");
                            Console.WriteLine($"  DEBUG: File exists: {File.Exists(libFile)}");

                            if (File.Exists(libFile))
                            {
                                libraryPaths.Add(libFile);
                            }
                        }
                    }
                }
            }

            return libraryPaths;
        }

        /// <summary>
        /// Get assembly files that dependent projects need to link against (call stubs, etc.)
        /// </summary>
        public List<string> GetDependencyAsmFiles(NovusProject project)
        {
            var asmFiles = new List<string>();

            foreach (var (depName, depInfo) in project.Dependencies)
            {
                // Only handle workspace-internal dependencies
                if (depInfo.Path != null && _projects.ContainsKey(depName))
                {
                    if (_buildOutputs.TryGetValue(depName, out var outputDir))
                    {
                        var depProject = _projects[depName];

                        // For library projects, include the call stubs
                        if (depProject.Package.Type == "library")
                        {
                            var libName = depProject.Package.Name;
                            var callStubsFile = Path.Combine(outputDir, $"{libName}_calls.s");
                            var libStubFile = Path.Combine(outputDir, $"{libName}_lib.s");

                            if (File.Exists(callStubsFile))
                            {
                                Console.WriteLine($"  ✓ Found library call stubs: {Path.GetFileName(callStubsFile)}");
                                asmFiles.Add(callStubsFile);
                            }

                            if (File.Exists(libStubFile))
                            {
                                Console.WriteLine($"  ✓ Found library stub: {Path.GetFileName(libStubFile)}");
                                asmFiles.Add(libStubFile);
                            }
                        }
                    }
                }
            }

            return asmFiles;
        }
    }

    /// <summary>
    /// Build a single project
    /// </summary>
    private static async Task<int> BuildProject(
        string projectDir,
        BuildOptions buildOptions,
        NovusWorkspace? workspace = null,
        BuildContext? buildContext = null,
        string? workspaceDir = null)
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

        // Determine build mode (Debug or Release)
        // Default to Debug unless --release is specified
        var buildMode = buildOptions.Release ? BuildMode.Release :
                       buildOptions.Debug ? BuildMode.Debug :
                       BuildMode.Debug;  // Default to debug

        // Determine output directory
        string outputDir;
        if (workspace != null)
        {
            // Workspace build - use centralized target directory
            var workspaceRoot = Path.GetDirectoryName(Path.Combine(workspaceDir!, "workspace.toml"))!;
            var targetRoot = Path.Combine(workspaceRoot, "target");

            // For now, simple structure: target/<config>/<type>/
            var config = buildMode == BuildMode.Release ? "release" : "debug";
            var typeSubdir = projectType.ToLowerInvariant() switch
            {
                "library" => "libs",
                "device" => "libs",
                "handler" => "libs",
                "test" => "tests",
                _ => "bins"
            };

            outputDir = Path.Combine(targetRoot, config, typeSubdir);
            Directory.CreateDirectory(outputDir);
        }
        else
        {
            // Standalone project - use project's output directory
            outputDir = Path.Combine(projectDir, project.Build.Output);
            Directory.CreateDirectory(outputDir);
        }

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

        // Validate CPU and FPU values
        var cpuError = ValidateCpu(targetCpu);
        if (cpuError != null)
        {
            Console.WriteLine($"Error: {cpuError}");
            return 1;
        }

        var fpuError = ValidateFpu(fpu);
        if (fpuError != null)
        {
            Console.WriteLine($"Error: {fpuError}");
            return 1;
        }

        // Optimization level: explicit > release default > project > workspace > 0
        int optimizationLevel;
        if (buildOptions.OptimizationLevel.HasValue)
        {
            optimizationLevel = buildOptions.OptimizationLevel.Value;
        }
        else if (buildMode == BuildMode.Release)
        {
            // Measured on the cli template: -O=1 13196 bytes, -O=2 15984, -O=0 14332.
            // -O=2 turns on IR inlining that costs more size than it saves on 68k.
            optimizationLevel = 1;  // Release default
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

        // Get dependency library paths if in workspace build
        var additionalLibraries = new List<string>();
        if (buildContext != null)
        {
            additionalLibraries = buildContext.GetDependencyLibraryPaths(project);
            if (additionalLibraries.Count > 0)
            {
                Console.WriteLine($"  Dependencies: {string.Join(", ", additionalLibraries.Select(Path.GetFileName))}");
            }
        }

        // Find any additional C files in the project directory (for library wrappers, etc.)
        var additionalCFiles = new List<string>();
        var cFiles = Directory.GetFiles(projectDir, "*.c", SearchOption.AllDirectories);
        foreach (var cFile in cFiles)
        {
            // Skip files in build output directories
            if (!PathUtility.ContainsDirectory(cFile, "target") &&
                !PathUtility.ContainsDirectory(cFile, "build"))
            {
                additionalCFiles.Add(cFile);
            }
        }
        if (additionalCFiles.Count > 0)
        {
            Console.WriteLine($"  Additional C files: {string.Join(", ", additionalCFiles.Select(Path.GetFileName))}");
        }

        // Collect additional assembly files from two sources:
        // 1. Explicitly listed in project.toml [build] section (AsmFiles array)
        // 2. Auto-discovered .s files in project directory (legacy behavior)
        var additionalAsmFiles = new List<string>();

        // First, process explicitly listed assembly files from project.toml
        if (project.Build.AsmFiles.Length > 0)
        {
            Console.WriteLine($"Processing {project.Build.AsmFiles.Length} assembly file(s) from project.toml...");
            foreach (var asmFileRelPath in project.Build.AsmFiles)
            {
                // Resolve path relative to project directory
                var asmFilePath = Path.Combine(projectDir, asmFileRelPath);
                var normalizedPath = Path.GetFullPath(asmFilePath);

                if (!File.Exists(normalizedPath))
                {
                    Console.WriteLine($"Error: Assembly file not found: {asmFileRelPath}");
                    Console.WriteLine($"  Looked for: {normalizedPath}");
                    return 1;
                }

                additionalAsmFiles.Add(normalizedPath);
                Console.WriteLine($"  → {asmFileRelPath}");
            }
        }

        // Then, auto-discover .s files in project directory (for backward compatibility)
        var asmFiles = Directory.GetFiles(projectDir, "*.s", SearchOption.AllDirectories);
        var normalizedExistingAsm = additionalAsmFiles.Select(f => Path.GetFullPath(f)).ToHashSet();

        foreach (var asmFile in asmFiles)
        {
            // Skip files in build output directories
            if (!PathUtility.ContainsDirectory(asmFile, "target") &&
                !PathUtility.ContainsDirectory(asmFile, "build"))
            {
                var normalizedPath = Path.GetFullPath(asmFile);
                // Only add if not already in the list from project.toml
                if (!normalizedExistingAsm.Contains(normalizedPath))
                {
                    additionalAsmFiles.Add(normalizedPath);
                    normalizedExistingAsm.Add(normalizedPath);
                }
            }
        }

        // Add dependency ASM files (library call stubs, etc.) from built libraries
        if (buildContext != null)
        {
            var depAsmFiles = buildContext.GetDependencyAsmFiles(project);
            // Add only if not already in the list (avoid duplicates by comparing normalized paths)
            // Reuse the normalized set from above
            foreach (var depAsmFile in depAsmFiles)
            {
                var normalizedPath = Path.GetFullPath(depAsmFile);
                if (!normalizedExistingAsm.Contains(normalizedPath))
                {
                    additionalAsmFiles.Add(depAsmFile);
                    normalizedExistingAsm.Add(normalizedPath);
                }
            }
        }

        if (additionalAsmFiles.Count > 0)
        {
            Console.WriteLine($"  Additional ASM files: {string.Join(", ", additionalAsmFiles.Select(Path.GetFileName))}");
        }

        // Determine output filename based on project type
        string outputFileName = projectType.ToLowerInvariant() switch
        {
            "library" => $"{project.Package.Name}.library",
            "device" => $"{project.Package.Name}.device",
            "handler" => $"{project.Package.Name}.handler",
            _ => project.Package.Name  // CLI and other executables use name as-is
        };

        // Convert to CompilerOptions
        var compilerOptions = new CompilerOptions
        {
            InputFile = inputFile,
            OutputFile = Path.Combine(outputDir, outputFileName),
            Cpu = targetCpu,
            Fpu = fpu,
            OptimizationLevel = optimizationLevel,
            BuildMode = buildMode,
            EmitAsmOnly = buildOptions.EmitAsmOnly || project.Build.EmitAsm,
            VbccPath = buildOptions.VbccPath ?? PathUtility.GetVbccPath(),
            NdkPath = UserConfig.ResolveNdkPath(buildOptions.NdkPath) ?? "",
            Verbose = buildOptions.Verbose,
            ProjectType = projectType,
            PackageName = project.Package.Name,  // Inject as PKG_NAME compile-time constant
            PackageVersion = project.Package.Version,  // Inject as PKG_VERSION compile-time constant and for @library generation
            AdditionalLibraries = additionalLibraries,
            AdditionalCFiles = additionalCFiles,
            AdditionalAsmFiles = additionalAsmFiles,
            // Forward safety level flags from BuildOptions (critical fix - was missing!)
            SafetyLevelOption = buildOptions.SafetyLevel,
            UnsafeMode = buildOptions.UnsafeMode,
            UseStdlibCache = buildOptions.UseStdlibCache,
            RebuildStdlibCache = buildOptions.RebuildStdlibCache
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
