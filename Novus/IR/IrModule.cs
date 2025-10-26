namespace Novus.IR;

/// <summary>
/// Represents a complete Novus compilation unit
/// </summary>
public class IrModule
{
    public List<IrFunction> Functions { get; } = new();
    public List<IrEnumType> Enums { get; } = new();
    public Dictionary<string, IrMonomorphizedType> MonomorphizedTypes { get; } = new();

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
}

/// <summary>
/// Represents a function in the IR
/// </summary>
public class IrFunction
{
    public string Name { get; set; }
    public IrType ReturnType { get; set; }
    public bool IsPublic { get; set; }  // true if 'pub' keyword used
    public bool IsExtern { get; set; }  // true if 'extern' keyword used
    public List<IrParameter> Parameters { get; } = new();
    public List<IrLocalVariable> LocalVariables { get; } = new();
    public List<IrBasicBlock> BasicBlocks { get; } = new();

    public IrFunction(string name, IrType returnType, bool isPublic = false, bool isExtern = false)
    {
        Name = name;
        ReturnType = returnType;
        IsPublic = isPublic;
        IsExtern = isExtern;
    }

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

    public IrParameter(string name, IrType type)
    {
        Name = name;
        Type = type;
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
    public List<IrInstruction> Instructions { get; } = new();

    public IrBasicBlock(string label)
    {
        Label = label;
    }

    public void AddInstruction(IrInstruction instruction)
    {
        Instructions.Add(instruction);
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
/// String literal value (pointer to null-terminated string in data section)
/// </summary>
public class IrStringLiteral : IrValue
{
    public string Value { get; set; }
    public string Label { get; set; }  // Unique label for this string in data section

    public IrStringLiteral(string value, string label) : base(new IrPointerType(IrIntType.U8))
    {
        Value = value;
        Label = label;
    }
}

/// <summary>
/// Variable reference
/// </summary>
public class IrVariable : IrValue
{
    public string Name { get; set; }

    public IrVariable(string name, IrType type) : base(type)
    {
        Name = name;
    }
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
/// Struct type - composite type with named fields
/// </summary>
public class IrStructType : IrType
{
    public string StructName { get; }
    public List<IrStructField> Fields { get; }
    private int? _cachedSize;

    public IrStructType(string structName, List<IrStructField> fields)
    {
        StructName = structName;
        Fields = fields;
    }

    public override int SizeInBytes
    {
        get
        {
            if (_cachedSize.HasValue)
                return _cachedSize.Value;

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
    }

    public override string Name => StructName;

    public IrStructField? GetField(string fieldName)
    {
        return Fields.FirstOrDefault(f => f.Name == fieldName);
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
