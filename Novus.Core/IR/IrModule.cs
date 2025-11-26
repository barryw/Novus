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
    public List<IrEnumType> Enums { get; } = new();
    public List<IrStructType> Structs { get; } = new();
    public List<IrTrait> Traits { get; } = new();
    public List<IrTraitImpl> TraitImpls { get; } = new();
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

    public void AddFunction(IrFunction function)
    {
        Functions.Add(function);
    }

    public void AddEnum(IrEnumType enumType)
    {
        Enums.Add(enumType);
    }

    public IrEnumType? GetEnum(string name)
    {
        return Enums.FirstOrDefault(e => e.EnumName == name);
    }

    public void AddTrait(IrTrait trait)
    {
        Traits.Add(trait);
    }

    public IrTrait? GetTrait(string name)
    {
        return Traits.FirstOrDefault(t => t.TraitName == name);
    }

    public void AddTraitImpl(IrTraitImpl traitImpl)
    {
        TraitImpls.Add(traitImpl);

        // If this is a Drop implementation, mark the type as implementing Drop
        if (traitImpl.TraitName == "Drop" && traitImpl.ImplementingType is IrStructType structType)
        {
            structType.ImplementsDrop = true;
        }
    }

    public IrTraitImpl? GetTraitImpl(string traitName, string typeName)
    {
        return TraitImpls.FirstOrDefault(ti => ti.TraitName == traitName && ti.TypeName == typeName);
    }

    /// <summary>
    /// Find trait implementation for a type that has a specific method
    /// Returns the mangled method name if found
    /// </summary>
    public string? FindTraitMethod(string typeName, string methodName)
    {
        // Look through all trait implementations for this type
        foreach (var traitImpl in TraitImpls.Where(ti => ti.TypeName == typeName))
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
        // Look through all trait implementations for this type
        foreach (var traitImpl in TraitImpls.Where(ti => ti.TypeName == typeName))
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
    /// Check if a type implements the Drop trait
    /// </summary>
    public bool TypeImplementsDrop(string typeName)
    {
        return TraitImpls.Any(ti => ti.TraitName == "Drop" && ti.TypeName == typeName);
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
        // Only struct types can implement Drop (primitives, pointers, etc. don't need cleanup)
        return false;
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
    public List<IrParameter> Parameters { get; } = new();
    public List<IrLocalVariable> LocalVariables { get; } = new();
    public List<IrBasicBlock> BasicBlocks { get; } = new();
    public List<IrBasicBlock> DeferredBlocks { get; } = new();  // Blocks to execute on function exit (LIFO)
    public List<string> GenericParameters { get; } = new();  // Generic type parameters (e.g., ["T", "U"])
    public IrWhereClause? WhereClause { get; set; }  // Generic type constraints (e.g., where T: Sortable)

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

    public IrBasicBlock CreateBasicBlock(string label)
    {
        var block = new IrBasicBlock(label);
        BasicBlocks.Add(block);
        return block;
    }
}

/// <summary>
/// Represents a function parameter
/// </summary>
public class IrParameter
{
    public string Name { get; set; }
    public IrType Type { get; set; }
    public bool IsVariadic { get; set; }  // true if this is a variadic parameter (...)

    public IrParameter(string name, IrType type, bool isVariadic = false)
    {
        Name = name;
        Type = type;
        IsVariadic = isVariadic;
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
    public SourceLocation Location { get; set; }

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
    public SourceLocation Location { get; set; }

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
    public IrValue InitialValue { get; set; }

    public IrLocalDecl(string name, IrType type, bool isMutable, IrValue initialValue)
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

    public IrDereferenceStore(IrValue pointer, IrValue value)
    {
        Pointer = pointer;
        Value = value;
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
/// String literal value - raw pointer to null-terminated string in data section
/// TODO: Will become Str type when implemented in stdlib
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
    public override string Name => $"&mut {PointeeType.Name}";
}

/// <summary>
/// Function pointer type
/// </summary>
public class IrFunctionPointerType : IrType
{
    public List<IrType> ParameterTypes { get; }
    public IrType ReturnType { get; }

    public IrFunctionPointerType(List<IrType> parameterTypes, IrType returnType)
    {
        ParameterTypes = parameterTypes;
        ReturnType = returnType;
    }

    public override int SizeInBytes => 4; // Function pointers are 32-bit addresses on 68k
    public override string Name
    {
        get
        {
            var paramStr = ParameterTypes.Count > 0
                ? string.Join(", ", ParameterTypes.Select(p => p.Name))
                : "";
            var retStr = ReturnType is IrVoidType ? "" : $" -> {ReturnType.Name}";
            return $"fn({paramStr}){retStr}";
        }
    }
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
            if (ElementTypes.Count == 0)
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
            if (ElementTypes.Count == 0)
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
    // Thread-local stack to detect recursive type definitions
    [ThreadStatic]
    private static HashSet<string>? _sizingStack;

    public string StructName { get; }
    public List<IrStructField> Fields { get; }
    public List<string> GenericParameters { get; }  // Type parameter names (e.g., ["T"])
    public string? CacheKey { get; set; }  // Cache key for monomorphized types (e.g., "Vec<i32>")
    public Novus.SemanticAnalysis.AttributeCollection? Attributes { get; set; }  // Struct attributes (@library, @packed, etc.)
    public IrWhereClause? WhereClause { get; set; }  // Generic type constraints (e.g., where T: Sortable)
    public bool ImplementsDrop { get; set; }  // True if this type implements the Drop trait
    private int? _cachedSize;

    public IrStructType(string structName, List<IrStructField> fields, List<string>? genericParams = null, string? cacheKey = null, Novus.SemanticAnalysis.AttributeCollection? attributes = null, IrWhereClause? whereClause = null)
    {
        StructName = structName;
        Fields = fields;
        GenericParameters = genericParams ?? new List<string>();
        CacheKey = cacheKey;
        Attributes = attributes;
        WhereClause = whereClause;
    }

    public override int SizeInBytes
    {
        get
        {
            if (_cachedSize.HasValue)
                return _cachedSize.Value;

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
                // Calculate total size with alignment
                int size = 0;
                foreach (var field in Fields)
                {
                    // Align field to its natural alignment (for now, use field size as alignment)
                    int fieldSize = field.Type.SizeInBytes;
                int alignment = fieldSize switch
                {
                    1 => 1,  // byte-aligned
                    2 => 2,  // word-aligned
                    _ => 2   // word-aligned for everything else (68k prefers word alignment)
                };

                // Pad to alignment
                if (size % alignment != 0)
                    size += alignment - (size % alignment);

                    field.Offset = size;
                    size += fieldSize;
                }

                // Pad final struct size to word boundary (68k likes word-aligned structs)
                if (size % 2 != 0)
                    size++;

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
            if (GenericParameters.Count > 0)
                return $"{StructName}<{string.Join(", ", GenericParameters)}>";
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

        return new IrStructType(genericStruct.StructName, monomorphizedFields, new List<string>(), cacheKey);
    }

    private static IrType SubstituteType(IrType type, Dictionary<string, IrType> substitutions)
    {
        // If the type is a generic parameter, substitute it
        if (type is IrStructType structType && structType.GenericParameters.Count == 1 &&
            substitutions.ContainsKey(structType.GenericParameters[0]))
        {
            return substitutions[structType.GenericParameters[0]];
        }

        // TODO: Handle nested generic types like Option<Vec<T>>

        return type;
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
/// Created by & or &mut expressions
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

    public IrDereferenceValue(IrValue pointerValue, IrType pointeeType) : base(pointeeType)
    {
        PointerValue = pointerValue;
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

    public IrStaticVariable(string name, IrType type, Visibility visibility, bool isMutable, IrValue initialValue)
    {
        Name = name;
        Type = type;
        Visibility = visibility;
        IsMutable = isMutable;
        InitialValue = initialValue;
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
