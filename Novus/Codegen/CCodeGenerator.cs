using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Novus.Diagnostics;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// C code generator for Novus.
/// Generates C99 code from Novus IR that can be compiled with VBCC.
/// Target: AmigaOS 2.0+ (68020+) using +aos68k -c99
/// </summary>
/// <remarks>
/// This class is split into multiple partial class files for maintainability:
/// - CCodeGenerator.cs - Main class with constructor and entry points
/// - CCodeGenerator.Types.cs - Type emission (structs, enums, tuples)
/// - CCodeGenerator.Instructions.cs - IR instruction emission
/// - CCodeGenerator.Helpers.cs - Utility/helper methods
///
/// STRICT ALIASING SAFETY:
/// This generator avoids strict aliasing violations through several mechanisms:
///
/// 1. Type-punning via memcpy: All operations that could violate strict aliasing
///    (e.g., struct copies, array element assignments with mismatched types) use
///    __novus_memcpy() instead of direct assignment. This is safe because memcpy
///    operates on byte buffers (uint8_t*) which can alias any type.
///
/// 2. Cast to uint8_t* for memcpy: All pointer casts for memcpy use uint8_t*
///    (or const uint8_t* for sources), which is the "byte-level access" escape hatch.
///
/// 3. Compound literal avoidance: Instead of compound literals like (Type){...},
///    we use static const temporaries for aggregate initialization to avoid
///    alignment issues on 68k.
///
/// 4. Element-by-element for small arrays: Small arrays (<=16 elements) are
///    initialized element-by-element to avoid memcpy overhead.
///
/// The remaining pointer casts in this generator are:
/// - (void*) for generic pointer usage (standard C pattern)
/// - AmigaOS-specific patterns (APTR, UBYTE* arithmetic) that are intentional
///
/// VBCC WORKAROUNDS:
/// This generator includes workarounds for known VBCC optimizer issues. Each
/// workaround is documented with:
/// - WORKAROUND_ID: A unique identifier (e.g., VBCC_001)
/// - AFFECTED_VERSIONS: VBCC versions where the issue exists
/// - DESCRIPTION: What the bug causes
/// - SOLUTION: How we work around it
///
/// See <see cref="VbccWorkarounds"/> for the registry of all workarounds.
/// </remarks>
public partial class CCodeGenerator
{
    private readonly IrModule _module;
    private readonly List<IrStringLiteral> _stringLiterals;
    private readonly string _cpuTarget;
    private readonly string _fpuMode;
    private readonly StringBuilder _output;
    private readonly HashSet<string> _requiredProtoHeaders;
    private readonly HashSet<string>? _explicitEntryPoints;
    private readonly bool _useSharedTypesHeader;
    private readonly string? _projectVersion;
    private readonly BuildMode _buildMode;
    private readonly SafetyLevel _safetyLevel;

    // Track which variables have been declared in the current function
    // to avoid redeclaration errors when the same variable is assigned in multiple branches
    private HashSet<string> _declaredVariables = new();

    // Track current function/struct being emitted (for error messages)
    private IrFunction? _currentEmittingFunction = null;
    private string? _currentEmittingStruct = null;

    // Track which function parameters were converted to pointers in the C signature
    // (due to TypeContainsHeapData) so we don't add & when passing them to other functions
    private HashSet<string> _pointerConvertedParameters = new();

    // Track member access instructions for move semantic analysis
    // Maps result variable name -> (source struct, field name, accessor, field type)
    private Dictionary<string, (string structValue, string fieldName, string accessor, IrStructType fieldType)> _memberAccessInfo = new();

    // Track index access instructions so we can reconstruct lvalue expressions when taking address
    // Maps result variable name -> (array expression, index expression)
    private Dictionary<string, (string arrayExpr, string indexExpr)> _indexAccessInfo = new();

    // Track field access chains so we can reconstruct lvalue expressions for stores and address-of
    // Maps result variable name -> (base expression, field name, accessor)
    // This allows reconstructing chains like self->timereq->tr_node from intermediate variables
    private Dictionary<string, (string baseExpr, string fieldName, string accessor)> _fieldAccessChainInfo = new();

    // VBCC 68K FIX: Track struct member accesses that are ONLY used for address-of operations.
    // When a struct field is only used to take its address (e.g., &(*screen).BitMap), we don't
    // need to copy the struct value - we can just track the chain info and emit &(base->field)
    // directly. This prevents illegal struct-by-value copies that cause Guru $80000004 on 68040.
    private HashSet<string> _addressOnlyStructMemberAccess = new();

    // Counter for defer block emissions to ensure unique labels across multiple defer cleanup sites
    private int _deferEmissionCounter = 0;

    // Suffix to append to labels during defer block emission (empty for normal code)
    private string _labelSuffix = "";

    // Track which defer blocks have been activated (had their IrDefer instruction emitted)
    // at the current point in the code flow. This is reset for each function.
    // Maps defer block -> defer index (1-based, matching _defer_N_active variable)
    private HashSet<int> _activatedDeferBlocks = new();

    // Block-scoped variable tracking for reduced stack usage
    // These are set during EmitFunction and used by EmitBasicBlock
    private Dictionary<string, HashSet<string>>? _currentBlockScopedVars;
    private Dictionary<string, (IrType Type, bool IsArray, int ArraySize)>? _currentLocalDeclVars;
    private Dictionary<string, IrType>? _currentStoredVars;
    // Arrays that should be declared as 'static' because they're filled with compile-time constants
    private HashSet<string>? _currentStaticConstArrays;

    // Liveness-based variable slot mapping for stack usage reduction
    // Maps IR variable names to shared slot names (e.g., "_formatter0" -> "_slot_StackFormatter_0")
    private Dictionary<string, string>? _variableToSlot;
    // Maps slot names to their types (for declaration)
    private Dictionary<string, IrType>? _slotTypes;

    // Statement-level debug symbol tracking for exact line numbers in crash dialogs
    // Maps label name -> (file, line, function name) for all emitted debug line markers
    private List<(string LabelName, string FileName, int Line, string FuncName)> _debugLineMarkers = new();
    // Track the last emitted line number to avoid duplicate markers for same line
    private int _lastEmittedDebugLine = -1;
    // Counter for unique debug label generation within a function
    private int _debugLabelCounter = 0;

    // Track last emitted #line directive to avoid duplicates
    private int _lastLineDirectiveLine = -1;
    private string? _lastLineDirectiveFile = null;

    // Dead label elimination: track which labels are actually targeted by branches
    // Labels not in this set will be skipped during emission to avoid VBCC warning 148
    private HashSet<string> _reachableLabels = new();

    // Track emitted labels within a function to prevent duplicate emission
    // (labels can be added both by EmitBasicBlock and IrLabel instructions)
    private HashSet<string> _emittedLabels = new();

    /// <summary>
    /// VBCC WORKAROUND: Track comparison expressions that can be inlined into conditional branches.
    ///
    /// AFFECTED VERSIONS: VBCC 0.9h and earlier (tested with vbcc 0.9h, AmigaOS target)
    ///
    /// PROBLEM: VBCC's optimizer can move stack cleanup instructions between a comparison
    /// result store and the subsequent conditional branch, clobbering the CPU condition flags.
    ///
    /// Example of problematic generated code:
    /// <code>
    ///   cmp.l d0,d1        ; Compare values
    ///   seq d2             ; Store condition result (equal) in d2
    ///   addq.l #4,sp       ; Stack cleanup INSERTED HERE - clobbers condition flags!
    ///   tst.b d2           ; Test d2 (but flags already wrong)
    ///   beq .L1            ; Branch uses clobbered flags
    /// </code>
    ///
    /// SOLUTION: Instead of storing the comparison result to a variable and then testing it,
    /// we inline the comparison expression directly into the if() statement:
    /// <code>
    ///   if (a == b) { ... }     // Inlined - no intermediate storage
    /// </code>
    /// Instead of:
    /// <code>
    ///   bool cond = a == b;     // Comparison stored
    ///   if (cond) { ... }       // Tested later - VBCC may insert stack ops between
    /// </code>
    ///
    /// This dictionary maps result variable names to their comparison expressions,
    /// e.g., "_slot_bool_0" -> "_slot_i32_s_0 == 0"
    ///
    /// The dictionary is cleared at the start of each function (see EmitFunction and
    /// EmitFunctionToBuilder) to prevent stale entries from leaking between functions.
    /// </summary>
    private Dictionary<string, string> _inlineableComparisons = new();

    // Threshold for element-by-element array initialization vs memcpy
    // Arrays smaller than this use element-by-element assignment (safer on 68k)
    // Arrays larger use memcpy from a static const temporary (better performance)
    private const int MaxElementByElementArraySize = 16;

    /// <summary>
    /// Emit safe array copy that avoids compound literal alignment issues on 68k.
    /// For small arrays: emits element-by-element assignments.
    /// For large arrays: emits memcpy (the array initializer in the source is already aligned).
    /// </summary>
    /// <param name="destExpr">Destination array expression (e.g., "varName.fieldName")</param>
    /// <param name="arrayLiteral">The array literal to copy from</param>
    /// <param name="arrayType">The array type</param>
    private void EmitSafeArrayCopy(string destExpr, IrArrayLiteral arrayLiteral, IrArrayType arrayType)
    {
        var elementType = GetCType(arrayType.ElementType);
        var size = arrayType.Length;

        // Check if all elements are zero - use memset for efficiency
        if (IsAllZeroArray(arrayLiteral))
        {
            _output.AppendLine($"    __novus_memset({destExpr}, 0, sizeof({destExpr}));");
            return;
        }

        // For small arrays, emit element-by-element assignment
        // This avoids compound literal alignment issues on 68k
        if (size <= MaxElementByElementArraySize)
        {
            for (int i = 0; i < arrayLiteral.Elements.Count; i++)
            {
                var elem = arrayLiteral.Elements[i];

                // VBCC 68K FIX: For struct literal elements, emit field-by-field assignment
                // instead of compound literal which causes misalignment on 68040
                if (elem is IrStructLiteral structLitElem)
                {
                    foreach (var kvp in structLitElem.FieldValues)
                    {
                        var fieldValue = EmitValue(kvp.Value);
                        _output.AppendLine($"    {destExpr}[{i}].{kvp.Key} = {fieldValue};");
                    }
                }
                else
                {
                    var elemValue = EmitValue(elem);
                    _output.AppendLine($"    {destExpr}[{i}] = {elemValue};");
                }
            }
            return;
        }

        // For larger arrays, use memcpy with sizeof to copy from a properly-declared source
        // The brace initializer is used in a context where VBCC will properly align it
        // We use a local static const array which VBCC places in .rodata with proper alignment
        var initValue = EmitArrayLiteralForInitializer(arrayLiteral);
        _output.AppendLine($"    {{");
        _output.AppendLine($"        static const {elementType} __init[{size}] = {initValue};");
        _output.AppendLine($"        __novus_memcpy((uint8_t*){destExpr}, (const uint8_t*)__init, sizeof({destExpr}));");
        _output.AppendLine($"    }}");
    }

    /// <summary>
    /// VBCC_004 FIX: Emits field-by-field assignment for nested enum values.
    /// Compound literals like (EnumType){ .tag = ..., .data = { ... } } cause misalignment on 68k.
    /// This method emits separate assignments for tag and data fields instead.
    /// </summary>
    /// <param name="destExpr">Destination expression (e.g., "varName.data.Err._0")</param>
    /// <param name="nestedEnumValue">The nested enum value to emit</param>
    private void EmitNestedEnumFieldByField(string destExpr, IrEnumValue nestedEnumValue)
    {
        var nestedEnumType = nestedEnumValue.Type as IrEnumType;
        if (nestedEnumType == null)
            throw new InvalidOperationException("EnumValue must have IrEnumType");

        var nestedEnumName = MangleName(nestedEnumType);
        bool nestedHasAnyData = nestedEnumType.Variants.Any(v => v.HasAssociatedData);

        if (nestedHasAnyData)
        {
            // Emit tag assignment
            _output.AppendLine($"    {destExpr}.tag = {nestedEnumName}_{nestedEnumValue.VariantName};");

            // Emit associated data assignments if present
            if (nestedEnumValue.AssociatedValues.Count > 0)
            {
                // Check if this is a unit type
                bool nestedIsUnitType = nestedEnumValue.AssociatedValues.Count == 1 &&
                                        nestedEnumValue.AssociatedValues[0].Type is IrTupleType unitTupleType &&
                                        unitTupleType.ElementTypes.Count == 0;

                if (!nestedIsUnitType)
                {
                    for (int j = 0; j < nestedEnumValue.AssociatedValues.Count; j++)
                    {
                        // Recursively handle nested struct literals
                        if (nestedEnumValue.AssociatedValues[j] is IrStructLiteral nestedNestedStructLit)
                        {
                            foreach (var kvp in nestedNestedStructLit.FieldValues)
                            {
                                var fieldValue = EmitValue(kvp.Value);
                                _output.AppendLine($"    {destExpr}.data.{nestedEnumValue.VariantName}._{j}.{kvp.Key} = {fieldValue};");
                            }
                        }
                        // Recursively handle nested enum literals
                        else if (nestedEnumValue.AssociatedValues[j] is IrEnumValue nestedNestedEnumValue)
                        {
                            EmitNestedEnumFieldByField($"{destExpr}.data.{nestedEnumValue.VariantName}._{j}", nestedNestedEnumValue);
                        }
                        else
                        {
                            var assocValue = EmitValue(nestedEnumValue.AssociatedValues[j]);
                            _output.AppendLine($"    {destExpr}.data.{nestedEnumValue.VariantName}._{j} = {assocValue};");
                        }
                    }
                }
                else
                {
                    // Unit type - set dummy field
                    _output.AppendLine($"    {destExpr}.data.{nestedEnumValue.VariantName}._dummy = 0;");
                }
            }
        }
        else
        {
            // For enums without associated data, just assign the whole thing
            // (it's just a single integer value, no compound literal needed)
            _output.AppendLine($"    {destExpr} = {nestedEnumName}_{nestedEnumValue.VariantName};");
        }
    }

    /// <summary>
    /// Determines if a function is a monomorphized trait implementation.
    /// These functions need special handling to avoid duplicate symbol errors when
    /// multiple modules use the same monomorphization (e.g., Vec<TagItem>::drop).
    /// </summary>
    public bool IsMonomorphizedTraitMethod(IrFunction function)
    {
        // Extern functions are never trait implementations
        if (function.IsExtern)
            return false;

        // Check if function has a 'self' parameter (trait methods always have self)
        bool hasSelfParam = function.Parameters.Any(p => p.Name == "self");
        if (!hasSelfParam)
            return false;

        // NOTE: Function names DON'T include the generic parameter (e.g., "Vec_drop" not "Vec_TagItem_drop")
        // The generic parameter is only in the mangled name and in the CacheKey.
        // So we can't rely on function name splitting alone - we must check parameters' CacheKeys.

        // Check if any parameter has a struct type with a generic-looking name
        // (contains the type parameter in the name, like Vec_TagItem)
        foreach (var param in function.Parameters)
        {
            var paramType = param.Type;

            // Unwrap pointer types (trait methods often use *T instead of &T)
            if (paramType is IrPointerType pointerType)
                paramType = pointerType.PointeeType;
            // Unwrap reference types
            else if (paramType is IrReferenceType refType)
                paramType = refType.PointeeType;
            else if (paramType is IrMutReferenceType mutRefType)
                paramType = mutRefType.PointeeType;

            if (paramType is IrStructType structType)
            {
                // Check if struct name looks like a monomorphization (e.g., "Vec_TagItem")
                var structParts = structType.Name.Split('_');
                if (structParts.Length > 1 && (structParts[0] == "Vec" || structParts[0] == "Option" || structParts[0] == "Result"))
                {
                    return true;
                }

                // MOST RELIABLE: Check CacheKey - if it contains generic parameters, this is monomorphized
                // CacheKey format: "Vec<TagItem>" or "Option<String>" etc.
                if (!string.IsNullOrEmpty(structType.CacheKey) && structType.CacheKey.Contains('<'))
                {
                    return true;
                }

                // Also check GenericParameters
                if (structType.GenericParameters.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if a function is a monomorphized generic function.
    /// Monomorphized functions should be emitted as 'static inline' to avoid duplicate symbols.
    /// </summary>
    /// <remarks>
    /// This method uses a two-tier detection strategy:
    /// 1. EXPLICIT FLAG (preferred): Check IrFunction.IsMonomorphized which is set during
    ///    generic instantiation in the frontend. This is the most reliable method.
    /// 2. HEURISTIC FALLBACK: For backward compatibility with IR that doesn't have the flag set,
    ///    we fall back to type-based and name-based heuristics. These will be deprecated
    ///    once all code paths set the explicit flag.
    /// </remarks>
    internal bool IsMonomorphizedFunction(IrFunction function)
    {
        // Extern functions are never monomorphized
        if (function.IsExtern)
            return false;

        // ============================================================================
        // PRIMARY DETECTION: Explicit monomorphization flag (most reliable)
        // ============================================================================
        // This flag is set during generic instantiation and is authoritative.
        if (function.IsMonomorphized)
            return true;

        // Also check the trait method flag
        if (function.IsMonomorphizedTraitMethod)
            return true;

        // ============================================================================
        // FALLBACK DETECTION: Heuristics for backward compatibility
        // ============================================================================
        // These heuristics exist for IR generated before explicit tracking was added.
        // They should be removed once all code paths set IsMonomorphized correctly.

        // All monomorphized functions are created with Private visibility
        if (function.Visibility != Visibility.Private)
            return false;

        // Best heuristic: Check if function parameters or return type have a CacheKey
        // (indicating a monomorphized generic type)
        foreach (var param in function.Parameters)
        {
            if (HasMonomorphizedType(param.Type))
                return true;
        }

        if (HasMonomorphizedType(function.ReturnType))
            return true;

        // Naming pattern heuristics (least reliable, but catches edge cases)
        var name = function.Name;

        // Pattern 1: Contains :: which indicates enum method with type args
        if (name.Contains("::"))
        {
            var parts = name.Split("::");
            if (parts.Length == 2)
            {
                var methodPart = parts[1];
                // If method part contains underscore, it likely has type args
                if (methodPart.Contains('_'))
                    return true;
            }
        }

        // Pattern 2: Known type suffixes in name
        var knownTypeSuffixes = new[] {
            "_i8", "_i16", "_i32", "_i64",
            "_u8", "_u16", "_u32", "_u64",
            "_bool", "_ptr_", "_String",
            "_f32", "_f64", "_fixed16", "_fixed32"
        };

        foreach (var suffix in knownTypeSuffixes)
        {
            if (name.Contains(suffix))
                return true;
        }

        // Pattern 3: Check if name starts with known generic type names
        foreach (var structType in _module.MonomorphizedTypes.Values)
        {
            var baseName = structType.BaseName;
            if (name.StartsWith($"{baseName}_"))
                return true;
        }

        foreach (var enumType in _module.Enums)
        {
            var enumName = enumType.EnumName;
            if (name.StartsWith($"{enumName}_"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a type is or contains a monomorphized generic type (has a CacheKey).
    /// Recursively checks all constituent types (arrays, tuples, pointers, etc.).
    /// </summary>
    private bool HasMonomorphizedType(IrType type)
    {
        return type switch
        {
            // Direct monomorphized types
            IrStructType structType when structType.CacheKey != null => true,
            IrEnumType enumType when enumType.CacheKey != null => true,

            // Recursive container types
            IrPointerType ptrType => HasMonomorphizedType(ptrType.PointeeType),
            IrReferenceType refType => HasMonomorphizedType(refType.PointeeType),
            IrMutReferenceType mutRefType => HasMonomorphizedType(mutRefType.PointeeType),
            IrArrayType arrayType => HasMonomorphizedType(arrayType.ElementType),
            IrTupleType tupleType => tupleType.ElementTypes.Any(HasMonomorphizedType),
            IrFunctionPointerType funcPtrType =>
                (funcPtrType.ReturnType != null && HasMonomorphizedType(funcPtrType.ReturnType)) ||
                funcPtrType.ParameterTypes.Any(HasMonomorphizedType),

            // Non-generic types
            _ => false
        };
    }

    public CCodeGenerator(IrModule module, List<IrStringLiteral> stringLiterals, string cpuTarget, string fpuMode, BuildMode buildMode = BuildMode.Debug, SafetyLevel? safetyLevel = null, HashSet<string>? explicitEntryPoints = null, bool useSharedTypesHeader = false, string? projectVersion = null)
    {
        _module = module;
        _stringLiterals = stringLiterals;
        _cpuTarget = cpuTarget;
        _fpuMode = fpuMode;
        _buildMode = buildMode;
        _safetyLevel = safetyLevel ?? SafetyLevelExtensions.GetDefaultForBuildMode(buildMode);
        _output = new StringBuilder();
        _requiredProtoHeaders = new HashSet<string>();
        _explicitEntryPoints = explicitEntryPoints;
        _projectVersion = projectVersion;
        _useSharedTypesHeader = useSharedTypesHeader;
    }

    /// <summary>
    /// Generate shared types header from a type registry.
    /// This header contains all type definitions used across modules.
    /// </summary>
    public static string GenerateSharedTypesHeader(TypeRegistry typeRegistry, IEnumerable<IrFunction>? allFunctions = null)
    {
        allFunctions ??= Enumerable.Empty<IrFunction>();
        var sb = new StringBuilder();

        sb.AppendLine("// Novus Standard Library - Shared Type Definitions");
        sb.AppendLine("// Auto-generated - do not edit");
        sb.AppendLine("//");
        sb.AppendLine("// This header contains all type definitions used by Novus stdlib.");
        sb.AppendLine("// Each function is compiled separately and includes this header.");
        sb.AppendLine();
        sb.AppendLine("#ifndef NOVUS_TYPES_H");
        sb.AppendLine("#define NOVUS_TYPES_H");
        sb.AppendLine();
        // AmigaOS NDK headers for external types
        // Include exec/types.h FIRST - it may define int*_t/uint*_t types
        sb.AppendLine("#include <exec/types.h>");
        sb.AppendLine();

        // Define standard integer types only if stdint.h wasn't included by exec/types.h
        // VBCC's exec/types.h includes <stdint.h> which defines __STDINT_H
        sb.AppendLine("// Integer types - fallback definitions if stdint.h not included");
        sb.AppendLine("#ifndef __STDINT_H");
        sb.AppendLine("typedef signed char int8_t;");
        sb.AppendLine("typedef short int16_t;");
        sb.AppendLine("typedef long int32_t;");
        sb.AppendLine("typedef long long int64_t;");
        sb.AppendLine("typedef unsigned char uint8_t;");
        sb.AppendLine("typedef unsigned short uint16_t;");
        sb.AppendLine("typedef unsigned long uint32_t;");
        sb.AppendLine("typedef unsigned long long uint64_t;");
        sb.AppendLine("#endif");
        sb.AppendLine();
        sb.AppendLine("// Boolean type - 1 byte to match Novus semantics (not C99's int-based bool)");
        sb.AppendLine("// Use unique guard to avoid conflicts with system headers (VBCC may use different stdbool guard)");
        sb.AppendLine("#ifndef __NOVUS_BOOL_DEFINED");
        sb.AppendLine("#define __NOVUS_BOOL_DEFINED");
        sb.AppendLine("typedef uint8_t bool;");
        sb.AppendLine("#define true ((bool)1)");
        sb.AppendLine("#define false ((bool)0)");
        sb.AppendLine("#endif");
        sb.AppendLine();
        sb.AppendLine("#include <exec/nodes.h>");
        sb.AppendLine("#include <exec/lists.h>");
        sb.AppendLine("#include <exec/ports.h>");
        sb.AppendLine("#include <exec/libraries.h>");
        sb.AppendLine("#include <exec/tasks.h>");
        sb.AppendLine("#include <exec/devices.h>");
        sb.AppendLine("#include <exec/semaphores.h>");
        sb.AppendLine("#include <intuition/intuition.h>");
        sb.AppendLine("#include <graphics/gfx.h>");
        sb.AppendLine("#include <graphics/text.h>");
        sb.AppendLine("#include <utility/tagitem.h>");
        sb.AppendLine("#include <dos/dos.h>");
        sb.AppendLine();
        sb.AppendLine("// AmigaOS clib prototype headers - compiler-neutral function declarations");
        sb.AppendLine("// Note: We use clib protos instead of proto/ headers because NDK3.9 proto");
        sb.AppendLine("// headers use SAS/C pragmas which VBCC doesn't understand. Functions are");
        sb.AppendLine("// linked via amiga.lib which provides the library stub glue code.");
        sb.AppendLine("#include <clib/exec_protos.h>");
        sb.AppendLine("#include <clib/dos_protos.h>");
        sb.AppendLine("#include <clib/intuition_protos.h>");
        sb.AppendLine("#include <clib/graphics_protos.h>");
        sb.AppendLine("#include <clib/gadtools_protos.h>");
        sb.AppendLine("#include <clib/alib_protos.h>"); // DoMethodA, etc.
        sb.AppendLine("#include <devices/timer.h>");
        // ReAction class headers (OS 3.5+) - tag definitions
        sb.AppendLine("#include <classes/window.h>");
        sb.AppendLine("#include <gadgets/layout.h>");
        sb.AppendLine("#include <gadgets/button.h>");
        sb.AppendLine("#include <gadgets/checkbox.h>");
        sb.AppendLine("#include <gadgets/integer.h>");
        sb.AppendLine("#include <gadgets/radiobutton.h>");
        sb.AppendLine("#include <images/label.h>");
        // ReAction class protos (for *_GetClass functions)
        sb.AppendLine("#include <clib/window_protos.h>");
        sb.AppendLine("#include <clib/layout_protos.h>");
        sb.AppendLine("#include <clib/button_protos.h>");
        sb.AppendLine("#include <clib/checkbox_protos.h>");
        sb.AppendLine("#include <clib/integer_protos.h>");
        sb.AppendLine("#include <clib/radiobutton_protos.h>");
        sb.AppendLine("#include <clib/label_protos.h>");
        sb.AppendLine();
        // IMPORTANT: These typedefs must include all structs from the NdkStructs static field.
        // When adding a new NDK struct to NdkStructs, also add its typedef here.
        // See NdkStructs field (~line 1955) for the canonical list.
        sb.AppendLine("// Typedefs for NDK structs (headers only define 'struct Foo', not 'Foo')");
        sb.AppendLine("typedef struct NewWindow NewWindow;");
        sb.AppendLine("typedef struct Window Window;");
        sb.AppendLine("typedef struct Screen Screen;");
        sb.AppendLine("typedef struct View View;");
        sb.AppendLine("typedef struct ViewPort ViewPort;");
        sb.AppendLine("typedef struct IntuiMessage IntuiMessage;");
        sb.AppendLine("typedef struct MsgPort MsgPort;");
        sb.AppendLine("typedef struct Message Message;");
        sb.AppendLine("typedef struct TagItem TagItem;");
        sb.AppendLine("typedef struct BitMap BitMap;");
        sb.AppendLine("typedef struct ColorMap ColorMap;");
        sb.AppendLine("typedef struct TextFont TextFont;");
        sb.AppendLine("typedef struct TextAttr TextAttr;");
        sb.AppendLine("typedef struct TTextAttr TTextAttr;");
        sb.AppendLine("typedef struct Task Task;");
        sb.AppendLine("typedef struct Node Node;");
        sb.AppendLine("typedef struct List List;");
        sb.AppendLine("typedef struct Library Library;");
        sb.AppendLine("typedef struct Menu Menu;");
        sb.AppendLine("typedef struct MenuItem MenuItem;");
        sb.AppendLine("typedef struct NewMenu NewMenu;");
        sb.AppendLine("typedef struct VisualInfo VisualInfo;");
        sb.AppendLine("typedef struct EasyStruct EasyStruct;");
        sb.AppendLine("typedef struct timeval timeval;");
        sb.AppendLine("typedef struct timerequest timerequest;");
        sb.AppendLine("typedef struct EClockVal EClockVal;");
        sb.AppendLine("typedef struct IORequest IORequest;");
        sb.AppendLine("typedef struct IOStdReq IOStdReq;");
        sb.AppendLine("typedef struct Device Device;");
        sb.AppendLine("typedef struct Unit Unit;");
        sb.AppendLine("typedef struct SimpleSprite SimpleSprite;");
        // GEL system types
        sb.AppendLine("typedef struct VSprite VSprite;");
        sb.AppendLine("typedef struct Bob Bob;");
        sb.AppendLine("typedef struct AnimOb AnimOb;");
        sb.AppendLine("typedef struct AnimComp AnimComp;");
        sb.AppendLine("typedef struct GelsInfo GelsInfo;");
        sb.AppendLine("typedef struct collTable collTable;");
        sb.AppendLine("typedef struct RastPort RastPort;");
        sb.AppendLine("typedef struct DBufInfo DBufInfo;");
        // Area fill types
        sb.AppendLine("typedef struct AreaInfo AreaInfo;");
        sb.AppendLine("typedef struct TmpRas TmpRas;");
        sb.AppendLine("typedef struct Layer Layer;");
        sb.AppendLine("typedef struct ClipRect ClipRect;");
        sb.AppendLine("typedef struct Region Region;");
        sb.AppendLine("typedef struct RegionRectangle RegionRectangle;");
        // Copper list types
        sb.AppendLine("typedef struct CopIns CopIns;");
        sb.AppendLine("typedef struct CopList CopList;");
        sb.AppendLine("typedef struct cprlist cprlist;");
        sb.AppendLine("typedef struct copinit copinit;");
        sb.AppendLine("typedef struct UCopList UCopList;");
        sb.AppendLine("typedef struct RasInfo RasInfo;");
        sb.AppendLine("typedef struct bltnode bltnode;");
        // DOS types that are commonly used in user code
        sb.AppendLine("typedef struct RDArgs RDArgs;");
        sb.AppendLine("typedef struct WBStartup WBStartup;");
        sb.AppendLine("typedef struct Process Process;");
        sb.AppendLine("typedef struct FileInfoBlock FileInfoBlock;");
        sb.AppendLine("typedef struct InfoData InfoData;");
        // BOOPSI/Intuition types for ReAction GUI
        sb.AppendLine("typedef struct DrawInfo DrawInfo;");
        sb.AppendLine("typedef struct IClass IClass;");
        sb.AppendLine("typedef struct Gadget Gadget;");
        sb.AppendLine("typedef struct Requester Requester;");
        sb.AppendLine();
        sb.AppendLine("// Sentinel value for \"unchanged\" pointer parameters (used by Amiga API)");
        sb.AppendLine("// Using explicit 32-bit constant to prevent VBCC from treating as 64-bit value");
        sb.AppendLine("static const void* NULL_PTR_SENTINEL = (void*)0xFFFFFFFFUL;");
        sb.AppendLine();
        sb.AppendLine("// Memory functions - no C library dependency");
        sb.AppendLine("void __novus_memset(void* dest, int value, uint32_t n);");
        sb.AppendLine("void __novus_memcpy(uint8_t* dest, const uint8_t* src, uint32_t n);");
        sb.AppendLine();

        var codegen = new CCodeGenerator(new IrModule(), new List<IrStringLiteral>(), "68020", "auto");

        // Struct types
        if (typeRegistry.StructTypes.Any())
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Struct Types");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();

            // Filter out generic structs (they should have been monomorphized already)
            // and sort by dependencies
            var concreteStructs = typeRegistry.StructTypes
                .Where(s => s.GenericParameters.Count == 0)
                .ToHashSet();
            var sortedStructs = codegen.TopologicalSortStructTypes(concreteStructs);

            // AmigaOS NDK structs: defined in std/ffi/amiga_structs.novus and std/ffi/*/
            // These are provided by NDK headers, so we skip generating definitions.
            // Uses the canonical NdkStructs static field as the single source of truth.
            // Primary method: Check for #[extern_type] attribute on struct
            // Fallback: Hardcoded NdkStructs list (for backward compatibility)

            // Filter structs: skip those marked with #[extern_type] or in the NdkStructs list
            var userStructs = sortedStructs
                .Where(s => {
                    // Primary check: #[extern_type] attribute
                    if (s.Attributes != null && s.Attributes.Has(Novus.SemanticAnalysis.KnownAttributes.ExternType))
                        return false;

                    // Fallback check: canonical NdkStructs list
                    return !NdkStructs.Contains(s.StructName);
                })
                .ToList();

            // CRITICAL: Collect and emit tuple types referenced by struct fields BEFORE struct definitions
            // This prevents duplicate typedef warnings when multiple structs use the same tuple type
            var tuplesUsedByStructs = new HashSet<IrTupleType>();
            foreach (var structType in userStructs)
            {
                foreach (var field in structType.Fields)
                {
                    codegen.CollectTupleTypesFromType(field.Type, tuplesUsedByStructs);
                }
            }

            if (tuplesUsedByStructs.Any())
            {
                sb.AppendLine("// Tuple types used by struct fields");
                foreach (var tupleType in tuplesUsedByStructs.OrderBy(t => codegen.GetTupleTypeName(t)))
                {
                    codegen.EmitTupleTypeToBuilder(sb, tupleType);
                }
                sb.AppendLine();
            }

            // CRITICAL: Collect enum types referenced by struct fields
            // This prevents "undefined type" errors when structs use enum types as fields
            var enumsUsedByStructs = new HashSet<IrEnumType>();
            var enumNamesByStructs = new HashSet<string>(); // Track names in case we have stub instances
            foreach (var structType in userStructs)
            {
                foreach (var field in structType.Fields)
                {
                    codegen.CollectEnumTypesFromType(field.Type, enumsUsedByStructs, enumNamesByStructs);
                }
            }

            // For any enum names we found but didn't get populated instances for,
            // look them up in the type registry to get the full version
            foreach (var enumName in enumNamesByStructs)
            {
                var canonicalEnum = typeRegistry.EnumTypes.FirstOrDefault(e => e.EnumName == enumName && e.Variants.Count > 0);
                if (canonicalEnum != null)
                {
                    enumsUsedByStructs.Add(canonicalEnum);
                }
            }

            // CRITICAL: Collect struct types that are used BY VALUE in enum variant payloads
            // These structs must be fully defined BEFORE the enums that contain them
            var structsUsedByEnumPayloads = new HashSet<IrStructType>();
            foreach (var enumType in enumsUsedByStructs)
            {
                codegen.CollectStructTypesFromEnumPayloads(enumType, structsUsedByEnumPayloads);
            }

            // PASS 1: Forward declarations for all user-defined structs
            // This is required for self-referential structs (e.g., Gadget* NextGadget in Gadget)
            sb.AppendLine("// Forward declarations");
            foreach (var structType in userStructs)
            {
                var structName = codegen.MangleName(structType);
                sb.AppendLine($"typedef struct {structName} {structName};");
            }
            sb.AppendLine();

            // PASS 2: Emit structs that are needed by enum payloads FIRST
            // This ensures enums like Option<Str> can reference Str by value
            var structsNeededByEnums = userStructs
                .Where(s => structsUsedByEnumPayloads.Any(e =>
                    (e.CacheKey ?? e.StructName) == (s.CacheKey ?? s.StructName)))
                .ToHashSet();

            if (structsNeededByEnums.Any())
            {
                sb.AppendLine("// Struct types used by enum payloads (defined early)");
                foreach (var structType in structsNeededByEnums.OrderBy(s => codegen.MangleName(s)))
                {
                    codegen.EmitStructTypeToBuilder(sb, structType);
                }
            }

            // PASS 3: Emit enum types that depend on those struct definitions
            if (enumsUsedByStructs.Any())
            {
                sb.AppendLine("// Enum types used by struct fields");
                foreach (var enumType in enumsUsedByStructs.OrderBy(e => codegen.MangleName(e)))
                {
                    codegen.EmitEnumTypeToBuilder(sb, enumType);
                }
                sb.AppendLine();
            }

            // PASS 4: Emit remaining struct definitions (those not already emitted)
            foreach (var structType in userStructs)
            {
                if (!structsNeededByEnums.Contains(structType))
                {
                    codegen.EmitStructTypeToBuilder(sb, structType);
                }
            }

            // Note: Vec_*_as_ptr functions are generated as regular functions in stdlib,
            // not as macros, due to VBCC optimizer bugs at -O1/-O2.
            // Use -O0 for now until VBCC is fixed.
        }

        // Tuple types
        if (typeRegistry.TupleTypes.Any())
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Tuple Types");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();

            // Deduplicate tuple types by their C type name to prevent redeclaration warnings
            var seenTupleNames = new HashSet<string>();
            foreach (var tupleType in typeRegistry.TupleTypes.OrderBy(t => t.Name))
            {
                var tupleName = codegen.GetTupleTypeName(tupleType);
                if (seenTupleNames.Add(tupleName))
                {
                    codegen.EmitTupleTypeToBuilder(sb, tupleType);
                }
            }
        }

        // Closure types (fat pointers)
        if (typeRegistry.ClosureTypes.Any())
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Closure Types (Fat Pointers)");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();

            // Deduplicate closure types by their C type name to prevent redeclaration warnings
            var seenClosureNames = new HashSet<string>();
            foreach (var closureType in typeRegistry.ClosureTypes)
            {
                var closureName = codegen.GetClosureTypeName(closureType);
                if (seenClosureNames.Add(closureName))
                {
                    codegen.EmitClosureTypeToBuilder(sb, closureType);
                }
            }
        }

        // Forward declarations for closure functions (__closure_N)
        var closureFunctions = allFunctions.Where(f => f.Name.StartsWith("__closure_")).ToList();
        if (closureFunctions.Any())
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Closure Function Forward Declarations");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();
            foreach (var closureFunc in closureFunctions)
            {
                var returnType = codegen.GetCType(closureFunc.ReturnType);
                // For forward declaration, use void* for env parameter to match closure struct's fn_ptr type
                var paramParts = new List<string>();
                foreach (var param in closureFunc.Parameters)
                {
                    if (param.Name == "__env")
                    {
                        // Use void* for the env parameter in forward declaration
                        paramParts.Add("void* __env");
                    }
                    else
                    {
                        paramParts.Add($"{codegen.GetCType(param.Type)} {param.Name}");
                    }
                }
                var paramList = paramParts.Count > 0 ? string.Join(", ", paramParts) : "void";
                sb.AppendLine($"extern {returnType} {closureFunc.Name}({paramList});");
            }
            sb.AppendLine();
        }

        // Enum types
        if (typeRegistry.EnumTypes.Any())
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Enum Types");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();

            // Sort enum types in dependency order: emit leaf types first, then types that depend on them
            // This handles cases like AppError { Audio(AudioError), Timer(TimerError) } where
            // AudioError and TimerError must be defined before AppError
            var concreteEnums = typeRegistry.EnumTypes
                .Where(e => codegen.IsConcreteEnum(e))
                .ToHashSet();
            var sortedEnumTypes = codegen.TopologicalSortEnumTypes(concreteEnums);
            foreach (var enumType in sortedEnumTypes)
            {
                codegen.EmitEnumTypeToBuilder(sb, enumType);
            }
        }

        // Runtime function declarations
        // These are needed for per-function compilation where each file includes this header
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Runtime Function Declarations");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // Always include panic (never elided)
        sb.AppendLine("// Panic handler - displays error and exits (never elided, even in release)");
        sb.AppendLine("void __novus_panic(const char* message, const char* file, int32_t line, int32_t col);");
        sb.AppendLine();

        // Assert handler (debug builds only, but declared here for simplicity)
        sb.AppendLine("// Assert handler - displays error and returns (debug builds only)");
        sb.AppendLine("void __novus_assert_failed(const char* file, int32_t line, int32_t col, const char* message);");
        sb.AppendLine();

        // Bounds check failure handler
        sb.AppendLine("// Bounds check failure handler - displays error when array index is out of bounds");
        sb.AppendLine("void __novus_bounds_check_failed(int32_t index, int32_t length, const char* file, int32_t line);");
        sb.AppendLine();

        // Division by zero check
        sb.AppendLine("// Division by zero check - displays error if divisor is zero");
        sb.AppendLine("void __novus_div_check(int32_t divisor, const char* file, int32_t line);");
        sb.AppendLine();

        // Null pointer check
        sb.AppendLine("// Null pointer check - displays error if pointer is null before dereference");
        sb.AppendLine("void __novus_null_pointer_error(const char* file, int32_t line);");
        sb.AppendLine();

        // Try operator error handler (for ? in i32-returning functions like main)
        sb.AppendLine("// Try operator error handler - prints error when ? fails in i32-returning functions");
        sb.AppendLine("void __novus_try_failed(const char* type_name, int32_t tag, const char* variant_names);");
        sb.AppendLine();

        // Memory functions
        sb.AppendLine("// Memory functions - no C library dependency");
        sb.AppendLine("void __novus_memset(void* dest, int value, uint32_t n);");
        sb.AppendLine("void __novus_memcpy(uint8_t* dest, const uint8_t* src, uint32_t n);");
        sb.AppendLine();

        // String conversion functions
        sb.AppendLine("// String conversion functions");
        sb.AppendLine("uint32_t i8_to_string(int8_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine("uint32_t i16_to_string(int16_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine("uint32_t i32_to_string(int32_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine("uint32_t i64_to_string(int64_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine("uint32_t u8_to_string(uint8_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine("uint32_t u16_to_string(uint16_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine("uint32_t u32_to_string(uint32_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine("uint32_t u64_to_string(uint64_t value, uint8_t* buffer, uint32_t buffer_size);");
        sb.AppendLine();

        // Raw memory allocation (used by closures for environment allocation)
        sb.AppendLine("// Raw memory allocation (used by closures)");
        sb.AppendLine("extern void* __novus_alloc_raw(uint32_t size, uint32_t flags);");
        sb.AppendLine("extern void __novus_free_raw(void* ptr, uint32_t size);");
        sb.AppendLine();

        // Memory tracking and MMU protection functions (always declared, only linked when used)
        sb.AppendLine("// Memory tracking runtime functions (linked when NOVUS_MEMORY_DEBUG is enabled)");
        sb.AppendLine("extern void* __novus_tracked_alloc(uint32_t size, uint32_t flags, const char* file, int32_t line);");
        sb.AppendLine("extern void __novus_tracked_free(void* ptr, uint32_t size, const char* file, int32_t line);");
        sb.AppendLine("extern void __novus_memory_report(void);");
        sb.AppendLine();
        sb.AppendLine("// MMU protection runtime functions");
        sb.AppendLine("extern void __novus_init_mmu_protection(void);");
        sb.AppendLine("extern void __novus_cleanup_mmu_protection(void);");
        sb.AppendLine();

        // Stack overflow detection
        sb.AppendLine("// Stack overflow detection runtime functions");
        sb.AppendLine("extern void __novus_init_stack_bounds(void);");
        sb.AppendLine("extern void __novus_check_stack(uint32_t required_bytes, const char* func_name, int32_t line);");
        sb.AppendLine();

        // Pointer access checking (paranoid mode)
        sb.AppendLine("// Pointer access checking (paranoid mode)");
        sb.AppendLine("extern void __novus_check_ptr_access(void* ptr, const char* file, int32_t line);");
        sb.AppendLine();

        // VBCC workaround: Comparison wrapper functions
        // VBCC's optimizer can move stack cleanup between comparison and branch, clobbering flags.
        // Using function calls creates sequence points that prevent reordering.
        sb.AppendLine("// VBCC workaround: Comparison wrapper functions");
        sb.AppendLine("extern int32_t __novus_is_null(void* ptr);");
        sb.AppendLine("extern int32_t __novus_cmp_eq_i32(int32_t a, int32_t b);");
        sb.AppendLine("extern int32_t __novus_cmp_ne_i32(int32_t a, int32_t b);");
        sb.AppendLine();

        sb.AppendLine("#endif // NOVUS_TYPES_H");

        return sb.ToString();
    }

    /// <summary>
    /// Returns the collected debug line markers from code generation.
    /// Call this after generating all function files to get statement-level debug info.
    /// </summary>
    public List<(string LabelName, string FileName, int Line, string FuncName)> GetDebugLineMarkers()
    {
        return _debugLineMarkers;
    }

    /// <summary>
    /// Generate a debug symbols C file that maps function and statement addresses to source locations.
    /// This is used by the runtime to display file:line information in crash dialogs.
    /// Must be generated separately from per-function files because it references all functions.
    /// </summary>
    /// <param name="modules">IR modules containing functions</param>
    /// <param name="statementMarkers">Optional statement-level debug markers collected during code generation</param>
    public static string GenerateDebugSymbolsFile(
        IEnumerable<IrModule> modules,
        List<(string LabelName, string FileName, int Line, string FuncName)>? statementMarkers = null)
    {
        var sb = new StringBuilder();

        // Collect all implemented functions with locations from all modules
        var functionsWithLocations = new List<(IrFunction Function, string CName)>();
        foreach (var module in modules)
        {
            foreach (var func in module.Functions)
            {
                // Skip extern functions (no implementation) and functions without locations
                if (func.IsExtern || func.BasicBlocks.Count == 0 || func.Location == null)
                    continue;

                // Compute C function name using the same logic as MangleName
                var cName = MangleNameStatic(func.Name);
                functionsWithLocations.Add((func, cName));
            }
        }

        var hasStatementMarkers = statementMarkers != null && statementMarkers.Count > 0;

        if (functionsWithLocations.Count == 0 && !hasStatementMarkers)
        {
            // Return empty file comment - no debug symbols to emit
            return "// No debug symbols (no functions with source locations)\n";
        }

        sb.AppendLine("// Generated by Novus compiler");
        sb.AppendLine("// Debug Symbol Table for runtime source location lookup");
        if (hasStatementMarkers)
            sb.AppendLine("// Includes statement-level debug markers for precise line numbers");
        sb.AppendLine("// Target: AmigaOS 2.0+ (68020+), C99");
        sb.AppendLine();
        sb.AppendLine("#include \"novus_types.h\"");
        sb.AppendLine();
        sb.AppendLine("// ============================================================================");
        sb.AppendLine("// Debug Symbol Table");
        sb.AppendLine("// ============================================================================");
        sb.AppendLine();

        // Emit the struct definition (matches the runtime's expectation)
        sb.AppendLine("typedef struct {");
        sb.AppendLine("    void* func_addr;       // Function/statement address");
        sb.AppendLine("    const char* file;      // Source file path");
        sb.AppendLine("    uint16_t line;         // Line number");
        sb.AppendLine("    const char* name;      // Function name");
        sb.AppendLine("} NovusDebugSymbol;");
        sb.AppendLine();

        // Declare the registration function from the runtime
        sb.AppendLine("extern void __novus_register_debug_symbols(const NovusDebugSymbol* symbols, uint32_t count);");
        sb.AppendLine();

        // Emit extern declarations for all functions we're referencing
        sb.AppendLine("// Function declarations for symbol table");
        foreach (var (func, cName) in functionsWithLocations)
        {
            // Declare with unspecified parameters (K&R style) - we only need the address
            // Using () instead of (void) because we don't know the actual signature
            sb.AppendLine($"extern void {cName}();");
        }
        sb.AppendLine();

        // Emit extern label declarations for statement markers
        // These labels are emitted as inline asm in the generated C code
        if (hasStatementMarkers)
        {
            sb.AppendLine("// Statement-level debug label declarations");
            sb.AppendLine("// These labels are defined via inline asm in the function bodies");
            foreach (var (labelName, _, _, _) in statementMarkers!)
            {
                sb.AppendLine($"extern void {labelName}(void);");
            }
            sb.AppendLine();
        }

        // Count total symbols (functions + statement markers)
        var totalSymbols = functionsWithLocations.Count + (statementMarkers?.Count ?? 0);

        // Emit the symbol table as static (accessed only via registration)
        sb.AppendLine($"static const NovusDebugSymbol __novus_debug_symbols[] = {{");

        // First emit function entry points
        foreach (var (func, cName) in functionsWithLocations)
        {
            var loc = func.Location!;
            var fileName = EscapeCStringStatic(System.IO.Path.GetFileName(loc.FilePath));
            var funcName = EscapeCStringStatic(func.Name);

            sb.AppendLine($"    {{ (void*){cName}, \"{fileName}\", {loc.Line}, \"{funcName}\" }},");
        }

        // Then emit statement-level markers
        if (hasStatementMarkers)
        {
            sb.AppendLine("    // Statement-level markers for precise line numbers:");
            foreach (var (labelName, fileName, line, funcName) in statementMarkers!)
            {
                var escapedFileName = EscapeCStringStatic(fileName);
                var escapedFuncName = EscapeCStringStatic(funcName);
                sb.AppendLine($"    {{ (void*){labelName}, \"{escapedFileName}\", {line}, \"{escapedFuncName}\" }},");
            }
        }

        sb.AppendLine("};");
        sb.AppendLine();

        // Emit registration function that's called during startup
        sb.AppendLine("void __novus_init_debug_symbols(void) {");
        sb.AppendLine($"    __novus_register_debug_symbols(__novus_debug_symbols, {totalSymbols});");
        sb.AppendLine("}");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Static version of EscapeCString for use in static methods
    /// </summary>
    private static string EscapeCStringStatic(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    /// <summary>
    /// Static version of MangleName for use in static methods
    /// </summary>
    private static string MangleNameStatic(string name)
    {
        // Must match MangleName exactly - handle all the same cases
        // Generic names like "From<DosError>" become "From_DosError"
        // Unit type like "Result<(), Error>" becomes "Result_unit_Error"
        return name.Replace("::", "_")
                   .Replace("()", "unit")    // Handle unit type before removing parens
                   .Replace("<", "_")
                   .Replace(">", "")
                   .Replace(",", "_")
                   .Replace("&", "ref_")
                   .Replace("*", "ptr_")
                   .Replace("(", "")         // Remove any remaining parens
                   .Replace(")", "")
                   .Replace(" ", "");
    }

    /// <summary>
    /// Generate a single-function C file for library modules.
    /// This enables the linker to pull in only the functions that are actually used.
    /// </summary>
    public string GenerateFunctionFile(IrFunction function)
    {
        // Check if function has unresolved types - skip it entirely
        if (HasUnresolvedTypes(function))
        {
            Console.WriteLine($"WARNING: Skipping function file '{function.Name}' due to unresolved types");
            return $"// SKIPPED: Function '{function.Name}' has unresolved types (not used by this build)\n// This function file is not needed.\n";
        }

        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"// Generated by Novus compiler");
        sb.AppendLine($"// Target: AmigaOS 2.0+ (68020+), C99");
        sb.AppendLine();

        // Include shared types header
        if (_useSharedTypesHeader)
        {
            sb.AppendLine("#include \"novus_types.h\"");
            sb.AppendLine();
        }
        else
        {
            // Fallback: emit inline type definitions (no C library dependency)
            EmitInlineTypeDefinitions(sb);
            sb.AppendLine();
        }

        // Forward declarations for FFI struct pointer types
        // These types are defined in NDK headers but need to be forward-declared
        // before they're used in enum variant definitions
        // Use typedef so we can use "Screen*" instead of "struct Screen*"
        // Skip when using shared types header - novus_types.h already has these
        if (!_useSharedTypesHeader)
        {
            var ffiTypes = CollectFFIStructTypesForFunction(function);
            if (ffiTypes.Count > 0)
            {
                sb.AppendLine("// Forward declarations for FFI struct types");
                foreach (var ffiType in ffiTypes.OrderBy(t => t))
                {
                    sb.AppendLine($"typedef struct {ffiType} {ffiType};");
                }
                sb.AppendLine();
            }
        }

        // Note: AmigaOS headers are included via novus_types.h
        // which includes <utility/tagitem.h> and typedefs TagItem

        // Check for generic types (both enums and structs) in the function
        var enumTypes = CollectEnumTypesForFunction(function);
        var genericEnums = enumTypes.Where(e => !IsConcreteEnum(e)).ToList();

        // Also check for generic structs by scanning the function's types
        var allTypes = new List<IrType>();
        allTypes.Add(function.ReturnType);
        allTypes.AddRange(function.Parameters.Select(p => p.Type));
        allTypes.AddRange(function.LocalVariables.Select(l => l.Type));
        var hasGenericStructs = allTypes.Any(t => t is IrStructType st && st.GenericParameters.Count > 0);

        if (genericEnums.Any() || hasGenericStructs)
        {
            // ERROR: Function references generic types that weren't properly monomorphized
            var messages = new List<string>();
            if (genericEnums.Any())
                messages.Add($"generic enums: {string.Join(", ", genericEnums.Select(e => e.Name))}");
            if (hasGenericStructs)
                messages.Add("generic structs");

            Console.WriteLine($"WARNING: Skipping function '{function.Name}' - references un-monomorphized generic types");
            Console.WriteLine($"         {string.Join("; ", messages)}");
            Console.WriteLine($"         This is an IR bug - the function should only reference concrete types.");

            // Generate a stub that calls panic so if it's accidentally called, we get an error
            var stubSb = new StringBuilder();
            stubSb.AppendLine($"// SKIPPED: Function '{function.Name}' references un-monomorphized generic types");
            stubSb.AppendLine($"// Generated by Novus compiler - STUB ONLY");
            stubSb.AppendLine();
            EmitInlineTypeDefinitions(stubSb);
            stubSb.AppendLine();
            stubSb.AppendLine($"void __novus_panic(const char* message, const char* file, int32_t line, int32_t col);");
            stubSb.AppendLine();

            // Generate a stub function signature
            var returnType = GetCType(function.ReturnType);
            var isVoidReturn = returnType == "void";
            var paramList = string.Join(", ", function.Parameters.Select(p => $"{GetCType(p.Type)} {p.Name}"));
            var mangledName = MangleName(function);

            stubSb.AppendLine($"{returnType} {mangledName}({paramList}) {{");
            stubSb.AppendLine($"    __novus_panic(\"Function {function.Name} contains un-monomorphized generic types\", __FILE__, __LINE__, 0);");
            if (!isVoidReturn)
            {
                // Need to return something to satisfy C compiler (unreachable code after panic)
                if (function.ReturnType is IrPointerType)
                    stubSb.AppendLine($"    return NULL;");
                else if (function.ReturnType is IrBoolType)
                    stubSb.AppendLine($"    return false;");
                else if (function.ReturnType is IrIntType || function.ReturnType is IrFloatType || function.ReturnType is IrFixedType)
                    stubSb.AppendLine($"    return 0;");
                else
                    stubSb.AppendLine($"    return ({returnType}){{0}};"); // Zero-initialize struct/enum
            }
            stubSb.AppendLine($"}}");

            return stubSb.ToString();
        }

        if (enumTypes.Count > 0)
        {
            var concreteEnums = enumTypes.Where(e => IsConcreteEnum(e)).ToHashSet();
            if (concreteEnums.Any())
            {
                // CRITICAL: Collect struct types used by enum payloads and emit them BEFORE enums
                // Without this, Result<WindowHandle, Error> would reference WindowHandle but it wouldn't be defined.
                // This mirrors the logic in GenerateSharedTypesHeader (lines 649-680).
                var structsUsedByEnumPayloads = new HashSet<IrStructType>();
                foreach (var enumType in concreteEnums)
                {
                    CollectStructTypesFromEnumPayloads(enumType, structsUsedByEnumPayloads);
                }

                // Filter out NDK structs and extern_type structs (already defined in headers)
                var userStructsForEnums = structsUsedByEnumPayloads
                    .Where(s => {
                        // Skip structs marked with #[extern_type] attribute
                        if (s.Attributes != null && s.Attributes.Has(Novus.SemanticAnalysis.KnownAttributes.ExternType))
                            return false;
                        // Skip NDK structs
                        return !NdkStructs.Contains(s.StructName);
                    })
                    .ToList();

                if (userStructsForEnums.Any())
                {
                    sb.AppendLine("// Struct types used by enum payloads (must be defined before enums)");
                    // Topologically sort to handle struct dependencies
                    var sortedStructs = TopologicalSortStructTypes(userStructsForEnums.ToHashSet());
                    foreach (var structType in sortedStructs)
                    {
                        EmitStructTypeToBuilder(sb, structType);
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("// Enum types used by this function");
                // Sort enum types in dependency order: emit leaf types first, then types that depend on them
                var sortedEnumTypes = TopologicalSortEnumTypes(concreteEnums);
                foreach (var enumType in sortedEnumTypes)
                {
                    EmitEnumTypeToBuilder(sb, enumType);
                }
                sb.AppendLine();
            }
        }

        // Emit module static variables as extern declarations
        // The actual definitions are in the separate {moduleName}_statics.c file
        if (_module.StaticVariables.Count > 0)
        {
            sb.AppendLine("// Module static variables (extern declarations)");
            foreach (var staticVar in _module.StaticVariables)
            {
                EmitStaticVariableExternToBuilder(sb, staticVar);
            }
            sb.AppendLine();
        }

        // Emit string literals (if any)
        if (_stringLiterals.Count > 0)
        {
            sb.AppendLine("// String literals");
            foreach (var literal in _stringLiterals)
            {
                var escaped = EscapeString(literal.Value);
                sb.AppendLine($"static const char {literal.Label}[] = \"{escaped}\";");
            }
            sb.AppendLine();
        }

        // All called functions need extern declarations now that monomorphized functions
        // have unique names and are compiled into separate object files
        var calledFunctionsWithSigs = GetCalledFunctionsWithSignatures(function);
        var externalFunctions = new List<(string FuncName, IrType ReturnType, List<IrValue> Arguments)>();

        foreach (var (funcName, (returnType, arguments)) in calledFunctionsWithSigs.OrderBy(kv => kv.Key))
        {
            // All called functions need extern declarations
            externalFunctions.Add((funcName, returnType, arguments));
        }

        // Emit extern declarations for regular functions
        if (externalFunctions.Count > 0)
        {
            sb.AppendLine("// External function declarations");
            foreach (var (funcName, returnType, arguments) in externalFunctions)
            {
                var funcObj = _module.Functions.FirstOrDefault(f => f.Name == funcName);
                if (funcObj != null)
                {
                    // Handle extern functions
                    if (funcObj.IsExtern)
                    {
                        // AmigaOS library functions (WaitTOF, AllocMem, etc.) are provided by proto headers
                        // which define macros that expand to inline library calls through the library base.
                        // We skip these because the proto headers already handle them.
                        // EXCEPTION: MUI functions (MUI_*) are provided by our stubs, not proto headers.
                        bool isAmigaOSFunction = funcName.Length > 0 &&
                                                 char.IsUpper(funcName[0]) &&
                                                 !funcName.StartsWith("__") &&
                                                 !funcName.StartsWith("MUI_") &&
                                                 !funcObj.IsVariadic;

                        if (isAmigaOSFunction)
                        {
                            // Proto headers define these as macros - no extern declaration needed
                            continue;
                        }

                        // Skip runtime functions already declared in novus_types.h
                        // These have special signatures (e.g., const qualifiers) that differ from IR
                        if (funcName is "__novus_memcpy" or "__novus_memset")
                        {
                            continue;
                        }

                        // Emit declaration for runtime extern functions (write, strlen, etc.)
                        var returnTypeStr = GetCType(funcObj.ReturnType);
                        var parameters = GetParameterList(funcObj, false);
                        sb.AppendLine($"extern {returnTypeStr} {funcName}({parameters});");
                        continue;
                    }

                    // VBCC FIX: Use output parameter for struct/enum returns EXCEPT for extern functions
                    // Extern functions use their actual C signatures (e.g., runtime functions return enums directly)
                    var isStructOrEnumReturn = funcObj.ReturnType is IrStructType or IrEnumType or IrTupleType;
                    var shouldUseOutParam = isStructOrEnumReturn && !funcObj.IsExtern;
                    var returnTypeStr2 = shouldUseOutParam ? "void" : GetCType(funcObj.ReturnType);
                    var parameters2 = GetParameterList(funcObj, shouldUseOutParam);

                    // Don't mangle public cross-module function names - use the plain name
                    // MangleName adds type suffixes for monomorphization, but public functions
                    // have stable names across modules
                    var exportedFuncName = funcObj.IsPublic ? MangleName(funcName) : MangleName(funcObj);
                    sb.AppendLine($"extern {returnTypeStr2} {exportedFuncName}({parameters2});");
                }
                else
                {
                    // Function not in current module - it must be a public function from another module
                    // (private functions can't be called cross-module).
                    // Don't add monomorphization suffixes to public cross-module calls.

                    // WORKAROUND: The IR builder sometimes adds type suffixes to cross-module calls.
                    // Strip them here. For example, "make_tags_TagItem" -> "make_tags"
                    var cleanFuncName = funcName;
                    if (returnType is IrStructType structType)
                    {
                        // Try to extract type args from struct name or CacheKey
                        List<string> typeArgs = new List<string>();

                        if (!string.IsNullOrEmpty(structType.CacheKey))
                        {
                            typeArgs = ExtractTypeArguments(structType.CacheKey);
                        }

                        // If we found type args, try to strip them from the function name
                        if (typeArgs.Count > 0)
                        {
                            var suffix = "_" + string.Join("_", typeArgs);
                            if (cleanFuncName.EndsWith(suffix))
                            {
                                cleanFuncName = cleanFuncName.Substring(0, cleanFuncName.Length - suffix.Length);
                            }
                        }
                        else if (!string.IsNullOrEmpty(structType.StructName))
                        {
                            // Fallback: check if function name ends with "_" + PascalCase word
                            var lastUnderscore = cleanFuncName.LastIndexOf('_');
                            if (lastUnderscore > 0 && lastUnderscore < cleanFuncName.Length - 1)
                            {
                                var potentialTypeSuffix = cleanFuncName.Substring(lastUnderscore + 1);
                                // Check if it looks like a type name (starts with uppercase)
                                if (char.IsUpper(potentialTypeSuffix[0]))
                                {
                                    cleanFuncName = cleanFuncName.Substring(0, lastUnderscore);
                                }
                            }
                        }
                    }

                    // Apply VBCC out-parameter workaround for struct/enum returns
                    var isStructOrEnumReturn = returnType is IrStructType or IrEnumType or IrTupleType;
                    var returnTypeStr = isStructOrEnumReturn ? "void" : GetCType(returnType);
                    var paramTypes = arguments.Select(arg => GetCType(arg.Type)).ToList();

                    // If using out-parameter, add output parameter as first parameter
                    if (isStructOrEnumReturn)
                    {
                        paramTypes.Insert(0, GetCType(returnType) + "* __out");
                    }

                    var paramList = paramTypes.Count > 0 ? string.Join(", ", paramTypes) : "void";
                    // Use raw function name without monomorphization suffixes
                    // (only apply basic :: and <> sanitization)
                    sb.AppendLine($"extern {returnTypeStr} {MangleName(cleanFuncName)}({paramList});");
                }
            }
            sb.AppendLine();
        }

        // Function implementation
        // EmitFunction writes to _output, so we need to capture its output
        // We'll temporarily redirect output
        var functionOutput = new StringBuilder();
        EmitFunctionToBuilder(functionOutput, function);
        sb.Append(functionOutput.ToString());

        return sb.ToString();
    }

    /// <summary>
    /// Emit a function to a specific StringBuilder (used for per-function file generation)
    /// </summary>
    private void EmitFunctionToBuilder(StringBuilder targetBuilder, IrFunction function)
    {
        // Set current function for defer cleanup
        _currentEmittingFunction = function;
        _declaredVariables.Clear();
        _memberAccessInfo.Clear();
        _activatedDeferBlocks.Clear();
        _indexAccessInfo.Clear();
        _fieldAccessChainInfo.Clear();
        _addressOnlyStructMemberAccess.Clear();
        _inlineableComparisons.Clear();  // VBCC workaround: reset comparison tracking

        // Reset debug line tracking for this function
        _lastEmittedDebugLine = -1;
        _debugLabelCounter = 0;
        // Reset #line directive tracking for this function
        _lastLineDirectiveLine = -1;
        _lastLineDirectiveFile = null;

        // Track which parameters were converted to pointers in the C signature
        _pointerConvertedParameters.Clear();
        foreach (var param in function.Parameters)
        {
            if (param.Type is IrStructType structType && TypeContainsHeapData(structType))
            {
                _pointerConvertedParameters.Add(param.Name);
            }
        }

        // Run liveness analysis for variable slot reuse
        var livenessAnalysis = new LivenessAnalysis(function);
        var (variableToSlot, slotTypes) = livenessAnalysis.Analyze();
        _variableToSlot = variableToSlot;
        _slotTypes = slotTypes;

        // Pre-scan function for address-only struct member accesses (VBCC 68K fix)
        // IMPORTANT: This must be called AFTER liveness analysis sets up _variableToSlot,
        // so that SanitizeVariableName correctly maps IR names to slot names.
        ScanForAddressOnlyStructMemberAccesses(function);

        // Dead label elimination: collect all branch targets
        // Labels not targeted by any branch will be skipped during emission (VBCC warning 148)
        CollectReachableLabels(function);

        // VBCC FIX: For struct/enum/tuple returns on 68k, use output parameter pattern
        // VBCC can't reliably return structs by value on 68k
        var isStructOrEnumReturn = function.ReturnType is IrStructType or IrEnumType or IrTupleType or IrTupleType;
        var shouldUseOutParam = isStructOrEnumReturn;
        var returnType = shouldUseOutParam ? "void" : GetCType(function.ReturnType);
        var parameters = GetParameterList(function, shouldUseOutParam);
        var funcName = MangleName(function);

        // Special case: main must return 'int' for VBCC compatibility
        if (funcName == "main" && returnType == "int32_t")
        {
            returnType = "int";
        }

        // Monomorphized trait implementations are generated in multiple modules.
        // Don't try to use inline/static - just let them be regular functions and
        // handle the duplicate symbols at the Program.cs level by generating them once.
        targetBuilder.AppendLine($"{returnType} {funcName}({parameters}) {{");

        // Emit defer activation flags (for proper control flow tracking)
        for (int i = 0; i < function.DeferredBlocks.Count; i++)
        {
            targetBuilder.AppendLine($"    bool _defer_{i + 1}_active = false;");
        }

        // Emit slot declarations for temporaries that have been consolidated
        // These are declared at function scope since they may be reused across the function
        foreach (var (slotName, slotType) in slotTypes)
        {
            // Skip unit type () / void - it has no runtime representation and can't be stored in a variable
            if (slotType is IrVoidType)
                continue;
            if (slotType is IrTupleType tupleType && tupleType.ElementTypes.Count == 0)
                continue;

            // DEFENSE IN DEPTH: Zero-initialize ALL slot variables.
            // 1. Structs/enums MUST use {0} for 68k alignment (VBCC ensures 2-byte alignment for initialized data)
            // 2. Primitives should also be zero-initialized as defense against liveness analysis bugs
            // This prevents UB from uninitialized variable access if control flow analysis is incorrect.
            var decl = (slotType is IrEnumType || slotType is IrStructType || slotType is IrTupleType)
                ? GetCVariableDeclaration(slotType, slotName, "= {0}")
                : GetCVariableDeclaration(slotType, slotName, "= 0");

            targetBuilder.AppendLine($"    {decl};");
            _declaredVariables.Add(slotName);
        }

        // Find all variables referenced by defer blocks and pre-declare them at function scope
        // This ensures defer cleanup code can reference these variables even if they're
        // declared in inner scopes (e.g., match arms) that may not be active
        var deferReferencedVars = new Dictionary<string, IrType>(); // var name (UNSANITIZED) -> type

        foreach (var deferBlock in function.DeferredBlocks)
        {
            foreach (var instruction in deferBlock.Instructions)
            {
                // Extract variable references from defer block instructions
                CollectVariableReferences(instruction, deferReferencedVars);
            }
        }

        // Helper to check if a variable is mapped to a slot (i.e., is a temporary)
        bool IsMappedToSlot(string varName)
        {
            return variableToSlot.ContainsKey(varName);
        }

        // CRITICAL FIX: Pre-declare ALL local variables at function start
        // This ensures variables are declared before they're used, regardless of control flow.
        // In C, labels can be jumped to out of order, but variables must be declared before use.
        // For loops are a common case: the init block declares a variable, but control flow
        // jumps to the condition label first where the variable is used.

        // Collect all non-temporary variables from IrLocalDecl and IrStore instructions
        var localDeclVars = new Dictionary<string, (IrType Type, bool IsArray, int ArraySize)>(); // from IrLocalDecl
        var storedVars = new Dictionary<string, IrType>(); // from IrStore (for variables not declared via IrLocalDecl)

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLocalDecl localDecl)
                {
                    // Skip if this variable is mapped to a slot (check ORIGINAL name since liveness uses original names)
                    if (IsMappedToSlot(localDecl.Name))
                        continue;

                    // Skip void types - they have no runtime representation
                    if (localDecl.Type is IrVoidType)
                        continue;

                    // Skip unit type () variables - they have no runtime representation
                    if (localDecl.Type is IrTupleType tupleType && tupleType.ElementTypes.Count == 0)
                        continue;

                    // BUG FIX: Skip if this variable is a module static - don't shadow it with a local
                    if (IsModuleStaticVariable(localDecl.Name))
                        continue;

                    var varName = SanitizeVariableName(localDecl.Name);
                    if (!localDeclVars.ContainsKey(varName))
                    {
                        // Track array information for proper declaration
                        var isArray = localDecl.Type is IrArrayType;
                        var arraySize = isArray ? ((IrArrayType)localDecl.Type).Length : 0;
                        localDeclVars[varName] = (localDecl.Type, isArray, arraySize);
                    }
                }
                else if (instruction is IrStore store)
                {
                    // Skip void types - they have no runtime representation
                    if (store.Value.Type is IrVoidType)
                        continue;

                    // Skip if this variable is mapped to a slot (check ORIGINAL name since liveness uses original names)
                    if (IsMappedToSlot(store.VariableName))
                        continue;

                    // BUG FIX: Skip if this variable is a module static - don't shadow it with a local
                    if (IsModuleStaticVariable(store.VariableName))
                        continue;

                    var varName = SanitizeVariableName(store.VariableName);
                    if (!storedVars.ContainsKey(varName))
                    {
                        storedVars[varName] = store.Value.Type;
                    }
                }
            }
        }

        // LIFETIME FIX: Analyze which arrays are filled with compile-time constant values
        // via IrIndexStore instructions. Such arrays can be made 'static' to ensure they
        // live for the entire program duration - critical when passed to external functions
        // (like MUI's make_radio) that may store pointers to the array.
        var staticConstArrays = AnalyzeStaticConstantArrays(function, localDeclVars);

        // Emit declarations for variables referenced by defer blocks
        // These must be declared at function scope so defer cleanup can access them
        // even if they're originally declared in inner scopes (e.g., match arms)
        foreach (var (varName, varType) in deferReferencedVars)
        {
            var sanitizedName = SanitizeVariableName(varName);
            if (!localDeclVars.ContainsKey(sanitizedName))
            {
                var decl = GetCVariableDeclaration(varType, sanitizedName);
                targetBuilder.AppendLine($"    {decl};  // Pre-declared for defer cleanup");
                _declaredVariables.Add(sanitizedName);
                localDeclVars[sanitizedName] = (varType, false, 0);  // Mark as declared to avoid double-declaration
            }
        }

        // Pre-declare all variables from IrLocalDecl at function start
        // This ensures they're available regardless of control flow order
        foreach (var (varName, varInfo) in localDeclVars)
        {
            // Skip if already declared (e.g., by defer pre-declaration above)
            if (_declaredVariables.Contains(varName))
                continue;

            var (varType, isArray, arraySize) = varInfo;

            if (isArray)
            {
                // Array declarations need special syntax
                var arrayType = (IrArrayType)varType;
                var elementType = GetCType(arrayType.ElementType);

                // LIFETIME FIX: Arrays filled with compile-time constants should be 'static'
                // to ensure they persist for the entire program duration. This is critical
                // when the array is passed to external functions (like MUI's make_radio)
                // that may store pointers to the array.
                var staticPrefix = staticConstArrays.Contains(varName) ? "static " : "";
                targetBuilder.AppendLine($"    {staticPrefix}{elementType} {varName}[{arraySize}];");
            }
            else
            {
                // CRITICAL FIX FOR 68K ALIGNMENT:
                // Structs, enums, and tuples MUST use {0} initializer to guarantee proper alignment on 68000.
                // VBCC ensures 2-byte alignment for initialized structs/enums, preventing Guru Meditation.
                var decl = (varType is IrEnumType || varType is IrStructType || varType is IrTupleType)
                    ? GetCVariableDeclaration(varType, varName, "= {0}")
                    : GetCVariableDeclaration(varType, varName);

                targetBuilder.AppendLine($"    {decl};");
            }
            _declaredVariables.Add(varName);
        }

        // Also emit declarations for variables that are stored to but never declared via IrLocalDecl
        // (common for match result variables when match is lowered to basic blocks)
        foreach (var (varName, varType) in storedVars)
        {
            if (!localDeclVars.ContainsKey(varName))
            {
                // CRITICAL FIX FOR 68K ALIGNMENT:
                // Structs, enums, and tuples MUST use {0} initializer to guarantee proper alignment on 68000.
                var decl = (varType is IrEnumType || varType is IrStructType || varType is IrTupleType)
                    ? GetCVariableDeclaration(varType, varName, "= {0}")
                    : GetCVariableDeclaration(varType, varName);

                targetBuilder.AppendLine($"    {decl};");
                _declaredVariables.Add(varName);
            }
        }

        // MMU protection: Initialize at start of main() if memory tracking is enabled
        if (_safetyLevel.EnableMemoryTracking() && funcName == "main")
        {
            targetBuilder.AppendLine("    // Initialize MMU protection (null page, guard pages)");
            targetBuilder.AppendLine("    __novus_init_mmu_protection();");
        }

        // Emit all basic blocks
        foreach (var block in function.BasicBlocks)
        {
            // Emit the block's label if it's a target of a branch (not just the entry block)
            // This is needed for derived methods that generate multi-block control flow
            if (block.Label != "entry" && _reachableLabels.Contains(block.Label))
            {
                var labelName = block.Label + _labelSuffix;
                targetBuilder.AppendLine($"{labelName}:;");
            }

            foreach (var instruction in block.Instructions)
            {
                // We need to emit instructions to the target builder
                // For now, just call EmitInstruction and capture from _output
                // This is a limitation - ideally EmitInstruction would take a StringBuilder
                var beforeLength = _output.Length;
                EmitInstruction(instruction);
                var emitted = _output.ToString().Substring(beforeLength);
                targetBuilder.Append(emitted);

                // Clear what we just appended from _output
                _output.Length = beforeLength;
            }
        }

        // Emit deferred cleanup at end of function ONLY if the last instruction is not a return
        var lastBlock = function.BasicBlocks.LastOrDefault();
        var lastInstruction = lastBlock?.Instructions.LastOrDefault();
        var endsWithReturn = lastInstruction is IrReturn;

        if (!endsWithReturn)
        {
            var beforeCleanupLength = _output.Length;
            EmitDeferredCleanup(function, 1);
            var cleanupEmitted = _output.ToString().Substring(beforeCleanupLength);
            targetBuilder.Append(cleanupEmitted);
            _output.Length = beforeCleanupLength;
        }

        targetBuilder.AppendLine("}");

        // Clear current function and liveness data
        _currentEmittingFunction = null;
        _variableToSlot = null;
        _slotTypes = null;
    }

    /// <summary>
    /// Generate a C file containing module static variable definitions.
    /// This file is compiled once and linked with all functions that reference the statics.
    /// </summary>
    public string GenerateStaticsFile()
    {
        if (_module.StaticVariables.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"// Generated by Novus compiler");
        sb.AppendLine($"// Target: AmigaOS 2.0+ (68020+), C99");
        sb.AppendLine($"// Module static variable definitions");
        sb.AppendLine();

        // Include shared types header
        if (_useSharedTypesHeader)
        {
            sb.AppendLine("#include \"novus_types.h\"");
            sb.AppendLine();
        }
        else
        {
            // Fallback: emit inline type definitions (no C library dependency)
            EmitInlineTypeDefinitions(sb);
            sb.AppendLine();
        }

        // Collect struct types used by static variables that need to be defined in the statics file.
        // We must recursively collect ALL struct types (including nested field types) and emit
        // them in topological order (dependencies first).
        var staticStructTypes = new HashSet<IrStructType>();
        var visitedStructs = new HashSet<string>();
        foreach (var staticVar in _module.StaticVariables)
        {
            CollectStructDependenciesRecursively(staticVar.Type, staticStructTypes, visitedStructs);
        }

        // Topologically sort struct types so dependencies come before dependents
        var sortedStructTypes = TopologicalSortStructTypes(staticStructTypes);

        // Emit struct typedefs for static variables
        if (sortedStructTypes.Count > 0)
        {
            sb.AppendLine("// Struct types for static variables");
            foreach (var structType in sortedStructTypes)
            {
                EmitStructTypeToBuilder(sb, structType);
            }
            sb.AppendLine();
        }

        // Emit all module static variables with their definitions
        sb.AppendLine("// Module static variables");
        foreach (var staticVar in _module.StaticVariables)
        {
            EmitStaticVariableToBuilder(sb, staticVar);
        }
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Helper to emit enum type to a specific StringBuilder (for shared header generation)
    /// </summary>
    private void EmitEnumTypeToBuilder(StringBuilder sb, IrEnumType enumType)
    {
        var enumName = MangleName(enumType);

        // Skip enums with no variants (stub/placeholder enums)
        // This can happen when a struct field references an enum type before the enum is fully resolved
        if (enumType.Variants.Count == 0)
        {
            return;
        }

        // Check if this enum has any associated data
        bool hasAnyData = enumType.Variants.Any(v => v.HasAssociatedData);

        // Add header guard to prevent redefinition (matches the guard used in .c files)
        var guardName = $"NOVUS_TYPE_{enumName.ToUpper()}_DEFINED";
        sb.AppendLine($"#ifndef {guardName}");
        sb.AppendLine($"#define {guardName}");
        sb.AppendLine($"// Enum: {enumType.Name}");

        if (!hasAnyData)
        {
            // OPTIMIZATION: For enums with no associated data, use plain C enum
            sb.AppendLine($"typedef enum {{");
            for (int i = 0; i < enumType.Variants.Count; i++)
            {
                var variant = enumType.Variants[i];
                var comma = i < enumType.Variants.Count - 1 ? "," : "";
                sb.AppendLine($"    {enumName}_{variant.Name} = {variant.Tag}{comma}");
            }
            sb.AppendLine($"}} {enumName};");
            sb.AppendLine($"#endif // {guardName}");
            sb.AppendLine();
            return;
        }

        // For enums WITH associated data, use the full struct+union representation
        sb.AppendLine($"enum {enumName}_Tag {{");
        for (int i = 0; i < enumType.Variants.Count; i++)
        {
            var variant = enumType.Variants[i];
            var comma = i < enumType.Variants.Count - 1 ? "," : "";
            sb.AppendLine($"    {enumName}_{variant.Name} = {variant.Tag}{comma}");
        }
        sb.AppendLine("};");
        sb.AppendLine();

        // BUG FIX #4: Check if any variant has associated data before generating union
        bool anyVariantHasData = enumType.Variants.Any(v => v.HasAssociatedData);

        if (anyVariantHasData)
        {
            sb.AppendLine($"union {enumName}_Data {{");
            foreach (var variant in enumType.Variants)
            {
                if (variant.HasAssociatedData)
                {
                    // BUG FIX #3: Validate AssociatedData before accessing
                    if (variant.AssociatedData == null)
                    {
                        throw new InvalidOperationException($"Variant '{variant.Name}' has HasAssociatedData=true but AssociatedData is null");
                    }

                    sb.AppendLine($"    struct {{");

                    // Check if this is a unit type - if so, generate struct with dummy field
                    // Unit type () is represented as a single IrTupleType with 0 elements
                    bool isUnitType = variant.AssociatedData.Count == 1 &&
                                     variant.AssociatedData[0] is IrTupleType tupleType &&
                                     tupleType.ElementTypes.Count == 0;

                    if (!isUnitType)
                    {
                        // Use aligned field emission to prevent Address Error on 68000
                        EmitAlignedVariantFields(sb, variant.AssociatedData, "        ");
                    }
                    else
                    {
                        // VBCC doesn't support empty structs, so add a dummy field
                        sb.AppendLine($"        char _dummy;");
                    }

                    sb.AppendLine($"    }} {variant.Name};");
                }
            }
            sb.AppendLine("};");
            sb.AppendLine();
        }

        sb.AppendLine($"typedef struct {{");
        sb.AppendLine($"    enum {enumName}_Tag tag;");
        if (anyVariantHasData)
        {
            sb.AppendLine($"    union {enumName}_Data data;");
        }
        sb.AppendLine($"}} {enumName};");
        sb.AppendLine($"#endif // {guardName}");
        sb.AppendLine();
    }

    /// <summary>
    /// Helper to emit static variable to a specific StringBuilder (for statics file generation).
    /// Note: Does NOT emit 'static' keyword - all variables in the statics file must be
    /// visible to other translation units (function files).
    /// </summary>
    private void EmitStaticVariableToBuilder(StringBuilder sb, IrStaticVariable staticVar)
    {
        // Do NOT use 'static' keyword - these need to be visible to other .o files
        var constKeyword = !staticVar.IsMutable ? "const" : "";

        // Memory section attribute for VBCC
        // For chip RAM (required for DMA: Copper, Blitter, audio, sprites), we need to use
        // __section("DATA_C") to create a proper MEMF_CHIP hunk. The __chip attribute doesn't
        // work correctly in VBCC - it just puts data in CODE section without chip flag.
        // DATA_C suffix creates a hunk with HUNKF_CHIP flag set.
        var sectionAttr = staticVar.Section switch
        {
            MemorySection.Chip => "__section(\"DATA_C\") ",
            MemorySection.Fast => "", // VBCC default is any memory, no specific attribute for fast-only
            _ => ""
        };

        // Emit the declaration with initialization
        var keywordStr = !string.IsNullOrEmpty(constKeyword) ? constKeyword + " " : "";

        // Special handling for arrays - use array syntax instead of pointer syntax
        if (staticVar.Type is IrArrayType arrayType)
        {
            var initialValue = EmitValue(staticVar.InitialValue);
            var elementType = GetCType(arrayType.ElementType);
            var size = arrayType.Length;
            sb.AppendLine($"{sectionAttr}{keywordStr}{elementType} {staticVar.Name}[{size}] = {initialValue};");
        }
        // VBCC FIX: For struct literals, use designated initializer syntax WITHOUT the type cast
        // VBCC doesn't support compound literal syntax `(Type){ ... }` for static initialization
        else if (staticVar.InitialValue is IrStructLiteral structLit)
        {
            var cType = GetCType(staticVar.Type);

            // VBCC/68k OPTIMIZATION: If the struct is all-zero initialized, emit {0} instead of
            // the full compound literal. This is critical for large structs (like ChipCache with
            // 128 CacheEntry elements = ~8KB) because VBCC's linker can fail to properly place
            // huge compound literals, resulting in the global being at address 0 (NULL).
            // C99 guarantees {0} initializes the entire struct to zero, and the Amiga loader
            // will properly zero BSS sections.
            //
            // VBCC BUG WORKAROUND: Also use {0} for very large structs (>4KB) regardless of content,
            // because VBCC's code generator produces broken initialization code for massive literals.
            // This can cause address errors (e.g., crash at address 0x6) on the Amiga.
            var structSize = GetStructSizeEstimate(staticVar.Type);
            if (IsAllZeroStruct(structLit) || structSize > 4096)
            {
                sb.AppendLine($"{sectionAttr}{keywordStr}{cType} {staticVar.Name} = {{0}};");
            }
            else
            {
                // Use EmitStructLiteralForInitializer which omits the type cast
                var initValue = EmitStructLiteralForInitializer(structLit);
                sb.AppendLine($"{sectionAttr}{keywordStr}{cType} {staticVar.Name} = {initValue};");
            }
        }
        else
        {
            var initialValue = EmitValue(staticVar.InitialValue);
            var cType = GetCType(staticVar.Type);
            sb.AppendLine($"{sectionAttr}{keywordStr}{cType} {staticVar.Name} = {initialValue};");
        }
    }

    /// <summary>
    /// Helper to emit static variable as extern declaration (for per-function files)
    /// </summary>
    private void EmitStaticVariableExternToBuilder(StringBuilder sb, IrStaticVariable staticVar)
    {
        var constKeyword = !staticVar.IsMutable ? "const" : "";
        var keywordStr = !string.IsNullOrEmpty(constKeyword) ? constKeyword + " " : "";

        // Special handling for arrays - use array syntax instead of pointer syntax
        if (staticVar.Type is IrArrayType arrayType)
        {
            var elementType = GetCType(arrayType.ElementType);
            var size = arrayType.Length;
            sb.AppendLine($"extern {keywordStr}{elementType} {staticVar.Name}[{size}];");
        }
        else
        {
            var cType = GetCType(staticVar.Type);
            sb.AppendLine($"extern {keywordStr}{cType} {staticVar.Name};");
        }
    }

    /// <summary>
    /// Helper to emit struct type to a specific StringBuilder (for shared header generation)
    /// </summary>
    private void EmitTupleTypeToBuilder(StringBuilder sb, IrTupleType tupleType)
    {
        var tupleName = GetTupleTypeName(tupleType);

        // Generate C struct definition for tuple
        sb.AppendLine($"typedef struct {{");
        for (int i = 0; i < tupleType.ElementTypes.Count; i++)
        {
            var elementType = GetCType(tupleType.ElementTypes[i]);
            sb.AppendLine($"    {elementType} __{i};");
        }
        sb.AppendLine($"}} {tupleName};");
        sb.AppendLine();
    }

    /// <summary>
    /// Helper to emit closure type (fat pointer struct) to a StringBuilder.
    /// Closures are 8-byte fat pointers containing a function pointer and environment pointer.
    /// </summary>
    private void EmitClosureTypeToBuilder(StringBuilder sb, IrClosureType closureType)
    {
        var closureName = GetClosureTypeName(closureType);

        // Generate function pointer type string for the closure's function
        // The closure function takes (void* env, param1, param2, ...) and returns the closure's return type
        var returnType = GetCType(closureType.ReturnType);
        var paramsWithEnv = new List<string> { "void*" };
        paramsWithEnv.AddRange(closureType.ParameterTypes.Select(GetCType));
        var paramStr = string.Join(", ", paramsWithEnv);

        // Emit the closure struct type
        sb.AppendLine($"// Closure type: closure({string.Join(", ", closureType.ParameterTypes.Select(t => t.Name))}) -> {closureType.ReturnType.Name}");
        sb.AppendLine($"typedef struct {{");
        sb.AppendLine($"    {returnType} (*fn_ptr)({paramStr});  // Function pointer (takes env as first arg)");
        sb.AppendLine($"    void* env_ptr;                       // Environment pointer (null if stateless)");
        sb.AppendLine($"}} {closureName};");
        sb.AppendLine();
    }

    private void EmitStructTypeToBuilder(StringBuilder sb, IrStructType structType)
    {
        var structName = MangleName(structType);
        _currentEmittingStruct = structName;  // For error messages

        // Add header guard to prevent redefinition (matches pattern used for enums)
        var guardName = $"NOVUS_TYPE_{structName.ToUpper()}_DEFINED";
        sb.AppendLine($"#ifndef {guardName}");
        sb.AppendLine($"#define {guardName}");
        sb.AppendLine($"// Struct: {structType.Name}");

        // Check for #[packed] attribute
        var isPacked = structType.Attributes?.Has("packed") ?? false;

        // Use named struct instead of anonymous to allow self-references
        // VBCC uses #pragma pack(1) for packed structs (not __attribute__)
        if (isPacked)
        {
            sb.AppendLine($"#pragma pack(1)");
        }
        sb.AppendLine($"struct {structName} {{");

        // Track current offset and max alignment for padding calculation
        int currentOffset = 0;
        int maxFieldAlignment = 1;
        int paddingFieldIndex = 0;

        foreach (var field in structType.Fields)
        {
            int fieldSize;
            int fieldAlign;

            // Special handling for array fields - need T[n] syntax, not T*
            if (field.Type is IrArrayType arrayType)
            {
                var elemSize = CalculateTypeSize(arrayType.ElementType);
                fieldSize = elemSize * arrayType.Length;
                fieldAlign = Math.Min(elemSize, 2); // 68k uses 2-byte alignment

                // Add explicit padding before field if needed (for 68000 alignment)
                if (!isPacked && fieldAlign > 1 && currentOffset % fieldAlign != 0)
                {
                    int paddingBytes = fieldAlign - (currentOffset % fieldAlign);
                    sb.AppendLine($"    uint8_t _pad{paddingFieldIndex}[{paddingBytes}];  // alignment padding");
                    currentOffset += paddingBytes;
                    paddingFieldIndex++;
                }

                var elementType = GetCType(arrayType.ElementType);
                sb.AppendLine($"    {elementType} {field.Name}[{arrayType.Length}];");
            }
            else
            {
                fieldSize = CalculateTypeSize(field.Type);
                fieldAlign = fieldSize >= 4 ? 2 : Math.Min(fieldSize, 2); // 68k uses 2-byte alignment

                // Add explicit padding before field if needed (for 68000 alignment)
                if (!isPacked && fieldAlign > 1 && currentOffset % fieldAlign != 0)
                {
                    int paddingBytes = fieldAlign - (currentOffset % fieldAlign);
                    sb.AppendLine($"    uint8_t _pad{paddingFieldIndex}[{paddingBytes}];  // alignment padding");
                    currentOffset += paddingBytes;
                    paddingFieldIndex++;
                }

                var fieldType = GetCType(field.Type);
                sb.AppendLine($"    {fieldType} {field.Name};");
            }

            currentOffset += fieldSize;
            maxFieldAlignment = Math.Max(maxFieldAlignment, fieldAlign);
        }

        // Add trailing padding for non-packed, non-NDK structs to ensure proper array alignment
        // This is CRITICAL for 68k: when structs are used in arrays, each element must start
        // at an address with proper alignment for the struct's largest field.
        if (!isPacked && maxFieldAlignment > 1)
        {
            int paddingNeeded = (maxFieldAlignment - (currentOffset % maxFieldAlignment)) % maxFieldAlignment;
            if (paddingNeeded > 0)
            {
                sb.AppendLine($"    uint8_t _pad[{paddingNeeded}];  // Padding for 68k array alignment");
            }
        }

        sb.AppendLine($"}};");
        if (isPacked)
        {
            sb.AppendLine($"#pragma pack()");
        }
        // Add typedef so we can use the struct name without "struct" prefix
        // This is essential for per-function files where forward declarations aren't shared
        sb.AppendLine($"typedef struct {structName} {structName};");
        sb.AppendLine($"#endif // {guardName}");
        sb.AppendLine();

        _currentEmittingStruct = null;  // Clear after emitting
    }

    /// <summary>
    /// Collect all enum types used by a single function (for per-function file generation)
    /// </summary>
    private HashSet<IrEnumType> CollectEnumTypesForFunction(IrFunction function)
    {
        var enumTypes = new HashSet<IrEnumType>();

        // Check return type (with transitive detection)
        CollectEnumTypesFromType(function.ReturnType, enumTypes);

        // Check parameters (with transitive detection)
        foreach (var param in function.Parameters)
        {
            CollectEnumTypesFromType(param.Type, enumTypes);
        }

        // Check local variables (with transitive detection)
        foreach (var local in function.LocalVariables)
        {
            CollectEnumTypesFromType(local.Type, enumTypes);
        }

        // Scan instructions for enum values
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Check for IrLocalDecl with enum types
                if (instruction is IrLocalDecl localDecl)
                    CollectEnumTypesFromType(localDecl.Type, enumTypes);

                // Check for IrMatch to get the matched enum type
                if (instruction is IrMatch match)
                    CollectEnumTypesFromType(match.MatchValue.Type, enumTypes);

                // Check for function calls that might use enum types
                if (instruction is IrCall call)
                {
                    CollectEnumTypesFromType(call.ReturnType, enumTypes);

                    foreach (var arg in call.Arguments)
                    {
                        CollectEnumTypesFromType(arg.Type, enumTypes);
                    }
                }
            }
        }

        return enumTypes;
    }

    /// <summary>
    /// Collect all FFI struct pointer types used by a single function for forward declarations
    /// </summary>
    private HashSet<string> CollectFFIStructTypesForFunction(IrFunction function)
    {
        var ffiTypes = new HashSet<string>();

        // Check return type
        CollectFFIStructTypesFromType(function.ReturnType, ffiTypes);

        // Check parameters
        foreach (var param in function.Parameters)
        {
            CollectFFIStructTypesFromType(param.Type, ffiTypes);
        }

        // Check local variables
        foreach (var local in function.LocalVariables)
        {
            CollectFFIStructTypesFromType(local.Type, ffiTypes);
        }

        // Scan instructions
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLocalDecl localDecl)
                    CollectFFIStructTypesFromType(localDecl.Type, ffiTypes);

                if (instruction is IrCall call)
                {
                    CollectFFIStructTypesFromType(call.ReturnType, ffiTypes);
                    foreach (var arg in call.Arguments)
                    {
                        CollectFFIStructTypesFromType(arg.Type, ffiTypes);
                    }
                }
            }
        }

        return ffiTypes;
    }

    /// <summary>
    /// CANONICAL LIST: NDK struct types defined by AmigaOS headers.
    ///
    /// This is the SINGLE SOURCE OF TRUTH for NDK struct names.
    /// These structs are:
    /// 1. Defined in AmigaOS SDK headers (not generated by Novus)
    /// 2. Referenced in std/ffi/amiga_structs.novus with #[extern_type] attribute
    /// 3. Used for forward declaration generation in novus_types.h
    /// 4. Used to filter out structs from user code generation
    ///
    /// WHEN ADDING NEW NDK STRUCTS:
    /// 1. Add the struct name to this list
    /// 2. Add the typedef to EmitNdkTypedefs() method (~line 475)
    /// 3. Define the struct in std/ffi/amiga_structs.novus with #[extern_type]
    ///
    /// See also: EmitNdkTypedefs() for the typedef emission that must be kept in sync.
    /// </summary>
    private static readonly HashSet<string> NdkStructs = new() {
        // Core exec types
        "Rectangle", "BitMap", "View", "ViewPort", "ColorMap", "UCopList",
        "Node", "MinNode", "Library", "List", "MinList", "TagItem", "Hook",
        "Task", "StackSwapStruct", "SignalSemaphore", "MsgPort", "Message",
        "Device", "Unit", "ExtendedNode", "Custom", "SpriteDef", "DBufInfo",
        "SimpleSprite",
        // Graphics types
        "TextAttr", "TTextAttr", "TextFont", "TextFontExtension",
        "ColorFontColors", "ColorTextFont", "TextExtent", "Layer", "RastPort",
        "AreaInfo", "TmpRas", "ClipRect", "Region", "RegionRectangle",
        "CopIns", "CopList", "cprlist", "copinit", "RasInfo", "Layer_Info",
        "ExtSprite", "MonitorSpec", "bltnode",
        // Intuition types
        "IBox", "DrawInfo", "Gadget", "Requester", "NewScreen", "Screen",
        "Window", "IntuiMessage", "NewWindow", "GadgetInfo", "IntuiText",
        // GadTools types
        "NewMenu", "Menu", "MenuItem", "VisualInfo", "NewGadget",
        // DOS types
        "IORequest", "IOStdReq", "timeval", "EClockVal", "timerequest",
        "DateStamp", "FileInfoBlock", "InfoData", "FileHandle", "DosPacket",
        "StandardPacket", "ErrorString", "RootNode", "DosLibrary", "CliProcList",
        "DosInfo", "Segment", "CommandLineInterface", "DeviceList", "DevInfo",
        "DosList", "AssignList", "DevProc", "FileLock", "RecordLock", "AChain",
        "AnchorPath", "LocalVar", "NotifyRequest", "NotifyMessage", "DateTime",
        "ExAllData", "ExAllControl", "Process",
        // Input types
        "IEPointerPixel", "IEPointerTablet", "InputEvent",
        // Intuition extended types
        "PubScreenNode", "EasyStruct",
        // Interrupt types
        "Interrupt", "IntVector", "SoftIntList",
        // BOOPSI types
        "opSet", "opUpdate", "opGet", "opAddTail", "opMember",
        // Rexx types
        "NexxStr", "RexxArg", "RexxMsg", "RexxRsrc", "RexxTask", "SrcNode",
        "RxsLib", "IoBuff", "RexxMsgPort",
        // Misc types
        "impFrameBox", "MemChunk", "MemHeader", "MemEntry", "MemList",
        "CSource", "RDArgs", "OldDrawerData", "DrawerData", "DiskObject",
        "FreeList", "WBArg", "AppMessage", "AppWindow", "AppWindowDropZone",
        "AppIcon", "AppMenuItem", "AppMenu", "WBStartup",
        // Clipboard types
        "ClipboardUnitPartial", "IOClipReq", "SatisfyMsg", "ClipHookMsg",
        // DRP types
        "DRPSourceMsg",
        // Parallel/Serial types
        "IOPArray", "IOExtPar", "IOTArray", "IOExtSer",
        // Expansion types
        "ExpansionRom", "ExpansionControl", "DiagArea", "ConfigDev",
        "CurrentBinding", "DosEnvec", "FileSysStartupMsg", "DeviceNode",
        // Disk resource types
        "DiscResourceUnit", "DiscResource",
        // Resident types
        "Resident", "CardHandle", "DeviceTData", "CardMemoryMap", "TP_AmigaXIP",
        // Graphics extended types
        "BitScaleArgs", "FontContents", "TFontContents", "FontContentsHeader",
        "DiskFontHeader", "AvailFonts", "TAvailFonts", "AvailFontsHeader",
        // Keymap types
        "KeyMap", "KeyMapNode", "KeyMapResource",
        // Glyph types
        "PGX", "GlyphEngine", "GlyphMap", "GlyphWidthEntry"
    };

    /// <summary>
    /// Recursively collect FFI struct pointer types from a type
    /// </summary>
    private void CollectFFIStructTypesFromType(IrType type, HashSet<string> ffiTypes)
    {
        switch (type)
        {
            case IrPointerType ptrType:
                // Check if this is a pointer to an NDK struct
                if (ptrType.PointeeType is IrStructType st && NdkStructs.Contains(st.StructName))
                {
                    ffiTypes.Add(st.StructName);
                }
                // Recursively check pointee type
                CollectFFIStructTypesFromType(ptrType.PointeeType, ffiTypes);
                break;

            case IrArrayType arrType:
                CollectFFIStructTypesFromType(arrType.ElementType, ffiTypes);
                break;

            case IrEnumType enumType:
                // Check enum variant associated data for FFI struct pointers
                foreach (var variant in enumType.Variants)
                {
                    if (variant.HasAssociatedData && variant.AssociatedData != null)
                    {
                        foreach (var dataType in variant.AssociatedData)
                        {
                            CollectFFIStructTypesFromType(dataType, ffiTypes);
                        }
                    }
                }
                break;

            case IrStructType structType:
                // Check struct fields for FFI struct pointers
                foreach (var field in structType.Fields)
                {
                    CollectFFIStructTypesFromType(field.Type, ffiTypes);
                }
                break;
        }
    }

    /// <summary>
    /// Recursively collect all enum types from a type, including those nested in structs, arrays, and enum variant associated data
    /// </summary>
    internal void CollectEnumTypesFromType(IrType type, HashSet<IrEnumType> enumTypes)
    {
        CollectEnumTypesFromType(type, enumTypes, new HashSet<string>(), null);
    }

    /// <summary>
    /// Recursively collect all enum types from a type, also tracking enum names for fallback resolution
    /// </summary>
    internal void CollectEnumTypesFromType(IrType type, HashSet<IrEnumType> enumTypes, HashSet<string>? enumNames)
    {
        CollectEnumTypesFromType(type, enumTypes, new HashSet<string>(), enumNames);
    }

    private void CollectEnumTypesFromType(IrType type, HashSet<IrEnumType> enumTypes, HashSet<string> visitedStructs, HashSet<string>? enumNames)
    {
        switch (type)
        {
            case IrEnumType enumType:
                // Track the enum name for fallback resolution (in case this is a stub)
                enumNames?.Add(enumType.EnumName);

                // Only add enums with variants (skip stubs/placeholders)
                // This ensures we don't try to emit empty enums that would cause C errors
                if (enumType.Variants.Count > 0)
                {
                    enumTypes.Add(enumType);
                }

                // Also recursively scan enum variant associated data for nested enum types
                // This is crucial for types like Result<T, E> where E might be another enum
                foreach (var variant in enumType.Variants)
                {
                    if (variant.HasAssociatedData && variant.AssociatedData != null)
                    {
                        foreach (var dataType in variant.AssociatedData)
                        {
                            CollectEnumTypesFromType(dataType, enumTypes, visitedStructs, enumNames);
                        }
                    }
                }
                break;

            case IrArrayType arrayType:
                // Recursively check the element type
                CollectEnumTypesFromType(arrayType.ElementType, enumTypes, visitedStructs, enumNames);
                break;

            case IrStructType structType:
                // Use cache key if available, otherwise struct name for cycle detection
                var structKey = structType.CacheKey ?? structType.StructName;
                if (visitedStructs.Contains(structKey))
                {
                    // Already visited this struct, skip to prevent infinite recursion
                    return;
                }
                visitedStructs.Add(structKey);

                // Recursively check all field types
                foreach (var field in structType.Fields)
                {
                    CollectEnumTypesFromType(field.Type, enumTypes, visitedStructs, enumNames);
                }
                break;

            case IrPointerType pointerType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(pointerType.PointeeType, enumTypes, visitedStructs, enumNames);
                break;

            case IrReferenceType refType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(refType.PointeeType, enumTypes, visitedStructs, enumNames);
                break;

            case IrMutReferenceType mutRefType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(mutRefType.PointeeType, enumTypes, visitedStructs, enumNames);
                break;

            // For other types (primitive, function pointers, etc.) we don't need to recurse
        }
    }

    /// <summary>
    /// Collect all struct types that are used BY VALUE in enum variant payloads.
    /// These structs must be fully defined before the enums that contain them.
    /// Note: Pointer types are excluded since they only need forward declarations.
    /// </summary>
    internal void CollectStructTypesFromEnumPayloads(IrEnumType enumType, HashSet<IrStructType> structTypes)
    {
        CollectStructTypesFromEnumPayloads(enumType, structTypes, new HashSet<string>());
    }

    private void CollectStructTypesFromEnumPayloads(IrEnumType enumType, HashSet<IrStructType> structTypes, HashSet<string> visitedEnums)
    {
        var enumKey = MangleName(enumType);
        if (visitedEnums.Contains(enumKey))
            return;
        visitedEnums.Add(enumKey);

        foreach (var variant in enumType.Variants)
        {
            if (variant.HasAssociatedData && variant.AssociatedData != null)
            {
                foreach (var dataType in variant.AssociatedData)
                {
                    CollectStructTypesFromType(dataType, structTypes, visitedEnums);
                }
            }
        }
    }

    private void CollectStructTypesFromType(IrType type, HashSet<IrStructType> structTypes, HashSet<string> visitedEnums)
    {
        switch (type)
        {
            case IrStructType structType:
                // Only add non-extern structs (those we actually emit definitions for)
                structTypes.Add(structType);
                break;

            case IrEnumType nestedEnum:
                // Recursively collect from nested enums
                CollectStructTypesFromEnumPayloads(nestedEnum, structTypes, visitedEnums);
                break;

            case IrTupleType tupleType:
                // Recursively check tuple element types
                foreach (var elementType in tupleType.ElementTypes)
                {
                    CollectStructTypesFromType(elementType, structTypes, visitedEnums);
                }
                break;

            case IrArrayType arrayType:
                // Recursively check array element type
                CollectStructTypesFromType(arrayType.ElementType, structTypes, visitedEnums);
                break;

            // Note: Pointer types are NOT recursed into because pointers only need
            // forward declarations, not full struct definitions.
        }
    }

    /// <summary>
    /// Recursively collect all struct types that a type depends on, including nested field types.
    /// This is used for static variable definitions where we need all struct dependencies to be
    /// defined before the struct that uses them.
    /// </summary>
    private void CollectStructDependenciesRecursively(IrType type, HashSet<IrStructType> structTypes, HashSet<string> visitedStructs)
    {
        switch (type)
        {
            case IrStructType structType:
                // Use CacheKey if available (for monomorphized types), otherwise StructName
                var key = structType.CacheKey ?? structType.StructName;
                if (visitedStructs.Contains(key))
                    return;
                visitedStructs.Add(key);

                // First, recursively collect all field dependencies (before adding this struct)
                foreach (var field in structType.Fields)
                {
                    CollectStructDependenciesRecursively(field.Type, structTypes, visitedStructs);
                }

                // Then add this struct (so dependencies are added before dependents)
                structTypes.Add(structType);
                break;

            case IrArrayType arrayType:
                CollectStructDependenciesRecursively(arrayType.ElementType, structTypes, visitedStructs);
                break;

            case IrTupleType tupleType:
                foreach (var elementType in tupleType.ElementTypes)
                {
                    CollectStructDependenciesRecursively(elementType, structTypes, visitedStructs);
                }
                break;

            case IrEnumType enumType:
                // Check enum variant payloads for struct types
                foreach (var variant in enumType.Variants)
                {
                    if (variant.HasAssociatedData && variant.AssociatedData != null)
                    {
                        foreach (var dataType in variant.AssociatedData)
                        {
                            CollectStructDependenciesRecursively(dataType, structTypes, visitedStructs);
                        }
                    }
                }
                break;

            // Pointer types don't need the full struct definition, just forward declaration
            // so we don't recurse into them
        }
    }

    /// <summary>
    /// Recursively collect all tuple types referenced by a given type.
    /// This is used to emit tuple type definitions before struct definitions that use them.
    /// </summary>
    internal void CollectTupleTypesFromType(IrType type, HashSet<IrTupleType> tupleTypes)
    {
        CollectTupleTypesFromType(type, tupleTypes, new HashSet<string>());
    }

    private void CollectTupleTypesFromType(IrType type, HashSet<IrTupleType> tupleTypes, HashSet<string> visitedStructs)
    {
        switch (type)
        {
            case IrTupleType tupleType:
                // Skip unit type ()
                if (tupleType.ElementTypes.Count > 0)
                {
                    tupleTypes.Add(tupleType);
                    // Also recursively scan tuple element types for nested tuples
                    foreach (var elementType in tupleType.ElementTypes)
                    {
                        CollectTupleTypesFromType(elementType, tupleTypes, visitedStructs);
                    }
                }
                break;

            case IrArrayType arrayType:
                CollectTupleTypesFromType(arrayType.ElementType, tupleTypes, visitedStructs);
                break;

            case IrStructType structType:
                // Use cache key if available, otherwise struct name for cycle detection
                var structKey = structType.CacheKey ?? structType.StructName;
                if (visitedStructs.Contains(structKey))
                {
                    // Already visited this struct, skip to prevent infinite recursion
                    return;
                }
                visitedStructs.Add(structKey);

                foreach (var field in structType.Fields)
                {
                    CollectTupleTypesFromType(field.Type, tupleTypes, visitedStructs);
                }
                break;

            case IrEnumType enumType:
                // Check enum variant associated data for tuple types
                foreach (var variant in enumType.Variants)
                {
                    if (variant.HasAssociatedData && variant.AssociatedData != null)
                    {
                        foreach (var dataType in variant.AssociatedData)
                        {
                            CollectTupleTypesFromType(dataType, tupleTypes, visitedStructs);
                        }
                    }
                }
                break;

            case IrPointerType pointerType:
                CollectTupleTypesFromType(pointerType.PointeeType, tupleTypes, visitedStructs);
                break;

            case IrReferenceType refType:
                CollectTupleTypesFromType(refType.PointeeType, tupleTypes, visitedStructs);
                break;

            case IrMutReferenceType mutRefType:
                CollectTupleTypesFromType(mutRefType.PointeeType, tupleTypes, visitedStructs);
                break;

            // For other types (primitive, function pointers, etc.) we don't need to recurse
        }
    }

    /// <summary>
    /// Check if an enum type is fully concrete (no generic parameters in it or its associated data).
    /// Returns false if the enum itself has generic parameters OR if any variant contains a generic type.
    /// </summary>
    private bool IsConcreteEnum(IrEnumType enumType)
    {
        // Check if the enum itself has generic parameters
        if (enumType.GenericParameters.Count > 0)
        {
            CompilerLogger.Debug(LogCategory.Generics, "IsConcreteEnum: {0} has {1} generic parameters - NOT CONCRETE", enumType.Name, enumType.GenericParameters.Count);
            return false;
        }

        // Check if any variant contains a generic type in its associated data
        foreach (var variant in enumType.Variants)
        {
            if (variant.HasAssociatedData && variant.AssociatedData != null)
            {
                foreach (var dataType in variant.AssociatedData)
                {
                    if (IsGenericType(dataType))
                    {
                        var typeName = dataType switch
                        {
                            IrStructType st => $"struct {st.Name} (generic params: {st.GenericParameters.Count})",
                            IrEnumType et => $"enum {et.Name} (generic params: {et.GenericParameters.Count})",
                            _ => dataType.ToString()
                        };
                        CompilerLogger.Debug(LogCategory.Generics, "IsConcreteEnum: {0} variant {1} contains generic type {2} - NOT CONCRETE", enumType.Name, variant.Name, typeName);
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a type is generic or contains generic types.
    /// WORKAROUND: GenericParameters.Count isn't reliable for structs/enums created by IR builder,
    /// so we also check CacheKey for generic type parameters like <T>.
    /// </summary>
    private bool IsGenericType(IrType type)
    {
        return type switch
        {
            // Self type is unresolved and should be treated as generic
            IrSelfType => true,
            IrGenericType => true,
            IrEnumType enumType => enumType.GenericParameters.Count > 0 ||
                                   (enumType.CacheKey != null && enumType.CacheKey.Contains("<T")),
            IrStructType structType => IsGenericStruct(structType),
            IrArrayType arrayType => IsGenericType(arrayType.ElementType),
            IrPointerType pointerType => IsGenericType(pointerType.PointeeType),
            IrReferenceType refType => IsGenericType(refType.PointeeType),
            IrMutReferenceType mutRefType => IsGenericType(mutRefType.PointeeType),
            _ => false
        };
    }

    private bool IsGenericStruct(IrStructType structType)
    {
        // A struct is generic if it has generic parameters
        if (structType.GenericParameters.Count > 0)
            return true;

        // WORKAROUND: CacheKey is sometimes stale (e.g., "Vec<T>" even for monomorphized Vec<u8>)
        // Only trust CacheKey if GenericParameters.Count > 0
        // If GenericParameters.Count == 0, the type has been monomorphized regardless of CacheKey
        return false;
    }

    /// <summary>
    /// Topologically sort enum types by dependencies.
    /// Returns a list where each enum appears after all enums it depends on.
    /// This ensures that when we emit StringError before Result[Str, StringError].
    /// </summary>
    private List<IrEnumType> TopologicalSortEnumTypes(HashSet<IrEnumType> enumTypes)
    {
        var result = new List<IrEnumType>();
        var visited = new HashSet<IrEnumType>();
        var visiting = new HashSet<IrEnumType>();

        // Helper to get enum name (handles generic instantiations)
        string GetEnumName(IrEnumType et) => et.CacheKey ?? MangleName(et);

        // Build a name-to-type map for efficient lookup
        var enumByName = enumTypes.ToDictionary(e => GetEnumName(e), e => e);

        // DFS visit function
        void Visit(IrEnumType enumType)
        {
            // Check for cycles (shouldn't happen in valid code, but be safe)
            if (visiting.Contains(enumType))
                return; // Skip cycles

            if (visited.Contains(enumType))
                return; // Already processed

            visiting.Add(enumType);

            // Visit all enum dependencies first (enums used in variant associated data)
            foreach (var variant in enumType.Variants)
            {
                if (variant.HasAssociatedData && variant.AssociatedData != null)
                {
                    foreach (var dataType in variant.AssociatedData)
                    {
                        // Find enum types in the associated data
                        if (dataType is IrEnumType dependentEnum)
                        {
                            var dependentName = GetEnumName(dependentEnum);
                            // Look up the enum by name instead of reference equality
                            if (enumByName.TryGetValue(dependentName, out var actualEnum))
                            {
                                Visit(actualEnum);
                            }
                        }
                    }
                }
            }

            visiting.Remove(enumType);
            visited.Add(enumType);
            result.Add(enumType);
        }

        // Visit all enum types
        foreach (var enumType in enumTypes.OrderBy(e => e.Name)) // Stable sort for determinism
        {
            Visit(enumType);
        }

        return result;
    }

    /// <summary>
    /// Topologically sort struct types by dependencies.
    /// Returns a list where each struct appears after all structs it depends on.
    /// This ensures that when we emit Vec_u8 before String (which contains Vec_u8).
    /// </summary>
    private List<IrStructType> TopologicalSortStructTypes(HashSet<IrStructType> structTypes)
    {
        var result = new List<IrStructType>();
        var visited = new HashSet<IrStructType>();
        var visiting = new HashSet<IrStructType>();

        // Helper to get struct name (same logic as TypeRegistry)
        string GetStructName(IrStructType st) => st.CacheKey ?? st.Name;

        // Build a name-to-type map for efficient lookup
        var structByName = structTypes.ToDictionary(s => GetStructName(s), s => s);

        // DFS visit function
        void Visit(IrStructType structType)
        {
            // Check for cycles (shouldn't happen in valid code, but be safe)
            if (visiting.Contains(structType))
                return; // Skip cycles

            if (visited.Contains(structType))
                return; // Already processed

            visiting.Add(structType);

            // Visit all struct dependencies first (structs used in fields)
            foreach (var field in structType.Fields)
            {
                // Find struct types in the field type (including nested in arrays)
                var fieldType = field.Type;

                // Unwrap array types to find the element type
                while (fieldType is IrArrayType arrayType)
                {
                    fieldType = arrayType.ElementType;
                }

                if (fieldType is IrStructType dependentStruct)
                {
                    var dependentName = GetStructName(dependentStruct);
                    // Look up the struct by name instead of reference equality
                    if (structByName.TryGetValue(dependentName, out var actualStruct))
                    {
                        Visit(actualStruct);
                    }
                }
            }

            visiting.Remove(structType);
            visited.Add(structType);
            result.Add(structType);
        }

        // Visit all struct types
        foreach (var structType in structTypes.OrderBy(s => s.Name)) // Stable sort for determinism
        {
            Visit(structType);
        }

        return result;
    }

    /// <summary>
    /// Get list of functions called by this function along with their signatures
    /// </summary>
    private Dictionary<string, (IrType ReturnType, List<IrValue> Arguments)> GetCalledFunctionsWithSignatures(IrFunction function)
    {
        var called = new Dictionary<string, (IrType ReturnType, List<IrValue> Arguments)>();

        // Helper to recursively scan a value for function addresses (handles casts, borrows, etc.)
        void ScanValueForFuncAddrs(IrValue value)
        {
            switch (value)
            {
                case IrFunctionAddress funcAddr:
                    var funcObj = _module.Functions.FirstOrDefault(f => f.Name == funcAddr.FunctionName);
                    if (funcObj != null && !called.ContainsKey(funcAddr.FunctionName))
                    {
                        called[funcAddr.FunctionName] = (funcObj.ReturnType, new List<IrValue>());
                    }
                    else if (funcAddr.Type is IrFunctionPointerType fpType && !called.ContainsKey(funcAddr.FunctionName))
                    {
                        // Use type info from the IrFunctionAddress itself
                        called[funcAddr.FunctionName] = (fpType.ReturnType, new List<IrValue>());
                    }
                    break;

                case IrFunctionRef funcRef:
                    if (!called.ContainsKey(funcRef.Function.Name))
                    {
                        called[funcRef.Function.Name] = (funcRef.Function.ReturnType, new List<IrValue>());
                    }
                    break;

                case IrCastValue castValue:
                    ScanValueForFuncAddrs(castValue.Value);
                    break;

                case IrBorrowValue borrowValue:
                    ScanValueForFuncAddrs(borrowValue.BorrowedValue);
                    break;

                case IrDereferenceValue derefValue:
                    ScanValueForFuncAddrs(derefValue.PointerValue);
                    break;

                case IrStructLiteral structLit:
                    foreach (var fieldValue in structLit.FieldValues.Values)
                        ScanValueForFuncAddrs(fieldValue);
                    break;

                case IrArrayLiteral arrayLit:
                    foreach (var element in arrayLit.Elements)
                        ScanValueForFuncAddrs(element);
                    break;
            }
        }

        // Check function parameters for function pointer types
        foreach (var param in function.Parameters)
        {
            if (param.Type is IrFunctionPointerType)
            {
                // Parameter is a function pointer - we can't know which function it points to statically,
                // but we should check if there's an IrFunctionAddress value assigned to it
            }
        }

        // Check local variables for function pointer types
        foreach (var local in function.LocalVariables)
        {
            if (local.Type is IrFunctionPointerType)
            {
                // Local variable is a function pointer - check for assignments in instructions
            }
        }

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCall call)
                {
                    if (!called.ContainsKey(call.FunctionName))
                    {
                        called[call.FunctionName] = (call.ReturnType, call.Arguments);
                    }

                    // Scan arguments for function addresses (including casts)
                    foreach (var arg in call.Arguments)
                    {
                        ScanValueForFuncAddrs(arg);
                    }
                }
                else if (instruction is IrIndirectCall indirectCall)
                {
                    // Extract function name from function pointer
                    if (indirectCall.FunctionPointer is IrFunctionAddress funcAddr)
                    {
                        if (!called.ContainsKey(funcAddr.FunctionName))
                        {
                            called[funcAddr.FunctionName] = (indirectCall.ReturnType, indirectCall.Arguments);
                        }
                    }
                    // Scan the function pointer and arguments
                    ScanValueForFuncAddrs(indirectCall.FunctionPointer);
                    foreach (var arg in indirectCall.Arguments)
                    {
                        ScanValueForFuncAddrs(arg);
                    }
                }
                else if (instruction is IrLocalDecl localDecl)
                {
                    // Scan initial value for function addresses (including casts)
                    if (localDecl.InitialValue != null)
                    {
                        ScanValueForFuncAddrs(localDecl.InitialValue);
                    }
                }
                else if (instruction is IrStore store)
                {
                    // Scan stored value for function addresses (including casts)
                    ScanValueForFuncAddrs(store.Value);
                }
                else if (instruction is IrReturn returnInst)
                {
                    // Scan return value for function addresses
                    if (returnInst.Value != null)
                    {
                        ScanValueForFuncAddrs(returnInst.Value);
                    }
                }
            }
        }

        // Also scan deferred blocks for function calls
        foreach (var deferredBlock in function.DeferredBlocks)
        {
            foreach (var instruction in deferredBlock.Instructions)
            {
                if (instruction is IrCall call)
                {
                    if (!called.ContainsKey(call.FunctionName))
                    {
                        called[call.FunctionName] = (call.ReturnType, call.Arguments);
                    }
                }
                else if (instruction is IrIndirectCall indirectCall)
                {
                    if (indirectCall.FunctionPointer is IrFunctionAddress funcAddr)
                    {
                        if (!called.ContainsKey(funcAddr.FunctionName))
                        {
                            called[funcAddr.FunctionName] = (indirectCall.ReturnType, indirectCall.Arguments);
                        }
                    }
                }
            }
        }

        return called;
    }

    /// <summary>
    /// Get list of functions called by this function
    /// </summary>
    private HashSet<string> GetCalledFunctions(IrFunction function)
    {
        var called = new HashSet<string>();

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCall call)
                {
                    called.Add(call.FunctionName);
                }
            }
        }

        return called;
    }

    public string Generate()
    {
        // First pass: detect which AmigaOS libraries are used
        DetectRequiredProtoHeaders();

        // Second pass: analyze reachable functions (dead code elimination)
        var reachableFunctions = AnalyzeReachableFunctions();

        // Check if this is a library module
        var libraryGen = new LibraryGenerator(_module, _projectVersion);

        EmitHeaders();
        EmitTypedefs(reachableFunctions);
        EmitStringLiterals();
        EmitExternalVariables();
        EmitStaticVariables();
        EmitForwardDeclarations(reachableFunctions);
        EmitFunctions(reachableFunctions);

        // Emit debug symbol table in debug builds for runtime source lookup
        if (_safetyLevel >= SafetyLevel.Full)
        {
            EmitDebugSymbolTable(reachableFunctions);
        }

        // Generate library boilerplate if @library attribute is present
        CompilerLogger.Debug(LogCategory.CodeGen, "CCodeGenerator: libraryGen.IsLibrary = {0}", libraryGen.IsLibrary);
        if (libraryGen.IsLibrary)
        {
            CompilerLogger.Debug(LogCategory.CodeGen, "Generating library boilerplate...");
            _output.AppendLine();
            _output.AppendLine(libraryGen.GenerateLibraryBaseStruct());
            _output.AppendLine(libraryGen.GenerateROMTag());
            _output.AppendLine(libraryGen.GenerateDefaultLifecycleFunctions());
        }

        // VBCC FIX: Transform goto-based for-loops to natural C for-loops
        // This works around vbcc stack allocation bugs with goto-based control flow
        var generatedCode = _output.ToString();
        generatedCode = TransformForLoopsForVbcc(generatedCode);

        return generatedCode;
    }

    /// <summary>
    /// Generate a C header file containing declarations for all exported functions.
    /// This header can be included by C programs that want to call Novus functions.
    /// </summary>
    public string GenerateHeader()
    {
        var sb = new StringBuilder();

        // Header guard
        sb.AppendLine("#ifndef NOVUS_EXPORTS_H");
        sb.AppendLine("#define NOVUS_EXPORTS_H");
        sb.AppendLine();

        // Emit inline type definitions (no C library dependency)
        EmitInlineTypeDefinitions(sb);
        sb.AppendLine();

        // Include shared types header if using per-function compilation
        if (_useSharedTypesHeader)
        {
            sb.AppendLine("#include \"novus_types.h\"");
            sb.AppendLine();
        }

        // Add extern "C" for C++ compatibility
        sb.AppendLine("#ifdef __cplusplus");
        sb.AppendLine("extern \"C\" {");
        sb.AppendLine("#endif");
        sb.AppendLine();

        // Generate function declarations for exported functions
        var exportedFunctions = _module.Functions.Where(f => f.IsExported && !f.IsExtern).ToList();

        if (exportedFunctions.Count > 0)
        {
            sb.AppendLine("// Exported Novus functions");
            sb.AppendLine();

            foreach (var function in exportedFunctions)
            {
                var returnType = GetCType(function.ReturnType);
                var funcName = function.Name;  // Don't mangle exported names
                var parameters = GetParameterList(function, hasOutputParameter: false);

                // Generate function declaration
                sb.AppendLine($"{returnType} {funcName}({parameters});");
            }
        }
        else
        {
            sb.AppendLine("// No exported functions");
        }

        sb.AppendLine();
        sb.AppendLine("#ifdef __cplusplus");
        sb.AppendLine("}");
        sb.AppendLine("#endif");
        sb.AppendLine();
        sb.AppendLine("#endif // NOVUS_EXPORTS_H");

        return sb.ToString();
    }

    private void DetectRequiredProtoHeaders()
    {
        // Map of function names to their proto headers
        var protoMap = new Dictionary<string, string>
        {
            // exec.library
            ["AllocMem"] = "exec",
            ["FreeMem"] = "exec",
            ["OpenLibrary"] = "exec",
            ["CloseLibrary"] = "exec",
            ["FindTask"] = "exec",
            ["Wait"] = "exec",
            ["Signal"] = "exec",
            ["AllocSignal"] = "exec",
            ["FreeSignal"] = "exec",

            // dos.library
            ["Output"] = "dos",
            ["Input"] = "dos",
            ["Write"] = "dos",
            ["Read"] = "dos",
            ["Open"] = "dos",
            ["Close"] = "dos",
            ["Printf"] = "dos",
        };

        foreach (var function in _module.Functions)
        {
            if (function.IsExtern && protoMap.TryGetValue(function.Name, out var header))
            {
                _requiredProtoHeaders.Add(header);
            }
        }
    }

    /// <summary>
    /// Analyze which functions are reachable from entry points (main or public functions).
    /// This enables dead code elimination - we only emit functions that can actually be called.
    /// </summary>
    private HashSet<string> AnalyzeReachableFunctions()
    {
        var reachable = new HashSet<string>();
        var worklist = new Queue<string>();

        // Find entry points:
        // 1. If there's a main function, it's the primary entry point
        // 2. If explicit entry points are provided, use only those
        // 3. For library modules without explicit entry points, include public functions
        // 4. ALWAYS include exported functions as entry points (external C code can call them)
        var entryPoints = new List<IrFunction>();
        var mainFunc = _module.Functions.FirstOrDefault(f => f.Name == "main" && !f.IsExtern);
        if (mainFunc != null)
        {
            entryPoints.Add(mainFunc);
        }
        else if (_explicitEntryPoints != null)
        {
            // Use explicit entry points (for smart cross-module DCE)
            entryPoints.AddRange(_module.Functions.Where(f =>
                _explicitEntryPoints.Contains(f.Name) && !f.IsExtern && f.BasicBlocks.Count > 0));
        }
        else
        {
            // For library modules: include ALL public functions as potential entry points
            // This is conservative but safe - the linker can eliminate truly unused functions
            entryPoints.AddRange(_module.Functions.Where(f => f.IsPublic && !f.IsExtern && f.BasicBlocks.Count > 0));
        }

        // CRITICAL: Always include exported functions as entry points
        // Exported functions can be called from external C code, so they must not be eliminated
        entryPoints.AddRange(_module.Functions.Where(f => f.IsExported && !f.IsExtern && f.BasicBlocks.Count > 0 && !entryPoints.Contains(f)));

        // Start BFS from entry points
        foreach (var entry in entryPoints)
        {
            reachable.Add(entry.Name);
            worklist.Enqueue(entry.Name);
        }

        // Build call graph and mark reachable functions
        while (worklist.Count > 0)
        {
            var currentName = worklist.Dequeue();
            var currentFunc = _module.Functions.FirstOrDefault(f => f.Name == currentName);
            if (currentFunc == null)
                continue;

            // Scan all instructions for function calls (including deferred blocks)
            var blocksToScan = new List<IrBasicBlock>(currentFunc.BasicBlocks);
            blocksToScan.AddRange(currentFunc.DeferredBlocks);

            foreach (var block in blocksToScan)
            {
                foreach (var instruction in block.Instructions)
                {
                    // Handle direct function calls
                    if (instruction is IrCall call)
                    {
                        // Mark called function as reachable
                        if (!reachable.Contains(call.FunctionName))
                        {
                            reachable.Add(call.FunctionName);

                            // If it's a function in this module (not extern), add to worklist
                            var calledFunc = _module.Functions.FirstOrDefault(f => f.Name == call.FunctionName && !f.IsExtern);
                            if (calledFunc != null)
                            {
                                worklist.Enqueue(call.FunctionName);
                            }
                        }

                        // Scan arguments for function references (e.g., passing functions as parameters)
                        foreach (var arg in call.Arguments)
                        {
                            ScanValueForFunctionReferences(arg, reachable, worklist);
                        }
                    }

                    // Handle indirect calls through function pointers
                    // We need to scan the function pointer value for IrFunctionAddress
                    if (instruction is IrIndirectCall indirectCall)
                    {
                        ScanValueForFunctionReferences(indirectCall.FunctionPointer, reachable, worklist);
                        // Also scan arguments
                        foreach (var arg in indirectCall.Arguments)
                        {
                            ScanValueForFunctionReferences(arg, reachable, worklist);
                        }
                    }

                    // Scan other instructions that might contain function addresses
                    // (e.g., store, local decl with initializer, etc.)
                    switch (instruction)
                    {
                        case IrLocalDecl localDecl when localDecl.InitialValue != null:
                            ScanValueForFunctionReferences(localDecl.InitialValue, reachable, worklist);
                            break;
                        case IrStore store:
                            ScanValueForFunctionReferences(store.Value, reachable, worklist);
                            break;
                        case IrMemberStore memberStore:
                            ScanValueForFunctionReferences(memberStore.Struct, reachable, worklist);
                            ScanValueForFunctionReferences(memberStore.Value, reachable, worklist);
                            break;
                        case IrDereferenceStore derefStore:
                            ScanValueForFunctionReferences(derefStore.Pointer, reachable, worklist);
                            ScanValueForFunctionReferences(derefStore.Value, reachable, worklist);
                            break;
                        case IrIndexStore indexStore:
                            ScanValueForFunctionReferences(indexStore.Array, reachable, worklist);
                            ScanValueForFunctionReferences(indexStore.Index, reachable, worklist);
                            ScanValueForFunctionReferences(indexStore.Value, reachable, worklist);
                            break;
                        case IrIndexedFieldStore indexedFieldStore:
                            ScanValueForFunctionReferences(indexedFieldStore.Array, reachable, worklist);
                            ScanValueForFunctionReferences(indexedFieldStore.Index, reachable, worklist);
                            ScanValueForFunctionReferences(indexedFieldStore.Value, reachable, worklist);
                            break;
                        case IrBinaryOp binaryOp:
                            ScanValueForFunctionReferences(binaryOp.Left, reachable, worklist);
                            ScanValueForFunctionReferences(binaryOp.Right, reachable, worklist);
                            break;
                        case IrMatch match:
                            ScanValueForFunctionReferences(match.MatchValue, reachable, worklist);
                            break;
                        case IrReturn returnInst when returnInst.Value != null:
                            ScanValueForFunctionReferences(returnInst.Value, reachable, worklist);
                            break;
                    }
                }
            }
        }

        // NOTE: For library modules (no main), we conservatively include all public functions
        // as potential entry points. True dead code elimination across modules requires either:
        // 1. Whole-program analysis (compile all modules together)
        // 2. Link-time optimization (linker eliminates unused functions)
        // 3. Explicit import tracking (only emit explicitly imported functions)
        // For now, we rely on the linker's --gc-sections to eliminate truly unused code.

        return reachable;
    }

    /// <summary>
    /// Recursively scan an IR value for function addresses (IrFunctionAddress).
    /// When found, mark those functions as reachable.
    /// </summary>
    private void ScanValueForFunctionReferences(IrValue value, HashSet<string> reachable, Queue<string> worklist)
    {
        switch (value)
        {
            case IrFunctionAddress funcAddr:
                // Found a function being referenced - mark it as reachable
                if (!reachable.Contains(funcAddr.FunctionName))
                {
                    reachable.Add(funcAddr.FunctionName);

                    // If it's a function in this module (not extern), add to worklist
                    var referencedFunc = _module.Functions.FirstOrDefault(f => f.Name == funcAddr.FunctionName && !f.IsExtern);
                    if (referencedFunc != null)
                    {
                        worklist.Enqueue(funcAddr.FunctionName);
                    }
                }
                break;

            case IrFunctionRef funcRef:
                // Found a function reference - mark it as reachable
                if (!reachable.Contains(funcRef.Function.Name))
                {
                    reachable.Add(funcRef.Function.Name);

                    // If it's a function in this module (not extern), add to worklist
                    if (!funcRef.Function.IsExtern && funcRef.Function.BasicBlocks.Count > 0)
                    {
                        worklist.Enqueue(funcRef.Function.Name);
                    }
                }
                break;

            case IrStructLiteral structLit:
                // Scan all fields for function references
                foreach (var fieldValue in structLit.FieldValues.Values)
                {
                    ScanValueForFunctionReferences(fieldValue, reachable, worklist);
                }
                break;

            case IrArrayLiteral arrayLit:
                // Scan all elements for function references
                foreach (var element in arrayLit.Elements)
                {
                    ScanValueForFunctionReferences(element, reachable, worklist);
                }
                break;

            case IrEnumValue enumValue:
                // Scan associated data for function references
                foreach (var data in enumValue.AssociatedValues)
                {
                    ScanValueForFunctionReferences(data, reachable, worklist);
                }
                break;

            case IrBorrowValue borrowValue:
                ScanValueForFunctionReferences(borrowValue.BorrowedValue, reachable, worklist);
                break;

            case IrDereferenceValue derefValue:
                ScanValueForFunctionReferences(derefValue.PointerValue, reachable, worklist);
                break;

            case IrCastValue castValue:
                ScanValueForFunctionReferences(castValue.Value, reachable, worklist);
                break;

            // For simple values (constants, variables), there's nothing to scan
            case IrConstant:
            case IrBoolConstant:
            case IrVariable:
            case IrStringLiteral:
                break;
        }
    }

    private void EmitHeaders()
    {
        _output.AppendLine("// Generated by Novus compiler");
        _output.AppendLine("// Target: AmigaOS 2.0+ (68020+), C99");
        _output.AppendLine();

        if (_useSharedTypesHeader)
        {
            _output.AppendLine("#include \"novus_types.h\"");
            _output.AppendLine();
        }
        else
        {
            // Emit inline type definitions (no C library dependency)
            EmitInlineTypeDefinitions(_output);
            _output.AppendLine();
        }

        // Memory tracking configuration
        if (_safetyLevel.EnableMemoryTracking())
        {
            _output.AppendLine("// Memory tracking enabled");
            _output.AppendLine("#define NOVUS_MEMORY_DEBUG 1");

            if (_safetyLevel.EnableMemoryPoisoning())
            {
                _output.AppendLine("#define NOVUS_MEMORY_PARANOID 1");
            }

            _output.AppendLine();
            _output.AppendLine("// Memory tracking runtime functions");
            _output.AppendLine("extern void* __novus_tracked_alloc(uint32_t size, uint32_t flags, const char* file, int32_t line);");
            _output.AppendLine("extern void __novus_tracked_free(void* ptr, uint32_t size, const char* file, int32_t line);");
            _output.AppendLine("extern void __novus_memory_report(void);");
            _output.AppendLine();
            _output.AppendLine("// MMU protection runtime functions");
            _output.AppendLine("extern void __novus_init_mmu_protection(void);");
            _output.AppendLine("extern void __novus_cleanup_mmu_protection(void);");
            _output.AppendLine();
        }

        // Stack overflow detection runtime functions (debug builds only)
        if (_buildMode == BuildMode.Debug)
        {
            _output.AppendLine("// Stack overflow detection runtime functions");
            _output.AppendLine("extern void __novus_init_stack_bounds(void);");
            _output.AppendLine("extern void __novus_check_stack(uint32_t required_bytes, const char* func_name, int32_t line);");
            _output.AppendLine();
        }

        // Note: AmigaOS headers are included earlier in the monolithic output
        // utility/tagitem.h is included and TagItem is typedef'd there

        // Don't emit proto headers - we'll use assembly stubs with i32 signatures
        // This avoids fighting VBCC's type system (BPTR, CONST_STRPTR, etc.)
        // Our assembly stubs handle the conversions from i32 to proper AmigaOS types
    }

    /// <summary>
    /// Emit inline type definitions for integer and boolean types.
    /// This replaces stdint.h and stdbool.h to avoid C library dependencies.
    /// </summary>
    private static void EmitInlineTypeDefinitions(StringBuilder sb)
    {
        // Include exec/types.h first - it includes <stdint.h> which defines __STDINT_H
        sb.AppendLine("#include <exec/types.h>");
        sb.AppendLine();
        sb.AppendLine("// Integer types - fallback definitions if stdint.h not included");
        sb.AppendLine("#ifndef __STDINT_H");
        sb.AppendLine("typedef signed char int8_t;");
        sb.AppendLine("typedef short int16_t;");
        sb.AppendLine("typedef long int32_t;");
        sb.AppendLine("typedef long long int64_t;");
        sb.AppendLine("typedef unsigned char uint8_t;");
        sb.AppendLine("typedef unsigned short uint16_t;");
        sb.AppendLine("typedef unsigned long uint32_t;");
        sb.AppendLine("typedef unsigned long long uint64_t;");
        sb.AppendLine("#endif");
        sb.AppendLine();
        sb.AppendLine("// Boolean type - 1 byte to match Novus semantics (not C99's int-based bool)");
        sb.AppendLine("// Use unique guard to avoid conflicts with system headers (VBCC may use different stdbool guard)");
        sb.AppendLine("#ifndef __NOVUS_BOOL_DEFINED");
        sb.AppendLine("#define __NOVUS_BOOL_DEFINED");
        sb.AppendLine("typedef uint8_t bool;");
        sb.AppendLine("#define true ((bool)1)");
        sb.AppendLine("#define false ((bool)0)");
        sb.AppendLine("#endif");
    }

    private void EmitTypedefs(HashSet<string> reachableFunctions)
    {
        // When using shared types header, we only need to emit types that aren't in the header
        // (e.g., monomorphized types with new type arguments not used in the stdlib)
        if (_useSharedTypesHeader)
        {
            // Emit only enum types used in this compilation but not in the shared header
            EmitEnumTypes(reachableFunctions);
            return;
        }

        // Full typedef emission (no shared header)
        // String type (fat pointer)
        _output.AppendLine("// String type (fat pointer: pointer + length)");
        _output.AppendLine("typedef struct {");
        _output.AppendLine("    uint8_t* ptr;");
        _output.AppendLine("    int32_t len;");
        _output.AppendLine("} String;");
        _output.AppendLine();

        // Emit enum types (only those used by reachable functions)
        EmitEnumTypes(reachableFunctions);

        // Struct types are forward-declared and defined on-demand in EmitStructType
        // as they are encountered in function signatures and variable declarations
    }

    private void EmitEnumTypes(HashSet<string> reachableFunctions)
    {
        // Collect all unique enum types used in reachable functions, external variables, and static variables
        var enumTypes = new HashSet<IrEnumType>();

        // Scan external variables for enum types
        foreach (var externVar in _module.ExternalVariables)
        {
            if (externVar.Type is IrEnumType enumExtVar)
                enumTypes.Add(enumExtVar);
        }

        // Scan static variables for enum types
        foreach (var staticVar in _module.StaticVariables)
        {
            if (staticVar.Type is IrEnumType enumStaticVar)
                enumTypes.Add(enumStaticVar);
        }

        // Scan only reachable functions for enum types
        foreach (var function in _module.Functions.Where(f => reachableFunctions.Contains(f.Name)))
        {
            if (function.ReturnType is IrEnumType enumRet)
                enumTypes.Add(enumRet);

            foreach (var param in function.Parameters)
            {
                if (param.Type is IrEnumType enumParam)
                    enumTypes.Add(enumParam);
            }

            // Scan local variables
            foreach (var local in function.LocalVariables)
            {
                if (local.Type is IrEnumType enumLocal)
                    enumTypes.Add(enumLocal);
            }

            // Scan instructions for enum values
            foreach (var block in function.BasicBlocks)
            {
                foreach (var instruction in block.Instructions)
                {
                    // Check for IrLocalDecl with enum types
                    if (instruction is IrLocalDecl localDecl && localDecl.Type is IrEnumType enumDeclType)
                        enumTypes.Add(enumDeclType);

                    // Check for IrMatch to get the matched enum type
                    if (instruction is IrMatch match && match.MatchValue.Type is IrEnumType matchEnumType)
                        enumTypes.Add(matchEnumType);
                }
            }
        }

        if (enumTypes.Count == 0)
            return;

        _output.AppendLine("// Enum types");

        // Emit enums in dependency order: simple (unit variant) enums first, then enums with associated data
        // This ensures that if Result<T, DosError> uses DosError, DosError is defined first
        var simpleEnums = new List<IrEnumType>();
        var complexEnums = new List<IrEnumType>();

        foreach (var enumType in enumTypes)
        {
            bool hasAssociatedData = enumType.Variants.Any(v => v.AssociatedData.Count > 0);
            if (hasAssociatedData)
            {
                complexEnums.Add(enumType);
            }
            else
            {
                simpleEnums.Add(enumType);
            }
        }

        // Emit simple enums first
        foreach (var enumType in simpleEnums)
        {
            EmitEnumType(enumType);
        }

        // Then emit complex enums
        foreach (var enumType in complexEnums)
        {
            EmitEnumType(enumType);
        }
    }

    private void EmitEnumType(IrEnumType enumType)
    {
        var enumName = MangleName(enumType);

        // Check if this enum has any associated data
        bool hasAnyData = enumType.Variants.Any(v => v.HasAssociatedData);

        // Add header guard to prevent redefinition (especially when using shared types header)
        var guardName = $"NOVUS_TYPE_{enumName.ToUpper()}_DEFINED";
        _output.AppendLine($"#ifndef {guardName}");
        _output.AppendLine($"#define {guardName}");
        _output.AppendLine($"// Enum: {enumType.Name}");

        if (!hasAnyData)
        {
            // OPTIMIZATION: For enums with no associated data, use plain C enum
            // This saves space by avoiding the struct+union overhead
            _output.AppendLine($"typedef enum {{");
            for (int i = 0; i < enumType.Variants.Count; i++)
            {
                var variant = enumType.Variants[i];
                var comma = i < enumType.Variants.Count - 1 ? "," : "";
                _output.AppendLine($"    {enumName}_{variant.Name} = {variant.Tag}{comma}");
            }
            _output.AppendLine($"}} {enumName};");
            _output.AppendLine($"#endif // {guardName}");
            _output.AppendLine();
            return;
        }

        // For enums WITH associated data, use the full struct+union representation

        // Emit variant tag enum
        _output.AppendLine($"enum {enumName}_Tag {{");
        for (int i = 0; i < enumType.Variants.Count; i++)
        {
            var variant = enumType.Variants[i];
            var comma = i < enumType.Variants.Count - 1 ? "," : "";
            _output.AppendLine($"    {enumName}_{variant.Name} = {variant.Tag}{comma}");
        }
        _output.AppendLine("};");
        _output.AppendLine();

        // Emit union for variant data
        _output.AppendLine($"union {enumName}_Data {{");
        foreach (var variant in enumType.Variants)
        {
            if (variant.HasAssociatedData)
            {
                // BUG FIX #3: Validate AssociatedData before accessing
                if (variant.AssociatedData == null)
                {
                    throw new InvalidOperationException($"Variant '{variant.Name}' has HasAssociatedData=true but AssociatedData is null");
                }

                _output.AppendLine($"    struct {{");

                // Check if this is a unit type - if so, generate struct with dummy field
                // Unit type () is represented as a single IrTupleType with 0 elements
                bool isUnitType = variant.AssociatedData.Count == 1 &&
                                 variant.AssociatedData[0] is IrTupleType tupleType &&
                                 tupleType.ElementTypes.Count == 0;

                if (!isUnitType)
                {
                    // Use aligned field emission to prevent Address Error on 68000
                    EmitAlignedVariantFields(_output, variant.AssociatedData, "        ");
                }
                else
                {
                    // VBCC doesn't support empty structs, so add a dummy field
                    _output.AppendLine($"        char _dummy;");
                }

                _output.AppendLine($"    }} {variant.Name};");
            }
        }
        _output.AppendLine("};");
        _output.AppendLine();

        // Emit main struct
        _output.AppendLine($"typedef struct {{");
        _output.AppendLine($"    enum {enumName}_Tag tag;");
        _output.AppendLine($"    union {enumName}_Data data;");
        _output.AppendLine($"}} {enumName};");
        _output.AppendLine($"#endif // {guardName}");
        _output.AppendLine();
    }

    /// <summary>
    /// Emit struct fields for an enum variant's associated data with proper 68k alignment padding.
    /// 68000 requires word alignment (2-byte) for word and long accesses.
    /// </summary>
    private void EmitAlignedVariantFields(StringBuilder sb, List<IrType> associatedData, string indent)
    {
        int currentOffset = 0;
        int paddingFieldIndex = 0;

        for (int i = 0; i < associatedData.Count; i++)
        {
            var fieldType = associatedData[i];
            int fieldSize = fieldType.SizeInBytes;
            var dataType = GetCType(fieldType);

            // 68000 alignment rules:
            // - 1-byte fields: no alignment needed
            // - 2+ byte fields: must be word-aligned (even address)
            int alignment = fieldSize switch
            {
                1 => 1,
                _ => 2
            };

            // Add padding if needed
            if (currentOffset % alignment != 0)
            {
                int paddingBytes = alignment - (currentOffset % alignment);
                sb.AppendLine($"{indent}uint8_t _pad{paddingFieldIndex}[{paddingBytes}];  // alignment padding");
                currentOffset += paddingBytes;
                paddingFieldIndex++;
            }

            sb.AppendLine($"{indent}{dataType} _{i};");
            currentOffset += fieldSize;
        }
    }

    private void EmitStringLiterals()
    {
        if (_stringLiterals.Count == 0)
            return;

        _output.AppendLine("// String literals");
        for (int i = 0; i < _stringLiterals.Count; i++)
        {
            var literal = _stringLiterals[i];
            // Escape string for C
            var escaped = EscapeString(literal.Value);
            _output.AppendLine($"static const char {literal.Label}[] = \"{escaped}\";");
        }
        _output.AppendLine();
    }

    internal string EscapeString(string value)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default:
                    // Escape any non-printable or non-ASCII characters as octal
                    // We use octal instead of hex because C's \x is greedy and will
                    // consume following hex digits (e.g., \x9b0 becomes 0x9b0, not 0x9b + '0')
                    if (c < 32 || c >= 127)
                    {
                        // Convert to octal manually (C# doesn't have octal format specifier)
                        int val = (int)c;
                        sb.Append($"\\{Convert.ToString(val, 8).PadLeft(3, '0')}");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private void EmitExternalVariables()
    {
        if (_module.ExternalVariables.Count == 0)
            return;

        _output.AppendLine("// External variables");
        foreach (var externVar in _module.ExternalVariables)
        {
            var cType = GetCType(externVar.Type);

            if (externVar.Address.HasValue)
            {
                // Hardware register at specific address - define as volatile pointer
                _output.AppendLine($"#define {externVar.Name} (*(volatile {cType}*)0x{externVar.Address.Value:X})");
            }
            else
            {
                // Extern variable resolved by linker
                _output.AppendLine($"extern {cType} {externVar.Name};");
            }
        }
        _output.AppendLine();
    }

    private void EmitStaticVariables()
    {
        if (_module.StaticVariables.Count == 0)
            return;

        _output.AppendLine("// Static variables");
        foreach (var staticVar in _module.StaticVariables)
        {
            var staticKeyword = staticVar.Visibility == Visibility.Private ? "static" : "";
            var constKeyword = !staticVar.IsMutable ? "const" : "";

            // Generate initial value
            var initialValue = EmitValue(staticVar.InitialValue);

            // Emit the declaration with initialization
            var keywords = new List<string>();
            if (!string.IsNullOrEmpty(staticKeyword)) keywords.Add(staticKeyword);
            if (!string.IsNullOrEmpty(constKeyword)) keywords.Add(constKeyword);

            var keywordStr = keywords.Count > 0 ? string.Join(" ", keywords) + " " : "";

            // Special handling for arrays - use array syntax instead of pointer syntax
            if (staticVar.Type is IrArrayType arrayType)
            {
                var elementType = GetCType(arrayType.ElementType);
                _output.AppendLine($"{keywordStr}{elementType} {staticVar.Name}[{arrayType.Length}] = {initialValue};");
            }
            else
            {
                var cType = GetCType(staticVar.Type);
                _output.AppendLine($"{keywordStr}{cType} {staticVar.Name} = {initialValue};");
            }
        }
        _output.AppendLine();
    }

    private void EmitForwardDeclarations(HashSet<string> reachableFunctions)
    {
        // DOS initialization is no longer unconditionally added
        // DOS library will be initialized automatically when DOS functions are first used
        // via the dos_init.o stub that gets linked only when DOS functions are detected

        // Emit extern declarations for extern functions (from FFI) - but only if they're reachable
        var externFunctions = _module.Functions
            .Where(f => f.IsExtern && reachableFunctions.Contains(f.Name))
            .ToList();
        if (externFunctions.Count > 0)
        {
            _output.AppendLine("// External function declarations (FFI)");
            foreach (var function in externFunctions)
            {
                // Extern functions use their actual C signatures - no VBCC output parameter workaround
                // The runtime implements these functions with direct return values
                var returnType = GetCType(function.ReturnType);
                var parameters = GetParameterList(function);
                _output.AppendLine($"extern {returnType} {MangleName(function)}({parameters});");
            }
            _output.AppendLine();
        }

        // Find all cross-module function calls (functions called but not defined in this module)
        // Only consider calls from reachable functions
        var crossModuleCalls = new Dictionary<string, (IrType ReturnType, List<IrType> ParameterTypes)>();
        // Only consider functions with implementations as "defined" - imported functions have no basic blocks
        var definedFunctionNames = new HashSet<string>(
            _module.Functions
                .Where(f => !f.IsExtern && f.BasicBlocks.Count > 0)
                .Select(f => f.Name)
        );

        // Also include extern functions as "defined" since they're FFI declarations
        var externFunctionNames = new HashSet<string>(
            _module.Functions.Where(f => f.IsExtern).Select(f => f.Name)
        );

        // Helper to scan a value for function addresses (used when functions are cast to integers)
        void ScanValueForFunctionAddresses(IrValue value)
        {
            switch (value)
            {
                case IrFunctionAddress funcAddr:
                    // Function address used (possibly cast to integer) - need declaration
                    if (!definedFunctionNames.Contains(funcAddr.FunctionName) &&
                        !externFunctionNames.Contains(funcAddr.FunctionName) &&
                        !crossModuleCalls.ContainsKey(funcAddr.FunctionName))
                    {
                        // Get the function's signature from the module
                        var funcObj = _module.Functions.FirstOrDefault(f => f.Name == funcAddr.FunctionName);
                        if (funcObj != null)
                        {
                            var paramTypes = funcObj.Parameters.Select(p => p.Type).ToList();
                            crossModuleCalls[funcAddr.FunctionName] = (funcObj.ReturnType, paramTypes);
                        }
                        else if (funcAddr.Type is IrFunctionPointerType fpType)
                        {
                            // Use the type information from the IrFunctionAddress itself
                            crossModuleCalls[funcAddr.FunctionName] = (fpType.ReturnType, fpType.ParameterTypes);
                        }
                    }
                    break;

                case IrCastValue castValue:
                    ScanValueForFunctionAddresses(castValue.Value);
                    break;

                case IrBorrowValue borrowValue:
                    ScanValueForFunctionAddresses(borrowValue.BorrowedValue);
                    break;

                case IrDereferenceValue derefValue:
                    ScanValueForFunctionAddresses(derefValue.PointerValue);
                    break;

                case IrStructLiteral structLit:
                    foreach (var fieldValue in structLit.FieldValues.Values)
                        ScanValueForFunctionAddresses(fieldValue);
                    break;

                case IrArrayLiteral arrayLit:
                    foreach (var element in arrayLit.Elements)
                        ScanValueForFunctionAddresses(element);
                    break;
            }
        }

        // Helper to scan a block for cross-module calls (handles nested defer blocks)
        void ScanBlockForCalls(IrBasicBlock block)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCall call)
                {
                    // If this function is not defined in current module, not an extern, and not already tracked
                    if (!definedFunctionNames.Contains(call.FunctionName) &&
                        !externFunctionNames.Contains(call.FunctionName) &&
                        !crossModuleCalls.ContainsKey(call.FunctionName))
                    {
                        // Extract parameter types from call arguments
                        var paramTypes = call.Arguments.Select(arg => arg.Type).ToList();
                        crossModuleCalls[call.FunctionName] = (call.ReturnType, paramTypes);
                    }
                    // Also scan arguments for function addresses
                    foreach (var arg in call.Arguments)
                        ScanValueForFunctionAddresses(arg);
                }
                else if (instruction is IrIndirectCall indirectCall)
                {
                    // Extract function name from function pointer
                    if (indirectCall.FunctionPointer is IrFunctionAddress funcAddr)
                    {
                        // If this function is not defined in current module, not an extern, and not already tracked
                        if (!definedFunctionNames.Contains(funcAddr.FunctionName) &&
                            !externFunctionNames.Contains(funcAddr.FunctionName) &&
                            !crossModuleCalls.ContainsKey(funcAddr.FunctionName))
                        {
                            // Extract parameter types from call arguments
                            var paramTypes = indirectCall.Arguments.Select(arg => arg.Type).ToList();
                            crossModuleCalls[funcAddr.FunctionName] = (indirectCall.ReturnType, paramTypes);
                        }
                    }
                    // Also scan the function pointer value and arguments for function addresses
                    ScanValueForFunctionAddresses(indirectCall.FunctionPointer);
                    foreach (var arg in indirectCall.Arguments)
                        ScanValueForFunctionAddresses(arg);
                }
                else if (instruction is IrDefer defer)
                {
                    // Recursively scan the deferred block
                    ScanBlockForCalls(defer.DeferredBlock);
                }
                else if (instruction is IrStore store)
                {
                    // Scan stored values for function addresses
                    ScanValueForFunctionAddresses(store.Value);
                }
                else if (instruction is IrLocalDecl localDecl)
                {
                    // Scan initial values for function addresses
                    if (localDecl.InitialValue != null)
                        ScanValueForFunctionAddresses(localDecl.InitialValue);
                }
                else if (instruction is IrReturn returnInst)
                {
                    // Scan return values for function addresses
                    if (returnInst.Value != null)
                        ScanValueForFunctionAddresses(returnInst.Value);
                }
            }
        }

        // Only scan reachable functions for cross-module calls
        foreach (var function in _module.Functions.Where(f => f.BasicBlocks.Count > 0 && reachableFunctions.Contains(f.Name)))
        {
            foreach (var block in function.BasicBlocks)
            {
                ScanBlockForCalls(block);
            }
        }

        // Emit cross-module function declarations
        if (crossModuleCalls.Count > 0)
        {
            _output.AppendLine("// Cross-module function declarations");
            foreach (var (funcName, (returnType, paramTypes)) in crossModuleCalls)
            {
                // VBCC FIX: Match function definition signature (use void + __out for struct/enum returns)
                var isStructOrEnumReturn = returnType is IrStructType or IrEnumType or IrTupleType;
                var cReturnType = isStructOrEnumReturn ? "void" : GetCType(returnType);

                // Build parameter list with __out if needed
                var paramList = new List<string>();
                if (isStructOrEnumReturn)
                {
                    paramList.Add($"{GetCType(returnType)}* __out");
                }
                paramList.AddRange(paramTypes.Select((type, index) => $"{GetCType(type)} p{index}"));

                var parameters = paramList.Count == 0 ? "void" : string.Join(", ", paramList);
                _output.AppendLine($"{cReturnType} {MangleName(funcName)}({parameters});");
            }
            _output.AppendLine();
        }

        // Only emit declarations for reachable implemented functions
        // SKIP monomorphized functions since they're static inline
        var implementedFunctions = _module.Functions
            .Where(f => !f.IsExtern
                        && f.BasicBlocks.Count > 0
                        && reachableFunctions.Contains(f.Name)
                        && !IsMonomorphizedFunction(f))
            .ToList();

        if (implementedFunctions.Count == 0)
            return;

        _output.AppendLine("// Forward declarations");
        foreach (var function in implementedFunctions)
        {
            // VBCC FIX: Match function definition signature (use void + __out for struct/enum returns)
            var isStructOrEnumReturn = function.ReturnType is IrStructType or IrEnumType or IrTupleType;
            var shouldUseOutParam = isStructOrEnumReturn;
            var returnType = shouldUseOutParam ? "void" : GetCType(function.ReturnType);
            var parameters = GetParameterList(function, shouldUseOutParam);

            // Special case: main returns int, not int32_t
            if (function.Name == "main" && returnType == "int32_t")
            {
                returnType = "int";
            }

            _output.AppendLine($"{returnType} {MangleName(function)}({parameters});");
        }

        // Runtime assert handler (implemented in novus_runtime.c)
        // Only include in debug mode
        if (_buildMode == BuildMode.Debug)
        {
            _output.AppendLine("void __novus_assert_failed(const char* file, int32_t line, int32_t col, const char* message);");
        }

        // Runtime panic handler (implemented in novus_runtime.c)
        // Always included (panic is never elided)
        _output.AppendLine("void __novus_panic(const char* message, const char* file, int32_t line, int32_t col);");

        // Runtime bounds check failure handler (implemented in novus_runtime.c)
        // Included when bounds checking is enabled
        // Note: The actual check is inlined in generated code; this function only handles failures
        if (_safetyLevel.EnableBoundsChecking())
        {
            _output.AppendLine("void __novus_bounds_check_failed(int32_t index, int32_t length, const char* file, int32_t line);");
        }

        // Runtime division by zero check (implemented in novus_runtime.c)
        // Included when division-by-zero checking is enabled
        if (_safetyLevel.EnableDivisionByZeroChecks())
        {
            _output.AppendLine("void __novus_div_check(int32_t divisor, const char* file, int32_t line);");
        }

        // Runtime null pointer check (implemented in runtime_mmu.c)
        // Included when null pointer checking is enabled
        if (_safetyLevel.EnableNullChecks())
        {
            _output.AppendLine("void __novus_null_pointer_error(const char* file, int32_t line);");
        }

        _output.AppendLine();
    }

    /// <summary>
    /// Check if a function has unresolved types that prevent C code generation.
    /// PUBLIC: Can be called from Program.cs to filter functions before generation.
    /// </summary>
    public bool HasUnresolvedTypes(IrFunction function)
    {
        // Special case: Don't skip String and Str functions - they're important for the stdlib
        var funcNameLower = function.Name.ToLower();
        if (funcNameLower.Contains("string::")  || funcNameLower.Contains("str::"))
        {
            return false;
        }

        // Check return type
        if (ContainsUnresolvedType(function.ReturnType))
            return true;

        // Check parameter types
        foreach (var param in function.Parameters)
        {
            if (ContainsUnresolvedType(param.Type))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Recursively check if a type contains unresolved generic types.
    /// </summary>
    private bool ContainsUnresolvedType(IrType type)
    {
        return type switch
        {
            IrUnresolvedGenericType => true,
            IrPartiallyResolvedGenericType => true,
            IrPointerType ptrType => ContainsUnresolvedType(ptrType.PointeeType),
            IrArrayType arrType => ContainsUnresolvedType(arrType.ElementType),
            IrReferenceType refType => ContainsUnresolvedType(refType.PointeeType),
            IrMutReferenceType mutRefType => ContainsUnresolvedType(mutRefType.PointeeType),
            IrFunctionPointerType fpType =>
                ContainsUnresolvedType(fpType.ReturnType) ||
                fpType.ParameterTypes.Any(pt => ContainsUnresolvedType(pt)),
            _ => false
        };
    }

    /// <summary>
    /// Escape a string for use in a C string literal
    /// </summary>
    private static string EscapeCString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    /// <summary>
    /// Get the C function name for an IR function
    /// </summary>
    private string GetCFunctionName(IrFunction func)
    {
        // For 'main', we keep it as 'main'
        if (func.Name == "main")
            return "main";

        // Otherwise, mangle the name to be valid C
        return MangleName(func.Name);
    }

    /// <summary>
    /// Emit a debug symbol table mapping function addresses to source locations.
    /// This enables the runtime to display file:line information when a crash occurs.
    /// The table consists of:
    /// 1. NovusDebugSymbol struct with function pointer, file, line, name
    /// 2. Global array of symbols sorted by function address
    /// 3. Count variable for runtime lookup
    /// </summary>
    private void EmitDebugSymbolTable(HashSet<string> reachableFunctions)
    {
        // Only emit for non-extern implemented functions with source locations
        var functionsWithLocations = _module.Functions
            .Where(f => !f.IsExtern && f.BasicBlocks.Count > 0
                        && reachableFunctions.Contains(f.Name)
                        && f.Location != null)
            .ToList();

        if (functionsWithLocations.Count == 0)
            return;

        _output.AppendLine();
        _output.AppendLine("// ============================================================================");
        _output.AppendLine("// Debug Symbol Table (for runtime source location lookup)");
        _output.AppendLine("// ============================================================================");
        _output.AppendLine();

        // Emit the struct definition (matches the runtime's expectation)
        _output.AppendLine("typedef struct {");
        _output.AppendLine("    void* func_addr;       // Function start address");
        _output.AppendLine("    const char* file;      // Source file path");
        _output.AppendLine("    uint16_t line;         // Line number");
        _output.AppendLine("    const char* name;      // Function name");
        _output.AppendLine("} NovusDebugSymbol;");
        _output.AppendLine();

        // Declare the registration function from the runtime
        _output.AppendLine("extern void __novus_register_debug_symbols(const NovusDebugSymbol* symbols, uint32_t count);");
        _output.AppendLine();

        // Emit the symbol table as static (accessed only via registration)
        _output.AppendLine($"static const NovusDebugSymbol __novus_debug_symbols[] = {{");

        foreach (var func in functionsWithLocations)
        {
            var loc = func.Location!;
            var fileName = EscapeCString(System.IO.Path.GetFileName(loc.FilePath));
            var funcName = EscapeCString(func.Name);
            var cFuncName = GetCFunctionName(func);

            _output.AppendLine($"    {{ (void*){cFuncName}, \"{fileName}\", {loc.Line}, \"{funcName}\" }},");
        }

        _output.AppendLine("};");
        _output.AppendLine();

        // Emit registration function that's called during startup (not static - called from main)
        _output.AppendLine("void __novus_init_debug_symbols(void) {");
        _output.AppendLine($"    __novus_register_debug_symbols(__novus_debug_symbols, {functionsWithLocations.Count});");
        _output.AppendLine("}");
        _output.AppendLine();
    }

    private void EmitFunctions(HashSet<string> reachableFunctions)
    {
        // Only emit reachable functions (dead code elimination)
        var implementedFunctions = _module.Functions
            .Where(f => !f.IsExtern && f.BasicBlocks.Count > 0 && reachableFunctions.Contains(f.Name))
            .ToList();

        if (implementedFunctions.Count == 0)
            return;

        // Check for functions with unresolved types and skip them with a warning
        var skippedFunctions = new List<string>();
        implementedFunctions = implementedFunctions
            .Where(f =>
            {
                if (HasUnresolvedTypes(f))
                {
                    skippedFunctions.Add(f.Name);
                    return false;
                }
                return true;
            })
            .ToList();

        // Warn about skipped functions
        foreach (var skipped in skippedFunctions)
        {
            System.Console.WriteLine($"WARNING: Skipping function '{skipped}' due to unresolved types (not used by this build)");
        }

        if (implementedFunctions.Count == 0)
            return;

        // Separate monomorphized functions from regular functions
        // Monomorphized functions must be emitted FIRST (before any callers) to avoid implicit declarations
        var monomorphizedFunctions = implementedFunctions.Where(f => IsMonomorphizedFunction(f)).ToList();
        var regularFunctions = implementedFunctions.Where(f => !IsMonomorphizedFunction(f)).ToList();

        _output.AppendLine("// Function implementations");

        // Emit monomorphized functions first (static inline)
        if (monomorphizedFunctions.Count > 0)
        {
            _output.AppendLine("// Monomorphized generic functions (must be defined before use)");
            foreach (var function in monomorphizedFunctions)
            {
                EmitFunction(function);
                _output.AppendLine();
            }
        }

        // Then emit regular functions
        foreach (var function in regularFunctions)
        {
            EmitFunction(function);
            _output.AppendLine();
        }
    }

    private void EmitFunction(IrFunction function)
    {
        // Reset state for each function
        _declaredVariables.Clear();
        _deferEmissionCounter = 0;  // Reset defer emission counter for unique labels
        _indexAccessInfo.Clear();
        _activatedDeferBlocks.Clear();
        _inlineableComparisons.Clear();  // VBCC workaround: reset comparison tracking

        // Track which parameters were converted to pointers in the C signature
        _pointerConvertedParameters.Clear();
        foreach (var param in function.Parameters)
        {
            if (param.Type is IrStructType structType && TypeContainsHeapData(structType))
            {
                _pointerConvertedParameters.Add(param.Name);
            }
        }

        // Set current function for defer cleanup
        _currentEmittingFunction = function;
        _memberAccessInfo.Clear();
        _fieldAccessChainInfo.Clear();
        _addressOnlyStructMemberAccess.Clear();

        // Reset debug line tracking for this function
        _lastEmittedDebugLine = -1;
        _debugLabelCounter = 0;
        // Reset #line directive tracking for this function
        _lastLineDirectiveLine = -1;
        _lastLineDirectiveFile = null;

        // Run liveness analysis for variable slot reuse
        var livenessAnalysis = new LivenessAnalysis(function);
        var (variableToSlot, slotTypes) = livenessAnalysis.Analyze();
        _variableToSlot = variableToSlot;
        _slotTypes = slotTypes;

        // Pre-scan function for address-only struct member accesses (VBCC 68K fix)
        // IMPORTANT: This must be called AFTER liveness analysis sets up _variableToSlot,
        // so that SanitizeVariableName correctly maps IR names to slot names.
        ScanForAddressOnlyStructMemberAccesses(function);

        // Dead label elimination: collect all branch targets
        // Labels not targeted by any branch will be skipped during emission (VBCC warning 148)
        CollectReachableLabels(function);

        // VBCC FIX: For struct/enum/tuple returns on 68k, use output parameter pattern
        // VBCC can't reliably return structs by value on 68k
        var isStructOrEnumReturn = function.ReturnType is IrStructType or IrEnumType or IrTupleType or IrTupleType;
        var shouldUseOutParam = isStructOrEnumReturn;
        var returnType = shouldUseOutParam ? "void" : GetCType(function.ReturnType);
        var parameters = GetParameterList(function, shouldUseOutParam);

        // Don't mangle exported functions - use original name for C linkage
        var funcName = function.IsExported ? function.Name : MangleName(function);

        // Special case: main must return 'int' for VBCC compatibility
        if (funcName == "main" && returnType == "int32_t")
        {
            returnType = "int";
        }

        // Check for @interrupt attribute - use vbcc's __interrupt keyword
        // This tells vbcc to save/restore ALL registers and use RTE instead of RTS
        var isInterruptHandler = function.Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.Interrupt) ?? false;
        var interruptPrefix = isInterruptHandler ? "__interrupt " : "";

        // Monomorphized trait implementations are generated in multiple modules.
        // Don't try to use inline/static - just let them be regular functions and
        // handle the duplicate symbols at the Program.cs level by generating them once.
        _output.AppendLine($"{interruptPrefix}{returnType} {funcName}({parameters}) {{");

        // Add comment for interrupt handlers explaining the calling convention
        if (isInterruptHandler)
        {
            _output.AppendLine("    /* @interrupt: This function saves/restores ALL registers (d0-d7/a0-a6) */");
            _output.AppendLine("    /* and returns via RTE instead of RTS. Do NOT call blocking functions! */");
        }

        // Check for @atomic attribute - wrap function body in Forbid/Permit
        var isAtomic = function.Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.Atomic) ?? false;
        if (isAtomic)
        {
            _output.AppendLine("    /* @atomic: Forbid task switching for this function */");
            _output.AppendLine("    Forbid();");
        }

        // Check for @no_interrupts attribute - wrap function body in Disable/Enable
        // WARNING: This is extremely dangerous and should be used sparingly!
        var isNoInterrupts = function.Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.NoInterrupts) ?? false;
        if (isNoInterrupts)
        {
            _output.AppendLine("    /* @no_interrupts: Disable ALL interrupts - KEEP SECTION SHORT! */");
            _output.AppendLine("    Disable();");
        }

        // In debug builds, emit stack overflow check at function entry
        // This checks if there's enough stack space before allocating locals
        if (_buildMode == BuildMode.Debug && _safetyLevel >= SafetyLevel.Basic)
        {
            // Estimate stack frame size (rough approximation based on local vars)
            int estimatedStackSize = 64; // Base overhead for frame pointer, saved regs
            foreach (var local in function.LocalVariables)
            {
                estimatedStackSize += CalculateTypeSize(local.Type);
            }

            // Get line number from function location if available
            var lineNum = function.Location?.Line ?? 0;

            _output.AppendLine($"    __novus_check_stack({estimatedStackSize}, \"{EscapeString(funcName)}\", {lineNum});");
        }

        // Build CFG for scope analysis and return path checking
        var cfg = new ControlFlowGraph(function);
        var (functionScopedVars, blockScopedVars) = cfg.ComputeVariableScopes();

        // Emit slot declarations for temporaries that have been consolidated
        // These are declared at function scope since they may be reused across the function
        foreach (var (slotName, slotType) in slotTypes)
        {
            // Skip unit type () / void - it has no runtime representation and can't be stored in a variable
            if (slotType is IrVoidType)
                continue;
            if (slotType is IrTupleType tupleType && tupleType.ElementTypes.Count == 0)
                continue;

            // DEFENSE IN DEPTH: Zero-initialize ALL slot variables.
            // 1. Structs/enums MUST use {0} for 68k alignment (VBCC ensures 2-byte alignment for initialized data)
            // 2. Primitives should also be zero-initialized as defense against liveness analysis bugs
            // This prevents UB from uninitialized variable access if control flow analysis is incorrect.
            var decl = (slotType is IrEnumType || slotType is IrStructType || slotType is IrTupleType)
                ? GetCVariableDeclaration(slotType, slotName, "= {0}")
                : GetCVariableDeclaration(slotType, slotName, "= 0");

            _output.AppendLine($"    {decl};");
            _declaredVariables.Add(slotName);
        }

        // Collect all non-temporary variables and their types for declaration
        var localDeclVars = new Dictionary<string, (IrType Type, bool IsArray, int ArraySize)>();
        var storedVars = new Dictionary<string, IrType>();

        // Helper to check if a variable is mapped to a slot (i.e., is a temporary)
        bool IsMappedToSlot(string varName)
        {
            return variableToSlot.ContainsKey(varName);
        }

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLocalDecl localDecl)
                {
                    // Use raw sanitization without slot mapping for collection phase
                    var varName = localDecl.Name.StartsWith("%") ? "_" + localDecl.Name.Substring(1) : localDecl.Name;

                    // Skip if this variable is mapped to a slot (check ORIGINAL name since liveness uses original names)
                    if (IsMappedToSlot(localDecl.Name))
                        continue;

                    // Skip unit type () variables - they have no runtime representation
                    if (localDecl.Type is IrTupleType tupleType && tupleType.ElementTypes.Count == 0)
                        continue;

                    // BUG FIX: Skip if this variable is a module static - don't shadow it with a local
                    if (IsModuleStaticVariable(localDecl.Name))
                        continue;

                    if (!localDeclVars.ContainsKey(varName))
                    {
                        var isArray = localDecl.Type is IrArrayType;
                        var arraySize = isArray ? ((IrArrayType)localDecl.Type).Length : 0;
                        localDeclVars[varName] = (localDecl.Type, isArray, arraySize);
                    }
                }
                else if (instruction is IrStore store)
                {
                    var varName = store.VariableName.StartsWith("%") ? "_" + store.VariableName.Substring(1) : store.VariableName;

                    // Skip if this variable is mapped to a slot (check ORIGINAL name since liveness uses original names)
                    if (IsMappedToSlot(store.VariableName))
                        continue;

                    // BUG FIX: Skip if this variable is a module static - don't shadow it with a local
                    if (IsModuleStaticVariable(store.VariableName))
                        continue;

                    if (!storedVars.ContainsKey(varName))
                    {
                        storedVars[varName] = store.Value.Type;
                    }
                }
            }
        }

        // LIFETIME FIX: Analyze which arrays are filled with compile-time constant values
        // via IrIndexStore instructions. Such arrays can be made 'static' to ensure they
        // live for the entire program duration - critical when passed to external functions
        // (like MUI's make_radio) that may store pointers to the array.
        var staticConstArrays = AnalyzeStaticConstantArrays(function, localDeclVars);

        // Pre-declare ONLY function-scoped variables at function start
        // Variables that are only used in one block will be declared in that block
        foreach (var (varName, varInfo) in localDeclVars)
        {
            // Only declare at function scope if the variable crosses block boundaries
            if (!functionScopedVars.Contains(varName))
                continue;

            var (varType, isArray, arraySize) = varInfo;

            if (isArray)
            {
                var arrayType = (IrArrayType)varType;
                var elementType = GetCType(arrayType.ElementType);

                // LIFETIME FIX: Arrays filled with compile-time constants should be 'static'
                var staticPrefix = staticConstArrays.Contains(varName) ? "static " : "";
                _output.AppendLine($"    {staticPrefix}{elementType} {varName}[{arraySize}];");
            }
            else
            {
                var decl = GetCVariableDeclaration(varType, varName);
                var cType = GetCType(varType);

                // CRITICAL FIX FOR 68K ALIGNMENT:
                // Enums, structs, and tuples MUST be initialized to force VBCC to align them properly
                // VBCC FIX: Split declaration and memset onto separate lines for compatibility
                if (varType is IrEnumType || varType is IrStructType || varType is IrTupleType)
                {
                    _output.AppendLine($"    {decl};");
                    _output.AppendLine($"    __novus_memset(&{varName}, 0, sizeof({cType}));");
                }
                else
                {
                    _output.AppendLine($"    {decl};");
                }
            }
            _declaredVariables.Add(varName);
        }

        // Also emit function-scoped declarations for stored variables
        foreach (var (varName, varType) in storedVars)
        {
            if (!localDeclVars.ContainsKey(varName) && functionScopedVars.Contains(varName))
            {
                // CRITICAL FIX FOR 68K ALIGNMENT:
                // Structs, enums, and tuples MUST use {0} initializer to guarantee proper alignment on 68000.
                var decl = (varType is IrEnumType || varType is IrStructType || varType is IrTupleType)
                    ? GetCVariableDeclaration(varType, varName, "= {0}")
                    : GetCVariableDeclaration(varType, varName);

                _output.AppendLine($"    {decl};");
                _declaredVariables.Add(varName);
            }
        }

        // Store scope info for use during block emission
        _currentBlockScopedVars = blockScopedVars;
        _currentLocalDeclVars = localDeclVars;
        _currentStoredVars = storedVars;
        _currentStaticConstArrays = staticConstArrays;

        // MMU protection: Initialize at start of main() if memory tracking is enabled
        if (_safetyLevel.EnableMemoryTracking() && funcName == "main")
        {
            _output.AppendLine("    // Initialize MMU protection (null page, guard pages)");
            _output.AppendLine("    __novus_init_mmu_protection();");
        }

        // Stack bounds initialization: Initialize at start of main() in debug builds
        if (_buildMode == BuildMode.Debug && _safetyLevel >= SafetyLevel.Basic && funcName == "main")
        {
            _output.AppendLine("    // Initialize stack bounds for overflow detection");
            _output.AppendLine("    __novus_init_stack_bounds();");
        }

        // Emit function body
        foreach (var block in function.BasicBlocks)
        {
            EmitBasicBlock(block);
        }

        // Clear scope info
        _currentBlockScopedVars = null;
        _currentLocalDeclVars = null;
        _currentStoredVars = null;
        _currentStaticConstArrays = null;
        _variableToSlot = null;
        _slotTypes = null;

        var allPathsReturn = cfg.AllPathsReturn();

        // Emit deferred cleanup at end of function ONLY if not all paths return
        // (if all paths return, cleanup was already emitted before each return statement)
        if (!allPathsReturn)
        {
            EmitDeferredCleanup(function, 1);

            // Emit @no_interrupts cleanup (Enable) if attribute is present
            // This must come AFTER deferred cleanup since defer might need interrupts
            if (function.Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.NoInterrupts) ?? false)
            {
                _output.AppendLine("    Enable(); /* @no_interrupts cleanup */");
            }

            // Emit @atomic cleanup (Permit) if attribute is present
            // This must come AFTER deferred cleanup and Enable since we want to restore task switching last
            if (function.Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.Atomic) ?? false)
            {
                _output.AppendLine("    Permit(); /* @atomic cleanup */");
            }
        }

        _output.AppendLine("}");

        // Clear current function
        _currentEmittingFunction = null;
    }

    private void EmitBasicBlock(IrBasicBlock block)
    {
        // Emit the block's label if it's a target of a branch (not just the entry block)
        // This is needed for derived methods that generate multi-block control flow
        // Skip if label was already emitted (prevents duplicate from IrLabel instructions)
        if (block.Label != "entry" && _reachableLabels.Contains(block.Label) && !_emittedLabels.Contains(block.Label))
        {
            var labelName = block.Label + _labelSuffix;
            _output.AppendLine($"{labelName}:;");
            _emittedLabels.Add(block.Label);
        }

        // Check if this block has block-scoped variables that need to be declared
        HashSet<string>? blockVars = null;
        var hasBlockScopedVars = _currentBlockScopedVars != null &&
                                  _currentBlockScopedVars.TryGetValue(block.Label, out blockVars) &&
                                  blockVars != null && blockVars.Count > 0;

        if (hasBlockScopedVars)
        {
            // Open a new scope for block-local variables
            _output.AppendLine("    {");

            // Emit declarations for block-scoped variables
            foreach (var varName in blockVars!)
            {
                // Skip if already declared (shouldn't happen, but defensive)
                if (_declaredVariables.Contains(varName))
                    continue;

                // Check if this is a local decl variable
                if (_currentLocalDeclVars != null && _currentLocalDeclVars.TryGetValue(varName, out var varInfo))
                {
                    var (varType, isArray, arraySize) = varInfo;

                    if (isArray)
                    {
                        var arrayType = (IrArrayType)varType;
                        var elementType = GetCType(arrayType.ElementType);

                        // LIFETIME FIX: Arrays filled with compile-time constants should be 'static'
                        var staticPrefix = (_currentStaticConstArrays?.Contains(varName) ?? false) ? "static " : "";
                        _output.AppendLine($"        {staticPrefix}{elementType} {varName}[{arraySize}];");
                    }
                    else
                    {
                        var decl = GetCVariableDeclaration(varType, varName);
                        var cType = GetCType(varType);

                        // CRITICAL FIX FOR 68K ALIGNMENT
                        // VBCC FIX: Split declaration and memset onto separate lines for compatibility
                        if (varType is IrEnumType || varType is IrStructType || varType is IrTupleType)
                        {
                            _output.AppendLine($"        {decl};");
                            _output.AppendLine($"        __novus_memset(&{varName}, 0, sizeof({cType}));");
                        }
                        else
                        {
                            _output.AppendLine($"        {decl};");
                        }
                    }
                    _declaredVariables.Add(varName);
                }
                // Check if this is a stored variable
                else if (_currentStoredVars != null && _currentStoredVars.TryGetValue(varName, out var varType2))
                {
                    // CRITICAL FIX FOR 68K ALIGNMENT:
                    // Structs, enums, and tuples MUST use {0} initializer to guarantee proper alignment on 68000.
                    var decl = (varType2 is IrEnumType || varType2 is IrStructType || varType2 is IrTupleType)
                        ? GetCVariableDeclaration(varType2, varName, "= {0}")
                        : GetCVariableDeclaration(varType2, varName);

                    _output.AppendLine($"        {decl};");
                    _declaredVariables.Add(varName);
                }
            }
        }

        // Emit the block's instructions
        foreach (var instruction in block.Instructions)
        {
            EmitInstruction(instruction);
        }

        if (hasBlockScopedVars)
        {
            // Close the scope
            _output.AppendLine("    }");
        }
    }

    /// <summary>
    /// Emit a #line directive if the instruction has a location that differs from the last emitted directive.
    /// This makes compiler error messages reference the original Novus source file and line.
    /// Only emitted in Debug builds to help with debugging generated C code.
    /// </summary>
    private void MaybeEmitLineDirective(IrInstruction instruction)
    {
        // Only emit #line directives in debug builds
        if (_buildMode != BuildMode.Debug)
            return;

        // Skip if no location info
        if (instruction.Location == null)
            return;

        var loc = instruction.Location;

        // Skip if same file and line as last directive (avoid redundant directives)
        if (loc.Line == _lastLineDirectiveLine && loc.FilePath == _lastLineDirectiveFile)
            return;

        // Skip IrLabel instructions - they're not real statements
        if (instruction is IrLabel)
            return;

        _lastLineDirectiveLine = loc.Line;
        _lastLineDirectiveFile = loc.FilePath;

        // Emit #line directive: #line linenum "filename"
        // The filename must be escaped for C string literal (quotes, backslashes)
        var escapedPath = loc.FilePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        _output.AppendLine($"#line {loc.Line} \"{escapedPath}\"");
    }

    /// <summary>
    /// Emit a debug line marker if the instruction has a location that differs from the last emitted line.
    /// This creates an addressable label in the generated code that the runtime can map back to source.
    /// Only emitted when building with SafetyLevel.Paranoid (--safety-level 3).
    /// </summary>
    private void MaybeEmitDebugLineMarker(IrInstruction instruction)
    {
        // Only emit debug markers in paranoid safety mode (level 3)
        if (_safetyLevel < SafetyLevel.Paranoid)
            return;

        // Skip if no location info
        if (instruction.Location == null)
            return;

        var loc = instruction.Location;

        // Skip if same line as last marker (avoid redundant markers)
        if (loc.Line == _lastEmittedDebugLine)
            return;

        // Skip IrLabel instructions - they're not real statements
        if (instruction is IrLabel)
            return;

        _lastEmittedDebugLine = loc.Line;

        // Generate unique label name: __dbg_{funcname}_{counter}
        var funcName = _currentEmittingFunction != null ? MangleName(_currentEmittingFunction) : "unknown";
        var labelName = $"__dbg_{funcName}_{_debugLabelCounter++}";
        var fileName = System.IO.Path.GetFileName(loc.FilePath);

        // Record this marker for the debug symbol table
        _debugLineMarkers.Add((labelName, fileName, loc.Line, _currentEmittingFunction?.Name ?? "unknown"));

        // Note: VBCC doesn't support GCC-style __asm volatile() for inline labels.
        // Instead, we emit a comment marker that can be used for approximate line info.
        // A future optimization could post-process assembly output to insert actual labels,
        // but the comment markers provide sufficient debug information for most use cases.
        _output.AppendLine($"    /* DBG: {labelName} = {fileName}:{loc.Line} */");
    }

    private void EmitInstruction(IrInstruction instruction)
    {
        // Emit #line directive for better error messages in debug builds
        MaybeEmitLineDirective(instruction);

        // Emit debug line marker before the instruction if location changed
        MaybeEmitDebugLineMarker(instruction);

        switch (instruction)
        {
            case IrLocalDecl localDecl:
                EmitLocalDecl(localDecl);
                break;

            case IrStore store:
                EmitStore(store);
                break;

            case IrBinaryOp binaryOp:
                EmitBinaryOp(binaryOp);
                break;

            case IrCall call:
                EmitCall(call);
                break;

            case IrIndirectCall indirectCall:
                EmitIndirectCall(indirectCall);
                break;

            case IrLabel label:
                EmitLabel(label);
                break;

            case IrBranch branch:
                EmitBranch(branch);
                break;

            case IrConditionalBranch condBranch:
                EmitConditionalBranch(condBranch);
                break;

            case IrReturn returnInst:
                EmitReturn(returnInst);
                break;

            case IrAssert assert:
                EmitAssert(assert);
                break;

            case IrPanic panic:
                EmitPanic(panic);
                break;

            case IrMatch match:
                EmitMatch(match);
                break;

            case IrExtractTag extractTag:
                EmitExtractTag(extractTag);
                break;

            case IrExtractVariantData extractData:
                EmitExtractVariantData(extractData);
                break;

            case IrMemberAccess memberAccess:
                EmitMemberAccess(memberAccess);
                break;

            case IrIndexAccess indexAccess:
                EmitIndexAccess(indexAccess);
                break;

            case IrIndexStore indexStore:
                EmitIndexStore(indexStore);
                break;

            case IrIndexedFieldStore indexedFieldStore:
                EmitIndexedFieldStore(indexedFieldStore);
                break;

            case IrMemberStore memberStore:
                EmitMemberStore(memberStore);
                break;

            case IrDereferenceStore derefStore:
                EmitDereferenceStore(derefStore);
                break;

            case IrCreateClosure createClosure:
                EmitCreateClosure(createClosure);
                break;

            case IrInvokeClosure invokeClosure:
                EmitInvokeClosure(invokeClosure);
                break;

            case IrLoadCapture loadCapture:
                EmitLoadCapture(loadCapture);
                break;

            case IrStoreCapture storeCapture:
                EmitStoreCapture(storeCapture);
                break;

            case IrDefer defer:
                // IrDefer marker - set the flag to indicate this defer block is now active
                // Find which defer block this corresponds to
                for (int i = 0; i < _currentEmittingFunction!.DeferredBlocks.Count; i++)
                {
                    if (_currentEmittingFunction.DeferredBlocks[i] == defer.DeferredBlock)
                    {
                        var deferIndex = i + 1;
                        _output.AppendLine($"    _defer_{deferIndex}_active = true;");
                        // Track that this defer block is now active in the code flow
                        _activatedDeferBlocks.Add(deferIndex);
                        break;
                    }
                }
                break;

            case IrInlineAsm inlineAsm:
                EmitInlineAsm(inlineAsm);
                break;

            default:
                // Unhandled instruction type - emit a comment for debugging.
                // All standard IR instructions should be handled in the cases above.
                _output.AppendLine($"    // Unhandled: {instruction.GetType().Name}");
                break;
        }
    }

    /// <summary>
    /// Deactivate defer blocks for variables that are being moved into a return value.
    /// This prevents use-after-free bugs where Drop is called on a resource that has been moved.
    ///
    /// Defer blocks are named: autoclean_{varName}_{counter}
    /// We extract the variable name and match it against variables in the return value.
    /// </summary>
    private void DeactivateDefersForMovedVariables(IrValue returnValue)
    {
        if (_currentEmittingFunction == null || _currentEmittingFunction.DeferredBlocks.Count == 0)
            return;

        // Collect all variable names used in the return value
        var movedVariables = new HashSet<string>();
        CollectMovedVariablesFromValue(returnValue, movedVariables);

        if (movedVariables.Count == 0)
            return;

        // Check each defer block to see if it's for a moved variable
        for (int i = 0; i < _currentEmittingFunction.DeferredBlocks.Count; i++)
        {
            var deferBlock = _currentEmittingFunction.DeferredBlocks[i];
            var deferIndex = i + 1;

            // Skip if this defer block isn't active
            if (!_activatedDeferBlocks.Contains(deferIndex))
                continue;

            // Defer blocks for RAII cleanup are named: autoclean_{varName}_{counter}
            // Extract the variable name from the label
            if (deferBlock.Label.StartsWith("autoclean_"))
            {
                // Remove "autoclean_" prefix
                var remainder = deferBlock.Label.Substring("autoclean_".Length);

                // Find the last underscore followed by digits (the counter)
                int lastUnderscore = remainder.LastIndexOf('_');
                if (lastUnderscore > 0)
                {
                    var varName = remainder.Substring(0, lastUnderscore);

                    // Check if this variable is being moved
                    if (movedVariables.Contains(varName))
                    {
                        // Deactivate this defer block - the variable is being moved, not dropped
                        _output.AppendLine($"    _defer_{deferIndex}_active = false;");

                        // CRITICAL: Also remove from _activatedDeferBlocks so EmitDeferredCleanup
                        // won't emit a dead-code check like "if (false) { ... }".
                        // VBCC miscompiles this pattern (sets Z flag from stack operations instead
                        // of testing the boolean variable), causing Guru 80000004 crashes.
                        _activatedDeferBlocks.Remove(deferIndex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recursively collect variable names from a value that might be struct/enum literals.
    /// These are the variables being "moved" into the return value.
    /// </summary>
    private void CollectMovedVariablesFromValue(IrValue value, HashSet<string> movedVariables)
    {
        switch (value)
        {
            case IrVariable variable:
                // Direct variable use - this is a move
                movedVariables.Add(variable.Name);
                break;

            case IrStructLiteral structLit:
                // Recursively check all field values
                foreach (var kvp in structLit.FieldValues)
                {
                    CollectMovedVariablesFromValue(kvp.Value, movedVariables);
                }
                break;

            case IrEnumValue enumValue:
                // Check associated values
                foreach (var assocValue in enumValue.AssociatedValues)
                {
                    CollectMovedVariablesFromValue(assocValue, movedVariables);
                }
                break;

            case IrTupleLiteral tupleLit:
                // Recursively check all tuple elements - tuples can contain moved variables
                foreach (var element in tupleLit.Elements)
                {
                    CollectMovedVariablesFromValue(element, movedVariables);
                }
                break;

            // Other value types (literals, function calls, etc.) don't involve moving variables
        }
    }

    /// <summary>
    /// Emit deferred cleanup blocks in LIFO order (last registered, first executed).
    /// This is called before function exit points (return statements, end of function).
    /// Only emits cleanup for defer blocks that have been activated in the code flow so far.
    /// </summary>
    private void EmitDeferredCleanup(IrFunction function, int indentLevel = 1)
    {
        if (function.DeferredBlocks.Count == 0)
            return;

        // If no defer blocks have been activated yet, don't emit any cleanup
        if (_activatedDeferBlocks.Count == 0)
            return;

        var indent = new string(' ', indentLevel * 4);

        // Generate unique suffix for this defer emission to avoid label conflicts
        // when the same defer block is emitted at multiple exit points
        var emissionId = _deferEmissionCounter++;
        var previousSuffix = _labelSuffix;
        _labelSuffix = $"_d{emissionId}";

        try
        {
            // Execute deferred blocks in LIFO order (reverse order)
            for (int i = function.DeferredBlocks.Count - 1; i >= 0; i--)
            {
                var deferBlock = function.DeferredBlocks[i];

                // Use 1-based index for the flag name (defer_1, defer_2, etc.)
                var deferIndex = i + 1;

                // CRITICAL FIX: Only emit cleanup for defer blocks that have been activated
                // in the code flow up to this point. This prevents referencing variables
                // that haven't been declared yet (e.g., variables declared inside match arms).
                if (!_activatedDeferBlocks.Contains(deferIndex))
                    continue;

                // Wrap defer block in if check - only execute if defer was reached
                _output.AppendLine($"{indent}// Defer cleanup block {function.DeferredBlocks.Count - i}");
                _output.AppendLine($"{indent}if (_defer_{deferIndex}_active) {{");

                // Emit all instructions in the deferred block
                foreach (var instruction in deferBlock.Instructions)
                {
                    // Save current output position
                    var beforeLength = _output.Length;

                    // Emit instruction (will write to _output with default indentation)
                    EmitInstruction(instruction);

                    // Get what was emitted and fix indentation
                    var emitted = _output.ToString().Substring(beforeLength);
                    _output.Length = beforeLength;  // Remove what we just added

                    // Re-emit with correct indentation (one more level for being inside the if)
                    var lines = emitted.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // Strip existing indentation and apply our indentation + extra indent for if block
                        var trimmed = line.TrimStart();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            _output.AppendLine($"{indent}    {trimmed}");
                        }
                    }
                }

                // Close the if block
                _output.AppendLine($"{indent}}}");
            }
        }
        finally
        {
            // Restore previous label suffix
            _labelSuffix = previousSuffix;
        }
    }

    private void EmitLocalDecl(IrLocalDecl localDecl)
    {
        // Skip void/unit type local declarations - they have no runtime representation
        // IMPORTANT: Check this BEFORE calling EmitValue to avoid trying to declare void variables
        if (localDecl.Type is IrVoidType)
            return;
        if (localDecl.Type is IrTupleType tupleType && tupleType.ElementTypes.Count == 0)
            return;

        // BUG FIX: Skip if this variable is a module static - don't shadow it with a local
        // Module statics are already declared as extern at the top of the file
        if (IsModuleStaticVariable(localDecl.Name))
            return;

        var varName = SanitizeVariableName(localDecl.Name);

        // VBCC FIX: When initializing from struct/enum literals, avoid C99 compound literals
        // which VBCC doesn't support. Instead, use field-by-field assignment.
        var isStructLiteral = localDecl.InitialValue is IrStructLiteral;
        var isEnumLiteral = localDecl.InitialValue is IrEnumValue;

        // Don't call EmitValue yet for struct/enum literals - we'll handle them specially
        var initValue = (isStructLiteral || isEnumLiteral) ? null : (localDecl.InitialValue != null ? EmitValue(localDecl.InitialValue) : null);

        // Check if this variable has already been declared in this function
        if (_declaredVariables.Contains(varName))
        {
            // Already declared - emit assignment only

            // VBCC FIX: For struct literals, emit field-by-field assignment instead of compound literal
            if (localDecl.InitialValue is IrStructLiteral structLitAssign)
            {
                var structTypeForFields = structLitAssign.Type as IrStructType;
                foreach (var kvp in structLitAssign.FieldValues)
                {
                    // VBCC FIX: Array fields cannot be assigned with brace initializers
                    // STRICT ALIASING FIX: Use static const temporary instead of compound literal
                    if (kvp.Value is IrArrayLiteral arrayFieldLitAssign)
                    {
                        // Get the ACTUAL field type from the struct definition, not the literal's type
                        // This handles cases like [0; 32] where literal has [i32;32] but field is [u8;32]
                        var actualFieldType = structTypeForFields?.GetField(kvp.Key)?.Type as IrArrayType;
                        var arrayFieldType = actualFieldType ?? (arrayFieldLitAssign.Type as IrArrayType);

                        // VBCC 68K FIX: Use safe array copy to avoid compound literal alignment issues
                        EmitSafeArrayCopy($"{varName}.{kvp.Key}", arrayFieldLitAssign, arrayFieldType!);
                    }
                    else
                    {
                        var fieldValue = EmitValue(kvp.Value);
                        _output.AppendLine($"    {varName}.{kvp.Key} = {fieldValue};");

                        // CRITICAL FIX: Move semantics for struct fields containing droppable content
                        var fieldSourceIsLocalVar = kvp.Value is IrVariable;
                        var fieldSourceIsSlot = fieldValue != null && fieldValue.StartsWith("_slot_");
                        if ((fieldSourceIsLocalVar || fieldSourceIsSlot) && TypeContainsDroppableContent(kvp.Value.Type))
                        {
                            var fieldCType = GetCType(kvp.Value.Type);
                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                            _output.AppendLine($"    __novus_memset(&{fieldValue}, 0, sizeof({fieldCType}));");
                        }
                    }
                }
            }
            // VBCC FIX: For enum literals with associated data, emit field-by-field assignment
            else if (localDecl.InitialValue is IrEnumValue enumValueAssign)
            {
                var enumType = enumValueAssign.Type as IrEnumType;
                var enumName = MangleName(enumType!);
                bool hasAnyData = enumType!.Variants.Any(v => v.HasAssociatedData);

                if (hasAnyData)
                {
                    // Emit tag assignment
                    _output.AppendLine($"    {varName}.tag = {enumName}_{enumValueAssign.VariantName};");

                    // Emit associated data assignments if present
                    if (enumValueAssign.AssociatedValues.Count > 0)
                    {
                        // Check if this is a unit type
                        bool isUnitType = enumValueAssign.AssociatedValues.Count == 1 &&
                                         enumValueAssign.AssociatedValues[0].Type is IrTupleType unitTupleType &&
                                         unitTupleType.ElementTypes.Count == 0;

                        if (!isUnitType)
                        {
                            for (int i = 0; i < enumValueAssign.AssociatedValues.Count; i++)
                            {
                                // VBCC_004: Compound literals cause misalignment on 68k - emit field-by-field for nested struct literals
                                if (enumValueAssign.AssociatedValues[i] is IrStructLiteral nestedStructLit)
                                {
                                    foreach (var kvp in nestedStructLit.FieldValues)
                                    {
                                        var fieldValue = EmitValue(kvp.Value);
                                        _output.AppendLine($"    {varName}.data.{enumValueAssign.VariantName}._{i}.{kvp.Key} = {fieldValue};");
                                    }
                                }
                                // VBCC_004: Compound literals cause misalignment on 68k - emit field-by-field for nested enum literals
                                else if (enumValueAssign.AssociatedValues[i] is IrEnumValue nestedEnumValueAssign)
                                {
                                    EmitNestedEnumFieldByField($"{varName}.data.{enumValueAssign.VariantName}._{i}", nestedEnumValueAssign);
                                }
                                else
                                {
                                    var assocValue = EmitValue(enumValueAssign.AssociatedValues[i]);
                                    var assocValueType = enumValueAssign.AssociatedValues[i].Type;
                                    // VBCC 68K FIX: Use memcpy for struct types to avoid illegal instruction on 68040
                                    if (TypeRequiresMemcpy(assocValueType))
                                    {
                                        var cType = GetCType(assocValueType);
                                        _output.AppendLine($"    __novus_memcpy((uint8_t*)&{varName}.data.{enumValueAssign.VariantName}._{i}, (uint8_t*)&{assocValue}, sizeof({cType}));");

                                        // CRITICAL FIX: Move semantics when copying local variable into enum variant data.
                                        // Zero the source to prevent deferred cleanup from double-freeing.
                                        var sourceIsLocalVar = enumValueAssign.AssociatedValues[i] is IrVariable;
                                        if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                        {
                                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                                            _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                        }
                                    }
                                    else
                                    {
                                        _output.AppendLine($"    {varName}.data.{enumValueAssign.VariantName}._{i} = {assocValue};");

                                        // CRITICAL FIX: Move semantics for direct assignment too
                                        var sourceIsLocalVar = enumValueAssign.AssociatedValues[i] is IrVariable;
                                        if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                        {
                                            var cType = GetCType(assocValueType);
                                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                                            _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Unit type - set dummy field
                            _output.AppendLine($"    {varName}.data.{enumValueAssign.VariantName}._dummy = 0;");
                        }
                    }
                }
                else
                {
                    // For enums without associated data, just assign the tag value
                    initValue = EmitValue(localDecl.InitialValue);
                    _output.AppendLine($"    {varName} = {initValue};");
                }
            }
            else if (localDecl.Type is IrArrayType arrayType && localDecl.InitialValue is IrArrayLiteral arrayLiteral)
            {
                // Arrays can't be assigned with initializer syntax after declaration.
                // VBCC 68K FIX: Use safe array copy to avoid compound literal alignment issues
                EmitSafeArrayCopy(varName, arrayLiteral, arrayType);
            }
            else
            {
                // Compute initValue now if we haven't already
                if (initValue == null && localDecl.InitialValue != null)
                    initValue = EmitValue(localDecl.InitialValue);

                // For struct/enum/tuple types that were pre-declared with {0} initializer, don't emit a `= 0` assignment.
                // The variable was already zero-initialized, and `structVar = 0` is invalid C.
                // Match result variables are initialized this way by the IR builder when we don't know the type yet.
                if ((localDecl.Type is IrEnumType || localDecl.Type is IrStructType || localDecl.Type is IrTupleType) &&
                    localDecl.InitialValue is IrConstant constVal &&
                    constVal.Value is long longVal && longVal == 0)
                {
                    // Skip the assignment - variable already zero-initialized with {0} at function start
                }
                // VBCC FIX: For complex types (nested structs OR enums with unions), use memcpy instead of assignment.
                // VBCC generates illegal instructions on 68040 for struct-by-value copies of these types.
                else if (localDecl.InitialValue != null && TypeRequiresMemcpy(localDecl.InitialValue.Type))
                {
                    var cType = GetCType(localDecl.Type);

                    // BUG FIX: Check if source is a pointer-converted parameter.
                    // These are already pointers in C (e.g., Sleep* sleep_future), so we should
                    // use them directly instead of taking their address with &.
                    var sourceIsPointerConvertedParam = localDecl.InitialValue is IrVariable srcVar &&
                                                        _pointerConvertedParameters.Contains(srcVar.Name);
                    var sourceAddr = sourceIsPointerConvertedParam ? $"(uint8_t*){initValue}" : $"(uint8_t*)&{initValue}";

                    _output.AppendLine($"    __novus_memcpy((uint8_t*)&{varName}, {sourceAddr}, sizeof({cType}));");

                    // CRITICAL FIX: Implement move semantics when copying from ANY local variable.
                    // When the source is a local variable (including slot variables and pattern-bound
                    // variables like `block` from `Some(block) => { var b = block; ... }`) AND the
                    // type contains Drop-able data, we must zero the source after copying to prevent
                    // double-free. Both source and target would otherwise contain the same pointer,
                    // and both would have Drop called when they go out of scope.
                    //
                    // We check if the InitialValue is an IrVariable (local variable reference) OR
                    // if the emitted value looks like a slot variable (_slot_ prefix).
                    var sourceIsLocalVar = localDecl.InitialValue is IrVariable;
                    var sourceIsSlot = initValue != null && initValue.StartsWith("_slot_");
                    if ((sourceIsLocalVar || sourceIsSlot) && TypeContainsDroppableContent(localDecl.Type))
                    {
                        _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                        // Use same address expression for memset
                        var zeroAddr = sourceIsPointerConvertedParam ? initValue : $"&{initValue}";
                        _output.AppendLine($"    __novus_memset({zeroAddr}, 0, sizeof({cType}));");
                    }
                }
                else
                {
                    var cType = GetCType(localDecl.Type);
                    var initType = GetCType(localDecl.InitialValue!.Type);

                    // SAFETY: Always add explicit cast when assigning integer constants to pointer types
                    var needsCast = initType != cType;
                    if (!needsCast && localDecl.Type is IrPointerType && localDecl.InitialValue is IrConstant)
                    {
                        needsCast = true;
                    }

                    if (needsCast)
                    {
                        _output.AppendLine($"    {varName} = ({cType}){initValue};");
                    }
                    else
                    {
                        _output.AppendLine($"    {varName} = {initValue};");
                    }

                    // CRITICAL FIX: Move semantics for direct assignment from local variables.
                    // Same logic as the memcpy branch above - when copying from a local variable
                    // that contains Drop-able content, zero the source to prevent double-free.
                    var sourceIsLocalVar = localDecl.InitialValue is IrVariable;
                    var sourceIsSlot = initValue != null && initValue.StartsWith("_slot_");
                    if ((sourceIsLocalVar || sourceIsSlot) && TypeContainsDroppableContent(localDecl.Type))
                    {
                        _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                        _output.AppendLine($"    __novus_memset(&{initValue}, 0, sizeof({cType}));");
                    }
                }
            }
        }
        else
        {
            // First declaration - emit with type
            // Special handling for array types with array literal initialization
            if (localDecl.Type is IrArrayType arrayType && localDecl.InitialValue is IrArrayLiteral arrayLiteral)
            {
                var elementType = GetCType(arrayType.ElementType);
                var size = arrayType.Length;

                // LIFETIME FIX: Arrays initialized with compile-time constants should be 'static'
                // to ensure they live for the entire program duration. This is critical when
                // the array is passed to external functions (like MUI's make_radio) that
                // may store pointers to the array. Without 'static', the array would be
                // stack-allocated and become invalid when the function returns.
                var isConstantArray = IsCompileTimeConstantArray(arrayLiteral);
                var staticPrefix = isConstantArray ? "static " : "";

                // Use standard C array initializer syntax: [static] Type name[size] = { elem1, elem2, ... };
                // This works correctly with VBCC for all types including structs
                _output.AppendLine($"    {staticPrefix}{elementType} {varName}[{size}] = {initValue};");
            }
            // VBCC FIX: For struct literals, declare variable then assign fields individually
            // This avoids VBCC C99 compound literal incompatibility
            else if (isStructLiteral && localDecl.InitialValue is IrStructLiteral structLitDecl)
            {
                var decl = GetCVariableDeclaration(localDecl.Type, varName);
                _output.AppendLine($"    {decl};");
                var structTypeDeclForFields = structLitDecl.Type as IrStructType;
                foreach (var kvp in structLitDecl.FieldValues)
                {
                    // VBCC FIX: Array fields cannot be assigned with brace initializers
                    // STRICT ALIASING FIX: Use static const temporary instead of compound literal
                    if (kvp.Value is IrArrayLiteral arrayFieldLitDecl)
                    {
                        // Get the ACTUAL field type from the struct definition, not the literal's type
                        // This handles cases like [0; 32] where literal has [i32;32] but field is [u8;32]
                        var actualFieldTypeDecl = structTypeDeclForFields?.GetField(kvp.Key)?.Type as IrArrayType;
                        var arrayFieldTypeDecl = actualFieldTypeDecl ?? (arrayFieldLitDecl.Type as IrArrayType);

                        // VBCC 68K FIX: Use safe array copy to avoid compound literal alignment issues
                        EmitSafeArrayCopy($"{varName}.{kvp.Key}", arrayFieldLitDecl, arrayFieldTypeDecl!);
                    }
                    else
                    {
                        var fieldValue = EmitValue(kvp.Value);
                        _output.AppendLine($"    {varName}.{kvp.Key} = {fieldValue};");

                        // CRITICAL FIX: Move semantics for struct fields containing droppable content
                        var fieldSourceIsLocalVar = kvp.Value is IrVariable;
                        var fieldSourceIsSlot = fieldValue != null && fieldValue.StartsWith("_slot_");
                        if ((fieldSourceIsLocalVar || fieldSourceIsSlot) && TypeContainsDroppableContent(kvp.Value.Type))
                        {
                            var fieldCType = GetCType(kvp.Value.Type);
                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                            _output.AppendLine($"    __novus_memset(&{fieldValue}, 0, sizeof({fieldCType}));");
                        }
                    }
                }
            }
            // VBCC FIX: For enum literals with associated data, declare variable then assign tag and data
            else if (isEnumLiteral && localDecl.InitialValue is IrEnumValue enumValueDecl)
            {
                var enumType = enumValueDecl.Type as IrEnumType;
                var enumName = MangleName(enumType!);
                bool hasAnyData = enumType!.Variants.Any(v => v.HasAssociatedData);

                var decl = GetCVariableDeclaration(localDecl.Type, varName);
                _output.AppendLine($"    {decl};");

                if (hasAnyData)
                {
                    // Emit tag assignment
                    _output.AppendLine($"    {varName}.tag = {enumName}_{enumValueDecl.VariantName};");

                    // Emit associated data assignments if present
                    if (enumValueDecl.AssociatedValues.Count > 0)
                    {
                        // Check if this is a unit type
                        bool isUnitType = enumValueDecl.AssociatedValues.Count == 1 &&
                                         enumValueDecl.AssociatedValues[0].Type is IrTupleType unitTupleType &&
                                         unitTupleType.ElementTypes.Count == 0;

                        if (!isUnitType)
                        {
                            for (int i = 0; i < enumValueDecl.AssociatedValues.Count; i++)
                            {
                                // VBCC_004: Compound literals cause misalignment on 68k - emit field-by-field for nested struct literals
                                if (enumValueDecl.AssociatedValues[i] is IrStructLiteral nestedStructLit)
                                {
                                    foreach (var kvp in nestedStructLit.FieldValues)
                                    {
                                        var fieldValue = EmitValue(kvp.Value);
                                        _output.AppendLine($"    {varName}.data.{enumValueDecl.VariantName}._{i}.{kvp.Key} = {fieldValue};");
                                    }
                                }
                                // VBCC_004: Compound literals cause misalignment on 68k - emit field-by-field for nested enum literals
                                else if (enumValueDecl.AssociatedValues[i] is IrEnumValue nestedEnumValueDecl)
                                {
                                    EmitNestedEnumFieldByField($"{varName}.data.{enumValueDecl.VariantName}._{i}", nestedEnumValueDecl);
                                }
                                else
                                {
                                    var assocValue = EmitValue(enumValueDecl.AssociatedValues[i]);
                                    var assocValueType = enumValueDecl.AssociatedValues[i].Type;
                                    // VBCC 68K FIX: Use memcpy for struct types to avoid illegal instruction on 68040
                                    if (TypeRequiresMemcpy(assocValueType))
                                    {
                                        var cType = GetCType(assocValueType);
                                        _output.AppendLine($"    __novus_memcpy((uint8_t*)&{varName}.data.{enumValueDecl.VariantName}._{i}, (uint8_t*)&{assocValue}, sizeof({cType}));");

                                        // CRITICAL FIX: Move semantics when copying local variable into enum variant data.
                                        var sourceIsLocalVar = enumValueDecl.AssociatedValues[i] is IrVariable;
                                        if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                        {
                                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                                            _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                        }
                                    }
                                    else
                                    {
                                        _output.AppendLine($"    {varName}.data.{enumValueDecl.VariantName}._{i} = {assocValue};");

                                        // CRITICAL FIX: Move semantics for direct assignment too
                                        var sourceIsLocalVar = enumValueDecl.AssociatedValues[i] is IrVariable;
                                        if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                        {
                                            var cType = GetCType(assocValueType);
                                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                                            _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Simple enum without data - just emit as cast
                    _output.AppendLine($"    {varName} = {initValue};");
                }
            }
            // VBCC FIX: For complex types (nested structs OR enums with unions), declare then use memcpy for initialization.
            // VBCC generates illegal instructions on 68040 for struct-by-value copies of these types.
            else if (localDecl.InitialValue != null && TypeRequiresMemcpy(localDecl.InitialValue.Type))
            {
                var decl = GetCVariableDeclaration(localDecl.Type, varName);
                var cType = GetCType(localDecl.Type);
                _output.AppendLine($"    {decl};");
                _output.AppendLine($"    __novus_memcpy((uint8_t*)&{varName}, (uint8_t*)&{initValue}, sizeof({cType}));");

                // CRITICAL FIX: Move semantics when copying from local variable
                var sourceIsLocalVar = localDecl.InitialValue is IrVariable;
                var sourceIsSlot = initValue != null && initValue.StartsWith("_slot_");
                if ((sourceIsLocalVar || sourceIsSlot) && TypeContainsDroppableContent(localDecl.Type))
                {
                    _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                    _output.AppendLine($"    __novus_memset(&{initValue}, 0, sizeof({cType}));");
                }
            }
            else
            {
                var decl = GetCVariableDeclaration(localDecl.Type, varName);
                var cType = GetCType(localDecl.Type);
                var initType = GetCType(localDecl.InitialValue!.Type);

                // SAFETY: Always add explicit cast when assigning integer constants to pointer types
                // This handles edge cases where the IR has coerced the type but the emitted value
                // is still just an integer literal without a cast
                var needsCast = initType != cType;
                if (!needsCast && localDecl.Type is IrPointerType && localDecl.InitialValue is IrConstant constVal)
                {
                    // Integer constant assigned to pointer - ensure cast is present
                    // (EmitIntegerConstant should handle this, but be defensive)
                    needsCast = true;
                }

                if (needsCast)
                {
                    _output.AppendLine($"    {decl} = ({cType}){initValue};");
                }
                else
                {
                    _output.AppendLine($"    {decl} = {initValue};");
                }

                // CRITICAL FIX: Move semantics when copying from local variable (direct assignment)
                var sourceIsLocalVar = localDecl.InitialValue is IrVariable;
                var sourceIsSlot = initValue != null && initValue.StartsWith("_slot_");
                if ((sourceIsLocalVar || sourceIsSlot) && TypeContainsDroppableContent(localDecl.Type))
                {
                    _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                    _output.AppendLine($"    __novus_memset(&{initValue}, 0, sizeof({cType}));");
                }
            }
            _declaredVariables.Add(varName);
        }
    }

    private void EmitStore(IrStore store)
    {
        // Skip void/unit type stores - they have no runtime representation
        if (store.Value.Type is IrVoidType)
            return;
        if (store.Value.Type is IrTupleType tupleType && tupleType.ElementTypes.Count == 0)
            return;

        var varName = SanitizeVariableName(store.VariableName);

        // VBCC FIX: VBCC doesn't support C99 compound literals in assignments.
        // For struct literals, emit field-by-field assignment instead of compound literal.
        if (store.Value is IrStructLiteral structLit)
        {
            foreach (var kvp in structLit.FieldValues)
            {
                var fieldValue = EmitValue(kvp.Value);
                _output.AppendLine($"    {varName}.{kvp.Key} = {fieldValue};");

                // CRITICAL FIX: Move semantics for struct fields containing droppable content
                var fieldSourceIsLocalVar = kvp.Value is IrVariable;
                var fieldSourceIsSlot = fieldValue != null && fieldValue.StartsWith("_slot_");
                if ((fieldSourceIsLocalVar || fieldSourceIsSlot) && TypeContainsDroppableContent(kvp.Value.Type))
                {
                    var fieldCType = GetCType(kvp.Value.Type);
                    _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                    _output.AppendLine($"    __novus_memset(&{fieldValue}, 0, sizeof({fieldCType}));");
                }
            }
            return;
        }

        // VBCC FIX: For enum literals with associated data, emit field-by-field assignment
        if (store.Value is IrEnumValue enumValue)
        {
            var enumType = enumValue.Type as IrEnumType;
            var enumName = MangleName(enumType!);
            bool hasAnyData = enumType!.Variants.Any(v => v.HasAssociatedData);

            if (hasAnyData)
            {
                // Emit tag assignment
                _output.AppendLine($"    {varName}.tag = {enumName}_{enumValue.VariantName};");

                // Emit associated data assignments if present
                if (enumValue.AssociatedValues.Count > 0)
                {
                    // Check if this is a unit type
                    bool isUnitType = enumValue.AssociatedValues.Count == 1 &&
                                     enumValue.AssociatedValues[0].Type is IrTupleType unitTupleType &&
                                     unitTupleType.ElementTypes.Count == 0;

                    if (!isUnitType)
                    {
                        for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
                        {
                            // VBCC_004: Compound literals cause misalignment on 68k - emit field-by-field for nested struct literals
                            if (enumValue.AssociatedValues[i] is IrStructLiteral nestedStructLit)
                            {
                                foreach (var kvp in nestedStructLit.FieldValues)
                                {
                                    var fieldValue = EmitValue(kvp.Value);
                                    _output.AppendLine($"    {varName}.data.{enumValue.VariantName}._{i}.{kvp.Key} = {fieldValue};");
                                }
                            }
                            // VBCC_004: Compound literals cause misalignment on 68k - emit field-by-field for nested enum literals
                            else if (enumValue.AssociatedValues[i] is IrEnumValue nestedEnumValueStore)
                            {
                                EmitNestedEnumFieldByField($"{varName}.data.{enumValue.VariantName}._{i}", nestedEnumValueStore);
                            }
                            else
                            {
                                var assocValue = EmitValue(enumValue.AssociatedValues[i]);
                                var assocValueType = enumValue.AssociatedValues[i].Type;
                                // VBCC 68K FIX: Use memcpy for struct types to avoid illegal instruction on 68040
                                if (TypeRequiresMemcpy(assocValueType))
                                {
                                    var cType = GetCType(assocValueType);
                                    _output.AppendLine($"    __novus_memcpy((uint8_t*)&{varName}.data.{enumValue.VariantName}._{i}, (uint8_t*)&{assocValue}, sizeof({cType}));");

                                    // CRITICAL FIX: Move semantics when copying local variable into enum variant data.
                                    var sourceIsLocalVar = enumValue.AssociatedValues[i] is IrVariable;
                                    if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                    {
                                        _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                                        _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                    }
                                }
                                else
                                {
                                    _output.AppendLine($"    {varName}.data.{enumValue.VariantName}._{i} = {assocValue};");

                                    // CRITICAL FIX: Move semantics for direct assignment too
                                    var sourceIsLocalVar = enumValue.AssociatedValues[i] is IrVariable;
                                    if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                    {
                                        var cType = GetCType(assocValueType);
                                        _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                                        _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Unit type - set dummy field
                        _output.AppendLine($"    {varName}.data.{enumValue.VariantName}._dummy = 0;");
                    }
                }
                return;
            }
            // For enums without associated data, fall through to simple assignment
        }

        var value = EmitValue(store.Value);

        // VBCC FIX: VBCC cannot compile struct-by-value assignment for complex types:
        // - Structs containing nested structs (e.g., SpriteData with ChipMemHandle)
        // - Enums with associated data (e.g., Result<T,E>, Option<T> - these become tagged unions)
        // VBCC generates illegal instructions on 68040 for these types.
        // Solution: Use __novus_memcpy() instead of direct assignment.
        if (TypeRequiresMemcpy(store.Value.Type))
        {
            var cType = GetCType(store.Value.Type);
            _output.AppendLine($"    __novus_memcpy((uint8_t*)&{varName}, (uint8_t*)&{value}, sizeof({cType}));");

            // CRITICAL FIX: Move semantics when copying from local variable
            var sourceIsLocalVar = store.Value is IrVariable;
            var sourceIsSlot = value != null && value.StartsWith("_slot_");
            if ((sourceIsLocalVar || sourceIsSlot) && TypeContainsDroppableContent(store.Value.Type))
            {
                _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                _output.AppendLine($"    __novus_memset(&{value}, 0, sizeof({cType}));");
            }
        }
        else
        {
            // For primitives, pointers, and simple structs/enums, direct assignment works fine
            _output.AppendLine($"    {varName} = {value};");

            // CRITICAL FIX: Move semantics when copying from local variable (direct assignment)
            var sourceIsLocalVar = store.Value is IrVariable;
            var sourceIsSlot = value != null && value.StartsWith("_slot_");
            if ((sourceIsLocalVar || sourceIsSlot) && TypeContainsDroppableContent(store.Value.Type))
            {
                var cType = GetCType(store.Value.Type);
                _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
                _output.AppendLine($"    __novus_memset(&{value}, 0, sizeof({cType}));");
            }
        }
    }

    private void EmitBinaryOp(IrBinaryOp binaryOp)
    {
        var cType = GetCType(binaryOp.Type);
        var resultName = SanitizeVariableName(binaryOp.ResultName);
        var left = EmitValue(binaryOp.Left);
        var right = EmitValue(binaryOp.Right);
        var alreadyDeclared = _declaredVariables.Contains(resultName);

        // Special handling for division/modulo with safety checks
        if (_safetyLevel.EnableDivisionByZeroChecks() &&
            (binaryOp.Operation == IrBinaryOp.OpKind.Div || binaryOp.Operation == IrBinaryOp.OpKind.Mod))
        {
            // Emit conditional: if divisor is zero, show error and return safe value
            // Otherwise perform the actual division
            if (!alreadyDeclared)
            {
                _output.AppendLine($"    {cType} {resultName};");
            }
            _output.AppendLine($"    if ({right} == 0) {{");
            _output.AppendLine($"        __novus_div_check({right}, \"<compiler-generated>\", 0);");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            // Return from function after error (emits proper return type)
            EmitErrorPathReturn(2);  // indent level 2
            _output.AppendLine($"    }} else {{");

            // Handle fixed-point division specially
            if (binaryOp.Type is IrFixedType fixedTypeDiv && binaryOp.Operation == IrBinaryOp.OpKind.Div)
            {
                int fracBits = fixedTypeDiv.BitWidth / 2;
                string wideType = fixedTypeDiv.BitWidth == 16 ? "int32_t" : "int64_t";
                _output.AppendLine($"        {resultName} = ({cType})((({wideType}){left} << {fracBits}) / {right});");
            }
            else
            {
                var op = GetBinaryOperator(binaryOp.Operation);
                _output.AppendLine($"        {resultName} = {left} {op} {right};");
            }
            _output.AppendLine($"    }}");
        }
        // Special handling for shift operations: mask shift amount to bit width
        else if (binaryOp.Operation == IrBinaryOp.OpKind.Shl || binaryOp.Operation == IrBinaryOp.OpKind.Shr)
        {
            // Determine bit width of the type being shifted
            int bitWidth = binaryOp.Type switch
            {
                IrIntType intType => intType.BitWidth,
                _ => 32  // Default to 32-bit for other types
            };

            var mask = bitWidth - 1;  // 31 for 32-bit, 15 for 16-bit, 7 for 8-bit
            var op = binaryOp.Operation == IrBinaryOp.OpKind.Shl ? "<<" : ">>";

            // Mask the shift amount to prevent undefined behavior
            if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {left} {op} (({right}) & {mask});");
            }
            else
            {
                _output.AppendLine($"    {cType} {resultName} = {left} {op} (({right}) & {mask});");
            }
        }
        // Special handling for fixed-point multiplication and division
        // fixed16 = 8.8 format (8 fractional bits), fixed32 = 16.16 format (16 fractional bits)
        else if (binaryOp.Type is IrFixedType fixedType &&
                 (binaryOp.Operation == IrBinaryOp.OpKind.Mul || binaryOp.Operation == IrBinaryOp.OpKind.Div))
        {
            // Fractional bits = half the total bit width
            int fracBits = fixedType.BitWidth / 2;
            // Use wider type for intermediate calculations to avoid overflow
            string wideType = fixedType.BitWidth == 16 ? "int32_t" : "int64_t";

            string expr;
            if (binaryOp.Operation == IrBinaryOp.OpKind.Mul)
            {
                // Fixed-point multiplication: (a * b) >> fracBits
                // Cast to wider type first to prevent overflow during multiply
                expr = $"({cType})((({wideType}){left} * ({wideType}){right}) >> {fracBits})";
            }
            else // Div
            {
                // Fixed-point division: (a << fracBits) / b
                // Cast to wider type first to allow the left shift without overflow
                expr = $"({cType})((({wideType}){left} << {fracBits}) / {right})";
            }

            if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {expr};");
            }
            else
            {
                _output.AppendLine($"    {cType} {resultName} = {expr};");
            }
        }
        // Handle signed integer arithmetic with wrapping semantics to avoid undefined behavior.
        // C99 signed integer overflow is UB, but Novus defines wrapping semantics.
        // We cast to unsigned, perform the operation, then cast back to signed.
        else if ((binaryOp.Operation == IrBinaryOp.OpKind.Add ||
                  binaryOp.Operation == IrBinaryOp.OpKind.Sub ||
                  binaryOp.Operation == IrBinaryOp.OpKind.Mul) &&
                 binaryOp.Type is IrIntType intType && intType.IsSigned)
        {
            var op = GetBinaryOperator(binaryOp.Operation);
            var unsignedType = intType.BitWidth switch
            {
                8 => "uint8_t",
                16 => "uint16_t",
                32 => "uint32_t",
                64 => "uint64_t",
                _ => cType // Fallback to signed if unexpected width
            };

            // Cast both operands to unsigned, perform operation, cast result back to signed
            // This ensures defined wrapping behavior on overflow
            var expr = $"({cType})(({unsignedType}){left} {op} ({unsignedType}){right})";

            if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {expr};");
            }
            else
            {
                _output.AppendLine($"    {cType} {resultName} = {expr};");
            }
        }
        else
        {
            var op = GetBinaryOperator(binaryOp.Operation);

            // VBCC WORKAROUND: Track comparison operations so they can be inlined into
            // conditional branches. This prevents VBCC from inserting stack cleanup
            // instructions between the comparison and the branch, which clobbers flags.
            bool isComparisonOp = binaryOp.Operation is IrBinaryOp.OpKind.Eq or IrBinaryOp.OpKind.Ne
                or IrBinaryOp.OpKind.Lt or IrBinaryOp.OpKind.Le
                or IrBinaryOp.OpKind.Gt or IrBinaryOp.OpKind.Ge;

            if (isComparisonOp)
            {
                // VBCC WORKAROUND (VBCC_001): Wrap ALL comparison operations in helper functions
                // VBCC's optimizer can move stack cleanup instructions between a comparison
                // and subsequent code that tests the result, clobbering CPU flags.
                // Using a function call creates a sequence point that prevents reordering.
                //
                // For pointer operands, cast to int32_t since the wrapper expects int32_t.
                var leftType = binaryOp.Left.Type;
                var rightType = binaryOp.Right.Type;
                bool leftIsPointer = leftType is IrPointerType;
                bool rightIsPointer = rightType is IrPointerType;
                var leftCast = leftIsPointer ? $"(int32_t){left}" : left;
                var rightCast = rightIsPointer ? $"(int32_t){right}" : right;

                // Store the comparison expression with casts for potential inlining
                _inlineableComparisons[resultName] = $"{leftCast} {op} {rightCast}";

                string wrapperCall;
                if (op == "==")
                    wrapperCall = $"__novus_cmp_eq_i32({leftCast}, {rightCast})";
                else if (op == "!=")
                    wrapperCall = $"__novus_cmp_ne_i32({leftCast}, {rightCast})";
                else
                    wrapperCall = $"({left} {op} {right})";  // < <= > >= don't have wrappers yet

                if (alreadyDeclared)
                {
                    _output.AppendLine($"    {resultName} = {wrapperCall};");
                }
                else
                {
                    _output.AppendLine($"    {cType} {resultName} = {wrapperCall};");
                }
            }
            else if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {left} {op} {right};");
            }
            else
            {
                _output.AppendLine($"    {cType} {resultName} = {left} {op} {right};");
            }
        }
    }

    private void EmitCall(IrCall call)
    {
        // Memory tracking: Intercept AllocMem/FreeMem calls when tracking is enabled
        if (_safetyLevel.EnableMemoryTracking())
        {
            // AllocMem(size, flags) -> __novus_tracked_alloc(size, flags, file, line)
            if (call.FunctionName == "AllocMem" && call.Arguments.Count == 2)
            {
                var sizeArg = EmitValue(call.Arguments[0]);
                var flagsArg = EmitValue(call.Arguments[1]);
                var fileName = call.Location?.FilePath ?? "unknown";
                var lineNumber = call.Location?.Line ?? 0;

                var callExpr = $"__novus_tracked_alloc({sizeArg}, {flagsArg}, \"{fileName}\", {lineNumber})";

                if (call.ResultName != null)
                {
                    var resultName = SanitizeVariableName(call.ResultName);
                    if (_declaredVariables.Contains(resultName))
                    {
                        _output.AppendLine($"    {resultName} = ({GetCType(call.ReturnType)}){callExpr};");
                    }
                    else
                    {
                        var decl = GetCVariableDeclaration(call.ReturnType, resultName);
                        _output.AppendLine($"    {decl} = ({GetCType(call.ReturnType)}){callExpr};");
                    }
                }
                else
                {
                    _output.AppendLine($"    {callExpr};");
                }
                return;
            }

            // FreeMem(ptr, size) -> __novus_tracked_free(ptr, size, file, line)
            if (call.FunctionName == "FreeMem" && call.Arguments.Count == 2)
            {
                var ptrArg = EmitValue(call.Arguments[0]);
                var sizeArg = EmitValue(call.Arguments[1]);
                var fileName = call.Location?.FilePath ?? "unknown";
                var lineNumber = call.Location?.Line ?? 0;

                _output.AppendLine($"    __novus_tracked_free({ptrArg}, {sizeArg}, \"{fileName}\", {lineNumber});");
                return;
            }
        }

        // For FFI calls, we need to cast pointer arguments to int32_t
        var function = _module.Functions.FirstOrDefault(f => f.Name == call.FunctionName);
        var args = new List<string>();

        for (int i = 0; i < call.Arguments.Count; i++)
        {
            var arg = call.Arguments[i];
            var argValue = EmitValue(arg);

            // BUG FIX: If the parameter type is a struct containing heap data,
            // the function signature expects a pointer, so pass by address
            if (function != null && i < function.Parameters.Count)
            {
                var paramType = function.Parameters[i].Type;

                // BUG FIX: If argument is a pointer to a primitive type, but parameter expects a value, dereference
                // This happens when calling helper functions from Display::fmt methods where self is int8_t*
                // NOTE: IrReferenceType and IrMutReferenceType are also "pointer-like" in C, so don't dereference for those either
                if (arg.Type is IrPointerType ptrType &&
                    (ptrType.PointeeType is IrIntType or IrBoolType or IrFloatType) &&
                    !(paramType is IrPointerType or IrReferenceType or IrMutReferenceType))
                {
                    argValue = $"*{argValue}";
                }
                // If parameter is a struct with heap data (passed by pointer in signature),
                // but the argument is not already a pointer, pass by address
                else if (paramType is IrStructType structType &&
                    TypeContainsHeapData(structType) &&
                    arg.Type is IrStructType)  // arg is struct value, not pointer
                {
                    // Check if the argument is a variable that was itself a pointer-converted parameter
                    // If so, it's already a pointer in C and doesn't need &
                    bool isPointerConvertedParam = arg is IrVariable variable &&
                                                   _pointerConvertedParameters.Contains(variable.Name);

                    if (!isPointerConvertedParam)
                    {
                        // VBCC 68K FIX: If the argument is a struct literal, emit a temporary variable
                        // with field-by-field assignment instead of using compound literal &(Struct){ ... }
                        // which causes misalignment on 68040
                        if (arg is IrStructLiteral structLitArg)
                        {
                            var tempVarName = $"_arg_tmp_{_tempCounter++}";
                            var cStructType = GetCType(structType);

                            // Declare the temporary variable
                            _output.AppendLine($"    {cStructType} {tempVarName};");

                            // Assign fields individually to avoid compound literal
                            foreach (var kvp in structLitArg.FieldValues)
                            {
                                var fValue = EmitValue(kvp.Value);
                                _output.AppendLine($"    {tempVarName}.{kvp.Key} = {fValue};");
                            }

                            // Use address of the temp variable
                            argValue = $"&{tempVarName}";
                        }
                        else
                        {
                            argValue = $"&{argValue}";
                        }
                    }
                }
                // If this is an extern FFI function and the parameter expects i32 but we have a pointer, cast it
                else if (function.IsExtern && paramType is IrIntType intType && intType.BitWidth == 32 && arg is IrVariable variable)
                {
                    // Check if the variable is a pointer type
                    // For now, we'll add the cast unconditionally for pointers
                    argValue = $"(int32_t){argValue}";
                }
                // BUG FIX: Array-to-slice conversion
                // If parameter is a pointer to a slice type (*Slice) but argument is a reference to an array (&[T; N]),
                // convert it to a temporary slice struct
                else if (paramType is IrReferenceType paramRefType &&
                         paramRefType.PointeeType is IrStructType sliceStructType &&
                         sliceStructType.StructName == "Slice" &&
                         arg.Type is IrReferenceType argRefType &&
                         argRefType.PointeeType is IrArrayType arrayType)
                {
                    Console.WriteLine($"[ARRAY-TO-SLICE] Triggered for call to function");
                    Console.WriteLine($"  Parameter type: {paramType}");
                    Console.WriteLine($"  Argument type: {arg.Type}");
                    Console.WriteLine($"  Array element type: {arrayType.ElementType}");
                    Console.WriteLine($"  Array length: {arrayType.Length}");

                    // VBCC 68K FIX: Create a temporary slice struct with field-by-field assignment
                    // instead of compound literal &((Slice_T){ ... }) which causes misalignment on 68040
                    var elementTypeName = GetCType(arrayType.ElementType);
                    var sliceTypeName = $"Slice_{SanitizeVariableName(elementTypeName)}";
                    var tempSliceName = $"_slice_tmp_{_tempCounter++}";

                    // Emit the temporary variable and field assignments
                    _output.AppendLine($"    {sliceTypeName} {tempSliceName};");
                    _output.AppendLine($"    {tempSliceName}.ptr = {argValue};");
                    _output.AppendLine($"    {tempSliceName}.len = {arrayType.Length};");

                    // Use address of the temp variable
                    argValue = $"&{tempSliceName}";

                    Console.WriteLine($"  Generated slice type: {sliceTypeName}");
                    Console.WriteLine($"  Final argValue: {argValue}");
                }
            }

            args.Add(argValue);
        }

        // VBCC FIX: For struct/enum returns on 68k, use output parameter pattern to avoid vbcc bugs
        // BUT: Extern functions use their actual C signatures and return values directly
        var isStructOrEnumReturn = call.ReturnType is IrStructType or IrEnumType or IrTupleType;
        var shouldUseOutParam = isStructOrEnumReturn && (function == null || !function.IsExtern);

        // WORKAROUND: Strip type suffixes from cross-module function calls BEFORE mangling
        // The IR builder sometimes adds type suffixes to public cross-module calls
        // For example, "make_tags_TagItem" should become "make_tags"
        var cleanedCallName = call.FunctionName;
        if (call.FunctionName.Contains("make_tags"))
        {
            CompilerLogger.Debug(LogCategory.CodeGen, "make_tags call: function={0}, FunctionName={1}, ReturnType={2}",
                function?.Name ?? "null", call.FunctionName, call.ReturnType);
            if (call.ReturnType is IrStructType st)
            {
                CompilerLogger.Debug(LogCategory.CodeGen, "  StructType: Name={0}, StructName={1}, CacheKey={2}",
                    st.Name, st.StructName, st.CacheKey ?? "null");
            }
        }
        if (function == null && call.ReturnType is IrStructType retStructTypeCheck)
        {
            // Try to extract type args from CacheKey
            List<string> typeArgs = new List<string>();
            if (!string.IsNullOrEmpty(retStructTypeCheck.CacheKey))
            {
                typeArgs = ExtractTypeArguments(retStructTypeCheck.CacheKey);
            }

            if (typeArgs.Count > 0)
            {
                var suffix = "_" + string.Join("_", typeArgs);
                if (cleanedCallName.EndsWith(suffix))
                {
                    cleanedCallName = cleanedCallName.Substring(0, cleanedCallName.Length - suffix.Length);
                }
            }
            else
            {
                // Fallback heuristic: strip "_PascalCase" suffix
                var lastUnderscore = cleanedCallName.LastIndexOf('_');
                if (lastUnderscore > 0 && lastUnderscore < cleanedCallName.Length - 1)
                {
                    var potentialTypeSuffix = cleanedCallName.Substring(lastUnderscore + 1);
                    if (char.IsUpper(potentialTypeSuffix[0]))
                    {
                        cleanedCallName = cleanedCallName.Substring(0, lastUnderscore);
                    }
                }
            }
        }

        // Don't mangle exported function names in calls
        // If we found the function, use its mangled name; otherwise fall back to string mangling
        var rawCallName = (function != null && function.IsExported) ? function.Name :
                          (function != null ? MangleName(function) : MangleName(cleanedCallName));

        var callFuncName = rawCallName;

        if (shouldUseOutParam && call.ResultName != null)
        {
            // Use output parameter pattern: void func(Result* out, args...)
            var resultName = SanitizeVariableName(call.ResultName);

            // Only declare if not already declared (e.g., as a slot for variable reuse)
            if (!_declaredVariables.Contains(resultName))
            {
                var decl = GetCVariableDeclaration(call.ReturnType, resultName);
                _output.AppendLine($"    {decl};");
            }

            // Call function with output parameter
            var allArgs = new List<string> { $"&{resultName}" };
            allArgs.AddRange(args);
            var callExpr = $"{callFuncName}({string.Join(", ", allArgs)})";
            _output.AppendLine($"    {callExpr};");
        }
        else
        {
            // Normal return value
            var callExpr = $"{callFuncName}({string.Join(", ", args)})";

            // Skip result storage for void/unit types - they have no runtime representation
            bool isVoidOrUnit = call.ReturnType is IrVoidType ||
                               (call.ReturnType is IrTupleType tt && tt.ElementTypes.Count == 0);

            if (call.ResultName != null && !isVoidOrUnit)
            {
                var resultName = SanitizeVariableName(call.ResultName);

                // Only emit declaration if not already declared (e.g., as a slot for variable reuse)
                if (_declaredVariables.Contains(resultName))
                {
                    _output.AppendLine($"    {resultName} = {callExpr};");
                }
                else
                {
                    var decl = GetCVariableDeclaration(call.ReturnType, resultName);
                    _output.AppendLine($"    {decl} = {callExpr};");
                }
            }
            else
            {
                _output.AppendLine($"    {callExpr};");
            }
        }
    }

    private void EmitIndirectCall(IrIndirectCall call)
    {
        // Emit an indirect call through a function pointer
        var funcPtr = EmitValue(call.FunctionPointer);
        var args = call.Arguments.Select(EmitValue).ToList();

        var callExpr = $"({funcPtr})({string.Join(", ", args)})";

        if (call.ResultName != null && call.ReturnType is not IrVoidType)
        {
            var resultName = SanitizeVariableName(call.ResultName);

            // Only emit declaration if not already declared (e.g., as a slot for variable reuse)
            if (_declaredVariables.Contains(resultName))
            {
                _output.AppendLine($"    {resultName} = {callExpr};");
            }
            else
            {
                var decl = GetCVariableDeclaration(call.ReturnType, resultName);
                _output.AppendLine($"    {decl} = {callExpr};");
            }
        }
        else
        {
            _output.AppendLine($"    {callExpr};");
        }
    }

    private void EmitLabel(IrLabel label)
    {
        // Dead label elimination: skip labels that aren't targeted by any branch
        // This prevents VBCC warning 148 (unused label) for unreachable labels
        // like match_end_N when all match arms return/branch elsewhere
        if (!_reachableLabels.Contains(label.Name))
        {
            return;
        }

        // Skip if this label was already emitted (prevents duplicate from EmitBasicBlock)
        if (_emittedLabels.Contains(label.Name))
        {
            return;
        }

        // Append suffix to label name to make it unique across defer block emissions
        var labelName = label.Name + _labelSuffix;

        // DISABLED: Match arm scopes cause variable scoping issues with nested matches
        // Since we're using gotos, we don't need explicit scopes - the labels provide structure
        // Close previous match arm scope if needed
        //if (_inMatchArmScope && (label.Name.StartsWith("match_arm_") || label.Name.StartsWith("match_end_") || label.Name.StartsWith("match_check_")))
        //{
        //    _output.AppendLine("    }");
        //    _inMatchArmScope = false;
        //}

        // Emit label (unindented for C style)
        // Add empty statement to ensure valid C (labels must be followed by a statement)
        _output.AppendLine($"{labelName}:;");
        _emittedLabels.Add(label.Name);

        // DISABLED: Match arm scopes cause variable scoping issues with nested matches
        // Open new scope for match arms
        //if (label.Name.StartsWith("match_arm_"))
        //{
        //    _output.AppendLine("    {");
        //    _inMatchArmScope = true;
        //}
    }

    private void EmitBranch(IrBranch branch)
    {
        // Append suffix to target label to match defer block emission
        var targetLabel = branch.Target + _labelSuffix;
        _output.AppendLine($"    goto {targetLabel};");
    }

    private void EmitConditionalBranch(IrConditionalBranch condBranch)
    {
        var conditionVar = EmitValue(condBranch.Condition);

        // VBCC WORKAROUND (VBCC_001): VBCC's optimizer can move stack cleanup instructions
        // between a comparison and its conditional branch, clobbering the CPU condition flags.
        // This causes Guru 80000004 (illegal instruction) on 68040.
        //
        // Example of buggy code generated by VBCC:
        //   jsr _memcpy           ; Call function
        //   move.l result,var     ; Store comparison result
        //   addq.w #12,a7         ; Stack cleanup (CLOBBERS Z flag!)
        //   beq label             ; Uses Z flag from addq, not from comparison!
        //
        // SOLUTION: Use a wrapper function __novus_cmp_ne_i32() that forces a sequence point.
        // The function call completes all pending stack cleanup before returning, ensuring
        // the comparison result is valid when we test it.
        //
        // This adds ~10 cycles overhead per comparison, but eliminates the bug completely.

        string condition;
        if (_inlineableComparisons.TryGetValue(conditionVar, out var inlinedExpr))
        {
            // Even with inlined comparisons, we need to go through the wrapper to avoid
            // VBCC reordering issues. Parse the comparison and use the appropriate wrapper.
            if (TryParseComparison(inlinedExpr, out var left, out var op, out var right))
            {
                if (op == "==")
                    condition = $"__novus_cmp_eq_i32({left}, {right})";
                else if (op == "!=")
                    condition = $"__novus_cmp_ne_i32({left}, {right})";
                else
                    // For other ops (<, >, etc), fall back to casting - these are less common
                    condition = $"(int32_t)({inlinedExpr})";
            }
            else
            {
                condition = $"__novus_cmp_ne_i32((int32_t)({inlinedExpr}), 0)";
            }
            _inlineableComparisons.Remove(conditionVar);
        }
        else
        {
            // Use wrapper function for the boolean test
            condition = $"__novus_cmp_ne_i32((int32_t){conditionVar}, 0)";
        }

        // Append suffix to target labels to match defer block emission
        var trueTarget = condBranch.TrueTarget + _labelSuffix;
        _output.AppendLine($"    if ({condition}) goto {trueTarget};");
        if (!string.IsNullOrEmpty(condBranch.FalseTarget))
        {
            var falseTarget = condBranch.FalseTarget + _labelSuffix;
            _output.AppendLine($"    goto {falseTarget};");
        }
    }

    /// <summary>
    /// Try to parse a simple comparison expression like "a == b" or "a != b"
    /// </summary>
    private bool TryParseComparison(string expr, out string left, out string op, out string right)
    {
        left = op = right = "";

        // Look for == or != operators
        var eqIdx = expr.IndexOf(" == ");
        if (eqIdx > 0)
        {
            left = expr.Substring(0, eqIdx).Trim();
            op = "==";
            right = expr.Substring(eqIdx + 4).Trim();
            return true;
        }

        var neIdx = expr.IndexOf(" != ");
        if (neIdx > 0)
        {
            left = expr.Substring(0, neIdx).Trim();
            op = "!=";
            right = expr.Substring(neIdx + 4).Trim();
            return true;
        }

        return false;
    }

    private void EmitReturn(IrReturn returnInst)
    {
        // CRITICAL FIX: Handle IrNever (unreachable!() or panic!()) specially.
        // The panic has already been emitted, and the function will never return.
        // We still need to emit the return statement to satisfy C semantics,
        // but we don't emit any cleanup since panic handles that.
        if (returnInst.Value is IrNever)
        {
            // Just emit a comment - the panic call already handles everything
            _output.AppendLine("    /* unreachable - panic already called */");
            return;
        }

        // CRITICAL FIX: The order of operations for return is:
        // 1. Deactivate defers for moved variables
        // 2. For struct/enum returns: COPY RETURN VALUE TO __out FIRST
        // 3. THEN emit deferred cleanup (which may include Drop calls)
        // 4. THEN emit @no_interrupts/@atomic cleanup
        // 5. Finally, return
        //
        // This ensures that when returning a struct with heap data (like String),
        // we copy the data to the output parameter BEFORE Drop frees it.

        // Step 1: Deactivate defer blocks for variables being MOVED into the return value
        // This prevents use-after-free when ownership is transferred to the caller.
        if (_currentEmittingFunction != null && returnInst.Value != null)
        {
            DeactivateDefersForMovedVariables(returnInst.Value);
        }

        // Step 2: For struct/enum returns, copy to __out BEFORE running deferred cleanup
        if (returnInst.Value != null)
        {
            // VBCC FIX: For struct/enum returns, write to output parameter instead of returning directly
            var isStructOrEnumReturn = _currentEmittingFunction != null &&
                                      (_currentEmittingFunction.ReturnType is IrStructType or IrEnumType or IrTupleType);
            var shouldUseOutParam = isStructOrEnumReturn;

            if (shouldUseOutParam)
            {
                // CRITICAL FIX: For struct/enum literals, avoid compound literal temporaries
                // which can be placed at odd addresses by VBCC, causing Guru Meditation 80000006
                // Instead, emit field-by-field assignments directly to *__out
                if (returnInst.Value is IrStructLiteral structLit)
                {
                    // Check if any fields are arrays (which can't be assigned after declaration)
                    bool hasArrayFields = structLit.FieldValues.Any(kvp => kvp.Value.Type is IrArrayType);

                    if (hasArrayFields)
                    {
                        // Use memset + individual non-array field assignments
                        _output.AppendLine($"    __novus_memset(__out, 0, sizeof(*__out));");
                        foreach (var kvp in structLit.FieldValues)
                        {
                            // Skip array fields (already zeroed by memset, would need element-by-element copy)
                            if (kvp.Value.Type is IrArrayType arrayType)
                            {
                                // For now, array literals in struct returns are assumed to be zero-initialized
                                // This matches the common case. Full array literal support would need element-by-element assignment
                                continue;
                            }
                            // CRITICAL FIX: If field value is a pointer-converted parameter, dereference it
                            if (kvp.Value is IrVariable fieldVarWithArray &&
                                kvp.Value.Type is IrStructType &&
                                _pointerConvertedParameters.Contains(fieldVarWithArray.Name))
                            {
                                var srcVarName = SanitizeVariableName(fieldVarWithArray.Name);
                                _output.AppendLine($"    __out->{kvp.Key} = *{srcVarName};");
                            }
                            else
                            {
                                var fieldValue = EmitValue(kvp.Value);
                                _output.AppendLine($"    __out->{kvp.Key} = {fieldValue};");
                            }
                        }
                    }
                    else
                    {
                        // No array fields - emit field-by-field assignments
                        foreach (var kvp in structLit.FieldValues)
                        {
                            // CRITICAL FIX: If field value is a pointer-converted parameter (struct by-value
                            // parameter converted to pointer for ABI), we need to dereference it
                            if (kvp.Value is IrVariable fieldVar &&
                                kvp.Value.Type is IrStructType fieldStructType &&
                                _pointerConvertedParameters.Contains(fieldVar.Name))
                            {
                                // The parameter is a pointer in C - need to dereference and copy
                                var srcVarName = SanitizeVariableName(fieldVar.Name);
                                _output.AppendLine($"    __out->{kvp.Key} = *{srcVarName};");
                            }
                            else
                            {
                                var fieldValue = EmitValue(kvp.Value);
                                _output.AppendLine($"    __out->{kvp.Key} = {fieldValue};");

                                // CRITICAL FIX: Move semantics for struct fields containing droppable content
                                // When a struct literal's field is a local variable, zero it to prevent double-free
                                var fieldSourceIsLocalVar = kvp.Value is IrVariable;
                                var fieldSourceIsSlot = fieldValue != null && fieldValue.StartsWith("_slot_");
                                if ((fieldSourceIsLocalVar || fieldSourceIsSlot) && TypeContainsDroppableContent(kvp.Value.Type))
                                {
                                    var fieldCType = GetCType(kvp.Value.Type);
                                    _output.AppendLine($"    /* Move semantics: zero source to prevent double-free on return */");
                                    _output.AppendLine($"    __novus_memset(&{fieldValue}, 0, sizeof({fieldCType}));");
                                }
                            }
                        }
                    }
                }
                else if (returnInst.Value is IrEnumValue enumValue)
                {
                    var enumType = enumValue.Type as IrEnumType;

                    // Check if this enum has ANY variants with associated data
                    bool hasAnyData = enumType!.Variants.Any(v => v.HasAssociatedData);

                    if (hasAnyData)
                    {
                        // CRITICAL FIX: For enum values with associated data, avoid compound literals
                        // Emit field-by-field assignments for the tagged union
                        var enumName = MangleName(enumType);

                        // Set the tag
                        _output.AppendLine($"    __out->tag = {enumName}_{enumValue.VariantName};");

                        // Set the associated data if present
                        if (enumValue.AssociatedValues.Count > 0)
                        {
                            // Check if this is a unit type
                            bool isUnitType = enumValue.AssociatedValues.Count == 1 &&
                                             enumValue.AssociatedValues[0].Type is IrTupleType tupleType &&
                                             tupleType.ElementTypes.Count == 0;

                            if (!isUnitType)
                            {
                                for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
                                {
                                    // CRITICAL FIX: If associated value is a struct literal, emit field-by-field
                                    // to avoid compound literal temporaries that can be misaligned
                                    if (enumValue.AssociatedValues[i] is IrStructLiteral assocStructLit)
                                    {
                                        foreach (var kvp in assocStructLit.FieldValues)
                                        {
                                            // CRITICAL FIX: If field value is a pointer-converted parameter containing a struct,
                                            // we need to copy field-by-field instead of assigning the pointer
                                            if (kvp.Value is IrVariable fieldVar &&
                                                kvp.Value.Type is IrStructType fieldStructType &&
                                                _pointerConvertedParameters.Contains(SanitizeVariableName(fieldVar.Name)))
                                            {
                                                // This is a by-value parameter that was converted to a pointer
                                                // Copy each field of the struct
                                                var srcVarName = SanitizeVariableName(fieldVar.Name);
                                                foreach (var structField in fieldStructType.Fields)
                                                {
                                                    _output.AppendLine($"    __out->data.{enumValue.VariantName}._{i}.{kvp.Key}.{structField.Name} = {srcVarName}->{structField.Name};");
                                                }
                                            }
                                            // VBCC FIX: Array fields cannot be assigned with brace initializers
                                            else if (kvp.Value is IrArrayLiteral arrayFieldLit)
                                            {
                                                // VBCC 68K FIX: Use safe array copy to avoid compound literal alignment issues
                                                var arrayFieldType = arrayFieldLit.Type as IrArrayType;
                                                EmitSafeArrayCopy($"__out->data.{enumValue.VariantName}._{i}.{kvp.Key}", arrayFieldLit, arrayFieldType!);
                                            }
                                            else
                                            {
                                                // Normal field assignment
                                                var fieldValue = EmitValue(kvp.Value);
                                                _output.AppendLine($"    __out->data.{enumValue.VariantName}._{i}.{kvp.Key} = {fieldValue};");

                                                // CRITICAL FIX: Move semantics for struct fields containing droppable content
                                                var fieldSourceIsLocalVar = kvp.Value is IrVariable;
                                                var fieldSourceIsSlot = fieldValue != null && fieldValue.StartsWith("_slot_");
                                                if ((fieldSourceIsLocalVar || fieldSourceIsSlot) && TypeContainsDroppableContent(kvp.Value.Type))
                                                {
                                                    var fieldCType = GetCType(kvp.Value.Type);
                                                    _output.AppendLine($"    /* Move semantics: zero source to prevent double-free on return */");
                                                    _output.AppendLine($"    __novus_memset(&{fieldValue}, 0, sizeof({fieldCType}));");
                                                }
                                            }
                                        }
                                    }
                                    // VBCC_004: Compound literals cause misalignment on 68k - emit field-by-field for nested enum literals
                                    else if (enumValue.AssociatedValues[i] is IrEnumValue nestedEnumValueReturn)
                                    {
                                        EmitNestedEnumFieldByField($"__out->data.{enumValue.VariantName}._{i}", nestedEnumValueReturn);
                                    }
                                    else
                                    {
                                        var assocValue = EmitValue(enumValue.AssociatedValues[i]);
                                        var assocValueType = enumValue.AssociatedValues[i].Type;
                                        // VBCC 68K FIX: Use memcpy for struct types to avoid illegal instruction on 68040
                                        if (TypeRequiresMemcpy(assocValueType))
                                        {
                                            var cType = GetCType(assocValueType);
                                            _output.AppendLine($"    __novus_memcpy((uint8_t*)&__out->data.{enumValue.VariantName}._{i}, (uint8_t*)&{assocValue}, sizeof({cType}));");

                                            // CRITICAL FIX: Move semantics when copying into return value's enum variant data.
                                            // Zero the source to prevent deferred cleanup from double-freeing.
                                            var sourceIsLocalVar = enumValue.AssociatedValues[i] is IrVariable;
                                            if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                            {
                                                _output.AppendLine($"    /* Move semantics: zero source to prevent double-free on return */");
                                                _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                            }
                                        }
                                        else
                                        {
                                            _output.AppendLine($"    __out->data.{enumValue.VariantName}._{i} = {assocValue};");

                                            // CRITICAL FIX: Move semantics for direct assignment too
                                            var sourceIsLocalVar = enumValue.AssociatedValues[i] is IrVariable;
                                            if (sourceIsLocalVar && TypeContainsDroppableContent(assocValueType))
                                            {
                                                var cType = GetCType(assocValueType);
                                                _output.AppendLine($"    /* Move semantics: zero source to prevent double-free on return */");
                                                _output.AppendLine($"    __novus_memset(&{assocValue}, 0, sizeof({cType}));");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Unit type - set dummy field
                                _output.AppendLine($"    __out->data.{enumValue.VariantName}._dummy = 0;");
                            }
                        }
                    }
                    else
                    {
                        // Simple enum without associated data - just assign the enum value
                        var value = EmitValue(enumValue);
                        _output.AppendLine($"    *__out = {value};");
                    }
                }
                else
                {
                    var value = EmitValue(returnInst.Value);

                    // BUG FIX: Check if the return value is a pointer-converted parameter.
                    // Struct parameters containing heap data are passed as pointers (e.g., ScreenBuilder* self),
                    // so when returning them via memcpy, we should NOT add & (they're already pointers).
                    bool isPointerConvertedParam = returnInst.Value is IrVariable returnVariable &&
                                                   _pointerConvertedParameters.Contains(returnVariable.Name);

                    // VBCC 68K FIX: Use memcpy for complex types (enums with associated data, nested structs)
                    // VBCC generates illegal instructions on 68040 for struct-by-value assignment of these types.
                    // This handles cases like `return event;` where event is WindowEvent (enum with data).
                    if (TypeRequiresMemcpy(returnInst.Value.Type))
                    {
                        var cType = GetCType(returnInst.Value.Type);

                        // For pointer-converted parameters, use the pointer directly (no &)
                        // For local variables, take address with &
                        var srcExpr = isPointerConvertedParam ? value : $"&{value}";
                        _output.AppendLine($"    __novus_memcpy((uint8_t*)__out, (uint8_t*){srcExpr}, sizeof({cType}));");

                        // CRITICAL FIX: Move semantics when copying local variable to return value.
                        // Zero the source to prevent deferred cleanup from double-freeing.
                        var sourceIsLocalVar = returnInst.Value is IrVariable;
                        if (sourceIsLocalVar && TypeContainsDroppableContent(returnInst.Value.Type))
                        {
                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free on return */");
                            // For pointer-converted params, dereference to get struct for memset
                            var zeroTarget = isPointerConvertedParam ? value : $"&{value}";
                            _output.AppendLine($"    __novus_memset({zeroTarget}, 0, sizeof({cType}));");
                        }
                    }
                    else
                    {
                        // For pointer-converted parameters, dereference to get the struct value
                        var assignValue = isPointerConvertedParam ? $"*{value}" : value;
                        _output.AppendLine($"    *__out = {assignValue};");

                        // CRITICAL FIX: Move semantics for direct assignment too
                        var sourceIsLocalVar = returnInst.Value is IrVariable;
                        if (sourceIsLocalVar && TypeContainsDroppableContent(returnInst.Value.Type))
                        {
                            var cType = GetCType(returnInst.Value.Type);
                            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free on return */");
                            var zeroTarget = isPointerConvertedParam ? value : $"&{value}";
                            _output.AppendLine($"    __novus_memset({zeroTarget}, 0, sizeof({cType}));");
                        }
                    }
                }

                // CRITICAL FIX: If returning a field from a by-value parameter containing heap data,
                // null out the source field to prevent double-free when the parameter's destructor runs.
                // Example: fn finish(self) -> String { self.buffer }
                // After copying self.buffer to output, we must null self.buffer.vec.ptr
                // Check if the return value came from a member access we tracked

                // CRITICAL BUG FIX: Sanitize the variable name before lookup
                // The _memberAccessInfo dictionary uses sanitized names (_t14), but returnVar.Name
                // contains the IR name (%t14), so we must sanitize before lookup.
                if (returnInst.Value is IrVariable returnVar)
                {
                    var sanitizedName = SanitizeVariableName(returnVar.Name);
                    if (_memberAccessInfo.TryGetValue(sanitizedName, out var accessInfo))
                    {
                        var (structValue, fieldName, accessor, fieldStructType) = accessInfo;
                        // This is a move operation - null out the source field to prevent double-free
                        NullOutPointerFields(structValue, fieldName, fieldStructType, accessor);
                    }
                }
            }
        }

        // Step 3: NOW emit deferred cleanup AFTER return value has been copied
        // This ensures Drop doesn't free memory we just copied to the caller
        if (_currentEmittingFunction != null)
        {
            EmitDeferredCleanup(_currentEmittingFunction, 1);
        }

        // Step 4: Emit @no_interrupts cleanup (Enable) after deferred cleanup
        // This must come AFTER deferred cleanup since defer might need interrupts
        if (_currentEmittingFunction?.Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.NoInterrupts) ?? false)
        {
            _output.AppendLine("    Enable(); /* @no_interrupts cleanup */");
        }

        // Step 5: Emit @atomic cleanup (Permit) after Enable
        if (_currentEmittingFunction?.Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.Atomic) ?? false)
        {
            _output.AppendLine("    Permit(); /* @atomic cleanup */");
        }

        // Memory tracking: Emit cleanup and report before main() returns
        if (_safetyLevel.EnableMemoryTracking() &&
            _currentEmittingFunction != null &&
            _currentEmittingFunction.Name == "main")
        {
            _output.AppendLine("    __novus_memory_report();");
            _output.AppendLine("    __novus_cleanup_mmu_protection();");
        }

        // Step 6: Finally, emit the actual return statement
        if (returnInst.Value != null)
        {
            var isStructOrEnumReturn = _currentEmittingFunction != null &&
                                      (_currentEmittingFunction.ReturnType is IrStructType or IrEnumType or IrTupleType);

            if (isStructOrEnumReturn)
            {
                // Struct/enum was already copied to __out above
                _output.AppendLine("    return;");
            }
            else
            {
                // Direct return for primitives/pointers
                var value = EmitValue(returnInst.Value);
                _output.AppendLine($"    return {value};");
            }
        }
        else
        {
            // No return value provided - this happens after exhaustive match expressions
            // where all arms return. The fallthrough path is never reached, but we still
            // need to satisfy the compiler by providing a return value for non-void functions.
            if (_currentEmittingFunction?.ReturnType is IrVoidType or null)
            {
                _output.AppendLine("    return;");
            }
            else if (_currentEmittingFunction?.ReturnType is IrStructType or IrEnumType or IrTupleType)
            {
                // For struct/enum returns (which use __out parameter), just return;
                _output.AppendLine("    return;");
            }
            else
            {
                // Non-void primitive/pointer return - provide a dummy zero value
                // This path should never be reached at runtime (exhaustive matches)
                var cType = GetCType(_currentEmittingFunction!.ReturnType);
                _output.AppendLine($"    return ({cType})0; /* unreachable */");
            }
        }
    }

    /// <summary>
    /// Emits a return statement appropriate for the current function's return type.
    /// Used after error handlers (panic, assert, div-by-zero) that display an error and return.
    /// For void functions: emits "return;"
    /// For non-void functions: emits "return (Type){0};" (zero-initialized value - VBCC compatible)
    /// </summary>
    /// <param name="indentLevel">Number of indentation levels (each is 4 spaces)</param>
    private void EmitErrorPathReturn(int indentLevel)
    {
        if (_currentEmittingFunction == null)
        {
            // Shouldn't happen, but be defensive
            _output.AppendLine($"{new string(' ', indentLevel * 4)}return;");
            return;
        }

        var indent = new string(' ', indentLevel * 4);
        var returnType = _currentEmittingFunction.ReturnType;

        if (returnType is IrVoidType)
        {
            // Void function: just return
            _output.AppendLine($"{indent}return;");
        }
        else if (returnType is IrStructType or IrEnumType or IrTupleType)
        {
            // Struct/enum/tuple returns use __out parameter, so C function is void
            // Just return without a value (error has already been reported)
            _output.AppendLine($"{indent}return;");
        }
        else
        {
            // Non-void primitive/pointer function: return zero-initialized value
            var cType = GetCType(returnType);
            _output.AppendLine($"{indent}return ({cType})0;  // Zero-initialized after error");
        }
    }

    private void EmitAssert(IrAssert assert)
    {
        // In release mode, asserts are completely elided (no-op)
        if (_buildMode == BuildMode.Release)
        {
            return;
        }

        // Evaluate the condition
        var condition = EmitValue(assert.Condition);

        // Generate the assertion check
        _output.AppendLine($"    if (!({condition})) {{");

        // Call runtime assert handler to display error (uses EasyRequest on Amiga)
        var fileName = assert.Location?.FilePath ?? "<unknown>";
        var line = assert.Location?.Line ?? 0;
        var col = assert.Location?.Column ?? 0;

        if (assert.Message != null)
        {
            // Escape the message for C string literal
            var escapedMessage = assert.Message
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

            _output.AppendLine($"        __novus_assert_failed(\"{fileName}\", {line}, {col}, \"{escapedMessage}\");");
        }
        else
        {
            _output.AppendLine($"        __novus_assert_failed(\"{fileName}\", {line}, {col}, NULL);");
        }

        // Execute deferred cleanup before exiting (CRITICAL for resource safety)
        if (_currentEmittingFunction != null)
        {
            EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2 for inside the if block
        }

        // Return from function after error (emits proper return type)
        EmitErrorPathReturn(2);  // indent level 2
        _output.AppendLine("    }");
    }

    private void EmitPanic(IrPanic panic)
    {
        // Panic is NEVER elided (even in release mode) - it's for unrecoverable runtime errors

        // Call runtime panic handler to display error (uses EasyRequest on Amiga)
        var fileName = panic.Location?.FilePath ?? "<unknown>";
        var line = panic.Location?.Line ?? 0;
        var col = panic.Location?.Column ?? 0;

        // Escape the message for C string literal
        var escapedMessage = panic.Message
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        _output.AppendLine($"    __novus_panic(\"{escapedMessage}\", \"{fileName}\", {line}, {col});");

        // Execute deferred cleanup before halting (CRITICAL for resource safety)
        if (_currentEmittingFunction != null)
        {
            EmitDeferredCleanup(_currentEmittingFunction, 1); // indent level 1
        }

        // Return from function after error (emits proper return type)
        // Note: This code is unreachable after showing the error, but required for C semantics
        EmitErrorPathReturn(1);  // indent level 1
    }

    /// <summary>
    /// Emit inline assembly for M68k/VBCC.
    ///
    /// VBCC uses "inline-assembly functions" which are function declarations followed by
    /// '=' and a string constant. When called, no function call is generated - the string
    /// is inserted directly into the assembly output.
    ///
    /// Example: double sin(__reg("fp0") double) = "\tfsin.x\tfp0\n";
    ///
    /// We generate:
    /// 1. An inline-assembly function declaration with __reg() for register bindings
    /// 2. A call to that function with our input values
    /// 3. Capture the result (return value is in the designated output register)
    /// </summary>
    private void EmitInlineAsm(IrInlineAsm inlineAsm)
    {
        _output.AppendLine("    /* Inline assembly block */");

        // Build the parameter list with __reg() specifiers
        var paramList = new List<string>();
        var argList = new List<string>();

        foreach (var input in inlineAsm.Inputs)
        {
            var regName = input.GetRegisterName();
            var cType = GetCType(input.Type);
            var inputValue = EmitValue(input.Value);

            if (input.IsAddressOf)
            {
                // For &var bindings, we pass a pointer and use address-of operator
                var ptrType = cType + "*";
                if (!string.IsNullOrEmpty(regName))
                {
                    paramList.Add($"__reg(\"{regName}\") {ptrType}");
                }
                else
                {
                    paramList.Add(ptrType);
                }
                argList.Add($"&{inputValue}");
            }
            else
            {
                // Normal value binding
                if (!string.IsNullOrEmpty(regName))
                {
                    paramList.Add($"__reg(\"{regName}\") {cType}");
                }
                else
                {
                    paramList.Add(cType);
                }
                argList.Add($"({cType}){inputValue}");
            }
        }

        // Check for mut...out writebacks early - we need this for return type determination
        var writebackInputs = inlineAsm.Inputs.Where(i => i.IsMutable && i.OutputRegister.HasValue).ToList();

        // Determine return type and register
        string returnType = "void";
        string outputReg = "";
        if (inlineAsm.Outputs.Count > 0)
        {
            var output = inlineAsm.Outputs[0];
            returnType = GetCType(output.Type);
            outputReg = output.GetRegisterName();
        }
        else if (writebackInputs.Count > 0)
        {
            // No explicit output, but we have mut...out writeback - use that as return
            var writebackInput = writebackInputs[0];
            returnType = GetCType(writebackInput.Type);
            outputReg = writebackInput.OutputRegister!.Value.ToString().ToLower();
        }

        // Build the assembly string
        var asmParts = new List<string>();

        // Add clobber register saves (for callee-saved registers d2-d7, a2-a6)
        var savedRegs = new List<string>();
        foreach (var clobber in inlineAsm.Clobbers)
        {
            var regName = clobber.ToString().ToLower();
            if (IsCalleeSavedRegister(clobber))
            {
                savedRegs.Add(regName);
            }
        }

        if (savedRegs.Count > 0)
        {
            var regList = string.Join("/", savedRegs);
            asmParts.Add($"\\tmovem.l {regList},-(sp)\\n");
        }

        // Add the user's assembly instructions
        foreach (var instruction in inlineAsm.Instructions)
        {
            var substituted = inlineAsm.SubstituteParameters(instruction);
            var escaped = substituted
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
            asmParts.Add($"\\t{escaped}\\n");
        }

        // Restore saved registers
        if (savedRegs.Count > 0)
        {
            var regList = string.Join("/", savedRegs);
            asmParts.Add($"\\tmovem.l (sp)+,{regList}\\n");
        }

        var asmString = string.Join("", asmParts);
        var paramString = string.Join(", ", paramList);
        var argString = string.Join(", ", argList);

        // Generate unique inline asm function name
        var funcName = $"__novus_inline_asm_{_inlineAsmCounter++}";

        // Generate the inline assembly function declaration
        // For functions with outputs, we need __reg on return to ensure the result stays in the right register
        if (!string.IsNullOrEmpty(outputReg))
        {
            _output.AppendLine($"    __reg(\"{outputReg}\") {returnType} {funcName}({paramString}) = \"{asmString}\";");
        }
        else
        {
            _output.AppendLine($"    {returnType} {funcName}({paramString}) = \"{asmString}\";");
        }

        // Call the inline assembly function and capture result
        if (inlineAsm.Outputs.Count == 1 && inlineAsm.ResultName != null)
        {
            var resultName = SanitizeVariableName(inlineAsm.ResultName);
            var output = inlineAsm.Outputs[0];
            var cType = GetCType(output.Type);

            // Declare result variable if needed
            if (!_declaredVariables.Contains(resultName))
            {
                _output.AppendLine($"    {GetCVariableDeclaration(output.Type, resultName)};");
                _declaredVariables.Add(resultName);
            }

            _output.AppendLine($"    {resultName} = {funcName}({argString});");

            // If the return register matches a writeback input, write back to the original variable
            foreach (var writebackInput in writebackInputs)
            {
                if (writebackInput.OutputRegister == output.Register)
                {
                    var originalVar = EmitValue(writebackInput.Value);
                    _output.AppendLine($"    {originalVar} = {resultName};");
                }
            }
        }
        else if (inlineAsm.Outputs.Count > 1 && inlineAsm.MultiResultNames != null)
        {
            // Multi-register return - VBCC doesn't support this directly
            // We'd need separate inline asm functions for each output
            // For now, just call and get first result
            var resultName = SanitizeVariableName(inlineAsm.MultiResultNames[0]);
            var output = inlineAsm.Outputs[0];

            if (!_declaredVariables.Contains(resultName))
            {
                _output.AppendLine($"    {GetCVariableDeclaration(output.Type, resultName)};");
                _declaredVariables.Add(resultName);
            }

            _output.AppendLine($"    {resultName} = {funcName}({argString});");

            // Multiple outputs would require additional asm helper functions with different signatures.
            // VBCC only supports a single return value per function, so multi-output asm would need
            // either output parameters or multiple accessor functions.
        }
        else if (writebackInputs.Count > 0)
        {
            // No explicit output, but we have mut...out writebacks
            // The asm function needs to return the writeback value
            // For simplicity, handle single writeback - VBCC only supports one return
            var writebackInput = writebackInputs[0];
            var writebackType = GetCType(writebackInput.Type);
            var originalVar = EmitValue(writebackInput.Value);

            // Generate a temp variable to capture the result
            var tempVar = $"__asm_writeback_{_inlineAsmCounter - 1}";
            _output.AppendLine($"    {writebackType} {tempVar} = {funcName}({argString});");
            _output.AppendLine($"    {originalVar} = {tempVar};");
        }
        else if (argList.Count > 0 || inlineAsm.Instructions.Count > 0)
        {
            // Void return - just call the function
            _output.AppendLine($"    {funcName}({argString});");
        }
        else if (inlineAsm.Instructions.Count > 0)
        {
            // No inputs - define and call with no args
            _output.AppendLine($"    {funcName}();");
        }

        _output.AppendLine("    /* End inline assembly */");
    }

    private int _inlineAsmCounter = 0;

    /// <summary>
    /// Check if a register is callee-saved (needs to be preserved across function calls).
    /// Caller-saved: d0, d1, a0, a1
    /// Callee-saved: d2-d7, a2-a6
    /// </summary>
    private static bool IsCalleeSavedRegister(M68kRegister reg)
    {
        return reg switch
        {
            M68kRegister.D2 => true,
            M68kRegister.D3 => true,
            M68kRegister.D4 => true,
            M68kRegister.D5 => true,
            M68kRegister.D6 => true,
            M68kRegister.D7 => true,
            M68kRegister.A2 => true,
            M68kRegister.A3 => true,
            M68kRegister.A4 => true,
            M68kRegister.A5 => true,
            M68kRegister.A6 => true,
            _ => false
        };
    }

    /// <summary>
    /// Emit closure creation code.
    /// Creates the fat pointer struct and initializes captured variables in the environment.
    /// </summary>
    private void EmitCreateClosure(IrCreateClosure createClosure)
    {
        var closureType = createClosure.ClosureType;
        var closureTypeName = GetClosureTypeName(closureType);
        var resultName = SanitizeVariableName(createClosure.ResultName);

        // Declare the closure variable if needed
        if (!_declaredVariables.Contains(resultName))
        {
            _output.AppendLine($"    {closureTypeName} {resultName};");
            _declaredVariables.Add(resultName);
        }

        // Set the function pointer
        _output.AppendLine($"    {resultName}.fn_ptr = {createClosure.GeneratedFunctionName};");

        // Handle environment
        if (createClosure.CapturedValues.Count == 0)
        {
            // Stateless closure - no environment needed
            _output.AppendLine($"    {resultName}.env_ptr = NULL;");
        }
        else
        {
            // Allocate environment struct
            var envTypeName = closureType.EnvironmentType != null
                ? MangleName(closureType.EnvironmentType)
                : $"__closure_env_{createClosure.GeneratedFunctionName}";

            _output.AppendLine($"    {resultName}.env_ptr = __novus_alloc_raw(sizeof(struct {envTypeName}), MEMF_FAST);");
            _output.AppendLine($"    struct {envTypeName}* __env = (struct {envTypeName}*){resultName}.env_ptr;");

            // Initialize refcount
            _output.AppendLine($"    __env->__refcount = 1;");

            // Copy captured values into environment
            foreach (var (name, value, mode) in createClosure.CapturedValues)
            {
                var valueExpr = EmitValue(value);
                if (mode == CaptureMode.ByValue)
                {
                    _output.AppendLine($"    __env->{name} = {valueExpr};");
                }
                else
                {
                    // By-reference or mutable capture - store pointer
                    _output.AppendLine($"    __env->{name} = &{valueExpr};");
                }
            }
        }
    }

    /// <summary>
    /// Emit closure invocation code.
    /// Calls the closure's function pointer with the environment as the first argument.
    /// </summary>
    private void EmitInvokeClosure(IrInvokeClosure invokeClosure)
    {
        var closureExpr = EmitValue(invokeClosure.Closure);
        var closureType = invokeClosure.Closure.Type as IrClosureType;

        // Build argument list - env_ptr first, then user arguments
        var args = new List<string> { $"{closureExpr}.env_ptr" };
        foreach (var arg in invokeClosure.Arguments)
        {
            args.Add(EmitValue(arg));
        }
        var argStr = string.Join(", ", args);

        // Call the function pointer
        if (invokeClosure.ResultName != null && invokeClosure.ReturnType is not IrVoidType)
        {
            var resultName = SanitizeVariableName(invokeClosure.ResultName);
            if (!_declaredVariables.Contains(resultName))
            {
                var decl = GetCVariableDeclaration(invokeClosure.ReturnType, resultName);
                _output.AppendLine($"    {decl};");
                _declaredVariables.Add(resultName);
            }
            _output.AppendLine($"    {resultName} = {closureExpr}.fn_ptr({argStr});");
        }
        else
        {
            _output.AppendLine($"    {closureExpr}.fn_ptr({argStr});");
        }
    }

    /// <summary>
    /// Emit load capture - loads a captured value from the environment.
    /// Called at the start of closure functions to bind captured variables.
    /// </summary>
    private void EmitLoadCapture(IrLoadCapture loadCapture)
    {
        var resultName = SanitizeVariableName(loadCapture.ResultName);
        var cType = GetCType(loadCapture.CaptureType);

        // Declare the local variable for the captured value
        if (!_declaredVariables.Contains(resultName))
        {
            _output.AppendLine($"    {GetCVariableDeclaration(loadCapture.CaptureType, resultName)};");
            _declaredVariables.Add(resultName);
        }

        // Load from environment (assumes __env parameter is available)
        if (loadCapture.Mode == CaptureMode.ByValue)
        {
            _output.AppendLine($"    {resultName} = __env->{loadCapture.CaptureName};");
        }
        else
        {
            // By-reference or mutable - dereference the stored pointer
            _output.AppendLine($"    {resultName} = *__env->{loadCapture.CaptureName};");
        }
    }

    /// <summary>
    /// Emit store capture - stores back to a mutable captured variable.
    /// Only used for |mut x| captures where x is modified inside the closure.
    /// </summary>
    private void EmitStoreCapture(IrStoreCapture storeCapture)
    {
        var valueExpr = EmitValue(storeCapture.Value);

        // Store through the pointer in the environment
        _output.AppendLine($"    *__env->{storeCapture.CaptureName} = {valueExpr};");
    }

    private void EmitMatch(IrMatch match)
    {
        var matchValue = EmitValue(match.MatchValue);
        var enumType = match.MatchValue.Type as IrEnumType;

        if (enumType == null)
            throw new InvalidOperationException("Match expression must be on an enum type");

        var enumName = MangleName(enumType);

        // If this match is an expression (has a result), declare the result variable
        // Skip void types - they have no runtime representation and can't be assigned to
        if (match.ResultName != null && match.ResultType != null && match.ResultType is not IrVoidType)
        {
            var resultVarName = SanitizeVariableName(match.ResultName);

            // Only declare if not already declared (e.g., as a slot for variable reuse)
            if (!_declaredVariables.Contains(resultVarName))
            {
                var decl = GetCVariableDeclaration(match.ResultType, resultVarName);
                _output.AppendLine($"    {decl};");
                _declaredVariables.Add(resultVarName);
            }
        }

        // Switch on the tag
        _output.AppendLine($"    switch ({matchValue}.tag) {{");

        bool hasWildcard = false;
        foreach (var arm in match.Arms)
        {
            if (arm.Pattern is IrVariantPattern variantPattern)
            {
                _output.AppendLine($"    case {enumName}_{variantPattern.VariantName}:");

                // Extract bound variables from variant data
                for (int i = 0; i < variantPattern.BoundVariables.Count; i++)
                {
                    var boundVar = variantPattern.BoundVariables[i];
                    var variant = enumType.GetVariant(variantPattern.VariantName);

                    // BUG FIX #3: Check for null variant before accessing properties
                    if (variant == null)
                    {
                        throw new InvalidOperationException($"Variant '{variantPattern.VariantName}' not found in enum '{enumName}'");
                    }

                    if (i < variant.AssociatedData.Count)
                    {
                        var decl = GetCVariableDeclaration(variant.AssociatedData[i], boundVar);
                        var boundVarType = variant.AssociatedData[i];
                        // VBCC 68K FIX: Use memcpy for struct types extracted from union to avoid illegal instruction on 68040
                        if (TypeRequiresMemcpy(boundVarType))
                        {
                            var cType = GetCType(boundVarType);
                            _output.AppendLine($"        {decl};");
                            _output.AppendLine($"        __novus_memcpy((uint8_t*)&{boundVar}, (uint8_t*)&{matchValue}.data.{variantPattern.VariantName}._{i}, sizeof({cType}));");
                        }
                        else
                        {
                            _output.AppendLine($"        {decl} = {matchValue}.data.{variantPattern.VariantName}._{i};");
                        }
                    }
                }

                _output.AppendLine($"        goto {arm.TargetLabel};");
            }
            else if (arm.Pattern is IrWildcardPattern)
            {
                hasWildcard = true;
                _output.AppendLine($"    default:");
                _output.AppendLine($"        goto {arm.TargetLabel};");
            }
        }

        // BUG FIX #2: Add default case if no wildcard pattern to catch invalid enum tags
        if (!hasWildcard)
        {
            _output.AppendLine($"    default:");
            _output.AppendLine($"        // Invalid enum tag - this should never happen in safe code");
            _output.AppendLine($"        __novus_panic(\"Invalid enum tag in match expression\", __FILE__, __LINE__, 0);");
        }

        _output.AppendLine("    }");
    }

    private void EmitExtractTag(IrExtractTag extractTag)
    {
        var enumValue = EmitValue(extractTag.EnumValue);
        var resultName = SanitizeVariableName(extractTag.ResultName);

        var enumType = extractTag.EnumValue.Type as IrEnumType;
        if (enumType == null)
            throw new InvalidOperationException("ExtractTag must be on an enum type");

        // Check if this enum has any associated data
        bool hasAnyData = enumType.Variants.Any(v => v.HasAssociatedData);

        // Check if variable was already declared (e.g., as a slot for variable reuse)
        var alreadyDeclared = _declaredVariables.Contains(resultName);

        if (!hasAnyData)
        {
            // OPTIMIZATION: For plain enums, the value IS the tag
            if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {enumValue};");
            }
            else
            {
                _output.AppendLine($"    int32_t {resultName} = {enumValue};");
            }
        }
        else
        {
            // For enums with associated data, extract the tag field
            if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {enumValue}.tag;");
            }
            else
            {
                _output.AppendLine($"    int32_t {resultName} = {enumValue}.tag;");
            }
        }
    }

    private void EmitExtractVariantData(IrExtractVariantData extractData)
    {
        // Skip unit type () extractions - unit has no runtime representation
        if (extractData.DataType is IrTupleType tupleType && tupleType.ElementTypes.Count == 0)
            return;

        var enumValue = EmitValue(extractData.EnumValue);
        var resultName = SanitizeVariableName(extractData.ResultName);
        var enumType = extractData.EnumValue.Type as IrEnumType;

        if (enumType == null)
            throw new InvalidOperationException("ExtractVariantData must be on an enum type");

        var dataType = GetCType(extractData.DataType);

        // Check if variable was already declared (e.g., as a slot for variable reuse)
        var alreadyDeclared = _declaredVariables.Contains(resultName);

        // VBCC 68K FIX: Use memcpy for struct types extracted from union to avoid illegal instruction on 68040
        if (TypeRequiresMemcpy(extractData.DataType))
        {
            if (!alreadyDeclared)
            {
                _output.AppendLine($"    {dataType} {resultName};");
                _declaredVariables.Add(resultName);
            }
            _output.AppendLine($"    __novus_memcpy((uint8_t*)&{resultName}, (uint8_t*)&{enumValue}.data.{extractData.VariantName}._{extractData.DataIndex}, sizeof({dataType}));");
        }
        // Extract data from the specific variant
        else if (alreadyDeclared)
        {
            _output.AppendLine($"    {resultName} = {enumValue}.data.{extractData.VariantName}._{extractData.DataIndex};");
        }
        else
        {
            _output.AppendLine($"    {dataType} {resultName} = {enumValue}.data.{extractData.VariantName}._{extractData.DataIndex};");
        }

        // CRITICAL FIX: After extracting data from an enum, zero out the source to prevent double-free.
        // When extracting Drop-able types from Option/Result, the data is moved (not copied).
        // We must invalidate the source enum so any subsequent Drop on the enum doesn't try
        // to free the same memory that was moved to the extracted variable.
        // We zero the source data using memset to ensure any pointers are nulled.
        //
        // IMPORTANT: Use TypeContainsDroppableContent (not TypeImplementsDrop) to catch types
        // like StringBuilder that contain Drop-able content through nested fields (String -> Vec).
        if (TypeContainsDroppableContent(extractData.DataType))
        {
            _output.AppendLine($"    /* Move semantics: zero source to prevent double-free */");
            _output.AppendLine($"    __novus_memset(&{enumValue}.data.{extractData.VariantName}._{extractData.DataIndex}, 0, sizeof({dataType}));");
        }
    }

    private void EmitMemberAccess(IrMemberAccess memberAccess)
    {
        var resultName = SanitizeVariableName(memberAccess.ResultName);
        var fieldType = GetCType(memberAccess.FieldType);
        var alreadyDeclared = _declaredVariables.Contains(resultName);

        // Special handling for struct literals: VBCC doesn't support member access on compound literals
        // We need to create a temporary variable first
        if (memberAccess.Struct is IrStructLiteral structLit)
        {
            // CRITICAL FIX FOR VBCC ALIGNMENT: Str struct literals cause crashes because VBCC
            // places them at odd addresses. Instead of creating the temp var with a compound literal,
            // extract the field value directly from the struct literal.
            var structType = memberAccess.Struct.Type as IrStructType;
            var isStrStruct = structType != null && structType.Name == "Str";

            if (isStrStruct && structLit.FieldValues.TryGetValue(memberAccess.FieldName, out var fieldValue))
            {
                // Directly emit the field value without creating the Str temporary
                var directValue = EmitValue(fieldValue);
                if (alreadyDeclared)
                {
                    _output.AppendLine($"    {resultName} = {directValue};");
                }
                else
                {
                    _output.AppendLine($"    {fieldType} {resultName} = {directValue};");
                }
            }
            else
            {
                // For other structs, use the temp variable approach but with field-by-field assignment
                // for Str structs to avoid compound literals
                var tempVarName = $"_str_tmp_{_tempCounter++}";
                var cStructType = GetCType(memberAccess.Struct.Type);

                if (isStrStruct)
                {
                    // Declare and assign fields individually for Str
                    _output.AppendLine($"    {cStructType} {tempVarName};");
                    foreach (var kvp in structLit.FieldValues)
                    {
                        var fValue = EmitValue(kvp.Value);
                        _output.AppendLine($"    {tempVarName}.{kvp.Key} = {fValue};");
                    }
                }
                else
                {
                    // For non-Str structs, use compound literal
                    var structValue = EmitValue(memberAccess.Struct);
                    _output.AppendLine($"    {cStructType} {tempVarName} = {structValue};");
                }
                if (alreadyDeclared)
                {
                    _output.AppendLine($"    {resultName} = {tempVarName}.{memberAccess.FieldName};");
                }
                else
                {
                    _output.AppendLine($"    {fieldType} {resultName} = {tempVarName}.{memberAccess.FieldName};");
                }
            }
        }
        else
        {
            // Determine the correct accessor (. or ->) and whether this is a move operation
            var accessor = GetStructAccessor(memberAccess.Struct);

            // If the struct is a dereference and we're using ->, emit just the pointer value
            // instead of (*ptr)->field, emit ptr->field
            string structValue;
            if (memberAccess.Struct is IrDereferenceValue derefValue && accessor == "->")
            {
                structValue = EmitValue(derefValue.PointerValue);
            }
            else
            {
                structValue = EmitValue(memberAccess.Struct);
            }

            // Emit null pointer check for pointer member access (using ->) if enabled
            if (accessor == "->" && _safetyLevel.EnableNullChecks())
            {
                var locInfo = memberAccess.Location;
                var filePath = locInfo?.FilePath != null ? System.IO.Path.GetFileName(locInfo.FilePath) : "<compiler-generated>";
                var line = locInfo?.Line ?? 0;

                // VBCC WORKAROUND (VBCC_001): Use __novus_is_null() to force a sequence point.
                // VBCC's optimizer can move addq.w #4,sp between a comparison and its branch,
                // clobbering condition flags. A function call creates a sequence point.
                _output.AppendLine($"    if (__novus_is_null({structValue})) {{");
                _output.AppendLine($"        __novus_null_pointer_error(\"{EscapeString(filePath)}\", {line});");

                // Execute deferred cleanup before returning
                if (_currentEmittingFunction != null)
                {
                    EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
                }

                // Return from function after error
                EmitErrorPathReturn(2);  // indent level 2
                _output.AppendLine($"    }}");
            }

            var isMovingField = false;  // Only true when actually consuming/moving the field
            IrStructType? sourceStructType = null;

            // Check if this is a move operation (for future move semantics)
            // Currently disabled to fix the &mut self bug - we treat all field accesses as non-moves
            if (memberAccess.Struct is IrVariable variable &&
                _currentEmittingFunction != null)
            {
                var param = _currentEmittingFunction.Parameters.FirstOrDefault(p => p.Name == variable.Name);
                if (param != null)
                {
                    // References and mut references are NEVER moves - they're borrows for access only
                    if (param.Type is IrReferenceType or IrMutReferenceType)
                    {
                        isMovingField = false;
                    }
                    // Struct parameters containing heap data are passed by pointer
                    else if (param.Type is IrStructType structType && TypeContainsHeapData(structType))
                    {
                        sourceStructType = structType;

                        // For by-value parameters, field access semantics depend on context:
                        // - Field reference for borrowing (e.g., &field or &mut field) - NOT a move
                        // - Field extraction for return/assignment (e.g., return self.field) - IS a move
                        // Currently we conservatively treat all field accesses from by-value params
                        // as non-moves to avoid incorrect Drop calls on borrowed fields.
                        // More precise analysis would require tracking borrow vs move at the IR level.
                        isMovingField = false;
                    }
                }
            }

            // VBCC 68K FIX: Skip struct copy if this member access result is ONLY used for address-of.
            // When the result variable is only ever used in &variable context, we don't need to
            // materialize the struct value at all - we just need the chain info for lvalue reconstruction.
            // This prevents illegal instruction crashes (Guru $80000004) on 68040 when VBCC generates
            // unsupported opcodes for large struct copies.
            bool skipStructCopy = _addressOnlyStructMemberAccess.Contains(resultName);

            if (!skipStructCopy)
            {
                // VBCC FIX: VBCC cannot compile struct-by-value assignment for complex types:
                // - Structs containing nested structs (e.g., SpriteData with ChipMemHandle)
                // - Enums with associated data (e.g., Result<T,E>, Option<T> - these become tagged unions)
                // VBCC generates illegal instructions on 68040 for these types.
                // Solution: Use __novus_memcpy() instead of direct assignment.
                bool useMemcpy = TypeRequiresMemcpy(memberAccess.FieldType);

                if (useMemcpy)
                {
                    // For structs with nested structs, use memcpy
                    if (alreadyDeclared)
                    {
                        _output.AppendLine($"    __novus_memcpy((uint8_t*)&{resultName}, (uint8_t*)&({structValue}{accessor}{memberAccess.FieldName}), sizeof({fieldType}));");
                    }
                    else
                    {
                        _output.AppendLine($"    {fieldType} {resultName};");
                        _output.AppendLine($"    __novus_memcpy((uint8_t*)&{resultName}, (uint8_t*)&({structValue}{accessor}{memberAccess.FieldName}), sizeof({fieldType}));");
                    }
                }
                else
                {
                    // For simple types and structs without nested structs, direct assignment works
                    if (alreadyDeclared)
                    {
                        _output.AppendLine($"    {resultName} = {structValue}{accessor}{memberAccess.FieldName};");
                    }
                    else
                    {
                        _output.AppendLine($"    {fieldType} {resultName} = {structValue}{accessor}{memberAccess.FieldName};");
                    }
                }
            }

            // Track this field access for lvalue chain reconstruction
            // This allows us to reconstruct the full chain (e.g., self->timereq->tr_node)
            // when this result is later used in a store or address-of operation
            // IMPORTANT: If structValue itself is a variable that came from a field access,
            // we need to use the reconstructed chain as the base to build a complete chain
            string chainBase = structValue;
            if (memberAccess.Struct is IrVariable baseVar)
            {
                var sanitizedBaseName = SanitizeVariableName(baseVar.Name);
                if (_fieldAccessChainInfo.ContainsKey(sanitizedBaseName))
                {
                    // The base came from a field access - use its reconstructed chain
                    chainBase = ReconstructFieldAccessChain(baseVar.Name);
                }
            }
            _fieldAccessChainInfo[resultName] = (chainBase, memberAccess.FieldName, accessor);

            // Track this member access for potential move semantics
            // If this result is later returned, we'll need to null out the source
            if (memberAccess.Struct is IrVariable trackVar &&
                _currentEmittingFunction != null &&
                memberAccess.FieldType is IrStructType trackFieldType &&
                TypeContainsHeapData(memberAccess.FieldType))
            {
                var trackParam = _currentEmittingFunction.Parameters.FirstOrDefault(p => p.Name == trackVar.Name);

                if (trackParam != null &&
                    trackParam.Type is IrStructType &&
                    TypeContainsHeapData(trackParam.Type) &&
                    trackParam.Type is not IrReferenceType and not IrMutReferenceType)
                {
                    // Track this for potential move detection in return statement
                    _memberAccessInfo[resultName] = (structValue, memberAccess.FieldName, accessor, trackFieldType);
                }
            }

            // Only null out pointer fields if this is actually a move operation
            // (not just accessing a field through a reference)
            if (isMovingField && sourceStructType != null &&
                memberAccess.FieldType is IrStructType moveFieldType &&
                TypeContainsHeapData(memberAccess.FieldType))
            {
                // Recursively null out all pointer fields in the moved struct
                NullOutPointerFields(structValue, memberAccess.FieldName, moveFieldType, accessor);
            }
        }
    }

    /// <summary>
    /// Null out pointer fields in a struct to prevent double-free after a move.
    /// This is called when we extract a heap-containing field from a by-value parameter.
    /// Respects SafetyLevel: skips in Unsafe mode, adds debug comments in Full/Paranoid.
    /// </summary>
    private void NullOutPointerFields(string structValue, string fieldName, IrStructType structType, string accessor)
    {
        // Skip null-outs in unsafe mode (no safety checks)
        if (_safetyLevel == SafetyLevel.Unsafe)
            return;

        // Add debug comment for Full and Paranoid modes
        if (_safetyLevel >= SafetyLevel.Full)
        {
            _output.AppendLine($"    // DEBUG: Nulling out moved field {fieldName} to prevent double-free");
        }

        foreach (var field in structType.Fields)
        {
            if (field.Type is IrPointerType)
            {
                // Null out the pointer field
                _output.AppendLine($"    {structValue}{accessor}{fieldName}.{field.Name} = 0;");
            }
            else if (field.Type is IrStructType nestedStruct && TypeContainsHeapData(nestedStruct))
            {
                // Recursively null out nested struct's pointer fields
                NullOutPointerFields(structValue, $"{fieldName}.{field.Name}", nestedStruct, accessor);
            }
        }
    }

    /// <summary>
    /// Reconstruct the full lvalue expression for a variable that may have come from a field access chain.
    /// For example, if _t1 = self->timereq and _t2 = _t1->tr_node, then reconstructing _t2 gives "self->timereq->tr_node".
    /// </summary>
    private string ReconstructFieldAccessChain(string varName)
    {
        var sanitizedName = SanitizeVariableName(varName);

        if (_fieldAccessChainInfo.TryGetValue(sanitizedName, out var chainInfo))
        {
            var (baseExpr, fieldName, accessor) = chainInfo;
            return $"{baseExpr}{accessor}{fieldName}";
        }

        // Not a field access chain - return the variable name as-is
        return sanitizedName;
    }

    /// <summary>
    /// Check if a variable came from a field access (and thus has chain info available).
    /// </summary>
    private bool IsFieldAccessVariable(string varName)
    {
        var sanitizedName = SanitizeVariableName(varName);
        return _fieldAccessChainInfo.ContainsKey(sanitizedName);
    }

    /// <summary>
    /// VBCC 68K FIX: Pre-scan a function to identify struct member accesses that are ONLY used
    /// for address-of operations. When a struct field is only used to take its address
    /// (e.g., &amp;(*screen).BitMap), we don't need to copy the struct value - we can just
    /// track the chain info and emit &amp;(base->field) directly.
    ///
    /// This prevents illegal struct-by-value copies that cause Guru $80000004 on 68040
    /// when VBCC generates unsupported instructions for large struct copies.
    ///
    /// The algorithm:
    /// 1. Find all IrMemberAccess instructions that produce struct-typed results
    /// 2. Track all uses of those result variables
    /// 3. If all uses are IrBorrowValue (address-of) or IrCastValue wrapping IrBorrowValue,
    ///    mark that member access as "address-only" - no copy needed
    /// </summary>
    private void ScanForAddressOnlyStructMemberAccesses(IrFunction function)
    {
        // Map from result variable name to the IrMemberAccess that produced it
        var structMemberAccesses = new Dictionary<string, IrMemberAccess>();

        // Map from variable name to count of non-address-of uses
        var nonAddressUses = new Dictionary<string, int>();

        // First pass: collect all struct-typed member accesses
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrMemberAccess memberAccess &&
                    memberAccess.FieldType is IrStructType &&
                    !string.IsNullOrEmpty(memberAccess.ResultName))
                {
                    var resultName = SanitizeVariableName(memberAccess.ResultName);
                    structMemberAccesses[resultName] = memberAccess;
                    nonAddressUses[resultName] = 0;  // Start with zero non-address uses
                }
            }
        }

        if (structMemberAccesses.Count == 0)
            return;  // No struct member accesses to analyze

        // Second pass: scan for uses of those results
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                ScanInstructionForStructMemberUses(instruction, structMemberAccesses, nonAddressUses);
            }
        }

        // Mark all member accesses with zero non-address uses as "address-only"
        foreach (var (varName, count) in nonAddressUses)
        {
            if (count == 0)
            {
                _addressOnlyStructMemberAccess.Add(varName);
            }
        }
    }

    /// <summary>
    /// Pre-scans a function to collect all labels that are actually targeted by branches.
    /// Labels not in _reachableLabels will be skipped during emission to avoid VBCC warning 148
    /// (unused label).
    /// </summary>
    private void CollectReachableLabels(IrFunction function)
    {
        _reachableLabels.Clear();
        _emittedLabels.Clear();  // Also clear emitted labels for the new function

        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case IrBranch branch:
                        _reachableLabels.Add(branch.Target);
                        break;
                    case IrConditionalBranch condBranch:
                        _reachableLabels.Add(condBranch.TrueTarget);
                        if (!string.IsNullOrEmpty(condBranch.FalseTarget))
                        {
                            _reachableLabels.Add(condBranch.FalseTarget);
                        }
                        break;
                    case IrMatch match:
                        // Match expressions generate switch statements that branch to arm labels
                        foreach (var arm in match.Arms)
                        {
                            _reachableLabels.Add(arm.TargetLabel);
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Helper for ScanForAddressOnlyStructMemberAccesses - checks if an instruction uses
    /// a struct member access result in a non-address-of context.
    /// </summary>
    private void ScanInstructionForStructMemberUses(
        IrInstruction instruction,
        Dictionary<string, IrMemberAccess> structMemberAccesses,
        Dictionary<string, int> nonAddressUses)
    {
        // Check all values used by this instruction
        void CheckValue(IrValue value, bool isAddressContext)
        {
            switch (value)
            {
                case IrVariable variable:
                    var sanitizedName = SanitizeVariableName(variable.Name);
                    if (structMemberAccesses.ContainsKey(sanitizedName) && !isAddressContext)
                    {
                        // Non-address use of a struct member access result
                        nonAddressUses[sanitizedName]++;
                    }
                    break;

                case IrBorrowValue borrowValue:
                    // Address-of context - check the borrowed value with isAddressContext=true
                    CheckValue(borrowValue.BorrowedValue, isAddressContext: true);
                    break;

                case IrCastValue castValue:
                    // Casts that wrap borrows preserve the address context
                    // (e.g., (*BitMap)&(screen->BitMap) - the cast doesn't materialize the struct)
                    if (castValue.Value is IrBorrowValue)
                    {
                        CheckValue(castValue.Value, isAddressContext: true);
                    }
                    else
                    {
                        CheckValue(castValue.Value, isAddressContext: false);
                    }
                    break;

                case IrDereferenceValue derefValue:
                    CheckValue(derefValue.PointerValue, isAddressContext: false);
                    break;

                case IrStructLiteral structLit:
                    foreach (var fieldValue in structLit.FieldValues.Values)
                    {
                        CheckValue(fieldValue, isAddressContext: false);
                    }
                    break;

                case IrEnumValue enumValue:
                    foreach (var assocValue in enumValue.AssociatedValues)
                    {
                        CheckValue(assocValue, isAddressContext: false);
                    }
                    break;
            }
        }

        // Check values based on instruction type
        switch (instruction)
        {
            case IrStore store:
                CheckValue(store.Value, isAddressContext: false);
                break;

            case IrLocalDecl localDecl when localDecl.InitialValue != null:
                CheckValue(localDecl.InitialValue, isAddressContext: false);
                break;

            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    CheckValue(arg, isAddressContext: false);
                }
                break;

            case IrReturn ret when ret.Value != null:
                CheckValue(ret.Value, isAddressContext: false);
                break;

            case IrConditionalBranch condBranch:
                CheckValue(condBranch.Condition, isAddressContext: false);
                break;

            case IrBinaryOp binOp:
                CheckValue(binOp.Left, isAddressContext: false);
                CheckValue(binOp.Right, isAddressContext: false);
                break;

            case IrMemberStore memberStore:
                CheckValue(memberStore.Value, isAddressContext: false);
                break;

            case IrIndexStore indexStore:
                CheckValue(indexStore.Value, isAddressContext: false);
                CheckValue(indexStore.Index, isAddressContext: false);
                break;

            case IrDereferenceStore derefStore:
                CheckValue(derefStore.Value, isAddressContext: false);
                CheckValue(derefStore.Pointer, isAddressContext: false);
                break;

            case IrMemberAccess memberAccess:
                // CRITICAL: The Struct operand of a member access is a non-address use!
                // If we have (*screen).BitMap.Depth, then:
                // 1. First MemberAccess: Struct=(*screen), Field=BitMap, Result=temp1
                // 2. Second MemberAccess: Struct=temp1, Field=Depth, Result=temp2
                // temp1 is used by the second MemberAccess as its Struct operand,
                // so temp1 cannot be eliminated - we need its value to read Depth from it.
                CheckValue(memberAccess.Struct, isAddressContext: false);
                break;
        }
    }

    /// <summary>
    /// Check if a variable name matches a module static variable.
    /// Used to prevent local variables from shadowing module statics.
    /// </summary>
    private bool IsModuleStaticVariable(string varName)
    {
        var sanitizedName = SanitizeVariableName(varName);
        return _module.StaticVariables.Any(sv => SanitizeVariableName(sv.Name) == sanitizedName);
    }

    /// <summary>
    /// Determine the correct accessor (. or ->) for struct member access.
    /// Returns "->" if the struct is accessed through a pointer, "." otherwise.
    ///
    /// BUG FIX: The IR builder represents &self and &mut self parameters as IrPointerType
    /// and then dereferences them when accessing fields. This method detects that pattern
    /// and uses -> accessor to avoid emitting (*ptr).field, instead emitting ptr->field.
    /// </summary>
    internal string GetStructAccessor(IrValue structValue)
    {
        // Special case: if the struct value is a dereference (*ptr), then the original
        // value is a pointer, so we should use -> accessor directly without dereferencing
        // This handles cases like `self.field` where `self` is a pointer parameter
        if (structValue is IrDereferenceValue)
        {
            // The dereference will be handled by using -> instead of (*...).
            return "->";
        }

        // Check if the struct is accessed through a pointer
        // This includes:
        // 1. Reference types (&T, &mut T) - pointers in C
        // 2. Pointer types (used by IR builder for &self/&mut self parameters)
        // 3. Struct parameters containing heap data - passed by pointer
        if (structValue is IrVariable variable && _currentEmittingFunction != null)
        {
            var param = _currentEmittingFunction.Parameters.FirstOrDefault(p => p.Name == variable.Name);
            if (param != null)
            {
                // Check if this parameter is emitted as a pointer in the C signature
                // by checking the same conditions that GetCParameter uses
                if (param.Type is IrReferenceType or IrMutReferenceType or IrPointerType)
                {
                    return "->";
                }
                else if (param.Type is IrStructType structType && TypeContainsHeapData(structType))
                {
                    return "->";
                }
            }
        }

        // Default to . accessor for value types
        return ".";
    }

    private void EmitIndexAccess(IrIndexAccess indexAccess)
    {
        var arrayValue = EmitValue(indexAccess.Array);
        var indexValue = EmitValue(indexAccess.Index);
        var resultName = SanitizeVariableName(indexAccess.ResultName);
        var elementType = GetCType(indexAccess.ElementType);
        var alreadyDeclared = _declaredVariables.Contains(resultName);

        // Store the array and index expressions for later use when taking address
        _indexAccessInfo[resultName] = (arrayValue, indexValue);

        // Get source location if available
        var filePath = indexAccess.Location?.FilePath ?? "<unknown>";
        var line = indexAccess.Location?.Line ?? 0;

        // Add runtime bounds check if enabled and array type information is available
        if (_safetyLevel.EnableBoundsChecking() && indexAccess.Array.Type is IrArrayType arrayType)
        {
            // Emit conditional: if index is out of bounds, show error and return
            // Otherwise perform the actual array access
            if (!alreadyDeclared)
            {
                _output.AppendLine($"    {elementType} {resultName};");
            }
            _output.AppendLine($"    if ((uint32_t){indexValue} >= (uint32_t){arrayType.Length}) {{");

            _output.AppendLine($"        __novus_bounds_check_failed({indexValue}, {arrayType.Length}, \"{EscapeString(filePath)}\", {line});");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            EmitErrorPathReturn(2);  // Exit after bounds check failure
            _output.AppendLine($"    }} else {{");
            _output.AppendLine($"        {resultName} = {arrayValue}[{indexValue}];");
            _output.AppendLine($"    }}");
        }
        // Add use-after-free check for pointer access at safety level 3
        else if (_safetyLevel.EnableMemoryTracking() && indexAccess.Array.Type is IrPointerType)
        {
            // Emit UAF check before pointer dereference
            _output.AppendLine($"    __novus_check_ptr_access((void*){arrayValue}, \"{EscapeString(filePath)}\", {line});");

            // Then perform the access
            if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {arrayValue}[{indexValue}];");
            }
            else
            {
                _output.AppendLine($"    {elementType} {resultName} = {arrayValue}[{indexValue}];");
            }
        }
        else
        {
            // No checking - direct access
            if (alreadyDeclared)
            {
                _output.AppendLine($"    {resultName} = {arrayValue}[{indexValue}];");
            }
            else
            {
                _output.AppendLine($"    {elementType} {resultName} = {arrayValue}[{indexValue}];");
            }
        }
    }

    private void EmitIndexStore(IrIndexStore indexStore)
    {
        var arrayValue = EmitValue(indexStore.Array);
        var indexValue = EmitValue(indexStore.Index);
        var storeValue = EmitValue(indexStore.Value);

        // Add runtime bounds check if enabled and array type information is available
        if (_safetyLevel.EnableBoundsChecking() && indexStore.Array.Type is IrArrayType arrayType)
        {
            // Emit conditional: if index is out of bounds, show error and return
            // Otherwise perform the actual array store
            _output.AppendLine($"    if ((uint32_t){indexValue} >= (uint32_t){arrayType.Length}) {{");

            // Use instruction location if available, otherwise use placeholder
            // IrInstruction.Location is set during IR building from AST source locations
            var locInfo = indexStore.Location;
            var fileName = locInfo?.FilePath != null ? System.IO.Path.GetFileName(locInfo.FilePath) : "<compiler-generated>";
            var lineNum = locInfo?.Line ?? 0;
            _output.AppendLine($"        __novus_bounds_check_failed({indexValue}, {arrayType.Length}, \"{fileName}\", {lineNum});");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            EmitErrorPathReturn(2);  // Exit after bounds check failure
            _output.AppendLine($"    }}");
        }

        // VBCC FIX: If the value being stored is a pointer-converted parameter (struct passed by pointer),
        // we need to dereference it to get the actual struct value for the array store.
        // Also applies to stores of struct values that need memcpy for VBCC compatibility.
        var actualStoreValue = storeValue;
        if (_pointerConvertedParameters.Contains(storeValue))
        {
            // The parameter is a pointer to a struct, we need to dereference it
            actualStoreValue = $"*{storeValue}";
        }

        // For struct types, VBCC may have trouble with direct assignment.
        // Use memcpy for struct-to-struct array element assignment.
        if (indexStore.Value.Type is IrStructType valueStructType &&
            TypeContainsHeapData(valueStructType))
        {
            var cType = GetCType(valueStructType);
            _output.AppendLine($"    __novus_memcpy((uint8_t*)&{arrayValue}[{indexValue}], (uint8_t*)&{actualStoreValue}, sizeof({cType}));");
        }
        // VBCC 68K FIX: When storing a struct literal into an array, emit field-by-field assignment
        // instead of using compound literal array[i] = (Struct){ ... } which causes misalignment on 68040
        else if (indexStore.Value is IrStructLiteral structLitStore)
        {
            foreach (var kvp in structLitStore.FieldValues)
            {
                var fValue = EmitValue(kvp.Value);
                _output.AppendLine($"    {arrayValue}[{indexValue}].{kvp.Key} = {fValue};");
            }
        }
        else
        {
            _output.AppendLine($"    {arrayValue}[{indexValue}] = {actualStoreValue};");
        }
    }

    private void EmitIndexedFieldStore(IrIndexedFieldStore indexedFieldStore)
    {
        var arrayValue = EmitValue(indexedFieldStore.Array);
        var indexValue = EmitValue(indexedFieldStore.Index);
        var storeValue = EmitValue(indexedFieldStore.Value);

        // Emit direct assignment to array[index].field
        // This is the whole point of this instruction - avoid creating a temporary copy
        _output.AppendLine($"    {arrayValue}[{indexValue}].{indexedFieldStore.FieldName} = {storeValue};");
    }

    private void EmitMemberStore(IrMemberStore memberStore)
    {
        // VBCC_004: Compound literals cause misalignment on 68040 - emit field-by-field for struct literals
        if (memberStore.Value is IrStructLiteral structLit)
        {
            // Determine the correct accessor (. or ->)
            var litAccessor = GetStructAccessor(memberStore.Struct);

            // If the struct is a dereference and we're using ->, emit just the pointer value
            string litStructValue;
            if (memberStore.Struct is IrDereferenceValue litDerefValue && litAccessor == "->")
            {
                litStructValue = EmitValue(litDerefValue.PointerValue);
            }
            else
            {
                litStructValue = EmitValue(memberStore.Struct);
            }

            // Handle field access chain reconstruction
            if (memberStore.Struct is IrVariable litStructVar)
            {
                var sanitizedStructName = SanitizeVariableName(litStructVar.Name);
                if (_fieldAccessChainInfo.ContainsKey(sanitizedStructName))
                {
                    litStructValue = ReconstructFieldAccessChain(litStructVar.Name);
                    litAccessor = ".";
                }
            }

            // Emit null pointer check for pointer member store (using ->) if enabled
            if (litAccessor == "->" && _safetyLevel.EnableNullChecks())
            {
                var locInfo = memberStore.Location;
                var filePath = locInfo?.FilePath != null ? System.IO.Path.GetFileName(locInfo.FilePath) : "<compiler-generated>";
                var line = locInfo?.Line ?? 0;

                // VBCC WORKAROUND (VBCC_001): function call forces sequence point
                _output.AppendLine($"    if (__novus_is_null({litStructValue})) {{");
                _output.AppendLine($"        __novus_null_pointer_error(\"{EscapeString(filePath)}\", {line});");

                // Execute deferred cleanup before returning
                if (_currentEmittingFunction != null)
                {
                    EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
                }

                // Return from function after error
                EmitErrorPathReturn(2);  // indent level 2
                _output.AppendLine($"    }}");
            }

            // Emit field-by-field assignments instead of compound literal
            foreach (var kvp in structLit.FieldValues)
            {
                var fieldValue = EmitValue(kvp.Value);
                _output.AppendLine($"    {litStructValue}{litAccessor}{memberStore.FieldName}.{kvp.Key} = {fieldValue};");
            }
            return;
        }

        var storeValue = EmitValue(memberStore.Value);

        // Determine the correct accessor (. or ->)
        var accessor = GetStructAccessor(memberStore.Struct);

        // If the struct is a dereference and we're using ->, emit just the pointer value
        // instead of (*ptr)->field, emit ptr->field
        string structValue;
        if (memberStore.Struct is IrDereferenceValue derefValue && accessor == "->")
        {
            structValue = EmitValue(derefValue.PointerValue);
        }
        else
        {
            structValue = EmitValue(memberStore.Struct);
        }

        // CRITICAL FIX: Check if the structValue is a variable that came from a field access chain
        // If so, we need to reconstruct the full lvalue expression to avoid storing to a temporary copy.
        // Example: if we have _t1 = self->timereq and we're storing to _t1.tr_node,
        // we should emit self->timereq->tr_node instead of _t1->tr_node (which would modify a copy)
        if (memberStore.Struct is IrVariable structVar)
        {
            var sanitizedStructName = SanitizeVariableName(structVar.Name);
            if (_fieldAccessChainInfo.ContainsKey(sanitizedStructName))
            {
                // Reconstruct the full chain
                structValue = ReconstructFieldAccessChain(structVar.Name);
                // The reconstructed chain already includes the accessor from the previous access,
                // so we need to determine the new accessor for the field we're storing to
                // Since we loaded a struct value (not a pointer), we use "."
                accessor = ".";
            }
        }

        // Emit null pointer check for pointer member store (using ->) if enabled
        if (accessor == "->" && _safetyLevel.EnableNullChecks())
        {
            var locInfo = memberStore.Location;
            var filePath = locInfo?.FilePath != null ? System.IO.Path.GetFileName(locInfo.FilePath) : "<compiler-generated>";
            var line = locInfo?.Line ?? 0;

            // VBCC WORKAROUND (VBCC_001): function call forces sequence point
            _output.AppendLine($"    if (__novus_is_null({structValue})) {{");
            _output.AppendLine($"        __novus_null_pointer_error(\"{EscapeString(filePath)}\", {line});");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            // Return from function after error
            EmitErrorPathReturn(2);  // indent level 2
            _output.AppendLine($"    }}");
        }

        _output.AppendLine($"    {structValue}{accessor}{memberStore.FieldName} = {storeValue};");
    }

    private void EmitDereferenceStore(IrDereferenceStore derefStore)
    {
        var pointerValue = EmitValue(derefStore.Pointer);
        var storeValue = EmitValue(derefStore.Value);

        // Emit null pointer check if enabled (safety level >= 2)
        if (_safetyLevel.EnableNullChecks())
        {
            var locInfo = derefStore.Location;
            var filePath = locInfo?.FilePath != null ? System.IO.Path.GetFileName(locInfo.FilePath) : "<compiler-generated>";
            var line = locInfo?.Line ?? 0;

            // VBCC WORKAROUND (VBCC_001): function call forces sequence point
            _output.AppendLine($"    if (__novus_is_null({pointerValue})) {{");
            _output.AppendLine($"        __novus_null_pointer_error(\"{EscapeString(filePath)}\", {line});");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            // Return from function after error
            EmitErrorPathReturn(2);  // indent level 2
            _output.AppendLine($"    }}");
        }

        _output.AppendLine($"    (*{pointerValue}) = {storeValue};");
    }

    internal string EmitBorrowValue(IrBorrowValue borrowValue)
    {
        // Check if we're borrowing a pointer-converted parameter
        // If so, the parameter is already a pointer in C, so don't add &
        if (borrowValue.BorrowedValue is IrVariable variable &&
            _pointerConvertedParameters.Contains(variable.Name))
        {
            // Parameter is already a pointer in C (e.g., Str* s), so just use it directly
            return EmitValue(variable);
        }

        // Check if we're borrowing a variable that came from an index access
        // If so, reconstruct the lvalue expression (&array[index]) instead of taking address of temporary
        if (borrowValue.BorrowedValue is IrVariable indexVar)
        {
            // Sanitize the variable name to match how it was stored in the dictionary
            var sanitizedName = SanitizeVariableName(indexVar.Name);

            if (_indexAccessInfo.TryGetValue(sanitizedName, out var indexInfo))
            {
                var (arrayExpr, indexExpr) = indexInfo;
                return $"&{arrayExpr}[{indexExpr}]";
            }
        }

        // CRITICAL FIX: Check if we're borrowing a variable that came from a field access chain
        // If so, reconstruct the full lvalue expression (&base->field1.field2) instead of taking
        // address of the temporary copy
        if (borrowValue.BorrowedValue is IrVariable fieldVar)
        {
            var sanitizedName = SanitizeVariableName(fieldVar.Name);

            if (_fieldAccessChainInfo.ContainsKey(sanitizedName))
            {
                var fullChain = ReconstructFieldAccessChain(fieldVar.Name);
                return $"&({fullChain})";
            }
        }

        // VBCC 68K FIX: When borrowing a struct literal, emit a temporary variable with field-by-field
        // assignment instead of using compound literal &(Struct){ ... } which causes misalignment on 68040.
        // This is critical for passing Str literals to functions like StackFormatter_write_str.
        if (borrowValue.BorrowedValue is IrStructLiteral structLit)
        {
            var structType = structLit.Type as IrStructType;
            if (structType != null)
            {
                var tempVarName = $"_borrow_tmp_{_tempCounter++}";
                var cStructType = GetCType(structType);

                // Declare the temporary variable
                _output.AppendLine($"    {cStructType} {tempVarName};");

                // Assign fields individually to avoid compound literal
                foreach (var kvp in structLit.FieldValues)
                {
                    var fValue = EmitValue(kvp.Value);
                    _output.AppendLine($"    {tempVarName}.{kvp.Key} = {fValue};");
                }

                // Return address of the temp variable
                return $"&{tempVarName}";
            }
        }

        // Normal case: add & to create a pointer
        return $"&{EmitValue(borrowValue.BorrowedValue)}";
    }

    internal string EmitValue(IrValue value)
    {
        return value switch
        {
            IrConstant constant => EmitIntegerConstant(constant),
            IrSizeOf sizeOf => EmitSizeOf(sizeOf),
            IrBoolConstant boolConst => boolConst.Value ? "true" : "false",
            IrFloatConstant floatConst => EmitFloatConstant(floatConst),
            IrFixedConstant fixedConst => EmitFixedConstant(fixedConst),
            IrVariable variable => EmitVariable(variable),
            IrGlobalVariable globalVar => globalVar.Name,  // Global variables use their name directly
            IrStringLiteral stringLit => $"(uint8_t*){stringLit.Label}",  // Just a pointer to null-terminated string data
            IrEnumValue enumValue => EmitEnumValue(enumValue),
            IrEnumConstructor enumCtor => EmitEnumConstructor(enumCtor),
            IrBorrowValue borrowValue => EmitBorrowValue(borrowValue),
            IrDereferenceValue derefValue => $"(*{EmitValue(derefValue.PointerValue)})",
            IrCastValue castValue => EmitCastValue(castValue),
            IrStructLiteral structLit => EmitStructLiteral(structLit),
            IrTupleLiteral tupleLit => EmitTupleLiteral(tupleLit),
            IrTupleElementAccess tupleAccess => EmitTupleElementAccess(tupleAccess),
            IrArrayLiteral arrayLit => EmitArrayLiteral(arrayLit),
            IrFunctionAddress funcAddr => funcAddr.FunctionName,  // Function name IS its address in C
            IrFunctionRef funcRef => funcRef.Function.Name,  // Function reference - emit function name
            IrFieldReference fieldRef => EmitFieldReference(fieldRef),  // Field reference for borrowing
            IrIndexedFieldAccess indexedField => EmitIndexedFieldAccess(indexedField),
            IrGenericAssociatedFunction genericFunc => throw new InvalidOperationException($"Generic associated function '{genericFunc.TypeName}::{genericFunc.MethodName}' must be monomorphized to a concrete function before code generation"),
            IrNever _ => "/* unreachable */",  // Never type - code is unreachable after panic
            _ => throw new NotSupportedException($"Unsupported value type: {value.GetType().Name}")
        };
    }

    internal string EmitVariable(IrVariable variable)
    {
        // Check if this variable is actually a constant reference
        // If so, inline the constant value instead of emitting the variable name
        if (_module.Constants.TryGetValue(variable.Name, out var constant))
        {
            return constant.Value.ToString() ?? "0";
        }

        // Fallback: Check for well-known Amiga system constants
        // This handles generic instantiation where constants from imported modules aren't available
        var wellKnownConstants = new Dictionary<string, long>
        {
            // Exec memory allocation flags (from exec.library)
            ["MEMF_PUBLIC"] = 0,
            ["MEMF_CHIP"] = 2,
            ["MEMF_FAST"] = 4,
            ["MEMF_CLEAR"] = 65536,
        };

        if (wellKnownConstants.TryGetValue(variable.Name, out var value))
        {
            return value.ToString();
        }

        // Not a constant - emit the variable name
        return SanitizeVariableName(variable.Name);
    }

    internal string EmitStructLiteral(IrStructLiteral structLit)
    {
        var structType = structLit.Type as IrStructType;
        if (structType == null)
            throw new InvalidOperationException("StructLiteral must have IrStructType");

        var typeName = GetCType(structType);
        var fields = structLit.FieldValues
            .Select(kvp => $".{kvp.Key} = {EmitValue(kvp.Value)}")
            .ToList();

        return $"({typeName}){{ {string.Join(", ", fields)} }}";
    }

    /// <summary>
    /// Emit struct literal for initializer context (static variables, field initializers).
    /// VBCC doesn't support compound literals `(Type){ ... }` in initializers, so we
    /// emit just `{ .field = value }` without the type cast.
    /// </summary>
    internal string EmitStructLiteralForInitializer(IrStructLiteral structLit)
    {
        var structType = structLit.Type as IrStructType;
        if (structType == null)
            throw new InvalidOperationException("StructLiteral must have IrStructType");

        var fields = structLit.FieldValues
            .Select(kvp => $".{kvp.Key} = {EmitValueForInitializer(kvp.Value)}")
            .ToList();

        return $"{{ {string.Join(", ", fields)} }}";
    }

    /// <summary>
    /// Emit value for initializer context. For struct/array literals, omits the type cast.
    /// </summary>
    internal string EmitValueForInitializer(IrValue value)
    {
        return value switch
        {
            IrStructLiteral structLit => EmitStructLiteralForInitializer(structLit),
            IrArrayLiteral arrayLit => EmitArrayLiteralForInitializer(arrayLit),
            _ => EmitValue(value)  // Other values are emitted normally
        };
    }

    /// <summary>
    /// Emit array literal for initializer context. VBCC doesn't support compound literals.
    /// Optimizes zero-filled arrays to {0} for smaller binaries.
    /// </summary>
    internal string EmitArrayLiteralForInitializer(IrArrayLiteral arrayLit)
    {
        // OPTIMIZATION: If all elements are zero-equivalent, emit {0} instead of listing every element
        // C99 guarantees {0} initializes entire array to zero, producing much smaller .c files
        // and faster compilation for large arrays
        if (IsAllZeroArray(arrayLit))
        {
            return "{0}";
        }

        var elements = arrayLit.Elements
            .Select(elem => EmitValueForInitializer(elem))
            .ToList();

        return $"{{ {string.Join(", ", elements)} }}";
    }

    /// <summary>
    /// Check if an array literal consists entirely of zero values.
    /// Works recursively for nested arrays and structs.
    /// </summary>
    private bool IsAllZeroArray(IrArrayLiteral arrayLit)
    {
        foreach (var elem in arrayLit.Elements)
        {
            if (!IsZeroValue(elem))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Check if a value is zero-equivalent (can be omitted in {0} initialization).
    /// </summary>
    private bool IsZeroValue(IrValue value)
    {
        return value switch
        {
            // Integer zero (any bit width) or null pointer (stored as 0L)
            IrConstant constant => constant.Value == 0,

            // Float zero
            IrFloatConstant floatConst => floatConst.Value == 0.0,

            // Bool false
            IrBoolConstant boolConst => !boolConst.Value,

            // Enum value - check if tag is 0 (e.g., Priority::Low = 0)
            IrEnumValue enumValue => IsZeroEnumValue(enumValue),

            // Nested array - all elements must be zero
            IrArrayLiteral nestedArray => IsAllZeroArray(nestedArray),

            // Struct literal - all fields must be zero
            IrStructLiteral structLit => IsAllZeroStruct(structLit),

            _ => false
        };
    }

    /// <summary>
    /// Check if an enum value is zero-equivalent.
    /// For simple enums (no associated data), check if the tag is 0.
    /// For enums with associated data, check if tag is 0 AND all associated values are zero.
    /// </summary>
    private bool IsZeroEnumValue(IrEnumValue enumValue)
    {
        var enumType = enumValue.Type as IrEnumType;
        if (enumType == null)
            return false;

        // Find the variant
        var variant = enumType.Variants.FirstOrDefault(v => v.Name == enumValue.VariantName);
        if (variant == null)
            return false;

        // Check if tag is 0
        if (variant.Tag != 0)
            return false;

        // If no associated data, it's zero
        if (!variant.HasAssociatedData || enumValue.AssociatedValues.Count == 0)
            return true;

        // Check if all associated values are zero
        foreach (var assocValue in enumValue.AssociatedValues)
        {
            if (!IsZeroValue(assocValue))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Check if a struct literal has all zero-valued fields.
    /// </summary>
    private bool IsAllZeroStruct(IrStructLiteral structLit)
    {
        foreach (var fieldValue in structLit.FieldValues.Values)
        {
            if (!IsZeroValue(fieldValue))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Check if an array literal contains only compile-time constant values.
    /// Arrays with only compile-time constants can be made 'static' to ensure
    /// they live for the entire program duration. This is critical when the
    /// array is passed to external functions (like MUI's make_radio) that
    /// store pointers to the array - if the array were stack-allocated,
    /// it would become invalid when the function returns.
    /// </summary>
    private bool IsCompileTimeConstantArray(IrArrayLiteral arrayLit)
    {
        foreach (var elem in arrayLit.Elements)
        {
            if (!IsCompileTimeConstant(elem))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Check if a value is a compile-time constant (can be placed in static storage).
    /// </summary>
    private bool IsCompileTimeConstant(IrValue value)
    {
        return value switch
        {
            // Integer constants (including null pointers represented as 0)
            IrConstant => true,

            // Float constants
            IrFloatConstant => true,

            // Bool constants
            IrBoolConstant => true,

            // Fixed-point constants
            IrFixedConstant => true,

            // String literals are in the data section, so their pointers are constant
            IrStringLiteral => true,

            // Cast of a compile-time constant is still constant
            IrCastValue castValue => IsCompileTimeConstant(castValue.Value),

            // Nested arrays - all elements must be constant
            IrArrayLiteral nestedArray => IsCompileTimeConstantArray(nestedArray),

            // Function addresses are compile-time constants
            IrFunctionAddress => true,

            // Variables are NOT compile-time constants (we need to trace through to their values)
            // However, we handle the special case of slot variables in AnalyzeStaticConstantArrays
            _ => false
        };
    }

    /// <summary>
    /// Analyzes a function to find arrays that are filled with compile-time constant values.
    /// This checks both:
    /// 1. IrLocalDecl instructions with IrArrayLiteral initial values
    /// 2. Arrays filled via IrIndexStore instructions
    ///
    /// Such arrays can be made 'static' to ensure they persist for the entire program duration.
    /// This is critical for arrays passed to external functions (like MUI's make_radio)
    /// that may store pointers to the array - without 'static', the array would be
    /// stack-allocated and become invalid when the function returns.
    /// </summary>
    private HashSet<string> AnalyzeStaticConstantArrays(
        IrFunction function,
        Dictionary<string, (IrType Type, bool IsArray, int ArraySize)> localDeclVars)
    {
        var result = new HashSet<string>();

        // Track which values are assigned to slot variables
        // Key: slot variable name (sanitized), Value: the value assigned to it
        var slotValues = new Dictionary<string, IrValue>();

        // Check arrays declared with IrArrayLiteral initial values
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLocalDecl localDecl &&
                    localDecl.Type is IrArrayType &&
                    localDecl.InitialValue is IrArrayLiteral arrayLiteral)
                {
                    var varName = SanitizeVariableName(localDecl.Name);

                    // Check if this array is in our local declarations
                    if (!localDeclVars.ContainsKey(varName))
                        continue;

                    // Check if all elements are compile-time constants
                    // For IrVariable elements that reference temps (%t), check if the
                    // array literal source expression elements were constants
                    bool allConstant = true;
                    int elemIdx = 0;
                    foreach (var elem in arrayLiteral.Elements)
                    {
                        var isConst = IsCompileTimeConstant(elem);  // First check direct constants
                        if (!isConst && elem is IrVariable varElem)
                        {
                            // For temp variables, we need to check if they were assigned constant values
                            // Unfortunately, the IR doesn't preserve this information.
                            // We'll check if the slot (after sanitization) would be assigned a constant
                            // by checking if ALL elements are temp variables that map to slots.
                            // This is conservative: we assume arrays of temps initialized with
                            // expressions like "string".ptr are constant because the string
                            // literals are in static storage.
                            var sanitizedElemName = SanitizeVariableName(varElem.Name);
                            // If this variable maps to a slot, check if it's a simple temp
                            if (sanitizedElemName.StartsWith("_slot_") && varElem.Name.StartsWith("%"))
                            {
                                // Temp variables that map to slots are typically results of
                                // constant expression evaluation (like "string".ptr)
                                // Check if the slot type is a pointer type (likely from string.ptr)
                                if (varElem.Type is IrPointerType)
                                {
                                    isConst = true; // Assume constant for pointer temps
                                }
                            }
                        }

                        if (!isConst)
                        {
                            allConstant = false;
                            break;
                        }
                        elemIdx++;
                    }

                    if (allConstant)
                    {
                        result.Add(varName);
                    }
                }
            }
        }

        // THIRD PASS: Check arrays filled via IrIndexStore instructions
        var arrayStores = new Dictionary<string, List<(int Index, IrValue Value)>>();
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrIndexStore indexStore)
                {
                    // Get the array variable name
                    string? arrayVarName = null;
                    if (indexStore.Array is IrVariable arrayVar)
                    {
                        arrayVarName = SanitizeVariableName(arrayVar.Name);
                    }

                    if (arrayVarName == null)
                        continue;

                    // Get the index value
                    if (indexStore.Index is not IrConstant indexConst)
                        continue; // Non-constant index - can't analyze

                    var index = (int)indexConst.Value;

                    // Check if this array is in our local declarations
                    if (!localDeclVars.ContainsKey(arrayVarName))
                        continue;

                    // Add to tracking
                    if (!arrayStores.ContainsKey(arrayVarName))
                        arrayStores[arrayVarName] = new List<(int, IrValue)>();

                    arrayStores[arrayVarName].Add((index, indexStore.Value));
                }
            }
        }

        // Analyze arrays filled via IrIndexStore
        foreach (var (arrayName, stores) in arrayStores)
        {
            // Skip if already marked as static from IrArrayLiteral
            if (result.Contains(arrayName))
                continue;

            if (!localDeclVars.TryGetValue(arrayName, out var varInfo))
                continue;

            if (!varInfo.IsArray)
                continue;

            var arraySize = varInfo.ArraySize;

            // Check if we have stores for all elements
            if (stores.Count != arraySize)
                continue; // Not all elements are explicitly set

            // Check if all stored values are compile-time constants
            bool allConstant = true;
            foreach (var (_, value) in stores)
            {
                if (!IsCompileTimeConstantValue(value, slotValues))
                {
                    allConstant = false;
                    break;
                }
            }

            if (allConstant)
            {
                result.Add(arrayName);
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if a value is a compile-time constant, following through slot variable
    /// assignments to find the actual value.
    /// </summary>
    private bool IsCompileTimeConstantValue(IrValue value, Dictionary<string, IrValue> slotValues)
    {
        // Direct constants
        if (IsCompileTimeConstant(value))
            return true;

        // Slot variable - follow through to the assigned value
        // Use sanitized name since slotValues keys are sanitized
        if (value is IrVariable varRef)
        {
            var sanitizedName = SanitizeVariableName(varRef.Name);
            if (sanitizedName.StartsWith("_slot_") && slotValues.TryGetValue(sanitizedName, out var slotValue))
            {
                return IsCompileTimeConstantValue(slotValue, slotValues);
            }
        }

        return false;
    }

    /// <summary>
    /// Estimate the size in bytes of a type for VBCC workaround decisions.
    /// This is a conservative estimate used to detect very large structs that
    /// VBCC cannot initialize properly.
    /// </summary>
    private int GetStructSizeEstimate(IrType type)
    {
        return type switch
        {
            IrBoolType => 1,
            IrIntType intType => intType.BitWidth / 8,  // 8, 16, 32, or 64 bits
            IrFloatType floatType => floatType.BitWidth / 8,  // 32 or 64 bits
            IrFixedType fixedType => fixedType.BitWidth / 8,  // 16 or 32 bits
            IrVoidType => 0,
            IrPointerType => 4,  // 32-bit pointer on Amiga
            IrReferenceType => 4,  // References are pointers
            IrMutReferenceType => 4,  // Mut references are pointers
            IrEnumType enumType => enumType.Variants.Any(v => v.HasAssociatedData) ? 8 : 4,  // Conservative estimate
            IrArrayType arrayType => GetStructSizeEstimate(arrayType.ElementType) * arrayType.Length,
            IrStructType structType => structType.Fields.Sum(f => GetStructSizeEstimate(f.Type)),
            _ => 4  // Default conservative estimate
        };
    }

    internal string EmitTupleLiteral(IrTupleLiteral tupleLit)
    {
        var tupleType = tupleLit.Type as IrTupleType;
        if (tupleType == null)
            throw new InvalidOperationException("TupleLiteral must have IrTupleType");

        // Unit type () - no value needed
        if (tupleType.ElementTypes.Count == 0)
            return "/* unit */";

        var typeName = GetCType(tupleType);
        var elements = tupleLit.Elements
            .Select((elem, idx) => $".__{idx} = {EmitValue(elem)}")
            .ToList();

        return $"({typeName}){{ {string.Join(", ", elements)} }}";
    }

    internal string EmitTupleElementAccess(IrTupleElementAccess tupleAccess)
    {
        var tupleValue = EmitValue(tupleAccess.Tuple);
        return $"({tupleValue}).__{tupleAccess.ElementIndex}";
    }

    internal string EmitArrayLiteral(IrArrayLiteral arrayLit)
    {
        var arrayType = arrayLit.Type as IrArrayType;
        if (arrayType == null)
            throw new InvalidOperationException("ArrayLiteral must have IrArrayType");

        // OPTIMIZATION: If all elements are zero-equivalent, emit {0}
        if (IsAllZeroArray(arrayLit))
        {
            return "{0}";
        }

        var elements = arrayLit.Elements
            .Select(elem => EmitValue(elem))
            .ToList();

        // Just emit the brace-enclosed elements without type cast
        // The variable declaration will provide the type
        return $"{{ {string.Join(", ", elements)} }}";
    }

    internal string EmitEnumValue(IrEnumValue enumValue)
    {
        var enumType = enumValue.Type as IrEnumType;
        if (enumType == null)
            throw new InvalidOperationException("EnumValue must have IrEnumType");

        var enumName = MangleName(enumType);

        // Check if this enum has any associated data
        bool hasAnyData = enumType.Variants.Any(v => v.HasAssociatedData);

        // OPTIMIZATION: For enums with no associated data, just emit the enum constant
        if (!hasAnyData)
        {
            return $"{enumName}_{enumValue.VariantName}";
        }

        // For enums WITH associated data, use compound literal
        var sb = new StringBuilder();
        sb.Append($"({enumName}){{ .tag = {enumName}_{enumValue.VariantName}");

        if (enumValue.AssociatedValues.Count > 0)
        {
            // Check if this is a unit type - single element that's an empty tuple
            bool isUnitType = enumValue.AssociatedValues.Count == 1 &&
                             enumValue.AssociatedValues[0].Type is IrTupleType tupleType &&
                             tupleType.ElementTypes.Count == 0;

            if (!isUnitType)
            {
                sb.Append($", .data = {{ .{enumValue.VariantName} = {{");
                for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
                {
                    var assocValue = EmitValue(enumValue.AssociatedValues[i]);
                    sb.Append($" ._{i} = {assocValue}");
                    if (i < enumValue.AssociatedValues.Count - 1)
                        sb.Append(",");
                }
                sb.Append(" } }");
            }
            else
            {
                // Unit type - initialize struct with dummy field set to 0
                sb.Append($", .data = {{ .{enumValue.VariantName} = {{ ._dummy = 0 }} }}");
            }
        }

        sb.Append(" }");
        return sb.ToString();
    }

    internal string EmitIntegerConstant(IrConstant constant)
    {
        // Emit integer constants with proper type suffix to avoid sign-extension issues
        // VBCC has a bug where negative i32 literals get sign-extended to 64-bit in assembly
        // Using explicit casts for proper 32-bit immediate operands

        var constantValue = constant.Value;

        // SPECIAL CASE: The literal 0 is used as NULL pointer in C
        // Emit it without a cast so C can implicitly convert it to any pointer type
        // This fixes crashes when passing 0 to functions expecting pointers (e.g., OpenWindowTagList)
        if (constantValue == 0)
        {
            return "0";
        }

        // SPECIAL CASE: Pointer-typed constants (e.g., (*u8)(-1) becomes IrConstant with pointer type)
        // These come from cast expressions like (*u8)(-1) which get optimized to constants
        if (constant.Type is IrPointerType ptrType)
        {
            var targetType = GetCType(constant.Type);
            // For -1, use NULL_PTR_SENTINEL which is defined as (void*)-1 in header
            // This avoids VBCC's optimizer converting the constant to a form that doesn't fit in moveq
            if (constantValue == -1)
            {
                return $"({targetType})NULL_PTR_SENTINEL";
            }
            var innerValue = constantValue.ToString();
            return $"({targetType}){innerValue}";
        }

        // Check the type to determine how to emit the constant
        var intType = constant.Type as IrIntType;
        if (intType != null)
        {
            // For 32-bit types, use explicit cast to prevent VBCC from treating
            // large/negative values as 64-bit immediates
            if (intType.BitWidth == 32)
            {
                if (intType.IsSigned)
                {
                    // Signed i32: cast to ensure 32-bit
                    var i32Value = unchecked((int)constantValue);
                    // Special case: INT32_MIN (-2147483648) cannot be written directly in C
                    // because 2147483648 doesn't fit in int, so it becomes long/long long.
                    // VBCC has a bug where even (-2147483647 - 1) gets optimized to a 64-bit
                    // immediate. Use hexadecimal to force 32-bit representation.
                    if (i32Value == int.MinValue)
                    {
                        return "(int32_t)0x80000000U";
                    }
                    return $"(int32_t){i32Value}";
                }
                else
                {
                    // Unsigned u32: cast and mask to 32 bits
                    var u32Value = unchecked((uint)constantValue);
                    return $"(uint32_t){u32Value}U";
                }
            }

            // For other integer sizes, add U suffix for unsigned types
            if (!intType.IsSigned)
            {
                var unsignedValue = unchecked((ulong)constantValue);
                return $"{unsignedValue}U";
            }
        }

        // For signed types or unknown types, emit as-is
        return constantValue.ToString();
    }

    internal string EmitSizeOf(IrSizeOf sizeOf)
    {
        // Compute size at compile time for known types
        // This is necessary because FFI types may not be available in generated C headers
        var size = CalculateTypeSize(sizeOf.TargetType);
        if (size > 0)
        {
            return size.ToString();
        }

        // Fallback to C's sizeof() for types we can't compute
        var typeName = GetCType(sizeOf.TargetType);
        return $"sizeof({typeName})";
    }

    /// <summary>
    /// Calculate the size of a type in bytes for 68k architecture.
    /// Uses 2-byte alignment for fields > 1 byte (typical 68k ABI).
    /// Returns 0 if the size cannot be determined or would overflow.
    /// </summary>
    private int CalculateTypeSize(IrType type)
    {
        return type switch
        {
            IrIntType it => it.BitWidth / 8,
            IrBoolType => 1,
            IrPointerType => 4,
            IrFloatType ft => ft.BitWidth / 8,
            IrFixedType fxt => fxt.BitWidth / 8,
            IrArrayType at => CalculateArrayTypeSize(at),
            IrStructType st => CalculateStructSize(st),
            IrEnumType et => CalculateEnumSize(et),
            _ => 0 // Unknown type, fallback to sizeof()
        };
    }

    /// <summary>
    /// Calculate array type size with overflow protection.
    /// Returns 0 if element size is unknown or multiplication would overflow.
    /// </summary>
    private int CalculateArrayTypeSize(IrArrayType arrayType)
    {
        var elemSize = CalculateTypeSize(arrayType.ElementType);
        if (elemSize == 0) return 0; // Unknown element type

        // Check for multiplication overflow
        // For 32-bit int: max safe multiplication is when both operands < 46341 (sqrt(int.MaxValue))
        // More precisely: check if elemSize * length would overflow
        if (arrayType.Length > 0 && elemSize > int.MaxValue / arrayType.Length)
        {
            // Overflow would occur - return 0 to fall back to sizeof()
            return 0;
        }

        return elemSize * arrayType.Length;
    }

    /// <summary>
    /// Calculate struct size with 68k alignment (2-byte for fields > 1 byte).
    /// </summary>
    private int CalculateStructSize(IrStructType structType)
    {
        int offset = 0;
        int maxAlignment = 1;

        foreach (var field in structType.Fields)
        {
            var fieldSize = CalculateTypeSize(field.Type);
            if (fieldSize == 0) return 0; // Can't compute

            // Get field alignment (min of field size and 2 for 68k)
            int fieldAlign = Math.Min(fieldSize, 2);
            if (fieldSize >= 4) fieldAlign = 2; // 68k typically uses 2-byte alignment

            maxAlignment = Math.Max(maxAlignment, fieldAlign);

            // Align offset
            if (fieldAlign > 1 && offset % fieldAlign != 0)
            {
                offset += fieldAlign - (offset % fieldAlign);
            }

            offset += fieldSize;
        }

        // Align struct to its maximum alignment
        if (maxAlignment > 1 && offset % maxAlignment != 0)
        {
            offset += maxAlignment - (offset % maxAlignment);
        }

        return offset;
    }

    /// <summary>
    /// Calculate enum size (tag + largest variant data).
    /// </summary>
    private int CalculateEnumSize(IrEnumType enumType)
    {
        int tagSize = 4; // u32 tag
        int maxDataSize = 0;

        foreach (var variant in enumType.Variants)
        {
            int variantSize = 0;
            if (variant.HasAssociatedData && variant.AssociatedData != null)
            {
                foreach (var dataType in variant.AssociatedData)
                {
                    var fieldSize = CalculateTypeSize(dataType);
                    if (fieldSize == 0) return 0; // Can't compute
                    variantSize += fieldSize;
                }
            }
            maxDataSize = Math.Max(maxDataSize, variantSize);
        }

        return tagSize + maxDataSize;
    }

    internal string EmitFloatConstant(IrFloatConstant floatConst)
    {
        // Emit float/double literals with appropriate suffix
        // f32 -> add 'f' suffix, f64 -> no suffix (default double)
        var floatType = floatConst.Type as IrFloatType;
        if (floatType == null)
            throw new InvalidOperationException("FloatConstant must have IrFloatType");

        return floatType.BitWidth == 32
            ? $"{floatConst.Value:G9}f"  // G9 preserves precision for float
            : $"{floatConst.Value:G17}"; // G17 preserves precision for double
    }

    internal string EmitFixedConstant(IrFixedConstant fixedConst)
    {
        // Convert fixed-point constant to integer representation
        // fixed16 = 8.8 format, fixed32 = 16.16 format (fractional bits = total bits / 2)
        var fixedType = fixedConst.Type as IrFixedType;
        if (fixedType == null)
            throw new InvalidOperationException("FixedConstant must have IrFixedType");

        // Calculate the integer representation: value * 2^fractional_bits
        // Fractional bits are half the total bit width
        int fractionalBits = fixedType.BitWidth / 2;
        long intValue = (long)(fixedConst.Value * (1 << fractionalBits));

        // Emit as integer cast to the appropriate fixed-point type
        return $"({GetCType(fixedType)}){intValue}";
    }

    internal string EmitEnumConstructor(IrEnumConstructor enumCtor)
    {
        // Enum constructor without arguments - just the variant tag
        // e.g., Option::None becomes Option_None
        var enumType = enumCtor.Type as IrEnumType;
        if (enumType == null)
            throw new InvalidOperationException("EnumConstructor must have IrEnumType");

        var enumName = MangleName(enumType);

        // Check if this enum has any associated data
        bool hasAnyData = enumType.Variants.Any(v => v.HasAssociatedData);

        if (!hasAnyData)
        {
            // Simple enum - just emit the constant name
            return $"{enumName}_{enumCtor.VariantName}";
        }
        else
        {
            // Tagged union - emit compound literal with just the tag
            return $"({enumName}){{ .tag = {enumName}_{enumCtor.VariantName} }}";
        }
    }

    internal string EmitCastValue(IrCastValue castValue)
    {
        // Get the target type in C syntax
        var targetType = GetCType(castValue.Type);

        // Special case: casting &indexVar where indexVar came from an index access
        // We want (cast)&array[index] not (cast)&tempVar
        if (castValue.Value is IrBorrowValue borrowValue &&
            borrowValue.BorrowedValue is IrVariable indexVar)
        {
            // Sanitize the variable name to match how it was stored in the dictionary
            var sanitizedName = SanitizeVariableName(indexVar.Name);

            if (_indexAccessInfo.TryGetValue(sanitizedName, out var indexInfo))
            {
                var (arrayExpr, indexExpr) = indexInfo;
                return $"({targetType})&{arrayExpr}[{indexExpr}]";
            }

            // Also check for field access chains
            if (_fieldAccessChainInfo.ContainsKey(sanitizedName))
            {
                var fullChain = ReconstructFieldAccessChain(indexVar.Name);
                return $"({targetType})&({fullChain})";
            }
        }

        // Recursively emit the inner value (handles nested casts)
        var innerValue = EmitValue(castValue.Value);

        // If inner value starts with a negative sign or contains operators, wrap in parentheses
        // This ensures (uint8_t*)-1 becomes (uint8_t*)(-1) and not (uint8_t*)-1 which can confuse the parser
        if (innerValue.StartsWith("-") || innerValue.Contains("+") || innerValue.Contains("*") || innerValue.Contains("/"))
        {
            innerValue = $"({innerValue})";
        }

        // Emit the cast: (target_type)inner_value
        return $"({targetType}){innerValue}";
    }

    internal string EmitFieldReference(IrFieldReference fieldRef)
    {
        // Emit a field reference as a field access expression (lvalue)
        // This is used when we need to take the address of a field without loading it first
        // Example: self.buffer -> self->buffer (when self is a pointer)

        var accessor = GetStructAccessor(fieldRef.Struct);

        // Emit the struct value and field access
        string structValue;
        if (fieldRef.Struct is IrDereferenceValue derefValue && accessor == "->")
        {
            // Optimization: (*ptr)->field becomes ptr->field
            structValue = EmitValue(derefValue.PointerValue);
        }
        else
        {
            structValue = EmitValue(fieldRef.Struct);
        }

        return $"{structValue}{accessor}{fieldRef.FieldName}";
    }

    internal string EmitIndexedFieldAccess(IrIndexedFieldAccess indexedField)
    {
        // Emit array[index].field access
        // Example: entries[i].nm_Type
        var arrayValue = EmitValue(indexedField.Array);
        var indexValue = EmitValue(indexedField.Index);
        return $"{arrayValue}[{indexValue}].{indexedField.FieldName}";
    }

    private int _tempCounter = 0;

    internal string SanitizeVariableName(string name)
    {
        // Apply slot mapping if available (for liveness-based variable reuse)
        // Check ORIGINAL name first since liveness analysis uses original IR names
        if (_variableToSlot != null && _variableToSlot.TryGetValue(name, out var slotName))
        {
            return slotName;
        }

        // Convert IR temp variable names (%t0, %t1, etc.) to valid C identifiers (_t0, _t1, etc.)
        if (name.StartsWith("%"))
        {
            return "_" + name.Substring(1);
        }

        return name;
    }

    internal string GetBinaryOperator(IrBinaryOp.OpKind operation)
    {
        return operation switch
        {
            IrBinaryOp.OpKind.Add => "+",
            IrBinaryOp.OpKind.Sub => "-",
            IrBinaryOp.OpKind.Mul => "*",
            IrBinaryOp.OpKind.Div => "/",
            IrBinaryOp.OpKind.Mod => "%",
            IrBinaryOp.OpKind.And => "&",
            IrBinaryOp.OpKind.Or => "|",
            IrBinaryOp.OpKind.Xor => "^",
            IrBinaryOp.OpKind.Shl => "<<",
            IrBinaryOp.OpKind.Shr => ">>",
            IrBinaryOp.OpKind.Eq => "==",
            IrBinaryOp.OpKind.Ne => "!=",
            IrBinaryOp.OpKind.Lt => "<",
            IrBinaryOp.OpKind.Le => "<=",
            IrBinaryOp.OpKind.Gt => ">",
            IrBinaryOp.OpKind.Ge => ">=",
            _ => throw new NotSupportedException($"Unsupported binary operation: {operation}")
        };
    }

    internal string GetCType(IrType type)
    {
        return type switch
        {
            IrIntType intType => intType.IsSigned
                ? intType.BitWidth switch
                {
                    8 => "int8_t",
                    16 => "int16_t",
                    32 => "int32_t",
                    64 => "int64_t",
                    _ => throw new NotSupportedException($"Unsupported int width: {intType.BitWidth}")
                }
                : intType.BitWidth switch
                {
                    8 => "uint8_t",
                    16 => "uint16_t",
                    32 => "uint32_t",
                    64 => "uint64_t",
                    _ => throw new NotSupportedException($"Unsupported uint width: {intType.BitWidth}")
                },
            IrBoolType => "bool",
            IrVoidType => "void",
            IrFloatType floatType => floatType.BitWidth == 32 ? "float" : "double",
            IrFixedType fixedType => fixedType.BitWidth == 16 ? "int16_t" : "int32_t",
            IrPointerType ptrType => $"{GetCType(ptrType.PointeeType)}*",
            IrEnumType enumType => MangleName(enumType),
            IrStructType structType => MangleName(structType),
            IrArrayType arrayType => $"{GetCType(arrayType.ElementType)}*",  // Arrays as pointers for now
            IrReferenceType refType => $"{GetCType(refType.PointeeType)}*",  // References as pointers
            IrMutReferenceType mutRefType => $"{GetCType(mutRefType.PointeeType)}*",  // Mut references as pointers
            IrFunctionPointerType fpType => GetFunctionPointerType(fpType),
            IrClosureType closureType => GetClosureTypeName(closureType),
            IrTupleType tupleType => GetTupleTypeName(tupleType),
            IrGenericType genericType => throw new InvalidOperationException($"Generic type parameter '{genericType.ParameterName}' in {(_currentEmittingStruct != null ? $"struct '{_currentEmittingStruct}'" : _currentEmittingFunction != null ? $"function '{_currentEmittingFunction.Name}'" : "unknown context")} was not substituted during monomorphization. Stack trace will show where this type is being used."),
            IrUnresolvedGenericType unresolvedGeneric => throw new InvalidOperationException($"Unresolved generic type '{unresolvedGeneric.Name}' must be monomorphized before code generation"),
            IrPartiallyResolvedGenericType partiallyResolved => throw new InvalidOperationException($"Partially resolved generic type must be fully monomorphized before code generation"),
            _ => throw new NotSupportedException($"Unsupported type: {type.GetType().Name}")
        };
    }

    internal string GetFunctionPointerType(IrFunctionPointerType fpType)
    {
        // Generate C function pointer type: return_type (*)(param1_type, param2_type, ...)
        var returnType = GetCType(fpType.ReturnType);
        var paramTypes = fpType.ParameterTypes.Count > 0
            ? string.Join(", ", fpType.ParameterTypes.Select(GetCType))
            : "void";
        return $"{returnType} (*)({paramTypes})";
    }

    /// <summary>
    /// Get the C struct name for a closure type (fat pointer: fn_ptr + env_ptr).
    /// Closures are represented as structs with two fields.
    /// </summary>
    internal string GetClosureTypeName(IrClosureType closureType)
    {
        // Generate a unique struct name based on the signature
        var paramTypes = closureType.ParameterTypes.Count > 0
            ? string.Join("_", closureType.ParameterTypes.Select(GetTypeNameForClosure))
            : "void";
        var returnType = GetTypeNameForClosure(closureType.ReturnType);
        return $"__Closure_{paramTypes}_{returnType}";
    }

    /// <summary>
    /// Get a simple type name for closure struct naming (alphanumeric only)
    /// </summary>
    private string GetTypeNameForClosure(IrType type)
    {
        return type switch
        {
            IrVoidType => "void",
            IrBoolType => "bool",
            IrIntType intType => intType.Name,
            IrFloatType floatType => floatType.BitWidth == 32 ? "f32" : "f64",
            IrPointerType ptrType => $"ptr_{GetTypeNameForClosure(ptrType.PointeeType)}",
            IrStructType structType => MangleName(structType).Replace("::", "_"),
            IrEnumType enumType => MangleName(enumType).Replace("::", "_"),
            _ => type.Name.Replace("*", "ptr").Replace(" ", "_").Replace("::", "_")
        };
    }

    /// <summary>
    /// Generate a C variable declaration, handling function pointer types correctly.
    /// Function pointer syntax in C requires the variable name inside parentheses:
    ///   int32_t (*callback)(int32_t, int32_t)  // correct
    ///   int32_t (*)(int32_t, int32_t) callback // incorrect
    ///
    /// For pointers to function pointers:
    ///   int32_t (**callback)(int32_t)  // correct (pointer to function pointer)
    ///   int32_t (*)(int32_t)* callback // incorrect
    /// </summary>
    /// <param name="type">The IR type of the variable</param>
    /// <param name="name">The variable name</param>
    /// <param name="initializer">Optional initializer expression (e.g., "= {0}" or "= 42")</param>
    internal string GetCVariableDeclaration(IrType type, string name, string? initializer = null)
    {
        // Check for function pointer (including through pointer indirections)
        var declaration = GetCDeclaratorForType(type, name);
        if (initializer != null)
        {
            declaration += $" {initializer}";
        }
        return declaration;
    }

    /// <summary>
    /// Build a C declarator for a type, handling function pointer syntax correctly.
    /// The declarator (name with pointer decorations) goes inside the function pointer parentheses.
    /// </summary>
    private string GetCDeclaratorForType(IrType type, string declarator)
    {
        switch (type)
        {
            case IrFunctionPointerType fpType:
            {
                // Function pointer: return_type (*declarator)(params)
                var returnType = GetCType(fpType.ReturnType);
                var paramTypes = fpType.ParameterTypes.Count > 0
                    ? string.Join(", ", fpType.ParameterTypes.Select(GetCType))
                    : "void";
                return $"{returnType} (*{declarator})({paramTypes})";
            }

            case IrPointerType ptrType when ContainsFunctionPointer(ptrType.PointeeType):
            {
                // Pointer to something containing a function pointer
                // Build up the declarator with * prefix, then recurse
                return GetCDeclaratorForType(ptrType.PointeeType, $"*{declarator}");
            }

            case IrReferenceType refType when ContainsFunctionPointer(refType.PointeeType):
            {
                // Reference to something containing a function pointer
                return GetCDeclaratorForType(refType.PointeeType, $"*{declarator}");
            }

            case IrMutReferenceType mutRefType when ContainsFunctionPointer(mutRefType.PointeeType):
            {
                // Mut reference to something containing a function pointer
                return GetCDeclaratorForType(mutRefType.PointeeType, $"*{declarator}");
            }

            default:
                // No function pointer involved, use simple syntax
                return $"{GetCType(type)} {declarator}";
        }
    }

    /// <summary>
    /// Check if a type contains a function pointer (directly or through pointer indirection).
    /// </summary>
    private bool ContainsFunctionPointer(IrType type)
    {
        return type switch
        {
            IrFunctionPointerType => true,
            IrPointerType ptrType => ContainsFunctionPointer(ptrType.PointeeType),
            IrReferenceType refType => ContainsFunctionPointer(refType.PointeeType),
            IrMutReferenceType mutRefType => ContainsFunctionPointer(mutRefType.PointeeType),
            _ => false
        };
    }

    internal string GetTupleTypeName(IrTupleType tupleType)
    {
        // Unit type () has no size and is never stored, so return void
        if (tupleType.ElementTypes.Count == 0)
            return "void";

        // Generate a mangled name for the tuple based on element types
        // Example: (u8, u8, u8) -> Tuple_u8_u8_u8
        var elementNames = tupleType.ElementTypes.Select(t => MangleTypeForTupleName(t));
        return $"Tuple_{string.Join("_", elementNames)}";
    }

    internal string MangleTypeForTupleName(IrType type)
    {
        return type switch
        {
            IrTupleType tupleType when tupleType.ElementTypes.Count == 0 => "unit",
            IrIntType intType => intType.IsSigned ? $"i{intType.BitWidth}" : $"u{intType.BitWidth}",
            IrBoolType => "bool",
            IrPointerType ptrType => $"ptr_{MangleTypeForTupleName(ptrType.PointeeType)}",
            IrStructType structType => SanitizeCTypeName(structType.CacheKey ?? structType.StructName),
            IrEnumType enumType => SanitizeCTypeName(enumType.CacheKey ?? enumType.EnumName),
            _ => SanitizeCTypeName(type.Name)
        };
    }

    /// <summary>
    /// Sanitize a type name for use in C identifiers.
    /// Replaces characters like angle brackets, commas, etc. with underscores.
    /// </summary>
    private string SanitizeCTypeName(string name)
    {
        return name.Replace("<", "_").Replace(">", "").Replace(",", "_").Replace(" ", "").Replace("(", "").Replace(")", "");
    }

    /// <summary>
    /// Check if a type contains heap-allocated data (pointers to heap memory).
    /// This is important for detecting types that should not be passed by value.
    /// </summary>
    private bool TypeContainsHeapData(IrType type)
    {
        switch (type)
        {
            case IrStructType structType:
                // Check if any field is a pointer or contains pointers
                return structType.Fields.Any(f =>
                    f.Type is IrPointerType || TypeContainsHeapData(f.Type));

            case IrPointerType:
                // Pointers themselves ARE heap data
                return true;

            case IrReferenceType:
            case IrMutReferenceType:
                // References are not considered heap data for this purpose
                // (they're already pointers and don't need special handling)
                return false;

            case IrArrayType arrayType:
                // Arrays might contain heap data in their elements
                return TypeContainsHeapData(arrayType.ElementType);

            default:
                // Primitives, enums without data, etc. don't contain heap data
                return false;
        }
    }

    /// <summary>
    /// Check if a type contains Drop-able content (including nested in enums like Option/Result).
    /// This is used to determine if move semantics (zeroing the source) is needed after copying.
    /// </summary>
    private bool TypeContainsDroppableContent(IrType type)
    {
        switch (type)
        {
            case IrStructType structType:
                // Check if this struct type implements Drop
                if (_module.TypeImplementsDrop(structType))
                    return true;

                // Check if any field contains Drop-able content
                return structType.Fields.Any(f => TypeContainsDroppableContent(f.Type));

            case IrEnumType enumType:
                // For enums like Option<T> and Result<T,E>, check if any variant's
                // associated data implements Drop or contains Drop-able content
                foreach (var variant in enumType.Variants)
                {
                    foreach (var dataType in variant.AssociatedData)
                    {
                        if (TypeContainsDroppableContent(dataType))
                            return true;
                    }
                }
                return false;

            case IrArrayType arrayType:
                // Arrays might contain Drop-able elements
                return TypeContainsDroppableContent(arrayType.ElementType);

            default:
                // Primitives, pointers, etc. don't need Drop
                return false;
        }
    }

    /// <summary>
    /// Check if a type requires memcpy instead of direct assignment for VBCC 68k.
    /// VBCC generates illegal instructions on 68040 for struct-by-value assignment.
    /// To be safe, we use memcpy for ALL struct and enum types, not just complex ones.
    /// This is conservative but ensures correctness on all 68k CPUs.
    /// </summary>
    private bool TypeRequiresMemcpy(IrType type)
    {
        switch (type)
        {
            case IrStructType:
                // ALL structs need memcpy - VBCC generates bad code even for simple structs
                // when they're extracted from unions or passed around
                return true;

            case IrEnumType enumType:
                // Enums with associated data become tagged unions in C.
                // VBCC generates illegal instructions when copying these struct-by-value.
                return enumType.Variants.Any(v => v.HasAssociatedData);

            case IrArrayType arrayType:
                // Arrays might contain structs
                return TypeRequiresMemcpy(arrayType.ElementType);

            default:
                // Primitives, pointers, etc. don't contain nested structs
                return false;
        }
    }

    internal string GetParameterList(IrFunction function, bool hasOutputParameter = false)
    {
        var parameters = new List<string>();

        // VBCC FIX: Add output parameter as first parameter for struct/enum returns
        if (hasOutputParameter)
        {
            var returnType = GetCType(function.ReturnType);
            parameters.Add($"{returnType}* __out");
        }

        // Add regular parameters
        parameters.AddRange(function.Parameters
            .Select(p => p.IsVariadic ? "..." : GetCParameter(p.Type, p.Name)));

        return parameters.Count > 0 ? string.Join(", ", parameters) : "void";
    }

    /// <summary>
    /// Generate a C parameter declaration, handling function pointers correctly.
    /// Function pointer syntax in C requires the parameter name inside parentheses:
    ///   int32_t (*callback)(int32_t, int32_t)  // correct
    ///   int32_t (*)(int32_t, int32_t) callback // incorrect
    ///
    /// BUG FIX: For structs containing heap data (pointers), pass by pointer instead
    /// of by value to avoid shallow copy issues and double-free bugs.
    /// </summary>
    internal string GetCParameter(IrType type, string name)
    {
        if (type is IrFunctionPointerType fpType)
        {
            // Special handling for function pointer parameters
            var returnType = GetCType(fpType.ReturnType);
            var paramTypes = fpType.ParameterTypes.Count > 0
                ? string.Join(", ", fpType.ParameterTypes.Select(GetCType))
                : "void";
            return $"{returnType} (*{name})({paramTypes})";
        }

        // BUG FIX: If this is a struct type (not already a pointer/reference) that contains
        // heap-allocated data, pass it by pointer to avoid shallow copy issues
        if (type is IrStructType structType &&
            TypeContainsHeapData(structType))
        {
            // Pass by pointer to enable move semantics
            var cType = GetCType(type);
            return $"{cType}* {name}";
        }

        // For all other types, use normal syntax
        return $"{GetCType(type)} {name}";
    }

    internal string MangleName(string name)
    {
        // Handle module separators and generic type parameters
        // Generic names like "From<DosError>" become "From_DosError"
        // Multiple type parameters like "HashMap<u32, u32>" become "HashMap_u32_u32"
        // Reference types like "Option<&u32>" become "Option_ref_u32"
        // Unit type like "Result<(), BlitterError>" becomes "Result_unit_BlitterError"
        return name.Replace("::", "_")
                   .Replace("()", "unit")    // Handle unit type before removing parens
                   .Replace("<", "_")
                   .Replace(">", "")
                   .Replace(",", "_")
                   .Replace("&", "ref_")
                   .Replace("*", "ptr_")
                   .Replace("(", "")         // Remove any remaining parens (e.g., from function types)
                   .Replace(")", "")
                   .Replace(" ", "");
    }

    /// <summary>
    /// Mangle a function name, including type parameters for generic functions.
    /// For generic functions like Vec::new() instantiated with Vec<bool>, this generates
    /// "Vec_bool_new" to avoid duplicate symbol errors during linking.
    /// </summary>
    public string MangleFunctionName(IrFunction function)
    {
        // Start with basic name mangling
        var baseName = MangleName(function.Name);

        // If not a monomorphized function, use base name as-is
        if (!IsMonomorphizedFunction(function))
            return baseName;

        // If the function name already contains type parameters (e.g., "Vec<u32>::new"),
        // then MangleName has already converted it to "Vec_u32_new" with the type args embedded.
        // In this case, we should NOT try to insert type args again.
        if (function.Name.Contains('<'))
            return baseName;

        // For monomorphized generic functions where the function name doesn't already
        // contain type args, we need to extract them from parameters or return type
        // and include them in the mangled name to avoid duplicate symbols.
        //
        // Strategy: Look at the function's parameters or return type to find
        // the concrete generic type being used.
        //
        // For example:
        //   Vec::new() with Vec<bool> has return type Vec_bool
        //   Vec::push(self: &mut Vec<bool>, value: bool) has first param type Vec_bool*

        // Try to extract type suffix from parameter types
        foreach (var param in function.Parameters)
        {
            // Check if parameter is a generic struct type
            if (param.Type is IrStructType structType && structType.CacheKey != null)
            {
                // Extract the concrete type from the CacheKey
                // e.g., "Vec<bool>" -> "bool"
                var typeArgs = ExtractTypeArguments(structType.CacheKey);
                if (typeArgs.Count > 0)
                {
                    var typeSuffix = string.Join("_", typeArgs);
                    // Insert type args between struct name and method name
                    // "Vec_new" + "bool" -> "Vec_bool_new"
                    return InsertTypeArguments(baseName, structType.StructName, typeSuffix);
                }
            }
            // Check if parameter is a pointer to a generic struct
            else if (param.Type is IrPointerType ptrType && ptrType.PointeeType is IrStructType innerStruct && innerStruct.CacheKey != null)
            {
                var typeArgs = ExtractTypeArguments(innerStruct.CacheKey);
                if (typeArgs.Count > 0)
                {
                    var typeSuffix = string.Join("_", typeArgs);
                    return InsertTypeArguments(baseName, innerStruct.StructName, typeSuffix);
                }
            }
        }

        // Try to extract type suffix from return type
        if (function.ReturnType is IrStructType returnStruct && returnStruct.CacheKey != null)
        {
            var typeArgs = ExtractTypeArguments(returnStruct.CacheKey);
            if (typeArgs.Count > 0)
            {
                var typeSuffix = string.Join("_", typeArgs);
                return InsertTypeArguments(baseName, returnStruct.StructName, typeSuffix);
            }
        }

        // Fallback: use base name (shouldn't happen for properly monomorphized functions)
        return baseName;
    }

    /// <summary>
    /// Extract type arguments from a cache key like "Vec<bool>" -> ["bool"]
    /// or "HashMap<String, i32>" -> ["String", "i32"]
    /// </summary>
    private List<string> ExtractTypeArguments(string cacheKey)
    {
        var result = new List<string>();

        var startIdx = cacheKey.IndexOf('<');
        if (startIdx < 0)
            return result;

        var endIdx = cacheKey.LastIndexOf('>');
        if (endIdx < 0)
            return result;

        var typeArgsStr = cacheKey.Substring(startIdx + 1, endIdx - startIdx - 1);

        // Split by comma, handling nested generics
        var parts = SplitTypeArguments(typeArgsStr);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            // Sanitize type name for C identifier
            var sanitized = trimmed.Replace("::", "_")
                                   .Replace("<", "_")
                                   .Replace(">", "")
                                   .Replace(",", "")
                                   .Replace("*", "ptr_")
                                   .Replace(" ", "");
            result.Add(sanitized);
        }

        return result;
    }

    /// <summary>
    /// Split type arguments by comma, respecting nested generics.
    /// "String, i32" -> ["String", "i32"]
    /// "Vec<i32>, bool" -> ["Vec<i32>", "bool"]
    /// </summary>
    private List<string> SplitTypeArguments(string typeArgs)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in typeArgs)
        {
            if (ch == '<')
            {
                depth++;
                current.Append(ch);
            }
            else if (ch == '>')
            {
                depth--;
                current.Append(ch);
            }
            else if (ch == ',' && depth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }

    /// <summary>
    /// Insert type arguments between the struct name and method name.
    /// baseName: "Vec_new", structName: "Vec", typeSuffix: "bool" -> "Vec_bool_new"
    /// baseName: "Vec_push", structName: "Vec", typeSuffix: "bool" -> "Vec_bool_push"
    /// </summary>
    internal string InsertTypeArguments(string baseName, string structName, string typeSuffix)
    {
        // Handle both :: and _ separators
        var separator = baseName.Contains("::") ? "::" : "_";

        if (baseName.StartsWith(structName + separator))
        {
            // Replace "Vec_method" with "Vec_bool_method"
            return structName + "_" + typeSuffix + "_" + baseName.Substring((structName + separator).Length);
        }

        // Fallback: append to end
        return baseName + "_" + typeSuffix;
    }

    /// <summary>
    /// Private wrapper for internal use - calls the public MangleFunctionName method.
    /// </summary>
    public string MangleName(IrFunction function) => MangleFunctionName(function);

    internal string MangleName(IrEnumType enumType)
    {
        // Use CacheKey for monomorphized generics, otherwise use EnumName
        var name = enumType.CacheKey ?? enumType.EnumName;

        // Sanitize for C identifier:
        // Option<i32> -> Option_i32
        // Result<i32, *u8> -> Result_i32_ptr_u8
        // Result<(), IntuitionError> -> Result_unit_IntuitionError
        // Option<&u32> -> Option_ref_u32
        return name.Replace("::", "_")
                   .Replace("()", "unit")     // Handle unit type before removing parens
                   .Replace("<", "_")
                   .Replace(">", "")
                   .Replace(",", "")
                   .Replace("*", "ptr_")
                   .Replace("&", "ref_")      // Handle reference types
                   .Replace("(", "")          // Remove any remaining parens
                   .Replace(")", "")
                   .Replace(" ", "");
    }

    // Struct names that clash with AmigaOS NDK types - must be prefixed
    // Only applies to Novus user-defined structs (those with fields), not FFI structs (no fields)
    private static readonly HashSet<string> NdkClashingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Point",      // graphics/gfx.h: typedef struct tPoint { WORD x,y; } Point;
        "Rect",       // graphics/gfx.h: struct Rectangle (Rect is commonly used)
    };

    internal string MangleName(IrStructType structType)
    {
        // Use CacheKey for monomorphized generics, otherwise use Name
        var name = structType.CacheKey ?? structType.Name;

        // Sanitize for C identifier:
        // Vec<i32> -> Vec_i32
        // HashMap<String, i32> -> HashMap_String_i32
        var sanitized = name.Replace("::", "_")
                   .Replace("()", "unit")     // Handle unit type before removing parens
                   .Replace("<", "_")
                   .Replace(">", "")
                   .Replace(",", "")
                   .Replace("*", "ptr_")
                   .Replace("&", "ref_")      // Handle reference types
                   .Replace("(", "")          // Remove any remaining parens
                   .Replace(")", "")
                   .Replace(" ", "");

        // Prefix with nv_ if it clashes with NDK type names
        // Only for user-defined structs (those with fields), not FFI structs (opaque/no fields)
        if (structType.Fields.Count > 0 && NdkClashingNames.Contains(sanitized))
        {
            return "nv_" + sanitized;
        }

        return sanitized;
    }

    /// <summary>
    /// VBCC FIX: Transform goto-based for-loop patterns into natural C for-loops.
    /// This works around vbcc's stack allocation bugs with goto-based control flow.
    ///
    /// Pattern to transform:
    ///   uint32_t _for_idx_N = 0;
    ///   goto for_cond_N;
    /// for_cond_N:;
    ///   bool _tX = _for_idx_N < _for_len_N;
    ///   if (_tX) goto for_body_N;
    ///   goto for_end_N;
    /// for_body_N:;
    ///   ... body ...
    ///   uint32_t _tY = _for_idx_N + 1;
    ///   _for_idx_N = _tY;
    ///   goto for_cond_N;
    /// for_end_N:;
    ///
    /// Into:
    ///   for (uint32_t _for_idx_N = 0; _for_idx_N < _for_len_N; _for_idx_N++) {
    ///     ... body ...
    ///   }
    /// </summary>
    internal string TransformForLoopsForVbcc(string code)
    {
        // Use regex to find and transform for-loop patterns
        // This is a simple pattern matcher that looks for the specific structure emitted by VisitForInLoop

        var pattern = @"    (?<type>uint32_t) (?<idx>_for_idx_\d+) = 0;\s+" +
                     @"    goto (?<cond>for_cond_\d+);\s+" +
                     @"(?<cond_label>\k<cond>):;\s+" +
                     @"    bool _t\d+ = \k<idx> < (?<len>_for_len_\d+);\s+" +
                     @"    if \(_t\d+\) goto (?<body>for_body_\d+);\s+" +
                     @"    goto (?<end>for_end_\d+);\s+" +
                     @"(?<body_label>\k<body>):;\s+" +
                     @"(?<body_content>(?:(?!uint32_t _t\d+ = \k<idx> \+ 1;).)+)" +
                     @"    uint32_t _t\d+ = \k<idx> \+ 1;\s+" +
                     @"    \k<idx> = _t\d+;\s+" +
                     @"    goto \k<cond>;\s+" +
                     @"(?<end_label>\k<end>):;";

        var transformed = System.Text.RegularExpressions.Regex.Replace(code, pattern, match =>
        {
            var type = match.Groups["type"].Value;
            var idx = match.Groups["idx"].Value;
            var len = match.Groups["len"].Value;
            var end = match.Groups["end"].Value;
            var bodyContent = match.Groups["body_content"].Value.Trim();

            // Keep the end label after the for-loop for break statements
            return $"    for ({type} {idx} = 0; {idx} < {len}; {idx}++) {{\n{bodyContent}\n    }}\n{end}:;";
        }, System.Text.RegularExpressions.RegexOptions.Singleline);

        return transformed;
    }

    /// <summary>
    /// Collect all variable references from an IR instruction and add them to the dictionary.
    /// This is used to find variables that are referenced in defer blocks so they can be
    /// pre-declared at function scope.
    /// </summary>
    private void CollectVariableReferences(IrInstruction instruction, Dictionary<string, IrType> variables)
    {
        switch (instruction)
        {
            case IrCall call:
                // Collect variables from call arguments
                foreach (var arg in call.Arguments)
                {
                    CollectVariableReferencesFromValue(arg, variables);
                }
                break;

            case IrStore store:
                // Collect from the value being stored
                CollectVariableReferencesFromValue(store.Value, variables);
                break;

            case IrLocalDecl localDecl:
                // Collect from the initial value
                if (localDecl.InitialValue != null)
                {
                    CollectVariableReferencesFromValue(localDecl.InitialValue, variables);
                }
                break;

            case IrBinaryOp binaryOp:
                CollectVariableReferencesFromValue(binaryOp.Left, variables);
                CollectVariableReferencesFromValue(binaryOp.Right, variables);
                break;

            case IrReturn ret:
                if (ret.Value != null)
                {
                    CollectVariableReferencesFromValue(ret.Value, variables);
                }
                break;

            case IrConditionalBranch condBranch:
                CollectVariableReferencesFromValue(condBranch.Condition, variables);
                break;

            case IrMemberAccess memberAccess:
                // Member access is an instruction that produces a result
                // We need to collect from the struct being accessed
                CollectVariableReferencesFromValue(memberAccess.Struct, variables);
                break;

            case IrIndexAccess indexAccess:
                // Index access is an instruction
                CollectVariableReferencesFromValue(indexAccess.Array, variables);
                CollectVariableReferencesFromValue(indexAccess.Index, variables);
                break;

            // Add more cases as needed for other instruction types
        }
    }

    /// <summary>
    /// Collect variable references from an IR value.
    /// </summary>
    private void CollectVariableReferencesFromValue(IrValue value, Dictionary<string, IrType> variables)
    {
        switch (value)
        {
            case IrVariable variable:
                // Found a variable reference - add it if not already in the dictionary
                if (!variables.ContainsKey(variable.Name))
                {
                    variables[variable.Name] = variable.Type;
                }
                break;

            case IrBorrowValue borrow:
                // Recursively collect from the borrowed value
                CollectVariableReferencesFromValue(borrow.BorrowedValue, variables);
                break;

            case IrDereferenceValue deref:
                CollectVariableReferencesFromValue(deref.PointerValue, variables);
                break;

            case IrCastValue cast:
                CollectVariableReferencesFromValue(cast.Value, variables);
                break;

            case IrTupleElementAccess tupleAccess:
                CollectVariableReferencesFromValue(tupleAccess.Tuple, variables);
                break;

            case IrFieldReference fieldRef:
                CollectVariableReferencesFromValue(fieldRef.Struct, variables);
                break;

            // Constants, literals, etc. don't reference variables
            case IrConstant:
            case IrBoolConstant:
            case IrFloatConstant:
            case IrFixedConstant:
            case IrStringLiteral:
            case IrStructLiteral:
            case IrTupleLiteral:
            case IrFunctionAddress:
                break;
        }
    }
}
