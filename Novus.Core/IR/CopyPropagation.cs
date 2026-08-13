namespace Novus.IR;

/// <summary>
/// Copy Propagation optimization pass.
/// Replaces uses of copied variables with their source values.
///
/// In SSA form, this eliminates redundant copy operations by propagating
/// the source directly to all uses of the copy.
///
/// Algorithm:
/// 1. Find all copy operations (x = y where y is a variable)
/// 2. Replace all uses of x with y
/// 3. Dead code elimination can later remove the unused copy instruction
///
/// Example:
///   x_0 = 5
///   y_0 = x_0        // Copy
///   z_0 = y_0 + 3    // Uses y_0
///
/// After copy propagation:
///   x_0 = 5
///   y_0 = x_0        // Will be removed by DCE if y_0 is not used elsewhere
///   z_0 = x_0 + 3    // Now uses x_0 directly
///
/// Benefits:
/// - Fewer temporary variables
/// - Better register allocation
/// - Enables other optimizations
/// - Combined with DCE, eliminates the copy instruction entirely
/// </summary>
public class CopyPropagation(IrFunction function)
{
    /// <summary>
    /// Map from copy destination to its source value
    /// Only contains simple copies (variable = variable)
    /// </summary>
    private Dictionary<string, IrVariable> _copies = new();

    /// <summary>
    /// Track which instructions were modified (for iteration)
    /// </summary>
    private bool _madeChanges = false;

    /// <summary>
    /// Run copy propagation until no more changes can be made
    /// Returns the number of substitutions performed
    /// </summary>
    public int Propagate()
    {
        int totalSubstitutions = 0;

        // Iterate until no more changes
        do
        {
            _madeChanges = false;
            _copies.Clear();

            // Pass 1: Identify copy operations
            IdentifyCopies();

            // Pass 2: Propagate copies to uses
            int substitutions = PropagateCopies();
            totalSubstitutions += substitutions;

        } while (_madeChanges);

        return totalSubstitutions;
    }

    /// <summary>
    /// Identify all copy operations (x = y where y is a variable)
    /// </summary>
    private void IdentifyCopies()
    {
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case IrLocalDecl decl when decl.InitialValue is IrVariable sourceVar:
                        _copies[decl.Name] = sourceVar;
                        break;

                    case IrStore store when store.Value is IrVariable sourceVar:
                        _copies[store.VariableName] = sourceVar;
                        break;

                    // Phi functions can also represent copies in some cases
                    // but we skip them for now as they're more complex
                }
            }
        }
    }

    /// <summary>
    /// Propagate copies throughout the function
    /// </summary>
    private int PropagateCopies()
    {
        int substitutionCount = 0;

        foreach (var block in function.BasicBlocks)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];
                var modified = PropagateInInstruction(instruction);
                if (modified != null)
                {
                    block.Instructions[i] = modified;
                    substitutionCount++;
                    _madeChanges = true;
                }
            }

            // Also propagate in phi functions
            for (int i = 0; i < block.PhiFunctions.Count; i++)
            {
                var phi = block.PhiFunctions[i];
                bool changed = false;

                for (int j = 0; j < phi.IncomingValues.Count; j++)
                {
                    var newValue = PropagateInValue(phi.IncomingValues[j]);
                    if (newValue != phi.IncomingValues[j])
                    {
                        phi.IncomingValues[j] = newValue;
                        changed = true;
                    }
                }

                if (changed)
                {
                    substitutionCount++;
                    _madeChanges = true;
                }
            }
        }

        return substitutionCount;
    }

    /// <summary>
    /// Propagate copies in a single instruction
    /// Returns the modified instruction, or null if no changes
    /// </summary>
    private IrInstruction? PropagateInInstruction(IrInstruction instruction)
    {
        bool changed = false;

        switch (instruction)
        {
            case IrLocalDecl decl:
                {
                    if (decl.InitialValue != null)
                    {
                        var newValue = PropagateInValue(decl.InitialValue);
                        if (newValue != decl.InitialValue)
                        {
                            decl.InitialValue = newValue;
                            changed = true;
                        }
                    }
                    break;
                }

            case IrStore store:
                {
                    var newValue = PropagateInValue(store.Value);
                    if (newValue != store.Value)
                    {
                        store.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrBinaryOp binOp:
                {
                    var newLeft = PropagateInValue(binOp.Left);
                    var newRight = PropagateInValue(binOp.Right);

                    if (newLeft != binOp.Left || newRight != binOp.Right)
                    {
                        binOp.Left = newLeft;
                        binOp.Right = newRight;
                        changed = true;
                    }
                    break;
                }

            case IrReturn ret when ret.Value != null:
                {
                    var newValue = PropagateInValue(ret.Value);
                    if (newValue != ret.Value)
                    {
                        ret.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrConditionalBranch condBranch:
                {
                    var newCond = PropagateInValue(condBranch.Condition);
                    if (newCond != condBranch.Condition)
                    {
                        condBranch.Condition = newCond;
                        changed = true;
                    }
                    break;
                }

            case IrCall call:
                {
                    for (int i = 0; i < call.Arguments.Count; i++)
                    {
                        var newArg = PropagateInValue(call.Arguments[i]);
                        if (newArg != call.Arguments[i])
                        {
                            call.Arguments[i] = newArg;
                            changed = true;
                        }
                    }
                    break;
                }

            case IrIndirectCall indirectCall:
                {
                    var newFp = PropagateInValue(indirectCall.FunctionPointer);
                    if (newFp != indirectCall.FunctionPointer)
                    {
                        indirectCall.FunctionPointer = newFp;
                        changed = true;
                    }

                    for (int i = 0; i < indirectCall.Arguments.Count; i++)
                    {
                        var newArg = PropagateInValue(indirectCall.Arguments[i]);
                        if (newArg != indirectCall.Arguments[i])
                        {
                            indirectCall.Arguments[i] = newArg;
                            changed = true;
                        }
                    }
                    break;
                }

            case IrIndexAccess indexAccess:
                {
                    var newArray = PropagateInValue(indexAccess.Array);
                    var newIndex = PropagateInValue(indexAccess.Index);
                    var newLength = indexAccess.Length == null ? null : PropagateInValue(indexAccess.Length);
                    if (newArray != indexAccess.Array || newIndex != indexAccess.Index || newLength != indexAccess.Length)
                    {
                        indexAccess.Array = newArray;
                        indexAccess.Index = newIndex;
                        indexAccess.Length = newLength;
                        changed = true;
                    }
                    break;
                }

            case IrSliceBoundsCheck sliceCheck:
                {
                    var start = PropagateInValue(sliceCheck.Start);
                    var end = PropagateInValue(sliceCheck.End);
                    var length = PropagateInValue(sliceCheck.Length);
                    if (start != sliceCheck.Start || end != sliceCheck.End || length != sliceCheck.Length)
                    {
                        sliceCheck.Start = start;
                        sliceCheck.End = end;
                        sliceCheck.Length = length;
                        changed = true;
                    }
                    break;
                }

            case IrMemberAccess memberAccess:
                {
                    var newStruct = PropagateInValue(memberAccess.Struct);
                    if (newStruct != memberAccess.Struct)
                    {
                        memberAccess.Struct = newStruct;
                        changed = true;
                    }
                    break;
                }

            case IrMemberStore memberStore:
                {
                    var newStruct = PropagateInValue(memberStore.Struct);
                    var newValue = PropagateInValue(memberStore.Value);
                    if (newStruct != memberStore.Struct || newValue != memberStore.Value)
                    {
                        memberStore.Struct = newStruct;
                        memberStore.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrIndexStore indexStore:
                {
                    var newArray = PropagateInValue(indexStore.Array);
                    var newIndex = PropagateInValue(indexStore.Index);
                    var newValue = PropagateInValue(indexStore.Value);
                    var newLength = indexStore.Length == null ? null : PropagateInValue(indexStore.Length);
                    if (newArray != indexStore.Array || newIndex != indexStore.Index || newValue != indexStore.Value || newLength != indexStore.Length)
                    {
                        indexStore.Array = newArray;
                        indexStore.Index = newIndex;
                        indexStore.Value = newValue;
                        indexStore.Length = newLength;
                        changed = true;
                    }
                    break;
                }

            case IrDereferenceStore derefStore:
                {
                    var newPointer = PropagateInValue(derefStore.Pointer);
                    var newValue = PropagateInValue(derefStore.Value);
                    if (newPointer != derefStore.Pointer || newValue != derefStore.Value)
                    {
                        derefStore.Pointer = newPointer;
                        derefStore.Value = newValue;
                        changed = true;
                    }
                    break;
                }

            case IrAssert assert:
                {
                    var newCond = PropagateInValue(assert.Condition);
                    if (newCond != assert.Condition)
                    {
                        assert.Condition = newCond;
                        changed = true;
                    }
                    break;
                }

            case IrExtractTag extractTag:
                {
                    var newEnum = PropagateInValue(extractTag.EnumValue);
                    if (newEnum != extractTag.EnumValue)
                    {
                        extractTag.EnumValue = newEnum;
                        changed = true;
                    }
                    break;
                }

            case IrExtractVariantData extractData:
                {
                    var newEnum = PropagateInValue(extractData.EnumValue);
                    if (newEnum != extractData.EnumValue)
                    {
                        extractData.EnumValue = newEnum;
                        changed = true;
                    }
                    break;
                }
        }

        return changed ? instruction : null;
    }

    /// <summary>
    /// Propagate copies in a value.
    /// Follows chains of copies transitively (x = y, y = z => use z)
    /// </summary>
    private IrValue PropagateInValue(IrValue value)
    {
        if (value is IrVariable variable)
        {
            var varName = variable.SsaName;

            // Follow chains of copies: if x = y and y = z, use z
            var visited = new HashSet<string>();
            while (_copies.TryGetValue(varName, out var sourceVar))
            {
                // Detect cycles (shouldn't happen in valid SSA, but be defensive)
                if (!visited.Add(varName))
                    break;

                varName = sourceVar.SsaName;
                variable = sourceVar;
            }

            return variable;
        }
        return value;
    }
}
