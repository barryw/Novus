using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Novus.Diagnostics;

namespace Novus.Toolchain;

/// <summary>
/// Integration with the VBCC toolchain (vasm assembler and vlink linker)
/// </summary>
public class VbccToolchain
{
    private readonly string _vbccPath;
    private readonly string _ndkPath;

    // Infrastructure version - increment when making breaking changes to stubs/runtime
    // This is separate from CODEGEN_VERSION (which is for C code generation)
    private const int INFRASTRUCTURE_VERSION = 1;

    public VbccToolchain(string vbccPath, string ndkPath)
    {
        _vbccPath = vbccPath;
        _ndkPath = ndkPath;

        if (!Directory.Exists(_vbccPath))
            throw new DirectoryNotFoundException($"VBCC path not found: {_vbccPath}");

        if (!Directory.Exists(_ndkPath))
            throw new DirectoryNotFoundException($"NDK path not found: {_ndkPath}");
    }

    /// <summary>
    /// Check if infrastructure files (stubs, runtime) have changed and invalidate cache if needed
    /// </summary>
    private async Task<bool> NeedsInfrastructureRebuild(string outputPath, string compilerDir)
    {
        var cacheFile = Path.Combine(outputPath, ".novus_infrastructure_hash");

        // Compute hash of all infrastructure files
        var infrastructureHash = ComputeInfrastructureHash(compilerDir);
        var currentHash = $"v{INFRASTRUCTURE_VERSION}_{infrastructureHash}";

        // Check if cache file exists and matches
        if (File.Exists(cacheFile))
        {
            var cachedHash = await File.ReadAllTextAsync(cacheFile);
            if (cachedHash.Trim() == currentHash)
            {
                return false; // No rebuild needed
            }
        }

        // Write new hash
        await File.WriteAllTextAsync(cacheFile, currentHash);
        return true; // Rebuild needed
    }

    /// <summary>
    /// Compute combined hash of all infrastructure files (stubs, runtime)
    /// </summary>
    private string ComputeInfrastructureHash(string compilerDir)
    {
        using var sha256 = SHA256.Create();
        using var stream = new MemoryStream();

        // Hash all .s files in stubs directory
        var stubsDir = Path.Combine(compilerDir, "stubs");
        if (Directory.Exists(stubsDir))
        {
            var stubFiles = Directory.GetFiles(stubsDir, "*.s").OrderBy(f => f).ToArray();
            foreach (var file in stubFiles)
            {
                var fileBytes = File.ReadAllBytes(file);
                stream.Write(fileBytes, 0, fileBytes.Length);
            }
        }

        // Hash all files in runtime directory
        var runtimeDir = Path.Combine(compilerDir, "runtime");
        if (Directory.Exists(runtimeDir))
        {
            var runtimeFiles = Directory.GetFiles(runtimeDir).OrderBy(f => f).ToArray();
            foreach (var file in runtimeFiles)
            {
                var fileBytes = File.ReadAllBytes(file);
                stream.Write(fileBytes, 0, fileBytes.Length);
            }
        }

        stream.Position = 0;
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant().Substring(0, 16);
    }

    /// <summary>
    /// Assemble an infrastructure file (with caching and dependency tracking)
    /// </summary>
    private async Task<bool> AssembleInfrastructureFile(
        string sourceFile,
        string outputFile,
        string cpu,
        bool forcebuild)
    {
        // If not forcing rebuild and output exists, skip assembly
        if (!forcebuild && File.Exists(outputFile))
        {
            return true; // Use cached version
        }

        // Assemble the file
        return await Assemble(sourceFile, outputFile, cpu, false);
    }

    /// <summary>
    /// Assemble a .s file to a .o object file using vasm
    /// </summary>
    public async Task<bool> Assemble(string asmFile, string objFile, string cpu = "68020", bool enableFpu = false)
    {
        var vasmPath = Path.Combine(_vbccPath, "bin", "vasmm68k_mot");

        var args = new List<string>
        {
            "-Fhunk",           // Amiga HUNK format
            $"-m{cpu}",         // CPU target (68000, 68020, etc.)
            "-quiet",           // Suppress unnecessary output
            "-nowarn=62",       // Suppress "imported symbol not referenced" warnings
            "-o", objFile,      // Output file
            asmFile             // Input file
        };

        // Add FPU support if requested
        if (enableFpu)
        {
            args.Insert(2, "-m68881");  // Enable 68881/68882 FPU instructions
        }

        // Don't print here - caller will show progress
        return await RunTool(vasmPath, args);
    }

    /// <summary>
    /// Link object files to create an Amiga executable using vlink
    /// </summary>
    public async Task<bool> Link(string[] objFiles, string outputFile, string fpuMode = "auto", bool includeStartup = true, bool isLibrary = false, BuildMode buildMode = BuildMode.Debug)
    {
        var vlinkPath = Path.Combine(_vbccPath, "bin", "vlink");

        var args = new List<string>
        {
            "-bamigahunk",      // Amiga HUNK format
            "-w",               // Suppress linker warnings (especially "imported symbol not referenced")
            "-o", outputFile    // Output executable
        };

        // Debug symbols and map file only in debug mode
        // Note: vlink doesn't have -g flag - debug symbols are embedded by vasm/vc
        if (buildMode == BuildMode.Debug)
        {
            args.Insert(1, "-M");   // Generate map file
        }
        else
        {
            // Release mode: strip symbols for smaller binaries
            args.Insert(1, "-s");   // Strip all symbols
        }

        // CRITICAL: Set MEMF_CHIP flag on DATA_C section so AmigaOS loader
        // allocates it in chip RAM. Paula DMA can only access chip RAM,
        // so embedded audio/graphics assets MUST be in chip RAM to play.
        // Value 2 = MEMF_CHIP (from exec/memory.h)
        args.Add("-hunkattr");
        args.Add("DATA_C=2");

        // Add linker flags (library-specific behavior)
        args.Add("-x");  // Discard local symbols

        // CRITICAL: -nostdlib prevents vlink from auto-linking C library functions
        // that expect VBCC's startup.o setup. We provide our own startup code.
        args.Add("-nostdlib");

        if (isLibrary)
        {
            // For shared libraries: MUST keep relocations, no dead code elimination of wrappers
            // -Bstatic would strip relocations needed for &RomTag self-reference
            // -gc-all would remove wrapper functions that appear "unreferenced"
            args.Add("-Cvbcc");  // VBCC calling convention
            // NO -Bstatic
            // NO -gc-all
        }
        else
        {
            // For executables: standard static linking
            args.Add("-Bstatic");  // Static linking
            args.Add("-Cvbcc");  // VBCC calling convention

            // Dead code elimination with section merging to avoid duplicate symbol errors
            // -sc merges all code sections, -sd merges all data/bss sections
            // This allows linking duplicate monomorphized functions from stdlib
            // NOTE: DATA_C sections are properly typed as DATA (via data_c directive fix in FixChipRamSectionDirectives)
            // so they won't be merged with CODE sections by -sc
            args.Add("-sc");      // Merge all code sections (required to link duplicate symbols)
            args.Add("-sd");      // Merge all data/bss sections
            args.Add("-gc-all");        // Dead code elimination
            args.Add("-e");             // Specify entry point for -gc-all to trace from
            args.Add("_start");         // Entry point defined in novus_startup.s
            args.Add("-P");             // Protect VBCC stack symbol from stripping
            args.Add("___stack");       // VBCC stack symbol (defined in novus_startup.s)
            args.Add("-P");             // Protect AmigaOS $STACK: cookie from stripping
            args.Add("___stack_cookie"); // AmigaOS $STACK: cookie string (defined in novus_startup.s)
        }

        // Add startup code first (must come before user object files)
        if (includeStartup)
        {
            var startupObj = Path.Combine(_vbccPath, "targets", "m68k-amigaos", "lib", "startup.o");
            if (File.Exists(startupObj))
            {
                args.Add(startupObj);
            }
            else
            {
                throw new InvalidOperationException(
                    $"VBCC m68k-amigaos target not found. Expected: {startupObj}\n" +
                    "Novus requires AmigaOS 3.0+ target. The kick13 (1.3) target is not supported.");
            }
        }

        // Add object files
        args.AddRange(objFiles);

        // Add VBCC library path for math libraries
        var vbccLibPath = Path.Combine(_vbccPath, "targets", "m68k-amigaos", "lib");
        if (!Directory.Exists(vbccLibPath))
        {
            throw new InvalidOperationException(
                $"VBCC m68k-amigaos target library not found. Expected: {vbccLibPath}\n" +
                "Novus requires AmigaOS 3.0+ target. The kick13 (1.3) target is not supported.");
        }

        args.Add($"-L{vbccLibPath}");

        // Link appropriate math library based on FPU mode
        if (fpuMode == "auto")
        {
            // Fat binary - needs both soft-float and hardware FPU libraries
            args.Add("-lmsoft");  // Soft-float library
            args.Add("-lm881");   // Hardware FPU library
        }
        else if (fpuMode == "soft")
        {
            // Software floating point only
            args.Add("-lmsoft");
        }
        else if (fpuMode == "68881" || fpuMode == "68040")
        {
            // Hardware FPU library
            args.Add("-lm881");
        }

        // Always link with C runtime library (provides startup, exit, etc.)
        args.Add("-lvc");

        // Add amiga.lib for system library wrappers (BeginIO, DoIO, etc.)
        // This is in the same vbcc lib directory
        args.Add("-lamiga");

        // Add standard Amiga libraries path and link with -lauto if NDK lib exists
        // -lauto: Provides automatic library base opening/closing
        var libPath = Path.Combine(_ndkPath, "lib");
        if (Directory.Exists(libPath))
        {
            args.Add($"-L{libPath}");
            args.Add("-lauto");
        }

        // Don't print here - caller shows final success message
        // Special handling for vlink with -M flag to save map file
        if (args.Contains("-M"))
        {
            return await RunToolWithMapFile(vlinkPath, args, outputFile + ".map");
        }
        return await RunTool(vlinkPath, args);
    }

    /// <summary>
    /// Compile a C file to an object file using VBCC's vc frontend (compile only, no linking)
    /// In debug mode with statement-level debug markers, we use a two-step process:
    /// 1. Generate assembly with -S
    /// 2. Post-process to inject debug labels from DBG comments
    /// 3. Assemble with vasm
    /// </summary>
    public async Task<bool> CompileToObject(
        string cFile,
        string objFile,
        string cpu = "68020",
        int optimization = 0,
        BuildMode buildMode = BuildMode.Debug,
        IEnumerable<string>? extraIncludePaths = null,
        bool enableFpu = false)
    {
        var vcPath = Path.Combine(_vbccPath, "bin", "vc");

        // Select config based on FPU mode
        // aos68k_fpu config has -m68881 in the assembler command to accept FPU instructions
        var configName = enableFpu ? "+aos68k_fpu" : "+aos68k";

        // Determine optimization level based on build mode
        // Note: Using O=0 for release to test if optimization is the issue
        var optLevel = 0;

        // Build include path arguments
        var includeArgs = new List<string>();
        if (extraIncludePaths != null)
        {
            foreach (var path in extraIncludePaths)
            {
                includeArgs.Add($"-I{path}");
            }
        }

        // Auto-detect runtime files and add their directory to include path
        // This allows split runtime files to include their shared header
        var cFileDir = Path.GetDirectoryName(cFile);
        if (cFileDir != null && cFileDir.EndsWith(Path.DirectorySeparatorChar + "runtime"))
        {
            includeArgs.Add($"-I{cFileDir}");
        }

        // In debug mode, we use a two-step compile: C -> asm (with post-processing) -> object
        // This allows us to inject debug labels that VBCC's direct C->obj path doesn't support
        if (buildMode == BuildMode.Debug)
        {
            var asmFile = Path.ChangeExtension(objFile, ".s");

            // Step 1: Compile C to assembly
            var asmArgs = new List<string>
            {
                configName,         // Target config (aos68k or aos68k_fpu)
                "-c99",             // Enable C99 standard
                $"-cpu={cpu}",      // CPU target
                "-g",               // Debug symbols
                $"-O={optLevel}",   // Optimization level
                "-use-framepointer", // CRITICAL: Force frame pointer (A6) for all functions
                "-S",               // Generate assembly output
                "-o", asmFile,      // Output assembly file
            };
            asmArgs.AddRange(includeArgs);
            asmArgs.Add(cFile);     // Input C file

            if (!await RunTool(vcPath, asmArgs))
            {
                Console.WriteLine($"VBCC assembly generation failed for {cFile}");
                return false;
            }

            // Step 2: Post-process assembly to inject debug labels
            await InjectDebugLabelsIntoAssembly(asmFile);

            // Step 2b: Fix chip RAM section directives
            // VBCC outputs 'section "DATA_C"' which vasm interprets as CODE type by default.
            // We need to change it to 'data_c' directive which properly sets DATA type + MEMF_CHIP.
            await FixChipRamSectionDirectives(asmFile);

            // Step 3: Assemble to object file (enableFpu passed through for consistent behavior)
            return await Assemble(asmFile, objFile, cpu, enableFpu);
        }

        // Release mode: direct C -> object compilation
        var args = new List<string>
        {
            configName,         // Target config (aos68k or aos68k_fpu)
            "-c99",             // Enable C99 standard
            $"-cpu={cpu}",      // CPU target
            $"-O={optLevel}",   // Optimization level
            "-use-framepointer", // CRITICAL: Force frame pointer (A6) for all functions to fix stack offset bugs
            "-c",               // Compile only, don't link
            "-o", objFile,      // Output object file
        };
        args.AddRange(includeArgs);
        args.Add(cFile);        // Input C file

        // Don't print here - caller will show progress
        return await RunTool(vcPath, args);
    }

    /// <summary>
    /// Post-process VBCC-generated assembly to inject debug labels.
    ///
    /// Strategy:
    /// 1. Parse the C file to extract mapping: C line number → (label name, Novus source:line)
    /// 2. Parse the assembly file to find VBCC's "debug N" directives
    /// 3. Inject labels before each "debug N" instruction based on the mapping
    /// </summary>
    private async Task InjectDebugLabelsIntoAssembly(string asmFile)
    {
        if (!File.Exists(asmFile))
            return;

        // Get corresponding C file
        var cFile = Path.ChangeExtension(asmFile, ".c");
        if (!File.Exists(cFile))
            return;

        // Step 1: Parse C file to build list of debug markers with their C line numbers
        // Format: /* DBG: __dbg_funcname_N = filename:line */
        var dbgPattern = new System.Text.RegularExpressions.Regex(
            @"/\*\s*DBG:\s*(__dbg_\w+)\s*=\s*(\S+):(\d+)\s*\*/");

        // List of (cLineNumber, labelName, fileName, novusLine) sorted by C line number
        var debugMarkers = new List<(int CLine, string LabelName, string FileName, int NovusLine)>();
        var cLines = await File.ReadAllLinesAsync(cFile);

        for (int i = 0; i < cLines.Length; i++)
        {
            var match = dbgPattern.Match(cLines[i]);
            if (match.Success)
            {
                var labelName = match.Groups[1].Value;
                var fileName = match.Groups[2].Value;
                var novusLine = int.Parse(match.Groups[3].Value);

                // The DBG comment is on line i+1 (1-indexed)
                // We want to inject the label at the first debug directive AT OR AFTER this line
                debugMarkers.Add((i + 1, labelName, fileName, novusLine));
            }
        }

        if (debugMarkers.Count == 0)
            return; // No debug markers to process

        // Step 2: Parse assembly and inject labels at "debug N" directives
        // For each debug marker, inject it at the first debug directive with line >= marker's line
        var asmLines = await File.ReadAllLinesAsync(asmFile);
        var output = new StringBuilder();
        var labelCount = 0;
        var debugDirectivePattern = new System.Text.RegularExpressions.Regex(@"^\s*debug\s+(\d+)");
        var sectionDirectivePattern = new System.Text.RegularExpressions.Regex(@"^\s*section\s+""(DATA|BSS)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var injectedLabels = new HashSet<string>(); // Track which labels we've already injected
        var markerIndex = 0; // Current marker we're trying to inject
        var inCodeSection = true; // Track if we're in the CODE section

        foreach (var line in asmLines)
        {
            // Check if we're about to leave the CODE section
            // If so, inject any remaining markers NOW, before switching sections
            if (inCodeSection && sectionDirectivePattern.IsMatch(line))
            {
                // Inject all remaining markers before leaving CODE section
                while (markerIndex < debugMarkers.Count)
                {
                    var marker = debugMarkers[markerIndex];
                    if (!injectedLabels.Contains(marker.LabelName))
                    {
                        // Emit the label as a global symbol so it appears in the symbol table
                        output.AppendLine($"\txdef\t_{marker.LabelName}");
                        output.AppendLine($"_{marker.LabelName}:");
                        output.AppendLine($"; Source: {marker.FileName}:{marker.NovusLine}");
                        labelCount++;
                        injectedLabels.Add(marker.LabelName);
                    }
                    markerIndex++;
                }
                inCodeSection = false;
            }

            var match = debugDirectivePattern.Match(line);
            if (match.Success && markerIndex < debugMarkers.Count && inCodeSection)
            {
                var debugLineNum = int.Parse(match.Groups[1].Value);

                // Inject all markers that should appear at or before this debug directive
                while (markerIndex < debugMarkers.Count && debugMarkers[markerIndex].CLine <= debugLineNum)
                {
                    var marker = debugMarkers[markerIndex];
                    if (!injectedLabels.Contains(marker.LabelName))
                    {
                        // Emit the label as a global symbol so it appears in the symbol table
                        output.AppendLine($"\txdef\t_{marker.LabelName}");
                        output.AppendLine($"_{marker.LabelName}:");
                        output.AppendLine($"; Source: {marker.FileName}:{marker.NovusLine}");
                        labelCount++;
                        injectedLabels.Add(marker.LabelName);
                    }
                    markerIndex++;
                }
            }
            output.AppendLine(line);
        }

        // Handle any remaining markers that didn't find a debug directive
        // This shouldn't happen if we injected them before the section change,
        // but as a safety net, inject them here with an explicit CODE section
        if (markerIndex < debugMarkers.Count)
        {
            output.AppendLine("\tsection\t\"CODE\",code"); // Ensure we're in CODE section
            while (markerIndex < debugMarkers.Count)
            {
                var marker = debugMarkers[markerIndex];
                if (!injectedLabels.Contains(marker.LabelName))
                {
                    output.AppendLine($"\txdef\t_{marker.LabelName}");
                    output.AppendLine($"_{marker.LabelName}:");
                    output.AppendLine($"; Source: {marker.FileName}:{marker.NovusLine}");
                    labelCount++;
                    injectedLabels.Add(marker.LabelName);
                }
                markerIndex++;
            }
        }

        if (labelCount > 0)
        {
            Console.WriteLine($"  → Injected {labelCount} debug label(s) into {Path.GetFileName(asmFile)}");
        }

        await File.WriteAllTextAsync(asmFile, output.ToString());
    }

    /// <summary>
    /// Fix chip RAM section directives in VBCC-generated assembly.
    ///
    /// Problem: VBCC outputs 'section "DATA_C"' for __section("DATA_C") attribute,
    /// but vasm's Motorola syntax treats this as a generic section that defaults to CODE type.
    ///
    /// Solution: Replace 'section "DATA_C"' with the dedicated 'data_c' directive,
    /// which vasm recognizes as DATA type with MEMF_CHIP attribute (value 2).
    /// </summary>
    private async Task FixChipRamSectionDirectives(string asmFile)
    {
        if (!File.Exists(asmFile))
            return;

        var content = await File.ReadAllTextAsync(asmFile);
        var modified = false;

        // Replace section "DATA_C" with data_c directive
        // Also handle variations with different whitespace/casing
        if (content.Contains("section\t\"DATA_C\"") || content.Contains("section \"DATA_C\""))
        {
            content = content.Replace("section\t\"DATA_C\"", "data_c");
            content = content.Replace("section \"DATA_C\"", "data_c");
            modified = true;
        }

        // Also handle DATA_F for fast RAM if needed
        if (content.Contains("section\t\"DATA_F\"") || content.Contains("section \"DATA_F\""))
        {
            content = content.Replace("section\t\"DATA_F\"", "data_f");
            content = content.Replace("section \"DATA_F\"", "data_f");
            modified = true;
        }

        // Handle CODE_C for chip RAM code (rare but possible)
        if (content.Contains("section\t\"CODE_C\"") || content.Contains("section \"CODE_C\""))
        {
            content = content.Replace("section\t\"CODE_C\"", "code_c");
            content = content.Replace("section \"CODE_C\"", "code_c");
            modified = true;
        }

        // Handle BSS_C for chip RAM BSS
        if (content.Contains("section\t\"BSS_C\"") || content.Contains("section \"BSS_C\""))
        {
            content = content.Replace("section\t\"BSS_C\"", "bss_c");
            content = content.Replace("section \"BSS_C\"", "bss_c");
            modified = true;
        }

        if (modified)
        {
            await File.WriteAllTextAsync(asmFile, content);
        }
    }

    /// <summary>
    /// Compile C code to an Amiga executable using VBCC's vc frontend
    /// </summary>
    public async Task<bool> CompileWithVC(
        List<string> cFiles,
        List<string> objectFiles,
        string outputPath,
        string cpu = "68020",
        int optimization = 2,
        BuildMode buildMode = BuildMode.Debug)
    {
        var vcPath = Path.Combine(_vbccPath, "bin", "vc");

        // Determine optimization level based on build mode
        // Note: Using O=0 for release to test if optimization is the issue
        var optLevel = 0;

        var args = new List<string>
        {
            "+aos68k",          // Target AmigaOS 2.0+ (68020+)
            "-c99",             // Enable C99 standard
            $"-cpu={cpu}",      // CPU target
            $"-O={optLevel}",   // Optimization level
            "-use-framepointer", // CRITICAL: Force frame pointer (A6) for all functions to fix stack offset bugs
            "-nostdlib",        // Don't use standard library startup
            "-o", outputPath    // Output file
        };

        // Debug symbols only in debug mode
        if (buildMode == BuildMode.Debug)
        {
            args.Insert(4, "-g");
        }

        // CRITICAL: Add pre-assembled object files FIRST
        // novus_startup.o MUST come before C code so _start is at CODE+0
        args.AddRange(objectFiles);

        // Add C source files AFTER object files
        args.AddRange(cFiles);

        // Link against VBCC runtime library ONLY
        // Don't use -lamiga because it provides DOS functions with wrong signatures
        args.Add("-lvc");        // VBCC runtime library (for C runtime support)

        Console.WriteLine($"Compiling with vc: {Path.GetFileName(outputPath)}");
        Console.WriteLine($"Debug: vc {string.Join(" ", args)}");
        return await RunTool(vcPath, args);
    }

    /// <summary>
    /// Compile a complete Novus source file to an Amiga executable
    /// </summary>
    public async Task<bool> CompileToExecutable(
        string asmSource,
        string outputPath,
        string baseName,
        string cpu = "68020",
        bool enableFpu = false,
        string fpuMode = "auto")
    {
        // Write assembly to temporary file
        var asmFile = Path.Combine(outputPath, $"{baseName}.s");
        var objFile = Path.Combine(outputPath, $"{baseName}.o");
        var exeFile = Path.Combine(outputPath, baseName);

        await File.WriteAllTextAsync(asmFile, asmSource);

        // For fat binaries (cpu="auto"), use 68020 for assembly since it contains CPU-specific code
        // The code generator ensures base code is 68000-compatible, with 68020+ code only in CPU-specific sections
        var assemblyCpu = cpu == "auto" ? "68020" : cpu;
        var compilerDir = AppContext.BaseDirectory;

        // Check if infrastructure files have changed - if so, force rebuild of all infrastructure
        var forceInfrastructureRebuild = await NeedsInfrastructureRebuild(outputPath, compilerDir);

        if (forceInfrastructureRebuild)
        {
            Console.WriteLine("  → Infrastructure files changed - rebuilding all infrastructure");
        }

        // Assemble (with FPU support if fat binary or FPU mode)
        if (!await Assemble(asmFile, objFile, assemblyCpu, enableFpu))
        {
            Console.WriteLine("Assembly failed");
            return false;
        }

        var objFiles = new List<string>();

        // CRITICAL: Assemble novus_startup.o FIRST so _start is at CODE+0
        var startupSource = Path.Combine(compilerDir, "stubs", "novus_startup.s");
        if (File.Exists(startupSource))
        {
            var startupObj = Path.Combine(outputPath, "novus_startup.o");
            if (!await AssembleInfrastructureFile(startupSource, startupObj, assemblyCpu, forceInfrastructureRebuild))
            {
                Console.WriteLine("novus_startup assembly failed");
                return false;
            }
            objFiles.Add(startupObj);
        }

        // Add main program object file
        objFiles.Add(objFile);

        // Assemble exec_base.o (required for _SysBase - always needed)
        var execBaseSource = Path.Combine(compilerDir, "stubs", "exec_base.s");
        if (File.Exists(execBaseSource))
        {
            var execBaseObj = Path.Combine(outputPath, "exec_base.o");
            if (!await AssembleInfrastructureFile(execBaseSource, execBaseObj, assemblyCpu, forceInfrastructureRebuild))
            {
                Console.WriteLine("exec_base assembly failed");
                return false;
            }
            objFiles.Add(execBaseObj);
        }

        // Detect which library stubs are needed and assemble them
        Console.WriteLine("\n=== DEBUG: CompileToExecutable - Detecting libraries ===");
        Console.WriteLine($"Scanning main assembly source for library references...");
        var requiredLibraries = DetectRequiredLibraries(asmSource);
        Console.WriteLine($"Required libraries: {string.Join(", ", requiredLibraries)}");

        foreach (var library in requiredLibraries)
        {
            var stubsSource = Path.Combine(compilerDir, "stubs", $"{library}_stubs.s");

            if (File.Exists(stubsSource))
            {
                var stubsObj = Path.Combine(outputPath, $"{library}_stubs.o");

                if (!await AssembleInfrastructureFile(stubsSource, stubsObj, assemblyCpu, forceInfrastructureRebuild))
                {
                    Console.WriteLine($"{library} stubs assembly failed");
                    return false;
                }

                objFiles.Add(stubsObj);

                // NOTE: dos_base.o and dos_init.o are now added explicitly in Program.cs
                // as part of the core runtime files for all executables
            }
            else
            {
                Console.WriteLine($"Warning: {library} functions used but stubs not found at {stubsSource}");
            }

            // Also check for library base storage files (e.g., bsdsocket_bases.s)
            // These provide storage for extern vars like SocketBase
            var basesSource = Path.Combine(compilerDir, "stubs", $"{library}_bases.s");
            if (File.Exists(basesSource))
            {
                var basesObj = Path.Combine(outputPath, $"{library}_bases.o");

                if (!await AssembleInfrastructureFile(basesSource, basesObj, assemblyCpu, forceInfrastructureRebuild))
                {
                    Console.WriteLine($"{library} bases assembly failed");
                    return false;
                }

                objFiles.Add(basesObj);
            }
        }

        // Link with appropriate math library (novus_startup.o already in objFiles)
        if (!await Link(objFiles.ToArray(), exeFile, fpuMode, includeStartup: false))
        {
            Console.WriteLine("Linking failed");
            return false;
        }

        Console.WriteLine($"Successfully created: {exeFile}");
        return true;
    }

    /// <summary>
    /// Compile a Novus source file with dependencies to an Amiga executable
    /// </summary>
    public async Task<bool> CompileToExecutableWithDependencies(
        string mainAsmSource,
        Dictionary<string, string> dependencyAssemblies,  // module path -> assembly
        string outputPath,
        string baseName,
        string cpu = "68020",
        bool enableFpu = false,
        string fpuMode = "auto")
    {
        // For fat binaries (cpu="auto"), use 68020 for assembly since it contains CPU-specific code
        // The code generator ensures base code is 68000-compatible, with 68020+ code only in CPU-specific sections
        var assemblyCpu = cpu == "auto" ? "68020" : cpu;
        var objFiles = new List<string>();
        var compilerDir = AppContext.BaseDirectory;

        // Check if infrastructure files have changed - if so, force rebuild of all infrastructure
        var forceInfrastructureRebuild = await NeedsInfrastructureRebuild(outputPath, compilerDir);

        if (forceInfrastructureRebuild)
        {
            Console.WriteLine("  → Infrastructure files changed - rebuilding all infrastructure");
        }

        // CRITICAL: Assemble novus_startup.o FIRST so _start is at CODE+0
        var startupSource = Path.Combine(compilerDir, "stubs", "novus_startup.s");
        if (File.Exists(startupSource))
        {
            var startupObj = Path.Combine(outputPath, "novus_startup.o");
            if (!await AssembleInfrastructureFile(startupSource, startupObj, assemblyCpu, forceInfrastructureRebuild))
            {
                Console.WriteLine("novus_startup assembly failed");
                return false;
            }
            objFiles.Add(startupObj);
        }

        // Assemble the main file
        var mainAsmFile = Path.Combine(outputPath, $"{baseName}.s");
        var mainObjFile = Path.Combine(outputPath, $"{baseName}.o");
        await File.WriteAllTextAsync(mainAsmFile, mainAsmSource);

        Console.WriteLine($"  → {baseName}.s → {baseName}.o");
        if (!await Assemble(mainAsmFile, mainObjFile, assemblyCpu, enableFpu))
        {
            Console.WriteLine("Main assembly failed");
            return false;
        }
        objFiles.Add(mainObjFile);

        // Assemble exec_base.o (required for _SysBase - always needed)
        var execBaseSource = Path.Combine(compilerDir, "stubs", "exec_base.s");
        if (File.Exists(execBaseSource))
        {
            var execBaseObj = Path.Combine(outputPath, "exec_base.o");
            if (!await AssembleInfrastructureFile(execBaseSource, execBaseObj, assemblyCpu, forceInfrastructureRebuild))
            {
                Console.WriteLine("exec_base assembly failed");
                return false;
            }
            objFiles.Add(execBaseObj);
        }

        // Extract symbols referenced by the main assembly
        var referencedSymbols = ExtractReferencedSymbols(mainAsmSource);

        // Only assemble and link dependency modules that export referenced symbols
        foreach (var (modulePath, asmSource) in dependencyAssemblies)
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);

            // Check if this dependency exports any symbols that are referenced
            var exportedSymbols = ExtractExportedSymbols(asmSource);
            var isReferenced = exportedSymbols.Any(sym => referencedSymbols.Contains(sym));

            if (!isReferenced)
            {
                Console.WriteLine($"  ⊘ Skipping {moduleName} (not referenced)");
                continue;
            }

            var depAsmFile = Path.Combine(outputPath, $"{moduleName}.s");
            var depObjFile = Path.Combine(outputPath, $"{moduleName}.o");

            await File.WriteAllTextAsync(depAsmFile, asmSource);

            Console.WriteLine($"  → {moduleName}.s → {moduleName}.o");
            if (!await Assemble(depAsmFile, depObjFile, assemblyCpu, enableFpu))
            {
                Console.WriteLine($"Dependency assembly failed: {moduleName}");
                return false;
            }
            objFiles.Add(depObjFile);
        }

        // Detect which library stubs are needed (scan only linked assemblies)
        Console.WriteLine("\n=== DEBUG: CompileToExecutableWithDependencies - Detecting libraries ===");
        var linkedAssemblies = new List<string> { mainAsmSource };
        Console.WriteLine($"Starting with main assembly source ({mainAsmSource.Length} chars)");

        // Add only the dependency assemblies that were actually linked
        foreach (var (modulePath, asmSource) in dependencyAssemblies)
        {
            var exportedSymbols = ExtractExportedSymbols(asmSource);
            if (exportedSymbols.Any(sym => referencedSymbols.Contains(sym)))
            {
                Console.WriteLine($"Adding linked dependency: {Path.GetFileNameWithoutExtension(modulePath)} ({asmSource.Length} chars)");
                linkedAssemblies.Add(asmSource);
            }
        }

        Console.WriteLine($"\nScanning {linkedAssemblies.Count} linked assembly source(s) for library references...");
        var requiredLibraries = new HashSet<string>();
        int assemblyIndex = 0;
        foreach (var asm in linkedAssemblies)
        {
            Console.WriteLine($"\n--- Scanning assembly #{++assemblyIndex} ---");
            var libs = DetectRequiredLibraries(asm);
            foreach (var lib in libs)
            {
                if (!requiredLibraries.Contains(lib))
                {
                    Console.WriteLine($"  Adding library '{lib}' to required set");
                }
                requiredLibraries.Add(lib);
            }
        }

        Console.WriteLine($"\n=== Final required libraries: {string.Join(", ", requiredLibraries)} ===\n");

        // Assemble library stubs (only once per library)
        foreach (var library in requiredLibraries)
        {
            var stubsSource = Path.Combine(compilerDir, "stubs", $"{library}_stubs.s");

            if (File.Exists(stubsSource))
            {
                var stubsObj = Path.Combine(outputPath, $"{library}_stubs.o");

                if (!await AssembleInfrastructureFile(stubsSource, stubsObj, assemblyCpu, forceInfrastructureRebuild))
                {
                    Console.WriteLine($"{library} stubs assembly failed");
                    return false;
                }

                objFiles.Add(stubsObj);

                // NOTE: dos_base.o and dos_init.o are now added explicitly in Program.cs
                // as part of the core runtime files for all executables
            }
            else
            {
                Console.WriteLine($"Warning: {library} functions used but stubs not found at {stubsSource}");
            }

            // Also check for library base storage files (e.g., bsdsocket_bases.s)
            // These provide storage for extern vars like SocketBase
            var basesSource = Path.Combine(compilerDir, "stubs", $"{library}_bases.s");
            if (File.Exists(basesSource))
            {
                var basesObj = Path.Combine(outputPath, $"{library}_bases.o");

                if (!await AssembleInfrastructureFile(basesSource, basesObj, assemblyCpu, forceInfrastructureRebuild))
                {
                    Console.WriteLine($"{library} bases assembly failed");
                    return false;
                }

                objFiles.Add(basesObj);
            }
        }

        // Link all object files (novus_startup.o already in objFiles)
        var exeFile = Path.Combine(outputPath, baseName);
        Console.WriteLine($"\nLinking {objFiles.Count} object file{(objFiles.Count > 1 ? "s" : "")}...");
        if (!await Link(objFiles.ToArray(), exeFile, fpuMode, includeStartup: false))
        {
            Console.WriteLine("Linking failed");
            return false;
        }

        Console.WriteLine($"\n✓ Successfully created: {Path.GetFileName(exeFile)}");
        return true;
    }

    /// <summary>
    /// Extract all external symbol references (xref) from assembly code
    /// </summary>
    private HashSet<string> ExtractReferencedSymbols(string asmSource)
    {
        var symbols = new HashSet<string>();

        // Look for "xref _symbolName" directives
        var lines = asmSource.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("xref\t") || trimmed.StartsWith("xref "))
            {
                var symbolName = trimmed.Substring(4).Trim();
                symbols.Add(symbolName);
            }
        }

        return symbols;
    }

    /// <summary>
    /// Extract all exported symbols (xdef) from assembly code
    /// </summary>
    private HashSet<string> ExtractExportedSymbols(string asmSource)
    {
        var symbols = new HashSet<string>();

        // Look for "xdef _symbolName" directives
        var lines = asmSource.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("xdef\t") || trimmed.StartsWith("xdef "))
            {
                var symbolName = trimmed.Substring(4).Trim();
                symbols.Add(symbolName);
            }
        }

        return symbols;
    }

    /// <summary>
    /// Detect which Amiga libraries are referenced in the assembly code
    /// </summary>
    private HashSet<string> DetectRequiredLibraries(string asmSource)
    {
        Console.WriteLine("\n=== DEBUG: DetectRequiredLibraries called ===");
        Console.WriteLine($"Assembly source length: {asmSource.Length} characters");
        Console.WriteLine($"First 500 chars of assembly:\n{asmSource.Substring(0, Math.Min(500, asmSource.Length))}\n");

        var libraries = new HashSet<string>();

        // Map of library names to their available stub files
        var libraryFunctions = new Dictionary<string, string[]>
        {
            ["dos"] = new[] { "_Output", "_Input", "_Error", "_Write", "_Read", "_Printf", "_Open", "_Close", "_CurrentDir", "_CreateDir", "_DeleteFile", "_Execute", "_Delay", "_IoErr", "_Seek", "_Rename", "_Lock", "_UnLock" },
            ["exec"] = new[] { "_AllocMem", "_FreeMem", "_ExecAllocMem", "_ExecFreeMem", "_OpenLibrary", "_CloseLibrary", "_FindTask", "_Wait", "_Signal", "_AllocSignal", "_FreeSignal" },
            ["intuition"] = new[] { "_OpenWindow", "_CloseWindow", "_OpenScreen", "_CloseScreen", "_OpenWindowTagList", "_OpenScreenTagList", "_DisplayAlert", "_AutoRequest" },
            ["graphics"] = new[] { "_LoadRGB4", "_SetRast", "_Move", "_Draw", "_Text", "_OpenFont", "_CloseFont", "_SetAPen", "_SetBPen" },
            ["diskfont"] = new[] { "_OpenDiskFont", "_AvailFonts" },
            ["icon"] = new[] { "_GetDiskObject", "_PutDiskObject", "_FreeDiskObject" },
            ["gadtools"] = new[] { "_CreateGadget", "_CreateMenus", "_LayoutMenus", "_FreeGadgets", "_FreeMenus" },
            ["utility"] = new[] { "_Stricmp", "_ToUpper", "_ToLower", "_GetTagData" },
            ["layers"] = new[] { "_LockLayer", "_UnlockLayer", "_ScrollLayer" },
            ["commodities"] = new[] { "_CreateCxObj", "_DeleteCxObj", "_ActivateCxObj" },
            ["keymap"] = new[] { "_SetKeyMapDefault", "_MapRawKey" },
            ["locale"] = new[] { "_OpenLocale", "_CloseLocale", "_FormatString" },
            ["expansion"] = new[] { "_FindConfigDev", "_ReadExpansionRom" },
            ["iffparse"] = new[] { "_OpenIFF", "_CloseIFF", "_ParseIFF" },
            ["timer"] = new[] { "_GetSysTime", "_AddTime", "_SubTime" },
            ["mathieeedoubbas"] = new[] { "_IEEEDPAdd", "_IEEEDPSub", "_IEEEDPMul", "_IEEEDPDiv" },
            ["mathieeesingbas"] = new[] { "_IEEESPAdd", "_IEEESPSub", "_IEEESPMul", "_IEEESPDiv" },
            // BSD Socket library (AmiTCP, Miami, Roadshow)
            ["bsdsocket"] = new[] { "_socket", "_bind", "_listen", "_accept", "_connect", "_send", "_recv", "_sendto", "_recvfrom", "_gethostbyname", "_gethostbyaddr", "_inet_aton", "_inet_ntoa", "_SetErrnoPtr", "_SocketBaseTags", "_CloseSocket", "_shutdown", "_setsockopt", "_getsockopt", "_select", "_WaitSelect", "_Errno", "_gethostname", "_getservbyname", "_getprotobyname" }
        };

        // Check each library's functions
        Console.WriteLine("Checking for library function references...");
        foreach (var (library, functions) in libraryFunctions)
        {
            var foundFunctions = new List<string>();

            foreach (var func in functions)
            {
                var pattern = $"xref\t{func}";
                if (asmSource.Contains(pattern))
                {
                    foundFunctions.Add(func);
                }
            }

            if (foundFunctions.Count > 0)
            {
                libraries.Add(library);
                Console.WriteLine($"  ✓ Detected library '{library}': found {foundFunctions.Count} function(s)");
                foreach (var func in foundFunctions)
                {
                    Console.WriteLine($"    - {func}");
                }
            }
        }

        if (libraries.Count == 0)
        {
            Console.WriteLine("  No library references detected");
        }

        Console.WriteLine($"=== Total detected libraries: {libraries.Count} ===\n");
        return libraries;
    }

    /// <summary>
    /// Run vlink with -M flag and save map output to file
    /// </summary>
    private async Task<bool> RunToolWithMapFile(string toolPath, List<string> args, string mapFilePath)
    {
        if (!File.Exists(toolPath))
        {
            Console.WriteLine($"Tool not found: {toolPath}");
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = string.Join(" ", args.Select(QuoteIfNeeded)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set VBCC environment variable so it can find config files
        startInfo.EnvironmentVariables["VBCC"] = _vbccPath;

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine("Failed to start process");
                return false;
            }

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    error.AppendLine(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var exitCode = process.ExitCode;

            // Save stdout to map file (this contains the symbol map from -M flag)
            if (output.Length > 0)
            {
                await File.WriteAllTextAsync(mapFilePath, output.ToString());
                Console.WriteLine($"  → Map file saved: {Path.GetFileName(mapFilePath)}");
            }

            if (error.Length > 0)
                Console.Error.WriteLine(error.ToString());

            return exitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running tool: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> RunTool(string toolPath, List<string> args)
    {
        if (!File.Exists(toolPath))
        {
            Console.WriteLine($"Tool not found: {toolPath}");
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = string.Join(" ", args.Select(QuoteIfNeeded)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set VBCC environment variable so it can find config files
        startInfo.EnvironmentVariables["VBCC"] = _vbccPath;

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine("Failed to start process");
                return false;
            }

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    output.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    error.AppendLine(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var exitCode = process.ExitCode;

            if (output.Length > 0)
                Console.WriteLine(output.ToString());

            if (error.Length > 0)
                Console.Error.WriteLine(error.ToString());

            return exitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running tool: {ex.Message}");
            return false;
        }
    }

    private static string QuoteIfNeeded(string arg)
    {
        // Quote if contains space or shell metacharacters
        return (arg.Contains(' ') || arg.Contains('<') || arg.Contains('>')) ? $"\"{arg}\"" : arg;
    }

    /// <summary>
    /// Assemble a single .asm file to .o object file
    /// Convenience method for building vendor libraries like ptplayer
    /// </summary>
    public async Task<bool> AssembleFile(string asmFile, string objFile, string cpu = "68020")
    {
        return await Assemble(asmFile, objFile, cpu, false);
    }

    /// <summary>
    /// Compile a single C file to .o object file using vc
    /// Convenience method for building vendor libraries like ptplayer
    /// </summary>
    public async Task<bool> CompileCFile(string cFile, string objFile, BuildMode buildMode = BuildMode.Debug)
    {
        var vcPath = Path.Combine(_vbccPath, "bin", "vc");

        var args = new List<string>
        {
            "+aos68k",          // Target AmigaOS 2.0+ (68020+)
            "-c99",             // Enable C99 standard
            "-O2",              // Optimization level
            "-c",               // Compile only, don't link
            "-o", objFile,      // Output file
            cFile               // Input file
        };

        // Debug symbols only in debug mode
        if (buildMode == BuildMode.Debug)
        {
            args.Insert(3, "-g");
        }

        return await RunTool(vcPath, args);
    }
}
