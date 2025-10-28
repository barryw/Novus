using System.Diagnostics;
using System.Text;

namespace Novus.Toolchain;

/// <summary>
/// Integration with the VBCC toolchain (vasm assembler and vlink linker)
/// </summary>
public class VbccToolchain
{
    private readonly string _vbccPath;
    private readonly string _ndkPath;

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
    public async Task<bool> Link(string[] objFiles, string outputFile, string fpuMode = "auto", bool includeStartup = true)
    {
        var vlinkPath = Path.Combine(_vbccPath, "bin", "vlink");

        var args = new List<string>
        {
            "-bamigahunk",      // Amiga HUNK format
            "-o", outputFile    // Output executable
        };

        // Add linker flags
        args.Add("-x");  // Discard local symbols
        args.Add("-Bstatic");  // Static linking
        args.Add("-Cvbcc");  // VBCC calling convention

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
                // Fallback to kick13 if amigaos not found
                startupObj = Path.Combine(_vbccPath, "targets", "m68k-kick13", "lib", "startup.o");
                if (File.Exists(startupObj))
                {
                    args.Add(startupObj);
                }
            }
        }

        // Add object files
        args.AddRange(objFiles);

        // Add VBCC library path for math libraries
        var vbccLibPath = Path.Combine(_vbccPath, "targets", "m68k-amigaos", "lib");
        if (!Directory.Exists(vbccLibPath))
        {
            // Fallback to kick13 if amigaos not found
            vbccLibPath = Path.Combine(_vbccPath, "targets", "m68k-kick13", "lib");
        }

        if (Directory.Exists(vbccLibPath))
        {
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
        }

        // Add standard Amiga libraries
        var libPath = Path.Combine(_ndkPath, "lib");
        if (Directory.Exists(libPath))
        {
            args.Add($"-L{libPath}");
            args.Add("-lamiga");  // Link with amiga.lib (provides _DOSBase, etc.)
        }

        // Don't print here - caller shows final success message
        return await RunTool(vlinkPath, args);
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

        // Assemble (with FPU support if fat binary or FPU mode)
        if (!await Assemble(asmFile, objFile, assemblyCpu, enableFpu))
        {
            Console.WriteLine("Assembly failed");
            return false;
        }

        var objFiles = new List<string> { objFile };

        // Detect which library stubs are needed and assemble them
        var requiredLibraries = DetectRequiredLibraries(asmSource);
        var compilerDir = AppContext.BaseDirectory;

        foreach (var library in requiredLibraries)
        {
            var stubsSource = Path.Combine(compilerDir, "stubs", $"{library}_stubs.s");

            if (File.Exists(stubsSource))
            {
                var stubsObj = Path.Combine(outputPath, $"{library}_stubs.o");

                if (!await Assemble(stubsSource, stubsObj, assemblyCpu, false))
                {
                    Console.WriteLine($"{library} stubs assembly failed");
                    return false;
                }

                objFiles.Add(stubsObj);

                // If using DOS library, also include dos_init.o for automatic DOSBase initialization
                if (library == "dos")
                {
                    var dosInitSource = Path.Combine(compilerDir, "stubs", "dos_init.s");
                    if (File.Exists(dosInitSource))
                    {
                        var dosInitObj = Path.Combine(outputPath, "dos_init.o");

                        if (!await Assemble(dosInitSource, dosInitObj, assemblyCpu, false))
                        {
                            Console.WriteLine("dos_init assembly failed");
                            return false;
                        }

                        objFiles.Add(dosInitObj);
                    }
                }
            }
            else
            {
                Console.WriteLine($"Warning: {library} functions used but stubs not found at {stubsSource}");
            }
        }

        // Link with appropriate math library and startup code
        if (!await Link(objFiles.ToArray(), exeFile, fpuMode, includeStartup: true))
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
        var linkedAssemblies = new List<string> { mainAsmSource };

        // Add only the dependency assemblies that were actually linked
        foreach (var (modulePath, asmSource) in dependencyAssemblies)
        {
            var exportedSymbols = ExtractExportedSymbols(asmSource);
            if (exportedSymbols.Any(sym => referencedSymbols.Contains(sym)))
            {
                linkedAssemblies.Add(asmSource);
            }
        }

        var requiredLibraries = new HashSet<string>();
        foreach (var asm in linkedAssemblies)
        {
            var libs = DetectRequiredLibraries(asm);
            foreach (var lib in libs)
            {
                requiredLibraries.Add(lib);
            }
        }

        // Assemble library stubs (only once per library)
        var compilerDir = AppContext.BaseDirectory;
        foreach (var library in requiredLibraries)
        {
            var stubsSource = Path.Combine(compilerDir, "stubs", $"{library}_stubs.s");

            if (File.Exists(stubsSource))
            {
                var stubsObj = Path.Combine(outputPath, $"{library}_stubs.o");

                if (!await Assemble(stubsSource, stubsObj, assemblyCpu, false))
                {
                    Console.WriteLine($"{library} stubs assembly failed");
                    return false;
                }

                objFiles.Add(stubsObj);

                // If using DOS library, also include dos_init.o for automatic DOSBase initialization
                if (library == "dos")
                {
                    var dosInitSource = Path.Combine(compilerDir, "stubs", "dos_init.s");
                    if (File.Exists(dosInitSource))
                    {
                        var dosInitObj = Path.Combine(outputPath, "dos_init.o");

                        if (!await Assemble(dosInitSource, dosInitObj, assemblyCpu, false))
                        {
                            Console.WriteLine("dos_init assembly failed");
                            return false;
                        }

                        objFiles.Add(dosInitObj);
                    }
                }
            }
            else
            {
                Console.WriteLine($"Warning: {library} functions used but stubs not found at {stubsSource}");
            }
        }

        // Link all object files
        var exeFile = Path.Combine(outputPath, baseName);
        Console.WriteLine($"\nLinking {objFiles.Count} object file{(objFiles.Count > 1 ? "s" : "")}...");
        if (!await Link(objFiles.ToArray(), exeFile, fpuMode, includeStartup: true))
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
        var libraries = new HashSet<string>();

        // Map of library names to their available stub files
        var libraryFunctions = new Dictionary<string, string[]>
        {
            ["dos"] = new[] { "_Output", "_Input", "_Error", "_Write", "_Read", "_Printf", "_Open", "_Close", "_CurrentDir", "_CreateDir", "_DeleteFile", "_Execute", "_Delay", "_IoErr", "_Seek", "_Rename", "_Lock", "_UnLock" },
            ["exec"] = new[] { "_AllocMem", "_FreeMem", "_OpenLibrary", "_CloseLibrary", "_FindTask", "_Wait", "_Signal", "_AllocSignal", "_FreeSignal" },
            ["intuition"] = new[] { "_OpenWindow", "_CloseWindow", "_OpenScreen", "_CloseScreen", "_DisplayAlert", "_AutoRequest" },
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
            ["mathieeesingbas"] = new[] { "_IEEESPAdd", "_IEEESPSub", "_IEEESPMul", "_IEEESPDiv" }
        };

        // Check each library's functions
        foreach (var (library, functions) in libraryFunctions)
        {
            if (functions.Any(func => asmSource.Contains($"xref\t{func}")))
            {
                libraries.Add(library);
            }
        }

        return libraries;
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
        return arg.Contains(' ') ? $"\"{arg}\"" : arg;
    }
}
