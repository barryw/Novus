using System.Text;
using System.Text.RegularExpressions;

namespace Novus.Tools;

/// <summary>
/// Parses Amiga NDK .fd (Function Descriptor) files and generates assembly stubs
/// </summary>
public class FdParser
{
    public record LibraryInfo(string Name, string BaseVariable, int InitialBias);

    public record FunctionDescriptor(
        string Name,
        List<string> Parameters,
        List<string> Registers,
        int Offset,
        bool IsPrivate);

    /// <summary>
    /// Parse an .fd file and extract library information
    /// </summary>
    public static (LibraryInfo Library, List<FunctionDescriptor> Functions) ParseFdFile(string fdPath)
    {
        var lines = File.ReadAllLines(fdPath);

        string? baseVariable = null;
        int currentBias = 30; // Default starting bias
        int currentOffset = 0;
        bool isPublic = true;
        var functions = new List<FunctionDescriptor>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("*"))
                continue;

            // Parse directives
            if (trimmed.StartsWith("##"))
            {
                if (trimmed.StartsWith("##base "))
                {
                    baseVariable = trimmed.Substring(7).Trim();
                }
                else if (trimmed.StartsWith("##bias "))
                {
                    if (int.TryParse(trimmed.Substring(7).Trim(), out int bias))
                    {
                        currentBias = bias;
                        currentOffset = -bias;
                    }
                }
                else if (trimmed == "##public")
                {
                    isPublic = true;
                }
                else if (trimmed == "##private")
                {
                    isPublic = false;
                }
                continue;
            }

            // Parse function declarations
            // Format: FunctionName(param1,param2)(d1/d2) or (param1,param2,param3)(a0,d0/d1)
            var match = Regex.Match(trimmed, @"^(\w+)\((.*?)\)\((.*?)\)");
            if (match.Success)
            {
                var functionName = match.Groups[1].Value;
                var paramsStr = match.Groups[2].Value;
                var registersStr = match.Groups[3].Value;

                var parameters = string.IsNullOrWhiteSpace(paramsStr)
                    ? new List<string>()
                    : paramsStr.Split(',').Select(p => p.Trim()).ToList();

                // Parse register specifications - can be like "a0,d0/d1/a1,d2"
                // Split by / first, then expand comma-separated groups
                var registers = new List<string>();
                if (!string.IsNullOrWhiteSpace(registersStr))
                {
                    var regGroups = registersStr.Split('/');
                    foreach (var group in regGroups)
                    {
                        var regs = group.Split(',').Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r));
                        registers.AddRange(regs);
                    }
                }

                // Ensure we have the same number of parameters and registers
                if (parameters.Count != registers.Count && registers.Count > 0)
                {
                    // Some .fd files have mismatched counts - just use what we have
                    // Console.WriteLine($"Warning: {functionName} has {parameters.Count} params but {registers.Count} registers");
                }

                functions.Add(new FunctionDescriptor(
                    functionName,
                    parameters,
                    registers,
                    currentOffset,
                    !isPublic));

                currentOffset -= 6; // Each function slot is 6 bytes
            }
        }

        if (baseVariable == null)
            throw new InvalidDataException($"No ##base directive found in {fdPath}");

        var libraryName = Path.GetFileNameWithoutExtension(fdPath).Replace("_lib", "");
        var library = new LibraryInfo(libraryName, baseVariable, currentBias);

        return (library, functions);
    }

    /// <summary>
    /// Generate assembly stubs for a library
    /// </summary>
    public static string GenerateAssemblyStubs(LibraryInfo library, List<FunctionDescriptor> functions)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"; {library.Name} library stubs for Novus");
        sb.AppendLine($"; Auto-generated from {library.Name}_lib.fd");
        sb.AppendLine();
        sb.AppendLine($"\txref\t{library.BaseVariable}\t; Provided by startup.o + -lamiga");
        sb.AppendLine();
        sb.AppendLine("\tsection\ttext,code");
        sb.AppendLine();

        foreach (var func in functions.Where(f => !f.IsPrivate))
        {
            GenerateFunctionStub(sb, library, func);
        }

        return sb.ToString();
    }

    private static void GenerateFunctionStub(StringBuilder sb, LibraryInfo library, FunctionDescriptor func)
    {
        sb.AppendLine($"; {func.Name}({string.Join(", ", func.Parameters)})");
        sb.AppendLine($"\txdef\t_{func.Name}");
        sb.AppendLine($"_{func.Name}:");

        // Determine which registers we need to save
        var usedDataRegs = func.Registers.Where(r => r.StartsWith('d')).ToList();
        var usedAddrRegs = func.Registers.Where(r => r.StartsWith('a') && r != "a6").ToList();

        // Build register save list
        var saveRegs = new List<string>();
        if (usedDataRegs.Any())
        {
            var minD = usedDataRegs.Min(r => int.Parse(r.Substring(1)));
            var maxD = usedDataRegs.Max(r => int.Parse(r.Substring(1)));
            if (minD == maxD)
                saveRegs.Add($"d{minD}");
            else
                saveRegs.Add($"d{minD}-d{maxD}");
        }
        if (usedAddrRegs.Any())
        {
            var minA = usedAddrRegs.Min(r => int.Parse(r.Substring(1)));
            var maxA = usedAddrRegs.Max(r => int.Parse(r.Substring(1)));
            if (minA == maxA)
                saveRegs.Add($"a{minA}");
            else
                saveRegs.Add($"a{minA}-a{maxA}");
        }
        saveRegs.Add("a6"); // Always save a6

        // Save registers
        if (saveRegs.Count > 0)
        {
            sb.AppendLine($"\tmovem.l\t{string.Join("/", saveRegs)},-(sp)");
        }

        // Load parameters from stack into registers
        // Stack layout: saved regs, then return address (4 bytes), then params
        int stackOffset = saveRegs.Count * 4 + 4; // saved regs + return address

        for (int i = 0; i < func.Registers.Count; i++)
        {
            var reg = func.Registers[i];
            // Use parameter name if available, otherwise just note it's a register load
            var comment = i < func.Parameters.Count ? func.Parameters[i] : $"reg{i}";
            sb.AppendLine($"\tmove.l\t{stackOffset}(sp),{reg}\t; {comment}");
            stackOffset += 4;
        }

        // Load library base into a6
        sb.AppendLine($"\tmove.l\t{library.BaseVariable},a6");

        // Call library function
        sb.AppendLine($"\tjsr\t{func.Offset}(a6)\t; {func.Name}()");

        // Restore registers
        if (saveRegs.Count > 0)
        {
            sb.AppendLine($"\tmovem.l\t(sp)+,{string.Join("/", saveRegs)}");
        }

        sb.AppendLine("\trts");
        sb.AppendLine();
    }

    /// <summary>
    /// Generate Novus module declaration for a library
    /// </summary>
    public static string GenerateNovusModule(LibraryInfo library, List<FunctionDescriptor> functions)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"// {library.Name} - Amiga {library.Name}.library bindings");
        sb.AppendLine($"// Auto-generated from {library.Name}_lib.fd");
        sb.AppendLine();
        sb.AppendLine($"module {library.Name};");
        sb.AppendLine();

        foreach (var func in functions.Where(f => !f.IsPrivate))
        {
            // Map Amiga types to Novus types (basic mapping for now)
            var novusParams = func.Parameters.Select(p => $"{p}: i32").ToList();

            sb.Append($"extern fn {func.Name}(");
            sb.Append(string.Join(", ", novusParams));
            sb.AppendLine(") -> i32;");
        }

        return sb.ToString();
    }
}
