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

        // Get the type name for method lookup
        string typeName;
        string baseTypeName;  // Base name for template lookup
        IrStructType? structType = null;
        IrEnumType? enumType = null;

        if (type is IrStructType st)
        {
            // Skip if this is a generic template (has unsubstituted generic parameters)
            // We can only instantiate Drop for concrete types
            if (st.GenericParameters.Count > 0)
            {
                return false;
            }

            structType = st;
            baseTypeName = st.StructName;  // Base name for template lookup (e.g., "Vec")
            // For monomorphized types, use CacheKey (e.g., "Vec<bool>")
            // For non-generic types, use StructName
            typeName = st.CacheKey ?? st.StructName;
        }
        else if (type is IrEnumType et)
        {
            enumType = et;
            baseTypeName = et.EnumName;
            typeName = et.EnumName;
        }
        else
        {
            // Only structs, enums, and tuples can have Drop
            return false;
        }

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
                IrFunction? instantiatedFunc = null;

                if (structType != null)
                {
                    // Pass isTraitImpl=true and traitName="Drop" for proper mangling
                    instantiatedFunc = _genericInstantiator.InstantiateStructMethod(
                        structType,
                        "drop",
                        isTraitImpl: true,
                        traitName: "Drop",
                        traitTypeArgs: new List<IrType>()
                    );
                }
                else if (enumType != null)
                {
                    // Enum Drop methods are not currently supported.
                    // Enums are Copy types by default, and complex enum payloads
                    // would need explicit Drop implementation on contained types.
                    return false;
                }

                if (instantiatedFunc != null)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Log the error for debugging
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
        else
        {
            // Generate call to var.drop()
            // This desugars to: Type_drop(&var var)
            string typeName;
            if (type is IrStructType structType)
            {
                // For monomorphized types, use CacheKey (e.g., "Vec<bool>")
                // For non-generic types, use StructName
                typeName = structType.CacheKey ?? structType.StructName;
            }
            else if (type is IrEnumType enumType)
            {
                typeName = enumType.EnumName;
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

            // The Drop trait implementation generates: Type_Drop_drop
            // (trait impl convention: {Type}_{Trait}_{method})
            // For monomorphized types like Vec<bool>, this would be Vec<bool>_Drop_drop
            var dropMethodName = $"{typeName}_Drop_drop";
            var dropMethod = _module.GetFunction(dropMethodName);
            if (dropMethod == null)
            {
                // This should never happen if EnsureDropMethodInstantiated was called first.
                // If it does happen, it means there's a bug in the Drop detection logic.
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
    /// </summary>
    private void InjectNestedTupleElementDrops(IrBasicBlock deferBlock, string parentTupleVar, int elementIndex, IrTupleType nestedTupleType)
    {
        // For nested tuples, we access the parent tuple's element and then drop each of its elements
        // that implement Drop, in reverse order
        for (int i = nestedTupleType.ElementTypes.Count - 1; i >= 0; i--)
        {
            var elementType = nestedTupleType.ElementTypes[i];

            if (!_module.TypeImplementsDrop(elementType))
            {
                continue;
            }

            // Handle deeply nested tuples recursively
            if (elementType is IrTupleType deeplyNestedTuple)
            {
                // This gets complex - for deeply nested tuples we'd need to chain the accesses
                // For now, create a synthetic access chain
                // Access: parentTuple.__elementIndex.__i
                var parentTupleVar_ = new IrVariable(parentTupleVar,
                    new IrTupleType(new List<IrType> { nestedTupleType })); // Approximate parent type
                var nestedAccess = new IrTupleElementAccess(parentTupleVar_, elementIndex, nestedTupleType);

                // Now recursively drop the deeply nested tuple's elements
                // This is getting complex - for now just log and skip deeply nested tuples
                // TODO: Full support for arbitrarily nested tuples
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

            // Access chain: parentTuple.__elementIndex (to get the nested tuple)
            // Then: nestedTuple.__i (to get the element)
            var parentTupleVarRef = new IrVariable(parentTupleVar,
                new IrTupleType(nestedTupleType.ElementTypes)); // Use the actual parent type

            // First access the nested tuple element from parent
            var nestedTupleAccess = new IrTupleElementAccess(parentTupleVarRef, elementIndex, nestedTupleType);

            // Then access the element within the nested tuple
            var innerElementAccess = new IrTupleElementAccess(nestedTupleAccess, i, elementType);

            // Borrow the inner element mutably for drop()
            var elementBorrow = new IrBorrowValue(innerElementAccess, new IrMutReferenceType(elementType), isMutable: true);

            // Create the drop() call
            var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
            dropCall.Arguments.Add(elementBorrow);
            deferBlock.AddInstruction(dropCall);
        }
    }
}
