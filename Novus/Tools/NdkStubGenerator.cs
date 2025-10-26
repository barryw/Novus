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
    /// Generate stubs for a specific library
    /// </summary>
    public void GenerateLibraryStubs(string libraryName)
    {
        var fdPath = Path.Combine(_ndkPath, "Include", "fd", $"{libraryName}_lib.fd");

        if (!File.Exists(fdPath))
        {
            Console.WriteLine($"Error: .fd file not found: {fdPath}");
            return;
        }

        Console.WriteLine($"Generating stubs for {libraryName}...");

        var (library, functions) = FdParser.ParseFdFile(fdPath);

        // Generate assembly stubs
        var stubsDir = Path.Combine(_outputPath, "stubs");
        Directory.CreateDirectory(stubsDir);

        var asmStubs = FdParser.GenerateAssemblyStubs(library, functions);
        var asmPath = Path.Combine(stubsDir, $"{libraryName}_stubs.s");
        File.WriteAllText(asmPath, asmStubs);
        Console.WriteLine($"  Generated: {asmPath}");

        // Generate Novus module
        var modulesDir = Path.Combine(_outputPath, "stdlib");
        Directory.CreateDirectory(modulesDir);

        var novusModule = FdParser.GenerateNovusModule(library, functions);
        var modulePath = Path.Combine(modulesDir, $"{libraryName}.novus");
        File.WriteAllText(modulePath, novusModule);
        Console.WriteLine($"  Generated: {modulePath}");

        Console.WriteLine($"  Functions: {functions.Count(f => !f.IsPrivate)} public");
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
