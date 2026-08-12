namespace Novus.IR;

/// <summary>
/// Type interning system - ensures that structurally equal types share the same instance.
/// This makes type equality checks fast (reference equality) and reduces memory usage.
///
/// Usage:
///   var interner = new TypeInterner();
///   var ptrType1 = interner.GetPointerType(IrIntType.I32);
///   var ptrType2 = interner.GetPointerType(IrIntType.I32);
///   // ptrType1 == ptrType2 (same reference)
/// </summary>
public class TypeInterner
{
    // Caches for different type kinds
    private readonly Dictionary<(IrType, int), IrArrayType> _arrayTypes = new();
    private readonly Dictionary<(IrType, string), IrArrayType> _symbolicArrayTypes = new();
    private readonly Dictionary<IrType, IrPointerType> _pointerTypes = new();
    private readonly Dictionary<IrType, IrReferenceType> _referenceTypes = new();
    private readonly Dictionary<IrType, IrMutReferenceType> _mutReferenceTypes = new();
    private readonly Dictionary<FunctionSignature, IrFunctionPointerType> _functionPointerTypes = new();
    private readonly Dictionary<ClosureSignature, IrClosureType> _closureTypes = new();
    private readonly Dictionary<TupleSignature, IrTupleType> _tupleTypes = new();
    private readonly Dictionary<string, IrStructType> _structTypes = new();
    private readonly Dictionary<string, IrEnumType> _enumTypes = new();
    private readonly Dictionary<string, IrGenericType> _genericTypes = new();

    /// <summary>
    /// Get or create an array type with the given element type and length
    /// </summary>
    public IrArrayType GetArrayType(IrType elementType, int length)
    {
        var key = (elementType, length);
        if (_arrayTypes.TryGetValue(key, out var cached))
            return cached;

        var newType = new IrArrayType(elementType, length);
        _arrayTypes[key] = newType;
        return newType;
    }

    public IrArrayType GetArrayType(IrType elementType, string lengthParameter)
    {
        var key = (elementType, lengthParameter);
        if (_symbolicArrayTypes.TryGetValue(key, out var cached))
            return cached;

        var newType = new IrArrayType(elementType, lengthParameter);
        _symbolicArrayTypes[key] = newType;
        return newType;
    }

    /// <summary>
    /// Get or create a pointer type to the given type
    /// </summary>
    public IrPointerType GetPointerType(IrType pointeeType)
    {
        if (_pointerTypes.TryGetValue(pointeeType, out var cached))
            return cached;

        var newType = new IrPointerType(pointeeType);
        _pointerTypes[pointeeType] = newType;
        return newType;
    }

    /// <summary>
    /// Get or create an immutable reference type to the given type
    /// </summary>
    public IrReferenceType GetReferenceType(IrType pointeeType)
    {
        if (_referenceTypes.TryGetValue(pointeeType, out var cached))
            return cached;

        var newType = new IrReferenceType(pointeeType);
        _referenceTypes[pointeeType] = newType;
        return newType;
    }

    /// <summary>
    /// Get or create a mutable reference type to the given type
    /// </summary>
    public IrMutReferenceType GetMutReferenceType(IrType pointeeType)
    {
        if (_mutReferenceTypes.TryGetValue(pointeeType, out var cached))
            return cached;

        var newType = new IrMutReferenceType(pointeeType);
        _mutReferenceTypes[pointeeType] = newType;
        return newType;
    }

    /// <summary>
    /// Get or create a function pointer type with the given signature
    /// </summary>
    public IrFunctionPointerType GetFunctionPointerType(
        List<IrType> parameterTypes,
        IrType returnType,
        IrCallingConvention callingConvention = IrCallingConvention.Novus,
        List<string?>? parameterRegisters = null,
        string? returnRegister = null)
    {
        parameterRegisters ??= Enumerable.Repeat<string?>(null, parameterTypes.Count).ToList();
        var signature = new FunctionSignature(parameterTypes, returnType, callingConvention, parameterRegisters, returnRegister);
        if (_functionPointerTypes.TryGetValue(signature, out var cached))
            return cached;

        var newType = new IrFunctionPointerType(parameterTypes, returnType, callingConvention, parameterRegisters, returnRegister);
        _functionPointerTypes[signature] = newType;
        return newType;
    }

    /// <summary>
    /// Get or create a closure type with the given signature.
    /// Note: Environment type is set later during closure analysis.
    /// </summary>
    public IrClosureType GetClosureType(List<IrType> parameterTypes, IrType returnType, IrStructType? environmentType = null)
    {
        var signature = new ClosureSignature(parameterTypes, returnType, environmentType);
        if (_closureTypes.TryGetValue(signature, out var cached))
            return cached;

        var newType = new IrClosureType(parameterTypes, returnType, environmentType);
        _closureTypes[signature] = newType;
        return newType;
    }

    /// <summary>
    /// Get or create a tuple type with the given element types
    /// </summary>
    public IrTupleType GetTupleType(List<IrType> elementTypes)
    {
        // Handle unit type specially
        if (elementTypes.Count == 0)
            return IrTupleType.Unit;

        var signature = new TupleSignature(elementTypes);
        if (_tupleTypes.TryGetValue(signature, out var cached))
            return cached;

        var newType = new IrTupleType(elementTypes);
        _tupleTypes[signature] = newType;
        return newType;
    }

    /// <summary>
    /// Register a struct type (structs are nominal types, identified by name)
    /// </summary>
    public IrStructType RegisterStructType(IrStructType structType)
    {
        if (_structTypes.TryGetValue(structType.StructName, out var cached))
            return cached;

        _structTypes[structType.StructName] = structType;
        return structType;
    }

    /// <summary>
    /// Get a previously registered struct type by name
    /// </summary>
    public IrStructType? GetStructType(string name)
    {
        _structTypes.TryGetValue(name, out var type);
        return type;
    }

    /// <summary>
    /// Register an enum type (enums are nominal types, identified by name)
    /// </summary>
    public IrEnumType RegisterEnumType(IrEnumType enumType)
    {
        // For enums, use the cache key if available (for monomorphized generics)
        // Otherwise use the enum name
        var key = enumType.CacheKey ?? enumType.EnumName;

        if (_enumTypes.TryGetValue(key, out var cached))
            return cached;

        _enumTypes[key] = enumType;
        return enumType;
    }

    /// <summary>
    /// Get a previously registered enum type by name
    /// </summary>
    public IrEnumType? GetEnumType(string name)
    {
        _enumTypes.TryGetValue(name, out var type);
        return type;
    }

    /// <summary>
    /// Get or create a generic type parameter
    /// </summary>
    public IrGenericType GetGenericType(string parameterName)
    {
        if (_genericTypes.TryGetValue(parameterName, out var cached))
            return cached;

        var newType = new IrGenericType(parameterName);
        _genericTypes[parameterName] = newType;
        return newType;
    }

    /// <summary>
    /// Clear all cached types (useful for testing or multi-module compilation)
    /// </summary>
    public void Clear()
    {
        _arrayTypes.Clear();
        _pointerTypes.Clear();
        _referenceTypes.Clear();
        _mutReferenceTypes.Clear();
        _functionPointerTypes.Clear();
        _closureTypes.Clear();
        _tupleTypes.Clear();
        _structTypes.Clear();
        _enumTypes.Clear();
        _genericTypes.Clear();
    }

    /// <summary>
    /// Helper struct for function signature equality
    /// </summary>
    private struct FunctionSignature : IEquatable<FunctionSignature>
    {
        public List<IrType> ParameterTypes { get; }
        public IrType ReturnType { get; }
        public IrCallingConvention CallingConvention { get; }
        public List<string?> ParameterRegisters { get; }
        public string? ReturnRegister { get; }

        public FunctionSignature(List<IrType> parameterTypes, IrType returnType,
            IrCallingConvention callingConvention, List<string?> parameterRegisters, string? returnRegister)
        {
            ParameterTypes = parameterTypes;
            ReturnType = returnType;
            CallingConvention = callingConvention;
            ParameterRegisters = parameterRegisters;
            ReturnRegister = returnRegister;
        }

        public bool Equals(FunctionSignature other)
        {
            if (!ReferenceEquals(ReturnType, other.ReturnType))
                return false;

            if (CallingConvention != other.CallingConvention || ReturnRegister != other.ReturnRegister)
                return false;

            if (ParameterTypes.Count != other.ParameterTypes.Count)
                return false;

            for (int i = 0; i < ParameterTypes.Count; i++)
            {
                if (!ReferenceEquals(ParameterTypes[i], other.ParameterTypes[i]))
                    return false;
                if (ParameterRegisters[i] != other.ParameterRegisters[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is FunctionSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ReturnType);
            hash.Add(CallingConvention);
            hash.Add(ReturnRegister);
            foreach (var param in ParameterTypes)
            {
                hash.Add(param);
            }
            foreach (var register in ParameterRegisters)
            {
                hash.Add(register);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Helper struct for tuple type equality
    /// </summary>
    private struct TupleSignature : IEquatable<TupleSignature>
    {
        public List<IrType> ElementTypes { get; }

        public TupleSignature(List<IrType> elementTypes)
        {
            ElementTypes = elementTypes;
        }

        public bool Equals(TupleSignature other)
        {
            if (ElementTypes.Count != other.ElementTypes.Count)
                return false;

            for (int i = 0; i < ElementTypes.Count; i++)
            {
                if (!ReferenceEquals(ElementTypes[i], other.ElementTypes[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is TupleSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var elem in ElementTypes)
            {
                hash.Add(elem);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Helper struct for closure signature equality
    /// </summary>
    private struct ClosureSignature : IEquatable<ClosureSignature>
    {
        public List<IrType> ParameterTypes { get; }
        public IrType ReturnType { get; }
        public IrStructType? EnvironmentType { get; }

        public ClosureSignature(List<IrType> parameterTypes, IrType returnType, IrStructType? environmentType)
        {
            ParameterTypes = parameterTypes;
            ReturnType = returnType;
            EnvironmentType = environmentType;
        }

        public bool Equals(ClosureSignature other)
        {
            if (!ReferenceEquals(ReturnType, other.ReturnType))
                return false;

            if (!ReferenceEquals(EnvironmentType, other.EnvironmentType))
                return false;

            if (ParameterTypes.Count != other.ParameterTypes.Count)
                return false;

            for (int i = 0; i < ParameterTypes.Count; i++)
            {
                if (!ReferenceEquals(ParameterTypes[i], other.ParameterTypes[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is ClosureSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ReturnType);
            hash.Add(EnvironmentType);
            foreach (var param in ParameterTypes)
            {
                hash.Add(param);
            }
            return hash.ToHashCode();
        }
    }
}
