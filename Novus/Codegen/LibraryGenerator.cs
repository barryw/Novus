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
        public string CName { get; set; } = "";  // Mangled C name
        public IrFunction Function { get; set; } = null!;
        public int VectorOffset { get; set; }
        public bool IsLifecycleFunction { get; set; }
    }

    public LibraryGenerator(IrModule module)
    {
        _module = module;
        _libraryFunctions = new List<LibraryFunction>();

        // Find the struct with @library attribute
        foreach (var structType in module.Structs)
        {
            if (structType.Attributes?.Has(KnownAttributes.Library) == true)
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
    /// Mangle a Novus name to a C-compatible name (same as CCodeGenerator).
    /// </summary>
    private string MangleName(string name)
    {
        return name.Replace("::", "_");
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
                CName = MangleName(function.Name),
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
        sb.AppendLine("// Library Entry Point");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Libraries need a _start() function that returns -1 to discourage");
        sb.AppendLine("// running from the shell. The real entry is the ROMTag structure.");
        sb.AppendLine();
        sb.AppendLine("LONG _start(void) {");
        sb.AppendLine("    return -1;  // Cannot run from CLI");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// ROMTag (Resident Structure)");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// The ROMTag is scanned by exec.library when the library loads.");
        sb.AppendLine("// It identifies this file as an AmigaOS library and provides");
        sb.AppendLine("// initialization information.");
        sb.AppendLine();

        // Include resident header (libraries.h already included in library base struct)
        sb.AppendLine("#include <exec/resident.h>");
        sb.AppendLine();

        // Library name and ID string
        sb.AppendLine($"static const char LibName[] = \"{libName}\";");
        sb.AppendLine($"static const char LibIdString[] = \"{libName} {version}.{revision}\";");
        sb.AppendLine();

        // Forward declarations
        var structName = $"{_libraryStruct.StructName}Base";
        sb.AppendLine("// Forward declarations for C functions");
        sb.AppendLine("static const ULONG InitTable[];  // Defined below");
        sb.AppendLine($"struct Library* LibInit(BPTR segList, struct {structName}* base);");
        sb.AppendLine($"struct Library* LibOpen(struct {structName}* base);");
        sb.AppendLine($"BPTR LibClose(struct {structName}* base);");
        sb.AppendLine($"BPTR LibExpunge(struct {structName}* base);");
        sb.AppendLine("LONG LibReserved(void);");
        sb.AppendLine();

        // Forward declarations for A6 wrapper functions (defined in assembly)
        sb.AppendLine("// Forward declarations for A6 wrapper functions (in assembly)");
        sb.AppendLine("extern void* LibOpen_Wrapper(void);");
        sb.AppendLine("extern void* LibClose_Wrapper(void);");
        sb.AppendLine("extern void* LibExpunge_Wrapper(void);");
        sb.AppendLine("extern void* LibReserved_Wrapper(void);");
        foreach (var func in _libraryFunctions)
        {
            if (!func.IsLifecycleFunction)
            {
                sb.AppendLine($"extern void* {func.CName}_Wrapper(void);");
            }
        }
        sb.AppendLine();

        // Function table - use wrapper functions for AmigaOS calling convention
        sb.AppendLine("// Function vector table (uses A6 wrappers)");
        sb.AppendLine("static const APTR FuncTable[] = {");
        sb.AppendLine("    (APTR)LibOpen_Wrapper,");
        sb.AppendLine("    (APTR)LibClose_Wrapper,");
        sb.AppendLine("    (APTR)LibExpunge_Wrapper,");
        sb.AppendLine("    (APTR)LibReserved_Wrapper,");

        foreach (var func in _libraryFunctions)
        {
            if (!func.IsLifecycleFunction)
            {
                sb.AppendLine($"    (APTR){func.CName}_Wrapper,  // Offset {func.VectorOffset}");
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
        sb.AppendLine("// AutoInit structure");
        sb.AppendLine("static const ULONG InitTable[] = {");
        sb.AppendLine($"    sizeof(struct {structName}),  // Data size");
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
    /// Get the C type name for an IR type (simplified version from CCodeGenerator).
    /// </summary>
    private string GetCType(IrType type)
    {
        return type switch
        {
            IrIntType intType => intType.IsSigned
                ? $"int{intType.SizeInBytes * 8}_t"
                : $"uint{intType.SizeInBytes * 8}_t",
            IrBoolType => "bool",
            IrPointerType ptrType => $"{GetCType(ptrType.PointeeType)}*",
            _ => "void*"
        };
    }

    /// <summary>
    /// Generate the library base structure definition.
    /// Includes necessary headers for struct Library definition.
    /// </summary>
    public string GenerateLibraryBaseStruct()
    {
        if (!IsLibrary || _libraryStruct == null)
            return "";

        var sb = new StringBuilder();
        var libName = GetLibraryName();
        var structName = $"{_libraryStruct.StructName}Base";

        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Library Base Structure");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // Include headers needed for struct Library
        sb.AppendLine("#include <exec/types.h>");
        sb.AppendLine("#include <exec/nodes.h>");
        sb.AppendLine("#include <exec/libraries.h>");
        sb.AppendLine("#include <dos/dos.h>");  // For BPTR
        sb.AppendLine();

        sb.AppendLine($"// The library base includes the standard Library header plus custom fields.");
        sb.AppendLine($"struct {structName} {{");
        sb.AppendLine("    struct Library lib_Node;");

        // Add custom fields from the @library struct
        foreach (var field in _libraryStruct.Fields)
        {
            var cType = GetCType(field.Type);
            sb.AppendLine($"    {cType} {field.Name};");
        }

        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generate default lifecycle functions if not provided by user.
    /// </summary>
    public string GenerateDefaultLifecycleFunctions()
    {
        if (!IsLibrary || _libraryStruct == null)
            return "";

        var sb = new StringBuilder();
        var structName = $"{_libraryStruct.StructName}Base";

        // Check which lifecycle functions are missing
        bool hasOpen = _libraryFunctions.Any(f => f.VectorOffset == -6);
        bool hasClose = _libraryFunctions.Any(f => f.VectorOffset == -12);
        bool hasExpunge = _libraryFunctions.Any(f => f.VectorOffset == -18);
        bool hasReserved = _libraryFunctions.Any(f => f.VectorOffset == -24);

        // Use extern SysBase from runtime, or get it from location 4
        sb.AppendLine("// Get exec.library base from absolute location 4");
        sb.AppendLine("#define SysBase (*(struct ExecBase**)4)");
        sb.AppendLine();

        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Lifecycle Functions");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // LibInit - always generate this as it's required
        sb.AppendLine($"struct Library* LibInit(BPTR segList, struct {structName}* base) {{");
        sb.AppendLine("    // Initialize library base fields");
        sb.AppendLine("    // (SysBase is available via macro #define at location 4)");
        sb.AppendLine("    base->lib_Node.lib_Node.ln_Type = NT_LIBRARY;");
        sb.AppendLine("    base->lib_Node.lib_Node.ln_Pri = 0;");
        sb.AppendLine("    base->lib_Node.lib_Node.ln_Name = (char*)LibName;");
        sb.AppendLine("    base->lib_Node.lib_Flags = LIBF_CHANGED | LIBF_SUMUSED;");
        sb.AppendLine($"    base->lib_Node.lib_Version = {GetLibraryVersion()};");
        sb.AppendLine($"    base->lib_Node.lib_Revision = {GetLibraryRevision()};");
        sb.AppendLine("    base->lib_Node.lib_IdString = (char*)LibIdString;");
        sb.AppendLine();
        sb.AppendLine("    // Store segment list for later unloading");
        sb.AppendLine("    // TODO: Store segList in library base");
        sb.AppendLine();
        sb.AppendLine("    return &base->lib_Node;");
        sb.AppendLine("}");
        sb.AppendLine();

        if (!hasOpen)
        {
            sb.AppendLine($"struct Library* LibOpen(struct {structName}* base) {{");
            sb.AppendLine("    base->lib_Node.lib_OpenCnt++;");
            sb.AppendLine("    base->lib_Node.lib_Flags &= ~LIBF_DELEXP;");
            sb.AppendLine("    return &base->lib_Node;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (!hasClose)
        {
            sb.AppendLine($"BPTR LibClose(struct {structName}* base) {{");
            sb.AppendLine("    base->lib_Node.lib_OpenCnt--;");
            sb.AppendLine("    if (base->lib_Node.lib_OpenCnt == 0 && (base->lib_Node.lib_Flags & LIBF_DELEXP)) {");
            sb.AppendLine("        return LibExpunge(base);");
            sb.AppendLine("    }");
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        if (!hasExpunge)
        {
            sb.AppendLine($"BPTR LibExpunge(struct {structName}* base) {{");
            sb.AppendLine("    if (base->lib_Node.lib_OpenCnt > 0) {");
            sb.AppendLine("        base->lib_Node.lib_Flags |= LIBF_DELEXP;");
            sb.AppendLine("        return 0;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    // Remove library from system list");
            sb.AppendLine("    // Remove((struct Node*)base);");
            sb.AppendLine();
            sb.AppendLine("    // Free library base memory");
            sb.AppendLine($"    // FreeMem(base, sizeof(struct {structName}));");
            sb.AppendLine();
            sb.AppendLine("    // Return segment list for DOS to unload");
            sb.AppendLine("    return 0;  // TODO: return actual segList");
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

    /// <summary>
    /// Generate A6 wrapper assembly for AmigaOS calling convention.
    /// These wrappers translate from register-based (A6=LibBase) to C calling convention.
    /// </summary>
    public string GenerateA6Wrappers()
    {
        if (!IsLibrary || _libraryStruct == null)
            return "";

        var sb = new StringBuilder();
        var structName = $"{_libraryStruct.StructName}Base";

        sb.AppendLine("; A6 Wrapper Functions for AmigaOS Calling Convention");
        sb.AppendLine("; Generated by Novus compiler");
        sb.AppendLine(";");
        sb.AppendLine("; AmigaOS calls library functions with:");
        sb.AppendLine(";   A6 = Library base pointer");
        sb.AppendLine(";   Parameters in D0/D1/A0/A1/D2/D3/A2/A3...");
        sb.AppendLine(";");
        sb.AppendLine("; These wrappers translate to C convention:");
        sb.AppendLine(";   Parameters pushed on stack (right to left)");
        sb.AppendLine(";   A6 passed as first parameter");
        sb.AppendLine(";   Result returned in D0");
        sb.AppendLine();

        sb.AppendLine("        SECTION CODE");
        sb.AppendLine();

        // Generate wrappers for lifecycle functions
        sb.AppendLine("; Lifecycle Function Wrappers");
        sb.AppendLine();

        // LibOpen wrapper: receives A6=base, calls LibOpen(base)
        sb.AppendLine("        XDEF    _LibOpen_Wrapper");
        sb.AppendLine("        XREF    _LibOpen");
        sb.AppendLine("_LibOpen_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push library base as parameter");
        sb.AppendLine("        jsr     _LibOpen        ; Call C function");
        sb.AppendLine("        addq.l  #4,sp           ; Clean up stack");
        sb.AppendLine("        rts                     ; Return (result in D0)");
        sb.AppendLine();

        // LibClose wrapper: receives A6=base, calls LibClose(base)
        sb.AppendLine("        XDEF    _LibClose_Wrapper");
        sb.AppendLine("        XREF    _LibClose");
        sb.AppendLine("_LibClose_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push library base");
        sb.AppendLine("        jsr     _LibClose       ; Call C function");
        sb.AppendLine("        addq.l  #4,sp           ; Clean up stack");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // LibExpunge wrapper: receives A6=base, calls LibExpunge(base)
        sb.AppendLine("        XDEF    _LibExpunge_Wrapper");
        sb.AppendLine("        XREF    _LibExpunge");
        sb.AppendLine("_LibExpunge_Wrapper:");
        sb.AppendLine("        move.l  a6,-(sp)        ; Push library base");
        sb.AppendLine("        jsr     _LibExpunge     ; Call C function");
        sb.AppendLine("        addq.l  #4,sp           ; Clean up stack");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // LibReserved wrapper
        sb.AppendLine("        XDEF    _LibReserved_Wrapper");
        sb.AppendLine("        XREF    _LibReserved");
        sb.AppendLine("_LibReserved_Wrapper:");
        sb.AppendLine("        jsr     _LibReserved    ; No parameters");
        sb.AppendLine("        rts");
        sb.AppendLine();

        // Generate wrappers for user functions
        sb.AppendLine("; User Function Wrappers");
        sb.AppendLine();

        foreach (var func in _libraryFunctions)
        {
            if (!func.IsLifecycleFunction)
            {
                GenerateUserFunctionWrapper(sb, func);
            }
        }

        sb.AppendLine("        END");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generate A6 wrapper for a user library function.
    /// </summary>
    private void GenerateUserFunctionWrapper(StringBuilder sb, LibraryFunction func)
    {
        var wrapperName = $"_{func.CName}_Wrapper";
        var cFuncName = $"_{func.CName}";

        sb.AppendLine($"; Wrapper for {func.CName}");
        sb.AppendLine($"        XDEF    {wrapperName}");
        sb.AppendLine($"        XREF    {cFuncName}");
        sb.AppendLine($"{wrapperName}:");

        // For now, generate a simple wrapper that just calls the function
        // TODO: Need to analyze function signature to properly marshal parameters
        var parameters = func.Function.Parameters;

        if (parameters.Count == 0)
        {
            // No parameters, just call
            sb.AppendLine($"        jsr     {cFuncName}");
        }
        else
        {
            // Push parameters in reverse order (right to left for C convention)
            // For now, assume parameters come in D0, D1, A0, A1 order
            // This is a simplified version - full implementation needs parameter analysis

            for (int i = parameters.Count - 1; i >= 0; i--)
            {
                var param = parameters[i];
                if (i == 0)
                {
                    // First parameter - D0 or A0 depending on type
                    if (IsPointerType(param.Type))
                        sb.AppendLine("        move.l  a0,-(sp)");
                    else
                        sb.AppendLine("        move.l  d0,-(sp)");
                }
                else if (i == 1)
                {
                    // Second parameter - D1 or A1
                    if (IsPointerType(param.Type))
                        sb.AppendLine("        move.l  a1,-(sp)");
                    else
                        sb.AppendLine("        move.l  d1,-(sp)");
                }
                // TODO: Handle more parameters (D2, D3, A2, A3...)
            }

            sb.AppendLine($"        jsr     {cFuncName}");

            // Clean up stack (4 bytes per parameter)
            int stackCleanup = parameters.Count * 4;
            if (stackCleanup <= 8)
                sb.AppendLine($"        addq.l  #{stackCleanup},sp");
            else
                sb.AppendLine($"        add.l   #{stackCleanup},sp");
        }

        sb.AppendLine("        rts");
        sb.AppendLine();
    }

    /// <summary>
    /// Check if a type is a pointer type.
    /// </summary>
    private bool IsPointerType(IrType type)
    {
        return type is IrPointerType;
    }
}
