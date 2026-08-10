using Novus.Diagnostics;
using Novus.IR;

namespace Novus.Frontend;

/// <summary>
/// IrBuilder partial class containing RAII/defer management and automatic Drop() handling.
/// This file contains methods for managing defer scopes and automatic resource cleanup.
/// </summary>
public partial class IrBuilder
{
    private bool EnsureDropMethodInstantiated(IrType type)
    {
        // Check if this type implements the Drop trait
        if (!_module.TypeImplementsDrop(type))
        {
            return false;
        }

        // Tuples don't have explicit Drop methods - we handle them by
        // dropping each element inline. Just return true so the caller
        // knows this type needs cleanup.
        if (type is IrTupleType tupleType)
        {
            // Recursively ensure all element types that need Drop have their methods ready
            foreach (var elementType in tupleType.ElementTypes)
            {
                if (_module.TypeImplementsDrop(elementType))
                {
                    EnsureDropMethodInstantiated(elementType);
                }
            }
            return true;
        }

        // Enums with droppable payloads generate inline Drop code.
        // We recursively ensure payload types have their Drop methods ready.
        if (type is IrEnumType enumType && _module.EnumNeedsDrop(enumType))
        {
            foreach (var variant in enumType.Variants)
            {
                foreach (var payloadType in variant.AssociatedData)
                {
                    if (_module.TypeImplementsDrop(payloadType))
                    {
                        EnsureDropMethodInstantiated(payloadType);
                    }
                }
            }
            return true;
        }

        // For struct types, check if they have an explicit Drop method or generic template
        if (type is not IrStructType st)
        {
            // Only structs, enums, and tuples can have Drop
            // (enums and tuples are handled above)
            return false;
        }

        // Skip if this is a generic template (has unsubstituted generic parameters)
        // We can only instantiate Drop for concrete types
        if (st.GenericParameters.Count > 0)
        {
            return false;
        }

        var baseTypeName = st.StructName;  // Base name for template lookup (e.g., "Vec")
        // For monomorphized types, use CacheKey (e.g., "Vec<bool>")
        // For non-generic types, use StructName
        var typeName = st.CacheKey ?? st.StructName;

        // Look for Type_drop method in the module
        // The Drop trait implementation generates: Type_Drop_drop
        // (trait impl convention: {Type}_{Trait}_{method})
        // For monomorphized types like Vec<bool>, this would be Vec<bool>_Drop_drop
        var dropMethod = $"{typeName}_Drop_drop";

        // Check if already instantiated
        if (_module.Functions.Any(f => f.Name == dropMethod))
        {
            return true;
        }

        // Check if there's a generic template for the drop() method
        // Use base type name for template lookup (e.g., "Vec" not "Vec<bool>")
        var templateKey = $"{baseTypeName}::drop";

        if (_genericInstantiator.HasMethodTemplate(templateKey))
        {
            // Instantiate the generic drop() method as a trait impl
            try
            {
                // Pass isTraitImpl=true and traitName="Drop" for proper mangling
                var instantiatedFunc = _genericInstantiator.InstantiateStructMethod(
                    st,
                    "drop",
                    isTraitImpl: true,
                    traitName: "Drop",
                    traitTypeArgs: new List<IrType>()
                );

                if (instantiatedFunc != null)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging - Drop instantiation failures can indicate
                // issues with generic type substitution or method lookup
                System.Diagnostics.Debug.WriteLine($"Failed to ensure Drop method for {st.StructName}: {ex.Message}");
                return false;
            }
        }

        // No drop method exists
        return false;
    }

    /// <summary>
    /// Check if a type has a drop() method.
    /// This enables automatic defer cleanup for RAII-style resource management.
    /// </summary>
    private bool TypeHasDropMethod(IrType type)
    {
        // Get the type name for method lookup
        string typeName;
        if (type is IrStructType structType)
        {
            typeName = structType.StructName;  // Use base name for generic types
        }
        else if (type is IrEnumType enumType)
        {
            typeName = enumType.EnumName;
        }
        else
        {
            // Only structs and enums can have methods
            return false;
        }

        // Look for Type_drop method in the module
        var dropMethod = $"{typeName}_drop";
        return _module.Functions.Any(f => f.Name == dropMethod);
    }

    /// <summary>
    /// Push a new defer scope. Variables declared in this scope will have their
    /// defer cleanup emitted when PopDeferScope() is called.
    /// </summary>
    private void PushDeferScope()
    {
        _scopeDeferStack.Push(new List<IrBasicBlock>());
    }

    /// <summary>
    /// Pop the current defer scope and emit cleanup for all defers registered in this scope.
    /// Returns the list of defer blocks that were emitted (in LIFO order).
    /// </summary>
    private List<IrBasicBlock> PopDeferScope()
    {
        if (_scopeDeferStack.Count == 0)
        {
            return new List<IrBasicBlock>();
        }

        var scopeDefers = _scopeDeferStack.Pop();

        // Emit defers in LIFO order (last registered, first executed)
        for (int i = scopeDefers.Count - 1; i >= 0; i--)
        {
            var deferBlock = scopeDefers[i];

            // Check if this defer block has already been emitted (prevent double-free)
            // A defer block can be registered at both function and scope level
            if (_emittedDeferBlocks.Contains(deferBlock))
            {
                continue; // Skip already-emitted defer blocks
            }

            // Emit all instructions in the defer block
            foreach (var instruction in deferBlock.Instructions)
            {
                _currentBlock!.AddInstruction(instruction);
            }

            // Mark as emitted to prevent double execution
            _emittedDeferBlocks.Add(deferBlock);

            // Remove from function-level defer list (so it doesn't get emitted again at function exit)
            _currentFunction!.DeferredBlocks.Remove(deferBlock);
        }

        return scopeDefers;
    }

    /// <summary>
    /// Inject an automatic defer block that calls drop() on a variable.
    /// This implements RAII-style cleanup for types with drop() methods.
    /// </summary>
    private void InjectAutomaticDrop(string varName, IrType type)
    {
        // Create a new basic block for the deferred drop() call
        var deferLabel = $"autoclean_{varName}_{_labelCounter++}";
        var deferBlock = new IrBasicBlock(deferLabel);

        // Save current block
        var savedBlock = _currentBlock;
        _currentBlock = deferBlock;

        // Handle tuple types specially - they don't have a single Drop method,
        // instead we drop each element that implements Drop (in reverse order for LIFO semantics)
        if (type is IrTupleType tupleType)
        {
            InjectTupleElementDrops(deferBlock, varName, tupleType);
        }
        // Handle enum types - they need runtime dispatch based on the tag to drop payloads
        else if (type is IrEnumType enumType)
        {
            InjectEnumDrop(deferBlock, varName, enumType);
        }
        else if (type is IrStructType structType)
        {
            // For monomorphized types, use CacheKey (e.g., "Vec<bool>")
            // For non-generic types, use StructName
            var typeName = structType.CacheKey ?? structType.StructName;

            // The Drop trait implementation generates: Type_Drop_drop
            var dropMethodName = $"{typeName}_Drop_drop";
            var dropMethod = _module.GetFunction(dropMethodName);
            if (dropMethod == null)
            {
                var errorLocation = _currentStatementLocation ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
                _diagnostics.ReportError(
                    ErrorCodes.MethodNotFound,
                    $"Drop method '{dropMethodName}' not found (this should have been instantiated already)",
                    errorLocation
                );
                _currentBlock = savedBlock;
                return;
            }

            // Load the variable and borrow it mutably for drop()
            var varRef = new IrVariable(varName, type);
            var mutBorrow = new IrBorrowValue(varRef, new IrMutReferenceType(type), isMutable: true);

            // Create the drop() call (drop() returns void)
            var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
            dropCall.Arguments.Add(mutBorrow);
            deferBlock.AddInstruction(dropCall);
        }
        else
        {
            // Use current statement location for error reporting (set by caller)
            var errorLocation = _currentStatementLocation ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Cannot generate drop call for type '{type.Name}'",
                errorLocation
            );
            _currentBlock = savedBlock;
            return;
        }

        // Restore current block
        _currentBlock = savedBlock;

        // Add the defer block to the function's deferred blocks list (LIFO)
        _currentFunction!.DeferredBlocks.Add(deferBlock);

        // ALSO add to current scope's defer list if we're in a scope
        if (_scopeDeferStack.Count > 0)
        {
            _scopeDeferStack.Peek().Add(deferBlock);
        }

        // Add defer instruction to current block (marker)
        _currentBlock!.AddInstruction(new IrDefer(deferBlock));
    }

    private void InjectParameterDrops()
    {
        foreach (var parameter in _currentFunction!.Parameters)
        {
            if (parameter.Type is not IrReferenceType and not IrMutReferenceType &&
                EnsureDropMethodInstantiated(parameter.Type))
            {
                InjectAutomaticDrop(parameter.Name, parameter.Type);
            }
        }
    }

    /// <summary>
    /// Inject drop code for an enum type. This generates a switch on the enum's tag
    /// and drops the payload for each variant that has a droppable payload.
    /// </summary>
    private void InjectEnumDrop(IrBasicBlock deferBlock, string varName, IrEnumType enumType)
    {
        // Check if any variant actually needs dropping
        bool anyVariantNeedsDrop = false;
        foreach (var variant in enumType.Variants)
        {
            foreach (var payloadType in variant.AssociatedData)
            {
                if (_module.TypeImplementsDrop(payloadType))
                {
                    anyVariantNeedsDrop = true;
                    break;
                }
            }
            if (anyVariantNeedsDrop) break;
        }

        if (!anyVariantNeedsDrop)
        {
            // No variant has a droppable payload, nothing to do
            return;
        }

        // We need to generate IR that:
        // 1. Loads the enum's tag
        // 2. For each variant with droppable payloads, check tag and drop those payloads

        var varRef = new IrVariable(varName, enumType);

        // Generate unique labels for this enum drop
        var uniqueId = _labelCounter++;
        var endLabel = $"enum_drop_end_{uniqueId}";

        // For each variant that has a droppable payload, generate drop code
        foreach (var variant in enumType.Variants)
        {
            bool variantNeedsDrop = false;
            foreach (var payloadType in variant.AssociatedData)
            {
                if (_module.TypeImplementsDrop(payloadType))
                {
                    variantNeedsDrop = true;
                    break;
                }
            }

            if (!variantNeedsDrop)
            {
                continue;
            }

            // Generate: if (var.tag == VariantTag) { drop payloads; }
            var variantLabel = $"enum_drop_{variant.Name}_{uniqueId}";
            var skipLabel = $"enum_drop_skip_{variant.Name}_{uniqueId}";

            // Extract the tag using IrExtractTag instruction
            var tagValue = $"_enum_tag_{variant.Name}_{uniqueId}";
            var extractTag = new IrExtractTag(tagValue, varRef);
            deferBlock.AddInstruction(extractTag);

            // Compare with variant's tag value
            var variantTagValue = new IrConstant(variant.Tag, IrIntType.I32);
            var compareResult = $"_enum_cmp_{variant.Name}_{uniqueId}";
            var compare = new IrBinaryOp(
                compareResult,
                IrBinaryOp.OpKind.Eq,
                new IrVariable(tagValue, IrIntType.I32),
                variantTagValue,
                IrBoolType.Instance
            );
            deferBlock.AddInstruction(compare);

            // Conditional branch: if tag matches, go to drop code; else skip
            var condBranch = new IrConditionalBranch(
                new IrVariable(compareResult, IrBoolType.Instance),
                variantLabel,
                skipLabel
            );
            deferBlock.AddInstruction(condBranch);

            // Label for this variant's drop code
            deferBlock.AddInstruction(new IrLabel(variantLabel));

            // Drop each payload that implements Drop
            for (int payloadIdx = 0; payloadIdx < variant.AssociatedData.Count; payloadIdx++)
            {
                var payloadType = variant.AssociatedData[payloadIdx];
                if (!_module.TypeImplementsDrop(payloadType))
                {
                    continue;
                }

                // Access the payload: var.data.VariantName._N
                var payloadAccess = new IrEnumPayloadAccess(varRef, enumType, variant.Name, payloadIdx, payloadType);

                // Generate drop call for this payload
                if (payloadType is IrStructType payloadStructType)
                {
                    var payloadTypeName = payloadStructType.CacheKey ?? payloadStructType.StructName;
                    var dropMethodName = $"{payloadTypeName}_Drop_drop";

                    // Borrow the payload mutably
                    var payloadBorrow = new IrBorrowValue(payloadAccess, new IrMutReferenceType(payloadType), isMutable: true);

                    var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
                    dropCall.Arguments.Add(payloadBorrow);
                    deferBlock.AddInstruction(dropCall);
                }
                else if (payloadType is IrTupleType payloadTupleType)
                {
                    // Handle tuple payloads - extract to temp and drop elements
                    var tempName = $"_enum_payload_tmp_{variant.Name}_{payloadIdx}_{uniqueId}";
                    var extractPayload = new IrExtractVariantData(tempName, varRef, variant.Name, payloadIdx, payloadType);
                    deferBlock.AddInstruction(extractPayload);
                    InjectTupleElementDrops(deferBlock, tempName, payloadTupleType);
                }
                else if (payloadType is IrEnumType payloadEnumType)
                {
                    // Handle nested enum payloads - extract to temp and recursively drop
                    var tempName = $"_enum_payload_tmp_{variant.Name}_{payloadIdx}_{uniqueId}";
                    var extractPayload = new IrExtractVariantData(tempName, varRef, variant.Name, payloadIdx, payloadType);
                    deferBlock.AddInstruction(extractPayload);
                    InjectEnumDrop(deferBlock, tempName, payloadEnumType);
                }
            }

            // Jump to end after dropping
            deferBlock.AddInstruction(new IrBranch(endLabel));

            // Skip label for when this variant doesn't match
            deferBlock.AddInstruction(new IrLabel(skipLabel));
        }

        // End label
        deferBlock.AddInstruction(new IrLabel(endLabel));
    }

    /// <summary>
    /// Inject drop calls for each element of a tuple that implements Drop.
    /// Elements are dropped in reverse order (LIFO) for proper cleanup semantics.
    /// </summary>
    private void InjectTupleElementDrops(IrBasicBlock deferBlock, string tupleVarName, IrTupleType tupleType)
    {
        // Drop elements in reverse order (last element first) for LIFO cleanup semantics
        for (int i = tupleType.ElementTypes.Count - 1; i >= 0; i--)
        {
            var elementType = tupleType.ElementTypes[i];

            // Only drop elements that implement Drop
            if (!_module.TypeImplementsDrop(elementType))
            {
                continue;
            }

            // Handle nested tuples recursively
            if (elementType is IrTupleType nestedTuple)
            {
                // For nested tuples, we need to create a synthetic variable name for the element
                // and recursively inject drops for its elements
                // This is done by accessing the tuple element and dropping its contents inline
                InjectNestedTupleElementDrops(deferBlock, tupleVarName, i, nestedTuple);
                continue;
            }

            // Get the drop method name for this element type
            string elementTypeName;
            if (elementType is IrStructType st)
            {
                elementTypeName = st.CacheKey ?? st.StructName;
            }
            else if (elementType is IrEnumType et)
            {
                elementTypeName = et.EnumName;
            }
            else
            {
                // Skip types that can't have Drop (shouldn't happen if TypeImplementsDrop is correct)
                continue;
            }

            var dropMethodName = $"{elementTypeName}_Drop_drop";
            var dropMethod = _module.GetFunction(dropMethodName);
            if (dropMethod == null)
            {
                // Log warning but continue - element might not actually need drop
                continue;
            }

            // Access the tuple element: tuple.__i
            var tupleVar = new IrVariable(tupleVarName, tupleType);
            var elementAccess = new IrTupleElementAccess(tupleVar, i, elementType);

            // Borrow the element mutably for drop()
            // We need to borrow the element via the tuple field access
            var elementBorrow = new IrBorrowValue(elementAccess, new IrMutReferenceType(elementType), isMutable: true);

            // Create the drop() call for this element
            var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
            dropCall.Arguments.Add(elementBorrow);
            deferBlock.AddInstruction(dropCall);
        }
    }

    /// <summary>
    /// Handle nested tuple drop - when a tuple element is itself a tuple.
    /// This method takes an IrValue representing the access path to the nested tuple,
    /// allowing proper chaining for deeply nested tuples like ((String, Vec), i32).
    /// </summary>
    private void InjectNestedTupleElementDrops(IrBasicBlock deferBlock, string parentTupleVar, int elementIndex, IrTupleType nestedTupleType)
    {
        // Build the access to the nested tuple: parentTuple.__elementIndex
        // We need to know the parent tuple's type to create the access
        // For now, create a synthetic parent tuple type containing the nested type at this index
        var parentTupleVarRef = new IrVariable(parentTupleVar, nestedTupleType);

        // Create the access to the nested tuple element from the parent
        var nestedTupleAccess = new IrTupleElementAccess(
            new IrVariable(parentTupleVar, new IrTupleType(new List<IrType> { nestedTupleType })),
            elementIndex,
            nestedTupleType);

        // Recursively drop elements of the nested tuple
        InjectNestedTupleElementDropsWithAccess(deferBlock, nestedTupleAccess, nestedTupleType);
    }

    /// <summary>
    /// Recursively inject drop calls for a nested tuple, given an IrValue that represents
    /// the access path to that tuple. This enables arbitrary nesting depth.
    /// </summary>
    private void InjectNestedTupleElementDropsWithAccess(IrBasicBlock deferBlock, IrValue tupleAccess, IrTupleType tupleType)
    {
        // Drop elements in reverse order (LIFO)
        for (int i = tupleType.ElementTypes.Count - 1; i >= 0; i--)
        {
            var elementType = tupleType.ElementTypes[i];

            if (!_module.TypeImplementsDrop(elementType))
            {
                continue;
            }

            // Access this element: tupleAccess.__i
            var innerElementAccess = new IrTupleElementAccess(tupleAccess, i, elementType);

            // Handle deeply nested tuples recursively
            if (elementType is IrTupleType deeplyNestedTuple)
            {
                // Recursively handle the deeply nested tuple
                InjectNestedTupleElementDropsWithAccess(deferBlock, innerElementAccess, deeplyNestedTuple);
                continue;
            }

            // Get the drop method for this element
            string elementTypeName;
            if (elementType is IrStructType st)
            {
                elementTypeName = st.CacheKey ?? st.StructName;
            }
            else if (elementType is IrEnumType et)
            {
                elementTypeName = et.EnumName;
            }
            else
            {
                continue;
            }

            var dropMethodName = $"{elementTypeName}_Drop_drop";
            var dropMethod = _module.GetFunction(dropMethodName);
            if (dropMethod == null)
            {
                continue;
            }

            // Borrow the element mutably for drop()
            var elementBorrow = new IrBorrowValue(innerElementAccess, new IrMutReferenceType(elementType), isMutable: true);

            // Create the drop() call
            var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
            dropCall.Arguments.Add(elementBorrow);
            deferBlock.AddInstruction(dropCall);
        }
    }

    /// <summary>
    /// Deactivate the defer for a variable if it implements Drop and is being moved.
    /// This prevents double-free when a variable's value is moved to another location.
    /// </summary>
    /// <param name="value">The value being assigned (may be a variable or expression)</param>
    private void DeactivateVariableDeferIfMove(IrValue value)
    {
        // Only deactivate if the value is a variable (not a temporary expression)
        // and its type implements Drop
        if (value is IrVariable sourceVar && _module.TypeImplementsDrop(sourceVar.Type))
        {
            DeactivateVariableDefer(sourceVar.Name);
        }
    }

    /// <summary>
    /// Deactivate the automatic defer block for a variable.
    /// Call this when a variable has been explicitly dropped via .drop() or when
    /// ownership has been moved elsewhere (e.g., assigned to another location).
    /// This prevents double-free by removing the automatic cleanup for this variable.
    /// </summary>
    /// <param name="varName">The name of the variable whose defer should be deactivated</param>
    private void DeactivateVariableDefer(string varName)
    {
        // Find and remove the defer block for the variable
        // The defer block label starts with "autoclean_{varName}_"
        var labelPrefix = $"autoclean_{varName}_";

        // Check current scope defers
        if (_scopeDeferStack.Count > 0)
        {
            var currentScopeDefers = _scopeDeferStack.Peek();
            var deferToRemove = currentScopeDefers.FirstOrDefault(
                d => d.Label.StartsWith(labelPrefix));
            if (deferToRemove != null)
            {
                currentScopeDefers.Remove(deferToRemove);
                // Also remove from function-level defers
                _currentFunction?.DeferredBlocks.Remove(deferToRemove);
                // Mark as emitted to prevent any other code path from emitting it
                _emittedDeferBlocks.Add(deferToRemove);
            }
        }

        // Also check function-level defers
        if (_currentFunction != null)
        {
            var functionLevelDefer = _currentFunction.DeferredBlocks.FirstOrDefault(
                d => d.Label.StartsWith(labelPrefix));
            if (functionLevelDefer != null)
            {
                _currentFunction.DeferredBlocks.Remove(functionLevelDefer);
                _emittedDeferBlocks.Add(functionLevelDefer);

                // Remove the IrDefer instruction from ALL blocks
                // This instruction tells the C code generator to activate the defer flag
                foreach (var block in _currentFunction.BasicBlocks)
                {
                    var deferInstructionsToRemove = block.Instructions
                        .OfType<IrDefer>()
                        .Where(defer => defer.DeferredBlock == functionLevelDefer)
                        .ToList();

                    foreach (var deferInst in deferInstructionsToRemove)
                    {
                        block.Instructions.Remove(deferInst);
                    }
                }

                // Also check the current block being built
                if (_currentBlock != null && !_currentFunction.BasicBlocks.Contains(_currentBlock))
                {
                    var currentBlockDefers = _currentBlock.Instructions
                        .OfType<IrDefer>()
                        .Where(defer => defer.DeferredBlock == functionLevelDefer)
                        .ToList();

                    foreach (var deferInst in currentBlockDefers)
                    {
                        _currentBlock.Instructions.Remove(deferInst);
                    }
                }
            }

            // The scope lookup above may remove the deferred block before the
            // function-level lookup sees it. Always remove its activation marker too.
            foreach (var block in _currentFunction.BasicBlocks)
            {
                block.Instructions.RemoveAll(instruction =>
                    instruction is IrDefer defer && defer.DeferredBlock.Label.StartsWith(labelPrefix));
            }
        }
    }

    /// <summary>
    /// Inject immediate Drop for a temporary value that is being discarded.
    /// This handles cases like `vec.pop()` where the return value is ignored.
    /// The value must be stored to a temporary variable, then immediately dropped.
    /// </summary>
    private void InjectDropForTemporary(IrValue value)
    {
        var type = value.Type;

        // Generate a unique temporary variable name
        var tempName = $"_discarded_temp_{_labelCounter++}";

        // Store the value to a temporary variable
        var localVar = new IrLocalVariable(tempName, type, isMutable: true);
        _currentFunction!.LocalVariables.Add(localVar);
        _localVariables[tempName] = localVar;
        _currentBlock!.AddInstruction(new IrLocalDecl(tempName, type, isMutable: true, value));

        // Now emit the drop call immediately (not deferred - we want it now)
        if (type is IrTupleType tupleType)
        {
            // For tuples, drop each element that implements Drop
            EmitImmediateTupleDrop(tempName, tupleType);
        }
        else if (type is IrEnumType enumType)
        {
            // For enums, emit tag-based drop dispatch
            EmitImmediateEnumDrop(tempName, enumType);
        }
        else if (type is IrStructType structType)
        {
            // For structs, call the Drop method directly
            var typeName = structType.CacheKey ?? structType.StructName;
            var dropMethodName = $"{typeName}_Drop_drop";

            // Ensure the drop method is instantiated
            EnsureDropMethodInstantiated(type);

            var dropMethod = _module.GetFunction(dropMethodName);
            if (dropMethod != null)
            {
                var tempVar = new IrVariable(tempName, type);
                var mutBorrow = new IrBorrowValue(tempVar, new IrMutReferenceType(type), isMutable: true);

                var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
                dropCall.Arguments.Add(mutBorrow);
                _currentBlock!.AddInstruction(dropCall);
            }
        }
    }

    /// <summary>
    /// Emit immediate drop calls for tuple elements (not deferred).
    /// </summary>
    private void EmitImmediateTupleDrop(string tupleVarName, IrTupleType tupleType)
    {
        // Drop elements in reverse order (LIFO)
        for (int i = tupleType.ElementTypes.Count - 1; i >= 0; i--)
        {
            var elementType = tupleType.ElementTypes[i];

            if (!_module.TypeImplementsDrop(elementType))
            {
                continue;
            }

            // Handle nested types recursively
            if (elementType is IrTupleType nestedTuple)
            {
                // For nested tuples, extract to temp and recurse
                var nestedTempName = $"_discarded_tuple_elem_{_labelCounter++}";
                var tupleVar = new IrVariable(tupleVarName, tupleType);
                var elementAccess = new IrTupleElementAccess(tupleVar, i, elementType);

                var nestedLocal = new IrLocalVariable(nestedTempName, elementType, isMutable: true);
                _currentFunction!.LocalVariables.Add(nestedLocal);
                _localVariables[nestedTempName] = nestedLocal;
                _currentBlock!.AddInstruction(new IrLocalDecl(nestedTempName, elementType, isMutable: true, elementAccess));

                EmitImmediateTupleDrop(nestedTempName, nestedTuple);
                continue;
            }

            if (elementType is IrEnumType nestedEnum)
            {
                var nestedTempName = $"_discarded_tuple_elem_{_labelCounter++}";
                var tupleVar = new IrVariable(tupleVarName, tupleType);
                var elementAccess = new IrTupleElementAccess(tupleVar, i, elementType);

                var nestedLocal = new IrLocalVariable(nestedTempName, elementType, isMutable: true);
                _currentFunction!.LocalVariables.Add(nestedLocal);
                _localVariables[nestedTempName] = nestedLocal;
                _currentBlock!.AddInstruction(new IrLocalDecl(nestedTempName, elementType, isMutable: true, elementAccess));

                EmitImmediateEnumDrop(nestedTempName, nestedEnum);
                continue;
            }

            // For struct elements, call Drop directly
            if (elementType is IrStructType st)
            {
                var typeName = st.CacheKey ?? st.StructName;
                var dropMethodName = $"{typeName}_Drop_drop";

                EnsureDropMethodInstantiated(elementType);
                var dropMethod = _module.GetFunction(dropMethodName);
                if (dropMethod != null)
                {
                    var tupleVar = new IrVariable(tupleVarName, tupleType);
                    var elementAccess = new IrTupleElementAccess(tupleVar, i, elementType);
                    var elementBorrow = new IrBorrowValue(elementAccess, new IrMutReferenceType(elementType), isMutable: true);

                    var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
                    dropCall.Arguments.Add(elementBorrow);
                    _currentBlock!.AddInstruction(dropCall);
                }
            }
        }
    }

    /// <summary>
    /// Emit immediate drop for enum (tag-based dispatch, not deferred).
    /// </summary>
    private void EmitImmediateEnumDrop(string varName, IrEnumType enumType)
    {
        // Check if any variant actually needs dropping
        bool anyVariantNeedsDrop = false;
        foreach (var variant in enumType.Variants)
        {
            foreach (var payloadType in variant.AssociatedData)
            {
                if (_module.TypeImplementsDrop(payloadType))
                {
                    anyVariantNeedsDrop = true;
                    break;
                }
            }
            if (anyVariantNeedsDrop) break;
        }

        if (!anyVariantNeedsDrop)
        {
            return;
        }

        var varRef = new IrVariable(varName, enumType);
        var uniqueId = _labelCounter++;
        var endLabel = $"imm_enum_drop_end_{uniqueId}";

        foreach (var variant in enumType.Variants)
        {
            bool variantNeedsDrop = false;
            foreach (var payloadType in variant.AssociatedData)
            {
                if (_module.TypeImplementsDrop(payloadType))
                {
                    variantNeedsDrop = true;
                    break;
                }
            }

            if (!variantNeedsDrop)
            {
                continue;
            }

            var variantLabel = $"imm_enum_drop_{variant.Name}_{uniqueId}";
            var skipLabel = $"imm_enum_drop_skip_{variant.Name}_{uniqueId}";

            // Extract the tag
            var tagValue = $"_imm_enum_tag_{variant.Name}_{uniqueId}";
            var extractTag = new IrExtractTag(tagValue, varRef);
            _currentBlock!.AddInstruction(extractTag);

            // Compare with variant's tag
            var variantTagValue = new IrConstant(variant.Tag, IrIntType.I32);
            var compareResult = $"_imm_enum_cmp_{variant.Name}_{uniqueId}";
            var compare = new IrBinaryOp(
                compareResult,
                IrBinaryOp.OpKind.Eq,
                new IrVariable(tagValue, IrIntType.I32),
                variantTagValue,
                IrBoolType.Instance
            );
            _currentBlock!.AddInstruction(compare);

            // Conditional branch
            var condBranch = new IrConditionalBranch(
                new IrVariable(compareResult, IrBoolType.Instance),
                variantLabel,
                skipLabel
            );
            _currentBlock!.AddInstruction(condBranch);

            // Variant drop code
            _currentBlock!.AddInstruction(new IrLabel(variantLabel));

            // Drop each payload
            for (int payloadIdx = 0; payloadIdx < variant.AssociatedData.Count; payloadIdx++)
            {
                var payloadType = variant.AssociatedData[payloadIdx];
                if (!_module.TypeImplementsDrop(payloadType))
                {
                    continue;
                }

                var payloadAccess = new IrEnumPayloadAccess(varRef, enumType, variant.Name, payloadIdx, payloadType);

                if (payloadType is IrStructType payloadStructType)
                {
                    var payloadTypeName = payloadStructType.CacheKey ?? payloadStructType.StructName;
                    var dropMethodName = $"{payloadTypeName}_Drop_drop";

                    EnsureDropMethodInstantiated(payloadType);

                    var payloadBorrow = new IrBorrowValue(payloadAccess, new IrMutReferenceType(payloadType), isMutable: true);
                    var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
                    dropCall.Arguments.Add(payloadBorrow);
                    _currentBlock!.AddInstruction(dropCall);
                }
                else if (payloadType is IrTupleType payloadTupleType)
                {
                    var tempName = $"_imm_enum_payload_tmp_{variant.Name}_{payloadIdx}_{uniqueId}";
                    var extractPayload = new IrExtractVariantData(tempName, varRef, variant.Name, payloadIdx, payloadType);
                    _currentBlock!.AddInstruction(extractPayload);

                    var tempLocal = new IrLocalVariable(tempName, payloadType, isMutable: true);
                    _currentFunction!.LocalVariables.Add(tempLocal);
                    _localVariables[tempName] = tempLocal;

                    EmitImmediateTupleDrop(tempName, payloadTupleType);
                }
                else if (payloadType is IrEnumType payloadEnumType)
                {
                    var tempName = $"_imm_enum_payload_tmp_{variant.Name}_{payloadIdx}_{uniqueId}";
                    var extractPayload = new IrExtractVariantData(tempName, varRef, variant.Name, payloadIdx, payloadType);
                    _currentBlock!.AddInstruction(extractPayload);

                    var tempLocal = new IrLocalVariable(tempName, payloadType, isMutable: true);
                    _currentFunction!.LocalVariables.Add(tempLocal);
                    _localVariables[tempName] = tempLocal;

                    EmitImmediateEnumDrop(tempName, payloadEnumType);
                }
            }

            // Jump to end
            _currentBlock!.AddInstruction(new IrBranch(endLabel));

            // Skip label
            _currentBlock!.AddInstruction(new IrLabel(skipLabel));
        }

        // End label
        _currentBlock!.AddInstruction(new IrLabel(endLabel));
    }
}
