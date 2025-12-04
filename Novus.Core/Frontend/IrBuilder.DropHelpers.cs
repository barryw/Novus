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

        // Get the type name for method lookup
        string typeName;
        string baseTypeName;  // Base name for template lookup
        IrStructType? structType = null;
        IrEnumType? enumType = null;

        if (type is IrStructType st)
        {
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
            // Only structs and enums can have methods
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
                    // TODO: Add support for enum methods if needed
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

        // Generate call to var.drop()
        // This desugars to: Type_drop(&mut var)
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
            // TODO: Pass context parameter to get accurate source location
            var errorLocation = _currentStatementLocation ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Cannot generate drop call for type '{type.Name}'",
                errorLocation
            );
            return;
        }

        // The Drop trait implementation generates: Type_Drop_drop
        // (trait impl convention: {Type}_{Trait}_{method})
        // For monomorphized types like Vec<bool>, this would be Vec<bool>_Drop_drop
        var dropMethodName = $"{typeName}_Drop_drop";
        var dropMethod = _module.GetFunction(dropMethodName);
        if (dropMethod == null)
        {
            // This should never happen if EnsureDropMethodInstantiated was called first
            // If it does happen, it means there's a bug in the Drop detection logic
            // TODO: Pass context parameter to get accurate source location
            var errorLocation = _currentStatementLocation ?? new SourceLocation(_inputFilePath ?? "unknown", 0, 0, 0, "");
            _diagnostics.ReportError(
                ErrorCodes.MethodNotFound,
                $"Drop method '{dropMethodName}' not found (this should have been instantiated already)",
                errorLocation
            );
            return;
        }

        // Load the variable and borrow it mutably for drop()
        var varRef = new IrVariable(varName, type);
        var mutBorrow = new IrBorrowValue(varRef, new IrMutReferenceType(type), isMutable: true);

        // Create the drop() call (drop() returns void)
        var dropCall = new IrCall(dropMethodName, IrVoidType.Instance, null);
        dropCall.Arguments.Add(mutBorrow);
        deferBlock.AddInstruction(dropCall);

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
}
