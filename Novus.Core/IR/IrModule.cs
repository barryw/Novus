using Novus.Diagnostics;
using Novus.HIR;

namespace Novus.IR;

/// <summary>
/// Visibility level for module items
/// </summary>
public enum Visibility
{
    Private,   // File-local (default)
    Internal,  // Target-wide (internal to this compilation unit)
    Public     // Exported (visible to other targets/packages)
}

/// <summary>
/// Represents a complete Novus compilation unit
/// </summary>
public class IrModule
{
    public List<IrFunction> Functions { get; } = new();

    /// <summary>
    /// O(1) function lookup by name. Maintained in sync with Functions list.
    /// Use GetFunction() for lookups instead of Functions.FirstOrDefault().
    /// </summary>
    private readonly Dictionary<string, IrFunction> _functionLookup = new();

    public List<IrEnumType> Enums { get; } = new();

    /// <summary>
    /// O(1) enum lookup by name. Maintained in sync with Enums list.
    /// Use GetEnum() for lookups instead of Enums.FirstOrDefault().
    /// </summary>
    private readonly Dictionary<string, IrEnumType> _enumLookup = new();

    public List<IrStructType> Structs { get; } = new();

    /// <summary>
    /// O(1) struct lookup by name. Maintained in sync with Structs list.
    /// Use GetStruct() for lookups instead of Structs.FirstOrDefault().
    /// </summary>
    private readonly Dictionary<string, IrStructType> _structLookup = new();

    public List<IrTrait> Traits { get; } = new();

    /// <summary>
    /// O(1) trait lookup by name. Maintained in sync with Traits list.
    /// Use GetTrait() for lookups instead of Traits.FirstOrDefault().
    /// </summary>
    private readonly Dictionary<string, IrTrait> _traitLookup = new();

    /// <summary>
    /// Private backing list for trait implementations. Use AddTraitImpl() to add.
    /// </summary>
    private readonly List<IrTraitImpl> _traitImpls = new();

    /// <summary>
    /// Read-only view of trait implementations.
    /// To add implementations, use AddTraitImpl() which maintains all lookup indices.
    /// </summary>
    public IReadOnlyList<IrTraitImpl> TraitImpls => _traitImpls;

    /// <summary>
    /// O(1) trait implementation lookup by (trait name, type name) tuple.
    /// Maintained in sync with TraitImpls list via AddTraitImpl().
    /// Use GetTraitImpl() for lookups instead of iterating TraitImpls.
    /// </summary>
    private readonly Dictionary<(string TraitName, string TypeName), IrTraitImpl> _traitImplLookup = new();

    /// <summary>
    /// O(1) trait implementations lookup by type name.
    /// Returns all trait implementations for a given type without iterating the full list.
    /// Used by FindTraitMethod and FindGenericTraitMethod for efficient lookups.
    /// </summary>
    private readonly Dictionary<string, List<IrTraitImpl>> _traitImplsByType = new();

    public Dictionary<string, IrMonomorphizedType> MonomorphizedTypes { get; } = new();

    /// <summary>
    /// Module constants - maps constant name to (visibility, type, value)
    /// Used by code generator to inline constant references
    /// </summary>
    public Dictionary<string, (Visibility Visibility, IrType Type, object Value)> Constants { get; } = new();

    /// <summary>
    /// Static variables - immutable or mutable global variables with fixed addresses
    /// </summary>
    public List<IrStaticVariable> StaticVariables { get; } = new();

    /// <summary>
    /// External variables - declared with 'extern var', resolved at link time
    /// </summary>
    public List<IrExternalVariable> ExternalVariables { get; } = new();

    /// <summary>
    /// HIR (High-level IR) instructions that need lowering to LIR
    /// These represent language features like Copper lists, Blitter jobs, async functions
    /// </summary>
    public List<HirInstruction> HirInstructions { get; } = new();

    /// <summary>
    /// Program stack size in bytes. Set via #[stack_size(N)] attribute.
    /// Default is 65536 (64KB). AmigaOS CLI default is only 4KB which is too small
    /// for most Novus programs.
    ///
    /// <para><b>Guidelines:</b></para>
    /// <list type="bullet">
    ///   <item>Simple programs: 8KB - 16KB</item>
    ///   <item>Typical applications: 32KB - 64KB (default)</item>
    ///   <item>Deep recursion: 128KB - 256KB</item>
    ///   <item>Large stack arrays: Calculate array_size + 32KB buffer</item>
    /// </list>
    ///
    /// <para><b>IMPORTANT:</b> Stack overflow on 68k crashes without warning.
    /// When in doubt, use more stack. The size is embedded in the executable.</para>
    ///
    /// <para>Example:</para>
    /// <code>
    /// #[stack_size(131072)]  // 128KB stack
    /// fn main() -> i32 { ... }
    /// </code>
    /// </summary>
    public int StackSize { get; set; } = 65536;

    /// <summary>
    /// Target CPU override set via #[cpu("68020")] attribute.
    /// When set, this overrides both command-line and project.toml CPU settings.
    /// A compile warning is emitted when this override is active.
    ///
    /// <para><b>Valid values:</b></para>
    /// <list type="bullet">
    ///   <item>68020 - minimum supported CPU (recommended default)</item>
    ///   <item>68030 - 68030 (cache-aware)</item>
    ///   <item>68040 - 68040 (avoids trappy ops)</item>
    ///   <item>68060 - 68060 (strict op selection)</item>
    ///   <item>68080 - Apollo Vampire FPGA (AMMX instructions)</item>
    /// </list>
    ///
    /// <para>Example:</para>
    /// <code>
    /// #[cpu("68020")]  // Force 68020+ instructions
    /// fn main() -> i32 { ... }
    /// </code>
    /// </summary>
    public string? CpuOverride { get; set; }

    public void AddFunction(IrFunction function)
    {
        Functions.Add(function);
        // Maintain lookup dictionary for O(1) access by name
        // Use indexer to allow overwrites (function may be re-registered during monomorphization)
        _functionLookup[function.Name] = function;
    }

    /// <summary>
    /// Get a function by name with O(1) lookup.
    /// Returns null if no function with the given name exists.
    /// </summary>
    public IrFunction? GetFunction(string name)
    {
        return _functionLookup.TryGetValue(name, out var func) ? func : null;
    }

    /// <summary>
    /// Check if a function with the given name exists (O(1) lookup).
    /// </summary>
    public bool HasFunction(string name)
    {
        return _functionLookup.ContainsKey(name);
    }

    public void AddEnum(IrEnumType enumType)
    {
        Enums.Add(enumType);
        // Maintain lookup dictionary for O(1) access by name
        // Use indexer to allow overwrites (enum may be re-registered during monomorphization)
        _enumLookup[enumType.EnumName] = enumType;
    }

    /// <summary>
    /// Get an enum by name with O(1) lookup.
    /// Returns null if no enum with the given name exists.
    /// </summary>
    public IrEnumType? GetEnum(string name)
    {
        return _enumLookup.TryGetValue(name, out var enumType) ? enumType : null;
    }

    public void AddStruct(IrStructType structType)
    {
        Structs.Add(structType);
        // Maintain lookup dictionary for O(1) access by name
        // Use indexer to allow overwrites (struct may be re-registered during monomorphization)
        _structLookup[structType.StructName] = structType;
    }

    /// <summary>
    /// Get a struct by name with O(1) lookup.
    /// Returns null if no struct with the given name exists.
    /// </summary>
    public IrStructType? GetStruct(string name)
    {
        return _structLookup.TryGetValue(name, out var structType) ? structType : null;
    }

    public void AddTrait(IrTrait trait)
    {
        Traits.Add(trait);
        // Maintain lookup dictionary for O(1) access by name
        // Use indexer to allow overwrites (trait may be re-registered during monomorphization)
        _traitLookup[trait.TraitName] = trait;
    }

    /// <summary>
    /// Get a trait by name with O(1) lookup.
    /// Returns null if no trait with the given name exists.
    /// </summary>
    public IrTrait? GetTrait(string name)
    {
        return _traitLookup.TryGetValue(name, out var trait) ? trait : null;
    }

    public void AddTraitImpl(IrTraitImpl traitImpl)
    {
        _traitImpls.Add(traitImpl);

        // Maintain lookup dictionary for O(1) access by (trait name, type name) composite key
        // Use indexer to allow overwrites (trait impl may be re-registered during monomorphization)
        _traitImplLookup[(traitImpl.TraitName, traitImpl.TypeName)] = traitImpl;

        // Maintain secondary index by type name for FindTraitMethod lookups
        if (!_traitImplsByType.TryGetValue(traitImpl.TypeName, out var implList))
        {
            implList = new List<IrTraitImpl>();
            _traitImplsByType[traitImpl.TypeName] = implList;
        }
        // Avoid duplicates (may be re-registered during monomorphization)
        if (!implList.Contains(traitImpl))
        {
            implList.Add(traitImpl);
        }

        // If this is a Drop implementation, mark the type as implementing Drop
        if (traitImpl.TraitName == "Drop" && traitImpl.ImplementingType is IrStructType structType)
        {
            structType.ImplementsDrop = true;
        }
    }

    /// <summary>
    /// Get a trait implementation by trait name and type name with O(1) lookup.
    /// Returns null if no trait implementation with the given composite key exists.
    /// </summary>
    public IrTraitImpl? GetTraitImpl(string traitName, string typeName)
    {
        return _traitImplLookup.TryGetValue((traitName, typeName), out var traitImpl) ? traitImpl : null;
    }

    /// <summary>
    /// Get all trait implementations for a given type name.
    /// Returns empty enumerable if no implementations exist.
    /// Uses O(1) dictionary lookup instead of filtering the full list.
    /// </summary>
    public IEnumerable<IrTraitImpl> GetTraitImplsForType(string typeName)
    {
        if (_traitImplsByType.TryGetValue(typeName, out var implList))
        {
            return implList;
        }
        return Enumerable.Empty<IrTraitImpl>();
    }

    /// <summary>
    /// Find trait implementation for a type that has a specific method
    /// Returns the mangled method name if found
    /// </summary>
    public string? FindTraitMethod(string typeName, string methodName)
    {
        // Use indexed lookup - O(1) to get the list, then O(k) where k = impls for this type
        foreach (var traitImpl in GetTraitImplsForType(typeName))
        {
            // Extract base trait name from potentially generic trait name
            // e.g., "From<DosError>" -> "From"
            var baseTraitName = traitImpl.TraitName;
            var genericIndex = baseTraitName.IndexOf('<');
            if (genericIndex > 0)
            {
                baseTraitName = baseTraitName.Substring(0, genericIndex);
            }

            // Check if this trait has the method
            var trait = GetTrait(baseTraitName);
            if (trait != null && trait.GetMethod(methodName) != null)
            {
                // Return the mangled name
                return traitImpl.GetMangledMethodName(methodName);
            }
        }

        return null;
    }

    /// <summary>
    /// Find trait implementation for a parameterized generic trait
    /// Returns the mangled convert method name if found
    /// Example: Find From<IntuitionError> implemented for GraphicsError
    ///   FindGenericTraitMethod("GraphicsError", "From", "IntuitionError", "convert")
    /// </summary>
    public string? FindGenericTraitMethod(string typeName, string traitBaseName, string traitParam, string methodName)
    {
        // Use indexed lookup - O(1) to get the list, then O(k) where k = impls for this type
        foreach (var traitImpl in GetTraitImplsForType(typeName))
        {
            // Extract base trait name from potentially generic trait name
            // e.g., "From<DosError>" -> "From"
            var baseTraitName = traitImpl.TraitName;
            var genericIndex = baseTraitName.IndexOf('<');
            if (genericIndex > 0)
            {
                baseTraitName = baseTraitName.Substring(0, genericIndex);
            }

            // Check if this is the right trait (e.g., "From")
            if (baseTraitName != traitBaseName)
            {
                continue;
            }

            // Check if the trait has the correct generic parameter
            // For From<IntuitionError>, we need TraitTypeArgs to contain IntuitionError
            if (traitImpl.TraitTypeArgs.Count > 0)
            {
                // Get the first type argument (From<T> has one parameter)
                var firstTypeArg = traitImpl.TraitTypeArgs[0];
                var typeArgName = firstTypeArg switch
                {
                    IrEnumType enumType => enumType.EnumName,
                    IrStructType structType => structType.StructName,
                    IrIntType intType => intType.IsSigned ? $"i{intType.BitWidth}" : $"u{intType.BitWidth}",
                    IrBoolType => "bool",
                    _ => firstTypeArg.Name
                };

                // Check if it matches the requested parameter
                if (typeArgName != traitParam)
                {
                    continue;
                }
            }

            // Check if this trait has the method
            var trait = GetTrait(baseTraitName);
            if (trait != null && trait.GetMethod(methodName) != null)
            {
                // Return the mangled name
                return traitImpl.GetMangledMethodName(methodName);
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a type implements the Drop trait.
    /// Uses O(1) lookup via _traitImplLookup.
    /// </summary>
    public bool TypeImplementsDrop(string typeName)
    {
        // O(1) lookup using composite key
        return GetTraitImpl("Drop", typeName) != null;
    }

    /// <summary>
    /// Check if a type implements the Drop trait (accepts IrType)
    /// </summary>
    public bool TypeImplementsDrop(IrType type)
    {
        if (type is IrStructType structType)
        {
            // For monomorphized types (e.g., Vec<bool>), check both:
            // 1. Exact match using CacheKey (e.g., "Vec<bool>")
            // 2. Generic base type match (e.g., "Vec" with generic impl)

            // First try exact match
            var typeName = structType.CacheKey ?? structType.StructName;
            if (TypeImplementsDrop(typeName))
            {
                return true;
            }

            // If that fails and this is a monomorphized type, check if the base generic type has a Drop impl
            if (structType.CacheKey != null)
            {
                // Check base struct name (e.g., "Vec" for "Vec<bool>")
                return TypeImplementsDrop(structType.StructName);
            }

            return false;
        }

        // Tuples need Drop if ANY of their elements implement Drop
        if (type is IrTupleType tupleType)
        {
            return TupleNeedsDrop(tupleType);
        }

        // Enums need Drop if ANY of their variant payloads implement Drop
        if (type is IrEnumType enumType)
        {
            return EnumNeedsDrop(enumType);
        }

        // Primitives, pointers, etc. don't need cleanup
        return false;
    }

    /// <summary>
    /// Check if an enum type needs Drop (i.e., has any variant with a payload that implements Drop)
    /// </summary>
    public bool EnumNeedsDrop(IrEnumType enumType)
    {
        foreach (var variant in enumType.Variants)
        {
            foreach (var payloadType in variant.AssociatedData)
            {
                if (TypeImplementsDrop(payloadType))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Check if a tuple type needs Drop (i.e., contains any elements that implement Drop)
    /// </summary>
    public bool TupleNeedsDrop(IrTupleType tupleType)
    {
        foreach (var elementType in tupleType.ElementTypes)
        {
            if (TypeImplementsDrop(elementType))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get all functions marked with @test attribute for test runner generation
    /// </summary>
    public IReadOnlyList<IrFunction> GetTestFunctions()
    {
        return Functions.Where(f =>
            f.Attributes?.Has(SemanticAnalysis.KnownAttributes.Test) == true
        ).ToList();
    }

    /// <summary>
    /// Get all functions marked with @bench or @benchmark attribute for benchmark runner generation
    /// </summary>
    public IReadOnlyList<IrFunction> GetBenchmarkFunctions()
    {
        return Functions.Where(f =>
            f.Attributes?.Has(SemanticAnalysis.KnownAttributes.Bench) == true ||
            f.Attributes?.Has(SemanticAnalysis.KnownAttributes.Benchmark) == true
        ).ToList();
    }
}

/// <summary>
/// Represents a function in the IR
/// </summary>
public class IrFunction
{
    public string Name { get; set; }
    public IrType ReturnType { get; set; }
    public Visibility Visibility { get; set; }
    public bool IsExtern { get; set; }  // true if 'extern' keyword used
    public bool IsVariadic { get; set; }  // true if function has variadic parameters (...)
    public bool IsExported { get; set; }  // true if #[export] attribute is present
    public bool IsConstFn { get; set; }   // true if 'const fn' - can be evaluated at compile-time
    public List<IrParameter> Parameters { get; } = new();
    public List<IrLocalVariable> LocalVariables { get; } = new();
    public List<IrBasicBlock> BasicBlocks { get; } = new();
    public List<IrBasicBlock> DeferredBlocks { get; } = new();  // Blocks to execute on function exit (LIFO)
    public List<string> GenericParameters { get; } = new();  // Generic type parameters (e.g., ["T", "U"])
    public IrWhereClause? WhereClause { get; set; }  // Generic type constraints (e.g., where T: Sortable)
    public SourceLocation? Location { get; set; }  // Source location of function definition (for debug info)
    public Novus.SemanticAnalysis.AttributeCollection? Attributes { get; set; }  // Function attributes (@inline, @noinline, @export, etc.)
    public IrCallingConvention CallingConvention { get; set; } = IrCallingConvention.Novus;
    public string? ReturnRegister { get; set; }

    /// <summary>
    /// Indicates this function is a monomorphized instance of a generic function.
    /// Set to true when the function is created via generic instantiation.
    /// Used by code generator to emit as 'static inline' to avoid duplicate symbols.
    /// </summary>
    public bool IsMonomorphized { get; set; }

    /// <summary>
    /// For monomorphized functions: the original generic function name before instantiation.
    /// E.g., for "Vec_push_i32", this would be "Vec_push".
    /// Null for non-monomorphized functions.
    /// </summary>
    public string? GenericSourceName { get; set; }

    /// <summary>
    /// For overloaded functions: the original unmangled function name.
    /// E.g., for "abs__i32", this would be "abs".
    /// Null for non-overloaded functions.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// For monomorphized functions: the type arguments used to instantiate this function.
    /// E.g., for "Vec_push_i32", this would contain [IrIntType(i32)].
    /// Empty for non-monomorphized functions.
    /// </summary>
    public List<IrType> MonomorphizationTypeArgs { get; } = new();

    /// <summary>
    /// Indicates this function is a trait method implementation for a monomorphized type.
    /// E.g., Drop::drop for Vec<TagItem>.
    /// These functions should be emitted as 'static inline' when used across modules.
    /// </summary>
    public bool IsMonomorphizedTraitMethod { get; set; }

    /// <summary>
    /// Cached control flow graph. Lazily built on first access via GetCFG().
    /// Call InvalidateCFG() after modifying basic blocks or control flow.
    /// </summary>
    private ControlFlowGraph? _cachedCfg;

    public IrFunction(string name, IrType returnType, Visibility visibility = Visibility.Private, bool isExtern = false, bool isVariadic = false)
    {
        Name = name;
        ReturnType = returnType;
        Visibility = visibility;
        IsExtern = isExtern;
        IsVariadic = isVariadic;
    }

    // Compatibility property for code that checks IsPublic
    public bool IsPublic => Visibility == Visibility.Public;

    /// <summary>
    /// Returns true if this function is marked as an interrupt handler (@interrupt).
    /// Interrupt handlers have special constraints: they cannot call non-interrupt-safe functions.
    /// </summary>
    public bool IsInterruptHandler =>
        Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.Interrupt) == true || IsInterruptVector;

    public bool IsInterruptVector =>
        Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.InterruptVector) ?? false;

    /// <summary>
    /// Returns true if this function is marked as interrupt-safe (@interrupt_safe).
    /// Interrupt-safe functions can be called from interrupt handlers.
    /// Pure functions (no side effects, no allocations, no OS calls) are typically interrupt-safe.
    /// </summary>
    public bool IsInterruptSafe => Attributes?.Has(Novus.SemanticAnalysis.KnownAttributes.InterruptSafe) ?? false;

    public IrBasicBlock CreateBasicBlock(string label)
    {
        var block = new IrBasicBlock(label);
        BasicBlocks.Add(block);
        InvalidateCFG(); // New block invalidates cached CFG
        return block;
    }

    /// <summary>
    /// Get the control flow graph for this function.
    /// The CFG is lazily built on first access and cached for subsequent calls.
    /// </summary>
    public ControlFlowGraph GetCFG()
    {
        if (_cachedCfg == null)
        {
            _cachedCfg = new ControlFlowGraph(this);
        }
        return _cachedCfg;
    }

    /// <summary>
    /// Invalidate the cached CFG. Must be called after any modifications to
    /// basic blocks or control flow instructions (branches, returns, etc.).
    /// </summary>
    public void InvalidateCFG()
    {
        _cachedCfg = null;
    }
}

/// <summary>
/// Represents a function parameter
/// </summary>
public class IrParameter : Novus.SemanticAnalysis.IParameterInfo
{
    public string Name { get; set; }
    public IrType Type { get; set; }
    public bool IsVariadic { get; set; }  // true if this is a variadic parameter (...)
    public string? Register { get; set; }

    // IParameterInfo implementation
    string Novus.SemanticAnalysis.IParameterInfo.ParameterName => Name;
    IrType Novus.SemanticAnalysis.IParameterInfo.ParameterType => Type;

    public IrParameter(string name, IrType type, bool isVariadic = false, string? register = null)
    {
        Name = name;
        Type = type;
        IsVariadic = isVariadic;
        Register = register;
    }
}

/// <summary>
/// Represents a local variable
/// </summary>
public class IrLocalVariable
{
    public string Name { get; set; }
    public IrType Type { get; set; }
    public bool IsMutable { get; set; }

    public IrLocalVariable(string name, IrType type, bool isMutable)
    {
        Name = name;
        Type = type;
        IsMutable = isMutable;
    }
}

/// <summary>
/// Represents a basic block in the IR (single entry, single exit)
/// </summary>
public class IrBasicBlock
{
    public string Label { get; set; }

    /// <summary>
    /// Phi functions for this block - these execute "simultaneously" at block entry
    /// before any normal instructions. Only populated when the function is in SSA form.
    /// </summary>
    public List<IrPhi> PhiFunctions { get; } = new();

    public List<IrInstruction> Instructions { get; } = new();

    public IrBasicBlock(string label)
    {
        Label = label;
    }

    public void AddInstruction(IrInstruction instruction)
    {
        Instructions.Add(instruction);
    }

    /// <summary>
    /// Add a phi function to this block
    /// </summary>
    public void AddPhi(IrPhi phi)
    {
        PhiFunctions.Add(phi);
    }
}

/// <summary>
/// Base class for all IR instructions
/// </summary>
public abstract class IrInstruction
{
    /// <summary>
    /// Source location where this instruction originated (for debug/error messages)
    /// </summary>
    public SourceLocation? Location { get; set; }

    /// <summary>
    /// Optional metadata for optimization passes (PGO hints, etc.)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Return instruction
/// </summary>
public class IrReturn : IrInstruction
{
    public IrValue? Value { get; set; }

    public IrReturn(IrValue? value = null)
    {
        Value = value;
    }
}

/// <summary>
/// Defer instruction - registers a block of code to execute on function exit
/// Deferred blocks execute in LIFO order (last registered, first executed)
/// </summary>
public class IrDefer : IrInstruction
{
    public IrBasicBlock DeferredBlock { get; set; }

    public IrDefer(IrBasicBlock deferredBlock)
    {
        DeferredBlock = deferredBlock;
    }
}

/// <summary>
/// Assert instruction - runtime assertion that panics on failure
/// Stripped in release builds unless explicitly enabled
/// </summary>
public class IrAssert : IrInstruction
{
    public IrValue Condition { get; set; }
    public string? Message { get; set; }

    public IrAssert(IrValue condition, string? message, SourceLocation location)
    {
        Condition = condition;
        Message = message;
        Location = location;
    }
}

/// <summary>
/// Runtime panic - unrecoverable error that displays GUI dialog and halts execution
/// Always emitted (never stripped, even in release builds)
/// </summary>
public class IrPanic : IrInstruction
{
    public string Message { get; set; }

    public IrPanic(string message, SourceLocation location)
    {
        Message = message;
        Location = location;
    }
}

/// <summary>
/// Structured for-loop hint - tells C codegen to emit natural C for-loop
/// This is a marker that precedes the loop variable initialization
/// </summary>
public class IrStructuredForLoopHint : IrInstruction
{
    public string LoopVarName { get; set; }
    public string LengthVarName { get; set; }
    public string BodyLabel { get; set; }
    public string CondLabel { get; set; }
    public string EndLabel { get; set; }

    public IrStructuredForLoopHint(string loopVarName, string lengthVarName, string bodyLabel, string condLabel, string endLabel)
    {
        LoopVarName = loopVarName;
        LengthVarName = lengthVarName;
        BodyLabel = bodyLabel;
        CondLabel = condLabel;
        EndLabel = endLabel;
    }
}

/// <summary>
/// Label instruction (marks a location for branching)
/// </summary>
public class IrLabel : IrInstruction
{
    public string Name { get; set; }

    public IrLabel(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Unconditional branch instruction
/// </summary>
public class IrBranch : IrInstruction
{
    public string Target { get; set; }

    public IrBranch(string target)
    {
        Target = target;
    }
}

/// <summary>
/// Conditional branch instruction
/// </summary>
public class IrConditionalBranch : IrInstruction
{
    public IrValue Condition { get; set; }
    public string TrueTarget { get; set; }
    public string FalseTarget { get; set; }

    public IrConditionalBranch(IrValue condition, string trueTarget, string falseTarget)
    {
        Condition = condition;
        TrueTarget = trueTarget;
        FalseTarget = falseTarget;
    }
}

/// <summary>
/// Phi function - merges values from multiple control flow paths in SSA form
/// A phi function has one incoming value for each predecessor block
/// The phi "executes" conceptually at block entry, selecting the value from
/// whichever predecessor was taken
///
/// Example: x_2 = φ(x_0 from block1, x_1 from block2)
/// </summary>
public class IrPhi : IrInstruction
{
    /// <summary>
    /// The variable being defined by this phi function (the LHS)
    /// </summary>
    public IrVariable Destination { get; set; }

    /// <summary>
    /// Incoming values - parallel array with IncomingBlocks
    /// IncomingValues[i] is the value to use if we came from IncomingBlocks[i]
    /// </summary>
    public List<IrValue> IncomingValues { get; } = new();

    /// <summary>
    /// Incoming blocks - parallel array with IncomingValues
    /// These are the predecessor blocks that provide the corresponding values
    /// </summary>
    public List<IrBasicBlock> IncomingBlocks { get; } = new();

    public IrPhi(IrVariable destination)
    {
        Destination = destination;
    }

    /// <summary>
    /// Add an incoming value from a specific predecessor block
    /// </summary>
    public void AddIncoming(IrValue value, IrBasicBlock block)
    {
        IncomingValues.Add(value);
        IncomingBlocks.Add(block);
    }

    /// <summary>
    /// Get the value for a specific predecessor block
    /// Returns null if the block is not a predecessor
    /// </summary>
    public IrValue? GetValueForBlock(IrBasicBlock block)
    {
        var index = IncomingBlocks.IndexOf(block);
        return index >= 0 ? IncomingValues[index] : null;
    }

    /// <summary>
    /// Replace all occurrences of an old value with a new value
    /// </summary>
    public void ReplaceValue(IrValue oldValue, IrValue newValue)
    {
        for (int i = 0; i < IncomingValues.Count; i++)
        {
            if (IncomingValues[i] == oldValue)
            {
                IncomingValues[i] = newValue;
            }
        }
    }
}

/// <summary>
/// Binary operation instruction (add, sub, mul, div, etc.)
/// </summary>
public class IrBinaryOp : IrInstruction
{
    public enum OpKind
    {
        Add, Sub, Mul, Div, Mod,
        And, Or, Xor,
        Shl, Shr,
        Eq, Ne, Lt, Le, Gt, Ge
    }

    public string ResultName { get; set; }
    public OpKind Operation { get; set; }
    public IrValue Left { get; set; }
    public IrValue Right { get; set; }
    public IrType Type { get; set; }

    public IrBinaryOp(string resultName, OpKind operation, IrValue left, IrValue right, IrType type)
    {
        ResultName = resultName;
        Operation = operation;
        Left = left;
        Right = right;
        Type = type;
    }
}

/// <summary>
/// Function call instruction
/// </summary>
public class IrCall : IrInstruction
{
    public string FunctionName { get; set; }
    public List<IrValue> Arguments { get; } = new();
    public IrType ReturnType { get; set; }
    public string? ResultName { get; set; }  // null for void functions

    public IrCall(string functionName, IrType returnType, string? resultName = null)
    {
        FunctionName = functionName;
        ReturnType = returnType;
        ResultName = resultName;
    }
}

/// <summary>
/// Local variable declaration instruction
/// </summary>
public class IrLocalDecl : IrInstruction
{
    public string Name { get; set; }
    public IrType Type { get; set; }
    public bool IsMutable { get; set; }
    /// <summary>
    /// Initial value for the variable. Can be null for uninitialized declarations
    /// like `var buf: [u8; 16]` where the array is stack-allocated but not initialized.
    /// </summary>
    public IrValue? InitialValue { get; set; }

    public IrLocalDecl(string name, IrType type, bool isMutable, IrValue? initialValue)
    {
        Name = name;
        Type = type;
        IsMutable = isMutable;
        InitialValue = initialValue;
    }
}

/// <summary>
/// Store instruction - assigns a value to a local variable
/// </summary>
public class IrStore : IrInstruction
{
    public string VariableName { get; set; }
    public IrValue Value { get; set; }

    public IrStore(string variableName, IrValue value)
    {
        VariableName = variableName;
        Value = value;
    }
}

/// <summary>
/// Dereference store instruction - assigns a value through a pointer/reference (*ptr = value)
/// </summary>
public class IrDereferenceStore : IrInstruction
{
    public IrValue Pointer { get; set; }
    public IrValue Value { get; set; }
    public bool IsVolatile { get; set; }

    public IrDereferenceStore(IrValue pointer, IrValue value, bool isVolatile = false)
    {
        Pointer = pointer;
        Value = value;
        IsVolatile = isVolatile;
    }
}

/// <summary>
/// Base class for IR values (constants, variables, etc.)
/// </summary>
public abstract class IrValue
{
    public IrType Type { get; set; }

    protected IrValue(IrType type)
    {
        Type = type;
    }
}

/// <summary>
/// Integer constant value
/// </summary>
public class IrConstant : IrValue
{
    public long Value { get; set; }

    public IrConstant(long value, IrType type) : base(type)
    {
        Value = value;
    }
}

/// <summary>
/// Size of type expression - emits C's sizeof() operator
/// </summary>
public class IrSizeOf : IrValue
{
    public IrType TargetType { get; set; }

    public IrSizeOf(IrType targetType, IrType returnType) : base(returnType)
    {
        TargetType = targetType;
    }
}

/// <summary>
/// Zeroed value expression - creates a zero-initialized value of the given type.
/// For structs this uses CopyMem from a zero-initialized local.
/// For primitives this returns the zero value (0, false, null, etc.)
/// </summary>
public class IrZeroed : IrValue
{
    public IrType TargetType { get; set; }

    public IrZeroed(IrType targetType) : base(targetType)
    {
        TargetType = targetType;
    }
}

/// <summary>
/// Drop in place instruction - calls the Drop trait implementation on a value via pointer.
/// This is used for manual resource cleanup of values stored in untyped memory (e.g., in Vec<T>).
/// Generates: if type has Drop, call drop() on *ptr
/// </summary>
public class IrDropInPlace : IrInstruction
{
    /// <summary>
    /// The pointer to the value to drop. Must be a pointer type.
    /// </summary>
    public IrValue Pointer { get; set; }

    /// <summary>
    /// The type being dropped (derived from the pointer's element type).
    /// </summary>
    public IrType ElementType { get; set; }

    public IrDropInPlace(IrValue pointer, IrType elementType)
    {
        Pointer = pointer;
        ElementType = elementType;
    }
}

/// <summary>
/// Boolean constant value
/// </summary>
public class IrBoolConstant : IrValue
{
    public bool Value { get; set; }

    public IrBoolConstant(bool value) : base(IrBoolType.Instance)
    {
        Value = value;
    }
}

/// <summary>
/// Const generic parameter reference (e.g., N in Buffer<const N: u32>)
/// This is a placeholder that gets substituted with an IrConstant during monomorphization
/// </summary>
public class IrConstGenericParamRef : IrValue
{
    public string ParameterName { get; }

    public IrConstGenericParamRef(string parameterName, IrType constType) : base(constType)
    {
        ParameterName = parameterName;
    }
}

/// <summary>
/// Floating point constant value
/// </summary>
public class IrFloatConstant : IrValue
{
    public double Value { get; set; }

    public IrFloatConstant(double value, IrFloatType type) : base(type)
    {
        Value = value;
    }
}

/// <summary>
/// Fixed-point constant value
/// </summary>
public class IrFixedConstant : IrValue
{
    public double Value { get; set; }  // Store as double, will convert to fixed-point in codegen

    public IrFixedConstant(double value, IrFixedType type) : base(type)
    {
        Value = value;
    }
}

/// <summary>
/// String literal value - raw pointer to null-terminated string in data section.
/// For now, uses *u8 pointer type. When std::Str is fully implemented,
/// string literals will be converted to Str values with ptr+len.
/// </summary>
public class IrStringLiteral : IrValue
{
    public string Value { get; set; }
    public string Label { get; set; }  // Unique label for this string in data section
    public int Length { get; set; }    // Pre-calculated string length

    public IrStringLiteral(string value, string label) : base(IrPointerType.U8Ptr)
    {
        Value = value;
        Label = label;
        Length = value.Length;  // Calculate length at compile time
    }
}

/// <summary>
/// Variable reference
/// </summary>
public class IrVariable : IrValue
{
    /// <summary>
    /// Base name of the variable (e.g., "x", "count", "result")
    /// In non-SSA form, this is the only name component
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// SSA version number for this variable
    /// -1 means not in SSA form (regular variable)
    /// >= 0 means SSA form with version number (e.g., x_0, x_1, x_2)
    /// </summary>
    public int Version { get; set; } = -1;

    /// <summary>
    /// Get the full SSA name including version (e.g., "x_2")
    /// If not in SSA form (Version == -1), returns just the base name
    /// </summary>
    public string SsaName => Version >= 0 ? $"{Name}_{Version}" : Name;

    /// <summary>
    /// Create a non-SSA variable (Version = -1)
    /// </summary>
    public IrVariable(string name, IrType type) : base(type)
    {
        Name = name;
        Version = -1;
    }

    /// <summary>
    /// Create an SSA variable with explicit version
    /// </summary>
    public IrVariable(string name, int version, IrType type) : base(type)
    {
        Name = name;
        Version = version;
    }

    /// <summary>
    /// Check if this variable is in SSA form
    /// </summary>
    public bool IsInSsaForm => Version >= 0;
}

/// <summary>
/// Struct literal value - represents a struct initialization with field values
/// </summary>
public class IrStructLiteral : IrValue
{
    public Dictionary<string, IrValue> FieldValues { get; set; }

    public IrStructLiteral(IrStructType type, Dictionary<string, IrValue> fieldValues) : base(type)
    {
        FieldValues = fieldValues;
    }
}

/// <summary>
/// Tuple literal value - ordered sequence of values
/// Example: (255, 128, 64) for RGB tuple
/// Unit value () is represented as a tuple with zero elements
/// </summary>
public class IrTupleLiteral : IrValue
{
    public List<IrValue> Elements { get; set; }

    public IrTupleLiteral(IrTupleType type, List<IrValue> elements) : base(type)
    {
        Elements = elements;
    }
}

/// <summary>
/// Represents types in the IR
/// </summary>
public abstract class IrType
{
    public abstract int SizeInBytes { get; }
    public abstract string Name { get; }
}

public class IrIntType : IrType
{
    public int BitWidth { get; }
    public bool IsSigned { get; }

    public IrIntType(int bitWidth, bool isSigned)
    {
        BitWidth = bitWidth;
        IsSigned = isSigned;
    }

    public override int SizeInBytes => BitWidth / 8;
    public override string Name => $"{(IsSigned ? 'i' : 'u')}{BitWidth}";

    // Predefined common types
    public static readonly IrIntType U8 = new(8, false);
    public static readonly IrIntType U16 = new(16, false);
    public static readonly IrIntType U32 = new(32, false);
    public static readonly IrIntType U64 = new(64, false);
    public static readonly IrIntType I8 = new(8, true);
    public static readonly IrIntType I16 = new(16, true);
    public static readonly IrIntType I32 = new(32, true);
    public static readonly IrIntType I64 = new(64, true);
}

public class IrBoolType : IrType
{
    public static readonly IrBoolType Instance = new();

    private IrBoolType() { }

    public override int SizeInBytes => 1;  // 1 byte (stored as u8)
    public override string Name => "bool";
}

public class IrVoidType : IrType
{
    public static readonly IrVoidType Instance = new();

    private IrVoidType() { }

    public override int SizeInBytes => 0;
    public override string Name => "void";
}

/// <summary>
/// The "never" type (!) - represents computations that never complete.
/// Used for functions that panic, infinite loops, or unreachable code.
/// </summary>
public class IrNeverType : IrType
{
    public static readonly IrNeverType Instance = new();

    private IrNeverType() { }

    public override int SizeInBytes => 0;  // Never types have no size
    public override string Name => "!";    // Rust convention for never type
}

/// <summary>
/// A value of never type - represents a diverging computation.
/// This is used to type-check unreachable!() and similar constructs.
/// </summary>
public class IrNever : IrValue
{
    public IrNever() : base(IrNeverType.Instance) { }
}

public class IrArrayType : IrType
{
    public IrType ElementType { get; }
    public int Length { get; }

    public IrArrayType(IrType elementType, int length)
    {
        ElementType = elementType;
        Length = length;
    }

    public override int SizeInBytes => ElementType.SizeInBytes * Length;
    public override string Name => $"[{Length}]{ElementType.Name}";
}

/// <summary>
/// Pointer type - all pointers are 32-bit addresses on 68k
/// CAN be null - must check before dereferencing
/// </summary>
public class IrPointerType : IrType
{
    public IrType PointeeType { get; }

    public IrPointerType(IrType pointeeType)
    {
        PointeeType = pointeeType;
    }

    public override int SizeInBytes => 4; // All pointers are 32-bit on 68k
    public override string Name => $"*{PointeeType.Name}";

    // Predefined common pointer types (for static field initialization)
    public static readonly IrPointerType U8Ptr = new(IrIntType.U8);
}

/// <summary>
/// Immutable reference type - GUARANTEED non-null at compile time
/// Read-only access to the referenced value
/// </summary>
public class IrReferenceType : IrType
{
    public IrType PointeeType { get; }

    public IrReferenceType(IrType pointeeType)
    {
        PointeeType = pointeeType;
    }

    public override int SizeInBytes => 4; // References are 32-bit addresses on 68k
    public override string Name => $"&{PointeeType.Name}";
}

/// <summary>
/// Mutable reference type - GUARANTEED non-null at compile time
/// Allows modification of the referenced value
/// </summary>
public class IrMutReferenceType : IrType
{
    public IrType PointeeType { get; }

    public IrMutReferenceType(IrType pointeeType)
    {
        PointeeType = pointeeType;
    }

    public override int SizeInBytes => 4; // References are 32-bit addresses on 68k
    public override string Name => $"&var {PointeeType.Name}";
}

/// <summary>
/// Calling convention carried by function declarations and function pointer types.
/// </summary>
public enum IrCallingConvention
{
    Novus,
    Amiga
}

public static class IrAmigaAbi
{
    public static bool TryNormalizeRegister(string? value, out string register)
    {
        register = value?.ToLowerInvariant() ?? "";
        if (register.Length == 2 && register[1] is >= '0' and <= '7')
        {
            if (register[0] == 'd' || (register[0] == 'a' && register[1] != '7'))
                return true;
        }

        return register.Length == 3 && register.StartsWith("fp", StringComparison.Ordinal) &&
               register[2] is >= '0' and <= '7';
    }
}

/// <summary>
/// Function pointer type.
/// </summary>
public class IrFunctionPointerType : IrType
{
    public List<IrType> ParameterTypes { get; }
    public IrType ReturnType { get; }
    public IrCallingConvention CallingConvention { get; }
    public List<string?> ParameterRegisters { get; }
    public string? ReturnRegister { get; }

    public IrFunctionPointerType(
        List<IrType> parameterTypes,
        IrType returnType,
        IrCallingConvention callingConvention = IrCallingConvention.Novus,
        List<string?>? parameterRegisters = null,
        string? returnRegister = null)
    {
        ParameterTypes = parameterTypes;
        ReturnType = returnType;
        CallingConvention = callingConvention;
        ParameterRegisters = parameterRegisters ?? Enumerable.Repeat<string?>(null, parameterTypes.Count).ToList();
        ReturnRegister = returnRegister;

        if (ParameterRegisters.Count != ParameterTypes.Count)
            throw new ArgumentException("A function pointer needs one register binding per parameter.", nameof(parameterRegisters));
    }

    public override int SizeInBytes => 4; // Function pointers are 32-bit addresses on 68k
    public override string Name
    {
        get
        {
            var paramStr = ParameterTypes.Count > 0
                ? string.Join(", ", ParameterTypes.Select((p, i) =>
                    ParameterRegisters[i] == null ? p.Name : $"{p.Name} in {ParameterRegisters[i]}"))
                : "";
            var retStr = ReturnType is IrVoidType
                ? ""
                : $" -> {ReturnType.Name}{(ReturnRegister == null ? "" : $" in {ReturnRegister}")}";
            var prefix = CallingConvention == IrCallingConvention.Amiga ? "amiga " : "";
            return $"{prefix}fn({paramStr}){retStr}";
        }
    }
}

/// <summary>
/// Closure type - function with captured environment (fat pointer)
/// Size is 8 bytes on 68k: 4-byte function pointer + 4-byte environment pointer
/// </summary>
public class IrClosureType : IrType
{
    public List<IrType> ParameterTypes { get; }
    public IrType ReturnType { get; }
    public IrStructType? EnvironmentType { get; set; }  // null if stateless (no captures)
    public List<CapturedVariable> CapturedVariables { get; } = new();

    public IrClosureType(List<IrType> parameterTypes, IrType returnType, IrStructType? environmentType = null)
    {
        ParameterTypes = parameterTypes;
        ReturnType = returnType;
        EnvironmentType = environmentType;
    }

    /// <summary>
    /// True if closure has no captures (can be optimized to function pointer)
    /// </summary>
    public bool IsStateless => EnvironmentType == null && CapturedVariables is [];

    public override int SizeInBytes => 8;  // Fat pointer: fn_ptr (4) + env_ptr (4)

    public override string Name
    {
        get
        {
            var paramStr = ParameterTypes.Count > 0
                ? string.Join(", ", ParameterTypes.Select(p => p.Name))
                : "";
            var retStr = ReturnType is IrVoidType ? "" : $" -> {ReturnType.Name}";
            return $"closure({paramStr}){retStr}";
        }
    }
}

/// <summary>
/// Represents a variable captured by a closure
/// </summary>
public class CapturedVariable
{
    public string Name { get; set; } = "";
    public IrType Type { get; set; } = IrVoidType.Instance;
    public CaptureMode Mode { get; set; } = CaptureMode.ByValue;
}

/// <summary>
/// How a variable is captured by a closure
/// </summary>
public enum CaptureMode
{
    /// <summary>Copy the value into the environment struct</summary>
    ByValue,
    /// <summary>Store a pointer to the original (for &x captures)</summary>
    ByReference,
    /// <summary>Store a pointer allowing mutation (for mut x captures)</summary>
    Mutable
}

/// <summary>
/// Floating point type (f32, f64)
/// Uses soft-float implementation on 68k
/// </summary>
public class IrFloatType : IrType
{
    public int BitWidth { get; }

    public IrFloatType(int bitWidth)
    {
        if (bitWidth != 32 && bitWidth != 64)
            throw new ArgumentException("Float bit width must be 32 or 64", nameof(bitWidth));
        BitWidth = bitWidth;
    }

    public override int SizeInBytes => BitWidth / 8;
    public override string Name => $"f{BitWidth}";

    // Predefined common types
    public static readonly IrFloatType F32 = new(32);
    public static readonly IrFloatType F64 = new(64);
}

/// <summary>
/// Fixed-point type (fixed16 = 8.8, fixed32 = 16.16)
/// For efficient realtime math on 68k
/// </summary>
public class IrFixedType : IrType
{
    public int BitWidth { get; }

    public IrFixedType(int bitWidth)
    {
        if (bitWidth != 16 && bitWidth != 32)
            throw new ArgumentException("Fixed bit width must be 16 or 32", nameof(bitWidth));
        BitWidth = bitWidth;
    }

    public override int SizeInBytes => BitWidth / 8;
    public override string Name => $"fixed{BitWidth}";

    // Predefined common types
    public static readonly IrFixedType Fixed16 = new(16);  // 8.8 fixed point
    public static readonly IrFixedType Fixed32 = new(32);  // 16.16 fixed point
}

/// <summary>
/// Tuple type - ordered sequence of types
/// Example: (u8, u8, u8) for RGB colors
/// Unit type () is represented as a tuple with zero elements
/// </summary>
public class IrTupleType : IrType
{
    public List<IrType> ElementTypes { get; }
    private int? _cachedSize;

    public IrTupleType(List<IrType> elementTypes)
    {
        ElementTypes = elementTypes ?? new List<IrType>();
    }

    public override int SizeInBytes
    {
        get
        {
            if (_cachedSize.HasValue)
                return _cachedSize.Value;

            // Unit type () has size 0
            if (ElementTypes is [])
            {
                _cachedSize = 0;
                return 0;
            }

            // Calculate total size with alignment
            int size = 0;
            foreach (var elementType in ElementTypes)
            {
                // Align element to its natural alignment
                int elementSize = elementType.SizeInBytes;
                int alignment = elementSize switch
                {
                    1 => 1,  // byte-aligned
                    2 => 2,  // word-aligned
                    _ => 2   // word-aligned for everything else (68k prefers word alignment)
                };

                // Pad to alignment
                if (size % alignment != 0)
                    size += alignment - (size % alignment);

                size += elementSize;
            }

            // Pad final tuple size to word boundary (68k likes word-aligned data)
            if (size % 2 != 0)
                size++;

            _cachedSize = size;
            return size;
        }
    }

    public override string Name
    {
        get
        {
            if (ElementTypes is [])
                return "()";
            return $"({string.Join(", ", ElementTypes.Select(t => t.Name))})";
        }
    }

    /// <summary>
    /// Static instance for unit type ()
    /// </summary>
    public static readonly IrTupleType Unit = new(new List<IrType>());
}

/// <summary>
/// Struct type - composite type with named fields
/// </summary>
public class IrStructType : IrType
{
    // Thread-local stack to detect recursive type definitions during size calculation.
    // Each thread gets its own HashSet to avoid synchronization overhead.
    [ThreadStatic]
    private static HashSet<string>? _sizingStack;

    public string StructName { get; }
    public List<IrStructField> Fields { get; }
    public List<string> GenericParameters { get; }  // Type parameter names (e.g., ["T"])
    public Dictionary<string, IrType>? ConstGenericParameters { get; }  // Const parameter names to types (e.g., {"N" -> u32})
    public List<IrType>? TypeArguments { get; set; }  // Actual type arguments for monomorphized types (e.g., [Str])
    public string? CacheKey { get; set; }  // Cache key for monomorphized types (e.g., "Vec<i32>")
    public Novus.SemanticAnalysis.AttributeCollection? Attributes { get; set; }  // Struct attributes (@library, @packed, etc.)
    public IrWhereClause? WhereClause { get; set; }  // Generic type constraints (e.g., where T: Sortable)
    public bool ImplementsDrop { get; set; }  // True if this type implements the Drop trait
    public bool IsUnion { get; }

    // Thread-safe cached size. Uses volatile read/write semantics.
    // -1 means not computed yet, any other value is the computed size.
    private volatile int _cachedSize = -1;

    public IrStructType(string structName, List<IrStructField> fields, List<string>? genericParams = null, string? cacheKey = null, Novus.SemanticAnalysis.AttributeCollection? attributes = null, IrWhereClause? whereClause = null, List<IrType>? typeArguments = null, Dictionary<string, IrType>? constGenericParams = null, bool isUnion = false)
    {
        StructName = structName;
        Fields = fields;
        GenericParameters = genericParams ?? new List<string>();
        ConstGenericParameters = constGenericParams;
        TypeArguments = typeArguments;
        CacheKey = cacheKey;
        Attributes = attributes;
        WhereClause = whereClause;
        IsUnion = isUnion;
    }

    public override int SizeInBytes
    {
        get
        {
            // Fast path: return cached size if already computed
            int cached = _cachedSize;
            if (cached >= 0)
                return cached;

            // Initialize thread-local stack if needed
            _sizingStack ??= new HashSet<string>();

            // Use cache key if available, otherwise struct name
            string typeKey = CacheKey ?? StructName;

            // Detect recursive type definition without indirection
            if (_sizingStack.Contains(typeKey))
            {
                throw new InvalidOperationException(
                    $"Recursive type definition without indirection: struct '{StructName}' contains itself directly. " +
                    "Use a pointer (*T) or reference (&T) to break the cycle.");
            }

            // Mark this type as being sized
            _sizingStack.Add(typeKey);

            try
            {
                // Check if struct is packed (no automatic alignment padding)
                bool isPacked = Attributes?.Has("packed") ?? false;

                // Calculate total size with Amiga 68k ABI word alignment.
                // Byte accesses can be at any address.
                int size = 0;
                foreach (var structField in Fields)
                {
                    int fieldSize = structField.Type.SizeInBytes;

                    if (IsUnion)
                    {
                        structField.Offset = 0;
                        size = Math.Max(size, fieldSize);
                        continue;
                    }

                    // For packed structs, fields are placed sequentially without padding
                    if (!isPacked)
                    {
                        // Amiga 68k ABI alignment rules:
                        // - 1-byte fields: no alignment needed
                        // - 2-byte fields (word): must be word-aligned (even address)
                        // - 4+ byte fields (long, pointers, structs): must be word-aligned (even address)
                        // Note: 68020+ can handle misaligned access but with performance penalty,
                        // Keep word/long fields ABI-compatible and efficiently aligned.
                        int alignment = fieldSize switch
                        {
                            1 => 1,  // byte-aligned (any address OK)
                            _ => 2   // word-aligned for everything else (68k requirement)
                        };

                        // Pad to alignment
                        if (size % alignment != 0)
                            size += alignment - (size % alignment);
                    }

                    structField.Offset = size;
                    size += fieldSize;
                }

                // Pad final struct size to word boundary so arrays of structs are properly aligned
                // (skip for packed structs)
                if (!isPacked && !IsUnion && size % 2 != 0)
                    size++;

                // Store computed size. Multiple threads may race here but they'll all
                // compute the same value, so the race is benign.
                _cachedSize = size;
                return size;
            }
            finally
            {
                // Remove this type from the sizing stack
                _sizingStack.Remove(typeKey);
            }
        }
    }

    public override string Name
    {
        get
        {
            // For generic structs, show the generic parameters (type + const)
            if (GenericParameters.Count > 0 || (ConstGenericParameters?.Count > 0))
            {
                var allParams = new List<string>(GenericParameters);
                if (ConstGenericParameters != null)
                {
                    foreach (var kvp in ConstGenericParameters)
                    {
                        allParams.Add($"const {kvp.Key}");
                    }
                }
                return $"{StructName}<{string.Join(", ", allParams)}>";
            }
            // For monomorphized structs, show the concrete type arguments
            if (TypeArguments != null && TypeArguments.Count > 0)
                return $"{StructName}<{string.Join(", ", TypeArguments.Select(t => t.Name))}>";
            return StructName;
        }
    }

    public IrStructField? GetField(string fieldName)
    {
        return Fields.FirstOrDefault(f => f.Name == fieldName);
    }

    /// <summary>
    /// Create a monomorphized version of a generic struct by substituting type parameters
    /// </summary>
    public static IrStructType Monomorphize(IrStructType genericStruct, List<IrType> typeArgs)
    {
        if (genericStruct.GenericParameters.Count != typeArgs.Count)
        {
            throw new ArgumentException($"Type argument count mismatch: expected {genericStruct.GenericParameters.Count}, got {typeArgs.Count}");
        }

        // Build substitution map
        var substitutions = new Dictionary<string, IrType>();
        for (int i = 0; i < genericStruct.GenericParameters.Count; i++)
        {
            substitutions[genericStruct.GenericParameters[i]] = typeArgs[i];
        }

        // Substitute type parameters in fields
        var monomorphizedFields = new List<IrStructField>();
        foreach (var field in genericStruct.Fields)
        {
            var substitutedType = SubstituteType(field.Type, substitutions);
            monomorphizedFields.Add(new IrStructField(field.Name, substitutedType));
        }

        // Build cache key (e.g., "Vec<i32>")
        var cacheKey = $"{genericStruct.StructName}<{string.Join(",", typeArgs.Select(t => t.Name))}>";

        // IMPORTANT: Pass typeArgs as typeArguments so InstantiateGenericMethod can use them
        return new IrStructType(genericStruct.StructName, monomorphizedFields, new List<string>(), cacheKey,
            genericStruct.Attributes, genericStruct.WhereClause, typeArgs, isUnion: genericStruct.IsUnion);
    }

    private static IrType SubstituteType(IrType type, Dictionary<string, IrType> substitutions)
    {
        // Direct generic parameter substitution (e.g., T -> i32)
        if (type is IrGenericType genericType)
        {
            if (substitutions.TryGetValue(genericType.ParameterName, out var substitution))
            {
                return substitution;
            }
            return type; // Unbound generic parameter, return as-is
        }

        // Legacy check: struct with single generic parameter used as a type parameter placeholder
        // This handles cases where IrStructType is used instead of IrGenericType
        if (type is IrStructType structType && structType.GenericParameters.Count == 1 &&
            structType.Fields is [] && // Only if it's a placeholder struct
            substitutions.ContainsKey(structType.GenericParameters[0]))
        {
            return substitutions[structType.GenericParameters[0]];
        }

        // Handle pointer types (e.g., *T -> *i32)
        if (type is IrPointerType pointerType)
        {
            var substitutedInner = SubstituteType(pointerType.PointeeType, substitutions);
            if (substitutedInner != pointerType.PointeeType)
            {
                return new IrPointerType(substitutedInner);
            }
            return type;
        }

        // Handle reference types (e.g., &T -> &i32)
        if (type is IrReferenceType refType)
        {
            var substitutedInner = SubstituteType(refType.PointeeType, substitutions);
            if (substitutedInner != refType.PointeeType)
            {
                return new IrReferenceType(substitutedInner);
            }
            return type;
        }

        // Handle mutable reference types (e.g., &var T -> &var i32)
        if (type is IrMutReferenceType mutRefType)
        {
            var substitutedInner = SubstituteType(mutRefType.PointeeType, substitutions);
            if (substitutedInner != mutRefType.PointeeType)
            {
                return new IrMutReferenceType(substitutedInner);
            }
            return type;
        }

        // Handle array types (e.g., [T; 10] -> [i32; 10])
        if (type is IrArrayType arrayType)
        {
            var substitutedElement = SubstituteType(arrayType.ElementType, substitutions);
            if (substitutedElement != arrayType.ElementType)
            {
                return new IrArrayType(substitutedElement, arrayType.Length);
            }
            return type;
        }

        // Handle nested generic struct types (e.g., Option<Vec<T>> -> Option<Vec<i32>>)
        // These have a CacheKey that contains the generic parameters
        if (type is IrStructType nestedStruct && nestedStruct.CacheKey != null && nestedStruct.CacheKey.Contains("<"))
        {
            // Check if any generic parameter from our substitutions appears in the CacheKey
            bool needsSubstitution = substitutions.Keys.Any(param => nestedStruct.CacheKey.Contains(param));
            if (needsSubstitution)
            {
                // Substitute in the CacheKey string
                string newCacheKey = nestedStruct.CacheKey;
                foreach (var (paramName, paramType) in substitutions)
                {
                    newCacheKey = SubstituteInCacheKey(newCacheKey, paramName, paramType.Name);
                }

                // Also substitute field types
                var substitutedFields = new List<IrStructField>();
                bool fieldsChanged = false;
                foreach (var field in nestedStruct.Fields)
                {
                    var substitutedFieldType = SubstituteType(field.Type, substitutions);
                    substitutedFields.Add(new IrStructField(field.Name, substitutedFieldType));
                    if (substitutedFieldType != field.Type)
                    {
                        fieldsChanged = true;
                    }
                }

                if (newCacheKey != nestedStruct.CacheKey || fieldsChanged)
                {
                    // Build typeArguments from the substitutions applied to the original type arguments
                    List<IrType>? newTypeArgs = null;
                    if (nestedStruct.TypeArguments != null && nestedStruct.TypeArguments.Count > 0)
                    {
                        newTypeArgs = nestedStruct.TypeArguments.Select(ta => SubstituteType(ta, substitutions)).ToList();
                    }
                    return new IrStructType(nestedStruct.StructName, substitutedFields, new List<string>(), newCacheKey, null, null, newTypeArgs);
                }
            }
        }

        // Handle nested generic enum types (e.g., Result<T, E> -> Result<i32, Error>)
        if (type is IrEnumType enumType && enumType.CacheKey != null && enumType.CacheKey.Contains("<"))
        {
            bool needsSubstitution = substitutions.Keys.Any(param => enumType.CacheKey.Contains(param));
            if (needsSubstitution)
            {
                string newCacheKey = enumType.CacheKey;
                foreach (var (paramName, paramType) in substitutions)
                {
                    newCacheKey = SubstituteInCacheKey(newCacheKey, paramName, paramType.Name);
                }

                // Also substitute in variants' associated data types
                var substitutedVariants = new List<IrEnumVariant>();
                bool variantsChanged = false;
                foreach (var variant in enumType.Variants)
                {
                    var substitutedData = new List<IrType>();
                    bool dataChanged = false;
                    foreach (var dataType in variant.AssociatedData)
                    {
                        var substitutedType = SubstituteType(dataType, substitutions);
                        substitutedData.Add(substitutedType);
                        if (substitutedType != dataType)
                        {
                            dataChanged = true;
                        }
                    }
                    substitutedVariants.Add(new IrEnumVariant(variant.Name, variant.Tag, substitutedData));
                    if (dataChanged)
                    {
                        variantsChanged = true;
                    }
                }

                if (newCacheKey != enumType.CacheKey || variantsChanged)
                {
                    var newEnumType = new IrEnumType(enumType.EnumName, substitutedVariants, new List<string>());
                    newEnumType.CacheKey = newCacheKey;
                    return newEnumType;
                }
            }
        }

        return type;
    }

    /// <summary>
    /// Substitute a type parameter in a CacheKey string, handling nested angle brackets correctly.
    /// For example: "Option<Vec<T>>" with T->i32 becomes "Option<Vec<i32>>"
    /// </summary>
    private static string SubstituteInCacheKey(string cacheKey, string paramName, string replacement)
    {
        // Simple case-sensitive replace, but we need to be careful about partial matches
        // e.g., "T" should not match "Type" or "TResult"
        var result = new System.Text.StringBuilder();
        int i = 0;
        while (i < cacheKey.Length)
        {
            // Check if we're at the start of the parameter name
            if (i + paramName.Length <= cacheKey.Length &&
                cacheKey.Substring(i, paramName.Length) == paramName)
            {
                // Check that it's not part of a larger identifier
                bool isWordStart = i == 0 || !char.IsLetterOrDigit(cacheKey[i - 1]) && cacheKey[i - 1] != '_';
                bool isWordEnd = i + paramName.Length == cacheKey.Length ||
                                 (!char.IsLetterOrDigit(cacheKey[i + paramName.Length]) && cacheKey[i + paramName.Length] != '_');

                if (isWordStart && isWordEnd)
                {
                    result.Append(replacement);
                    i += paramName.Length;
                    continue;
                }
            }
            result.Append(cacheKey[i]);
            i++;
        }
        return result.ToString();
    }
}

/// <summary>
/// Represents a field within a struct
/// </summary>
public class IrStructField
{
    public string Name { get; set; }
    public IrType Type { get; set; }
    public int Offset { get; set; }  // Offset in bytes from start of struct

    public IrStructField(string name, IrType type)
    {
        Name = name;
        Type = type;
    }
}

/// <summary>
/// Function address value (for taking address of a function)
/// </summary>
public class IrFunctionAddress : IrValue
{
    public string FunctionName { get; set; }

    public IrFunctionAddress(string functionName, IrFunctionPointerType type) : base(type)
    {
        FunctionName = functionName;
    }
}

/// <summary>
/// Borrow value - represents a reference to another value
/// Created by & or &var expressions
/// </summary>
public class IrBorrowValue : IrValue
{
    public IrValue BorrowedValue { get; set; }
    public bool IsMutable { get; set; }

    public IrBorrowValue(IrValue borrowedValue, IrType referenceType, bool isMutable) : base(referenceType)
    {
        BorrowedValue = borrowedValue;
        IsMutable = isMutable;
    }
}

/// <summary>
/// Dereference value - represents dereferencing a pointer or reference
/// Created by *expr expressions
/// </summary>
public class IrDereferenceValue : IrValue
{
    public IrValue PointerValue { get; set; }
    public bool IsVolatile { get; set; }

    public IrDereferenceValue(IrValue pointerValue, IrType pointeeType, bool isVolatile = false) : base(pointeeType)
    {
        PointerValue = pointerValue;
        IsVolatile = isVolatile;
    }
}

/// <summary>
/// Cast value - represents a type cast operation
/// Created by (type)expr expressions
/// Supports nested casts: (T1)(T2)expr
/// </summary>
public class IrCastValue : IrValue
{
    public IrValue Value { get; set; }
    public IrType SourceType { get; set; }

    public IrCastValue(IrValue value, IrType sourceType, IrType targetType) : base(targetType)
    {
        Value = value;
        SourceType = sourceType;
    }
}

/// <summary>
/// Field reference value - represents an lvalue reference to a struct field
/// Used when we need to pass &struct.field to a function without loading the field value first
/// This avoids creating a copy of the field when we just need its address
/// </summary>
public class IrFieldReference : IrValue
{
    public IrValue Struct { get; set; }
    public string FieldName { get; set; }

    public IrFieldReference(IrValue structValue, string fieldName, IrType fieldType) : base(fieldType)
    {
        Struct = structValue;
        FieldName = fieldName;
    }
}

/// <summary>
/// Indexed field access - represents array[index].field without creating intermediate struct copy
/// This is critical for 68k to avoid creating misaligned struct temporaries
/// Example: self.entries[i].nm_Type becomes IrIndexedFieldAccess instead of IrIndexAccess + IrMemberAccess
/// </summary>
public class IrIndexedFieldAccess : IrValue
{
    public IrValue Array { get; set; }
    public IrValue Index { get; set; }
    public string FieldName { get; set; }
    public int FieldOffset { get; set; }

    public IrIndexedFieldAccess(IrValue array, IrValue index, string fieldName, int fieldOffset, IrType fieldType)
        : base(fieldType)
    {
        Array = array;
        Index = index;
        FieldName = fieldName;
        FieldOffset = fieldOffset;
    }
}

/// <summary>
/// Tuple element access - represents accessing an element by index from a tuple
/// Example: accessing element 0 from tuple (u8, u8, u8)
/// </summary>
public class IrTupleElementAccess : IrValue
{
    public IrValue Tuple { get; set; }
    public int ElementIndex { get; set; }

    public IrTupleElementAccess(IrValue tupleValue, int elementIndex, IrType elementType) : base(elementType)
    {
        Tuple = tupleValue;
        ElementIndex = elementIndex;
    }
}

/// <summary>
/// Indirect function call through a function pointer
/// </summary>
public class IrIndirectCall : IrInstruction
{
    public IrValue FunctionPointer { get; set; }
    public List<IrValue> Arguments { get; } = new();
    public IrType ReturnType { get; set; }
    public string? ResultName { get; set; }  // null for void functions

    public IrIndirectCall(IrValue functionPointer, IrType returnType, string? resultName = null)
    {
        FunctionPointer = functionPointer;
        ReturnType = returnType;
        ResultName = resultName;
    }
}

/// <summary>
/// Closure creation instruction - allocates environment and creates fat pointer
/// </summary>
public class IrCreateClosure : IrInstruction
{
    public string ResultName { get; set; }
    public IrClosureType ClosureType { get; set; }
    public string GeneratedFunctionName { get; set; }  // Name of the generated closure function
    public List<(string VarName, IrValue Value, CaptureMode Mode)> CapturedValues { get; } = new();

    public IrCreateClosure(string resultName, IrClosureType closureType, string generatedFunctionName)
    {
        ResultName = resultName;
        ClosureType = closureType;
        GeneratedFunctionName = generatedFunctionName;
    }
}

/// <summary>
/// Closure invocation - extracts function and environment pointers, calls with hidden env parameter
/// </summary>
public class IrInvokeClosure : IrInstruction
{
    public IrValue Closure { get; set; }  // The closure fat pointer
    public List<IrValue> Arguments { get; } = new();
    public IrType ReturnType { get; set; }
    public string? ResultName { get; set; }  // null for void closures

    public IrInvokeClosure(IrValue closure, IrType returnType, string? resultName = null)
    {
        Closure = closure;
        ReturnType = returnType;
        ResultName = resultName;
    }
}

/// <summary>
/// Load a captured variable from closure environment
/// Used inside generated closure functions to access captures
/// </summary>
public class IrLoadCapture : IrInstruction
{
    public string ResultName { get; set; }
    public string CaptureName { get; set; }
    public IrType CaptureType { get; set; }
    public int FieldOffset { get; set; }  // Offset in environment struct
    public CaptureMode Mode { get; set; }

    public IrLoadCapture(string resultName, string captureName, IrType captureType, int fieldOffset, CaptureMode mode)
    {
        ResultName = resultName;
        CaptureName = captureName;
        CaptureType = captureType;
        FieldOffset = fieldOffset;
        Mode = mode;
    }
}

/// <summary>
/// Store to a captured variable in closure environment (for mutable captures)
/// </summary>
public class IrStoreCapture : IrInstruction
{
    public string CaptureName { get; set; }
    public IrValue Value { get; set; }
    public int FieldOffset { get; set; }  // Offset in environment struct

    public IrStoreCapture(string captureName, IrValue value, int fieldOffset)
    {
        CaptureName = captureName;
        Value = value;
        FieldOffset = fieldOffset;
    }
}

/// <summary>
/// Array literal value (for initialization)
/// </summary>
public class IrArrayLiteral : IrValue
{
    public List<IrValue> Elements { get; } = new();

    public IrArrayLiteral(IrArrayType type) : base(type)
    {
    }
}

/// <summary>
/// Array index access instruction
/// Gets the address or value of array[index]
/// </summary>
public class IrIndexAccess : IrInstruction
{
    public string ResultName { get; set; }
    public IrValue Array { get; set; }
    public IrValue Index { get; set; }
    public IrType ElementType { get; set; }

    public IrIndexAccess(string resultName, IrValue array, IrValue index, IrType elementType)
    {
        ResultName = resultName;
        Array = array;
        Index = index;
        ElementType = elementType;
    }
}

/// <summary>
/// Struct member access instruction - loads a field from a struct
/// </summary>
public class IrMemberAccess : IrInstruction
{
    public string ResultName { get; set; }
    public IrValue Struct { get; set; }
    public string FieldName { get; set; }
    public IrType FieldType { get; set; }
    public int FieldOffset { get; set; }  // Offset in bytes from struct base

    public IrMemberAccess(string resultName, IrValue structValue, string fieldName, IrType fieldType, int fieldOffset)
    {
        ResultName = resultName;
        Struct = structValue;
        FieldName = fieldName;
        FieldType = fieldType;
        FieldOffset = fieldOffset;
    }
}

/// <summary>
/// Struct member store instruction - stores a value to a struct field
/// </summary>
public class IrMemberStore : IrInstruction
{
    public IrValue Struct { get; set; }
    public string FieldName { get; set; }
    public int FieldOffset { get; set; }
    public IrValue Value { get; set; }

    public IrMemberStore(IrValue structValue, string fieldName, int fieldOffset, IrValue value)
    {
        Struct = structValue;
        FieldName = fieldName;
        FieldOffset = fieldOffset;
        Value = value;
    }
}

/// <summary>
/// Store value to array[index]
/// </summary>
public class IrIndexStore : IrInstruction
{
    public IrValue Array { get; set; }
    public IrValue Index { get; set; }
    public IrValue Value { get; set; }

    public IrIndexStore(IrValue array, IrValue index, IrValue value)
    {
        Array = array;
        Index = index;
        Value = value;
    }
}

/// <summary>
/// Store value to array[index].field - specialized instruction for indexed field assignment
/// This avoids creating a temporary copy of the struct element
/// Example: menu_array[i].nm_Type = value
/// </summary>
public class IrIndexedFieldStore : IrInstruction
{
    public IrValue Array { get; set; }
    public IrValue Index { get; set; }
    public string FieldName { get; set; }
    public int FieldOffset { get; set; }
    public IrValue Value { get; set; }

    public IrIndexedFieldStore(IrValue array, IrValue index, string fieldName, int fieldOffset, IrValue value)
    {
        Array = array;
        Index = index;
        FieldName = fieldName;
        FieldOffset = fieldOffset;
        Value = value;
    }
}

/// <summary>
/// Memory section for static variables (Amiga-specific)
/// </summary>
public enum MemorySection
{
    /// Default - placed in any available memory (usually Fast RAM)
    Default,
    /// Chip RAM - required for DMA (Copper, Blitter, sprites, audio)
    Chip,
    /// Fast RAM - CPU-only access, faster than chip RAM
    Fast
}

/// <summary>
/// Static variable - global variable with fixed address and lifetime
/// Can be immutable or mutable (requires unsafe to access if mut)
/// </summary>
public class IrStaticVariable
{
    public string Name { get; set; }
    public IrType Type { get; set; }
    public Visibility Visibility { get; set; }
    public bool IsMutable { get; set; }
    public IrValue InitialValue { get; set; }
    /// <summary>
    /// Memory section for Amiga DMA requirements (Copper lists, Blitter, sprites, audio)
    /// </summary>
    public MemorySection Section { get; set; } = MemorySection.Default;

    public IrStaticVariable(string name, IrType type, Visibility visibility, bool isMutable, IrValue initialValue, MemorySection section = MemorySection.Default)
    {
        Name = name;
        Type = type;
        Visibility = visibility;
        IsMutable = isMutable;
        InitialValue = initialValue;
        Section = section;
    }
}

/// <summary>
/// External variable - declared with 'extern var', resolved at link time
/// Used for library bases, hardware registers, and FFI
/// </summary>
public class IrExternalVariable
{
    public string Name { get; set; }
    public IrType Type { get; set; }
    public long? Address { get; set; }  // Optional fixed address (e.g., hardware registers)

    public IrExternalVariable(string name, IrType type, long? address = null)
    {
        Name = name;
        Type = type;
        Address = address;
    }
}

/// <summary>
/// Global variable value reference - refers to a static or extern variable
/// </summary>
public class IrGlobalVariable : IrValue
{
    public string Name { get; set; }

    public IrGlobalVariable(string name, IrType type) : base(type)
    {
        Name = name;
    }
}

/// <summary>
/// Copper list data - pre-computed copper list words for chip RAM
/// Each copper instruction is 2 words (4 bytes):
/// - WAIT: (VP<<8 | HP<<1 | 1, $fffe)  - Wait for beam position
/// - MOVE: (register offset, value)     - Write value to custom chip register
/// - SKIP: (VP<<8 | HP<<1 | 1, $ffff)   - Skip next instruction if beam past position
/// - END:  ($ffff, $fffe)               - Terminate copper list
/// </summary>
public class IrCopperListData : IrValue
{
    /// <summary>
    /// Label for this copper list in the data section
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Pre-computed copper list words (pairs of u16 values)
    /// Each instruction is 2 words: instruction word + data word
    /// </summary>
    public List<ushort> Words { get; } = new();

    /// <summary>
    /// Whether this copper list requires chip RAM placement
    /// </summary>
    public bool RequiresChipRam { get; set; } = true;

    public IrCopperListData(string label, List<ushort> words) : base(new IrPointerType(IrIntType.U16))
    {
        Label = label;
        Words = words;
    }
}

/// <summary>
/// Blitter operation data - pre-computed blitter register values
/// </summary>
public class IrBlitterOpData : IrValue
{
    /// <summary>
    /// BLTCON0 register value (control, minterms, channel enables)
    /// </summary>
    public ushort BltCon0 { get; set; }

    /// <summary>
    /// BLTCON1 register value (fill mode, exclusive fill, etc.)
    /// </summary>
    public ushort BltCon1 { get; set; }

    /// <summary>
    /// BLTSIZE register value (height << 6 | width_words)
    /// </summary>
    public ushort BltSize { get; set; }

    /// <summary>
    /// Source A pointer expression
    /// </summary>
    public IrValue? SourceA { get; set; }

    /// <summary>
    /// Source B pointer expression
    /// </summary>
    public IrValue? SourceB { get; set; }

    /// <summary>
    /// Source C pointer expression
    /// </summary>
    public IrValue? SourceC { get; set; }

    /// <summary>
    /// Destination pointer expression
    /// </summary>
    public IrValue? Destination { get; set; }

    /// <summary>
    /// Source A modulo
    /// </summary>
    public short ModuloA { get; set; }

    /// <summary>
    /// Source B modulo
    /// </summary>
    public short ModuloB { get; set; }

    /// <summary>
    /// Source C modulo
    /// </summary>
    public short ModuloC { get; set; }

    /// <summary>
    /// Destination modulo
    /// </summary>
    public short ModuloD { get; set; }

    /// <summary>
    /// Whether to wait for blitter completion after operation
    /// </summary>
    public bool WaitForCompletion { get; set; } = true;

    public IrBlitterOpData() : base(IrTupleType.Unit)
    {
    }
}

/// <summary>
/// IR instruction for writing to Amiga custom chip hardware registers.
/// Used by lowering passes to emit direct hardware register access.
/// </summary>
public class IrHardwareWrite : IrInstruction
{
    /// <summary>
    /// Register address (absolute, e.g., 0xDFF040 for BLTCON0)
    /// </summary>
    public uint RegisterAddress { get; set; }

    /// <summary>
    /// Value to write (either constant or IrValue for runtime values)
    /// </summary>
    public IrValue Value { get; set; }

    /// <summary>
    /// Size of write operation in bytes (2 for word, 4 for long)
    /// </summary>
    public int WriteSize { get; set; } = 2;

    /// <summary>
    /// Whether this is a volatile write (always true for hardware)
    /// </summary>
    public bool IsVolatile { get; set; } = true;

    public IrHardwareWrite(uint registerAddress, IrValue value, int writeSize = 2)
    {
        RegisterAddress = registerAddress;
        Value = value;
        WriteSize = writeSize;
    }
}

/// <summary>
/// IR instruction for reading from Amiga custom chip hardware registers.
/// Used for polling hardware status (e.g., blitter busy bit).
/// </summary>
public class IrHardwareRead : IrInstruction
{
    /// <summary>
    /// Variable name to store the read result
    /// </summary>
    public string ResultName { get; set; }

    /// <summary>
    /// Register address (absolute, e.g., 0xDFF002 for DMACONR)
    /// </summary>
    public uint RegisterAddress { get; set; }

    /// <summary>
    /// Size of read operation in bytes (2 for word, 4 for long)
    /// </summary>
    public int ReadSize { get; set; } = 2;

    /// <summary>
    /// Type of the result
    /// </summary>
    public IrType ResultType { get; set; }

    /// <summary>
    /// Whether this is a volatile read (always true for hardware)
    /// </summary>
    public bool IsVolatile { get; set; } = true;

    public IrHardwareRead(string resultName, uint registerAddress, int readSize = 2)
    {
        ResultName = resultName;
        RegisterAddress = registerAddress;
        ReadSize = readSize;
        ResultType = readSize == 4 ? IrIntType.U32 : IrIntType.U16;
    }
}

/// <summary>
/// Represents a 68k CPU register for inline assembly.
/// </summary>
public enum M68kRegister
{
    // Data registers
    D0, D1, D2, D3, D4, D5, D6, D7,
    // Address registers
    A0, A1, A2, A3, A4, A5, A6,
    // Special - no register specified (use calling convention)
    None
}

/// <summary>
/// Kind of compile-time constant in an asm use() clause.
/// </summary>
public enum AsmUseKind
{
    /// <summary>sizeof(Type) - size of a type in bytes</summary>
    SizeOf,
    /// <summary>offsetof(Type, field) - byte offset of a field in a struct</summary>
    OffsetOf,
    /// <summary>alignof(Type) - alignment requirement of a type</summary>
    AlignOf,
    /// <summary>const CONSTANT - value of a compile-time constant</summary>
    Constant
}

/// <summary>
/// Represents a compile-time constant available in an inline assembly block via use() clause.
/// The Novus compiler resolves these values at compile time and substitutes literal numbers
/// into the assembly string. VBCC never sees symbolic names.
/// </summary>
public class IrAsmUseItem
{
    /// <summary>
    /// Kind of use item (sizeof, offsetof, alignof, or constant)
    /// </summary>
    public AsmUseKind Kind { get; set; }

    /// <summary>
    /// Target type for sizeof/offsetof/alignof
    /// </summary>
    public IrType? TargetType { get; set; }

    /// <summary>
    /// Field name for offsetof
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// Constant name for const use items
    /// </summary>
    public string? ConstantName { get; set; }

    /// <summary>
    /// The resolved numeric value (computed at compile time)
    /// </summary>
    public long ResolvedValue { get; set; }

    /// <summary>
    /// The substitution pattern name used in the assembly body.
    /// For sizeof(Player) -> "sizeof_Player"
    /// For offsetof(Player, health) -> "offsetof_Player_health"
    /// </summary>
    public string SubstitutionName { get; set; } = "";

    /// <summary>
    /// Create a sizeof use item
    /// </summary>
    public static IrAsmUseItem SizeOf(IrType type, long size, string typeName)
    {
        return new IrAsmUseItem
        {
            Kind = AsmUseKind.SizeOf,
            TargetType = type,
            ResolvedValue = size,
            SubstitutionName = $"sizeof_{typeName}"
        };
    }

    /// <summary>
    /// Create an offsetof use item
    /// </summary>
    public static IrAsmUseItem OffsetOf(IrType type, string fieldName, long offset, string typeName)
    {
        return new IrAsmUseItem
        {
            Kind = AsmUseKind.OffsetOf,
            TargetType = type,
            FieldName = fieldName,
            ResolvedValue = offset,
            SubstitutionName = $"offsetof_{typeName}_{fieldName}"
        };
    }

    /// <summary>
    /// Create an alignof use item
    /// </summary>
    public static IrAsmUseItem AlignOf(IrType type, long alignment, string typeName)
    {
        return new IrAsmUseItem
        {
            Kind = AsmUseKind.AlignOf,
            TargetType = type,
            ResolvedValue = alignment,
            SubstitutionName = $"alignof_{typeName}"
        };
    }

    /// <summary>
    /// Create a constant use item
    /// </summary>
    public static IrAsmUseItem Const(string name, long value)
    {
        return new IrAsmUseItem
        {
            Kind = AsmUseKind.Constant,
            ConstantName = name,
            ResolvedValue = value,
            SubstitutionName = name
        };
    }
}

/// <summary>
/// Represents an input parameter to an inline assembly block.
/// </summary>
public class IrAsmInput
{
    /// <summary>
    /// Name of the parameter (used for %param substitution in asm)
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The value to pass as input (can be a variable reference, expression result, etc.)
    /// </summary>
    public IrValue Value { get; set; }

    /// <summary>
    /// The type of the input
    /// </summary>
    public IrType Type { get; set; }

    /// <summary>
    /// Optional explicit register binding (None = use calling convention inference)
    /// </summary>
    public M68kRegister Register { get; set; } = M68kRegister.None;

    /// <summary>
    /// Whether this is an address-of binding (&amp;var in reg)
    /// When true, the address of the variable is passed instead of its value.
    /// </summary>
    public bool IsAddressOf { get; set; }

    /// <summary>
    /// Whether this is a mutable binding (mut var in reg out reg)
    /// When true, the variable can be modified by the assembly block.
    /// </summary>
    public bool IsMutable { get; set; }

    /// <summary>
    /// Output register for writeback (used with IsMutable)
    /// After the asm block executes, the value in this register is written back to the variable.
    /// </summary>
    public M68kRegister? OutputRegister { get; set; }

    /// <summary>
    /// Variable name for writeback (for code generation)
    /// </summary>
    public string? WritebackVariable { get; set; }

    public IrAsmInput(string name, IrValue value, IrType type, M68kRegister register = M68kRegister.None)
    {
        Name = name;
        Value = value;
        Type = type;
        Register = register;
    }

    /// <summary>
    /// Get the register name as a string (e.g., "d0", "a1")
    /// </summary>
    public string GetRegisterName()
    {
        return Register switch
        {
            M68kRegister.D0 => "d0",
            M68kRegister.D1 => "d1",
            M68kRegister.D2 => "d2",
            M68kRegister.D3 => "d3",
            M68kRegister.D4 => "d4",
            M68kRegister.D5 => "d5",
            M68kRegister.D6 => "d6",
            M68kRegister.D7 => "d7",
            M68kRegister.A0 => "a0",
            M68kRegister.A1 => "a1",
            M68kRegister.A2 => "a2",
            M68kRegister.A3 => "a3",
            M68kRegister.A4 => "a4",
            M68kRegister.A5 => "a5",
            M68kRegister.A6 => "a6",
            _ => ""
        };
    }
}

/// <summary>
/// Represents an output value from an inline assembly block.
/// </summary>
public class IrAsmOutput
{
    /// <summary>
    /// The type of the output value
    /// </summary>
    public IrType Type { get; set; }

    /// <summary>
    /// The register containing the output value
    /// </summary>
    public M68kRegister Register { get; set; }

    public IrAsmOutput(IrType type, M68kRegister register)
    {
        Type = type;
        Register = register;
    }

    /// <summary>
    /// Get the register name as a string (e.g., "d0", "a1")
    /// </summary>
    public string GetRegisterName()
    {
        return Register switch
        {
            M68kRegister.D0 => "d0",
            M68kRegister.D1 => "d1",
            M68kRegister.D2 => "d2",
            M68kRegister.D3 => "d3",
            M68kRegister.D4 => "d4",
            M68kRegister.D5 => "d5",
            M68kRegister.D6 => "d6",
            M68kRegister.D7 => "d7",
            M68kRegister.A0 => "a0",
            M68kRegister.A1 => "a1",
            M68kRegister.A2 => "a2",
            M68kRegister.A3 => "a3",
            M68kRegister.A4 => "a4",
            M68kRegister.A5 => "a5",
            M68kRegister.A6 => "a6",
            _ => "d0"  // Default to d0 for return values
        };
    }
}

/// <summary>
/// IR instruction representing an inline assembly block.
/// Inline assembly allows embedding 68k assembly instructions directly in Novus code
/// with type-safe parameter binding and explicit register control.
/// </summary>
public class IrInlineAsm : IrInstruction
{
    /// <summary>
    /// Input parameters to the assembly block
    /// </summary>
    public List<IrAsmInput> Inputs { get; set; } = new();

    /// <summary>
    /// Output values from the assembly block (empty for void return)
    /// </summary>
    public List<IrAsmOutput> Outputs { get; set; } = new();

    /// <summary>
    /// List of registers that are modified by the assembly but not used as outputs.
    /// The compiler will save/restore callee-saved registers (d2-d7, a2-a6) in this list.
    /// </summary>
    public List<M68kRegister> Clobbers { get; set; } = new();

    /// <summary>
    /// Compile-time constants available in the assembly body via use() clause.
    /// Examples: sizeof(Type), offsetof(Type, field), alignof(Type), const CONSTANT
    /// </summary>
    public List<IrAsmUseItem> UseItems { get; set; } = new();

    /// <summary>
    /// Whether the assembly modifies memory (acts as a memory barrier)
    /// </summary>
    public bool ClobbersMemory { get; set; }

    /// <summary>
    /// Whether the assembly block is volatile (prevents reordering/elimination)
    /// </summary>
    public bool IsVolatile { get; set; }

    /// <summary>
    /// The raw assembly instructions (each string is one instruction line)
    /// May contain %param placeholders that will be substituted with register names.
    /// </summary>
    public List<string> Instructions { get; set; } = new();

    /// <summary>
    /// Variable name to store the result (if single output), null if void or multi-output
    /// </summary>
    public string? ResultName { get; set; }

    /// <summary>
    /// Variable names for multi-value returns (e.g., tuple destructuring)
    /// </summary>
    public List<string>? MultiResultNames { get; set; }

    public IrInlineAsm()
    {
    }

    /// <summary>
    /// Get the return type of this assembly block.
    /// Returns void if no outputs, the single output type if one output,
    /// or a tuple type if multiple outputs.
    /// </summary>
    public IrType GetReturnType()
    {
        if (Outputs is [])
            return IrVoidType.Instance;
        if (Outputs.Count == 1)
            return Outputs[0].Type;
        return new IrTupleType(Outputs.Select(o => o.Type).ToList());
    }

    /// <summary>
    /// Get the set of callee-saved registers that need to be saved/restored.
    /// Only returns registers from d2-d7 and a2-a6 that appear in the clobber list.
    /// </summary>
    public IEnumerable<M68kRegister> GetCalleeSavedClobbers()
    {
        var calleeSaved = new HashSet<M68kRegister>
        {
            M68kRegister.D2, M68kRegister.D3, M68kRegister.D4,
            M68kRegister.D5, M68kRegister.D6, M68kRegister.D7,
            M68kRegister.A2, M68kRegister.A3, M68kRegister.A4,
            M68kRegister.A5, M68kRegister.A6
        };
        return Clobbers.Where(r => calleeSaved.Contains(r));
    }

    /// <summary>
    /// Perform parameter and use item substitution on an assembly instruction.
    /// Replaces %param_name with the actual register name.
    /// Replaces sizeof_Type, offsetof_Type_Field, etc. with their numeric values.
    /// </summary>
    public string SubstituteParameters(string instruction)
    {
        var result = instruction;

        // Substitute input parameters (%param_name -> register)
        foreach (var input in Inputs)
        {
            var regName = input.GetRegisterName();
            if (!string.IsNullOrEmpty(regName))
            {
                result = result.Replace($"%{input.Name}", regName);
            }
        }

        // Substitute use items (sizeof_Type, offsetof_Type_Field, etc. -> numeric value)
        foreach (var useItem in UseItems)
        {
            // Handle both immediate (#sizeof_X) and displacement (sizeof_X(a0)) forms
            // For immediate: #sizeof_Player -> #44
            // For displacement: offsetof_Player_position(a0) -> 4(a0)
            result = result.Replace($"#{useItem.SubstitutionName}", $"#{useItem.ResolvedValue}");
            result = result.Replace($"{useItem.SubstitutionName}(", $"{useItem.ResolvedValue}(");
            // Also handle bare substitution (e.g., move.l sizeof_Player,d0)
            result = result.Replace(useItem.SubstitutionName, useItem.ResolvedValue.ToString());
        }

        return result;
    }

    /// <summary>
    /// Parse a register name string to M68kRegister enum.
    /// </summary>
    public static M68kRegister ParseRegister(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "d0" => M68kRegister.D0,
            "d1" => M68kRegister.D1,
            "d2" => M68kRegister.D2,
            "d3" => M68kRegister.D3,
            "d4" => M68kRegister.D4,
            "d5" => M68kRegister.D5,
            "d6" => M68kRegister.D6,
            "d7" => M68kRegister.D7,
            "a0" => M68kRegister.A0,
            "a1" => M68kRegister.A1,
            "a2" => M68kRegister.A2,
            "a3" => M68kRegister.A3,
            "a4" => M68kRegister.A4,
            "a5" => M68kRegister.A5,
            "a6" => M68kRegister.A6,
            _ => M68kRegister.None
        };
    }

    /// <summary>
    /// Check if a register is a data register (d0-d7)
    /// </summary>
    public static bool IsDataRegister(M68kRegister reg)
    {
        return reg >= M68kRegister.D0 && reg <= M68kRegister.D7;
    }

    /// <summary>
    /// Check if a register is an address register (a0-a6)
    /// </summary>
    public static bool IsAddressRegister(M68kRegister reg)
    {
        return reg >= M68kRegister.A0 && reg <= M68kRegister.A6;
    }
}
