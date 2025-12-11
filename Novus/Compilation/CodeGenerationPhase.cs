using System.Security.Cryptography;
using Novus.Codegen;
using Novus.IR;

namespace Novus.Compilation;

/// <summary>
/// Phase 2: Generate C code files from IR modules.
/// Creates shared types header and per-function C files.
/// </summary>
public class CodeGenerationPhase : ICompilationPhase
{
    public string Name => "C Code Generation";

    public async Task<bool> ExecuteAsync(CompilationContext context)
    {
        if (context.MainModuleIR == null)
            return false;

        Console.WriteLine("Generating C code...");

        // Build type registry from all modules
        var typeRegistry = new TypeRegistry();
        typeRegistry.RegisterModule(context.MainModuleIR.IrModule);
        foreach (var moduleIR in context.AllModulesIR.Values)
        {
            typeRegistry.RegisterModule(moduleIR.IrModule);
        }

        // Collect all functions for the shared header (including closure functions)
        var allFunctionsForHeader = context.MainModuleIR.IrModule.Functions.ToList();
        foreach (var moduleIR in context.AllModulesIR.Values)
        {
            allFunctionsForHeader.AddRange(moduleIR.IrModule.Functions);
        }

        // Generate shared types header
        var sharedTypesHeader = CCodeGenerator.GenerateSharedTypesHeader(typeRegistry, allFunctionsForHeader);

        // Compute hash of types header
        context.TypesHeaderHash = ComputeStringHash(sharedTypesHeader);

        // Write shared types header
        context.TypesHeaderPath = Path.Combine(context.OutputDir, "novus_types.h");
        await File.WriteAllTextAsync(context.TypesHeaderPath, sharedTypesHeader);

        // Generate main module functions
        await GenerateModuleFunctions(
            context,
            context.MainModuleIR,
            context.BaseName,
            isMain: true);

        // For libraries, generate A6 wrapper assembly and interface files
        if (context.IsLibrary || context.IsDevice)
        {
            await GenerateLibraryArtifacts(context);
        }

        // Track monomorphized functions across all modules
        var generatedMonomorphizedFunctions = new Dictionary<string, (string moduleName, IrFunction function, CCodeGenerator codegen)>();

        // Generate library module functions
        foreach (var (modulePath, moduleIR) in context.AllModulesIR)
        {
            await GenerateModuleFunctions(
                context,
                moduleIR,
                moduleIR.ModuleName,
                isMain: false,
                modulePath,
                generatedMonomorphizedFunctions);
        }

        // Always generate debug_symbols.c
        await GenerateDebugSymbols(context);

        // Add runtime files
        AddRuntimeFiles(context);

        return true;
    }

    private async Task GenerateModuleFunctions(
        CompilationContext context,
        ModuleIR moduleIR,
        string moduleName,
        bool isMain,
        string? modulePath = null,
        Dictionary<string, (string moduleName, IrFunction function, CCodeGenerator codegen)>? generatedMonomorphized = null)
    {
        var functions = moduleIR.IrModule.Functions
            .Where(f => !f.IsExtern && f.BasicBlocks.Count > 0)
            .ToList();

        if (functions.Count == 0)
            return;

        var codegen = new CCodeGenerator(
            moduleIR.IrModule,
            moduleIR.StringLiterals,
            context.Options.Cpu,
            context.Options.Fpu,
            context.Options.BuildMode,
            safetyLevel: context.SafetyLevel,
            explicitEntryPoints: null,
            useSharedTypesHeader: true,
            projectVersion: context.Options.PackageVersion);

        context.AllCodeGenerators.Add(codegen);

        // Filter out functions with unresolved types
        var generableFunctions = functions
            .Where(f =>
            {
                bool hasUnresolved = codegen.HasUnresolvedTypes(f);
                if (hasUnresolved && context.Options.Verbose)
                {
                    Console.WriteLine($"    [FILTERED OUT] {f.Name} has unresolved types");
                }
                return !hasUnresolved;
            })
            .ToList();

        foreach (var function in generableFunctions)
        {
            // Check if this is a monomorphized function
            if (generatedMonomorphized != null)
            {
                bool isMonomorphized = codegen.IsMonomorphizedFunction(function);
                var mangledName = codegen.MangleName(function);

                if (isMonomorphized)
                {
                    if (generatedMonomorphized.ContainsKey(mangledName))
                    {
                        if (context.Options.Verbose)
                        {
                            Console.WriteLine($"    [SKIPPED DUPLICATE] {mangledName} (already in {generatedMonomorphized[mangledName].moduleName})");
                        }
                        continue;
                    }
                    generatedMonomorphized[mangledName] = (moduleName, function, codegen);
                }
            }

            var functionCCode = codegen.GenerateFunctionFile(function);
            var sanitizedFunctionName = function.Name
                .Replace("::", "_")
                .Replace("()", "unit")  // Handle unit type before removing parens
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace(",", "_")
                .Replace(" ", "")
                .Replace("&", "ref_")
                .Replace("*", "ptr_")
                .Replace("(", "")       // Remove any remaining parens
                .Replace(")", "");
            var functionCFile = Path.Combine(context.OutputDir, $"{moduleName}_{sanitizedFunctionName}.c");
            await File.WriteAllTextAsync(functionCFile, functionCCode);
            context.CFiles.Add(functionCFile);
        }

        // Generate statics file if module has static variables
        var staticsCCode = codegen.GenerateStaticsFile();
        if (!string.IsNullOrEmpty(staticsCCode))
        {
            var staticsCFile = Path.Combine(context.OutputDir, $"{moduleName}_statics.c");
            await File.WriteAllTextAsync(staticsCFile, staticsCCode);
            context.CFiles.Add(staticsCFile);
        }

        if (isMain)
        {
            // Generate exports header if there are any exported functions
            var exportsHeader = codegen.GenerateHeader();
            var exportedFunctions = moduleIR.IrModule.Functions
                .Where(f => f.IsExported && !f.IsExtern)
                .ToList();

            if (exportedFunctions.Count > 0)
            {
                var exportsHeaderPath = Path.Combine(context.OutputDir, $"{context.BaseName}_exports.h");
                await File.WriteAllTextAsync(exportsHeaderPath, exportsHeader);
                Console.WriteLine($"  → {context.BaseName}_exports.h ({exportedFunctions.Count} exported function{(exportedFunctions.Count > 1 ? "s" : "")})");
            }

            Console.WriteLine($"  → {context.BaseName} ({functions.Count} function{(functions.Count > 1 ? "s" : "")})");
        }
        else
        {
            var isStdModule = modulePath?.Contains("/std/") ?? false;
            var displayName = isStdModule ? $"std::{moduleName}" : moduleName;
            Console.WriteLine($"  → {displayName} ({functions.Count} function{(functions.Count > 1 ? "s" : "")})");
        }
    }

    private async Task GenerateLibraryArtifacts(CompilationContext context)
    {
        if (context.MainModuleIR == null)
            return;

        var libraryGen = new LibraryGenerator(context.MainModuleIR.IrModule);
        if (!libraryGen.IsLibrary)
            return;

        // Generate A6 wrappers
        var wrapperAsm = libraryGen.GenerateA6Wrappers();
        var wrapperAsmFile = Path.Combine(context.OutputDir, $"{context.BaseName}_wrappers.s");
        await File.WriteAllTextAsync(wrapperAsmFile, wrapperAsm);
        Console.WriteLine($"  → {Path.GetFileName(wrapperAsmFile)} (A6 wrappers)");

        // Generate C header
        var cHeader = libraryGen.GenerateCHeader();
        var headerFile = Path.Combine(context.OutputDir, $"{context.BaseName}.h");
        await File.WriteAllTextAsync(headerFile, cHeader);
        Console.WriteLine($"  → {Path.GetFileName(headerFile)} (C header)");

        // Generate Novus FFI binding
        var novusFfi = libraryGen.GenerateNovusFFI();
        var ffiFile = Path.Combine(context.OutputDir, $"{context.BaseName}.novus");
        await File.WriteAllTextAsync(ffiFile, novusFfi);
        Console.WriteLine($"  → {Path.GetFileName(ffiFile)} (Novus FFI)");

        // Generate FD file for VBCC auto-library support
        var fdFile = libraryGen.GenerateFDFile();
        var fdFilePath = Path.Combine(context.OutputDir, $"{context.BaseName}_lib.fd");
        await File.WriteAllTextAsync(fdFilePath, fdFile);
        Console.WriteLine($"  → {Path.GetFileName(fdFilePath)} (FD file)");

        // Generate library stub for auto-open/close
        var stubAsm = libraryGen.GenerateLibraryStub();
        var stubAsmFile = Path.Combine(context.OutputDir, $"{context.BaseName}_lib.s");
        await File.WriteAllTextAsync(stubAsmFile, stubAsm);
        Console.WriteLine($"  → {Path.GetFileName(stubAsmFile)} (library stub)");

        // Generate client call stubs
        var callStubAsm = libraryGen.GenerateClientCallStubs();
        var callStubFile = Path.Combine(context.OutputDir, $"{context.BaseName}_calls.s");
        await File.WriteAllTextAsync(callStubFile, callStubAsm);
        Console.WriteLine($"  → {Path.GetFileName(callStubFile)} (client call stubs)");

        // Generate default lifecycle functions
        var lifecycleCCode = libraryGen.GenerateDefaultLifecycleFunctions();
        if (!string.IsNullOrEmpty(lifecycleCCode))
        {
            var lifecycleCFile = Path.Combine(context.OutputDir, $"{context.BaseName}_lifecycle.c");
            await File.WriteAllTextAsync(lifecycleCFile, lifecycleCCode);
            context.CFiles.Add(lifecycleCFile);
            Console.WriteLine($"  → {Path.GetFileName(lifecycleCFile)} (lifecycle functions)");
        }
    }

    private async Task GenerateDebugSymbols(CompilationContext context)
    {
        if (context.MainModuleIR == null)
            return;

        string debugSymbolsCode;
        if (context.SafetyLevel >= SafetyLevel.Paranoid)
        {
            var allModules = new List<IrModule> { context.MainModuleIR.IrModule };
            allModules.AddRange(context.AllModulesIR.Values.Select(m => m.IrModule));

            // Collect statement-level debug markers
            var allDebugMarkers = new List<(string LabelName, string FileName, int Line, string FuncName)>();
            foreach (var codegen in context.AllCodeGenerators)
            {
                allDebugMarkers.AddRange(codegen.GetDebugLineMarkers());
            }

            debugSymbolsCode = CCodeGenerator.GenerateDebugSymbolsFile(allModules, allDebugMarkers);
            var markerCount = allDebugMarkers.Count;
            Console.WriteLine($"  → debug_symbols.c ({markerCount} statement marker{(markerCount != 1 ? "s" : "")} for precise line info)");
        }
        else
        {
            // Generate stub for non-debug builds
            debugSymbolsCode = "// No debug symbols in release build\n" +
                               "void __novus_init_debug_symbols(void) { /* no-op */ }\n";
        }

        var debugSymbolsCFile = Path.Combine(context.OutputDir, "debug_symbols.c");
        await File.WriteAllTextAsync(debugSymbolsCFile, debugSymbolsCode);
        context.CFiles.Add(debugSymbolsCFile);
    }

    private void AddRuntimeFiles(CompilationContext context)
    {
        var runtimeDir = Path.Combine(context.CompilerDir, "runtime");
        var runtimeFiles = new[]
        {
            "runtime_alloc.c",     // Minimal: raw AllocMem/FreeMem wrappers (no deps)
            "runtime_core.c",      // Core: memset, memcpy, strlen, error display
            "runtime_errors.c",    // Assert, panic, bounds check, div check
            "runtime_hwdetect.c",  // CPU, FPU, chipset detection
            "runtime_fmt.c",       // Integer to string conversions
            "runtime_semaphore.c", // Semaphore wrappers
            "runtime_mmu.c",       // MMU detection and null page protection
            "runtime_memtrack.c",  // Memory tracking for debugging
        };

        foreach (var runtimeFile in runtimeFiles)
        {
            var runtimeCFile = Path.Combine(runtimeDir, runtimeFile);
            if (File.Exists(runtimeCFile))
            {
                context.CFiles.Add(runtimeCFile);
            }
        }

        Console.WriteLine($"  → {runtimeFiles.Length} runtime modules (split for DCE)");
    }

    private static string ComputeStringHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
