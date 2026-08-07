using Novus.Diagnostics;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend.Generics;

/// <summary>
/// Implementation of IGenericInstantiator
/// Handles monomorphization of generic functions, methods, and types
/// </summary>
public class GenericInstantiatorImpl : IGenericInstantiator
{
    private readonly IInstantiationContext _context;
    private readonly MonomorphizationCache _cache;

    public GenericInstantiatorImpl(IInstantiationContext context)
    {
        _context = context;
        _cache = new MonomorphizationCache();
    }

    public MonomorphizationCache Cache => _cache;

    #region IGenericInstantiator Implementation

    public IrType InstantiateType(IrType genericType, Dictionary<string, IrType> substitutions)
    {
        return _context.SubstitutionEngine.SubstituteGenericTypes(genericType, substitutions);
    }

    public IrFunction? InstantiateStructMethod(
        IrStructType monomorphizedStruct,
        string methodName,
        bool isTraitImpl = false,
        string? traitName = null,
        List<IrType>? traitTypeArgs = null,
        List<IrValue>? arguments = null)
    {
        var baseTypeName = monomorphizedStruct.StructName;
        var templateKey = InstantiationKeyBuilder.BuildMethodTemplateKey(baseTypeName, methodName);

        // Check if we have a template for this method
        if (!_cache.TryGetMethodTemplate(templateKey, out var template) || template == null)
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, templateConstants, _, methodGenericParams, sourceModulePath) = template;

        // Restore constants from template
        _context.RestoreConstantsFromTuples(templateConstants);

        // Build type substitution map
        var baseStruct = _context.LookupStruct(baseTypeName);
        if (baseStruct == null)
        {
            var errorLocation = new SourceLocation(_context.InputFilePath ?? "unknown", 0, 0, 0, "");
            _context.ReportError(
                ErrorCodes.StructNotFound,
                $"Struct '{baseTypeName}' not found",
                errorLocation);
            return null;
        }

        var typeSubstitutions = TypeSubstitutionHelper.BuildStructTypeSubstitutions(
            baseStruct,
            monomorphizedStruct);

        if (typeSubstitutions == null || typeSubstitutions.Count < baseStruct.GenericParameters.Count)
        {
            // Fallback: scan fields to extract generic type mappings
            typeSubstitutions ??= new Dictionary<string, IrType>();
            for (int i = 0; i < baseStruct.Fields.Count && i < monomorphizedStruct.Fields.Count; i++)
            {
                TypeSubstitutionHelper.ExtractGenericTypeMapping(
                    baseStruct.Fields[i].Type,
                    monomorphizedStruct.Fields[i].Type,
                    typeSubstitutions);
            }
        }

        // Verify all generic parameters were resolved
        foreach (var genericParam in baseStruct.GenericParameters)
        {
            if (!typeSubstitutions.ContainsKey(genericParam))
            {
                var errorLocation = new SourceLocation(_context.InputFilePath ?? "unknown", 0, 0, 0, "");
                _context.ReportError(
                    ErrorCodes.GenericParameterNotFound,
                    $"Generic parameter '{genericParam}' not found in monomorphized struct {monomorphizedStruct.CacheKey}",
                    errorLocation);
                return null;
            }
        }

        if (methodGenericParams is { Count: > 0 })
        {
            var savedInferenceSubstitutions = _context.CurrentTypeSubstitutions;
            var savedGenericParams = methodGenericParams
                .Select(name => (name, type: _context.LookupGenericParameter(name)))
                .Where(entry => entry.type != null)
                .ToDictionary(entry => entry.name, entry => entry.type!);

            foreach (var name in methodGenericParams)
                _context.RegisterGenericParameter(name, new IrGenericType(name));
            _context.CurrentTypeSubstitutions = typeSubstitutions;

            try
            {
                var parameterContexts = funcDecl.parameterList()?.parameter() ?? [];
                for (var i = 0; i < parameterContexts.Length && i < (arguments?.Count ?? 0); i++)
                {
                    TypeSubstitutionHelper.ExtractGenericTypeMapping(
                        _context.ParseType(parameterContexts[i].type()),
                        arguments![i].Type,
                        typeSubstitutions);
                }
            }
            finally
            {
                _context.CurrentTypeSubstitutions = savedInferenceSubstitutions;
                _context.ClearGenericParameters();
                foreach (var (name, type) in savedGenericParams)
                    _context.RegisterGenericParameter(name, type);
            }

            foreach (var name in methodGenericParams)
            {
                if (!typeSubstitutions.ContainsKey(name))
                {
                    _context.ReportError(
                        ErrorCodes.GenericParameterNotFound,
                        $"Cannot infer type for method generic parameter '{name}' in {baseTypeName}::{methodName}",
                        new SourceLocation(_context.InputFilePath ?? "unknown", 0, 0, 0, ""));
                    return null;
                }
            }
        }

        var methodTypeSuffix = methodGenericParams is { Count: > 0 }
            ? "_" + string.Join("_", methodGenericParams.Select(name => _context.GetTypeCacheKey(typeSubstitutions[name])))
            : "";
        var instantiationKey = InstantiationKeyBuilder.BuildMethodKey(
            monomorphizedStruct.CacheKey ?? baseTypeName, methodName, isTraitImpl, traitName) + methodTypeSuffix;
        var mangledMethodName = _context.GenerateMethodMangledName(
            monomorphizedStruct.CacheKey ?? baseTypeName,
            methodName,
            isTraitImpl,
            traitName,
            traitTypeArgs ?? new List<IrType>()) + methodTypeSuffix;

        if (_cache.IsMethodInstantiated(instantiationKey))
            return _context.Module.GetFunction(mangledMethodName);

        // Set up instantiation state
        var savedSubstitutions = _context.CurrentTypeSubstitutions;
        var savedSelfType = _context.CurrentSelfType;
        _context.CurrentTypeSubstitutions = typeSubstitutions;
        _context.CurrentSelfType = monomorphizedStruct;

        try
        {
            // Create the function
            var returnType = _context.ParseReturnType(funcDecl.type());
            if (returnType == null)
            {
                return null;
            }

            // Substitute generic types in return type
            returnType = _context.SubstitutionEngine.SubstituteGenericTypes(returnType!, typeSubstitutions);

            // Generate mangled name
            var mangledTypeName = monomorphizedStruct.CacheKey ?? baseTypeName;
            var function = new IrFunction(mangledMethodName, returnType!, Visibility.Private, false);

            // Parse parameters with substitutions
            if (funcDecl.parameterList() != null)
            {
                var paramList = funcDecl.parameterList();

                // Handle self parameter
                _context.ParseSelfParameter(paramList.selfParameter(), function, monomorphizedStruct);

                // Add regular parameters - need to substitute generic types
                foreach (var paramCtx in paramList.parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = _context.ParseType(paramCtx.type());

                    // Substitute generic types recursively
                    paramType = _context.SubstitutionEngine.SubstituteGenericTypes(paramType, typeSubstitutions);

                    function.Parameters.Add(new IrParameter(paramName, paramType));
                }

                // Add variadic parameter if present
                _context.ParseVariadicParameter(paramList, function);
            }

            _context.Module.AddFunction(function);

            // Build function body
            var savedFunction = _context.CurrentFunction;
            var savedBlock = _context.CurrentBlock;
            // CRITICAL: Save and restore local variables to avoid corrupting the outer function's state
            // When monomorphizing a generic method called from another method, we must not overwrite
            // the outer method's local variables (especially 'self')
            var savedLocalVariables = new Dictionary<string, IrLocalVariable>(_context.LocalVariables);
            // CRITICAL: Save statement-level state (like _ifLabels) to avoid corrupting the outer
            // function's if-statement processing when the inner method also contains if-statements
            var savedStatementState = _context.SaveStatementState();
            _context.LocalVariables.Clear();
            _context.CurrentFunction = function;

            try
            {
                // Import dependencies from the source module before visiting the body
                // This ensures that functions/statics referenced in the template are available
                if (sourceModulePath != null)
                {
                    _context.ImportModuleDependencies(sourceModulePath);
                }

                // Add parameters to local variables
                foreach (var param in function.Parameters)
                {
                    _context.LocalVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, false);
                }

                // Create entry block
                var entryBlock = new IrBasicBlock("entry");
                function.BasicBlocks.Add(entryBlock);
                _context.CurrentBlock = entryBlock;

                // Visit the function body with type substitutions active
                if (funcDecl.block() != null)
                {
                    _context.VisitFunctionBody(funcDecl.block());
                }
            }
            finally
            {
                _context.CurrentFunction = savedFunction;
                _context.CurrentBlock = savedBlock;
                // Restore local variables from outer function
                _context.LocalVariables.Clear();
                foreach (var kvp in savedLocalVariables)
                {
                    _context.LocalVariables[kvp.Key] = kvp.Value;
                }
                // Restore statement state from outer function
                _context.RestoreStatementState(savedStatementState);
            }

            // Mark as instantiated
            _cache.MarkMethodInstantiated(instantiationKey);

            return function;
        }
        finally
        {
            _context.CurrentTypeSubstitutions = savedSubstitutions;
            _context.CurrentSelfType = savedSelfType;
        }
    }

    public IrFunction? InstantiateEnumMethod(
        IrEnumType monomorphizedEnum,
        string methodName,
        List<IrValue> arguments)
    {
        var baseTypeName = monomorphizedEnum.EnumName;
        var templateKey = InstantiationKeyBuilder.BuildMethodTemplateKey(baseTypeName, methodName);

        // Check if we have a template for this method
        if (!_cache.TryGetMethodTemplate(templateKey, out var template) || template == null)
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, templateConstants, _, methodGenericParams, sourceModulePath) = template;

        // Build type substitution map
        var baseEnum = _context.LookupEnum(baseTypeName);
        if (baseEnum == null)
        {
            var errorLocation = new SourceLocation(_context.InputFilePath ?? "unknown", 0, 0, 0, "");
            _context.ReportError(
                ErrorCodes.EnumNotFound,
                $"Enum '{baseTypeName}' not found",
                errorLocation);
            return null;
        }

        var typeSubstitutions = TypeSubstitutionHelper.BuildEnumTypeSubstitutions(baseEnum, monomorphizedEnum);
        if (typeSubstitutions == null)
        {
            typeSubstitutions = new Dictionary<string, IrType>();
        }

        // Verify all enum-level generic parameters were resolved
        foreach (var genericParam in baseEnum.GenericParameters)
        {
            if (!typeSubstitutions.ContainsKey(genericParam))
            {
                var errorLocation = new SourceLocation(_context.InputFilePath ?? "unknown", 0, 0, 0, "");
                _context.ReportError(
                    ErrorCodes.GenericParameterNotFound,
                    $"Generic parameter '{genericParam}' not found in monomorphized enum {monomorphizedEnum.CacheKey ?? monomorphizedEnum.EnumName}",
                    errorLocation);
                return null;
            }
        }

        // Handle method-level generic parameters (e.g., <E> in fn ok_or<E>(self, err: E) -> Result<T, E>)
        if (methodGenericParams != null && methodGenericParams.Count > 0)
        {
            // Register method-level generic parameters temporarily so we can parse parameter types
            var savedGenericParams = new Dictionary<string, IrGenericType>();
            foreach (var methodParam in methodGenericParams)
            {
                var existing = _context.LookupGenericParameter(methodParam);
                if (existing != null)
                {
                    savedGenericParams[methodParam] = existing;
                }
                _context.RegisterGenericParameter(methodParam, new IrGenericType(methodParam));
            }

            try
            {
                // Parse template parameter types to infer method-level generics from arguments
                var templateParams = new List<IrParameter>();
                if (funcDecl.parameterList() != null)
                {
                    var paramList = funcDecl.parameterList();

                    foreach (var paramCtx in paramList.parameter())
                    {
                        var paramName = paramCtx.IDENTIFIER().GetText();
                        var paramType = _context.ParseType(paramCtx.type());
                        templateParams.Add(new IrParameter(paramName, paramType));
                    }
                }

                // Infer method-level generics from arguments
                // Arguments MAY include 'self' as first argument - we need to detect this
                // Check if first argument is the receiver (self) by comparing its type with the monomorphized enum
                var hasSelfParameter = funcDecl.parameterList()?.selfParameter() != null;
                var firstArgIsSelf = hasSelfParameter && arguments.Count > 0 &&
                    (arguments[0].Type.Equals(monomorphizedEnum) ||
                     (arguments[0].Type is IrPointerType ptr && ptr.PointeeType.Equals(monomorphizedEnum)));
                var nonSelfArguments = firstArgIsSelf
                    ? arguments.Skip(1).ToList()
                    : arguments;

                for (int i = 0; i < templateParams.Count && i < nonSelfArguments.Count; i++)
                {
                    TypeSubstitutionHelper.ExtractGenericTypeMapping(
                        templateParams[i].Type,
                        nonSelfArguments[i].Type,
                        typeSubstitutions);
                }

                // Verify all method-level generic parameters were resolved
                foreach (var methodParam in methodGenericParams)
                {
                    if (!typeSubstitutions.ContainsKey(methodParam))
                    {
                        var errorLocation = new SourceLocation(_context.InputFilePath ?? "unknown", 0, 0, 0, "");
                        _context.ReportError(
                            ErrorCodes.GenericParameterNotFound,
                            $"Cannot infer type for method generic parameter '{methodParam}' in {baseTypeName}::{methodName}",
                            errorLocation);
                        return null;
                    }
                }
            }
            finally
            {
                // Restore generic parameters
                foreach (var methodParam in methodGenericParams)
                {
                    if (savedGenericParams.TryGetValue(methodParam, out var saved))
                    {
                        _context.RegisterGenericParameter(methodParam, saved);
                    }
                }
            }
        }

        // Build instantiation key - include method-level generics in the key
        var allTypeArgKeys = new List<string>();
        foreach (var p in genericParams)
        {
            if (typeSubstitutions.TryGetValue(p, out var subst))
            {
                allTypeArgKeys.Add(_context.GetTypeCacheKey(subst));
            }
        }
        if (methodGenericParams != null)
        {
            foreach (var p in methodGenericParams)
            {
                if (typeSubstitutions.TryGetValue(p, out var subst))
                {
                    allTypeArgKeys.Add(_context.GetTypeCacheKey(subst));
                }
            }
        }
        var instantiationKey = $"{monomorphizedEnum.CacheKey}::{methodName}::{string.Join(",", allTypeArgKeys)}";

        // Check if already instantiated
        if (_cache.IsMethodInstantiated(instantiationKey))
        {
            // Already generated, look it up
            var cachedMangledName = InstantiationKeyBuilder.BuildEnumMethodMangledName(
                baseTypeName,
                methodName,
                allTypeArgKeys);
            return _context.Module.GetFunction(cachedMangledName);
        }

        // Restore constants and set up state
        _context.RestoreConstantsFromTuples(templateConstants);
        var savedSubstitutions = _context.CurrentTypeSubstitutions;
        var savedSelfType = _context.CurrentSelfType;
        _context.CurrentTypeSubstitutions = typeSubstitutions;
        _context.CurrentSelfType = monomorphizedEnum;

        try
        {
            // Create the function
            var returnType = _context.ParseReturnType(funcDecl.type());
            if (returnType == null)
            {
                return null;
            }
            returnType = _context.SubstitutionEngine.SubstituteGenericTypes(returnType, typeSubstitutions);

            // Create mangled name from all type arguments (enum-level + method-level)
            var mangledName = InstantiationKeyBuilder.BuildEnumMethodMangledName(
                baseTypeName,
                methodName,
                allTypeArgKeys);

            var function = new IrFunction(mangledName, returnType, Visibility.Private, false);

            // Parse parameters with substitutions
            if (funcDecl.parameterList() != null)
            {
                var paramList = funcDecl.parameterList();

                // Handle self parameter if present
                if (paramList.selfParameter() != null)
                {
                    _context.ParseSelfParameter(paramList.selfParameter(), function, monomorphizedEnum);
                }

                // Add regular parameters
                foreach (var paramCtx in paramList.parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = _context.ParseType(paramCtx.type());
                    paramType = _context.SubstitutionEngine.SubstituteGenericTypes(paramType, typeSubstitutions);
                    function.Parameters.Add(new IrParameter(paramName, paramType));
                }

                // Add variadic parameter if present
                _context.ParseVariadicParameter(paramList, function);
            }

            // Check if function already exists
            var existingFunc = _context.Module.GetFunction(mangledName);
            if (existingFunc != null)
            {
                return existingFunc;
            }

            _context.Module.AddFunction(function);

            // Build function body
            var savedFunction = _context.CurrentFunction;
            var savedBlock = _context.CurrentBlock;
            // CRITICAL: Save and restore local variables to avoid corrupting the outer function's state
            // When monomorphizing an enum method (e.g. Option::is_some) called from another method (e.g. HashMap::insert),
            // we must not overwrite the outer method's local variables (especially 'self')
            var savedLocalVariables = new Dictionary<string, IrLocalVariable>(_context.LocalVariables);
            // CRITICAL: Save statement-level state to avoid corrupting the outer function's if-statement processing
            var savedStatementState = _context.SaveStatementState();
            _context.LocalVariables.Clear();
            _context.CurrentFunction = function;

            try
            {
                // Import dependencies from the source module before visiting the body
                // This ensures that functions/statics referenced in the template are available
                if (sourceModulePath != null)
                {
                    _context.ImportModuleDependencies(sourceModulePath);
                }

                var entryBlock = new IrBasicBlock("entry");
                function.BasicBlocks.Add(entryBlock);
                _context.CurrentBlock = entryBlock;

                // Add parameters to local variables
                foreach (var param in function.Parameters)
                {
                    _context.LocalVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, false);
                }

                // Visit the function body
                if (funcDecl.block() != null)
                {
                    _context.VisitFunctionBody(funcDecl.block());
                }
            }
            finally
            {
                _context.CurrentFunction = savedFunction;
                _context.CurrentBlock = savedBlock;
                // Restore local variables from outer function
                _context.LocalVariables.Clear();
                foreach (var kvp in savedLocalVariables)
                {
                    _context.LocalVariables[kvp.Key] = kvp.Value;
                }
                // Restore statement state from outer function
                _context.RestoreStatementState(savedStatementState);
            }

            _cache.MarkMethodInstantiated(instantiationKey);

            return function;
        }
        finally
        {
            _context.CurrentTypeSubstitutions = savedSubstitutions;
            _context.CurrentSelfType = savedSelfType;
        }
    }

    public IrFunction? InstantiateFunction(
        string functionName,
        Dictionary<string, IrType> typeSubstitutions)
    {
        // Check if we have a template for this function
        if (!_cache.TryGetFunctionTemplate(functionName, out var template) || template == null)
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, templateConstants, whereClause, _, sourceModulePath) = template;

        // Validate generic constraints before instantiation
        if (!ValidateGenericConstraints(functionName, whereClause, genericParams, typeSubstitutions, funcDecl))
        {
            return null; // Constraint validation failed, error already reported
        }

        // Build instantiation key
        var instantiationKey = InstantiationKeyBuilder.BuildFunctionKey(functionName, typeSubstitutions);

        // Check if already instantiated
        if (_cache.IsFunctionInstantiated(instantiationKey))
        {
            // Already generated, look it up
            var existingMangledName = InstantiationKeyBuilder.BuildGenericFunctionMangledName(
                functionName,
                typeSubstitutions);
            return _context.Module.GetFunction(existingMangledName);
        }

        // Restore constants and set up state
        _context.RestoreConstantsFromTuples(templateConstants);
        var savedSubstitutions = _context.CurrentTypeSubstitutions;
        _context.CurrentTypeSubstitutions = typeSubstitutions;

        try
        {
            // Create the function with substituted return type
            var returnType = _context.ParseReturnType(funcDecl.type());
            if (returnType == null)
            {
                return null;
            }
            returnType = _context.SubstitutionEngine.SubstituteGenericTypes(returnType, typeSubstitutions);

            var mangledFunctionName = InstantiationKeyBuilder.BuildGenericFunctionMangledName(
                functionName,
                typeSubstitutions);
            var function = new IrFunction(mangledFunctionName, returnType, Visibility.Private, false);

            // Parse parameters with substitutions
            if (funcDecl.parameterList() != null)
            {
                var paramList = funcDecl.parameterList();
                foreach (var paramCtx in paramList.parameter())
                {
                    var paramName = paramCtx.IDENTIFIER().GetText();
                    var paramType = _context.ParseType(paramCtx.type());

                    // Substitute generic types recursively
                    paramType = _context.SubstitutionEngine.SubstituteGenericTypes(paramType, typeSubstitutions);

                    function.Parameters.Add(new IrParameter(paramName, paramType));
                }

                // Add variadic parameter if present
                _context.ParseVariadicParameter(paramList, function);
            }

            _context.Module.AddFunction(function);

            // Build function body
            var savedFunction = _context.CurrentFunction;
            var savedBlock = _context.CurrentBlock;
            // CRITICAL: Save and restore local variables to avoid corrupting the outer function's state
            // When monomorphizing a generic method called from another method, we must not overwrite
            // the outer method's local variables (especially 'self')
            var savedLocalVariables = new Dictionary<string, IrLocalVariable>(_context.LocalVariables);
            // CRITICAL: Save statement-level state to avoid corrupting the outer function's if-statement processing
            var savedStatementState = _context.SaveStatementState();
            _context.LocalVariables.Clear();
            _context.CurrentFunction = function;

            try
            {
                // Import dependencies from the source module before visiting the body
                // This ensures that functions/statics referenced in the template are available
                if (sourceModulePath != null)
                {
                    _context.ImportModuleDependencies(sourceModulePath);
                }

                // Add parameters to local variables
                foreach (var param in function.Parameters)
                {
                    _context.LocalVariables[param.Name] = new IrLocalVariable(param.Name, param.Type, false);
                }

                // Create entry block
                var entryBlock = new IrBasicBlock("entry");
                function.BasicBlocks.Add(entryBlock);
                _context.CurrentBlock = entryBlock;

                // Visit the function body with type substitutions active
                if (funcDecl.block() != null)
                {
                    _context.VisitFunctionBody(funcDecl.block());
                }
            }
            finally
            {
                _context.CurrentFunction = savedFunction;
                _context.CurrentBlock = savedBlock;
                // Restore local variables from outer function
                _context.LocalVariables.Clear();
                foreach (var kvp in savedLocalVariables)
                {
                    _context.LocalVariables[kvp.Key] = kvp.Value;
                }
                // Restore statement state from outer function
                _context.RestoreStatementState(savedStatementState);
            }

            _cache.MarkFunctionInstantiated(instantiationKey);

            return function;
        }
        finally
        {
            _context.CurrentTypeSubstitutions = savedSubstitutions;
        }
    }

    /// <summary>
    /// Validate that type substitutions satisfy all constraints in the where clause.
    /// Reports E0100 errors for any constraint violations.
    /// </summary>
    /// <param name="functionName">Name of the function being instantiated (for error messages)</param>
    /// <param name="whereClause">The where clause constraints to validate</param>
    /// <param name="genericParams">The generic parameter names</param>
    /// <param name="typeSubstitutions">The concrete types being substituted</param>
    /// <param name="funcDecl">The function declaration context (for source location)</param>
    /// <returns>True if all constraints are satisfied, false otherwise</returns>
    private bool ValidateGenericConstraints(
        string functionName,
        IrWhereClause? whereClause,
        List<string> genericParams,
        Dictionary<string, IrType> typeSubstitutions,
        NovusParser.FunctionDeclarationContext funcDecl)
    {
        if (whereClause == null || whereClause.Constraints is [])
            return true;

        var allSatisfied = true;

        foreach (var constraint in whereClause.Constraints)
        {
            // Get the concrete type for this constrained parameter
            if (!typeSubstitutions.TryGetValue(constraint.TypeParameter, out var concreteType))
                continue;

            // Check each trait bound
            foreach (var bound in constraint.Bounds)
            {
                if (!TypeImplementsTrait(concreteType, bound.TraitName))
                {
                    // Get source location from function declaration
                    var location = new SourceLocation(
                        _context.InputFilePath ?? "unknown",
                        funcDecl.Start.Line,
                        funcDecl.Start.Column + 1,
                        funcDecl.Stop.Column - funcDecl.Start.Column + 1,
                        ""
                    );

                    _context.ReportError(
                        "E0100",
                        $"type '{GetTypeName(concreteType)}' does not implement trait '{bound.TraitName}' " +
                        $"(required by constraint on '{constraint.TypeParameter}' in function '{functionName}')",
                        location
                    );
                    allSatisfied = false;
                }
            }
        }

        return allSatisfied;
    }

    /// <summary>
    /// Check if a type implements a specific trait.
    /// Uses the module's trait impl registry.
    /// </summary>
    private bool TypeImplementsTrait(IrType type, string traitName)
    {
        var typeName = GetTypeName(type);
        return _context.Module.GetTraitImpl(traitName, typeName) != null;
    }

    /// <summary>
    /// Get the base type name for trait lookup.
    /// </summary>
    private string GetTypeName(IrType type)
    {
        return type switch
        {
            IrStructType structType => structType.StructName,
            IrEnumType enumType => enumType.EnumName,
            IrPointerType ptrType => GetTypeName(ptrType.PointeeType),
            IrArrayType arrayType => GetTypeName(arrayType.ElementType),
            IrIntType intType => intType.IsSigned ? $"i{intType.BitWidth}" : $"u{intType.BitWidth}",
            IrBoolType => "bool",
            IrTupleType tupleType => $"({string.Join(", ", tupleType.ElementTypes.Select(GetTypeName))})",
            _ => type.Name
        };
    }

    public void RegisterMethodTemplate(string templateKey, GenericTemplate template)
    {
        _cache.RegisterMethodTemplate(templateKey, template);
    }

    public void RegisterFunctionTemplate(string functionName, GenericTemplate template)
    {
        _cache.RegisterFunctionTemplate(functionName, template);
    }

    public bool HasMethodInstantiation(string cacheKey)
    {
        return _cache.IsMethodInstantiated(cacheKey);
    }

    public bool HasFunctionInstantiation(string cacheKey)
    {
        return _cache.IsFunctionInstantiated(cacheKey);
    }

    public bool HasMethodTemplate(string templateKey)
    {
        return _cache.HasMethodTemplate(templateKey);
    }

    public bool HasFunctionTemplate(string functionName)
    {
        return _cache.HasFunctionTemplate(functionName);
    }

    public bool TryGetMethodTemplate(string templateKey, out GenericTemplate? template)
    {
        return _cache.TryGetMethodTemplate(templateKey, out template);
    }

    public bool TryGetFunctionTemplate(string functionName, out GenericTemplate? template)
    {
        return _cache.TryGetFunctionTemplate(functionName, out template);
    }

    public Dictionary<string, IrType>? InferGenericTypes(
        List<string> genericParams,
        List<IrParameter> templateParams,
        List<IrValue> arguments)
    {
        return TypeSubstitutionHelper.InferGenericTypes(genericParams, templateParams, arguments);
    }

    public Dictionary<string, IrType>? InferEnumGenericTypes(
        IrEnumType baseEnum,
        string methodName,
        List<IrValue> arguments,
        IrType? expectedReturnType)
    {
        // Look up the method template to get parameter types
        var templateKey = InstantiationKeyBuilder.BuildMethodTemplateKey(baseEnum.EnumName, methodName);
        if (!_cache.TryGetMethodTemplate(templateKey, out var template) || template == null)
        {
            return null; // No template found
        }

        var (genericParams, funcDecl, _, _, _, _) = template;

        // Save existing generic parameters before registering new ones
        var savedGenericParams = new Dictionary<string, IrGenericType>();
        foreach (var paramName in genericParams)
        {
            var existing = _context.LookupGenericParameter(paramName);
            if (existing != null)
            {
                savedGenericParams[paramName] = existing;
            }
        }

        // Register generic parameters so they can be recognized when parsing parameter types
        foreach (var paramName in genericParams)
        {
            _context.RegisterGenericParameter(paramName, new IrGenericType(paramName));
        }

        try
        {
            // Parse template parameters to get their generic types
            var templateParams = new List<IrParameter>();
            if (funcDecl.parameterList() != null)
            {
                var paramList = funcDecl.parameterList();
                var regularParams = paramList.parameter();

                var savedSubstitutions = _context.CurrentTypeSubstitutions;
                _context.CurrentTypeSubstitutions = null;

                try
                {
                    foreach (var paramCtx in regularParams)
                    {
                        var paramName = paramCtx.IDENTIFIER().GetText();
                        var paramType = _context.ParseType(paramCtx.type());
                        templateParams.Add(new IrParameter(paramName, paramType));
                    }
                }
                finally
                {
                    _context.CurrentTypeSubstitutions = savedSubstitutions;
                }
            }

            var typeSubstitutions = new Dictionary<string, IrType>();

            // Step 1: Infer from arguments if available
            if (arguments.Count == templateParams.Count)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    var argType = arguments[i].Type;
                    var paramType = templateParams[i].Type;
                    TypeSubstitutionHelper.ExtractGenericTypeMapping(paramType, argType, typeSubstitutions);
                }
            }

            // Step 2: Try to infer from expected return type if we still have unresolved generics
            if (expectedReturnType != null && funcDecl.type() != null)
            {
                var savedSubstitutions = _context.CurrentTypeSubstitutions;
                _context.CurrentTypeSubstitutions = null;

                try
                {
                    var templateReturnType = _context.ParseType(funcDecl.type());
                    // Extract type mappings from return type
                    TypeSubstitutionHelper.ExtractGenericTypeMapping(
                        templateReturnType,
                        expectedReturnType,
                        typeSubstitutions);
                }
                finally
                {
                    _context.CurrentTypeSubstitutions = savedSubstitutions;
                }
            }

            // Verify all generic parameters from the enum were resolved
            foreach (var genericParam in baseEnum.GenericParameters)
            {
                if (!typeSubstitutions.ContainsKey(genericParam))
                {
                    return null; // Could not infer all required type parameters
                }
            }

            return typeSubstitutions;
        }
        finally
        {
            // Restore generic parameters to their previous state
            // First clear all, then restore saved ones
            _context.ClearGenericParameters();
            foreach (var kvp in savedGenericParams)
            {
                _context.RegisterGenericParameter(kvp.Key, kvp.Value);
            }
        }
    }

    public void ExtractGenericTypeMapping(
        IrType baseType,
        IrType monomorphizedType,
        Dictionary<string, IrType> substitutions)
    {
        TypeSubstitutionHelper.ExtractGenericTypeMapping(baseType, monomorphizedType, substitutions);
    }

    #endregion
}
