using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Novus.Diagnostics;
using Novus.HIR;
using Novus.IR;
using Novus.Parser;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing expression visitor methods.
/// This file contains methods for processing all expression types: literals, operators,
/// method calls, member access, array operations, casts, and more.
/// </summary>
public partial class IrBuilder
{
    public override object? VisitPrimaryExpr([NotNull] NovusParser.PrimaryExprContext context)
    {
        return Visit(context.primaryExpression());
    }

    public override object? VisitCallExpr([NotNull] NovusParser.CallExprContext context)
    {
        // Handle method calls (e.g., v.len())
        // Method calls desugar to: Type::method(receiver, args...)
        if (context.expression() is NovusParser.MemberAccessExprContext memberAccessCtx)
        {
            return HandleMethodCallIr(context, memberAccessCtx);
        }

        // Check if this is a call to a generic function template (before evaluating funcExpr)
        // Generic functions aren't in _module.Functions yet, so we need to check the template dictionary
        string? genericFuncName = null;
        if (context.expression() is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.primaryExpression() is NovusParser.IdentifierExprContext identExpr)
        {
            genericFuncName = identExpr.identifier().GetText();
        }

        var funcExpr = (IrValue?)Visit(context.expression());
        if (funcExpr == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Function expression is null or invalid",
                errorLocation
            );
            return null;
        }

        // Parse arguments
        var arguments = new List<IrValue>();
        if (context.argumentList() != null)
        {
            // Determine expected argument types from the callee's signature
            // This is critical for monomorphizing generic types like Option::None
            List<IrType>? expectedArgTypes = null;

            // Check if this is an enum constructor - if so, we can use expected types for arguments
            if (funcExpr is IrEnumConstructor tempEnumCtor &&
                _expectedType is IrEnumType expectedEnumType &&
                expectedEnumType.EnumName == (tempEnumCtor.Type as IrEnumType)?.EnumName)
            {
                // Expected type matches the enum we're constructing
                // Extract expected argument types from the corresponding variant
                var expectedVariant = expectedEnumType.GetVariant(tempEnumCtor.VariantName);
                if (expectedVariant != null)
                {
                    expectedArgTypes = expectedVariant.AssociatedData;
                }
            }
            // For regular function calls, use the function's parameter types
            else if (funcExpr is IrFunctionRef funcRefForArgs)
            {
                expectedArgTypes = funcRefForArgs.Function.Parameters
                    .Where(p => !p.IsVariadic)
                    .Select(p => p.Type)
                    .ToList();
            }

            int argIdx = 0;
            foreach (var argCtx in context.argumentList().expression())
            {
                // Set expected type for this argument if available
                var savedExpectedType = _expectedType;
                if (expectedArgTypes != null && argIdx < expectedArgTypes.Count)
                {
                    _expectedType = expectedArgTypes[argIdx];
                }
                else
                {
                    _expectedType = null;
                }

                var argValue = (IrValue?)Visit(argCtx);

                // Restore expected type
                _expectedType = savedExpectedType;

                if (argValue != null)
                {
                    // IMPORTANT: Apply current type substitutions to argument type
                    // This handles the case where a generic function calls another generic function
                    // with generic arguments (e.g., double<T> calling identity(x) where x: T)
                    var argType = argValue.Type;
                    if (_currentTypeSubstitutions != null)
                    {
                        argType = _typeParser.SubstituteGenericTypes(argType, _currentTypeSubstitutions);
                    }

                    // If type was substituted, create a new IrVariable with the substituted type
                    if (argType != argValue.Type && argValue is IrVariable argVar)
                    {
                        arguments.Add(new IrVariable(argVar.Name, argType));
                    }
                    else
                    {
                        arguments.Add(argValue);
                    }
                }
                argIdx++;
            }
        }

        // NOTE: Str → *u8 coercion is now handled later, after function lookup,
        // so we can check the actual parameter types and only coerce when needed

        // If it's a generic function template, infer types and instantiate
        if (genericFuncName != null && _genericFunctionTemplates.ContainsKey(genericFuncName))
        {
            // Get template and parse parameters
            var template = _genericFunctionTemplates[genericFuncName];

            // Save and clear type substitutions so we get the generic template types
            var savedTypeSubstitutions = _currentTypeSubstitutions;
            _currentTypeSubstitutions = null;

            // Set up generic params temporarily
            // TODO: Use child scope instead of save/restore pattern
            _symbols.ClearGenericParameters();
            foreach (var paramName in template.GenericParams)
            {
                _symbols.RegisterGenericParameter(paramName, new IrGenericType(paramName));
            }

            // Parse template parameters
            var templateParams = new List<IrParameter>();
            if (template.Context.parameterList() != null)
            {
                var paramList = template.Context.parameterList();
                ParseRegularParameters(paramList, templateParams);

                // Add variadic parameter if present (for template analysis)
                ParseVariadicParameter(paramList, templateParams);
            }

            // Restore generic params
            _symbols.ClearGenericParameters();
            // TODO: Restore saved params when we implement save/restore properly

            // Restore type substitutions
            _currentTypeSubstitutions = savedTypeSubstitutions;

            // Infer types
            var typeSubstitutions = InferGenericFunctionTypes(template.GenericParams, templateParams, arguments);
            if (typeSubstitutions == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Cannot infer type arguments for '{genericFuncName}'",
                    errorLocation
                );
                return null;
            }

            // Instantiate
            var instantiatedFunc = _genericInstantiator.InstantiateFunction(genericFuncName, typeSubstitutions);
            if (instantiatedFunc == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Failed to instantiate '{genericFuncName}'",
                    errorLocation
                );
                return null;
            }

            // Create call
            var genericCallResult = $"%t{_tempCounter++}";
            var genericCall = new IrCall(instantiatedFunc.Name, instantiatedFunc.ReturnType, genericCallResult);
            genericCall.Location = GetLocation(context);
            foreach (var arg in arguments)
            {
                genericCall.Arguments.Add(arg);
            }
            _currentBlock!.AddInstruction(genericCall);
            return new IrVariable(genericCallResult, instantiatedFunc.ReturnType);
        }

        // Handle generic associated function calls (e.g., Vec::new())
        if (funcExpr is IrGenericAssociatedFunction genericAssocFunc)
        {
            // We need to determine the concrete type parameters
            // Priority: 1) Explicit type args (turbo-fish), 2) Expected type, 3) Unresolved

            IrStructType? monomorphizedStruct = null;

            // 1. Check for explicit type arguments (turbo-fish syntax: Vec::<u32>::with_capacity)
            if (genericAssocFunc.ExplicitTypeArgs != null && genericAssocFunc.ExplicitTypeArgs.Count > 0)
            {
                // User provided explicit type arguments
                if (genericAssocFunc.ExplicitTypeArgs.Count != genericAssocFunc.GenericParameters.Count)
                {
                    var errorLocation = GetLocation(context);
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Wrong number of type arguments for '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}': expected {genericAssocFunc.GenericParameters.Count}, got {genericAssocFunc.ExplicitTypeArgs.Count}",
                        errorLocation
                    );
                    return null;
                }

                // Build monomorphized struct from explicit type args (same logic as ParseNamedType)
                var baseStruct = _symbols.LookupStruct(genericAssocFunc.TypeName);
                if (baseStruct == null)
                {
                    var errorLocation = GetLocation(context);
                    _diagnostics.ReportError(ErrorCodes.StructNotFound, $"Struct '{genericAssocFunc.TypeName}' not found", errorLocation);
                    return null;
                }
                var typeArgs = genericAssocFunc.ExplicitTypeArgs;

                // Create cache key
                var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                var cacheKey = $"{baseStruct.StructName}<{string.Join(",", typeArgKeys)}>";

                // Check cache first
                if (_symbols.LookupMonomorphizedStruct(cacheKey) != null)
                {
                    monomorphizedStruct = _symbols.LookupMonomorphizedStruct(cacheKey)!;
                }
                else
                {
                    // Create monomorphized struct with concrete types
                    var typeSubstitutions = new Dictionary<string, IrType>();
                    for (int i = 0; i < baseStruct.GenericParameters.Count; i++)
                    {
                        typeSubstitutions[baseStruct.GenericParameters[i]] = typeArgs[i];
                    }

                    // Create monomorphized fields using recursive substitution
                    var monomorphizedFields = new List<IrStructField>();
                    bool fullyMonomorphized = true;

                    foreach (var origField in baseStruct.Fields)
                    {
                        var fieldType = _typeParser.SubstituteGenericTypes(origField.Type, typeSubstitutions);
                        monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));

                        // Check if field type is still generic
                        if (_typeParser.ContainsGenericTypes(fieldType))
                        {
                            fullyMonomorphized = false;
                        }
                    }

                    // Create new struct type with concrete types (no generic parameters)
                    monomorphizedStruct = new IrStructType(baseStruct.StructName, monomorphizedFields, null, cacheKey);

                    // Force calculation of field offsets only if fully monomorphized
                    if (fullyMonomorphized)
                    {
                        _ = monomorphizedStruct.SizeInBytes;
                    }

                    // Cache it for future use
                    _symbols.RegisterMonomorphizedStruct(cacheKey, monomorphizedStruct);
                }
            }
            // 2. Try to infer from expected type
            else if (_expectedType != null && _expectedType is IrStructType expectedStruct && expectedStruct.GenericParameters.Count == 0)
            {
                // Expected type is a monomorphized struct like Vec<i32>
                monomorphizedStruct = expectedStruct;
            }
            // 3. No explicit type args and no expected type - create unresolved generic
            else if (_expectedType == null)
            {
                // No expected type - create unresolved generic that will be inferred from usage
                // Example: let vec = Vec::new() → vec has type Vec<UnresolvedGeneric>
                // When vec.push(42i32) is called later, we'll resolve it to Vec<i32>

                var unresolvedTypeArgs = new List<IrType>();
                for (int i = 0; i < genericAssocFunc.GenericParameters.Count; i++)
                {
                    unresolvedTypeArgs.Add(new IrUnresolvedGenericType());
                }

                var partiallyResolvedType = new IrPartiallyResolvedGenericType(
                    genericAssocFunc.TypeName,
                    unresolvedTypeArgs
                );

                // Return a placeholder value with the partially resolved type
                // The actual instantiation will happen when we see method calls on this value
                var placeholderResult = $"%t{_tempCounter++}";
                return new IrVariable(placeholderResult, partiallyResolvedType);
            }

            if (monomorphizedStruct == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Could not infer generic type parameters for '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}()'. Consider using turbo-fish syntax: {genericAssocFunc.TypeName}::<Type>::{genericAssocFunc.MethodName}",
                    errorLocation
                );
                return null;
            }

            // Instantiate the generic method with the monomorphized struct
            var instantiatedFunc = _genericInstantiator.InstantiateStructMethod(monomorphizedStruct, genericAssocFunc.MethodName);
            if (instantiatedFunc == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Failed to instantiate generic method '{genericAssocFunc.TypeName}::{genericAssocFunc.MethodName}'",
                    errorLocation
                );
                return null;
            }

            // Generate call to instantiated function
            var callResult = $"%t{_tempCounter++}";
            var callInst = new IrCall(instantiatedFunc.Name, instantiatedFunc.ReturnType, callResult);
            callInst.Location = GetLocation(context);
            foreach (var arg in arguments)
            {
                callInst.Arguments.Add(arg);
            }
            _currentBlock!.AddInstruction(callInst);

            return new IrVariable(callResult, instantiatedFunc.ReturnType);
        }

        // Handle non-generic function reference calls
        if (funcExpr is IrFunctionRef funcRef)
        {
            // Validate argument count
            var funcRefNonVariadicCount = funcRef.Function.Parameters.Count(p => !p.IsVariadic);
            if (funcRef.Function.IsVariadic)
            {
                if (arguments.Count < funcRefNonVariadicCount)
                {
                    var errorLocation = GetLocation(context);
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Variadic function '{funcRef.Function.Name}' expects at least {funcRefNonVariadicCount} arguments, got {arguments.Count}",
                        errorLocation
                    );
                    return null;
                }
            }
            else
            {
                if (arguments.Count != funcRef.Function.Parameters.Count)
                {
                    var errorLocation = GetLocation(context);
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Function '{funcRef.Function.Name}' expects {funcRef.Function.Parameters.Count} arguments, got {arguments.Count}",
                        errorLocation
                    );
                    return null;
                }
            }

            // Apply automatic coercions for function arguments
            for (int i = 0; i < arguments.Count; i++)
            {
                // Skip variadic parameters (they don't have a declared type)
                if (i >= funcRef.Function.Parameters.Count || funcRef.Function.Parameters[i].IsVariadic)
                    continue;

                var paramType = funcRef.Function.Parameters[i].Type;
                var argValue = arguments[i];

                // Coercion 1: Array → Pointer decay (e.g., [T; N] → *T)
                // This allows passing arrays directly to functions expecting pointers
                // Arrays decay to pointers to their first element
                if (paramType is IrPointerType expectedPtrType &&
                    argValue.Type is IrArrayType arrayType &&
                    expectedPtrType.PointeeType.Name == arrayType.ElementType.Name)
                {
                    // Array-to-pointer decay: just use the array value directly
                    // The backend will handle getting the address of the first element
                    arguments[i] = argValue;
                    continue;  // Move to next argument
                }

                // Coercion 2: Str/String → *u8 for string parameters
                if (paramType is IrPointerType ptrType &&
                    ptrType.PointeeType.Equals(IrIntType.U8) &&
                    argValue.Type is IrStructType structType &&
                    (structType.StructName == "Str" || structType.StructName == "String"))
                {
                    if (structType.StructName == "Str")
                    {
                        // If argValue is a struct literal, extract the ptr field directly (no instruction needed)
                        if (argValue is IrStructLiteral strLiteral)
                        {
                            if (!strLiteral.FieldValues.TryGetValue("ptr", out var ptrValue))
                            {
                                var errorLocation = GetLocation(context);
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct literal must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }
                            arguments[i] = ptrValue;  // Use the ptr value directly
                        }
                        else
                        {
                            // For Str variables (not literals), we need the member access
                            var ptrField = structType.GetField("ptr");
                            if (ptrField == null)
                            {
                                var errorLocation = GetLocation(context);
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }

                            var ptrTempName = $"%t{_tempCounter++}";
                            var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                            var ptrFieldAccess = new IrMemberAccess(ptrTempName, argValue, "ptr", u8PtrType, ptrField.Offset);
                            _currentBlock!.AddInstruction(ptrFieldAccess);
                            arguments[i] = new IrVariable(ptrTempName, u8PtrType);
                        }
                    }
                    else if (structType.StructName == "String")
                    {
                        // For String, call the as_ptr() method
                        var asPtrMethodName = "String::as_ptr";
                        var asPtrMethod = _module.GetFunction(asPtrMethodName);

                        if (asPtrMethod == null)
                        {
                            var errorLocation = GetLocation(context);
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "String type must have as_ptr() method for automatic coercion to *u8",
                                errorLocation
                            );
                            return null;
                        }

                        // Call String::as_ptr()
                        var receiverArg = new IrBorrowValue(argValue, asPtrMethod.Parameters[0].Type, false);
                        var resultTempName = $"%t{_tempCounter++}";
                        var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                        var methodCall = new IrCall(asPtrMethodName, u8PtrType, resultTempName);
                        methodCall.Location = GetLocation(context);
                        methodCall.Arguments.Add(receiverArg);
                        _currentBlock!.AddInstruction(methodCall);
                        arguments[i] = new IrVariable(resultTempName, u8PtrType);
                    }
                }

                // Coercion 3: &[T; N] → Slice<T>
                // When a pointer to an array is passed to a function expecting Slice<T>
                if (paramType is IrStructType sliceStruct &&
                    sliceStruct.StructName == "Slice" &&
                    argValue.Type is IrPointerType ptrToArrayType &&
                    ptrToArrayType.PointeeType is IrArrayType ptrArrayType)
                {
                    // Get the 'ptr' field to extract the element type T from Slice<T>
                    var ptrField = sliceStruct.GetField("ptr");
                    if (ptrField?.Type is IrPointerType slicePtrType)
                    {
                        // Verify array element type matches Slice element type
                        if (slicePtrType.PointeeType.Equals(ptrArrayType.ElementType))
                        {
                            // Create pointer to array element (pointer to array decays to pointer to first element)
                            var arrayElemPtrType = _typeInterner.GetPointerType(ptrArrayType.ElementType);

                            // Cast the pointer-to-array to a pointer-to-element
                            // *[T; N] → *T (decay to pointer to first element)
                            var ptrValue = new IrCastValue(argValue, argValue.Type, arrayElemPtrType);

                            // Create the length constant
                            var lenValue = new IrConstant(ptrArrayType.Length, IrIntType.U32);

                            // Create Slice struct literal
                            var sliceFields = new Dictionary<string, IrValue>
                            {
                                { "ptr", ptrValue },
                                { "len", lenValue }
                            };

                            var sliceValue = new IrStructLiteral(sliceStruct, sliceFields);

                            // Slice structs containing pointers are passed by reference in C ABI
                            // (see CCodeGenerator.TypeContainsHeapData), so we need to create a borrow
                            // of the slice struct, not just the struct value itself
                            var sliceRefType = _typeInterner.GetReferenceType(sliceStruct);
                            arguments[i] = new IrBorrowValue(sliceValue, sliceRefType, false);
                            continue;
                        }
                    }
                }

                // Coercion 4: &[T; N] → Slice<T> (sized array reference to Slice struct)
                // When a function expects &[T] (unsized slice), actually pass Slice<T>
                // This is because &[T] is syntactic sugar for Slice<T>
                if (paramType is IrReferenceType expectedSliceRef &&
                    expectedSliceRef.PointeeType is IrArrayType expectedSliceArray &&
                    expectedSliceArray.Length == -1 &&
                    argValue.Type is IrReferenceType actualArrayRef &&
                    actualArrayRef.PointeeType is IrArrayType actualArray &&
                    actualArray.Length >= 0 &&
                    expectedSliceArray.ElementType.Equals(actualArray.ElementType))
                {
                    // The function parameter is &[T] which is unsized slice syntax
                    // We need to create a Slice<T> struct from the array reference
                    // First, look up the Slice<T> struct
                    var sliceStructDef = _module.Structs.FirstOrDefault(s => s.StructName == "Slice");
                    if (sliceStructDef != null)
                    {
                        // Check if we already have Slice<ElementType> monomorphized
                        var elementType = expectedSliceArray.ElementType;
                        var sliceCacheKey = $"Slice<{elementType.Name}>";
                        var monomorphizedSliceStruct = _module.Structs.FirstOrDefault(s => s.CacheKey == sliceCacheKey);

                        if (monomorphizedSliceStruct != null)
                        {
                            // Create Slice<T> from &[T; N]
                            // Get pointer to first element
                            var elemPtrType = _typeInterner.GetPointerType(elementType);
                            var ptrValue = new IrCastValue(argValue, argValue.Type, elemPtrType);

                            // Create length constant
                            var lenValue = new IrConstant(actualArray.Length, IrIntType.U32);

                            // Create Slice struct literal
                            var sliceFields = new Dictionary<string, IrValue>
                            {
                                { "ptr", ptrValue },
                                { "len", lenValue }
                            };

                            var sliceValue = new IrStructLiteral(monomorphizedSliceStruct, sliceFields);
                            arguments[i] = sliceValue;
                            continue;
                        }
                    }

                    // Fallback: if Slice<T> isn't available, just pass the reference as-is
                    // This will likely fail type checking later
                }

                // Coercion 5: String/Str → &String/&Str for reference parameters
                // Automatically create a reference when a value is passed to a function expecting a reference
                if (paramType is IrReferenceType expectedRefType &&
                    argValue.Type is IrStructType argStructType &&
                    (argStructType.StructName == "String" || argStructType.StructName == "Str") &&
                    expectedRefType.PointeeType is IrStructType expectedPointeeStruct &&
                    expectedPointeeStruct.StructName == argStructType.StructName)
                {
                    // Create a borrow (immutable reference) of the string value
                    var refType = _typeInterner.GetReferenceType(argStructType);
                    arguments[i] = new IrBorrowValue(argValue, refType, false);
                    continue;
                }
            }

            // Generate call instruction
            var callResultName = $"%t{_tempCounter++}";
            var callInstruction = new IrCall(funcRef.Function.Name, funcRef.Function.ReturnType, callResultName);
            callInstruction.Location = GetLocation(context);
            foreach (var arg in arguments)
            {
                callInstruction.Arguments.Add(arg);
            }
            _currentBlock!.AddInstruction(callInstruction);

            return new IrVariable(callResultName, funcRef.Function.ReturnType);
        }

        // Check if this is an enum constructor call
        if (funcExpr is IrEnumConstructor enumCtor)
        {
            // Create an enum value with the provided arguments
            var enumType = enumCtor.Type as IrEnumType;
            if (enumType == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    "Enum constructor must have enum type",
                    errorLocation
                );
                return null;
            }

            var variant = enumType.GetVariant(enumCtor.VariantName);
            if (variant == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.EnumNotFound,
                    $"Variant '{enumCtor.VariantName}' not found in enum '{enumType.EnumName}'",
                    errorLocation
                );
                return null;
            }

            // Validate argument count
            if (arguments.Count != variant.AssociatedData.Count)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Variant '{enumCtor.VariantName}' expects {variant.AssociatedData.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }

            // If enum has generic parameters, perform type inference to monomorphize
            IrEnumType finalEnumType = enumType;
            if (enumType.GenericParameters.Count > 0)
            {
                var typeSubstitutions = new Dictionary<string, IrType>();

                // PRIORITY 1: Use expected type for monomorphization if available
                // This allows From<T> trait conversions to work correctly
                if (_expectedType is IrEnumType expectedEnumType &&
                    expectedEnumType.EnumName == enumType.EnumName &&
                    expectedEnumType.GenericParameters.Count == 0) // Expected type is monomorphized
                {
                    // Extract concrete types from expected enum by matching variant structure
                    for (int paramIdx = 0; paramIdx < enumType.GenericParameters.Count; paramIdx++)
                    {
                        var paramName = enumType.GenericParameters[paramIdx];

                        // Find this parameter in a variant and extract the concrete type
                        for (int varIdx = 0; varIdx < enumType.Variants.Count; varIdx++)
                        {
                            var origVariant = enumType.Variants[varIdx];
                            var expectedVar = expectedEnumType.Variants[varIdx];

                            for (int dataIdx = 0; dataIdx < origVariant.AssociatedData.Count; dataIdx++)
                            {
                                var expectedTypeFromVariant = expectedVar.AssociatedData[dataIdx];
                                if (origVariant.AssociatedData[dataIdx] is IrGenericType gt &&
                                    gt.ParameterName == paramName)
                                {
                                    typeSubstitutions[paramName] = expectedTypeFromVariant;
                                    break;
                                }
                            }

                            if (typeSubstitutions.ContainsKey(paramName))
                                break;
                        }
                    }

                    // Use the expected type directly if it matches
                    if (typeSubstitutions.Count == enumType.GenericParameters.Count)
                    {
                        finalEnumType = expectedEnumType;
                    }
                }

                // PRIORITY 2: Fall back to argument types for any missing parameters
                // This handles cases where expected type is not available or incomplete
                for (int i = 0; i < arguments.Count; i++)
                {
                    var argType = arguments[i].Type;
                    var paramType = variant.AssociatedData[i];

                    if (paramType is IrGenericType gt)
                    {
                        if (!typeSubstitutions.ContainsKey(gt.ParameterName))
                        {
                            typeSubstitutions[gt.ParameterName] = argType;
                        }
                    }
                }

                // PRIORITY 3: If not using expected type, create monomorphized enum from type substitutions
                if (finalEnumType == enumType) // Haven't assigned finalEnumType yet
                {
                    // Create cache key using proper type keys
                    var typeArgKeys = enumType.GenericParameters.Select(p =>
                    {
                        var key = typeSubstitutions.ContainsKey(p) ? GetTypeCacheKey(typeSubstitutions[p]) : p;
                        return key;
                    });
                    var cacheKey = $"{enumType.EnumName}<{string.Join(",", typeArgKeys)}>";

                    // Check cache first
                    if (_symbols.LookupMonomorphizedEnum(cacheKey) != null)
                    {
                        finalEnumType = _symbols.LookupMonomorphizedEnum(cacheKey)!;
                    }
                    else
                    {
                        // Create monomorphized enum type
                        var monomorphizedVariants = new List<IrEnumVariant>();
                        foreach (var origVariant in enumType.Variants)
                        {
                            var monomorphizedData = new List<IrType>();
                            foreach (var dataType in origVariant.AssociatedData)
                            {
                                if (dataType is IrGenericType genType && typeSubstitutions.ContainsKey(genType.ParameterName))
                                {
                                    monomorphizedData.Add(typeSubstitutions[genType.ParameterName]);
                                }
                                else
                                {
                                    monomorphizedData.Add(dataType);
                                }
                            }
                            monomorphizedVariants.Add(new IrEnumVariant(origVariant.Name, origVariant.Tag, monomorphizedData));
                        }

                        // Create new enum type with concrete types
                        finalEnumType = new IrEnumType(enumType.EnumName, monomorphizedVariants, null, cacheKey);

                        // Only cache if fully monomorphized (no generic types in variants)
                        bool isFullyMonomorphized = !monomorphizedVariants.Any(v =>
                            v.AssociatedData.Any(d => d is IrGenericType));

                        if (isFullyMonomorphized)
                        {
                            _symbols.RegisterMonomorphizedEnum(cacheKey, finalEnumType);
                        }
                    }
                }
            }

            // Apply From<T> trait conversions if needed for arguments
            var finalVariant = finalEnumType.GetVariant(enumCtor.VariantName);
            var convertedArguments = new List<IrValue>();

            for (int i = 0; i < arguments.Count; i++)
            {
                var arg = arguments[i];
                var expectedType = finalVariant!.AssociatedData[i];

                // Check if type conversion is needed
                if (!TypesEqual(arg.Type, expectedType))
                {
                    // Try to convert via From<ArgType> trait
                    var convertedArg = TryConvertViaFromTrait(arg, expectedType);
                    if (convertedArg != null)
                    {
                        convertedArguments.Add(convertedArg);
                    }
                    else
                    {
                        // No conversion available, use original (will fail at runtime or be caught elsewhere)
                        convertedArguments.Add(arg);
                    }
                }
                else
                {
                    // No conversion needed
                    convertedArguments.Add(arg);
                }
            }

            // Create the enum value with the monomorphized type
            return new IrEnumValue(finalEnumType, enumCtor.VariantName, finalVariant!.Tag, convertedArguments);
        }

        string? resultName;
        IrType returnType;

        // Handle case where funcExpr is null (e.g., failed to resolve function)
        if (funcExpr == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.FunctionNotFound,
                $"Cannot resolve function call - expression did not evaluate to a callable",
                errorLocation
            );
            return null;
        }

        // Check if this is an indirect call through a function pointer
        if (funcExpr.Type is IrFunctionPointerType fpType)
        {
            // Indirect call through function pointer
            if (arguments.Count != fpType.ParameterTypes.Count)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Function pointer expects {fpType.ParameterTypes.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }

            // Apply automatic Str/String → *u8 coercion only when parameter type is *u8
            for (int i = 0; i < arguments.Count; i++)
            {
                var paramType = fpType.ParameterTypes[i];
                var argValue = arguments[i];

                // Only coerce if parameter is *u8 and argument is Str or String
                if (paramType is IrPointerType ptrType &&
                    ptrType.PointeeType.Equals(IrIntType.U8) &&
                    argValue.Type is IrStructType structType &&
                    (structType.StructName == "Str" || structType.StructName == "String"))
                {
                    if (structType.StructName == "Str")
                    {
                        // If argValue is a struct literal, extract the ptr field directly (no instruction needed)
                        if (argValue is IrStructLiteral strLiteral)
                        {
                            if (!strLiteral.FieldValues.TryGetValue("ptr", out var ptrValue))
                            {
                                var errorLocation = GetLocation(context);
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct literal must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }
                            arguments[i] = ptrValue;  // Use the ptr value directly
                        }
                        else
                        {
                            // For Str variables (not literals), we need the member access
                            var ptrField = structType.GetField("ptr");
                            if (ptrField == null)
                            {
                                var errorLocation = GetLocation(context);
                                _diagnostics.ReportError(
                                    ErrorCodes.InvalidExpressionType,
                                    "Str struct must have a 'ptr' field",
                                    errorLocation
                                );
                                return null;
                            }

                            var ptrTempName = $"%t{_tempCounter++}";
                            var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                            var ptrFieldAccess = new IrMemberAccess(ptrTempName, argValue, "ptr", u8PtrType, ptrField.Offset);
                            _currentBlock!.AddInstruction(ptrFieldAccess);
                            arguments[i] = new IrVariable(ptrTempName, u8PtrType);
                        }
                    }
                    else if (structType.StructName == "String")
                    {
                        // For String, call the as_ptr() method
                        var asPtrMethodName = "String::as_ptr";
                        var asPtrMethod = _module.GetFunction(asPtrMethodName);

                        if (asPtrMethod == null)
                        {
                            var errorLocation = GetLocation(context);
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "String type must have as_ptr() method for automatic coercion to *u8",
                                errorLocation
                            );
                            return null;
                        }

                        // Call String::as_ptr()
                        var receiverArg = new IrBorrowValue(argValue, asPtrMethod.Parameters[0].Type, false);
                        var resultTempName = $"%t{_tempCounter++}";
                        var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                        var methodCall = new IrCall(asPtrMethodName, u8PtrType, resultTempName);
                        methodCall.Location = GetLocation(context);
                        methodCall.Arguments.Add(receiverArg);
                        _currentBlock!.AddInstruction(methodCall);
                        arguments[i] = new IrVariable(resultTempName, u8PtrType);
                    }
                }

                // Coercion: String/Str → &String/&Str for reference parameters
                if (paramType is IrReferenceType expectedRefType &&
                    argValue.Type is IrStructType argStructType &&
                    (argStructType.StructName == "String" || argStructType.StructName == "Str") &&
                    expectedRefType.PointeeType is IrStructType expectedPointeeStruct &&
                    expectedPointeeStruct.StructName == argStructType.StructName)
                {
                    // Create a borrow (immutable reference) of the string value
                    var refType = _typeInterner.GetReferenceType(argStructType);
                    arguments[i] = new IrBorrowValue(argValue, refType, false);
                    continue;
                }

                // Coercion: &T → *T or &mut T → *T when parameter expects a pointer
                if (paramType is IrPointerType expectedPtrType &&
                    (argValue.Type is IrReferenceType argRefType || argValue.Type is IrMutReferenceType argMutRefType))
                {
                    var argPointeeType = argValue.Type is IrReferenceType r ? r.PointeeType :
                                        argValue.Type is IrMutReferenceType m ? m.PointeeType : null;

                    if (argPointeeType != null && argPointeeType.Equals(expectedPtrType.PointeeType))
                    {
                        // References and pointers have the same representation (both are addresses)
                        // so we can just reinterpret the reference as a pointer (zero-cost conversion)
                        if (argValue is IrVariable variable)
                        {
                            arguments[i] = new IrVariable(variable.Name, expectedPtrType);
                        }
                        else if (argValue is IrBorrowValue borrowValue)
                        {
                            // For borrow expressions, we need to evaluate them first, then cast
                            var tempName = $"%t{_tempCounter++}";
                            var moveOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Add,
                                argValue, new IrConstant(0, argValue.Type), argValue.Type);
                            _currentBlock!.AddInstruction(moveOp);
                            arguments[i] = new IrVariable(tempName, expectedPtrType);
                        }
                        else
                        {
                            // For other expressions, create a temp with pointer type
                            var tempName = $"%t{_tempCounter++}";
                            var moveOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Add,
                                argValue, new IrConstant(0, argValue.Type), argValue.Type);
                            _currentBlock!.AddInstruction(moveOp);
                            arguments[i] = new IrVariable(tempName, expectedPtrType);
                        }
                        continue;
                    }
                }
            }

            returnType = fpType.ReturnType;
            resultName = returnType is not IrVoidType ? $"%t{_tempCounter++}" : null;

            var indirectCall = new IrIndirectCall(funcExpr, returnType, resultName);
            indirectCall.Location = GetLocation(context);
            foreach (var arg in arguments)
            {
                indirectCall.Arguments.Add(arg);
            }

            _currentBlock!.AddInstruction(indirectCall);

            if (resultName != null)
            {
                return new IrVariable(resultName, returnType);
            }

            return null;
        }

        // Direct call - funcExpr should be an identifier
        if (funcExpr is not IrVariable funcVar)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Function call target must be an identifier or function pointer",
                errorLocation
            );
            return null;
        }

        var functionName = funcVar.Name;

        // Check if this is an associated function on an enum that needs instantiation (e.g., Option::FromPointer)
        if (functionName.Contains("::"))
        {
            var parts = functionName.Split("::");
            if (parts.Length == 2)
            {
                var typeName = parts[0];
                var methodName = parts[1];

                // Check if it's an enum type
                if (_symbols.HasEnum(typeName))
                {
                    var enumType = _symbols.LookupEnum(typeName)!;
                    if (enumType.GenericParameters.Count > 0)
                    {
                        // Need to infer type arguments and monomorphize the enum before instantiation
                        var typeSubstitutions = InferGenericEnumTypeArguments(enumType, methodName, arguments, _expectedType);

                        if (typeSubstitutions != null)
                        {
                            // Monomorphize the enum with inferred type arguments using the shared TypeParser
                            var monomorphizedEnum = _typeParser.SubstituteGenericTypes(enumType, typeSubstitutions) as IrEnumType;

                            if (monomorphizedEnum != null)
                            {
                                // Now instantiate the method with the monomorphized enum
                                var instantiatedFunc = _genericInstantiator.InstantiateEnumMethod(monomorphizedEnum, methodName, arguments);
                                if (instantiatedFunc != null)
                                {
                                    functionName = instantiatedFunc.Name;
                                }
                                else
                                {
                                    // Try just the method name (impl methods are currently stored without type prefix)
                                    functionName = methodName;
                                }
                            }
                        }
                        else
                        {
                            // Could not infer type arguments
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                $"Cannot infer generic type arguments for {typeName}::{methodName}. Please provide explicit type arguments.",
                                errorLocation
                            );
                            return null;
                        }
                    }
                }
            }
        }

        // Look up the function in the module to get its return type
        var function = _module.GetFunction(functionName);
        if (function == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unknown function: {functionName}",
                errorLocation
            );
            return null;
        }

        // Check argument count matches parameter count
        var nonVariadicCount = function.Parameters.Count(p => !p.IsVariadic);
        if (function.IsVariadic)
        {
            if (arguments.Count < nonVariadicCount)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Variadic function '{functionName}' expects at least {nonVariadicCount} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }
        else
        {
            if (arguments.Count != function.Parameters.Count)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Function {functionName} expects {function.Parameters.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }

        // Apply automatic &[T; N] → Slice<T> coercion when parameter type is Slice<T>
        for (int i = 0; i < arguments.Count; i++)
        {
            // Skip variadic parameters (they don't have a declared type)
            if (i >= function.Parameters.Count || function.Parameters[i].IsVariadic)
                continue;

            var paramType = function.Parameters[i].Type;
            var argValue = arguments[i];

            // Coerce &[T; N] → Slice<T>
            // When a pointer to an array is passed to a function expecting Slice<T>
            if (paramType is IrStructType sliceStruct &&
                sliceStruct.StructName == "Slice" &&
                argValue.Type is IrPointerType ptrToArrayType &&
                ptrToArrayType.PointeeType is IrArrayType ptrArrayType)
            {
                // Get the 'ptr' field to extract the element type T from Slice<T>
                var ptrField = sliceStruct.GetField("ptr");
                if (ptrField?.Type is IrPointerType slicePtrType)
                {
                    // Verify array element type matches Slice element type
                    if (slicePtrType.PointeeType.Equals(ptrArrayType.ElementType))
                    {
                        // Create pointer to array element (pointer to array decays to pointer to first element)
                        var arrayElemPtrType = _typeInterner.GetPointerType(ptrArrayType.ElementType);

                        // Cast the pointer-to-array to a pointer-to-element
                        // *[T; N] → *T (decay to pointer to first element)
                        var ptrValue = new IrCastValue(argValue, argValue.Type, arrayElemPtrType);

                        // Create the length constant
                        var lenValue = new IrConstant(ptrArrayType.Length, IrIntType.U32);

                        // Create Slice struct literal
                        var sliceFields = new Dictionary<string, IrValue>
                        {
                            { "ptr", ptrValue },
                            { "len", lenValue }
                        };

                        var sliceValue = new IrStructLiteral(sliceStruct, sliceFields);

                        // Slice structs containing pointers are passed by reference in C ABI
                        // (see CCodeGenerator.TypeContainsHeapData), so we need to create a borrow
                        // of the slice struct, not just the struct value itself
                        var sliceRefType = _typeInterner.GetReferenceType(sliceStruct);
                        arguments[i] = new IrBorrowValue(sliceValue, sliceRefType, false);
                        continue;
                    }
                }
            }
        }

        // Apply automatic Str/String → *u8 coercion only when parameter type is *u8
        for (int i = 0; i < arguments.Count; i++)
        {
            // Skip variadic parameters (they don't have a declared type)
            if (i >= function.Parameters.Count || function.Parameters[i].IsVariadic)
                continue;

            var paramType = function.Parameters[i].Type;
            var argValue = arguments[i];

            // Only coerce if parameter is *u8 and argument is Str or String
            if (paramType is IrPointerType ptrType &&
                ptrType.PointeeType.Equals(IrIntType.U8) &&
                argValue.Type is IrStructType structType &&
                (structType.StructName == "Str" || structType.StructName == "String"))
            {
                if (structType.StructName == "Str")
                {
                    // If argValue is a struct literal, extract the ptr field directly (no instruction needed)
                    if (argValue is IrStructLiteral strLiteral)
                    {
                        if (!strLiteral.FieldValues.TryGetValue("ptr", out var ptrValue))
                        {
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "Str struct literal must have a 'ptr' field",
                                errorLocation
                            );
                            return null;
                        }
                        arguments[i] = ptrValue;  // Use the ptr value directly
                    }
                    else
                    {
                        // For Str variables (not literals), we need the member access
                        var ptrField = structType.GetField("ptr");
                        if (ptrField == null)
                        {
                            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                            _diagnostics.ReportError(
                                ErrorCodes.InvalidExpressionType,
                                "Str struct must have a 'ptr' field",
                                errorLocation
                            );
                            return null;
                        }

                        var ptrTempName = $"%t{_tempCounter++}";
                        var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                        var ptrFieldAccess = new IrMemberAccess(ptrTempName, argValue, "ptr", u8PtrType, ptrField.Offset);
                        _currentBlock!.AddInstruction(ptrFieldAccess);
                        arguments[i] = new IrVariable(ptrTempName, u8PtrType);
                    }
                }
                else if (structType.StructName == "String")
                {
                    // For String, call the as_ptr() method
                    var asPtrMethodName = "String::as_ptr";
                    var asPtrMethod = _module.GetFunction(asPtrMethodName);

                    if (asPtrMethod == null)
                    {
                        var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                        _diagnostics.ReportError(
                            ErrorCodes.InvalidExpressionType,
                            "String type must have as_ptr() method for automatic coercion to *u8",
                            errorLocation
                        );
                        return null;
                    }

                    // Call String::as_ptr()
                    var receiverArg = new IrBorrowValue(argValue, asPtrMethod.Parameters[0].Type, false);
                    var resultTempName = $"%t{_tempCounter++}";
                    var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                    var methodCall = new IrCall(asPtrMethodName, u8PtrType, resultTempName);
                    methodCall.Arguments.Add(receiverArg);
                    _currentBlock!.AddInstruction(methodCall);
                    arguments[i] = new IrVariable(resultTempName, u8PtrType);
                }
            }
        }

        // Apply automatic String/Str → &String/&Str coercion when parameter type is a reference
        for (int i = 0; i < arguments.Count; i++)
        {
            // Skip variadic parameters (they don't have a declared type)
            if (i >= function.Parameters.Count || function.Parameters[i].IsVariadic)
                continue;

            var paramType = function.Parameters[i].Type;
            var argValue = arguments[i];

            // Coercion: String/Str → &String/&Str for reference parameters
            if (paramType is IrReferenceType expectedRefType &&
                argValue.Type is IrStructType argStructType &&
                (argStructType.StructName == "String" || argStructType.StructName == "Str") &&
                expectedRefType.PointeeType is IrStructType expectedPointeeStruct &&
                expectedPointeeStruct.StructName == argStructType.StructName)
            {
                // Create a borrow (immutable reference) of the string value
                var refType = _typeInterner.GetReferenceType(argStructType);
                arguments[i] = new IrBorrowValue(argValue, refType, false);
                continue;
            }
        }

        // Apply automatic &T → *T coercion for extern function calls
        for (int i = 0; i < arguments.Count; i++)
        {
            // Skip variadic parameters (they don't have a declared type)
            if (i >= function.Parameters.Count || function.Parameters[i].IsVariadic)
                continue;

            var paramType = function.Parameters[i].Type;
            var argValue = arguments[i];

            // Coercion: &T → *T or &mut T → *T when parameter expects a pointer
            if (paramType is IrPointerType expectedPtrType &&
                (argValue.Type is IrReferenceType argRefType || argValue.Type is IrMutReferenceType argMutRefType))
            {
                var argPointeeType = argValue.Type is IrReferenceType r ? r.PointeeType :
                                    argValue.Type is IrMutReferenceType m ? m.PointeeType : null;

                if (argPointeeType != null && argPointeeType.Equals(expectedPtrType.PointeeType))
                {
                    // References and pointers have the same representation (both are addresses)
                    // so we can just reinterpret the reference as a pointer (zero-cost conversion)
                    if (argValue is IrVariable variable)
                    {
                        arguments[i] = new IrVariable(variable.Name, expectedPtrType);
                    }
                    else if (argValue is IrBorrowValue borrowValue)
                    {
                        // For borrow expressions, we need to evaluate them first, then cast
                        var tempName = $"%t{_tempCounter++}";
                        var moveOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Add,
                            argValue, new IrConstant(0, argValue.Type), argValue.Type);
                        _currentBlock!.AddInstruction(moveOp);
                        arguments[i] = new IrVariable(tempName, expectedPtrType);
                    }
                    else
                    {
                        // For other expressions, create a temp with pointer type
                        var tempName = $"%t{_tempCounter++}";
                        var moveOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Add,
                            argValue, new IrConstant(0, argValue.Type), argValue.Type);
                        _currentBlock!.AddInstruction(moveOp);
                        arguments[i] = new IrVariable(tempName, expectedPtrType);
                    }
                    continue;
                }
            }
        }

        // Insert implicit casts for arguments where needed
        // (e.g., u32 -> i32 for same-bit-width conversions)
        // Note: Str -> *u8 coercion is handled above (only when param type is *u8)
        for (int i = 0; i < arguments.Count; i++)
        {
            var argType = arguments[i].Type;

            // For variadic functions, arguments beyond the non-variadic params don't have a corresponding parameter
            IrType? paramType = null;
            if (i < function.Parameters.Count && !function.Parameters[i].IsVariadic)
            {
                paramType = function.Parameters[i].Type;
            }

            // If types don't exactly match but are compatible integer types of same width (non-variadic only)
            if (paramType != null &&
                !argType.Equals(paramType) &&
                argType is IrIntType argInt &&
                paramType is IrIntType paramInt &&
                argInt.BitWidth == paramInt.BitWidth)
            {
                // Same-size integer cast - just reinterpret the bits
                if (arguments[i] is IrConstant constant)
                {
                    // For constants, create new constant with target type
                    arguments[i] = new IrConstant(constant.Value, paramType);
                }
                else if (arguments[i] is IrVariable variable)
                {
                    // For variables, create new variable reference with target type (zero-cost cast)
                    arguments[i] = new IrVariable(variable.Name, paramType);
                }
                else
                {
                    // For other expressions, create a temp variable with the target type
                    // The value is already computed, we just need to reference it with the new type
                    var castTempName = $"%t{_tempCounter++}";
                    var moveOp = new IrBinaryOp(castTempName, IrBinaryOp.OpKind.Add,
                        arguments[i], new IrConstant(0, arguments[i].Type), arguments[i].Type);
                    _currentBlock!.AddInstruction(moveOp);
                    arguments[i] = new IrVariable(castTempName, paramType);
                }
            }
        }

        // Create the call instruction
        returnType = function.ReturnType;
        resultName = returnType is not IrVoidType ? $"%t{_tempCounter++}" : null;

        var call = new IrCall(functionName, returnType, resultName);
        call.Location = GetLocation(context);
        foreach (var arg in arguments)
        {
            call.Arguments.Add(arg);
        }

        _currentBlock!.AddInstruction(call);

        // Return the result variable if non-void
        if (resultName != null)
        {
            return new IrVariable(resultName, returnType);
        }

        return null;
    }

    /// <summary>
    /// Try to resolve generic type parameters from a method call
    /// Example: vec.push(42i32) where vec is Vec<UnresolvedGeneric> → infer T = i32
    /// </summary>
    private IrStructType? TryResolveGenericFromMethodCall(IrPartiallyResolvedGenericType partialType, string methodName, List<IrValue> methodArgs)
    {
        // Look up the method template for this generic type
        var templateKey = $"{partialType.GenericTypeName}::{methodName}";

        if (!_genericInstantiator.TryGetMethodTemplate(templateKey, out var template) || template == null)
        {
            // Method not found or not generic
            return null;
        }

        var (genericParams, funcDecl, _) = template;

        // Parse the method's parameter types from the template
        // Note: 'self' parameter is handled specially in the grammar and may not be in parameterList
        var paramContexts = funcDecl.parameterList()?.parameter()?.ToList() ?? new List<NovusParser.ParameterContext>();

        if (paramContexts.Count == 0)
        {
            // No non-self parameters to infer from
            return null;
        }

        // Try to infer generic type from the method arguments
        // For Vec::push(&mut self, value: T), paramContexts contains just [value: T]
        // methodArgs contains the user-provided arguments (not including self)
        // So paramContexts[0] corresponds to methodArgs[0]

        var unresolvedTypeToResolvedType = new Dictionary<IrUnresolvedGenericType, IrType>();

        for (int i = 0; i < paramContexts.Count && i < methodArgs.Count; i++)
        {
            var paramCtx = paramContexts[i];
            var paramTypeName = paramCtx.type().GetText();
            var argType = methodArgs[i].Type;

            // Check if parameter type is a generic parameter (e.g., "T")
            if (genericParams.Contains(paramTypeName))
            {
                // This parameter has a generic type - use the argument's type
                var paramIndex = genericParams.IndexOf(paramTypeName);

                if (paramIndex < partialType.TypeArguments.Count &&
                    partialType.TypeArguments[paramIndex] is IrUnresolvedGenericType unresolvedType)
                {
                    unresolvedTypeToResolvedType[unresolvedType] = argType;
                }
            }
        }

        // Resolve all unresolved type parameters
        bool allResolved = true;
        foreach (var typeArg in partialType.TypeArguments)
        {
            if (typeArg is IrUnresolvedGenericType unresolvedType)
            {
                if (unresolvedTypeToResolvedType.TryGetValue(unresolvedType, out var resolvedType))
                {
                    unresolvedType.ResolvedType = resolvedType;
                }
                else
                {
                    allResolved = false;
                }
            }
        }

        if (!allResolved)
        {
            return null; // Couldn't resolve all type parameters
        }

        // Now that all type parameters are resolved, create the monomorphized struct
        var resolvedTypeArgs = partialType.GetResolvedTypeArguments();

        // Find the generic struct template
        var genericStruct = _symbols.GetLocalStructs().Values.FirstOrDefault(s =>
            s.StructName == partialType.GenericTypeName && s.GenericParameters.Count > 0);

        if (genericStruct == null)
        {
            return null;
        }

        // Create monomorphized struct
        var monomorphizedStruct = IrStructType.Monomorphize(genericStruct, resolvedTypeArgs);

        // Cache the monomorphized struct
        var cacheKey = monomorphizedStruct.CacheKey ?? monomorphizedStruct.Name;
        if (_symbols.LookupMonomorphizedStruct(cacheKey) == null)
        {
            _symbols.RegisterMonomorphizedStruct(cacheKey, monomorphizedStruct);
        }

        // Update the partial type to mark it as fully resolved
        partialType.FullyResolvedType = monomorphizedStruct;

        return monomorphizedStruct;
    }

    /// <summary>
    /// Handle method calls (e.g., v.len())
    /// Desugars to: Type::method(receiver, args...)
    /// </summary>
    private object? HandleMethodCallIr(NovusParser.CallExprContext callCtx, NovusParser.MemberAccessExprContext memberAccessCtx)
    {
        // Get the receiver (the thing before the dot)
        var receiverExpr = memberAccessCtx.expression();
        var methodName = memberAccessCtx.IDENTIFIER().GetText();

        // Evaluate the receiver
        var receiver = (IrValue?)Visit(receiverExpr);
        if (receiver == null)
        {
            var errorLocation = GetLocation(callCtx);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Method call receiver is null",
                errorLocation
            );
            return null;
        }

        // Handle unresolved generic types - resolve them from method arguments
        if (receiver.Type is IrPartiallyResolvedGenericType partialType)
        {
            // Parse method arguments to infer the generic type
            var methodArgs = new List<IrValue>();
            if (callCtx.argumentList() != null)
            {
                foreach (var argCtx in callCtx.argumentList().expression())
                {
                    var argValue = (IrValue?)Visit(argCtx);
                    if (argValue != null)
                    {
                        methodArgs.Add(argValue);
                    }
                }
            }

            // Try to resolve the generic type from method arguments
            var resolvedType = TryResolveGenericFromMethodCall(partialType, methodName, methodArgs);
            if (resolvedType != null)
            {
                // Update the receiver's type
                if (receiver is IrVariable irVar)
                {
                    receiver = new IrVariable(irVar.Name, resolvedType);

                    // Also update the local variable's type in the symbol table
                    if (_localVariables.TryGetValue(irVar.Name, out var localVar))
                    {
                        localVar.Type = resolvedType;
                    }
                }
            }
            else
            {
                var errorLocation = GetLocation(callCtx);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Cannot infer generic type parameters for '{partialType.GenericTypeName}' from method call '{methodName}'",
                    errorLocation
                );
                return null;
            }
        }

        // Get the type name for method lookup
        string typeName;
        var receiverType = receiver.Type;

        if (receiverType is IrStructType structType)
        {
            typeName = structType.StructName;  // Use base name, not full generic name
        }
        else if (receiverType is IrEnumType enumType)
        {
            typeName = enumType.EnumName;
        }
        else if (receiverType is IrPointerType ptrType)
        {
            // Auto-dereference pointers
            if (ptrType.PointeeType is IrStructType pointeeStruct)
            {
                typeName = pointeeStruct.StructName;  // Use base name, not full generic name
            }
            else if (ptrType.PointeeType is IrEnumType pointeeEnum)
            {
                typeName = pointeeEnum.EnumName;
            }
            else if (ptrType.PointeeType is IrGenericType genericType)
            {
                // Handle generic type parameters (e.g., *K where K: Hash)
                // During monomorphization, substitute K with concrete type from _currentTypeSubstitutions
                if (_currentTypeSubstitutions != null && _currentTypeSubstitutions.TryGetValue(genericType.ParameterName, out var concreteType))
                {
                    // Use the concrete type name for method lookup
                    typeName = concreteType.Name;
                }
                else
                {
                    // Generic type not substituted yet - this is an error during monomorphization
                    var errorLocation = GetLocation(callCtx);
                    _diagnostics.ReportError(
                        ErrorCodes.CannotCallMethodOnType,
                        $"Cannot call methods on unresolved generic type parameter: {genericType.ParameterName}",
                        errorLocation
                    );
                    return null;
                }
            }
            else
            {
                // Allow methods on primitive types (u64, bool, etc.)
                typeName = ptrType.PointeeType.Name;
            }
        }
        else if (receiverType is IrReferenceType refType)
        {
            // Auto-dereference immutable references
            if (refType.PointeeType is IrStructType refStruct)
            {
                typeName = refStruct.StructName;
            }
            else if (refType.PointeeType is IrEnumType refEnum)
            {
                typeName = refEnum.EnumName;
            }
            else if (refType.PointeeType is IrGenericType genericType)
            {
                // Handle generic type parameters (e.g., &K where K: Hash)
                // During monomorphization, substitute K with concrete type from _currentTypeSubstitutions
                if (_currentTypeSubstitutions != null && _currentTypeSubstitutions.TryGetValue(genericType.ParameterName, out var concreteType))
                {
                    // Use the concrete type name for method lookup
                    typeName = concreteType.Name;
                }
                else
                {
                    // Generic type not substituted yet - this is an error during monomorphization
                    var errorLocation = GetLocation(callCtx);
                    _diagnostics.ReportError(
                        ErrorCodes.CannotCallMethodOnType,
                        $"Cannot call methods on unresolved generic type parameter: {genericType.ParameterName}",
                        errorLocation
                    );
                    return null;
                }
            }
            else if (refType.PointeeType is IrArrayType arrayType && arrayType.Length == -1)
            {
                // Handle unsized slices &[T] which are fat pointers at runtime
                // They should work like Slice<T> struct
                // For now, only .len() is supported as an intrinsic
                if (methodName == "len")
                {
                    // Treat &[T] like Slice<T> and access the .len field
                    // At runtime, &[T] parameters are passed as Slice<T> structs
                    // So we can access the len field directly from the receiver

                    // The receiver should be the slice value (which is actually a Slice<T> at runtime)
                    // We need to access the .len field
                    var lenFieldName = "len";
                    var lenResultName = $"%t{_tempCounter++}";

                    // Try to find Slice<ElementType> to get the field offset
                    var elementType = arrayType.ElementType;
                    var sliceCacheKey = $"Slice<{elementType.Name}>";
                    var sliceStruct = _module.Structs.FirstOrDefault(s => s.CacheKey == sliceCacheKey);

                    if (sliceStruct != null)
                    {
                        var lenField = sliceStruct.GetField(lenFieldName);
                        if (lenField != null)
                        {
                            // Generate member access to the len field
                            var memberAccess = new IrMemberAccess(lenResultName, receiver, lenFieldName, lenField.Type, lenField.Offset);
                            _currentBlock!.AddInstruction(memberAccess);
                            return new IrVariable(lenResultName, lenField.Type);
                        }
                    }

                    // Fallback error: Slice<T> struct not found
                    var lenErrorLocation = GetLocation(callCtx);
                    _diagnostics.ReportError(
                        ErrorCodes.MethodNotFound,
                        $"Cannot call .len() on slice '{receiverType.Name}': Slice<{elementType.Name}> type not found",
                        lenErrorLocation,
                        helpTexts: new List<string>
                        {
                            "Make sure to import std::memory::slice",
                            "The Slice<T> struct must be available for slice operations"
                        }
                    );
                    return null;
                }

                var errorLocation = GetLocation(callCtx);
                _diagnostics.ReportError(
                    ErrorCodes.MethodNotFound,
                    $"Method '{methodName}' not found for slice type '{receiverType.Name}'",
                    errorLocation,
                    helpTexts: new List<string>
                    {
                        "Slices currently only support the .len() method"
                    }
                );
                return null;
            }
            else
            {
                // Allow methods on primitive types (u64, bool, etc.)
                typeName = refType.PointeeType.Name;
            }
        }
        else if (receiverType is IrMutReferenceType mutRefType)
        {
            // Auto-dereference mutable references
            if (mutRefType.PointeeType is IrStructType mutRefStruct)
            {
                typeName = mutRefStruct.StructName;
            }
            else if (mutRefType.PointeeType is IrEnumType mutRefEnum)
            {
                typeName = mutRefEnum.EnumName;
            }
            else if (mutRefType.PointeeType is IrGenericType genericType)
            {
                // Handle generic type parameters (e.g., &mut K where K: Hash)
                // During monomorphization, substitute K with concrete type from _currentTypeSubstitutions
                if (_currentTypeSubstitutions != null && _currentTypeSubstitutions.TryGetValue(genericType.ParameterName, out var concreteType))
                {
                    // Use the concrete type name for method lookup
                    typeName = concreteType.Name;
                }
                else
                {
                    // Generic type not substituted yet - this is an error during monomorphization
                    var errorLocation = GetLocation(callCtx);
                    _diagnostics.ReportError(
                        ErrorCodes.CannotCallMethodOnType,
                        $"Cannot call methods on unresolved generic type parameter: {genericType.ParameterName}",
                        errorLocation
                    );
                    return null;
                }
            }
            else
            {
                // Allow methods on primitive types (u64, bool, etc.)
                typeName = mutRefType.PointeeType.Name;
            }
        }
        else if (receiverType is IrIntType || receiverType is IrBoolType)
        {
            // Primitive types can have trait method implementations (e.g., u32::hash())
            typeName = receiverType.Name;
        }
        else if (receiverType is IrGenericType genericType)
        {
            // Handle direct generic type parameters (e.g., K where K: Hash)
            // During monomorphization, substitute K with concrete type from _currentTypeSubstitutions
            if (_currentTypeSubstitutions != null && _currentTypeSubstitutions.TryGetValue(genericType.ParameterName, out var concreteType))
            {
                // Use the concrete type name for method lookup
                typeName = concreteType.Name;
            }
            else
            {
                // Generic type not substituted yet - this is an error during monomorphization
                var errorLocation = GetLocation(callCtx);
                _diagnostics.ReportError(
                    ErrorCodes.CannotCallMethodOnType,
                    $"Cannot call methods on unresolved generic type parameter: {genericType.ParameterName}",
                    errorLocation
                );
                return null;
            }
        }
        else
        {
            var errorLocation = GetLocation(callCtx);
            _diagnostics.ReportError(
                ErrorCodes.CannotCallMethodOnType,
                $"Cannot call methods on type: {receiverType.Name}",
                errorLocation
            );
            return null;
        }

        // Build the mangled function name: Type::method
        // Note: For monomorphized generic types, we'll try Type::method first,
        // then fall back to instantiation if needed
        var mangledMethodName = Generics.InstantiationKeyBuilder.BuildInherentMethodName(typeName, methodName);

        // Look up the method
        var method = _module.GetFunction(mangledMethodName);

        // If method not found, try to instantiate it for monomorphized structs or enums
        if (method == null)
        {
            IrStructType? monomorphizedStruct = null;
            IrEnumType? monomorphizedEnum = null;

            // Check if receiver is a monomorphized struct
            if (receiverType is IrStructType receiverStruct && receiverStruct.CacheKey != null)
            {
                monomorphizedStruct = receiverStruct;
            }
            // Check if receiver is a pointer to a monomorphized struct (&Vec<i32>, &mut Vec<i32>)
            else if (receiverType is IrPointerType ptrType && ptrType.PointeeType is IrStructType pointeeStruct && pointeeStruct.CacheKey != null)
            {
                monomorphizedStruct = pointeeStruct;
            }
            // Check if receiver is a reference to a monomorphized struct (&Vec<i32>)
            else if (receiverType is IrReferenceType refType && refType.PointeeType is IrStructType refPointeeStruct && refPointeeStruct.CacheKey != null)
            {
                monomorphizedStruct = refPointeeStruct;
            }
            // Check if receiver is a mutable reference to a monomorphized struct (&mut Vec<i32>)
            else if (receiverType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType mutRefPointeeStruct && mutRefPointeeStruct.CacheKey != null)
            {
                monomorphizedStruct = mutRefPointeeStruct;
            }
            // Check if receiver is a monomorphized enum
            else if (receiverType is IrEnumType receiverEnum && receiverEnum.CacheKey != null)
            {
                monomorphizedEnum = receiverEnum;
            }
            // Check if receiver is a pointer to a monomorphized enum (&Option<i32>, &mut Option<i32>)
            else if (receiverType is IrPointerType ptrType2 && ptrType2.PointeeType is IrEnumType pointeeEnum && pointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = pointeeEnum;
            }
            // Check if receiver is a reference to a monomorphized enum (&Option<i32>)
            else if (receiverType is IrReferenceType refType2 && refType2.PointeeType is IrEnumType refPointeeEnum && refPointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = refPointeeEnum;
            }
            // Check if receiver is a mutable reference to a monomorphized enum (&mut Option<i32>)
            else if (receiverType is IrMutReferenceType mutRefType2 && mutRefType2.PointeeType is IrEnumType mutRefPointeeEnum && mutRefPointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = mutRefPointeeEnum;
            }
            // Check if receiver is a generic enum template (not yet monomorphized)
            // This can happen when a variable has a declared type that includes type arguments,
            // but the receiver's type is still the generic template (e.g., Option<T> instead of Option<i32>)
            else if (receiverType is IrEnumType receiverEnumTemplate &&
                     receiverEnumTemplate.GenericParameters.Count > 0 &&
                     receiverEnumTemplate.CacheKey == null)
            {
                // Try to find the monomorphized version by looking up the variable's declared type
                // This happens when: let opt1: Option<i32> = ... then opt1.is_some()
                // The receiver variable should have the monomorphized type in _localVariables or function parameters
                if (receiver is IrVariable irVar)
                {
                    // Check local variables first
                    if (_localVariables.TryGetValue(irVar.Name, out var localVar))
                    {
                        // The local variable's type should be the monomorphized version
                        if (localVar.Type is IrEnumType localEnumType && localEnumType.CacheKey != null)
                        {
                            monomorphizedEnum = localEnumType;
                            // Update the receiver to use the correct monomorphized type
                            receiver = new IrVariable(irVar.Name, localEnumType);
                        }
                    }
                    // Check function parameters
                    else if (_currentFunction != null)
                    {
                        var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == irVar.Name);
                        if (param != null && param.Type is IrEnumType paramEnumType && paramEnumType.CacheKey != null)
                        {
                            monomorphizedEnum = paramEnumType;
                            // Update the receiver to use the correct monomorphized type
                            receiver = new IrVariable(irVar.Name, paramEnumType);
                        }
                    }
                }
            }

            if (monomorphizedStruct != null)
            {
                method = _genericInstantiator.InstantiateStructMethod(monomorphizedStruct, methodName);
            }
            else if (monomorphizedEnum != null)
            {
                // Parse arguments to pass to instantiation (needed for type inference)
                var methodArgs = new List<IrValue>();
                if (callCtx.argumentList() != null)
                {
                    foreach (var argCtx in callCtx.argumentList().expression())
                    {
                        var argValue = (IrValue?)Visit(argCtx);
                        if (argValue != null)
                        {
                            methodArgs.Add(argValue);
                        }
                    }
                }

                method = _genericInstantiator.InstantiateEnumMethod(monomorphizedEnum, methodName, methodArgs);
            }
        }

        // If method not found for structs, try to instantiate it for monomorphized enums
        if (method == null)
        {
            IrEnumType? monomorphizedEnum = null;

            // Check if receiver is a monomorphized enum (e.g., Option<i32>)
            if (receiverType is IrEnumType receiverEnum && receiverEnum.CacheKey != null)
            {
                monomorphizedEnum = receiverEnum;
            }
            // Check if receiver is a pointer to a monomorphized enum
            else if (receiverType is IrPointerType ptrType && ptrType.PointeeType is IrEnumType pointeeEnum && pointeeEnum.CacheKey != null)
            {
                monomorphizedEnum = pointeeEnum;
            }

            if (monomorphizedEnum != null)
            {
                // Build complete arguments list: [receiver, ...user_args]
                // This is needed for type inference to work properly
                var allArgs = new List<IrValue> { receiver };

                // Add user-provided arguments
                if (callCtx.argumentList() != null)
                {
                    foreach (var argCtx in callCtx.argumentList().expression())
                    {
                        var argValue = (IrValue?)Visit(argCtx);
                        if (argValue != null)
                        {
                            allArgs.Add(argValue);
                        }
                    }
                }

                // Instantiate the generic enum method with all arguments (including receiver)
                // The method will infer generic type parameters from the argument types
                method = _genericInstantiator.InstantiateEnumMethod(monomorphizedEnum, methodName, allArgs);
            }
        }

        // If method still not found, try looking for trait method implementations
        // Trait methods are mangled as: TypeName_TraitName_methodName
        if (method == null)
        {
            // Search for any function that matches the pattern: typeName_*_methodName
            // This handles trait implementations for primitive types and other types
            var traitMethodPattern = $"{typeName}_";
            var traitMethodSuffix = $"_{methodName}";
            method = _module.Functions.FirstOrDefault(f =>
                f.Name.StartsWith(traitMethodPattern) &&
                f.Name.EndsWith(traitMethodSuffix) &&
                f.Name.Length > traitMethodPattern.Length + traitMethodSuffix.Length - 1);
        }

        if (method == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                $"Method '{methodName}' not found for type '{typeName}'",
                errorLocation
            );
            return null;
        }

        // Build arguments list: [receiver, ...user_args]
        // Check if we need to borrow the receiver (for &self or &mut self)
        IrValue receiverArg = receiver;
        if (method.Parameters.Count > 0)
        {
            var firstParamType = method.Parameters[0].Type;

            // If method expects a pointer/reference but we have a value, borrow it
            if ((firstParamType is IrPointerType || firstParamType is IrReferenceType || firstParamType is IrMutReferenceType)
                && receiver.Type is not IrPointerType && receiver.Type is not IrReferenceType && receiver.Type is not IrMutReferenceType)
            {
                // IMPORTANT FIX: If the receiver was loaded from a field access (e.g., self.buffer),
                // we need to borrow the FIELD directly, not the loaded copy.
                // Check if the last instruction is a member access that produced this receiver variable.
                IrValue valueToBoflow = receiver;

                if (receiver is IrVariable receiverVar && _currentBlock != null)
                {
                    // Look for the member access instruction that produced this variable
                    // Search backwards through the block's instructions
                    IrMemberAccess? foundMemberAccess = null;
                    for (int i = _currentBlock.Instructions.Count - 1; i >= 0; i--)
                    {
                        var inst = _currentBlock.Instructions[i];
                        if (inst is IrMemberAccess memberAccess &&
                            memberAccess.ResultName == receiverVar.Name)
                        {
                            foundMemberAccess = memberAccess;
                            _currentBlock.Instructions.RemoveAt(i);
                            break;
                        }
                        // Stop searching if we hit an instruction that might use this variable
                        // (this avoids removing a member access that was already used)
                        if (inst is IrCall || inst is IrStore)
                        {
                            break;
                        }
                    }

                    if (foundMemberAccess != null)
                    {
                        // Found it! Create a field reference instead of using the loaded value
                        valueToBoflow = new IrFieldReference(foundMemberAccess.Struct, foundMemberAccess.FieldName, receiver.Type);
                    }
                }

                // Wrap receiver in IrBorrowValue to take its address
                bool isMutable = firstParamType is IrMutReferenceType || firstParamType is IrPointerType;
                receiverArg = new IrBorrowValue(valueToBoflow, firstParamType, isMutable);
            }
        }

        var arguments = new List<IrValue> { receiverArg };

        // Add user-provided arguments
        if (callCtx.argumentList() != null)
        {
            foreach (var argCtx in callCtx.argumentList().expression())
            {
                var argValue = (IrValue?)Visit(argCtx);
                if (argValue != null)
                {
                    arguments.Add(argValue);
                }
            }
        }

        // Validate argument count
        var nonVariadicCount = method.Parameters.Count(p => !p.IsVariadic);
        if (method.IsVariadic)
        {
            if (arguments.Count < nonVariadicCount)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Variadic method '{methodName}' expects at least {nonVariadicCount} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }
        else
        {
            if (arguments.Count != method.Parameters.Count)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Method {methodName} expects {method.Parameters.Count} arguments, got {arguments.Count}",
                    errorLocation
                );
                return null;
            }
        }

        // Note: Str -> *u8 coercion is already handled in VisitCallExpr

        // Create the call instruction
        // Use the actual function name from the method (e.g., "Vec_push" for monomorphized generics)
        // instead of the mangled name (e.g., "Vec::push")
        var returnType = method.ReturnType;
        var resultName = returnType is not IrVoidType ? $"%t{_tempCounter++}" : null;

        var call = new IrCall(method.Name, returnType, resultName);
        call.Location = GetLocation(callCtx);
        foreach (var arg in arguments)
        {
            call.Arguments.Add(arg);
        }

        _currentBlock!.AddInstruction(call);

        // Return the result variable if non-void
        if (resultName != null)
        {
            return new IrVariable(resultName, returnType);
        }

        return null;
    }

    public override object? VisitBorrowExpr([NotNull] NovusParser.BorrowExprContext context)
    {
        var exprContext = context.expression();

        // Check if this is a mutable borrow (&mut) or immutable borrow (&)
        bool isMutable = context.GetChild(1)?.GetText() == "mut";

        // Handle function pointers specially (backward compatibility)
        if (exprContext.Start.Type == NovusLexer.IDENTIFIER &&
            exprContext.ChildCount == 1)
        {
            var name = exprContext.GetText();

            // Check if it's a function (for function pointers)
            var function = _module.GetFunction(name);
            if (function != null)
            {
                // Create function pointer type from function signature
                var paramTypes = function.Parameters.Select(p => p.Type).ToList();
                var fpType = _typeInterner.GetFunctionPointerType(paramTypes, function.ReturnType);
                return new IrFunctionAddress(name, fpType);
            }
        }

        // For variables, struct members, array elements, etc., create a pointer
        // In Novus, & produces pointer types, not reference types
        // Visit the expression to get its value
        var value = Visit(exprContext) as IrValue;

        // If expression had an error, bail out
        if (value == null)
        {
            return null;
        }

        // Create a pointer type (& in Novus produces *T, not &T)
        var ptrType = _typeInterner.GetPointerType(value.Type);

        // For code generation, pointers are addresses
        // We return the value itself - the semantic analyzer will track borrowing
        // At codegen time, we'll take the address of the value

        // Create a "borrow" value that wraps the original value with pointer type
        return new IrBorrowValue(value, ptrType, isMutable);
    }

    public override object? VisitIndexExpr([NotNull] NovusParser.IndexExprContext context)
    {
        var baseExpr = Visit(context.expression(0)) as IrValue;
        var indexExpr = Visit(context.expression(1)) as IrValue;

        // If either expression had an error, bail out
        if (baseExpr == null || indexExpr == null)
        {
            return null;
        }

        // Handle array indexing
        if (baseExpr.Type is IrArrayType arrayType)
        {
            // Create an index access instruction for arrays
            var tempName = $"%t{_tempCounter++}";
            var indexAccess = new IrIndexAccess(tempName, baseExpr, indexExpr, arrayType.ElementType);
            indexAccess.Location = GetLocation(context);
            _currentBlock!.AddInstruction(indexAccess);

            // Track struct array element access for optimization
            if (arrayType.ElementType is IrStructType)
            {
                _indexAccessTemps[tempName] = (baseExpr, indexExpr, arrayType.ElementType);
            }

            return new IrVariable(tempName, arrayType.ElementType);
        }

        // Handle pointer indexing: ptr[index] = *(ptr + index * sizeof(T))
        if (baseExpr.Type is IrPointerType ptrType)
        {
            // Create an index access instruction for pointers
            var tempName = $"%t{_tempCounter++}";
            var indexAccess = new IrIndexAccess(tempName, baseExpr, indexExpr, ptrType.PointeeType);
            indexAccess.Location = GetLocation(context);
            _currentBlock!.AddInstruction(indexAccess);

            // Track struct pointer element access for optimization
            if (ptrType.PointeeType is IrStructType)
            {
                _indexAccessTemps[tempName] = (baseExpr, indexExpr, ptrType.PointeeType);
            }

            return new IrVariable(tempName, ptrType.PointeeType);
        }

        var errorLocation = GetLocation(context);
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot index into non-array/non-pointer type: {baseExpr.Type.Name}",
            errorLocation
        );
        return null;
    }

    public override object? VisitArrayLiteral([NotNull] NovusParser.ArrayLiteralContext context)
    {
        var expressions = context.expression();
        var elements = new List<IrValue>();

        // Visit all element expressions
        foreach (var exprCtx in expressions)
        {
            var value = Visit(exprCtx) as IrValue;
            if (value == null)
            {
                // Error already reported by child visitor, bail out
                return null;
            }
            elements.Add(value);
        }

        if (elements.Count == 0)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Array literals cannot be empty",
                errorLocation
            );
            return null;
        }

        // Infer array type from first element
        var elementType = elements[0].Type;
        var arrayType = _typeInterner.GetArrayType(elementType, elements.Count);

        // Create array literal value
        var arrayLiteral = new IrArrayLiteral(arrayType);
        foreach (var elem in elements)
        {
            // TODO: Check that all elements have compatible types
            arrayLiteral.Elements.Add(elem);
        }

        return arrayLiteral;
    }

    public override object? VisitArrayRepeatLiteral([NotNull] NovusParser.ArrayRepeatLiteralContext context)
    {
        // Array repeat literal: [value; count]
        var expressions = context.expression();
        if (expressions.Length != 2)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Array repeat literal must have exactly 2 expressions",
                errorLocation
            );
            return null;
        }

        // Visit the value expression
        var value = (IrValue)Visit(expressions[0])!;

        // Visit the count expression - must be a constant
        var countExpr = (IrValue)Visit(expressions[1])!;
        if (countExpr is not IrConstant countConstant)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidArrayRepeatCount,
                "Array repeat count must be a compile-time constant",
                errorLocation
            );
            return null;
        }

        // Extract count value (handle all integer types)
        int count = countConstant.Type switch
        {
            IrIntType intType => intType.IsSigned switch
            {
                true => intType.BitWidth switch
                {
                    8 => (sbyte)countConstant.Value,
                    16 => (short)countConstant.Value,
                    32 => (int)countConstant.Value,
                    64 => (int)(long)countConstant.Value,
                    _ => 0  // ERROR: $"Unsupported signed integer bit width: {intType.BitWidth}"
                },
                false => intType.BitWidth switch
                {
                    8 => (byte)countConstant.Value,
                    16 => (ushort)countConstant.Value,
                    32 => (int)(uint)countConstant.Value,
                    64 => (int)(ulong)countConstant.Value,
                    _ => 0  // ERROR: $"Unsupported unsigned integer bit width: {intType.BitWidth}
                }
            },
            _ => 0  // ERROR: "Array repeat count must be an integer"
        };

        if (count < 0)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidArrayRepeatCount,
                "Array repeat count must be non-negative",
                errorLocation
            );
            return null;
        }

        // Create array type
        var arrayType = _typeInterner.GetArrayType(value.Type, count);

        // Create array literal with repeated value
        var arrayLiteral = new IrArrayLiteral(arrayType);
        for (int i = 0; i < count; i++)
        {
            arrayLiteral.Elements.Add(value);
        }

        return arrayLiteral;
    }

    public override object? VisitAdditiveExpr([NotNull] NovusParser.AdditiveExprContext context)
    {
        var left = Visit(context.expression(0)) as IrValue;
        var right = Visit(context.expression(1)) as IrValue;

        // If either operand had an error, bail out
        if (left == null || right == null)
        {
            return null;
        }

        var op = context.GetChild(1).GetText() == "+" ? IrBinaryOp.OpKind.Add : IrBinaryOp.OpKind.Sub;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, op, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitShiftExpr([NotNull] NovusParser.ShiftExprContext context)
    {
        var left = Visit(context.expression(0)) as IrValue;
        var right = Visit(context.expression(1)) as IrValue;

        // If either operand had an error, bail out
        if (left == null || right == null)
        {
            return null;
        }

        // Determine which shift operator is used
        var opKind = context.LSHIFT() != null ? IrBinaryOp.OpKind.Shl : IrBinaryOp.OpKind.Shr;

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, opKind, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitBitwiseAndExpr([NotNull] NovusParser.BitwiseAndExprContext context)
    {
        var left = Visit(context.expression(0)) as IrValue;
        var right = Visit(context.expression(1)) as IrValue;

        // If either operand had an error, bail out
        if (left == null || right == null)
        {
            return null;
        }

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.And, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitBitwiseXorExpr([NotNull] NovusParser.BitwiseXorExprContext context)
    {
        var left = Visit(context.expression(0)) as IrValue;
        var right = Visit(context.expression(1)) as IrValue;

        // If either operand had an error, bail out
        if (left == null || right == null)
        {
            return null;
        }

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Xor, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitBitwiseOrExpr([NotNull] NovusParser.BitwiseOrExprContext context)
    {
        var left = Visit(context.expression(0)) as IrValue;
        var right = Visit(context.expression(1)) as IrValue;

        // If either operand had an error, bail out
        if (left == null || right == null)
        {
            return null;
        }

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Or, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitCastExpr([NotNull] NovusParser.CastExprContext context)
    {
        var targetType = ParseType(context.type());
        var value = Visit(context.expression()) as IrValue;

        // If child expression had an error, bail out
        if (value == null)
        {
            return null;
        }

        // If it's already a constant, just change its type
        if (value is IrConstant constant)
        {
            return new IrConstant(constant.Value, targetType);
        }

        // Create an explicit cast value
        // This preserves the cast operation for the code generator
        // Supports nested casts: (T1)(T2)expr becomes IrCastValue(IrCastValue(expr, T2), T1)
        return new IrCastValue(value, value.Type, targetType);
    }

    public override object? VisitAsCastExpr([NotNull] NovusParser.AsCastExprContext context)
    {
        var value = Visit(context.expression()) as IrValue;
        var targetType = ParseType(context.type());

        // If child expression had an error, bail out
        if (value == null)
        {
            return null;
        }

        // If it's already a constant, just change its type
        if (value is IrConstant constant)
        {
            return new IrConstant(constant.Value, targetType);
        }

        // Create an explicit cast value
        // Same semantics as C-style cast, just different syntax
        return new IrCastValue(value, value.Type, targetType);
    }

    public override object? VisitMultiplicativeExpr([NotNull] NovusParser.MultiplicativeExprContext context)
    {
        var left = Visit(context.expression(0)) as IrValue;
        var right = Visit(context.expression(1)) as IrValue;

        // If either operand had an error, bail out
        if (left == null || right == null)
        {
            return null;
        }

        var opText = context.GetChild(1).GetText();
        var op = opText switch
        {
            "*" => IrBinaryOp.OpKind.Mul,
            "/" => IrBinaryOp.OpKind.Div,
            "%" => IrBinaryOp.OpKind.Mod,
            _ => IrBinaryOp.OpKind.Add  // ERROR: $"Unknown operator: {opText}"
        };

        var tempName = $"%t{_tempCounter++}";
        var binOp = new IrBinaryOp(tempName, op, left, right, left.Type);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, left.Type);
    }

    public override object? VisitComparisonExpr([NotNull] NovusParser.ComparisonExprContext context)
    {
        var left = Visit(context.expression(0)) as IrValue;
        var right = Visit(context.expression(1)) as IrValue;

        // If either operand had an error, bail out
        if (left == null || right == null)
        {
            return null;
        }

        var opText = context.GetChild(1).GetText();
        var op = opText switch
        {
            "==" => IrBinaryOp.OpKind.Eq,
            "!=" => IrBinaryOp.OpKind.Ne,
            "<" => IrBinaryOp.OpKind.Lt,
            "<=" => IrBinaryOp.OpKind.Le,
            ">" => IrBinaryOp.OpKind.Gt,
            ">=" => IrBinaryOp.OpKind.Ge,
            _ => IrBinaryOp.OpKind.Add  // ERROR: $"Unknown comparison operator: {opText}"
        };

        var tempName = $"%t{_tempCounter++}";
        // Comparison result is a boolean
        var binOp = new IrBinaryOp(tempName, op, left, right, IrBoolType.Instance);
        _currentBlock!.AddInstruction(binOp);

        return new IrVariable(tempName, IrBoolType.Instance);
    }

    public override object? VisitUnaryExpr([NotNull] NovusParser.UnaryExprContext context)
    {
        SourceLocation errorLocation;
        var op = context.GetChild(0).GetText();

        // Handle dereference specially - we need to determine the type first
        if (op == "*")
        {
            var operand = Visit(context.expression()) as IrValue;

            // If operand had an error, bail out
            if (operand == null)
            {
                return null;
            }

            // Determine the pointee type
            IrType pointeeType;
            if (operand.Type is IrPointerType ptrType)
            {
                pointeeType = ptrType.PointeeType;
            }
            else if (operand.Type is IrReferenceType refType)
            {
                pointeeType = refType.PointeeType;
            }
            else if (operand.Type is IrMutReferenceType mutRefType)
            {
                pointeeType = mutRefType.PointeeType;
            }
            else
            {
                errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.CannotDereferenceType,
                    $"Cannot dereference non-pointer/reference type: {operand.Type.Name}",
                    errorLocation
                );
                return null;
            }

            // Create a dereference value
            return new IrDereferenceValue(operand, pointeeType);
        }

        // For other unary ops, visit the operand first
        var operandValue = Visit(context.expression()) as IrValue;

        // If operand had an error, bail out
        if (operandValue == null)
        {
            return null;
        }

        if (op == "!")
        {
            // Logical NOT: false becomes true, true becomes false
            // For pointers, first convert to bool (ptr != 0), then negate
            IrValue boolOperand = operandValue;

            if (operandValue.Type is IrPointerType or IrReferenceType or IrMutReferenceType)
            {
                // Convert pointer to bool: ptr != 0
                var ptrToBoolTemp = $"%t{_tempCounter++}";
                var zeroValue = new IrConstant(0, IrIntType.U32);
                var comparison = new IrBinaryOp(ptrToBoolTemp, IrBinaryOp.OpKind.Ne, operandValue, zeroValue, IrBoolType.Instance);
                _currentBlock!.AddInstruction(comparison);
                boolOperand = new IrVariable(ptrToBoolTemp, IrBoolType.Instance);
            }

            // Implemented as: result = (operand XOR 1)
            // This flips the boolean bit: 0 XOR 1 = 1, 1 XOR 1 = 0
            var tempName = $"%t{_tempCounter++}";
            var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Xor, boolOperand, new IrConstant(1, new IrIntType(32, false)), IrBoolType.Instance);
            _currentBlock!.AddInstruction(binOp);
            return new IrVariable(tempName, IrBoolType.Instance);
        }
        else if (op == "~")
        {
            // Bitwise NOT: XOR with -1 (all bits set)
            var tempName = $"%t{_tempCounter++}";
            var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Xor, operandValue, new IrConstant(-1, operandValue.Type), operandValue.Type);
            _currentBlock!.AddInstruction(binOp);
            return new IrVariable(tempName, operandValue.Type);
        }
        else if (op == "-")
        {
            // Unary minus: subtract from 0
            var tempName = $"%t{_tempCounter++}";
            var binOp = new IrBinaryOp(tempName, IrBinaryOp.OpKind.Sub, new IrConstant(0, operandValue.Type), operandValue, operandValue.Type);
            _currentBlock!.AddInstruction(binOp);
            return new IrVariable(tempName, operandValue.Type);
        }

        errorLocation = GetLocation(context);
        _diagnostics.ReportError(
            ErrorCodes.UnknownOperator,
            $"Unknown unary operator: {op}",
            errorLocation
        );
        return null;
    }

    public override object? VisitDereferenceExpr([NotNull] NovusParser.DereferenceExprContext context)
    {
        // Handle pointer/reference dereference: *ptr
        var operand = (IrValue)Visit(context.expression())!;

        // Determine the pointee type
        IrType pointeeType;
        if (operand.Type is IrPointerType ptrType)
        {
            pointeeType = ptrType.PointeeType;
        }
        else if (operand.Type is IrReferenceType refType)
        {
            pointeeType = refType.PointeeType;
        }
        else if (operand.Type is IrMutReferenceType mutRefType)
        {
            pointeeType = mutRefType.PointeeType;
        }
        else
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.CannotDereferenceType,
                $"Cannot dereference non-pointer/reference type: {operand.Type.Name}",
                errorLocation
            );
            return null;
        }

        // Create a dereference value
        return new IrDereferenceValue(operand, pointeeType);
    }

    public override object? VisitPostIncrementExpr([NotNull] NovusParser.PostIncrementExprContext context)
    {
        // Post-increment: return old value, but increment the lvalue
        return HandlePostIncrementDecrement(context.expression(), isIncrement: true);
    }

    public override object? VisitPostDecrementExpr([NotNull] NovusParser.PostDecrementExprContext context)
    {
        // Post-decrement: return old value, but decrement the lvalue
        return HandlePostIncrementDecrement(context.expression(), isIncrement: false);
    }

    public override object? VisitTryExpr([NotNull] NovusParser.TryExprContext context)
    {
        // The ? operator for Result propagation with automatic error conversion via From trait
        // expr? desugars to:
        // match expr {
        //     Ok(val) => val,
        //     Err(err) => return Err(TargetError::convert(err))  // Auto-convert if needed
        // }

        var innerExpr = Visit(context.expression()) as IrValue;
        if (innerExpr == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                "? operator requires an expression that returns a value",
                errorLocation
            );
            return null;
        }

        // 1. Verify innerExpr type is Result<T, E>
        if (innerExpr.Type is not IrEnumType resultType || resultType.EnumName != "Result")
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                $"? operator requires a Result<T, E> type, got {innerExpr.Type}",
                errorLocation
            );
            return null;
        }

        // Extract T and E types from Result<T, E>
        // Result has two variants: Ok(T) and Err(E)
        var okVariant = resultType.Variants.FirstOrDefault(v => v.Name == "Ok");
        if (okVariant == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidType,
                "Result type missing Ok variant",
                errorLocation
            );
            return null;
        }

        var errVariant = resultType.Variants.FirstOrDefault(v => v.Name == "Err");
        if (errVariant == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidType,
                "Result type missing Err variant",
                errorLocation
            );
            return null;
        }

        if (okVariant.AssociatedData.Count == 0)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Result::Ok variant missing associated data",
                errorLocation
            );
            return null;
        }
        if (errVariant.AssociatedData.Count == 0)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Result::Err variant missing associated data",
                errorLocation
            );
            return null;
        }

        var okPayloadType = okVariant.AssociatedData[0];
        var sourceErrorType = errVariant.AssociatedData[0];

        // 2. Get current function's return type Result<T2, E2>
        if (_currentFunction == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                "? operator can only be used inside a function",
                errorLocation
            );
            return null;
        }

        // Check if function returns Result<T, E> or i32 (for main-style functions)
        bool isResultReturn = _currentFunction.ReturnType is IrEnumType funcResultType && funcResultType.EnumName == "Result";
        bool isI32Return = _currentFunction.ReturnType is IrIntType retIntType && retIntType.BitWidth == 32 && retIntType.IsSigned;

        if (!isResultReturn && !isI32Return)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.TryOperatorInvalidContext,
                $"? operator requires function to return Result<T, E> or i32, got {_currentFunction.ReturnType}",
                errorLocation
            );
            return null;
        }

        IrType? targetErrorType = null;
        IrEnumVariant? funcErrVariant = null;

        if (isResultReturn)
        {
            var resultReturnType = (IrEnumType)_currentFunction.ReturnType;
            funcErrVariant = resultReturnType.Variants.FirstOrDefault(v => v.Name == "Err");
            if (funcErrVariant == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.TryOperatorInvalidType,
                    "Function return type Result missing Err variant",
                    errorLocation
                );
                return null;
            }

            if (funcErrVariant.AssociatedData.Count == 0)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    "Function return type Result::Err missing associated data",
                    errorLocation
                );
                return null;
            }

            targetErrorType = funcErrVariant.AssociatedData[0];
        }

        // 3. Generate match expression to unwrap Result
        // This is similar to VisitMatchExpr, but we generate the match structure in IR directly

        // Create temporary variable to hold the result expression
        var resultTemp = $"%try_result_{_tempCounter++}";
        var resultLocal = new IrLocalVariable(resultTemp, innerExpr.Type, false);
        _currentFunction.LocalVariables.Add(resultLocal);
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, innerExpr.Type, false, innerExpr));
        var resultVar = new IrVariable(resultTemp, innerExpr.Type);

        // Create a variable to hold the unwrapped Ok value (declared before branching)
        var okValueTemp = $"%try_ok_val_{_tempCounter++}";
        var okValueLocal = new IrLocalVariable(okValueTemp, okPayloadType, true);
        _currentFunction.LocalVariables.Add(okValueLocal);
        _localVariables[okValueTemp] = okValueLocal;

        // Initialize with a default value (will be overwritten in Ok branch)
        IrValue defaultValue = okPayloadType is IrIntType intType
            ? new IrConstant(0, intType)
            : okPayloadType is IrBoolType
                ? new IrBoolConstant(false)
                : new IrConstant(0, okPayloadType);
        _currentBlock.AddInstruction(new IrLocalDecl(okValueTemp, okPayloadType, true, defaultValue));

        // Create blocks for Ok and Err branches
        var okBlock = _currentFunction.CreateBasicBlock($"try_ok_{_tempCounter}");
        var errBlock = _currentFunction.CreateBasicBlock($"try_err_{_tempCounter}");
        var continueBlock = _currentFunction.CreateBasicBlock($"try_continue_{_tempCounter}");

        // Test which variant we have
        var tagTemp = $"%try_tag_{_tempCounter++}";
        _currentBlock.AddInstruction(new IrExtractTag(tagTemp, resultVar));
        var tagVar = new IrVariable(tagTemp, IrIntType.I32);

        // Branch on tag: compare with Ok tag
        var okTagValue = new IrConstant(okVariant.Tag, IrIntType.I32);
        var isOkTemp = $"%try_isok_{_tempCounter++}";
        _currentBlock.AddInstruction(new IrBinaryOp(
            isOkTemp,
            IrBinaryOp.OpKind.Eq,
            tagVar,
            okTagValue,
            IrBoolType.Instance
        ));
        _currentBlock.AddInstruction(new IrConditionalBranch(
            new IrVariable(isOkTemp, IrBoolType.Instance),
            okBlock.Label,
            errBlock.Label
        ));

        // Ok branch: extract value, store it, and continue
        _currentBlock = okBlock;
        okBlock.AddInstruction(new IrLabel(okBlock.Label));
        var extractedTemp = $"%try_extracted_{_tempCounter++}";
        okBlock.AddInstruction(new IrExtractVariantData(extractedTemp, resultVar, "Ok", 0, okPayloadType));
        okBlock.AddInstruction(new IrStore(okValueTemp, new IrVariable(extractedTemp, okPayloadType)));
        okBlock.AddInstruction(new IrBranch(continueBlock.Label));

        // Err branch: extract error and handle based on function return type
        _currentBlock = errBlock;
        errBlock.AddInstruction(new IrLabel(errBlock.Label));
        var errValueTemp = $"%try_err_val_{_tempCounter++}";
        errBlock.AddInstruction(new IrExtractVariantData(errValueTemp, resultVar, "Err", 0, sourceErrorType));
        var errVar = new IrVariable(errValueTemp, sourceErrorType);

        if (isI32Return)
        {
            // For i32-returning functions (like main), print error and return 1
            // Generate: __novus_try_failed("ErrorType", tag, "Variant1,Variant2,..."); return 1;
            var errorTypeName = GetTypeName(sourceErrorType);

            // Extract the tag to get the variant name at runtime
            var errTagTemp = $"%try_err_tag_{_tempCounter++}";
            errBlock.AddInstruction(new IrExtractTag(errTagTemp, errVar));

            // Create string literals for type name and variant names
            var typeNameLabel = $"_str{_stringCounter++}";
            var typeNameLiteral = new IrStringLiteral(errorTypeName, typeNameLabel);
            StringLiterals.Add(typeNameLiteral);

            string variantNames;
            if (sourceErrorType is IrEnumType sourceEnumType)
            {
                variantNames = string.Join(",", sourceEnumType.Variants.Select(v => v.Name));
            }
            else
            {
                variantNames = "Unknown";
            }
            var variantNamesLabel = $"_str{_stringCounter++}";
            var variantNamesLiteral = new IrStringLiteral(variantNames, variantNamesLabel);
            StringLiterals.Add(variantNamesLiteral);

            // Call runtime helper: __novus_try_failed(error_type_name, tag, variant_names)
            var callTemp = $"%try_print_{_tempCounter++}";
            var printCall = new IrCall("__novus_try_failed", IrVoidType.Instance, callTemp);
            printCall.Arguments.Add(typeNameLiteral);
            printCall.Arguments.Add(new IrVariable(errTagTemp, IrIntType.I32));
            printCall.Arguments.Add(variantNamesLiteral);

            errBlock.AddInstruction(printCall);
            // Use RETURN_FAIL (20) - the standard DOS error code for failures
            errBlock.AddInstruction(new IrReturn(new IrConstant(20, IrIntType.I32)));
        }
        else
        {
            // For Result-returning functions, convert error if needed and return Err
            IrValue finalError;
            if (!TypesEqual(sourceErrorType, targetErrorType!))
            {
                // Need to convert error via From<E>::convert()
                var sourceTypeName = GetTypeName(sourceErrorType);
                var targetTypeName = GetTypeName(targetErrorType!);

                var convertMethodName = _module.FindGenericTraitMethod(targetTypeName, "From", sourceTypeName, "convert");

                if (convertMethodName == null)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Cannot convert {sourceTypeName} to {targetTypeName}: no From<{sourceTypeName}> implementation found for {targetTypeName}",
                        errorLocation
                    );
                    return null;
                }

                var convertedTemp = $"%try_converted_{_tempCounter++}";
                var convertCall = new IrCall(convertMethodName, targetErrorType!, convertedTemp);
                convertCall.Location = GetLocation(context);
                convertCall.Arguments.Add(errVar);
                errBlock.AddInstruction(convertCall);
                finalError = new IrVariable(convertedTemp, targetErrorType!);
            }
            else
            {
                finalError = errVar;
            }

            // Construct Result::Err(finalError) and return it
            var resultReturnType = (IrEnumType)_currentFunction.ReturnType;
            var returnErrTemp = $"%try_return_err_{_tempCounter++}";
            var returnErrLocal = new IrLocalVariable(returnErrTemp, resultReturnType, false);
            _currentFunction.LocalVariables.Add(returnErrLocal);
            var funcErrTag = funcErrVariant!.Tag;
            var returnErrValue = new IrEnumValue(resultReturnType, "Err", funcErrTag, new List<IrValue> { finalError });
            errBlock.AddInstruction(new IrLocalDecl(returnErrTemp, resultReturnType, false, returnErrValue));
            errBlock.AddInstruction(new IrReturn(new IrVariable(returnErrTemp, resultReturnType)));
        }

        // Continue block: the value from Ok is the result of this expression
        _currentBlock = continueBlock;
        continueBlock.AddInstruction(new IrLabel(continueBlock.Label));
        return new IrVariable(okValueTemp, okPayloadType);
    }

    private bool TypesEqual(IrType a, IrType b)
    {
        // Simple type equality check - uses Name property for comparison
        return a.Name == b.Name;
    }

    private string GetTypeName(IrType type)
    {
        return type switch
        {
            IrEnumType enumType => enumType.Name,
            IrStructType structType => structType.Name,
            IrIntType intType => intType.IsSigned ? $"i{intType.BitWidth}" : $"u{intType.BitWidth}",
            IrBoolType => "bool",
            IrPointerType ptrType => $"*{GetTypeName(ptrType.PointeeType)}",
            _ => type.ToString() ?? "unknown"
        };
    }

    /// <summary>
    /// Attempts to convert a value to a target type using the From<SourceType> trait.
    /// Returns the converted value if successful, or null if no conversion is available.
    /// This enables automatic error conversion in Result::Err and similar contexts.
    /// </summary>
    private IrValue? TryConvertViaFromTrait(IrValue sourceValue, IrType targetType)
    {
        var sourceType = sourceValue.Type;
        var sourceTypeName = GetTypeName(sourceType);
        var targetTypeName = GetTypeName(targetType);

        // Look for From<sourceType> trait implementation for targetType
        var convertMethodName = _module.FindTraitMethod(targetTypeName, "convert");

        if (convertMethodName == null)
        {
            // No From trait implementation found
            return null;
        }

        // Generate IR to call the convert method
        // Pattern from ? operator: call From<SourceType>::convert(sourceValue)
        var convertedTemp = $"%from_converted_{_tempCounter++}";
        var convertCall = new IrCall(convertMethodName, targetType, convertedTemp);
        // Note: No Location set - this is compiler-generated conversion code
        convertCall.Arguments.Add(sourceValue);

        // Add the call instruction to current block
        if (_currentBlock != null)
        {
            _currentBlock.AddInstruction(convertCall);
        }

        return new IrVariable(convertedTemp, targetType);
    }

    private IrValue HandlePostIncrementDecrement(ParserRuleContext exprContext, bool isIncrement)
    {
        // For post-inc/dec, we need to:
        // 1. Load current value and save it
        // 2. Increment/decrement the lvalue
        // 3. Return the saved old value

        // Unwrap parentheses if present (e.g., (*p)++ should work)
        exprContext = UnwrapParentheses(exprContext);

        // First, use the same logic as pre-inc/dec to compute and store the new value
        // But we need to save the old value first

        // Load the current value
        var currentValue = (IrValue)Visit(exprContext)!;

        // Save the old value to return later
        var oldValueTemp = $"%post{(isIncrement ? "inc" : "dec")}_save{_tempCounter++}";
        var oldValueLocal = new IrLocalVariable(oldValueTemp, currentValue.Type, false);
        _currentFunction!.LocalVariables.Add(oldValueLocal);
        _currentBlock!.AddInstruction(new IrStore(oldValueTemp, currentValue));

        // Compute the new value (current +/- 1)
        var newValueTemp = $"%t{_tempCounter++}";
        var op = isIncrement
            ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type)
            : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type);
        _currentBlock.AddInstruction(op);

        var newValue = new IrVariable(newValueTemp, currentValue.Type);

        // Now store the new value back to the lvalue (same logic as pre-inc/dec)
        StoreToLvalue(exprContext, newValue);

        // Return the old value
        return new IrVariable(oldValueTemp, currentValue.Type);
    }

    private void StoreToLvalue(ParserRuleContext exprContext, IrValue value)
    {
        SourceLocation errorLocation;

        // Case 0: Parenthesized expression - unwrap and recurse
        if (exprContext is NovusParser.ParenExprContext parenCtx)
        {
            StoreToLvalue(parenCtx.expression(), value);
            return;
        }

        // Case 1: Simple variable (identifier)
        if (exprContext is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            var varName = identCtx.identifier().GetText();
            _currentBlock!.AddInstruction(new IrStore(varName, value));
            return;
        }

        // Case 2: Member access (obj.field)
        if (exprContext is NovusParser.MemberAccessExprContext memberCtx)
        {
            var baseExpr = (IrValue)Visit(memberCtx.expression())!;
            var memberName = memberCtx.IDENTIFIER().GetText();

            // Auto-dereference pointers and references to structs (like in VisitMemberAccessExpr)
            IrValue actualBase = baseExpr;
            IrType baseType = baseExpr.Type;

            if (baseType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
            {
                // Auto-dereference the pointer - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, ptrType.PointeeType);
                baseType = ptrType.PointeeType;
            }
            else if (baseType is IrReferenceType refType && refType.PointeeType is IrStructType)
            {
                // Auto-dereference the reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, refType.PointeeType);
                baseType = refType.PointeeType;
            }
            else if (baseType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
            {
                // Auto-dereference the mutable reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, mutRefType.PointeeType);
                baseType = mutRefType.PointeeType;
            }

            if (baseType is not IrStructType structType)
            {
                errorLocation = GetLocation(exprContext);
                _diagnostics.ReportError(
                    ErrorCodes.CannotAccessMember,
                    $"Cannot access member '{memberName}' on non-struct type '{baseType}'",
                    errorLocation
                );
                return;
            }

            var field = structType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field == null)
            {
                errorLocation = GetLocation(exprContext);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Struct '{structType.Name}' has no field '{memberName}'",
                    errorLocation
                );
                return;
            }

            _currentBlock!.AddInstruction(new IrMemberStore(actualBase, memberName, field.Offset, value));
            return;
        }

        // Case 3: Index access (arr[i])
        if (exprContext is NovusParser.IndexExprContext indexCtx)
        {
            var arrayExpr = (IrValue)Visit(indexCtx.expression(0))!;
            var indexExpr = (IrValue)Visit(indexCtx.expression(1))!;

            _currentBlock!.AddInstruction(new IrIndexStore(arrayExpr, indexExpr, value));
            return;
        }

        // Case 4: Dereference (*ptr)
        if (exprContext is NovusParser.DereferenceExprContext derefCtx)
        {
            var ptrExpr = (IrValue)Visit(derefCtx.expression())!;
            _currentBlock!.AddInstruction(new IrDereferenceStore(ptrExpr, value));
            return;
        }

        // Case 5: If we have a PrimaryExpr that we haven't handled yet, it might be wrapping something
        // This can happen with certain parse tree structures
        if (exprContext is NovusParser.PrimaryExprContext unhandledPrimaryCtx)
        {
            var child = unhandledPrimaryCtx.GetChild(0);

            // Try dereference
            if (child is NovusParser.DereferenceExprContext derefChild)
            {
                var ptrExpr = (IrValue)Visit(derefChild.expression())!;
                _currentBlock!.AddInstruction(new IrDereferenceStore(ptrExpr, value));
                return;
            }
        }

        errorLocation = GetLocation(exprContext);
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Cannot store to expression type: {exprContext.GetType().Name}",
            errorLocation
        );
        return;
    }

    public override object? VisitPreIncrementExpr([NotNull] NovusParser.PreIncrementExprContext context)
    {
        // Pre-increment: increment the lvalue and return new value
        return HandlePreIncrementDecrement(context.expression(), isIncrement: true);
    }

    public override object? VisitPreDecrementExpr([NotNull] NovusParser.PreDecrementExprContext context)
    {
        // Pre-decrement: decrement the lvalue and return new value
        return HandlePreIncrementDecrement(context.expression(), isIncrement: false);
    }

    private ParserRuleContext UnwrapParentheses(ParserRuleContext exprContext)
    {
        // Recursively unwrap parenthesized expressions: (((*p))) -> *p
        // Parse tree structure:
        // PrimaryExprContext (expression) -> ParenExprContext (primaryExpression) -> inner expression

        bool changed = true;
        while (changed)
        {
            changed = false;

            // If it's a ParenExpr, unwrap it
            if (exprContext is NovusParser.ParenExprContext parenCtx)
            {
                exprContext = parenCtx.expression();
                changed = true;
            }
            // If it's a PrimaryExpr wrapping a ParenExpr, unwrap the ParenExpr
            else if (exprContext is NovusParser.PrimaryExprContext primaryCtx)
            {
                // The first child of PrimaryExpr is the primaryExpression
                // Check if it's a ParenExpr
                if (primaryCtx.GetChild(0) is NovusParser.ParenExprContext parenChild)
                {
                    // Get the expression inside the parentheses
                    exprContext = parenChild.expression();
                    changed = true;
                }
            }
        }

        return exprContext;
    }

    private IrValue HandlePreIncrementDecrement(ParserRuleContext exprContext, bool isIncrement)
    {
        SourceLocation errorLocation;

        // Unwrap parentheses if present (e.g., ++(*p) should work)
        exprContext = UnwrapParentheses(exprContext);

        // Case 1: Simple variable (identifier)
        if (exprContext is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            var varName = identCtx.identifier().GetText();
            var currentValue = (IrValue)Visit(exprContext)!;

            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, currentValue.Type), currentValue.Type);
            _currentBlock!.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, currentValue.Type);
            _currentBlock.AddInstruction(new IrStore(varName, newValue));
            return newValue;
        }

        // Case 2: Member access (obj.field)
        if (exprContext is NovusParser.MemberAccessExprContext memberCtx)
        {
            var baseExpr = (IrValue)Visit(memberCtx.expression())!;
            var memberName = memberCtx.IDENTIFIER().GetText();

            // Auto-dereference pointers and references to structs (like in VisitMemberAccessExpr)
            IrValue actualBase = baseExpr;
            IrType baseType = baseExpr.Type;

            if (baseType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
            {
                // Auto-dereference the pointer - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, ptrType.PointeeType);
                baseType = ptrType.PointeeType;
            }
            else if (baseType is IrReferenceType refType && refType.PointeeType is IrStructType)
            {
                // Auto-dereference the reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, refType.PointeeType);
                baseType = refType.PointeeType;
            }
            else if (baseType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
            {
                // Auto-dereference the mutable reference - wrap in IrDereferenceValue
                actualBase = new IrDereferenceValue(actualBase, mutRefType.PointeeType);
                baseType = mutRefType.PointeeType;
            }

            // Get the struct type and field info
            if (baseType is not IrStructType structType)
            {
                errorLocation = GetLocation(exprContext);
                _diagnostics.ReportError(
                    ErrorCodes.CannotAccessMember,
                    $"Cannot access member '{memberName}' on non-struct type '{baseType}'",
                    errorLocation
                );
                // Return error recovery value
                return new IrConstant(0, IrIntType.I32);
            }

            var field = structType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field == null)
            {
                errorLocation = GetLocation(exprContext);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Struct '{structType.Name}' has no field '{memberName}'",
                    errorLocation
                );
                // Return error recovery value
                return new IrConstant(0, IrIntType.I32);
            }

            // Load current value
            var loadTemp = $"%member_load_{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrMemberAccess(loadTemp, actualBase, memberName, field.Type, field.Offset));
            var currentValue = new IrVariable(loadTemp, field.Type);

            // Increment/decrement
            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, field.Type), field.Type)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, field.Type), field.Type);
            _currentBlock.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, field.Type);
            _currentBlock.AddInstruction(new IrMemberStore(actualBase, memberName, field.Offset, newValue));
            return newValue;
        }

        // Case 3: Index access (arr[i])
        if (exprContext is NovusParser.IndexExprContext indexCtx)
        {
            var arrayExpr = (IrValue)Visit(indexCtx.expression(0))!;
            var indexExpr = (IrValue)Visit(indexCtx.expression(1))!;

            // Determine element type
            IrType elementType;
            if (arrayExpr.Type is IrPointerType pt)
                elementType = pt.PointeeType;
            else if (arrayExpr.Type is IrArrayType at)
                elementType = at.ElementType;
            else
            {
                errorLocation = GetLocation(exprContext);
                _diagnostics.ReportError(
                    ErrorCodes.CannotIndexType,
                    $"Cannot index type '{arrayExpr.Type}'",
                    errorLocation
                );
                // Return error recovery value
                return new IrConstant(0, IrIntType.I32);
            }

            // Load current value
            var loadTemp = $"%index_load_{_tempCounter++}";
            _currentBlock!.AddInstruction(new IrIndexAccess(loadTemp, arrayExpr, indexExpr, elementType));
            var currentValue = new IrVariable(loadTemp, elementType);

            // Increment/decrement
            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, elementType), elementType)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, elementType), elementType);
            _currentBlock.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, elementType);
            _currentBlock.AddInstruction(new IrIndexStore(arrayExpr, indexExpr, newValue));
            return newValue;
        }

        // Case 4: Dereference (*ptr or *ref)
        if (exprContext is NovusParser.DereferenceExprContext derefCtx)
        {
            var ptrExpr = (IrValue)Visit(derefCtx.expression())!;

            // Determine pointee type (handle pointers and references)
            IrType pointeeType;
            if (ptrExpr.Type is IrPointerType ptrType)
            {
                pointeeType = ptrType.PointeeType;
            }
            else if (ptrExpr.Type is IrReferenceType refType)
            {
                pointeeType = refType.PointeeType;
            }
            else if (ptrExpr.Type is IrMutReferenceType mutRefType)
            {
                pointeeType = mutRefType.PointeeType;
            }
            else
            {
                errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.CannotDereferenceType,
                    $"Cannot dereference non-pointer/reference type '{ptrExpr.Type}'",
                    errorLocation
                );
                // Return error recovery value
                return new IrConstant(0, IrIntType.I32);
            }

            // Load current value (dereference)
            var currentValue = new IrDereferenceValue(ptrExpr, pointeeType);

            // Increment/decrement
            var newValueTemp = $"%t{_tempCounter++}";
            var op = isIncrement
                ? new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Add, currentValue, new IrConstant(1, pointeeType), pointeeType)
                : new IrBinaryOp(newValueTemp, IrBinaryOp.OpKind.Sub, currentValue, new IrConstant(1, pointeeType), pointeeType);
            _currentBlock!.AddInstruction(op);

            var newValue = new IrVariable(newValueTemp, pointeeType);
            _currentBlock.AddInstruction(new IrDereferenceStore(ptrExpr, newValue));
            return newValue;
        }

        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Pre-{(isIncrement ? "increment" : "decrement")} not supported for expression type: {exprContext.GetType().Name}",
            errorLocation
        );
        // Return error recovery value
        return new IrConstant(0, IrIntType.I32);
    }

    public override object? VisitLogicalAndExpr([NotNull] NovusParser.LogicalAndExprContext context)
    {
        // Short-circuit evaluation: if left is false, don't evaluate right
        var left = Visit(context.expression(0)) as IrValue;

        // If left operand had an error, bail out
        if (left == null)
        {
            return null;
        }

        var evalRightLabel = $"and_right_{_labelCounter}";
        var setTrueLabel = $"and_true_{_labelCounter}";
        var setFalseLabel = $"and_false_{_labelCounter}";
        var endLabel = $"and_end_{_labelCounter}";
        _labelCounter++;

        var resultTemp = $"%t{_tempCounter++}";

        // Add to function's local variables for stack alerrorLocation
        var localVar = new IrLocalVariable(resultTemp, IrIntType.I32, false);
        _currentFunction!.LocalVariables.Add(localVar);

        // If left is false, short-circuit to false result
        _currentBlock!.AddInstruction(new IrConditionalBranch(left, evalRightLabel, setFalseLabel));

        // Evaluate right
        _currentBlock!.AddInstruction(new IrLabel(evalRightLabel));
        var right = Visit(context.expression(1)) as IrValue;

        // If right operand had an error, bail out
        if (right == null)
        {
            return null;
        }

        // If right is true, set result to 1, otherwise 0
        _currentBlock!.AddInstruction(new IrConditionalBranch(right, setTrueLabel, setFalseLabel));

        // Both are true, set result to 1
        _currentBlock!.AddInstruction(new IrLabel(setTrueLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // Set result to 0 (false)
        _currentBlock!.AddInstruction(new IrLabel(setFalseLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        return new IrVariable(resultTemp, IrIntType.I32);
    }

    public override object? VisitLogicalOrExpr([NotNull] NovusParser.LogicalOrExprContext context)
    {
        // Short-circuit evaluation: if left is true, don't evaluate right
        var left = Visit(context.expression(0)) as IrValue;

        // If left operand had an error, bail out
        if (left == null)
        {
            return null;
        }

        var evalRightLabel = $"or_right_{_labelCounter}";
        var setTrueLabel = $"or_true_{_labelCounter}";
        var setFalseLabel = $"or_false_{_labelCounter}";
        var endLabel = $"or_end_{_labelCounter}";
        _labelCounter++;

        var resultTemp = $"%t{_tempCounter++}";

        // Add to function's local variables for stack alerrorLocation
        var localVar = new IrLocalVariable(resultTemp, IrIntType.I32, false);
        _currentFunction!.LocalVariables.Add(localVar);

        // If left is true, short-circuit to true result
        _currentBlock!.AddInstruction(new IrConditionalBranch(left, setTrueLabel, evalRightLabel));

        // Evaluate right
        _currentBlock!.AddInstruction(new IrLabel(evalRightLabel));
        var right = Visit(context.expression(1)) as IrValue;

        // If right operand had an error, bail out
        if (right == null)
        {
            return null;
        }

        // If right is true, set result to 1, otherwise 0
        _currentBlock!.AddInstruction(new IrConditionalBranch(right, setTrueLabel, setFalseLabel));

        // Set result to true
        _currentBlock!.AddInstruction(new IrLabel(setTrueLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(1, IrIntType.I32)));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // Set result to 0 (false)
        _currentBlock!.AddInstruction(new IrLabel(setFalseLabel));
        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, IrIntType.I32, false, new IrConstant(0, IrIntType.I32)));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        return new IrVariable(resultTemp, IrIntType.I32);
    }

    public override object? VisitIfExpr([NotNull] NovusParser.IfExprContext context)
    {
        // If expression: if condition { trueBlock } else { falseBlock }
        // or: if condition { trueBlock } else if ...
        var condition = Visit(context.expression()) as IrValue;

        // If condition had an error, bail out
        if (condition == null)
        {
            return null;
        }

        var trueLabel = $"if_true_{_labelCounter}";
        var falseLabel = $"if_false_{_labelCounter}";
        var endLabel = $"if_end_{_labelCounter}";
        _labelCounter++;

        var resultTemp = $"%t{_tempCounter++}";

        // Automatic pointer-to-bool coercion: if ptr { ... } else { ... }
        // Convert pointer to bool by comparing with null (ptr != 0)
        if (condition.Type is IrPointerType or IrReferenceType or IrMutReferenceType)
        {
            var ptrToBoolTemp = $"%t{_tempCounter++}";
            var zeroValue = new IrConstant(0, IrIntType.U32);
            var comparison = new IrBinaryOp(ptrToBoolTemp, IrBinaryOp.OpKind.Ne, condition, zeroValue, IrBoolType.Instance);
            _currentBlock!.AddInstruction(comparison);
            condition = new IrVariable(ptrToBoolTemp, IrBoolType.Instance);
        }

        // Branch based on condition
        _currentBlock!.AddInstruction(new IrConditionalBranch(condition, trueLabel, falseLabel));

        // True branch - evaluate the block and get its value
        _currentBlock!.AddInstruction(new IrLabel(trueLabel));
        var trueValue = VisitBlockAsExpression(context.block(0));
        if (trueValue == null)
        {
            return null;
        }
        var resultType = trueValue.Type; // Get type from the true branch value

        // Add to function's local variables for stack allocation
        var localVar = new IrLocalVariable(resultTemp, resultType, false);
        _currentFunction!.LocalVariables.Add(localVar);

        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, trueValue));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // False branch - set expected type for bidirectional type checking
        _currentBlock!.AddInstruction(new IrLabel(falseLabel));
        var savedExpectedType = _expectedType;
        _expectedType = resultType; // Use the type from the true branch

        IrValue? falseValue;
        if (context.ifElseChain() != null)
        {
            // else if chain - recurse
            falseValue = Visit(context.ifElseChain()) as IrValue;
        }
        else
        {
            // Final else block
            falseValue = VisitBlockAsExpression(context.block(1));
        }

        _expectedType = savedExpectedType; // Restore previous expected type

        if (falseValue == null)
        {
            return null;
        }

        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, falseValue));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        return new IrVariable(resultTemp, resultType);
    }

    public override object? VisitIfElseChain([NotNull] NovusParser.IfElseChainContext context)
    {
        // else if chain: if condition { trueBlock } else { falseBlock }
        // This is essentially the same as IfExpr
        var condition = Visit(context.expression()) as IrValue;

        // If condition had an error, bail out
        if (condition == null)
        {
            return null;
        }

        var trueLabel = $"elif_true_{_labelCounter}";
        var falseLabel = $"elif_false_{_labelCounter}";
        var endLabel = $"elif_end_{_labelCounter}";
        _labelCounter++;

        var resultTemp = $"%t{_tempCounter++}";

        // Automatic pointer-to-bool coercion
        if (condition.Type is IrPointerType or IrReferenceType or IrMutReferenceType)
        {
            var ptrToBoolTemp = $"%t{_tempCounter++}";
            var zeroValue = new IrConstant(0, IrIntType.U32);
            var comparison = new IrBinaryOp(ptrToBoolTemp, IrBinaryOp.OpKind.Ne, condition, zeroValue, IrBoolType.Instance);
            _currentBlock!.AddInstruction(comparison);
            condition = new IrVariable(ptrToBoolTemp, IrBoolType.Instance);
        }

        // Branch based on condition
        _currentBlock!.AddInstruction(new IrConditionalBranch(condition, trueLabel, falseLabel));

        // True branch
        _currentBlock!.AddInstruction(new IrLabel(trueLabel));
        var trueValue = VisitBlockAsExpression(context.block(0));
        if (trueValue == null)
        {
            return null;
        }
        var resultType = trueValue.Type;

        // Add to function's local variables for stack allocation
        var localVar = new IrLocalVariable(resultTemp, resultType, false);
        _currentFunction!.LocalVariables.Add(localVar);

        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, trueValue));
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // False branch
        _currentBlock!.AddInstruction(new IrLabel(falseLabel));
        var savedExpectedType = _expectedType;
        _expectedType = resultType;

        IrValue? falseValue;
        if (context.ifElseChain() != null)
        {
            falseValue = Visit(context.ifElseChain()) as IrValue;
        }
        else
        {
            falseValue = VisitBlockAsExpression(context.block(1));
        }

        _expectedType = savedExpectedType;

        if (falseValue == null)
        {
            return null;
        }

        _currentBlock!.AddInstruction(new IrLocalDecl(resultTemp, resultType, false, falseValue));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));

        return new IrVariable(resultTemp, resultType);
    }

    /// <summary>
    /// Visit a block as an expression and return the value of the last expression.
    /// If the block ends with an expression without semicolon, that's the value.
    /// Otherwise, the block returns void/unit.
    /// </summary>
    private IrValue? VisitBlockAsExpression(NovusParser.BlockContext block)
    {
        // For if-expressions, the block should contain statements and end with an expression
        // The last statement (if it's an expression statement without semicolon) is the value

        // Visit all statements in the block
        var statements = block.statement();
        if (statements == null || statements.Length == 0)
        {
            // Empty block - return unit/void (use i32 0 as a placeholder)
            return new IrConstant(0, IrIntType.I32);
        }

        // Visit all statements except the last
        for (int i = 0; i < statements.Length - 1; i++)
        {
            Visit(statements[i]);
        }

        // The last statement should be an expression statement whose value is the block's value
        var lastStmt = statements[statements.Length - 1];

        // Check if it's an expression statement (no semicolon means it's the return value)
        if (lastStmt.expressionStatement() != null)
        {
            var exprStmt = lastStmt.expressionStatement();
            // In Novus, expression statements that return a value don't have a trailing semicolon
            // For now, we'll just evaluate the expression and return its value
            var value = Visit(exprStmt.expression()) as IrValue;
            return value;
        }
        else if (lastStmt.returnStatement() != null)
        {
            // A return statement in an if-expression - evaluate and return the value
            var retStmt = lastStmt.returnStatement();
            if (retStmt.expression() != null)
            {
                return Visit(retStmt.expression()) as IrValue;
            }
            return new IrConstant(0, IrIntType.I32);
        }
        else
        {
            // Not an expression statement - visit it and return a default value
            Visit(lastStmt);
            return new IrConstant(0, IrIntType.I32);
        }
    }

    public override object? VisitFloatLiteral([NotNull] NovusParser.FloatLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.FLOAT_LITERAL().GetText();
        var (value, type) = ParseFloatLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        // If expected type is fixed-point and we have an unsuffixed float literal,
        // convert to fixed-point. This allows: var pos: fixed16 = 100.0
        if (_expectedType is IrFixedType expectedFixed && type is IrFloatType)
        {
            return new IrFixedConstant(value, expectedFixed);
        }

        // Return appropriate constant type based on whether it's float or fixed
        if (type is IrFloatType floatType)
        {
            return new IrFloatConstant(value, floatType);
        }
        else if (type is IrFixedType fixedType)
        {
            return new IrFixedConstant(value, fixedType);
        }
        else
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unexpected float literal type: {type.Name}",
                errorLocation
            );
            return null;
        }
    }

    public override object? VisitIntegerLiteral([NotNull] NovusParser.IntegerLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.INTEGER_LITERAL().GetText();
        var (value, type) = ParseIntegerLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        return new IrConstant(value, type);
    }

    public override object? VisitBinaryLiteral([NotNull] NovusParser.BinaryLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.BINARY_LITERAL().GetText();
        var (value, type) = ParseBinaryLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        return new IrConstant(value, type);
    }

    public override object? VisitHexLiteral([NotNull] NovusParser.HexLiteralContext context)
    {
        var isNegative = context.GetText().StartsWith("-");
        var text = context.HEX_LITERAL().GetText();
        var (value, type) = ParseHexLiteral(text);

        if (isNegative)
        {
            value = -value;
        }

        return new IrConstant(value, type);
    }

    public override object? VisitBoolLiteral([NotNull] NovusParser.BoolLiteralContext context)
    {
        var text = context.GetText();
        var value = text == "true";
        return new IrBoolConstant(value);
    }

    public override object? VisitNullLiteral([NotNull] NovusParser.NullLiteralContext context)
    {
        // null keyword is syntactic sugar for integer literal 0
        // It will be emitted as bare "0" in C, allowing implicit conversion to any pointer type
        var (value, type) = ParseIntegerLiteral("0");
        return new IrConstant(value, type);
    }

    public override object? VisitStringLiteral([NotNull] NovusParser.StringLiteralContext context)
    {
        var text = context.STRING_LITERAL().GetText();
        // Remove quotes
        var stringValue = text[1..^1];

        // Check if string contains interpolation (unescaped curly braces)
        // If it does, handle it as an interpolated string
        bool hasInterpolation = false;
        for (int i = 0; i < stringValue.Length; i++)
        {
            if (stringValue[i] == '\\')
            {
                // Skip escaped character
                i++;
                continue;
            }
            if (stringValue[i] == '{')
            {
                hasInterpolation = true;
                break;
            }
        }

        if (hasInterpolation)
        {
            // Handle as interpolated string - strip quotes and process
            var content = text.Substring(1, text.Length - 2);
            return HandleInterpolatedStringImpl(content, context);
        }

        // Process escape sequences
        stringValue = ProcessEscapeSequences(stringValue);

        // Create unique label for this string (still null-terminated in data section for C FFI)
        var label = $"_str{_stringCounter++}";
        var stringLiteral = new IrStringLiteral(stringValue, label);
        StringLiterals.Add(stringLiteral);

        // String literals now create Str struct instances: Str { ptr: *u8, len: u32 }
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            // When Str type is not available, fall back to *u8 (C-style string pointer)
            // This allows string literals to work in modules that can't import std::strings
            return stringLiteral;  // Return just the *u8 pointer
        }

        // Create struct literal with ptr and len fields
        var fieldValues = new Dictionary<string, IrValue>
        {
            ["ptr"] = stringLiteral,  // IrStringLiteral is still *u8 pointer to data
            ["len"] = new IrConstant(stringValue.Length, IrIntType.U32)  // Length without null terminator
        };

        // If expected type is *u8, return just the pointer (automatic coercion)
        // This allows string literals to work in contexts like Option::Some("string") where Option<*u8> is expected
        if (_expectedType is IrPointerType expectedPtrType &&
            expectedPtrType.PointeeType.Equals(IrIntType.U8))
        {
            return stringLiteral;  // Return just the *u8 pointer, not the full Str struct
        }

        return new IrStructLiteral(strType, fieldValues);
    }

    public override object? VisitInterpolatedStringLiteral([NotNull] NovusParser.InterpolatedStringLiteralContext context)
    {
        // Get the f-string text and parse it into segments
        var fstring = context.GetChild(0)?.GetText();
        if (fstring == null)
        {
            return null;
        }

        // Strip prefix and suffix based on whether it's f"..." or just "..."
        var content = fstring.StartsWith("f\"")
            ? fstring.Substring(2, fstring.Length - 3)  // f"..." -> ...
            : fstring.Substring(1, fstring.Length - 2);  // "..." -> ...

        return HandleInterpolatedStringImpl(content, context);
    }

    private object? HandleInterpolatedStringImpl(string content, ParserRuleContext context)
    {
        // Auto-import required modules for f-string support
        // F-strings need:
        // - std::strings::display for Display trait implementations on primitive types
        // - std::strings::format for StackFormatter type (stack-allocated, zero heap allocation!)
        // - std::strings::core for Str type
        bool isStdLibraryModule = _inputFilePath != null && _inputFilePath.Contains(System.IO.Path.DirectorySeparatorChar + "std" + System.IO.Path.DirectorySeparatorChar);

        if (!isStdLibraryModule)
        {
            // Always import these modules to ensure all necessary types and methods are available
            // The ImportModule function handles already-processed modules correctly
            ImportModule("std::strings::display", importAll: true);
            ImportModule("std::strings::format", importAll: true);
            ImportModule("std::strings::core", importAll: true);
        }

        // Parse the string into string and expression segments
        var segments = ParseInterpolatedString(content);

        // Look up the StackFormatter type (stack-allocated, no heap allocation!)
        var formatterType = _symbols.LookupStruct("StackFormatter");
        if (formatterType == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Interpolated strings require StackFormatter type from std::fmt module",
                errorLocation
            );
            return null;
        }

        // Create a temporary variable for the StackFormatter
        var formatterVarName = $"_formatter{_tempCounter++}";

        // Call StackFormatter::new() to create the formatter
        // This returns StackFormatter directly (no Option wrapping - always succeeds!)
        var formatterNewMethodName = "StackFormatter::new";
        var formatterNewMethod = _module.GetFunction(formatterNewMethodName);
        if (formatterNewMethod == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "StackFormatter::new() method not found. Ensure std::fmt is imported.",
                errorLocation
            );
            return null;
        }

        // Call StackFormatter::new() - returns StackFormatter directly
        var formatterNewResultName = $"%t{_tempCounter++}";
        var formatterNewCall = new IrCall(formatterNewMethodName, formatterNewMethod.ReturnType, formatterNewResultName);
        formatterNewCall.Location = GetLocation(context);
        _currentBlock!.AddInstruction(formatterNewCall);
        var formatterNewResult = new IrVariable(formatterNewResultName, formatterNewMethod.ReturnType);

        // Store formatter in a local variable (mutable)
        var formatterVar = new IrLocalVariable(formatterVarName, formatterType, true);
        _currentFunction!.LocalVariables.Add(formatterVar);
        _localVariables[formatterVarName] = formatterVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(formatterVarName, formatterType, true, formatterNewResult));

        // Process each segment
        foreach (var segment in segments)
        {
            if (segment.IsStringSegment)
            {
                // Call f.write_str("segment")
                EmitWriteStrLiteral(formatterVarName, formatterType, segment.StringContent);
            }
            else
            {
                // Parse the expression and call expr.fmt(&mut f)
                EmitFormatExpression(formatterVarName, formatterType, segment.Expression);
            }
        }

        // Call f.as_str() to get a Str (pointer into the stack buffer)
        var formatterVarRef = new IrVariable(formatterVarName, formatterType);
        var asStrMethodName = _module.FindTraitMethod("StackFormatter", "as_str");
        if (asStrMethodName == null)
        {
            asStrMethodName = "StackFormatter::as_str";
        }

        var asStrMethod = _module.GetFunction(asStrMethodName);
        if (asStrMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "StackFormatter::as_str() method not found",
                errorLocation
            );
            return null;
        }

        // Call as_str() - takes &self (borrow)
        var asStrResultName = $"%t{_tempCounter++}";
        var asStrCall = new IrCall(asStrMethodName, asStrMethod.ReturnType, asStrResultName);
        asStrCall.Location = GetLocation(context);
        // as_str takes &self, so we need to borrow the formatter
        var formatterBorrow = new IrBorrowValue(formatterVarRef, asStrMethod.Parameters[0].Type, false);
        asStrCall.Arguments.Add(formatterBorrow);
        _currentBlock!.AddInstruction(asStrCall);

        // Return the Str result (no heap allocation, just a pointer into the stack buffer!)
        return new IrVariable(asStrResultName, asStrMethod.ReturnType);
    }

    private void EmitWriteStrLiteral(string formatterVarName, IrType formatterType, string stringContent)
    {
        // Create a string literal for the segment
        var label = $"_str{_stringCounter++}";
        var stringLiteral = new IrStringLiteral(stringContent, label);
        StringLiterals.Add(stringLiteral);

        // Create Str struct
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var strFieldValues = new Dictionary<string, IrValue>
        {
            ["ptr"] = stringLiteral,
            ["len"] = new IrConstant(stringContent.Length, IrIntType.U32)
        };
        var strValue = new IrStructLiteral(strType, strFieldValues);

        // Call the other EmitWriteStr method with the Str value
        EmitWriteStr(formatterVarName, formatterType, strValue);
    }

    private void EmitFormatExpression(string formatterVarName, IrType formatterType, string expressionText)
    {
        // Parse the expression text into an AST node
        var inputStream = new AntlrInputStream(expressionText);
        var lexer = new NovusLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new NovusParser(tokens);

        // Parse as an expression
        var exprContext = parser.expression();

        // Visit the expression to generate IR
        var exprValue = Visit(exprContext) as IrValue;
        if (exprValue == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Failed to evaluate expression in f-string: {expressionText}",
                errorLocation
            );
            return;
        }

        // Get the type of the expression
        var exprType = exprValue.Type;

        // Handle different types appropriately
        if (exprType is IrStructType st && st.StructName == "Str")
        {
            // For Str types, just write the string directly
            EmitWriteStr(formatterVarName, formatterType, exprValue);
        }
        else if (exprType is IrStructType strSt && strSt.StructName == "String")
        {
            // For String types, call String::as_str() and write the result
            // This bypasses the Display trait which expects Formatter, not StackFormatter
            EmitFormatString(formatterVarName, formatterType, exprValue);
        }
        else if (exprType is IrIntType intType)
        {
            // For integer types, convert to string using built-in functions
            EmitFormatInteger(formatterVarName, formatterType, exprValue, intType);
        }
        else if (exprType is IrBoolType)
        {
            // For bool, write "true" or "false"
            EmitFormatBool(formatterVarName, formatterType, exprValue);
        }
        else
        {
            // For other types, try to find Display::fmt() implementation
            var typeName = exprType switch
            {
                IrStructType structType => structType.StructName,
                IrEnumType enumType => enumType.EnumName,
                _ => exprType.Name
            };
            var fmtMethodName = _module.FindTraitMethod(typeName, "fmt");
            if (fmtMethodName == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Type '{typeName}' does not implement Display trait. All types in f-strings must implement Display.",
                    errorLocation
                );
                return;
            }

            var fmtMethod = _module.GetFunction(fmtMethodName);
            if (fmtMethod == null)
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.MethodNotFound,
                    $"Display::fmt() method not found for type '{typeName}'",
                    errorLocation
                );
                return;
            }

            // Call expr.fmt(&mut formatter)
            // First parameter is &self (the expression value)
            var exprBorrow = new IrBorrowValue(exprValue, fmtMethod.Parameters[0].Type, false);

            // Second parameter is &mut Formatter
            var formatterVarRef = new IrVariable(formatterVarName, formatterType);
            var formatterBorrow = new IrBorrowValue(formatterVarRef, fmtMethod.Parameters[1].Type, true);

            var fmtResultName = $"%t{_tempCounter++}";
            var fmtCall = new IrCall(fmtMethodName, fmtMethod.ReturnType, fmtResultName);
            fmtCall.Location = GetLocation(exprContext);
            fmtCall.Arguments.Add(exprBorrow);
            fmtCall.Arguments.Add(formatterBorrow);
            _currentBlock!.AddInstruction(fmtCall);
        }
    }

    private void EmitWriteStr(string formatterVarName, IrType formatterType, IrValue strValue)
    {
        // Determine the formatter type name (StackFormatter or Formatter)
        string formatterTypeName = "Formatter";
        if (formatterType is IrStructType structType)
        {
            formatterTypeName = structType.StructName;
        }

        // Find write_str method for the specific formatter type
        var writeStrMethodName = $"{formatterTypeName}::write_str";

        var writeStrMethod = _module.GetFunction(writeStrMethodName);
        if (writeStrMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                $"{writeStrMethodName}() method not found",
                errorLocation
            );
            return;
        }

        // Call f.write_str(str_value)
        var formatterVarRef = new IrVariable(formatterVarName, formatterType);
        var formatterBorrow = new IrBorrowValue(formatterVarRef, writeStrMethod.Parameters[0].Type, true);
        var writeStrResultName = $"%t{_tempCounter++}";
        var writeStrCall = new IrCall(writeStrMethodName, writeStrMethod.ReturnType, writeStrResultName);
        writeStrCall.Arguments.Add(formatterBorrow);
        writeStrCall.Arguments.Add(strValue);
        _currentBlock!.AddInstruction(writeStrCall);
    }

    private void EmitFormatInteger(string formatterVarName, IrType formatterType, IrValue intValue, IrIntType intType)
    {
        // Bypass Display trait and directly call runtime i{N}_to_string functions
        // This avoids pulling in ALL Display implementations and reduces code size dramatically

        // Determine the runtime function name based on integer type
        string? runtimeFuncName = intType.Name switch
        {
            "i8" => "i8_to_string",
            "i16" => "i16_to_string",
            "i32" => "i32_to_string",
            "i64" => "i64_to_string",
            "u8" => "u8_to_string",
            "u16" => "u16_to_string",
            "u32" => "u32_to_string",
            "u64" => "u64_to_string",
            _ => null
        };

        if (runtimeFuncName == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Integer type '{intType.Name}' not supported in f-strings",
                errorLocation
            );
            return;
        }

        // Ensure the runtime function is declared as external
        if (!_module.Functions.Any(f => f.Name == runtimeFuncName))
        {
            var runtimeFunc = new IrFunction(
                runtimeFuncName,
                IrIntType.U32,
                Visibility.Private,
                isExtern: true
            );
            runtimeFunc.Parameters.Add(new IrParameter("value", intType));
            runtimeFunc.Parameters.Add(new IrParameter("buffer", new IrPointerType(IrIntType.U8)));
            runtimeFunc.Parameters.Add(new IrParameter("buffer_size", IrIntType.U32));
            _module.Functions.Add(runtimeFunc);
        }

        // Allocate a small stack buffer for the string (16 bytes is enough for any 64-bit integer)
        const int bufferSize = 16;
        var bufferVarName = $"_intbuf{_tempCounter++}";
        var bufferType = new IrArrayType(IrIntType.U8, bufferSize);
        var bufferVar = new IrLocalVariable(bufferVarName, bufferType, true);
        _currentFunction!.LocalVariables.Add(bufferVar);
        _localVariables[bufferVarName] = bufferVar;

        // Initialize buffer to zeros
        var zeroArray = new IrArrayLiteral(bufferType);
        for (int i = 0; i < bufferSize; i++)
        {
            zeroArray.Elements.Add(new IrConstant(0, IrIntType.U8));
        }
        _currentBlock!.AddInstruction(new IrLocalDecl(bufferVarName, bufferType, true, zeroArray));

        // Get pointer to buffer: (*u8)&buffer
        var bufferVarRef = new IrVariable(bufferVarName, bufferType);
        var refType = new IrReferenceType(bufferType);
        var borrowExpr = new IrBorrowValue(bufferVarRef, refType, true); // Mutable borrow since function writes to buffer
        var pointerType = new IrPointerType(IrIntType.U8);
        var bufferPtr = new IrCastValue(borrowExpr, refType, pointerType);

        // Call runtime function: len = i16_to_string(value, buffer_ptr, 16)
        var lenResultName = $"%t{_tempCounter++}";
        var toStringCall = new IrCall(runtimeFuncName, IrIntType.U32, lenResultName);
        toStringCall.Arguments.Add(intValue);
        toStringCall.Arguments.Add(bufferPtr);
        toStringCall.Arguments.Add(new IrConstant(bufferSize, IrIntType.U32));
        _currentBlock!.AddInstruction(toStringCall);

        // Create Str from buffer pointer and returned length
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var strValue = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = bufferPtr,
            ["len"] = new IrVariable(lenResultName, IrIntType.U32)
        });

        // Write the Str to the formatter
        EmitWriteStr(formatterVarName, formatterType, strValue);
    }

    private void EmitFormatBool(string formatterVarName, IrType formatterType, IrValue boolValue)
    {
        // Create string literals for "true" and "false"
        var trueLabel = $"_str{_stringCounter++}";
        var trueLiteral = new IrStringLiteral("true", trueLabel);
        StringLiterals.Add(trueLiteral);

        var falseLabel = $"_str{_stringCounter++}";
        var falseLiteral = new IrStringLiteral("false", falseLabel);
        StringLiterals.Add(falseLiteral);

        // Create Str structs
        var strType = _symbols.LookupStruct("Str");
        if (strType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.TypeNotFound,
                "Str type not found",
                errorLocation
            );
            return;
        }

        var trueStr = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = trueLiteral,
            ["len"] = new IrConstant(4, IrIntType.U32)
        });

        var falseStr = new IrStructLiteral(strType, new Dictionary<string, IrValue>
        {
            ["ptr"] = falseLiteral,
            ["len"] = new IrConstant(5, IrIntType.U32)
        });

        // Use conditional to select the right string
        var trueLabel2 = $"_true{_labelCounter++}";
        var falseLabel2 = $"_false{_labelCounter++}";
        var endLabel = $"_end{_labelCounter++}";

        _currentBlock!.AddInstruction(new IrConditionalBranch(boolValue, trueLabel2, falseLabel2));

        // True branch
        _currentBlock!.AddInstruction(new IrLabel(trueLabel2));
        EmitWriteStr(formatterVarName, formatterType, trueStr);
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // False branch
        _currentBlock!.AddInstruction(new IrLabel(falseLabel2));
        EmitWriteStr(formatterVarName, formatterType, falseStr);
        _currentBlock!.AddInstruction(new IrBranch(endLabel));

        // End
        _currentBlock!.AddInstruction(new IrLabel(endLabel));
    }

    private void EmitFormatString(string formatterVarName, IrType formatterType, IrValue stringValue)
    {
        // Call String::as_str() to get a Str, then write it to the formatter
        // This bypasses the Display trait which expects Formatter, not StackFormatter

        var asStrMethodName = "String::as_str";
        var asStrMethod = _module.GetFunction(asStrMethodName);
        if (asStrMethod == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                "String::as_str() method not found",
                errorLocation
            );
            return;
        }

        // Borrow the String value
        var stringBorrow = new IrBorrowValue(stringValue, asStrMethod.Parameters[0].Type, false);

        // Call String::as_str(&self)
        var asStrResultName = $"%t{_tempCounter++}";
        var asStrCall = new IrCall(asStrMethodName, asStrMethod.ReturnType, asStrResultName);
        asStrCall.Arguments.Add(stringBorrow);
        _currentBlock!.AddInstruction(asStrCall);

        // Get the Str result and write it to the formatter
        var strValue = new IrVariable(asStrResultName, asStrMethod.ReturnType);
        EmitWriteStr(formatterVarName, formatterType, strValue);

        // Drop the temporary String after we've extracted and copied its data
        // This prevents memory leaks when functions like move_to() return String values
        // that get used in f-strings and then need to be cleaned up.
        EmitDropForTemporaryString(stringValue);
    }

    /// <summary>
    /// Emit a Drop call for a temporary String value used in f-strings.
    /// This is necessary because String contains Vec&lt;u8&gt; which owns heap memory.
    ///
    /// NOTE: We call Vec_u8_drop directly on the String's vec field rather than
    /// String_Drop_drop because String intentionally doesn't implement Drop.
    /// This is due to a compiler bug where defer cleanup runs before return
    /// value is copied out, causing double-free when functions return String.
    /// </summary>
    private void EmitDropForTemporaryString(IrValue stringValue)
    {
        // Get the String type and find the 'vec' field
        var stringType = stringValue.Type as IrStructType;
        if (stringType == null || stringType.StructName != "String")
        {
            return;
        }

        var vecField = stringType.Fields.FirstOrDefault(f => f.Name == "vec");
        if (vecField == null)
        {
            return;
        }

        // Get the Vec<u8> type from the field
        var vecType = vecField.Type as IrStructType;
        if (vecType == null)
        {
            return;
        }

        // Ensure the Drop method for Vec<u8> is instantiated
        // This may trigger generic instantiation if not already done
        EnsureDropMethodInstantiated(vecType);

        // Now look for the drop method - try trait impl name first (Vec<u8>_Drop_drop),
        // then regular method name (Vec_u8_drop)
        var typeName = vecType.CacheKey ?? vecType.StructName;
        var dropMethodName = $"{typeName}_Drop_drop";
        var dropMethod = _module.GetFunction(dropMethodName);

        if (dropMethod == null)
        {
            // Try the regular method name (mangled: Vec<u8> -> Vec_u8)
            dropMethodName = $"{typeName.Replace("<", "_").Replace(">", "_").TrimEnd('_')}_drop";
            dropMethod = _module.GetFunction(dropMethodName);
        }

        if (dropMethod == null)
        {
            // Vec doesn't have drop instantiated - this shouldn't happen but handle gracefully
            return;
        }

        // Use IrMemberAccess to load the 'vec' field into a temp variable
        var vecTempName = $"%t{_tempCounter++}";
        var memberAccess = new IrMemberAccess(vecTempName, stringValue, "vec", vecField.Type, vecField.Offset);
        _currentBlock!.AddInstruction(memberAccess);

        // Create a variable reference to the loaded vec field
        var vecVarRef = new IrVariable(vecTempName, vecField.Type);

        // Create a mutable borrow of the vec for calling drop(&mut self)
        var vecMutBorrow = new IrBorrowValue(vecVarRef, new IrMutReferenceType(vecField.Type), isMutable: true);

        // Call Vec_u8_drop(&mut string.vec)
        var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
        dropCall.Arguments.Add(vecMutBorrow);
        _currentBlock!.AddInstruction(dropCall);
    }

    private List<InterpolationSegment> ParseInterpolatedString(string content)
    {
        var segments = new List<InterpolationSegment>();
        var i = 0;
        var currentString = new System.Text.StringBuilder();

        while (i < content.Length)
        {
            if (content[i] == '\\' && i + 1 < content.Length)
            {
                // Handle escape sequences
                var nextChar = content[i + 1];
                if (nextChar == '{' || nextChar == '}')
                {
                    // Escaped brace: \{ -> {, \} -> }
                    currentString.Append(nextChar);
                    i += 2;
                }
                else if (nextChar == 'x' && i + 3 < content.Length)
                {
                    // Hex escape: \xNN
                    var hexDigits = content.Substring(i + 2, 2);
                    var byteValue = Convert.ToByte(hexDigits, 16);
                    currentString.Append((char)byteValue);
                    i += 4;
                }
                else
                {
                    // Standard escape sequences
                    var escapeChar = nextChar switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        'b' => '\b',
                        'f' => '\f',
                        '"' => '"',
                        '\'' => '\'',
                        '\\' => '\\',
                        '0' => '\0',
                        _ => nextChar  // Unknown escape sequence - just use the character as-is
                    };
                    currentString.Append(escapeChar);
                    i += 2;
                }
            }
            else if (content[i] == '{')
            {
                // Start of interpolation
                // Save any accumulated string content
                if (currentString.Length > 0)
                {
                    segments.Add(new InterpolationSegment { IsStringSegment = true, StringContent = currentString.ToString() });
                    currentString.Clear();
                }

                // Find the matching closing brace
                var braceDepth = 1;
                var exprStart = i + 1;
                i++;

                while (i < content.Length && braceDepth > 0)
                {
                    if (content[i] == '{')
                    {
                        braceDepth++;
                    }
                    else if (content[i] == '}')
                    {
                        braceDepth--;
                    }

                    if (braceDepth > 0)
                    {
                        i++;
                    }
                }

                if (braceDepth != 0)
                {
                    var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        "Unmatched braces in f-string interpolation",
                        errorLocation
                    );
                    // Return empty list for error recovery
                    return new List<InterpolationSegment>();
                }

                // Extract the expression
                var expression = content.Substring(exprStart, i - exprStart);
                segments.Add(new InterpolationSegment { IsStringSegment = false, Expression = expression });
                i++; // Skip the closing brace
            }
            else
            {
                // Regular character
                currentString.Append(content[i]);
                i++;
            }
        }

        // Add any remaining string content
        if (currentString.Length > 0)
        {
            segments.Add(new InterpolationSegment { IsStringSegment = true, StringContent = currentString.ToString() });
        }

        return segments;
    }

    private class InterpolationSegment
    {
        public bool IsStringSegment { get; set; }
        public string StringContent { get; set; } = "";
        public string Expression { get; set; } = "";
    }

    public override object? VisitSizeofExpr([NotNull] NovusParser.SizeofExprContext context)
    {
        // @sizeof(Type) - compile-time intrinsic that returns size in bytes as u32
        var typeCtx = context.type();
        var targetType = ParseType(typeCtx);

        if (targetType == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"could not determine type for @sizeof",
                errorLocation
            );
            return null;
        }

        // Return a SizeOf IR value that will be emitted as C's sizeof() operator
        // This ensures we get the correct size including VBCC's automatic struct padding
        return new IrSizeOf(targetType, IrIntType.U32);
    }

    private string ProcessEscapeSequences(string input)
    {
        // First handle hex escapes (\xNN) before other replacements
        // This prevents issues with backslashes in the hex escape pattern
        var result = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\\x([0-9A-Fa-f]{2})",
            m => ((char)Convert.ToByte(m.Groups[1].Value, 16)).ToString()
        );

        // Then handle standard escape sequences
        return result
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\r", "\r")
            .Replace("\\b", "\b")
            .Replace("\\f", "\f")
            .Replace("\\\"", "\"")
            .Replace("\\'", "'")
            .Replace("\\\\", "\\");
    }

    public override object? VisitIdentifierExpr([NotNull] NovusParser.IdentifierExprContext context)
    {
        var name = context.identifier().GetText();

        // Check if this is a qualified enum constructor or associated function (e.g., Result::Ok, Vec::new)
        if (name.Contains("::"))
        {
            var parts = name.Split("::");
            if (parts.Length == 2)
            {
                var typeName = parts[0];
                var memberName = parts[1];

                // Try enum variant first
                if (_symbols.HasEnum(typeName))
                {
                    var enumType = _symbols.LookupEnum(typeName)!;
                    var variant = enumType.GetVariant(memberName);

                    if (variant != null)
                    {
                        // Use expected type if it's a more specific (concrete) version of this enum
                        var concreteEnumType = enumType;
                        if (_expectedType is IrEnumType expectedEnum &&
                            expectedEnum.EnumName == enumType.EnumName &&
                            expectedEnum.CacheKey != null)
                        {
                            // Use the concrete type from context (e.g., Option<MemoryBlock> instead of Option<T>)
                            concreteEnumType = expectedEnum;
                        }
                        else
                        {
                        }

                        // For unit variants (no associated data), create the enum value directly
                        if (variant.AssociatedData.Count == 0)
                        {
                            return new IrEnumValue(concreteEnumType, memberName, variant.Tag, new List<IrValue>());
                        }

                        // For variants with data, return a constructor for use in call expressions
                        return new IrEnumConstructor(concreteEnumType, memberName, variant.Tag);
                    }
                }

                // Try associated function (struct method without self parameter)
                var mangledName = name; // Already has :: format

                // Check if this is a generic type - look in generic method templates
                if (_symbols.HasStruct(typeName))
                {
                    var structType = _symbols.LookupStruct(typeName)!;

                    // If the struct is generic, check generic method templates
                    if (structType.GenericParameters.Count > 0)
                    {
                        var templateKey = mangledName;
                        if (_genericInstantiator.HasMethodTemplate(templateKey))
                        {
                            // Return a special marker for generic associated function
                            // This will be instantiated later when we know the concrete types
                            return new IrGenericAssociatedFunction(typeName, memberName, structType.GenericParameters);
                        }
                    }
                }

                // Try to find the function in the module
                var function = _module.GetFunction(mangledName);
                if (function != null)
                {
                    // Check if this is an associated function (no self parameter)
                    if (function.Parameters.Count == 0 || function.Parameters[0].Name != "self")
                    {
                        // Return a function reference that can be called
                        return new IrFunctionRef(function);
                    }
                }
            }
        }

        // Check if it's a constant - inline the value
        var constantSymbol = _symbols.LookupConstant(name);
        if (constantSymbol != null)
        {
            return new IrConstant((int)constantSymbol.Value, constantSymbol.Type);
        }
        else
        {
        }

        // Check if it's a static variable
        var staticVar = _module.StaticVariables.FirstOrDefault(sv => sv.Name == name);
        if (staticVar != null)
        {
            return new IrGlobalVariable(name, staticVar.Type);
        }

        // Check if it's an extern variable
        var externVar = _module.ExternalVariables.FirstOrDefault(ev => ev.Name == name);
        if (externVar != null)
        {
            return new IrGlobalVariable(name, externVar.Type);
        }

        // Check if it's a local variable
        if (_localVariables.ContainsKey(name))
        {
            var localVar = _localVariables[name];
            return new IrVariable(name, localVar.Type);
        }

        // Check if it's a parameter
        if (_currentFunction != null)
        {
            var param = _currentFunction.Parameters.FirstOrDefault(p => p.Name == name);
            if (param != null)
            {
                return new IrVariable(name, param.Type);
            }
        }

        // Check if it's a known system global variable (CPU, FPU, Chipset)
        // These are declared in system.novus as extern vars
        if (name == "CPU" && _symbols.HasEnum("SystemCPU"))
        {
            return new IrVariable(name, _symbols.LookupEnum("SystemCPU")!);
        }
        if (name == "FPU" && _symbols.HasEnum("SystemFPU"))
        {
            return new IrVariable(name, _symbols.LookupEnum("SystemFPU")!);
        }
        if (name == "Chipset" && _symbols.HasEnum("SystemChipset"))
        {
            return new IrVariable(name, _symbols.LookupEnum("SystemChipset")!);
        }

        // Check if it's a function name (for both calls and function pointers)
        var funcRef = _module.GetFunction(name);
        if (funcRef != null)
        {
            // Return a function reference that can be called or used as a function pointer
            return new IrFunctionRef(funcRef);
        }

        // Otherwise, assume it's a temporary variable or function name
        return new IrVariable(name, IrIntType.I32); // Default type for temps
    }

    public override object? VisitSelfExpr([NotNull] NovusParser.SelfExprContext context)
    {
        // Return the 'self' variable (parameter in method)
        if (_localVariables.ContainsKey("self"))
        {
            return new IrVariable("self", _localVariables["self"].Type);
        }
        else if (_currentFunction != null && _currentFunction.Parameters.Any(p => p.Name == "self"))
        {
            var selfParam = _currentFunction.Parameters.First(p => p.Name == "self");
            return new IrVariable("self", selfParam.Type);
        }

        var errorLocation = GetLocation(context);
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            "'self' can only be used inside methods",
            errorLocation
        );
        return null;
    }

    public override object? VisitParenExpr([NotNull] NovusParser.ParenExprContext context)
    {
        return Visit(context.expression());
    }

    public override object? VisitUnitLiteral([NotNull] NovusParser.UnitLiteralContext context)
    {
        // Unit type () - creates a zero-element tuple
        return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
    }

    public override object? VisitTupleLiteral([NotNull] NovusParser.TupleLiteralContext context)
    {
        var expressions = context.expression();
        var elements = new List<IrValue>();

        foreach (var exprCtx in expressions)
        {
            var value = Visit(exprCtx) as IrValue;
            if (value == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Invalid expression in tuple literal",
                    errorLocation
                );
                return null;
            }
            elements.Add(value);
        }

        // Get element types and create tuple type
        var elementTypes = elements.Select(e => e.Type).ToList();
        var tupleType = _typeInterner.GetTupleType(elementTypes);

        return new IrTupleLiteral(tupleType, elements);
    }

    public override object? VisitStructLiteral([NotNull] NovusParser.StructLiteralContext context)
    {
        var structName = context.typeName().GetText();

        if (!_symbols.HasStruct(structName))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unknown struct type '{structName}'",
                errorLocation
            );
            return null;
        }

        // Get the base struct type
        var baseStructType = _symbols.LookupStruct(structName);
        if (baseStructType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(ErrorCodes.StructNotFound, $"Struct '{structName}' not found", errorLocation);
            return null;
        }

        // Use expected type for bidirectional type checking if it's a monomorphized version of this struct
        IrStructType structType;
        if (_expectedType is IrStructType expectedStruct && expectedStruct.StructName == structName)
        {
            // Use the expected monomorphized type (e.g., Vec<i32>)
            structType = expectedStruct;
        }
        else
        {
            // Use the base generic type (e.g., Vec<T>)
            structType = baseStructType;
        }

        var fieldValues = new Dictionary<string, IrValue>();

        // Process field initializers
        foreach (var fieldInit in context.structFieldInit())
        {
            var fieldName = fieldInit.IDENTIFIER().GetText();
            var fieldValue = (IrValue?)Visit(fieldInit.expression());

            if (fieldValue == null)
            {
                var errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Field '{fieldName}' in struct '{structName}' requires a value",
                    errorLocation
                );
                return null;
            }

            // Apply automatic Str → u32 coercion for fields that expect u32
            // This allows passing string literals/variables to TagItem.ti_Data and similar fields
            var field = structType.GetField(fieldName);
            if (field != null &&
                field.Type is IrIntType intType && intType == IrIntType.U32 &&
                fieldValue.Type is IrStructType strStructType && strStructType.StructName == "Str")
            {
                // Extract the .ptr field from the Str struct
                IrValue ptrValue;
                if (fieldValue is IrStructLiteral strLiteral)
                {
                    // For string literals, extract the ptr field directly
                    if (!strLiteral.FieldValues.TryGetValue("ptr", out ptrValue!))
                    {
                        var errorLocation = GetLocation(context);
                        _diagnostics.ReportError(
                            ErrorCodes.InvalidExpressionType,
                            "Str struct literal must have a 'ptr' field for coercion to u32",
                            errorLocation
                        );
                        return null;
                    }
                }
                else
                {
                    // For Str variables, create a member access instruction to get .ptr
                    var ptrField = strStructType.GetField("ptr");
                    if (ptrField == null)
                    {
                        var errorLocation = GetLocation(context);
                        _diagnostics.ReportError(
                            ErrorCodes.InvalidExpressionType,
                            "Str struct must have a 'ptr' field for coercion to u32",
                            errorLocation
                        );
                        return null;
                    }

                    var ptrTempName = $"%t{_tempCounter++}";
                    var u8PtrType = _typeInterner.GetPointerType(IrIntType.U8);
                    var ptrFieldAccess = new IrMemberAccess(ptrTempName, fieldValue, "ptr", u8PtrType, ptrField.Offset);
                    _currentBlock!.AddInstruction(ptrFieldAccess);
                    ptrValue = new IrVariable(ptrTempName, u8PtrType);
                }

                // Cast the pointer (*u8) to u32
                var u8PtrTypeForCast = _typeInterner.GetPointerType(IrIntType.U8);
                fieldValue = new IrCastValue(ptrValue, u8PtrTypeForCast, IrIntType.U32);
            }

            fieldValues[fieldName] = fieldValue;
        }

        // If the base struct is generic and we don't have an expected type, infer the concrete type from field values
        if (baseStructType.GenericParameters.Count > 0 && _expectedType == null)
        {
            // Infer generic type parameters from field values
            var typeSubstitutions = new Dictionary<string, IrType>();

            foreach (var field in baseStructType.Fields)
            {
                if (fieldValues.TryGetValue(field.Name, out var fieldValue))
                {
                    // Extract generic type mappings from field type and value type
                    ExtractGenericTypeMapping(field.Type, fieldValue.Type, typeSubstitutions);
                }
            }

            // Check if all generic parameters were inferred
            if (typeSubstitutions.Count == baseStructType.GenericParameters.Count)
            {
                // Check if all type arguments are concrete (not generic)
                var typeArgs = baseStructType.GenericParameters.Select(p => typeSubstitutions[p]).ToList();
                bool allConcrete = typeArgs.All(t => !(t is IrGenericType));

                if (allConcrete)
                {
                    // All type arguments are concrete - create monomorphized struct type
                    var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
                    var cacheKey = $"{baseStructType.StructName}<{string.Join(",", typeArgKeys)}>";

                    // Check cache first
                    if (_symbols.LookupMonomorphizedStruct(cacheKey) != null)
                    {
                        structType = _symbols.LookupMonomorphizedStruct(cacheKey)!;
                    }
                    else
                    {
                        // Create monomorphized fields using recursive substitution
                        var monomorphizedFields = new List<IrStructField>();
                        bool fullyMonomorphized = true;

                        foreach (var origField in baseStructType.Fields)
                        {
                            var fieldType = _typeParser.SubstituteGenericTypes(origField.Type, typeSubstitutions);
                            monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));

                            // Check if field type is still generic
                            if (_typeParser.ContainsGenericTypes(fieldType))
                            {
                                fullyMonomorphized = false;
                            }
                        }

                        // Create new struct type with concrete types
                        structType = new IrStructType(baseStructType.StructName, monomorphizedFields, null, cacheKey);

                        // Force calculation of field offsets only if fully monomorphized
                        if (fullyMonomorphized)
                        {
                            _ = structType.SizeInBytes;
                        }

                        // Cache it for future use
                        _symbols.RegisterMonomorphizedStruct(cacheKey, structType);
                    }
                }
                // else: some type arguments are still generic, use base generic type
            }
        }

        // Validate that all fields are initialized
        foreach (var field in structType.Fields)
        {
            if (!fieldValues.ContainsKey(field.Name))
            {
                var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Field '{field.Name}' in struct '{structName}' is not initialized",
                    errorLocation
                );
                return null;
            }
        }

        return new IrStructLiteral(structType, fieldValues);
    }

    public override object? VisitStructArrayInit([NotNull] NovusParser.StructArrayInitContext context)
    {
        // Handle Vec { {10, 20, 30} } syntax
        // This is syntactic sugar for collections that can be initialized from an array literal

        var structName = context.typeName().GetText();

        if (!_symbols.HasStruct(structName))
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Unknown struct type '{structName}'",
                errorLocation
            );
            return null;
        }

        var baseStructType = _symbols.LookupStruct(structName);
        if (baseStructType == null)
        {
            var errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(ErrorCodes.StructNotFound, $"Struct '{structName}' not found", errorLocation);
            return null;
        }

        // Get the array literal expressions
        var arrayLiteralCtx = context.arrayLiteralInit();
        var elements = new List<IrValue>();
        IrType? elementType = null;

        foreach (var exprCtx in arrayLiteralCtx.expression())
        {
            var elem = (IrValue?)Visit(exprCtx);
            if (elem == null)
            {
                var errorLocation = GetLocation(exprCtx);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    "Invalid array element",
                    errorLocation
                );
                return null;
            }
            elements.Add(elem);
            if (elementType == null)
            {
                elementType = elem.Type;
            }
        }

        if (elements.Count == 0 || elementType == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Struct array initializer requires at least one element",
                errorLocation
            );
            return null;
        }

        // Create array type and literal
        var arrayType = new IrArrayType(elementType, elements.Count);
        var arrayLiteral = new IrArrayLiteral(arrayType);
        foreach (var elem in elements)
        {
            arrayLiteral.Elements.Add(elem);
        }

        // For now, only support this for Vec type
        if (structName != "Vec")
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Struct array initializer syntax is only supported for Vec, not '{structName}'",
                errorLocation
            );
            return null;
        }

        var arrayLength = elements.Count;

        // Create a static variable to hold the array data
        var staticVarName = $"_vec_data_{_staticVarCounter++}";
        var staticVar = new IrStaticVariable(staticVarName, arrayType, Visibility.Private, false, arrayLiteral);
        _module.StaticVariables.Add(staticVar);

        // Monomorphize Vec<T> to Vec<elementType> if it's generic
        IrStructType vecType;
        if (baseStructType.GenericParameters.Count > 0)
        {
            // Vec is generic (e.g., Vec<T> from std::collections)
            // Monomorphize to Vec<elementType>
            var typeArgs = new List<IrType> { elementType };
            var typeArgKeys = typeArgs.Select(t => GetTypeCacheKey(t));
            var cacheKey = $"{baseStructType.StructName}<{string.Join(",", typeArgKeys)}>";

            // Check cache first
            if (_symbols.LookupMonomorphizedStruct(cacheKey) != null)
            {
                vecType = _symbols.LookupMonomorphizedStruct(cacheKey)!;
            }
            else
            {
                // Create type substitution map
                var typeSubstitutions = new Dictionary<string, IrType>();
                for (int i = 0; i < baseStructType.GenericParameters.Count && i < typeArgs.Count; i++)
                {
                    typeSubstitutions[baseStructType.GenericParameters[i]] = typeArgs[i];
                }

                // Create monomorphized fields
                var monomorphizedFields = new List<IrStructField>();
                foreach (var origField in baseStructType.Fields)
                {
                    var fieldType = _typeParser.SubstituteGenericTypes(origField.Type, typeSubstitutions);
                    monomorphizedFields.Add(new IrStructField(origField.Name, fieldType));
                }

                // Create monomorphized struct type
                vecType = new IrStructType(baseStructType.StructName, monomorphizedFields, null, cacheKey);
                _ = vecType.SizeInBytes; // Force size calculation

                // Cache it
                _symbols.RegisterMonomorphizedStruct(cacheKey, vecType);
            }
        }
        else
        {
            // Vec is not generic (custom Vec with concrete types)
            vecType = baseStructType;
        }

        // Build field values: ptr/data = &static_array, len = array_length, capacity = array_length
        var fieldValues = new Dictionary<string, IrValue>();

        // Determine pointer field name (ptr for std::collections::Vec, data for custom Vec)
        var pointerFieldName = vecType.GetField("ptr") != null ? "ptr" : "data";

        // Pointer field: cast static var to pointer
        var staticVarRef = new IrVariable(staticVarName, arrayType);
        var refType = new IrReferenceType(arrayType);
        var borrowExpr = new IrBorrowValue(staticVarRef, refType, false);
        var pointerType = new IrPointerType(elementType);
        var dataPtr = new IrCastValue(borrowExpr, refType, pointerType);
        fieldValues[pointerFieldName] = dataPtr;

        // len and capacity fields
        // IMPORTANT: capacity must be 0 for static-backed Vecs so Vec_drop won't try to free the static data
        // Static const data cannot be freed on AmigaOS - it would cause a crash (error 81000005)
        fieldValues["len"] = new IrConstant(arrayLength, IrIntType.U32);
        fieldValues["capacity"] = new IrConstant(0, IrIntType.U32);

        return new IrStructLiteral(vecType, fieldValues);
    }

    public override object? VisitMemberAccessExpr([NotNull] NovusParser.MemberAccessExprContext context)
    {
        // Clear expected type when visiting base expression - the expected type is for the
        // whole member access result, not the base. This prevents e.g. "string".ptr from
        // returning *u8 when we need the Str struct to access .ptr on it.
        var savedExpectedType = _expectedType;
        _expectedType = null;
        var baseExpr = (IrValue?)Visit(context.expression());
        _expectedType = savedExpectedType;
        if (baseExpr == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                "Member access requires a base expression",
                errorLocation
            );
            return null;
        }

        var memberName = context.IDENTIFIER().GetText();

        // OPTIMIZATION: Detect array[index].field pattern to avoid dead intermediate struct copies
        // This is critical for 68k to prevent creating misaligned struct temporaries
        if (baseExpr is IrVariable indexedVar && _indexAccessTemps.TryGetValue(indexedVar.Name, out var indexInfo))
        {
            var (array, index, elementType) = indexInfo;
            if (elementType is IrStructType indexedStructType)
            {
                var indexedField = indexedStructType.GetField(memberName);
                if (indexedField == null)
                {
                    var errorLocation = GetLocation(context);
                    _diagnostics.ReportError(
                        ErrorCodes.InvalidExpressionType,
                        $"Struct '{indexedStructType.Name}' does not have a field named '{memberName}'",
                        errorLocation
                    );
                    return null;
                }

                // Remove the now-unnecessary IrIndexAccess instruction from the current block
                // This prevents the dead intermediate struct variable from being created
                var lastInst = _currentBlock!.Instructions.LastOrDefault();
                if (lastInst is IrIndexAccess indexAccess && indexAccess.ResultName == indexedVar.Name)
                {
                    _currentBlock.Instructions.Remove(indexAccess);
                }

                // IMPORTANT: Substitute generic types in field type if we're in a monomorphized context
                var indexedFieldType = indexedField.Type;
                if (_currentTypeSubstitutions != null)
                {
                    indexedFieldType = _typeParser.SubstituteGenericTypes(indexedFieldType, _currentTypeSubstitutions);
                }

                // Return IrIndexedFieldAccess value instead of creating IrMemberAccess instruction
                return new IrIndexedFieldAccess(array, index, memberName, indexedField.Offset, indexedFieldType);
            }
        }

        // Auto-dereference pointers and references to structs
        IrValue actualBase = baseExpr;
        IrType baseType = baseExpr.Type;

        if (baseType is IrPointerType ptrType && ptrType.PointeeType is IrStructType)
        {
            // Auto-dereference the pointer - wrap in IrDereferenceValue
            actualBase = new IrDereferenceValue(actualBase, ptrType.PointeeType);
            baseType = ptrType.PointeeType;
        }
        else if (baseType is IrReferenceType refType && refType.PointeeType is IrStructType)
        {
            // Auto-dereference the reference - wrap in IrDereferenceValue
            actualBase = new IrDereferenceValue(actualBase, refType.PointeeType);
            baseType = refType.PointeeType;
        }
        else if (baseType is IrMutReferenceType mutRefType && mutRefType.PointeeType is IrStructType)
        {
            // Auto-dereference the mutable reference - wrap in IrDereferenceValue
            actualBase = new IrDereferenceValue(actualBase, mutRefType.PointeeType);
            baseType = mutRefType.PointeeType;
        }

        // Check if the base expression is a struct type
        if (baseType is not IrStructType structType)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.CannotAccessMember,
                $"Cannot access member '{memberName}' on non-struct type '{baseType.Name}'",
                errorLocation
            );
            return null;
        }

        // Find the field
        var field = structType.GetField(memberName);
        if (field == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Struct '{structType.Name}' does not have a field named '{memberName}'",
                errorLocation
            );
            return null;
        }

        // Generate a member access instruction
        var resultName = $"%t{_tempCounter++}";

        // IMPORTANT: Substitute generic types in field type if we're in a monomorphized context
        // For example, when accessing `self.entries[index].value` in `HashMap<u32,u32>::insert`,
        // the field type `V` should be substituted with `u32`
        var fieldType = field.Type;
        if (_currentTypeSubstitutions != null)
        {
            fieldType = _typeParser.SubstituteGenericTypes(fieldType, _currentTypeSubstitutions);
        }

        var memberAccess = new IrMemberAccess(resultName, actualBase, memberName, fieldType, field.Offset);
        _currentBlock!.AddInstruction(memberAccess);

        return new IrVariable(resultName, fieldType);
    }

    public override object? VisitTurboFishExpr([NotNull] NovusParser.TurboFishExprContext context)
    {
        // Handle turbo-fish syntax: Type::<Args>
        // This creates a parameterized type expression that can then be used with :: to access members
        var baseExpr = context.expression();
        var genericArgsCtx = context.genericTypeArgs();

        // Parse the generic type arguments
        var explicitTypeArgs = new List<IrType>();
        foreach (var typeCtx in genericArgsCtx.typeList().type())
        {
            var irType = ParseType(typeCtx);
            explicitTypeArgs.Add(irType);
        }

        // The base expression should be an identifier for the type
        string? typeName = null;
        if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            typeName = identCtx.identifier().GetText();
        }

        if (typeName == null)
        {
            var errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Turbo-fish expression must reference a type",
                errorLocation
            );
            return null;
        }

        // Return a marker that stores the type name and explicit type arguments
        // This will be consumed by PathExpr when accessing members
        return new IrTurboFishType(typeName, explicitTypeArgs);
    }

    public override object? VisitPathExpr([NotNull] NovusParser.PathExprContext context)
    {
        SourceLocation errorLocation;

        // Handle path expressions: Type::name
        // This can be:
        // 1. Enum variants: Option::Some, Result::Ok
        // 2. Associated functions (static methods): Vec::new, Vec::with_capacity
        // 3. Members accessed on turbo-fish types: (Vec::<u32>)::with_capacity
        var baseExpr = context.expression();
        var memberName = context.IDENTIFIER().GetText();

        // Check if base expression is a turbo-fish type
        List<IrType>? explicitTypeArgs = null;
        string? typeName = null;

        var baseValue = Visit(baseExpr);
        if (baseValue is IrTurboFishType turboFish)
        {
            typeName = turboFish.TypeName;
            explicitTypeArgs = turboFish.TypeArguments;
        }
        // The base expression should be an identifier for the type
        else if (baseExpr is NovusParser.PrimaryExprContext primaryCtx &&
            primaryCtx.GetChild(0) is NovusParser.IdentifierExprContext identCtx)
        {
            typeName = identCtx.identifier().GetText();
        }

        if (typeName == null)
        {
            var baseExprType = baseExpr?.GetType().Name ?? "null";
            var baseExprText = baseExpr?.GetText() ?? "null";
            errorLocation = GetLocation(context);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Path expression must reference a type (got {baseExprType}: '{baseExprText}')",
                errorLocation
            );
            return null;
        }

        // Try enum variant first
        if (_symbols.HasEnum(typeName))
        {
            var enumType = _symbols.LookupEnum(typeName)!;
            var variant = enumType.GetVariant(memberName);

            if (variant == null)
            {
                errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.InvalidExpressionType,
                    $"Enum '{typeName}' has no variant '{memberName}'",
                    errorLocation
                );
                return null;
            }

            // Use expected type if it's a more specific (concrete) version of this enum
            var concreteEnumType = enumType;
            if (_expectedType is IrEnumType expectedEnum &&
                expectedEnum.EnumName == enumType.EnumName &&
                expectedEnum.CacheKey != null)
            {
                // Use the concrete type from context (e.g., Option<MemoryBlock> instead of Option<T>)
                concreteEnumType = expectedEnum;
            }

            // For unit variants (no associated data), create the enum value directly
            if (variant.AssociatedData.Count == 0)
            {
                return new IrEnumValue(concreteEnumType, memberName, variant.Tag, new List<IrValue>());
            }

            // Return enum constructor for variants with data
            return new IrEnumConstructor(concreteEnumType, memberName, variant.Tag);
        }

        // Try associated function (struct method without self parameter)
        var mangledName = Generics.InstantiationKeyBuilder.BuildInherentMethodName(typeName, memberName);

        // Check if this is a generic type - look in generic method templates
        if (_symbols.HasStruct(typeName))
        {
            var structType = _symbols.LookupStruct(typeName)!;

            // If the struct is generic, check generic method templates
            if (structType.GenericParameters.Count > 0)
            {
                var templateKey = mangledName;
                if (_genericInstantiator.HasMethodTemplate(templateKey))
                {
                    // Return a special marker for generic associated function
                    // This will be instantiated later when we know the concrete types
                    return new IrGenericAssociatedFunction(typeName, memberName, structType.GenericParameters, explicitTypeArgs);
                }
            }
        }

        // Try to find the function in the module
        var function = _module.GetFunction(mangledName);
        if (function != null)
        {
            // Check if this is an associated function (no self parameter)
            if (function.Parameters.Count == 0 || function.Parameters[0].Name != "self")
            {
                // Return a function reference that can be called
                return new IrFunctionRef(function);
            }
            else
            {
                errorLocation = GetLocation(context);
                _diagnostics.ReportError(
                    ErrorCodes.CannotCallMethodOnType,
                    $"Cannot call method '{memberName}' of type '{typeName}' without an instance (it requires 'self')",
                    errorLocation
                );
                return null;
            }
        }

        errorLocation = new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            $"Type '{typeName}' has no associated function or variant '{memberName}'",
            errorLocation
        );
        return null;
    }


    /// <summary>
    /// Parse a type from the AST - delegates to TypeParser for unified type parsing logic
    /// </summary>
    /// Returns true if the type has a drop() method (either already instantiated or newly instantiated).
    /// </summary>

    /// <summary>
    /// Recursively extracts generic type mappings by comparing base and monomorphized types.
    /// Handles nested generics in pointers, arrays, and other type constructors.
    /// </summary>

    // ===========================
    // Copper DSL Expression
    // ===========================

    /// <summary>
    /// Handle Copper DSL expression: copper { wait(0, 100); move(COLOR00, $F00); }
    /// Generates HIR Copper instructions that will be lowered to actual copper list data.
    /// </summary>
    public override object? VisitCopperExpr([NotNull] NovusParser.CopperExprContext context)
    {
        var copperList = context.copperList();
        var operations = new List<HirCopperInstruction>();

        // Process each copper operation
        foreach (var opContext in copperList.copperOperation())
        {
            // Get the operation name from copperOpName (IDENTIFIER)
            var opName = opContext.copperOpName().IDENTIFIER().GetText().ToLower();
            var arg0 = (IrValue?)Visit(opContext.expression(0));
            var arg1 = (IrValue?)Visit(opContext.expression(1));

            if (arg0 != null && arg1 != null)
            {
                switch (opName)
                {
                    case "wait":
                        operations.Add(new HirCopperInstruction.Wait(arg0, arg1));
                        break;
                    case "move":
                        operations.Add(new HirCopperInstruction.Move(arg0, arg1));
                        break;
                    case "skip":
                        operations.Add(new HirCopperInstruction.Skip(arg0, arg1));
                        break;
                }
            }
        }

        // Generate a temporary to hold the copper list pointer
        var resultTemp = $"%copper_{_tempCounter++}";
        var copperPtrType = new IrPointerType(IrIntType.U16);

        // Create HIR copper list node with result variable info
        var copperListNode = new HirCopperList(operations)
        {
            ResultName = resultTemp,
            ResultType = copperPtrType
        };

        // Add HIR instruction to module - will be lowered by CopperLoweringPass
        _module.HirInstructions.Add(copperListNode);

        // Declare local variable to hold the copper list pointer
        var resultLocal = new IrLocalVariable(resultTemp, copperPtrType, false);
        _currentFunction?.LocalVariables.Add(resultLocal);
        _currentBlock?.AddInstruction(new IrLocalDecl(resultTemp, copperPtrType, false, new IrConstant(0, IrIntType.U32)));

        return new IrVariable(resultTemp, copperPtrType);
    }

    // ===========================
    // Blitter DSL Expression
    // ===========================

    // Custom chip base address
    private const uint CUSTOM_BASE = 0xDFF000;

    // Blitter register offsets
    private const ushort DMACONR = 0x002;   // DMA control read (bit 14 = BLTBUSY)
    private const ushort BLTCON0 = 0x040;   // Blitter control register 0
    private const ushort BLTCON1 = 0x042;   // Blitter control register 1
    private const ushort BLTAFWM = 0x044;   // Blitter first word mask for source A
    private const ushort BLTALWM = 0x046;   // Blitter last word mask for source A
    private const ushort BLTCPT = 0x048;    // Blitter pointer to source C (32-bit)
    private const ushort BLTBPT = 0x04C;    // Blitter pointer to source B (32-bit)
    private const ushort BLTAPT = 0x050;    // Blitter pointer to source A (32-bit)
    private const ushort BLTDPT = 0x054;    // Blitter pointer to destination D (32-bit)
    private const ushort BLTSIZE = 0x058;   // Blitter start and size (starts blit!)
    private const ushort BLTCMOD = 0x060;   // Blitter modulo for source C
    private const ushort BLTBMOD = 0x062;   // Blitter modulo for source B
    private const ushort BLTAMOD = 0x064;   // Blitter modulo for source A
    private const ushort BLTDMOD = 0x066;   // Blitter modulo for destination D
    private const ushort BLTADAT = 0x074;   // Blitter source A data (pattern)

    // Common minterms
    private const byte MINTERM_COPY = 0xF0;     // D = A

    /// <summary>
    /// Handle Blitter DSL expression: blitter { source: ptr, dest: screen, width: 16, height: 16, minterm: $F0 }
    /// Generates inline blitter register write code.
    /// </summary>
    public override object? VisitBlitterExpr([NotNull] NovusParser.BlitterExprContext context)
    {
        var blitterJob = context.blitterJob();
        var fields = new Dictionary<string, IrValue>();

        // Process each blitter field
        foreach (var fieldContext in blitterJob.blitterField())
        {
            var fieldName = fieldContext.IDENTIFIER().GetText();
            var fieldValue = (IrValue?)Visit(fieldContext.expression());

            if (fieldValue != null)
            {
                fields[fieldName.ToLower()] = fieldValue;
            }
        }

        // Extract and validate blitter parameters
        IrValue? sourceA = fields.GetValueOrDefault("source") ?? fields.GetValueOrDefault("sourcea");
        IrValue? sourceB = fields.GetValueOrDefault("sourceb");
        IrValue? sourceC = fields.GetValueOrDefault("sourcec");
        IrValue? destination = fields.GetValueOrDefault("dest") ?? fields.GetValueOrDefault("destination");

        if (destination == null)
        {
            // Error: No destination specified - this will be caught by semantic analysis
            // For now, just return unit and don't generate any code
            return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
        }

        // Extract dimensions (default 16x16)
        int widthPixels = 16;
        int heightLines = 16;

        if (fields.TryGetValue("width", out var widthVal) && TryEvaluateConstant(widthVal, out int w))
            widthPixels = w;
        if (fields.TryGetValue("height", out var heightVal) && TryEvaluateConstant(heightVal, out int h))
            heightLines = h;

        // Width in words (16 pixels per word for 1bpp)
        int widthWords = (widthPixels + 15) / 16;

        // BLTSIZE format: height (10 bits) << 6 | width_words (6 bits)
        ushort bltSize = (ushort)((heightLines << 6) | widthWords);

        // Extract minterm (default copy)
        byte minterm = MINTERM_COPY;
        if (fields.TryGetValue("minterm", out var mintermVal) && TryEvaluateConstant(mintermVal, out int mt))
            minterm = (byte)(mt & 0xFF);

        // Determine which channels are used
        bool useA = sourceA != null;
        bool useB = sourceB != null;
        bool useC = sourceC != null;
        bool useD = true; // Always use destination

        // BLTCON0: ASH3-0 | USEA | USEB | USEC | USED | LF7-0
        ushort bltcon0 = minterm;
        if (useA) bltcon0 |= 0x0800;  // USEA
        if (useB) bltcon0 |= 0x0400;  // USEB
        if (useC) bltcon0 |= 0x0200;  // USEC
        if (useD) bltcon0 |= 0x0100;  // USED

        // Extract shift value for source A
        if (fields.TryGetValue("shift", out var shiftVal) && TryEvaluateConstant(shiftVal, out int shift))
            bltcon0 |= (ushort)((shift & 0x0F) << 12);

        // BLTCON1: BSH3-0 | 0 | EFE | IFE | FCI | DESC | LINE | 0
        ushort bltcon1 = 0;
        if (fields.TryGetValue("descending", out var descVal) && descVal is IrBoolConstant descBool && descBool.Value)
            bltcon1 |= 0x0002;  // DESC
        if (fields.TryGetValue("fill", out var fillVal) && fillVal is IrBoolConstant fillBool && fillBool.Value)
            bltcon1 |= 0x0008;  // IFE

        // Extract modulos (default 0)
        short modA = 0, modB = 0, modC = 0, modD = 0;
        if (fields.TryGetValue("moda", out var modaVal) && TryEvaluateConstant(modaVal, out int ma))
            modA = (short)ma;
        if (fields.TryGetValue("modb", out var modbVal) && TryEvaluateConstant(modbVal, out int mb))
            modB = (short)mb;
        if (fields.TryGetValue("modc", out var modcVal) && TryEvaluateConstant(modcVal, out int mc))
            modC = (short)mc;
        if (fields.TryGetValue("modd", out var moddVal) && TryEvaluateConstant(moddVal, out int md))
            modD = (short)md;

        // Check if we should wait for completion (default true)
        bool waitForCompletion = true;
        if (fields.TryGetValue("wait", out var waitVal) && waitVal is IrBoolConstant waitBool)
            waitForCompletion = waitBool.Value;

        // Generate blitter register write code
        GenerateBlitterCode(bltcon0, bltcon1, bltSize, sourceA, sourceB, sourceC, destination,
                            modA, modB, modC, modD, waitForCompletion);

        // Blitter jobs return unit (execute synchronously)
        return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
    }

    /// <summary>
    /// Generate IR instructions for blitter register setup.
    /// This generates straight-line code that writes to hardware registers.
    /// Note: Does NOT generate wait loops - users should call WaitBlit() from std::ffi::graphics.
    /// </summary>
    private void GenerateBlitterCode(ushort bltcon0, ushort bltcon1, ushort bltSize,
                                      IrValue? sourceA, IrValue? sourceB, IrValue? sourceC, IrValue destination,
                                      short modA, short modB, short modC, short modD, bool waitForCompletion)
    {
        if (_currentBlock == null) return;

        var u16PtrType = new IrPointerType(IrIntType.U16);
        var u32PtrType = new IrPointerType(IrIntType.U32);
        var i16PtrType = new IrPointerType(IrIntType.I16);

        // Helper to create a hardware register write (16-bit)
        void WriteReg16(ushort offset, ushort value)
        {
            var addr = new IrConstant(CUSTOM_BASE + offset, IrIntType.U32);
            var ptr = new IrCastValue(addr, IrIntType.U32, u16PtrType);
            var val = new IrConstant(value, IrIntType.U16);
            _currentBlock?.AddInstruction(new IrDereferenceStore(ptr, val));
        }

        // Helper to create a hardware register write (32-bit pointer)
        void WriteReg32Ptr(ushort offset, IrValue ptrValue)
        {
            var addr = new IrConstant(CUSTOM_BASE + offset, IrIntType.U32);
            var ptr = new IrCastValue(addr, IrIntType.U32, u32PtrType);
            // Cast the pointer to u32 for storage
            var ptrAsU32 = new IrCastValue(ptrValue, ptrValue.Type, IrIntType.U32);
            _currentBlock?.AddInstruction(new IrDereferenceStore(ptr, ptrAsU32));
        }

        // Helper to create a modulo register write (signed 16-bit)
        void WriteModulo(ushort offset, short value)
        {
            var addr = new IrConstant(CUSTOM_BASE + offset, IrIntType.U32);
            var ptr = new IrCastValue(addr, IrIntType.U32, i16PtrType);
            var val = new IrConstant(value, IrIntType.I16);
            _currentBlock?.AddInstruction(new IrDereferenceStore(ptr, val));
        }

        // Note: We don't generate wait loops inline - the user should call WaitBlit()
        // from std::ffi::graphics before and after blitter operations for proper
        // synchronization. This keeps the DSL simple and predictable.

        // 1. Write control registers (order matters!)
        WriteReg16(BLTCON1, bltcon1);
        WriteReg16(BLTCON0, bltcon0);

        // 2. Write word masks (full words by default)
        WriteReg16(BLTAFWM, 0xFFFF);
        WriteReg16(BLTALWM, 0xFFFF);

        // 3. Write modulos
        if ((bltcon0 & 0x0800) != 0) WriteModulo(BLTAMOD, modA);  // Source A
        if ((bltcon0 & 0x0400) != 0) WriteModulo(BLTBMOD, modB);  // Source B
        if ((bltcon0 & 0x0200) != 0) WriteModulo(BLTCMOD, modC);  // Source C
        WriteModulo(BLTDMOD, modD);  // Destination

        // 4. Write source/destination pointers
        if (sourceA != null) WriteReg32Ptr(BLTAPT, sourceA);
        if (sourceB != null) WriteReg32Ptr(BLTBPT, sourceB);
        if (sourceC != null) WriteReg32Ptr(BLTCPT, sourceC);
        WriteReg32Ptr(BLTDPT, destination);

        // 5. Write BLTSIZE to trigger the blit (MUST BE LAST!)
        WriteReg16(BLTSIZE, bltSize);

        // Note: If waitForCompletion was true, users should call WaitBlit() after this.
        // We emit a comment to document this but don't actually generate the wait code
        // since it requires proper basic block handling for loops.
    }

    /// <summary>
    /// Try to evaluate an IrValue to a compile-time constant integer
    /// </summary>
    private bool TryEvaluateConstant(IrValue value, out int result)
    {
        result = 0;

        switch (value)
        {
            case IrConstant constant:
                result = (int)constant.Value;
                return true;

            case IrCastValue cast:
                return TryEvaluateConstant(cast.Value, out result);

            default:
                return false;
        }
    }
}
