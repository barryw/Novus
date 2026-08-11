using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Collects type definitions from all modules to generate a shared types header.
/// This eliminates duplicate type definitions across generated C files.
/// </summary>
public class TypeRegistry
{
    private readonly List<IrEnumType> _enumTypes = new();
    private readonly List<IrStructType> _structTypes = new();
    private readonly List<IrTupleType> _tupleTypes = new();
    private readonly List<IrClosureType> _closureTypes = new();
    private bool _needsString = false;

    /// <summary>
    /// Register types from a module
    /// </summary>
    public void RegisterModule(IrModule module)
    {
        // Register all concrete structs from the module
        // This ensures struct definitions are included even if not directly used in function signatures
        foreach (var structType in module.Structs)
        {
            // Only register non-generic (concrete) structs
            if (structType.GenericParameters is [])
            {
                AddOrUpdateStructType(structType);
            }
        }

        // Register all concrete enums from the module
        foreach (var enumType in module.Enums)
        {
            // Only register non-generic (concrete) enums
            if (IsConcreteEnum(enumType))
            {
                AddOrUpdateEnumType(enumType);
            }
        }

        // Scan external variables for enum and struct types
        foreach (var externVar in module.ExternalVariables)
        {
            if (externVar.Type is IrEnumType enumExtVar && IsConcreteEnum(enumExtVar))
            {
                AddOrUpdateEnumType(enumExtVar);
            }
            else if (externVar.Type is IrStructType structExtVar && structExtVar.GenericParameters is [])
            {
                AddOrUpdateStructType(structExtVar);
            }
        }

        // Scan static variables for enum and struct types
        foreach (var staticVar in module.StaticVariables)
        {
            if (staticVar.Type is IrEnumType enumStaticVar && IsConcreteEnum(enumStaticVar))
            {
                AddOrUpdateEnumType(enumStaticVar);
            }
            else if (staticVar.Type is IrStructType structStaticVar && structStaticVar.GenericParameters is [])
            {
                AddOrUpdateStructType(structStaticVar);
            }
        }

        // Scan all functions for actually-used enum types
        // This includes return types, parameters, local variables, and instruction operands
        foreach (var function in module.Functions)
        {
            // Check return type
            if (function.ReturnType is IrEnumType enumRet && IsConcreteEnum(enumRet))
            {
                AddOrUpdateEnumType(enumRet);
            }
            else if (function.ReturnType is IrTupleType tupleRet)
            {
                AddTupleType(tupleRet);
            }

            // Check parameters
            foreach (var param in function.Parameters)
            {
                if (param.Type is IrEnumType enumParam && IsConcreteEnum(enumParam))
                {
                    AddOrUpdateEnumType(enumParam);
                }
                else if (param.Type is IrTupleType tupleParam)
                {
                    AddTupleType(tupleParam);
                }
            }

            // Check local variables
            foreach (var local in function.LocalVariables)
            {
                if (local.Type is IrEnumType enumLocal && IsConcreteEnum(enumLocal))
                {
                    AddOrUpdateEnumType(enumLocal);
                }
                else if (local.Type is IrTupleType tupleLocal)
                {
                    AddTupleType(tupleLocal);
                }
            }

            // Scan instructions for enum, struct, and tuple types
            foreach (var block in function.BasicBlocks)
            {
                foreach (var instruction in block.Instructions)
                {
                    if (instruction is IrLocalDecl localDecl)
                    {
                        // Unwrap pointer/reference types to find the underlying struct/enum type
                        // This is needed when code uses pointers to structs (e.g., BufferedWindow*)
                        var declType = localDecl.Type;
                        if (declType is IrPointerType ptrDeclType)
                            declType = ptrDeclType.PointeeType;
                        else if (declType is IrReferenceType refDeclType)
                            declType = refDeclType.PointeeType;
                        else if (declType is IrMutReferenceType mutRefDeclType)
                            declType = mutRefDeclType.PointeeType;

                        if (declType is IrEnumType enumDeclType && IsConcreteEnum(enumDeclType))
                        {
                            AddOrUpdateEnumType(enumDeclType);
                        }
                        else if (declType is IrStructType structDeclType &&
                                 structDeclType.GenericParameters is [])
                        {
                            AddOrUpdateStructType(structDeclType);
                        }
                        else if (declType is IrTupleType tupleDeclType)
                        {
                            AddTupleType(tupleDeclType);
                        }

                        // Also check the initial value for tuple types
                        if (localDecl.InitialValue != null)
                        {
                            ScanValueForTypes(localDecl.InitialValue);
                        }
                    }

                    if (instruction is IrMatch match &&
                        match.MatchValue.Type is IrEnumType matchEnumType &&
                        IsConcreteEnum(matchEnumType))
                    {
                        AddOrUpdateEnumType(matchEnumType);
                    }

                    if (instruction is IrStore store)
                    {
                        ScanValueForTypes(store.Value);
                    }

                    if (instruction is IrReturn returnInstr && returnInstr.Value != null)
                    {
                        ScanValueForTypes(returnInstr.Value);
                    }
                }
            }

            // Check return type for struct
            if (function.ReturnType is IrStructType structRet && structRet.GenericParameters is [])
            {
                AddOrUpdateStructType(structRet);
            }

            // Check parameters for struct
            foreach (var param in function.Parameters)
            {
                // Handle reference types (e.g., &self, &mut self) and pointer types (e.g., *self)
                // When methods are compiled, &self becomes a raw pointer in the C code,
                // so we need the full struct definition to access fields via self->field
                var paramType = param.Type;
                if (paramType is IrReferenceType refType)
                    paramType = refType.PointeeType;
                else if (paramType is IrMutReferenceType mutRefType)
                    paramType = mutRefType.PointeeType;
                else if (paramType is IrPointerType ptrType)
                    paramType = ptrType.PointeeType;

                if (paramType is IrStructType structParam && structParam.GenericParameters is [])
                {
                    AddOrUpdateStructType(structParam);
                }
            }
        }

        // After collecting all struct types, scan struct fields for transitively referenced enum types
        // This ensures enums referenced by struct fields are included in the shared types header
        CollectTransitiveEnumTypes();
    }

    /// <summary>
    /// Recursively collect enum types that are transitively referenced by struct fields, enum variants, arrays, pointers, etc.
    /// This ensures all enum types needed by the shared types header are included.
    /// Uses fixed-point iteration to handle mutually recursive types.
    /// </summary>
    private void CollectTransitiveEnumTypes()
    {
        // Fixed-point iteration: keep scanning until no new types are discovered
        bool changed = true;
        while (changed)
        {
            changed = false;
            int startCount = _enumTypes.Count;
            int startStructCount = _structTypes.Count;

            // Process each struct type and collect any enum types from its fields
            foreach (var structType in _structTypes.ToList())  // ToList() to avoid modification during iteration
            {
                foreach (var field in structType.Fields)
                {
                    CollectEnumTypesFromType(field.Type);
                }
            }

            // Process each enum type and collect any enum/struct types from its variant associated data
            // This is crucial for catching types like Result<Str, StringError> where StringError
            // is buried in the associated data of the Err variant, and Result<BStr, StringError>
            // where BStr struct is in the Ok variant
            foreach (var enumType in _enumTypes.ToList())  // ToList() to avoid modification during iteration
            {
                foreach (var variant in enumType.Variants)
                {
                    if (variant.HasAssociatedData && variant.AssociatedData != null)
                    {
                        foreach (var dataType in variant.AssociatedData)
                        {
                            // Collect enums
                            CollectEnumTypesFromType(dataType);

                            // Also collect structs! (BStr in Result<BStr, E>)
                            if (dataType is IrStructType structType && structType.GenericParameters is [])
                            {
                                AddOrUpdateStructType(structType);
                            }
                        }
                    }
                }
            }

            // Check if we discovered any new types
            if (_enumTypes.Count > startCount || _structTypes.Count > startStructCount)
                changed = true;
        }
    }

    /// <summary>
    /// Recursively collect all enum types from a type, including those nested in structs, arrays, and pointers
    /// </summary>
    private void CollectEnumTypesFromType(IrType type)
    {
        CollectEnumTypesFromType(type, new HashSet<string>());
    }

    private void CollectEnumTypesFromType(IrType type, HashSet<string> visitedStructs)
    {
        switch (type)
        {
            case IrEnumType enumType when IsConcreteEnum(enumType):
                AddOrUpdateEnumType(enumType);
                break;

            case IrTupleType tupleType:
                AddTupleType(tupleType);
                // Recursively check element types
                foreach (var elementType in tupleType.ElementTypes)
                {
                    CollectEnumTypesFromType(elementType, visitedStructs);
                }
                break;

            case IrArrayType arrayType:
                // Recursively check the element type
                CollectEnumTypesFromType(arrayType.ElementType, visitedStructs);
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
                    CollectEnumTypesFromType(field.Type, visitedStructs);
                }
                break;

            case IrPointerType pointerType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(pointerType.PointeeType, visitedStructs);
                break;

            case IrReferenceType refType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(refType.PointeeType, visitedStructs);
                break;

            case IrMutReferenceType mutRefType:
                // Recursively check the pointee type
                CollectEnumTypesFromType(mutRefType.PointeeType, visitedStructs);
                break;

            case IrClosureType closureType:
                // Add the closure type and recursively check parameter/return types
                AddClosureType(closureType);
                foreach (var paramType in closureType.ParameterTypes)
                {
                    CollectEnumTypesFromType(paramType, visitedStructs);
                }
                CollectEnumTypesFromType(closureType.ReturnType, visitedStructs);
                break;

            // For other types (primitive, function pointers, etc.) we don't need to recurse
        }
    }

    private void AddTupleType(IrTupleType tupleType)
    {
        // Skip unit type () - it doesn't need a struct definition
        if (tupleType.ElementTypes is [])
            return;

        // Check if we already have this tuple type (by comparing element types)
        var exists = _tupleTypes.Any(t =>
            t.ElementTypes.Count == tupleType.ElementTypes.Count &&
            t.ElementTypes.Zip(tupleType.ElementTypes, (a, b) => a.Name == b.Name).All(x => x));

        if (!exists)
        {
            _tupleTypes.Add(tupleType);
        }
    }

    /// <summary>
    /// Recursively scan an IrValue for tuple, enum, and struct types
    /// </summary>
    private void ScanValueForTypes(IrValue value)
    {
        if (value == null)
            return;

        // Check the value's type
        if (value.Type is IrTupleType tupleType)
        {
            AddTupleType(tupleType);
        }
        else if (value.Type is IrEnumType enumType && IsConcreteEnum(enumType))
        {
            AddOrUpdateEnumType(enumType);
        }
        else if (value.Type is IrStructType structType && structType.GenericParameters is [])
        {
            AddOrUpdateStructType(structType);
        }
        else if (value.Type is IrClosureType closureType)
        {
            AddClosureType(closureType);
        }

        // For tuple literals, also scan element types recursively
        if (value is IrTupleLiteral tupleLiteral)
        {
            foreach (var element in tupleLiteral.Elements)
            {
                ScanValueForTypes(element);
            }
        }
    }

    private string GetStructName(IrStructType structType)
    {
        // Use CacheKey for monomorphized generics, otherwise use Name
        return structType.CacheKey ?? structType.Name;
    }

    private bool UsesStringType(IrFunction function)
    {
        // Check return type
        if (function.ReturnType is IrStructType structType && structType.Name == "String")
            return true;

        // Check parameters
        foreach (var param in function.Parameters)
        {
            if (param.Type is IrStructType st && st.Name == "String")
                return true;
        }

        // Check local variables and instructions (simplified check)
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLocalDecl localDecl &&
                    localDecl.Type is IrStructType lst && lst.Name == "String")
                    return true;
            }
        }

        return false;
    }

    private string GetEnumName(IrEnumType enumType)
    {
        return enumType.CacheKey ?? enumType.EnumName;
    }

    /// <summary>
    /// Check if an enum type is fully concrete (no generic parameters in it or its associated data).
    /// This is crucial for monomorphized types like Option<Formatter> which still have GenericParameters.Count > 0
    /// but are actually fully concrete because all type parameters have been replaced.
    /// </summary>
    private bool IsConcreteEnum(IrEnumType enumType)
    {
        // If the enum has generic parameters AND no CacheKey, it's definitely generic
        // But if it has a CacheKey (like "Option<Formatter>"), it's a monomorphized instance
        if (enumType.GenericParameters.Count > 0)
        {
            // Monomorphized enums have a CacheKey that shows the concrete type instantiation
            if (string.IsNullOrEmpty(enumType.CacheKey))
            {
                // No CacheKey means it's the original generic definition
                return false;
            }

            // Has a CacheKey - check if it contains any remaining generic type parameters (like <T>)
            if (enumType.CacheKey.Contains("<T"))
            {
                return false;
            }
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
            IrEnumType enumType => !IsConcreteEnum(enumType),
            IrStructType structType => structType.GenericParameters.Count > 0 ||
                                        (structType.CacheKey != null && structType.CacheKey.Contains("<T")),
            IrArrayType arrayType => IsGenericType(arrayType.ElementType),
            IrPointerType pointerType => IsGenericType(pointerType.PointeeType),
            IrReferenceType refType => IsGenericType(refType.PointeeType),
            IrMutReferenceType mutRefType => IsGenericType(mutRefType.PointeeType),
            _ => false
        };
    }

    /// <summary>
    /// Add or update an enum type in the registry.
    /// If an enum with the same name already exists, keeps the version with more variants.
    /// This handles cases where enum types are encountered multiple times with varying completeness.
    /// </summary>
    private void AddOrUpdateEnumType(IrEnumType enumType)
    {
        var existingEnum = _enumTypes.FirstOrDefault(e => GetEnumName(e) == GetEnumName(enumType));
        if (existingEnum == null)
        {
            _enumTypes.Add(enumType);
        }
        else if (enumType.Variants.Count > existingEnum.Variants.Count)
        {
            // Replace with the version that has more variants (the fully-defined version)
            _enumTypes.Remove(existingEnum);
            _enumTypes.Add(enumType);
        }
    }

    /// <summary>
    /// Add or update a struct type in the registry.
    /// If a struct with the same name already exists, keeps the version with more fields.
    /// This handles cases where struct types are encountered multiple times with varying completeness.
    /// </summary>
    private void AddOrUpdateStructType(IrStructType structType)
    {
        var existingStruct = _structTypes.FirstOrDefault(s => GetStructName(s) == GetStructName(structType));
        if (existingStruct == null)
        {
            _structTypes.Add(structType);
        }
        else if (structType.Fields.Count > existingStruct.Fields.Count)
        {
            // Replace with the version that has more fields (the fully-defined version)
            _structTypes.Remove(existingStruct);
            _structTypes.Add(structType);
        }
    }

    public IEnumerable<IrEnumType> EnumTypes => _enumTypes;
    public IEnumerable<IrStructType> StructTypes => _structTypes;
    public IEnumerable<IrTupleType> TupleTypes => _tupleTypes;
    public IEnumerable<IrClosureType> ClosureTypes => _closureTypes;
    public bool NeedsString => _needsString;

    /// <summary>
    /// Add a closure type to the registry
    /// </summary>
    private void AddClosureType(IrClosureType closureType)
    {
        // Check if we already have this closure type (by comparing signatures)
        var exists = _closureTypes.Any(c =>
            c.ParameterTypes.Count == closureType.ParameterTypes.Count &&
            c.ParameterTypes.Zip(closureType.ParameterTypes, (a, b) => a.Name == b.Name).All(x => x) &&
            c.ReturnType.Name == closureType.ReturnType.Name);

        if (!exists)
        {
            _closureTypes.Add(closureType);
        }
    }
}
