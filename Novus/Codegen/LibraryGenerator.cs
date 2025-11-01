using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Codegen;

/// <summary>
/// Generates AmigaOS shared library boilerplate from @library attributes.
///
/// When a struct has @library attribute, this generates:
/// - ROMTag structure (for exec.library scanning)
/// - Function vector table (negative offsets from library base)
/// - A6 calling convention wrappers (AmigaOS → C calling convention)
/// - Default lifecycle functions (Open/Close/Expunge/Reserved)
///
/// This eliminates the need for manual assembly files.
/// </summary>
public class LibraryGenerator
{
    private readonly IrModule _module;
    private readonly IrStructType? _libraryStruct;
    private readonly AttributeInfo? _libraryAttribute;
    private readonly List<LibraryFunction> _libraryFunctions;

    /// <summary>
    /// Represents a library function with its metadata.
    /// </summary>
    private class LibraryFunction
    {
        public string Name { get; set; } = "";
        public IrFunction Function { get; set; } = null!;
        public int VectorOffset { get; set; }
        public bool IsLifecycleFunction { get; set; }
    }

    public LibraryGenerator(IrModule module)
    {
        _module = module;
        _libraryFunctions = new List<LibraryFunction>();

        // Find the struct with @library attribute
        foreach (var monotype in module.MonomorphizedTypes.Values)
        {
            if (monotype.ConcreteType is IrStructType structType &&
                structType.Attributes?.Has(KnownAttributes.Library) == true)
            {
                _libraryStruct = structType;
                _libraryAttribute = structType.Attributes.Get(KnownAttributes.Library);
                break;
            }
        }

        if (_libraryStruct != null)
        {
            AnalyzeLibraryFunctions();
        }
    }

    /// <summary>
    /// Returns true if this module is a library (has @library attribute).
    /// </summary>
    public bool IsLibrary => _libraryStruct != null;

    /// <summary>
    /// Get the library name from the @library attribute.
    /// </summary>
    public string GetLibraryName()
    {
        if (_libraryAttribute == null)
            return "";

        var name = _libraryAttribute.GetString("name");
        if (name == null)
            throw new InvalidOperationException("@library attribute requires 'name' parameter");

        if (!name.EndsWith(".library"))
            throw new InvalidOperationException("Library name must end with '.library'");

        return name;
    }

    /// <summary>
    /// Get the library version from the @library attribute.
    /// </summary>
    public int GetLibraryVersion()
    {
        if (_libraryAttribute == null)
            return 0;

        var version = _libraryAttribute.GetInt("version");
        if (version == null)
            throw new InvalidOperationException("@library attribute requires 'version' parameter");

        return version.Value;
    }

    /// <summary>
    /// Get the library revision from the @library attribute.
    /// </summary>
    public int GetLibraryRevision()
    {
        if (_libraryAttribute == null)
            return 0;

        var revision = _libraryAttribute.GetInt("revision");
        return revision ?? 0;
    }

    /// <summary>
    /// Analyze which functions belong to the library.
    /// </summary>
    private void AnalyzeLibraryFunctions()
    {
        if (_libraryStruct == null)
            return;

        // Find all public functions in impl block for the library struct
        var structName = _libraryStruct.StructName;

        // Vector offset -6: Open
        // Vector offset -12: Close
        // Vector offset -18: Expunge
        // Vector offset -24: Reserved
        // Vector offset -30: First user function
        // Vector offset -36: Second user function
        // etc.

        int nextOffset = -30; // User functions start at -30

        foreach (var function in _module.Functions)
        {
            // Skip extern functions
            if (function.IsExtern)
                continue;

            // Check if this function belongs to the library struct
            // Convention: functions are named "StructName_methodName" or just "methodName"
            // For now, include all public functions (we'll refine this)
            if (!function.IsPublic)
                continue;

            var libFunc = new LibraryFunction
            {
                Name = function.Name,
                Function = function
            };

            // Check if it's a lifecycle function by name
            var lowerName = function.Name.ToLower();
            if (lowerName.Contains("open") && !lowerName.Contains("library"))
            {
                libFunc.VectorOffset = -6;
                libFunc.IsLifecycleFunction = true;
            }
            else if (lowerName.Contains("close") && !lowerName.Contains("library"))
            {
                libFunc.VectorOffset = -12;
                libFunc.IsLifecycleFunction = true;
            }
            else if (lowerName.Contains("expunge"))
            {
                libFunc.VectorOffset = -18;
                libFunc.IsLifecycleFunction = true;
            }
            else if (lowerName.Contains("reserved"))
            {
                libFunc.VectorOffset = -24;
                libFunc.IsLifecycleFunction = true;
            }
            else
            {
                // User function - assign next offset
                libFunc.VectorOffset = nextOffset;
                libFunc.IsLifecycleFunction = false;
                nextOffset -= 6; // Next function
            }

            _libraryFunctions.Add(libFunc);
        }

        // Sort by vector offset (most negative first, which is standard AmigaOS order)
        _libraryFunctions.Sort((a, b) => a.VectorOffset.CompareTo(b.VectorOffset));
    }

    /// <summary>
    /// Generate the ROMTag structure in C.
    /// </summary>
    public string GenerateROMTag()
    {
        if (!IsLibrary)
            return "";

        var sb = new StringBuilder();
        var libName = GetLibraryName();
        var version = GetLibraryVersion();
        var revision = GetLibraryRevision();

        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// ROMTag (Resident Structure)");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// The ROMTag is scanned by exec.library when the library loads.");
        sb.AppendLine("// It identifies this file as an AmigaOS library and provides");
        sb.AppendLine("// initialization information.");
        sb.AppendLine();

        // Include necessary headers
        sb.AppendLine("#include <exec/types.h>");
        sb.AppendLine("#include <exec/resident.h>");
        sb.AppendLine("#include <exec/libraries.h>");
        sb.AppendLine();

        // Library name and ID string
        sb.AppendLine($"static const char LibName[] = \"{libName}\";");
        sb.AppendLine($"static const char LibIdString[] = \"{libName} {version}.{revision}\";");
        sb.AppendLine();

        // Forward declarations
        sb.AppendLine("// Forward declarations");
        sb.AppendLine("struct Library* LibInit(BPTR segList, struct Library *sysBase);");
        sb.AppendLine("struct Library* LibOpen(void);");
        sb.AppendLine("BPTR LibClose(void);");
        sb.AppendLine("BPTR LibExpunge(void);");
        sb.AppendLine("LONG LibReserved(void);");
        sb.AppendLine();

        // Function table
        sb.AppendLine("// Function vector table");
        sb.AppendLine("static const APTR FuncTable[] = {");
        sb.AppendLine("    (APTR)LibOpen,");
        sb.AppendLine("    (APTR)LibClose,");
        sb.AppendLine("    (APTR)LibExpunge,");
        sb.AppendLine("    (APTR)LibReserved,");

        foreach (var func in _libraryFunctions)
        {
            if (!func.IsLifecycleFunction)
            {
                sb.AppendLine($"    (APTR){func.Name},  // Offset {func.VectorOffset}");
            }
        }

        sb.AppendLine("    (APTR)-1");
        sb.AppendLine("};");
        sb.AppendLine();

        // ROMTag structure
        sb.AppendLine("// ROMTag structure");
        sb.AppendLine("const struct Resident RomTag = {");
        sb.AppendLine("    RTC_MATCHWORD,       // Magic word");
        sb.AppendLine("    &RomTag,             // Pointer to itself");
        sb.AppendLine("    &RomTag + 1,         // End marker");
        sb.AppendLine("    RTF_AUTOINIT,        // Flags");
        sb.AppendLine($"    {version},                 // Version");
        sb.AppendLine("    NT_LIBRARY,          // Type");
        sb.AppendLine("    0,                   // Priority");
        sb.AppendLine("    (char*)LibName,      // Name");
        sb.AppendLine("    (char*)LibIdString,  // ID string");
        sb.AppendLine("    (APTR)&InitTable     // Init table pointer");
        sb.AppendLine("};");
        sb.AppendLine();

        // AutoInit structure
        int libBaseSize = CalculateLibraryBaseSize();
        int negSize = _libraryFunctions.Count * 6;

        sb.AppendLine("// AutoInit structure");
        sb.AppendLine("static const ULONG InitTable[] = {");
        sb.AppendLine($"    sizeof(struct Library),  // Data size");
        sb.AppendLine("    (ULONG)FuncTable,         // Function table");
        sb.AppendLine("    0,                        // Data table");
        sb.AppendLine("    (ULONG)LibInit            // Init routine");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Calculate the size of the library base structure.
    /// </summary>
    private int CalculateLibraryBaseSize()
    {
        if (_libraryStruct == null)
            return 0;

        // sizeof(Library) + custom fields
        // Library header is typically 34 bytes on 68k
        int size = 34;

        foreach (var field in _libraryStruct.Fields)
        {
            size += GetFieldSize(field.Type);
        }

        return size;
    }

    /// <summary>
    /// Get the size of a field type in bytes.
    /// </summary>
    private int GetFieldSize(IrType type)
    {
        return type switch
        {
            IrIntType intType => intType.SizeInBytes,
            IrPointerType => 4, // 32-bit pointers on 68k
            IrBoolType => 1,
            _ => 4 // Default to 4 bytes for unknown types
        };
    }

    /// <summary>
    /// Generate default lifecycle functions if not provided by user.
    /// </summary>
    public string GenerateDefaultLifecycleFunctions()
    {
        if (!IsLibrary)
            return "";

        var sb = new StringBuilder();
        var libName = GetLibraryName();

        // Check which lifecycle functions are missing
        bool hasOpen = _libraryFunctions.Any(f => f.VectorOffset == -6);
        bool hasClose = _libraryFunctions.Any(f => f.VectorOffset == -12);
        bool hasExpunge = _libraryFunctions.Any(f => f.VectorOffset == -18);
        bool hasReserved = _libraryFunctions.Any(f => f.VectorOffset == -24);

        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Default Lifecycle Functions");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        if (!hasOpen)
        {
            sb.AppendLine("struct Library* LibOpen(void) {");
            sb.AppendLine("    struct Library* base;");
            sb.AppendLine("    __asm volatile (\"move.l %%a6,%0\" : \"=r\"(base));");
            sb.AppendLine("    base->lib_OpenCnt++;");
            sb.AppendLine("    base->lib_Flags &= ~LIBF_DELEXP;");
            sb.AppendLine("    return base;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (!hasClose)
        {
            sb.AppendLine("BPTR LibClose(void) {");
            sb.AppendLine("    struct Library* base;");
            sb.AppendLine("    __asm volatile (\"move.l %%a6,%0\" : \"=r\"(base));");
            sb.AppendLine("    base->lib_OpenCnt--;");
            sb.AppendLine("    if (base->lib_OpenCnt == 0 && (base->lib_Flags & LIBF_DELEXP)) {");
            sb.AppendLine("        return LibExpunge();");
            sb.AppendLine("    }");
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (!hasExpunge)
        {
            sb.AppendLine("BPTR LibExpunge(void) {");
            sb.AppendLine("    struct Library* base;");
            sb.AppendLine("    __asm volatile (\"move.l %%a6,%0\" : \"=r\"(base));");
            sb.AppendLine("    if (base->lib_OpenCnt > 0) {");
            sb.AppendLine("        base->lib_Flags |= LIBF_DELEXP;");
            sb.AppendLine("        return 0;");
            sb.AppendLine("    }");
            sb.AppendLine("    // TODO: Remove from library list");
            sb.AppendLine("    // TODO: Free library base");
            sb.AppendLine("    return 0;  // Return seglist");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (!hasReserved)
        {
            sb.AppendLine("LONG LibReserved(void) {");
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
