using System.Text;

namespace Novus.Tools;

/// <summary>
/// Tool to generate Amiga library stubs and Novus modules from NDK .fd files
/// </summary>
public class NdkStubGenerator
{
    private readonly string _ndkPath;
    private readonly string _outputPath;

    public NdkStubGenerator(string ndkPath, string outputPath)
    {
        _ndkPath = ndkPath;
        _outputPath = outputPath;
    }

    /// <summary>
    /// Generate complete FFI bindings for a specific library (functions, structs, constants)
    /// </summary>
    public void GenerateLibraryStubs(string libraryName)
    {
        var fdPath = Path.Combine(_ndkPath, "Include", "fd", $"{libraryName}_lib.fd");

        if (!File.Exists(fdPath))
        {
            Console.WriteLine($"Error: .fd file not found: {fdPath}");
            return;
        }

        Console.WriteLine($"Generating FFI bindings for {libraryName}...");

        var (library, functions) = FdParser.ParseFdFile(fdPath);

        // Generate assembly stubs
        var stubsDir = Path.Combine(_outputPath, "stubs");
        Directory.CreateDirectory(stubsDir);

        var asmStubs = FdParser.GenerateAssemblyStubs(library, functions);
        var asmPath = Path.Combine(stubsDir, $"{libraryName}_stubs.s");
        File.WriteAllText(asmPath, asmStubs);
        Console.WriteLine($"  Generated assembly stubs: {asmPath}");

        // Parse structs and constants from headers
        var structs = new List<NdkHeaderParser.StructDefinition>();
        var constants = new List<NdkHeaderParser.ConstantDefinition>();

        // Only parse headers for THIS library (no cross-library pollution)
        var headerDir = Path.Combine(_ndkPath, "Include", "include_h", libraryName);

        if (Directory.Exists(headerDir))
        {
            var headers = Directory.GetFiles(headerDir, "*.h");
            foreach (var header in headers)
            {
                try
                {
                    // Parse structs
                    var parsedStructs = NdkHeaderParser.ParseStructs(header);
                    structs.AddRange(parsedStructs);

                    // Parse constants
                    var parsedConstants = NdkHeaderParser.ParseConstants(header, null);
                    constants.AddRange(parsedConstants);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: Failed to parse {Path.GetFileName(header)}: {ex.Message}");
                }
            }
        }

        // Deduplicate structs by name (keep first occurrence)
        var uniqueStructs = structs
            .GroupBy(s => s.Name)
            .Select(g => g.First())
            .ToList();
        structs = uniqueStructs;

        // Deduplicate constants by name (keep first occurrence)
        var uniqueConstants = constants
            .GroupBy(c => c.Name)
            .Select(g => g.First())
            .ToList();
        constants = uniqueConstants;

        // Generate Novus FFI module with functions, structs, and constants
        var modulesDir = Path.Combine(_outputPath, "std", "ffi");
        Directory.CreateDirectory(modulesDir);

        var novusModule = GenerateCompleteNovusModule(library, functions, structs, constants);
        var modulePath = Path.Combine(modulesDir, $"{libraryName}.novus");
        File.WriteAllText(modulePath, novusModule);
        Console.WriteLine($"  Generated Novus module: {modulePath}");

        Console.WriteLine($"  Functions: {functions.Count(f => !f.IsPrivate)} public");
        Console.WriteLine($"  Structs: {structs.Count}");
        Console.WriteLine($"  Constants: {constants.Count}");
    }

    /// <summary>
    /// Generate complete Novus module with functions, structs, and constants
    /// </summary>
    private string GenerateCompleteNovusModule(
        FdParser.LibraryInfo library,
        List<FdParser.FunctionDescriptor> functions,
        List<NdkHeaderParser.StructDefinition> structs,
        List<NdkHeaderParser.ConstantDefinition> constants)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"// {library.Name}.library FFI Bindings");
        sb.AppendLine($"// Raw 1:1 mappings to {library.Name}.library");
        sb.AppendLine($"//");
        sb.AppendLine($"// Auto-generated from NDK 3.9");
        sb.AppendLine($"// - Functions from {library.Name}_lib.fd");
        sb.AppendLine($"// - Structs from include/{library.Name}/*.h");
        sb.AppendLine($"// - Constants from include/{library.Name}/*.h");
        sb.AppendLine();

        // Generate constants
        if (constants.Count > 0)
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Constants");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();
            sb.Append(NdkHeaderParser.GenerateNovusConstants(constants));
            sb.AppendLine();
        }

        // Generate structs
        if (structs.Count > 0)
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Structs");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();
            foreach (var structDef in structs)
            {
                sb.Append(NdkHeaderParser.GenerateNovusStruct(structDef));
                sb.AppendLine();
            }
        }

        // Generate function declarations
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Functions");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        foreach (var func in functions.Where(f => !f.IsPrivate))
        {
            // Map Amiga types to Novus types (basic mapping for now)
            var novusParams = func.Parameters.Select(p => $"{p}: i32").ToList();

            sb.Append($"extern fn {func.Name}(");
            sb.Append(string.Join(", ", novusParams));
            sb.AppendLine(") -> i32");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate stubs for all common Amiga libraries
    /// </summary>
    public void GenerateCommonLibraries()
    {
        var commonLibraries = new[]
        {
            "dos",          // DOS library (file I/O, processes)
            "exec",         // Exec library (memory, tasks, IPC)
            "intuition",    // Intuition library (GUI)
            "graphics",     // Graphics library
            "layers",       // Layers library
            "diskfont",     // Disk font library
            "icon",         // Icon library
            "gadtools",     // Gadtools library
            "utility",      // Utility library
            "commodities",  // Commodities library
            "keymap",       // Keymap library
            "locale",       // Locale library
            "mathieeedoubbas",  // IEEE math libraries
            "mathieeesingbas",
            "expansion",    // Expansion library
            "timer",        // Timer device
            "iffparse"      // IFF parser
        };

        Console.WriteLine("Generating stubs for common Amiga libraries...\n");

        foreach (var lib in commonLibraries)
        {
            try
            {
                GenerateLibraryStubs(lib);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error generating {lib}: {ex.Message}");
            }
        }

        Console.WriteLine("\nGeneration complete!");
    }

    /// <summary>
    /// List all available libraries in the NDK
    /// </summary>
    public void ListAvailableLibraries()
    {
        var fdDir = Path.Combine(_ndkPath, "Include", "fd");

        if (!Directory.Exists(fdDir))
        {
            Console.WriteLine($"Error: FD directory not found: {fdDir}");
            return;
        }

        var fdFiles = Directory.GetFiles(fdDir, "*_lib.fd")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name!.Replace("_lib", ""))
            .OrderBy(name => name)
            .ToList();

        Console.WriteLine($"Available libraries in NDK ({fdFiles.Count} total):\n");

        foreach (var lib in fdFiles)
        {
            Console.WriteLine($"  {lib}");
        }
    }
}
