using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// C code generator for Novus.
/// Generates C99 code from Novus IR that can be compiled with VBCC.
/// Target: AmigaOS 2.0+ (68020+) using +aos68k -c99
/// </summary>
public class CCodeGenerator
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

    // Track current function being emitted (for defer cleanup)
    private IrFunction? _currentEmittingFunction = null;

    // Track which function parameters were converted to pointers in the C signature
    // (due to TypeContainsHeapData) so we don't add & when passing them to other functions
    private HashSet<string> _pointerConvertedParameters = new();

    // Track member access instructions for move semantic analysis
    // Maps result variable name -> (source struct, field name, accessor, field type)
    private Dictionary<string, (string structValue, string fieldName, string accessor, IrStructType fieldType)> _memberAccessInfo = new();

    // Track index access instructions so we can reconstruct lvalue expressions when taking address
    // Maps result variable name -> (array expression, index expression)
    private Dictionary<string, (string arrayExpr, string indexExpr)> _indexAccessInfo = new();

    /// <summary>
    /// Determines if a function is a monomorphized generic function.
    /// Monomorphized functions should be emitted as 'static inline' to avoid duplicate symbols.
    /// </summary>
    private bool IsMonomorphizedFunction(IrFunction function)
    {
        // All monomorphized functions are created with Private visibility
        if (function.Visibility != Visibility.Private)
            return false;

        // Extern functions are never monomorphized
        if (function.IsExtern)
            return false;

        // Best detection: Check if function parameters or return type have a CacheKey
        // (indicating a monomorphized generic type)
        foreach (var param in function.Parameters)
        {
            if (HasMonomorphizedType(param.Type))
                return true;
        }

        if (HasMonomorphizedType(function.ReturnType))
            return true;

        // Check for naming patterns that indicate monomorphization:
        // 1. Enum methods: "Type::method_typeArgs" (e.g., "Option::FromPointer_u8")
        // 2. Struct methods: "Type_method" (e.g., "Vec_push")
        // 3. Generic functions: "function_typeArgs" (e.g., "identity_i32")

        var name = function.Name;

        // Pattern 1: Contains :: which indicates enum method with type args
        if (name.Contains("::"))
        {
            // Check if it has type args after the method name
            // Format: Type::method_typeArgs
            var parts = name.Split("::");
            if (parts.Length == 2)
            {
                var methodPart = parts[1];
                // If method part contains underscore, it likely has type args
                // This catches cases like "FromPointer_u8", "unwrap_u8"
                return methodPart.Contains("_");
            }
        }

        // Pattern 2: Struct method or generic function with type args
        // These use underscore separator: "Vec_push", "identity_i32"
        // We need to distinguish between:
        //   - Regular functions with underscores: "my_function"
        //   - Monomorphized functions: "Vec_push", "identity_i32"
        //
        // Heuristic: If the name has underscore and contains a known type suffix,
        // it's likely monomorphized. Known type suffixes: i8, i16, i32, i64, u8, u16, u32, u64,
        // bool, ptr_, String, etc.

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

        // Pattern 3: Check if name contains struct/enum type names followed by underscore
        // This catches "Vec_push", "Option_unwrap", etc.
        // We can check against registered struct/enum types
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
    /// </summary>
    private bool HasMonomorphizedType(IrType type)
    {
        if (type is IrStructType structType && structType.CacheKey != null)
            return true;
        if (type is IrEnumType enumType && enumType.CacheKey != null)
            return true;
        if (type is IrPointerType ptrType)
            return HasMonomorphizedType(ptrType.PointeeType);
        if (type is IrReferenceType refType)
            return HasMonomorphizedType(refType.PointeeType);
        return false;
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
    public static string GenerateSharedTypesHeader(TypeRegistry typeRegistry)
    {
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
        sb.AppendLine("#include <stdint.h>");
        sb.AppendLine("#include <stdbool.h>");
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

            foreach (var structType in sortedStructs)
            {
                codegen.EmitStructTypeToBuilder(sb, structType);
            }

            // Note: Vec_*_as_ptr functions are generated as regular functions in stdlib,
            // not as macros, due to VBCC optimizer bugs at -O1/-O2.
            // Use -O0 for now until VBCC is fixed.
        }

        // Enum types
        if (typeRegistry.EnumTypes.Any())
        {
            sb.AppendLine("// ============================================================================");
            sb.AppendLine("// Enum Types");
            sb.AppendLine("// ============================================================================");
            sb.AppendLine();

            foreach (var enumType in typeRegistry.EnumTypes.OrderBy(e => e.Name))
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

        sb.AppendLine("#endif // NOVUS_TYPES_H");

        return sb.ToString();
    }

    /// <summary>
    /// Generate a single-function C file for library modules.
    /// This enables the linker to pull in only the functions that are actually used.
    /// </summary>
    public string GenerateFunctionFile(IrFunction function)
    {
        // Skip functions that use BStr type directly (BStr itself is not exported in the types header)
        // This is fine because BStr is typically only used internally and not by user code
        var functionNameLower = function.Name.ToLower();
        if (functionNameLower.Contains("bstr::"))
        {
            Console.WriteLine($"WARNING: Skipping function file '{function.Name}' (uses BStr type not in shared header)");
            return $"// SKIPPED: Function '{function.Name}' uses BStr which is not exported\n";
        }

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
            // Fallback: include basic headers
            sb.AppendLine("#include <stdint.h>");
            sb.AppendLine("#include <stdbool.h>");
            sb.AppendLine();
        }

        // Include AmigaOS headers (same as monolithic generator)
        sb.AppendLine("#include <utility/tagitem.h>");
        sb.AppendLine("typedef struct TagItem TagItem;");
        sb.AppendLine();

        // Check for generic types (both enums and structs) in the function
        var enumTypes = CollectEnumTypesForFunction(function);
        Console.WriteLine($"DEBUG GenFunction({function.Name}): Found {enumTypes.Count} enum types: {string.Join(", ", enumTypes.Select(e => $"{e.Name}/{e.EnumName}"))}");
        foreach (var et in enumTypes)
        {
            Console.WriteLine($"  - {et.Name} (EnumName={et.EnumName}, GenericParams={et.GenericParameters.Count})");
            foreach (var v in et.Variants)
            {
                if (v.HasAssociatedData && v.AssociatedData != null)
                {
                    foreach (var ad in v.AssociatedData)
                    {
                        if (ad is IrStructType st)
                            Console.WriteLine($"      variant {v.Name} has struct {st.Name} (GenericParams={st.GenericParameters.Count}, CacheKey={st.CacheKey})");
                        else if (ad is IrEnumType et2)
                            Console.WriteLine($"      variant {v.Name} has enum {et2.Name} (GenericParams={et2.GenericParameters.Count}, CacheKey={et2.CacheKey})");
                    }
                }
            }
        }
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
            stubSb.AppendLine("#include <stdint.h>");
            stubSb.AppendLine("#include <stdbool.h>");
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
                    // VBCC FIX: Use output parameter for struct/enum returns EXCEPT for extern functions
                    // Extern functions use their actual C signatures (e.g., runtime functions return enums directly)
                    var isStructOrEnumReturn = funcObj.ReturnType is IrStructType or IrEnumType;
                    var shouldUseOutParam = isStructOrEnumReturn && !funcObj.IsExtern;
                    var returnTypeStr = shouldUseOutParam ? "void" : GetCType(funcObj.ReturnType);
                    var parameters = GetParameterList(funcObj, shouldUseOutParam);
                    sb.AppendLine($"extern {returnTypeStr} {MangleName(funcObj)}({parameters});");
                }
                else
                {
                    // Function not in current module - extract signature from call site
                    var returnTypeStr = GetCType(returnType);
                    var paramTypes = arguments.Select(arg => GetCType(arg.Type)).ToList();
                    var paramList = paramTypes.Count > 0 ? string.Join(", ", paramTypes) : "void";
                    sb.AppendLine($"extern {returnTypeStr} {MangleName(funcName)}({paramList});");
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
        _indexAccessInfo.Clear();

        // Track which parameters were converted to pointers in the C signature
        _pointerConvertedParameters.Clear();
        foreach (var param in function.Parameters)
        {
            if (param.Type is IrStructType structType && TypeContainsHeapData(structType))
            {
                _pointerConvertedParameters.Add(param.Name);
            }
        }

        // VBCC FIX: For struct/enum returns on 68k, use output parameter pattern
        var isStructOrEnumReturn = function.ReturnType is IrStructType or IrEnumType;
        var shouldUseOutParam = isStructOrEnumReturn;
        var returnType = shouldUseOutParam ? "void" : GetCType(function.ReturnType);
        var parameters = GetParameterList(function, shouldUseOutParam);
        var funcName = MangleName(function);

        // Special case: main must return 'int' for VBCC compatibility
        if (funcName == "main" && returnType == "int32_t")
        {
            returnType = "int";
        }

        // No need for 'static' modifier - monomorphized functions now have unique
        // type-parameterized names (e.g., Vec_bool_push, Vec_u8_push) to prevent
        // duplicate symbol errors during linking.
        targetBuilder.AppendLine($"{returnType} {funcName}({parameters}) {{");

        // Emit all basic blocks
        foreach (var block in function.BasicBlocks)
        {
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

        // Clear current function
        _currentEmittingFunction = null;
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
            // Fallback: include basic headers
            sb.AppendLine("#include <stdint.h>");
            sb.AppendLine("#include <stdbool.h>");
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
                    for (int i = 0; i < variant.AssociatedData.Count; i++)
                    {
                        var dataType = GetCType(variant.AssociatedData[i]);
                        sb.AppendLine($"        {dataType} _{i};");
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

        // Generate initial value
        var initialValue = EmitValue(staticVar.InitialValue);

        // Emit the declaration with initialization
        var keywordStr = !string.IsNullOrEmpty(constKeyword) ? constKeyword + " " : "";

        // Special handling for arrays - use array syntax instead of pointer syntax
        if (staticVar.Type is IrArrayType arrayType)
        {
            var elementType = GetCType(arrayType.ElementType);
            var size = arrayType.Length;
            sb.AppendLine($"{keywordStr}{elementType} {staticVar.Name}[{size}] = {initialValue};");
        }
        else
        {
            var cType = GetCType(staticVar.Type);
            sb.AppendLine($"{keywordStr}{cType} {staticVar.Name} = {initialValue};");
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
    private void EmitStructTypeToBuilder(StringBuilder sb, IrStructType structType)
    {
        var structName = MangleName(structType);

        sb.AppendLine($"// Struct: {structType.Name}");
        sb.AppendLine($"typedef struct {{");

        foreach (var field in structType.Fields)
        {
            // Special handling for array fields - need T[n] syntax, not T*
            if (field.Type is IrArrayType arrayType)
            {
                var elementType = GetCType(arrayType.ElementType);
                var size = arrayType.Length;
                sb.AppendLine($"    {elementType} {field.Name}[{size}];");
            }
            else
            {
                var fieldType = GetCType(field.Type);
                sb.AppendLine($"    {fieldType} {field.Name};");
            }
        }

        sb.AppendLine($"}} {structName};");
        sb.AppendLine();
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
    /// Recursively collect all enum types from a type, including those nested in structs, arrays, and enum variant associated data
    /// </summary>
    private void CollectEnumTypesFromType(IrType type, HashSet<IrEnumType> enumTypes)
    {
        switch (type)
        {
            case IrEnumType enumType:
                enumTypes.Add(enumType);

                // Also recursively scan enum variant associated data for nested enum types
                // This is crucial for types like Result<T, E> where E might be another enum
                foreach (var variant in enumType.Variants)
                {
                    if (variant.HasAssociatedData && variant.AssociatedData != null)
                    {
                        foreach (var dataType in variant.AssociatedData)
                        {
                            CollectEnumTypesFromType(dataType, enumTypes);
                        }
                    }
                }
                break;

            case IrArrayType arrayType:
                // Recursively check the element type
                CollectEnumTypesFromType(arrayType.ElementType, enumTypes);
                break;

            case IrStructType structType:
                // Recursively check all field types
                foreach (var field in structType.Fields)
                {
                    CollectEnumTypesFromType(field.Type, enumTypes);
                }
                break;

            case IrPointerType pointerType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(pointerType.PointeeType, enumTypes);
                break;

            case IrReferenceType refType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(refType.PointeeType, enumTypes);
                break;

            case IrMutReferenceType mutRefType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(mutRefType.PointeeType, enumTypes);
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
            Console.WriteLine($"DEBUG IsConcreteEnum: {enumType.Name} has {enumType.GenericParameters.Count} generic parameters - NOT CONCRETE");
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
                        Console.WriteLine($"DEBUG IsConcreteEnum: {enumType.Name} variant {variant.Name} contains generic type {typeName} - NOT CONCRETE");
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
                        if (dataType is IrEnumType dependentEnum && enumTypes.Contains(dependentEnum))
                        {
                            Visit(dependentEnum);
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
                // Find struct types in the field type
                if (field.Type is IrStructType dependentStruct)
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

                    // Also check for function pointers used as arguments
                    foreach (var arg in call.Arguments)
                    {
                        if (arg is IrFunctionAddress funcAddr)
                        {
                            var funcObj = _module.Functions.FirstOrDefault(f => f.Name == funcAddr.FunctionName);
                            if (funcObj != null && !called.ContainsKey(funcAddr.FunctionName))
                            {
                                called[funcAddr.FunctionName] = (funcObj.ReturnType, new List<IrValue>());
                            }
                        }
                        else if (arg is IrFunctionRef funcRef)
                        {
                            if (!called.ContainsKey(funcRef.Function.Name))
                            {
                                called[funcRef.Function.Name] = (funcRef.Function.ReturnType, new List<IrValue>());
                            }
                        }
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
                }
                else if (instruction is IrLocalDecl localDecl)
                {
                    // Check if local variable is initialized with a function address
                    if (localDecl.InitialValue is IrFunctionAddress funcAddr)
                    {
                        var funcObj = _module.Functions.FirstOrDefault(f => f.Name == funcAddr.FunctionName);
                        if (funcObj != null && !called.ContainsKey(funcAddr.FunctionName))
                        {
                            called[funcAddr.FunctionName] = (funcObj.ReturnType, new List<IrValue>());
                        }
                    }
                    else if (localDecl.InitialValue is IrFunctionRef funcRef)
                    {
                        if (!called.ContainsKey(funcRef.Function.Name))
                        {
                            called[funcRef.Function.Name] = (funcRef.Function.ReturnType, new List<IrValue>());
                        }
                    }
                }
                else if (instruction is IrStore store)
                {
                    // Check if storing a function address to a variable
                    if (store.Value is IrFunctionAddress funcAddr)
                    {
                        var funcObj = _module.Functions.FirstOrDefault(f => f.Name == funcAddr.FunctionName);
                        if (funcObj != null && !called.ContainsKey(funcAddr.FunctionName))
                        {
                            called[funcAddr.FunctionName] = (funcObj.ReturnType, new List<IrValue>());
                        }
                    }
                    else if (store.Value is IrFunctionRef funcRef)
                    {
                        if (!called.ContainsKey(funcRef.Function.Name))
                        {
                            called[funcRef.Function.Name] = (funcRef.Function.ReturnType, new List<IrValue>());
                        }
                    }
                }
                else if (instruction is IrReturn returnInst)
                {
                    // Check if returning a function pointer
                    if (returnInst.Value is IrFunctionAddress funcAddr)
                    {
                        var funcObj = _module.Functions.FirstOrDefault(f => f.Name == funcAddr.FunctionName);
                        if (funcObj != null && !called.ContainsKey(funcAddr.FunctionName))
                        {
                            called[funcAddr.FunctionName] = (funcObj.ReturnType, new List<IrValue>());
                        }
                    }
                    else if (returnInst.Value is IrFunctionRef funcRef)
                    {
                        if (!called.ContainsKey(funcRef.Function.Name))
                        {
                            called[funcRef.Function.Name] = (funcRef.Function.ReturnType, new List<IrValue>());
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

        // Generate library boilerplate if @library attribute is present
        Console.WriteLine($"DEBUG CCodeGenerator: libraryGen.IsLibrary = {libraryGen.IsLibrary}");
        if (libraryGen.IsLibrary)
        {
            Console.WriteLine("DEBUG: Generating library boilerplate...");
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

        // Include standard types needed for function signatures
        sb.AppendLine("#include <stdint.h>");
        sb.AppendLine("#include <stdbool.h>");
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
            _output.AppendLine("#include <stdint.h>");
            _output.AppendLine("#include <stdbool.h>");
            _output.AppendLine();
        }

        // Include AmigaOS headers for external structs
        // TagItem is defined in utility/tagitem.h
        _output.AppendLine("#include <utility/tagitem.h>");
        _output.AppendLine("typedef struct TagItem TagItem;");
        _output.AppendLine();

        // Don't emit proto headers - we'll use assembly stubs with i32 signatures
        // This avoids fighting VBCC's type system (BPTR, CONST_STRPTR, etc.)
        // Our assembly stubs handle the conversions from i32 to proper AmigaOS types
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

        // TODO: Add struct typedefs as needed
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
                for (int i = 0; i < variant.AssociatedData.Count; i++)
                {
                    var dataType = GetCType(variant.AssociatedData[i]);
                    _output.AppendLine($"        {dataType} _{i};");
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

    private string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
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
                }
                else if (instruction is IrDefer defer)
                {
                    // Recursively scan the deferred block
                    ScanBlockForCalls(defer.DeferredBlock);
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
                var isStructOrEnumReturn = returnType is IrStructType or IrEnumType;
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
            var isStructOrEnumReturn = function.ReturnType is IrStructType or IrEnumType;
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
        // Clear declared variables for this function
        _declaredVariables.Clear();
        _indexAccessInfo.Clear();

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

        // VBCC FIX: For struct/enum returns on 68k, use output parameter pattern
        var isStructOrEnumReturn = function.ReturnType is IrStructType or IrEnumType;
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

        // No need for 'static' modifier - monomorphized functions now have unique
        // type-parameterized names (e.g., Vec_bool_push, Vec_u8_push) to prevent
        // duplicate symbol errors during linking.
        _output.AppendLine($"{returnType} {funcName}({parameters}) {{");

        // Emit function body
        foreach (var block in function.BasicBlocks)
        {
            EmitBasicBlock(block);
        }

        // Close any pending match arm scope
        if (_inMatchArmScope)
        {
            _output.AppendLine("    }");
            _inMatchArmScope = false;
        }

        // Build CFG to check if all paths return
        var cfg = new ControlFlowGraph(function);
        var allPathsReturn = cfg.AllPathsReturn();

        // Emit deferred cleanup at end of function ONLY if not all paths return
        // (if all paths return, cleanup was already emitted before each return statement)
        if (!allPathsReturn)
        {
            EmitDeferredCleanup(function, 1);
        }

        _output.AppendLine("}");

        // Clear current function
        _currentEmittingFunction = null;
    }

    private void EmitBasicBlock(IrBasicBlock block)
    {
        foreach (var instruction in block.Instructions)
        {
            EmitInstruction(instruction);
        }
    }

    private void EmitInstruction(IrInstruction instruction)
    {
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

            case IrMemberStore memberStore:
                EmitMemberStore(memberStore);
                break;

            case IrDereferenceStore derefStore:
                EmitDereferenceStore(derefStore);
                break;

            case IrDefer defer:
                // IrDefer is just a marker - defer blocks are emitted at function exit points
                // We don't emit anything here
                break;

            default:
                _output.AppendLine($"    // TODO: {instruction.GetType().Name}");
                break;
        }
    }

    /// <summary>
    /// Emit deferred cleanup blocks in LIFO order (last registered, first executed).
    /// This is called before function exit points (return statements, end of function).
    /// </summary>
    private void EmitDeferredCleanup(IrFunction function, int indentLevel = 1)
    {
        if (function.DeferredBlocks.Count == 0)
            return;

        var indent = new string(' ', indentLevel * 4);

        // Execute deferred blocks in LIFO order (reverse order)
        for (int i = function.DeferredBlocks.Count - 1; i >= 0; i--)
        {
            var deferBlock = function.DeferredBlocks[i];

            // Emit a comment for debugging
            _output.AppendLine($"{indent}// Defer cleanup block {function.DeferredBlocks.Count - i}");

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

                // Re-emit with correct indentation
                var lines = emitted.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Strip existing indentation and apply our indentation
                    var trimmed = line.TrimStart();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        _output.AppendLine($"{indent}{trimmed}");
                    }
                }
            }
        }
    }

    private void EmitLocalDecl(IrLocalDecl localDecl)
    {
        var varName = SanitizeVariableName(localDecl.Name);
        var initValue = EmitValue(localDecl.InitialValue);

        // Check if this variable has already been declared in this function
        if (_declaredVariables.Contains(varName))
        {
            // Already declared - emit assignment only
            var cType = GetCType(localDecl.Type);
            var initType = GetCType(localDecl.InitialValue.Type);
            if (initType != cType)
            {
                _output.AppendLine($"    {varName} = ({cType}){initValue};");
            }
            else
            {
                _output.AppendLine($"    {varName} = {initValue};");
            }
        }
        else
        {
            // First declaration - emit with type
            // Special handling for array types with array literal initialization
            if (localDecl.Type is IrArrayType arrayType && localDecl.InitialValue is IrArrayLiteral)
            {
                var elementType = GetCType(arrayType.ElementType);
                var size = arrayType.Length;
                _output.AppendLine($"    {elementType} {varName}[{size}] = {initValue};");
            }
            else
            {
                var cType = GetCType(localDecl.Type);
                var initType = GetCType(localDecl.InitialValue.Type);
                if (initType != cType)
                {
                    _output.AppendLine($"    {cType} {varName} = ({cType}){initValue};");
                }
                else
                {
                    _output.AppendLine($"    {cType} {varName} = {initValue};");
                }
            }
            _declaredVariables.Add(varName);
        }
    }

    private void EmitStore(IrStore store)
    {
        var varName = SanitizeVariableName(store.VariableName);
        var value = EmitValue(store.Value);

        // TODO: Add cast support if needed for Store
        _output.AppendLine($"    {varName} = {value};");
    }

    private void EmitBinaryOp(IrBinaryOp binaryOp)
    {
        var cType = GetCType(binaryOp.Type);
        var resultName = SanitizeVariableName(binaryOp.ResultName);
        var left = EmitValue(binaryOp.Left);
        var right = EmitValue(binaryOp.Right);

        // Special handling for division/modulo with safety checks
        if (_safetyLevel.EnableDivisionByZeroChecks() &&
            (binaryOp.Operation == IrBinaryOp.OpKind.Div || binaryOp.Operation == IrBinaryOp.OpKind.Mod))
        {
            var op = GetBinaryOperator(binaryOp.Operation);

            // Emit conditional: if divisor is zero, show error and return safe value
            // Otherwise perform the actual division
            _output.AppendLine($"    {cType} {resultName};");
            _output.AppendLine($"    if ({right} == 0) {{");
            _output.AppendLine($"        __novus_div_check({right}, \"<compiler-generated>\", 0);");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            _output.AppendLine($"        return 1;  // Exit after division by zero error");
            _output.AppendLine($"    }} else {{");
            _output.AppendLine($"        {resultName} = {left} {op} {right};");
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
            _output.AppendLine($"    {cType} {resultName} = {left} {op} (({right}) & {mask});");
        }
        else
        {
            var op = GetBinaryOperator(binaryOp.Operation);
            _output.AppendLine($"    {cType} {resultName} = {left} {op} {right};");
        }
    }

    private void EmitCall(IrCall call)
    {
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
                if (arg.Type is IrPointerType ptrType &&
                    (ptrType.PointeeType is IrIntType or IrBoolType or IrFloatType) &&
                    !(paramType is IrPointerType))
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
                        argValue = $"&{argValue}";
                    }
                }
                // If this is an extern FFI function and the parameter expects i32 but we have a pointer, cast it
                else if (function.IsExtern && paramType is IrIntType intType && intType.BitWidth == 32 && arg is IrVariable variable)
                {
                    // Check if the variable is a pointer type
                    // For now, we'll add the cast unconditionally for pointers
                    argValue = $"(int32_t){argValue}";
                }
            }

            args.Add(argValue);
        }

        // VBCC FIX: For struct/enum returns on 68k, use output parameter pattern to avoid vbcc bugs
        // BUT: Extern functions use their actual C signatures and return values directly
        var isStructOrEnumReturn = call.ReturnType is IrStructType or IrEnumType;
        var shouldUseOutParam = isStructOrEnumReturn && (function == null || !function.IsExtern);

        // Don't mangle exported function names in calls
        // If we found the function, use its mangled name; otherwise fall back to string mangling
        var callFuncName = (function != null && function.IsExported) ? function.Name :
                          (function != null ? MangleName(function) : MangleName(call.FunctionName));

        if (shouldUseOutParam && call.ResultName != null)
        {
            // Use output parameter pattern: void func(Result* out, args...)
            var cType = GetCType(call.ReturnType);
            var resultName = SanitizeVariableName(call.ResultName);

            // Declare result variable
            _output.AppendLine($"    {cType} {resultName};");

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

            if (call.ResultName != null && call.ReturnType is not IrVoidType)
            {
                var cType = GetCType(call.ReturnType);
                var resultName = SanitizeVariableName(call.ResultName);
                _output.AppendLine($"    {cType} {resultName} = {callExpr};");
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
            var cType = GetCType(call.ReturnType);
            var resultName = SanitizeVariableName(call.ResultName);
            _output.AppendLine($"    {cType} {resultName} = {callExpr};");
        }
        else
        {
            _output.AppendLine($"    {callExpr};");
        }
    }

    private bool _inMatchArmScope = false;

    private void EmitLabel(IrLabel label)
    {
        // Close previous match arm scope if needed
        if (_inMatchArmScope && (label.Name.StartsWith("match_arm_") || label.Name.StartsWith("match_end_") || label.Name.StartsWith("match_check_")))
        {
            _output.AppendLine("    }");
            _inMatchArmScope = false;
        }

        // Emit label (unindented for C style)
        // Add empty statement to ensure valid C (labels must be followed by a statement)
        _output.AppendLine($"{label.Name}:;");

        // Open new scope for match arms
        if (label.Name.StartsWith("match_arm_"))
        {
            _output.AppendLine("    {");
            _inMatchArmScope = true;
        }
    }

    private void EmitBranch(IrBranch branch)
    {
        _output.AppendLine($"    goto {branch.Target};");
    }

    private void EmitConditionalBranch(IrConditionalBranch condBranch)
    {
        var condition = EmitValue(condBranch.Condition);
        _output.AppendLine($"    if ({condition}) goto {condBranch.TrueTarget};");
        if (!string.IsNullOrEmpty(condBranch.FalseTarget))
        {
            _output.AppendLine($"    goto {condBranch.FalseTarget};");
        }
    }

    private void EmitReturn(IrReturn returnInst)
    {
        // Emit deferred cleanup before returning
        if (_currentEmittingFunction != null)
        {
            EmitDeferredCleanup(_currentEmittingFunction, 1);
        }

        if (returnInst.Value != null)
        {
            // VBCC FIX: For struct/enum returns, write to output parameter instead of returning directly
            var isStructOrEnumReturn = _currentEmittingFunction != null &&
                                      (_currentEmittingFunction.ReturnType is IrStructType or IrEnumType);
            var shouldUseOutParam = isStructOrEnumReturn;

            if (shouldUseOutParam)
            {
                var value = EmitValue(returnInst.Value);
                _output.AppendLine($"    *__out = {value};");

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

                _output.AppendLine("    return;");
            }
            else
            {
                var value = EmitValue(returnInst.Value);
                _output.AppendLine($"    return {value};");
            }
        }
        else
        {
            _output.AppendLine("    return;");
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
        var fileName = assert.Location.FilePath;
        var line = assert.Location.Line;
        var col = assert.Location.Column;

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

        // Exit with error code
        _output.AppendLine("        return 1;  // Assert failed");
        _output.AppendLine("    }");
    }

    private void EmitPanic(IrPanic panic)
    {
        // Panic is NEVER elided (even in release mode) - it's for unrecoverable runtime errors

        // Call runtime panic handler to display error (uses EasyRequest on Amiga)
        var fileName = panic.Location.FilePath;
        var line = panic.Location.Line;
        var col = panic.Location.Column;

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

        // __novus_panic never returns, but for C semantics we add unreachable return
        // This helps the C compiler understand control flow
        _output.AppendLine("    return 1;  // Unreachable (panic never returns)");
    }

    private void EmitMatch(IrMatch match)
    {
        var matchValue = EmitValue(match.MatchValue);
        var enumType = match.MatchValue.Type as IrEnumType;

        if (enumType == null)
            throw new InvalidOperationException("Match expression must be on an enum type");

        var enumName = MangleName(enumType);

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
                        var dataType = GetCType(variant.AssociatedData[i]);
                        _output.AppendLine($"        {dataType} {boundVar} = {matchValue}.data.{variantPattern.VariantName}._{i};");
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
            _output.AppendLine($"        abort();");
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

        if (!hasAnyData)
        {
            // OPTIMIZATION: For plain enums, the value IS the tag
            _output.AppendLine($"    int32_t {resultName} = {enumValue};");
        }
        else
        {
            // For enums with associated data, extract the tag field
            _output.AppendLine($"    int32_t {resultName} = {enumValue}.tag;");
        }
    }

    private void EmitExtractVariantData(IrExtractVariantData extractData)
    {
        var enumValue = EmitValue(extractData.EnumValue);
        var resultName = SanitizeVariableName(extractData.ResultName);
        var enumType = extractData.EnumValue.Type as IrEnumType;

        if (enumType == null)
            throw new InvalidOperationException("ExtractVariantData must be on an enum type");

        var dataType = GetCType(extractData.DataType);

        // Extract data from the specific variant
        _output.AppendLine($"    {dataType} {resultName} = {enumValue}.data.{extractData.VariantName}._{extractData.DataIndex};");
    }

    private void EmitMemberAccess(IrMemberAccess memberAccess)
    {
        var resultName = SanitizeVariableName(memberAccess.ResultName);
        var fieldType = GetCType(memberAccess.FieldType);

        // Special handling for struct literals: VBCC doesn't support member access on compound literals
        // We need to create a temporary variable first
        if (memberAccess.Struct is IrStructLiteral)
        {
            var tempVarName = $"_str_tmp_{_tempCounter++}";
            var structType = GetCType(memberAccess.Struct.Type);
            var structValue = EmitValue(memberAccess.Struct);
            _output.AppendLine($"    {structType} {tempVarName} = {structValue};");
            _output.AppendLine($"    {fieldType} {resultName} = {tempVarName}.{memberAccess.FieldName};");
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

                        // For by-value parameters, accessing a field IS a move
                        // TODO: We need better analysis to distinguish between:
                        // - Field access for further use (e.g., passing &field to a function) - NOT a move
                        // - Field extraction for return/assignment (e.g., return self.field) - IS a move
                        // For now, we treat all field accesses from by-value params as non-moves
                        // to fix the immediate bug with &mut self methods
                        isMovingField = false;
                    }
                }
            }

            _output.AppendLine($"    {fieldType} {resultName} = {structValue}{accessor}{memberAccess.FieldName};");

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
    /// Determine the correct accessor (. or ->) for struct member access.
    /// Returns "->" if the struct is accessed through a pointer, "." otherwise.
    ///
    /// BUG FIX: The IR builder represents &self and &mut self parameters as IrPointerType
    /// and then dereferences them when accessing fields. This method detects that pattern
    /// and uses -> accessor to avoid emitting (*ptr).field, instead emitting ptr->field.
    /// </summary>
    private string GetStructAccessor(IrValue structValue)
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

        // Store the array and index expressions for later use when taking address
        _indexAccessInfo[resultName] = (arrayValue, indexValue);

        // Add runtime bounds check if enabled and array type information is available
        if (_safetyLevel.EnableBoundsChecking() && indexAccess.Array.Type is IrArrayType arrayType)
        {
            // Emit conditional: if index is out of bounds, show error and return
            // Otherwise perform the actual array access
            _output.AppendLine($"    {elementType} {resultName};");
            _output.AppendLine($"    if ((uint32_t){indexValue} >= (uint32_t){arrayType.Length}) {{");

            // Note: We don't have location info on IR instructions yet, so we use 0
            // TODO: Add location tracking to IR instructions
            _output.AppendLine($"        __novus_bounds_check_failed({indexValue}, {arrayType.Length}, \"<compiler-generated>\", 0);");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            _output.AppendLine($"        return 0;  // Exit after bounds check failure");
            _output.AppendLine($"    }} else {{");
            _output.AppendLine($"        {resultName} = {arrayValue}[{indexValue}];");
            _output.AppendLine($"    }}");
        }
        else
        {
            // No bounds checking - direct access
            _output.AppendLine($"    {elementType} {resultName} = {arrayValue}[{indexValue}];");
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

            // Note: We don't have location info on IR instructions yet, so we use 0
            // TODO: Add location tracking to IR instructions
            _output.AppendLine($"        __novus_bounds_check_failed({indexValue}, {arrayType.Length}, \"<compiler-generated>\", 0);");

            // Execute deferred cleanup before returning
            if (_currentEmittingFunction != null)
            {
                EmitDeferredCleanup(_currentEmittingFunction, 2); // indent level 2
            }

            _output.AppendLine($"        return 0;  // Exit after bounds check failure");
            _output.AppendLine($"    }}");
        }

        _output.AppendLine($"    {arrayValue}[{indexValue}] = {storeValue};");
    }

    private void EmitMemberStore(IrMemberStore memberStore)
    {
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

        _output.AppendLine($"    {structValue}{accessor}{memberStore.FieldName} = {storeValue};");
    }

    private void EmitDereferenceStore(IrDereferenceStore derefStore)
    {
        var pointerValue = EmitValue(derefStore.Pointer);
        var storeValue = EmitValue(derefStore.Value);

        _output.AppendLine($"    (*{pointerValue}) = {storeValue};");
    }

    private string EmitBorrowValue(IrBorrowValue borrowValue)
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
        if (borrowValue.BorrowedValue is IrVariable indexVar &&
            _indexAccessInfo.TryGetValue(indexVar.Name, out var indexInfo))
        {
            var (arrayExpr, indexExpr) = indexInfo;
            return $"&{arrayExpr}[{indexExpr}]";
        }

        // Normal case: add & to create a pointer
        return $"&{EmitValue(borrowValue.BorrowedValue)}";
    }

    private string EmitValue(IrValue value)
    {
        return value switch
        {
            IrConstant constant => constant.Value.ToString(),
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
            IrArrayLiteral arrayLit => EmitArrayLiteral(arrayLit),
            IrFunctionAddress funcAddr => funcAddr.FunctionName,  // Function name IS its address in C
            IrFunctionRef funcRef => funcRef.Function.Name,  // Function reference - emit function name
            IrFieldReference fieldRef => EmitFieldReference(fieldRef),  // Field reference for borrowing
            IrGenericAssociatedFunction genericFunc => throw new InvalidOperationException($"Generic associated function '{genericFunc.TypeName}::{genericFunc.MethodName}' must be monomorphized to a concrete function before code generation"),
            _ => throw new NotSupportedException($"Unsupported value type: {value.GetType().Name}")
        };
    }

    private string EmitVariable(IrVariable variable)
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

    private string EmitStructLiteral(IrStructLiteral structLit)
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

    private string EmitArrayLiteral(IrArrayLiteral arrayLit)
    {
        var arrayType = arrayLit.Type as IrArrayType;
        if (arrayType == null)
            throw new InvalidOperationException("ArrayLiteral must have IrArrayType");

        var elements = arrayLit.Elements
            .Select(elem => EmitValue(elem))
            .ToList();

        // Just emit the brace-enclosed elements without type cast
        // The variable declaration will provide the type
        return $"{{ {string.Join(", ", elements)} }}";
    }

    private string EmitEnumValue(IrEnumValue enumValue)
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

        sb.Append(" }");
        return sb.ToString();
    }

    private string EmitFloatConstant(IrFloatConstant floatConst)
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

    private string EmitFixedConstant(IrFixedConstant fixedConst)
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

    private string EmitEnumConstructor(IrEnumConstructor enumCtor)
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

    private string EmitCastValue(IrCastValue castValue)
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
        }

        // Recursively emit the inner value (handles nested casts)
        var innerValue = EmitValue(castValue.Value);

        // Emit the cast: (target_type)inner_value
        return $"({targetType}){innerValue}";
    }

    private string EmitFieldReference(IrFieldReference fieldRef)
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

    private int _tempCounter = 0;

    private string SanitizeVariableName(string name)
    {
        // Convert IR temp variable names (%t0, %t1, etc.) to valid C identifiers (_t0, _t1, etc.)
        if (name.StartsWith("%"))
        {
            return "_" + name.Substring(1);
        }
        return name;
    }

    private string GetBinaryOperator(IrBinaryOp.OpKind operation)
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

    private string GetCType(IrType type)
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
            IrUnresolvedGenericType unresolvedGeneric => throw new InvalidOperationException($"Unresolved generic type '{unresolvedGeneric.Name}' must be monomorphized before code generation"),
            IrPartiallyResolvedGenericType partiallyResolved => throw new InvalidOperationException($"Partially resolved generic type must be fully monomorphized before code generation"),
            _ => throw new NotSupportedException($"Unsupported type: {type.GetType().Name}")
        };
    }

    private string GetFunctionPointerType(IrFunctionPointerType fpType)
    {
        // Generate C function pointer type: return_type (*)(param1_type, param2_type, ...)
        var returnType = GetCType(fpType.ReturnType);
        var paramTypes = fpType.ParameterTypes.Count > 0
            ? string.Join(", ", fpType.ParameterTypes.Select(GetCType))
            : "void";
        return $"{returnType} (*)({paramTypes})";
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

    private string GetParameterList(IrFunction function, bool hasOutputParameter = false)
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
    private string GetCParameter(IrType type, string name)
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

    private string MangleName(string name)
    {
        // For now, keep names simple
        // TODO: Handle generic instantiations, modules, etc.
        return name.Replace("::", "_");
    }

    /// <summary>
    /// Mangle a function name, including type parameters for generic functions.
    /// For generic functions like Vec::new() instantiated with Vec<bool>, this generates
    /// "Vec_bool_new" to avoid duplicate symbol errors during linking.
    /// </summary>
    private string MangleName(IrFunction function)
    {
        // Start with basic name mangling
        var baseName = MangleName(function.Name);

        // If not a monomorphized function, use base name as-is
        if (!IsMonomorphizedFunction(function))
            return baseName;

        // For monomorphized generic functions, we need to extract the type arguments
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
    private string InsertTypeArguments(string baseName, string structName, string typeSuffix)
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

    private string MangleName(IrEnumType enumType)
    {
        // Use CacheKey for monomorphized generics, otherwise use EnumName
        var name = enumType.CacheKey ?? enumType.EnumName;

        // Sanitize for C identifier:
        // Option<i32> -> Option_i32
        // Result<i32, *u8> -> Result_i32_ptr_u8
        return name.Replace("::", "_")
                   .Replace("<", "_")
                   .Replace(">", "")
                   .Replace(",", "")
                   .Replace("*", "ptr_")
                   .Replace(" ", "");
    }

    private string MangleName(IrStructType structType)
    {
        // Use CacheKey for monomorphized generics, otherwise use Name
        var name = structType.CacheKey ?? structType.Name;

        // Sanitize for C identifier:
        // Vec<i32> -> Vec_i32
        // HashMap<String, i32> -> HashMap_String_i32
        return name.Replace("::", "_")
                   .Replace("<", "_")
                   .Replace(">", "")
                   .Replace(",", "")
                   .Replace("*", "ptr_")
                   .Replace(" ", "");
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
    private string TransformForLoopsForVbcc(string code)
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
}
