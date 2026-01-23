namespace Novus.IR;

/// <summary>
/// Dead Store Elimination (DSE) optimization pass.
/// Removes stores to variables that are overwritten before being read.
///
/// Unlike Dead Code Elimination (DCE), which removes instructions whose
/// results are never used, DSE specifically targets store operations that
/// are "dead" because:
/// 1. The variable is overwritten by another store before any read
/// 2. The variable goes out of scope without being read
/// 3. The store initializes a variable that is immediately reassigned
///
/// This pass is particularly effective at removing:
/// - Redundant initialization (e.g., `let x = 0; x = compute();`)
/// - Zeroing of moved values that are never read again
/// - Loop initialization that's immediately overwritten
///
/// The pass works on SSA form, where each definition is unique.
/// </summary>
public class DeadStoreElimination(IrFunction function)
{
    /// <summary>
    /// Maps variable names to the store instruction that defines them
    /// </summary>
    private readonly Dictionary<string, IrInstruction> _definitions = new();

    /// <summary>
    /// Set of variables that have been read (used)
    /// </summary>
    private readonly HashSet<string> _usedVariables = new();

    /// <summary>
    /// Set of stores that are dead (can be removed)
    /// </summary>
    private readonly HashSet<IrInstruction> _deadStores = new();

    /// <summary>
    /// Track stores within each basic block for local analysis
    /// </summary>
    private readonly Dictionary<IrBasicBlock, Dictionary<string, IrInstruction>> _blockStores = new();

    /// <summary>
    /// Run the dead store elimination pass.
    /// Returns the number of stores eliminated.
    /// </summary>
    public int Eliminate()
    {
        // Phase 1: Collect all definitions and uses
        CollectDefinitionsAndUses();

        // Phase 2: Find stores that are overwritten within the same block
        FindLocallyDeadStores();

        // Phase 3: Find stores to variables that are never read
        FindGloballyDeadStores();

        // Phase 4: Remove dead stores
        return RemoveDeadStores();
    }

    /// <summary>
    /// Collect all variable definitions and uses across the function.
    /// </summary>
    private void CollectDefinitionsAndUses()
    {
        _definitions.Clear();
        _usedVariables.Clear();

        foreach (var block in function.BasicBlocks)
        {
            // Phi functions define and use variables
            foreach (var phi in block.PhiFunctions)
            {
                if (phi.Destination != null)
                {
                    _definitions[phi.Destination.SsaName] = phi;
                }

                foreach (var value in phi.IncomingValues)
                {
                    CollectUsedVariables(value);
                }
            }

            foreach (var instruction in block.Instructions)
            {
                // Track definitions
                var definedVar = GetDefinedVariable(instruction);
                if (definedVar != null)
                {
                    _definitions[definedVar] = instruction;
                }

                // Track uses
                CollectUsedVariablesFromInstruction(instruction);
            }
        }
    }

    /// <summary>
    /// Find stores within a block that are overwritten before any read.
    /// This handles patterns like:
    ///   x = 0
    ///   x = compute()  // First store is dead
    /// </summary>
    private void FindLocallyDeadStores()
    {
        _blockStores.Clear();

        foreach (var block in function.BasicBlocks)
        {
            var blockStoreMap = new Dictionary<string, IrInstruction>();

            foreach (var instruction in block.Instructions)
            {
                // First, check if this instruction READS any variables
                var usedVars = GetVariablesUsedByInstruction(instruction);
                foreach (var usedVar in usedVars)
                {
                    // This variable is read, so remove it from pending stores
                    // (the store is NOT dead)
                    blockStoreMap.Remove(usedVar);
                }

                // Then, check if this instruction STORES to a variable
                var storedVar = GetStoredVariable(instruction);
                if (storedVar != null)
                {
                    // If there's an existing store to this variable that wasn't read,
                    // mark it as dead
                    if (blockStoreMap.TryGetValue(storedVar, out var previousStore))
                    {
                        // Previous store is dead - it's overwritten without being read
                        _deadStores.Add(previousStore);
                    }

                    // Track this store as the current definition
                    blockStoreMap[storedVar] = instruction;
                }
            }

            _blockStores[block] = blockStoreMap;
        }
    }

    /// <summary>
    /// Find stores to variables that are never read anywhere in the function.
    /// </summary>
    private void FindGloballyDeadStores()
    {
        foreach (var (varName, definition) in _definitions)
        {
            // Skip if this definition has side effects
            if (HasSideEffects(definition))
                continue;

            // If the variable is never used, the store is dead
            if (!_usedVariables.Contains(varName))
            {
                // Mark as dead unless it's already marked (from local analysis)
                _deadStores.Add(definition);
            }
        }
    }

    /// <summary>
    /// Remove all dead stores from the function.
    /// Returns the number of stores removed.
    /// </summary>
    private int RemoveDeadStores()
    {
        int removedCount = 0;

        foreach (var block in function.BasicBlocks)
        {
            // Remove dead phi functions
            var livePhis = block.PhiFunctions.Where(phi => !_deadStores.Contains(phi)).ToList();
            removedCount += block.PhiFunctions.Count - livePhis.Count;
            block.PhiFunctions.Clear();
            foreach (var phi in livePhis)
            {
                block.PhiFunctions.Add(phi);
            }

            // Remove dead instructions
            var liveInstructions = block.Instructions.Where(instr => !_deadStores.Contains(instr)).ToList();
            removedCount += block.Instructions.Count - liveInstructions.Count;
            block.Instructions.Clear();
            foreach (var instr in liveInstructions)
            {
                block.Instructions.Add(instr);
            }
        }

        return removedCount;
    }

    /// <summary>
    /// Get the variable name defined by an instruction (if it's a store-like operation).
    /// </summary>
    private string? GetDefinedVariable(IrInstruction instruction)
    {
        return instruction switch
        {
            IrLocalDecl decl => decl.Name,
            IrStore store => store.VariableName,
            IrBinaryOp binOp => binOp.ResultName,
            IrCall call => call.ResultName,
            IrIndexAccess indexAccess => indexAccess.ResultName,
            IrMemberAccess memberAccess => memberAccess.ResultName,
            IrExtractTag extractTag => extractTag.ResultName,
            IrExtractVariantData extractData => extractData.ResultName,
            IrCreateClosure createClosure => createClosure.ResultName,
            IrLoadCapture loadCapture => loadCapture.ResultName,
            _ => null
        };
    }

    /// <summary>
    /// Get the variable being stored to (for local dead store analysis).
    /// This is more specific than GetDefinedVariable - it only returns
    /// variables being stored to, not computed results.
    /// </summary>
    private string? GetStoredVariable(IrInstruction instruction)
    {
        return instruction switch
        {
            IrLocalDecl decl => decl.Name,
            IrStore store => store.VariableName,
            _ => null
        };
    }

    /// <summary>
    /// Collect variables used by a value expression.
    /// </summary>
    private void CollectUsedVariables(IrValue value)
    {
        switch (value)
        {
            case IrVariable variable:
                _usedVariables.Add(variable.SsaName);
                break;

            case IrBorrowValue borrow:
                CollectUsedVariables(borrow.BorrowedValue);
                break;

            case IrDereferenceValue deref:
                CollectUsedVariables(deref.PointerValue);
                break;

            case IrCastValue cast:
                CollectUsedVariables(cast.Value);
                break;

            case IrFieldReference fieldRef:
                CollectUsedVariables(fieldRef.Struct);
                break;

            case IrStructLiteral structLit:
                foreach (var (_, fieldValue) in structLit.FieldValues)
                {
                    CollectUsedVariables(fieldValue);
                }
                break;

            case IrTupleLiteral tupleLit:
                foreach (var element in tupleLit.Elements)
                {
                    CollectUsedVariables(element);
                }
                break;

            case IrArrayLiteral arrayLit:
                foreach (var element in arrayLit.Elements)
                {
                    CollectUsedVariables(element);
                }
                break;

            // IrEnumConstructor is a simple tag constructor with no captured values
            // IrEnumValue is the one that holds variant data values
        }
    }

    /// <summary>
    /// Collect variables used by an instruction.
    /// </summary>
    private void CollectUsedVariablesFromInstruction(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrBinaryOp binOp:
                CollectUsedVariables(binOp.Left);
                CollectUsedVariables(binOp.Right);
                break;

            case IrStore store:
                CollectUsedVariables(store.Value);
                break;

            case IrReturn ret:
                if (ret.Value != null)
                    CollectUsedVariables(ret.Value);
                break;

            case IrConditionalBranch condBranch:
                CollectUsedVariables(condBranch.Condition);
                break;

            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    CollectUsedVariables(arg);
                }
                break;

            case IrIndirectCall indirectCall:
                CollectUsedVariables(indirectCall.FunctionPointer);
                foreach (var arg in indirectCall.Arguments)
                {
                    CollectUsedVariables(arg);
                }
                break;

            case IrLocalDecl decl:
                if (decl.InitialValue != null)
                {
                    CollectUsedVariables(decl.InitialValue);
                }
                break;

            case IrIndexAccess indexAccess:
                CollectUsedVariables(indexAccess.Array);
                CollectUsedVariables(indexAccess.Index);
                break;

            case IrMemberAccess memberAccess:
                CollectUsedVariables(memberAccess.Struct);
                break;

            case IrMemberStore memberStore:
                CollectUsedVariables(memberStore.Struct);
                CollectUsedVariables(memberStore.Value);
                break;

            case IrIndexStore indexStore:
                CollectUsedVariables(indexStore.Array);
                CollectUsedVariables(indexStore.Index);
                CollectUsedVariables(indexStore.Value);
                break;

            case IrDereferenceStore derefStore:
                CollectUsedVariables(derefStore.Pointer);
                CollectUsedVariables(derefStore.Value);
                break;

            case IrAssert assert:
                CollectUsedVariables(assert.Condition);
                break;

            case IrExtractTag extractTag:
                CollectUsedVariables(extractTag.EnumValue);
                break;

            case IrExtractVariantData extractData:
                CollectUsedVariables(extractData.EnumValue);
                break;

            case IrMatch match:
                CollectUsedVariables(match.MatchValue);
                break;

            case IrCreateClosure createClosure:
                foreach (var (_, captureValue, _) in createClosure.CapturedValues)
                {
                    CollectUsedVariables(captureValue);
                }
                break;

            case IrInvokeClosure invokeClosure:
                CollectUsedVariables(invokeClosure.Closure);
                foreach (var arg in invokeClosure.Arguments)
                {
                    CollectUsedVariables(arg);
                }
                break;

            case IrStoreCapture storeCapture:
                CollectUsedVariables(storeCapture.Value);
                break;
        }
    }

    /// <summary>
    /// Get variables used by an instruction (for local dead store analysis).
    /// Returns the SSA names of variables read by this instruction.
    /// </summary>
    private HashSet<string> GetVariablesUsedByInstruction(IrInstruction instruction)
    {
        var used = new HashSet<string>();

        void Collect(IrValue value)
        {
            if (value is IrVariable variable)
            {
                used.Add(variable.SsaName);
            }
            else if (value is IrBorrowValue borrow)
            {
                Collect(borrow.BorrowedValue);
            }
            else if (value is IrDereferenceValue deref)
            {
                Collect(deref.PointerValue);
            }
            else if (value is IrCastValue cast)
            {
                Collect(cast.Value);
            }
            else if (value is IrFieldReference fieldRef)
            {
                Collect(fieldRef.Struct);
            }
            else if (value is IrStructLiteral structLit)
            {
                foreach (var (_, fieldValue) in structLit.FieldValues)
                {
                    Collect(fieldValue);
                }
            }
            else if (value is IrTupleLiteral tupleLit)
            {
                foreach (var element in tupleLit.Elements)
                {
                    Collect(element);
                }
            }
            else if (value is IrArrayLiteral arrayLit)
            {
                foreach (var element in arrayLit.Elements)
                {
                    Collect(element);
                }
            }
            // IrEnumConstructor is a simple tag constructor with no captured values
        }

        switch (instruction)
        {
            case IrBinaryOp binOp:
                Collect(binOp.Left);
                Collect(binOp.Right);
                break;

            case IrStore store:
                Collect(store.Value);
                break;

            case IrReturn ret:
                if (ret.Value != null)
                    Collect(ret.Value);
                break;

            case IrConditionalBranch condBranch:
                Collect(condBranch.Condition);
                break;

            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    Collect(arg);
                }
                break;

            case IrIndirectCall indirectCall:
                Collect(indirectCall.FunctionPointer);
                foreach (var arg in indirectCall.Arguments)
                {
                    Collect(arg);
                }
                break;

            case IrLocalDecl decl:
                if (decl.InitialValue != null)
                {
                    Collect(decl.InitialValue);
                }
                break;

            case IrIndexAccess indexAccess:
                Collect(indexAccess.Array);
                Collect(indexAccess.Index);
                break;

            case IrMemberAccess memberAccess:
                Collect(memberAccess.Struct);
                break;

            case IrMemberStore memberStore:
                Collect(memberStore.Struct);
                Collect(memberStore.Value);
                break;

            case IrIndexStore indexStore:
                Collect(indexStore.Array);
                Collect(indexStore.Index);
                Collect(indexStore.Value);
                break;

            case IrDereferenceStore derefStore:
                Collect(derefStore.Pointer);
                Collect(derefStore.Value);
                break;

            case IrAssert assert:
                Collect(assert.Condition);
                break;

            case IrExtractTag extractTag:
                Collect(extractTag.EnumValue);
                break;

            case IrExtractVariantData extractData:
                Collect(extractData.EnumValue);
                break;

            case IrMatch match:
                Collect(match.MatchValue);
                break;

            case IrCreateClosure createClosure:
                foreach (var (_, captureValue, _) in createClosure.CapturedValues)
                {
                    Collect(captureValue);
                }
                break;

            case IrInvokeClosure invokeClosure:
                Collect(invokeClosure.Closure);
                foreach (var arg in invokeClosure.Arguments)
                {
                    Collect(arg);
                }
                break;

            case IrStoreCapture storeCapture:
                Collect(storeCapture.Value);
                break;
        }

        return used;
    }

    /// <summary>
    /// Check if an instruction has side effects (must be kept even if result unused).
    /// </summary>
    private bool HasSideEffects(IrInstruction instruction)
    {
        return instruction switch
        {
            // Control flow
            IrReturn _ => true,
            IrBranch _ => true,
            IrConditionalBranch _ => true,
            IrLabel _ => true,

            // Calls (may have side effects)
            IrCall _ => true,
            IrIndirectCall _ => true,
            IrInvokeClosure _ => true,

            // Memory operations to external memory
            IrDereferenceStore _ => true,
            IrMemberStore _ => true,
            IrIndexStore _ => true,
            IrIndexedFieldStore _ => true,
            IrStoreCapture _ => true,

            // Hardware operations
            IrHardwareWrite _ => true,
            IrHardwareRead _ => true,
            IrInlineAsm _ => true,

            // Assertions and panics
            IrAssert _ => true,
            IrPanic _ => true,

            // Defer
            IrDefer _ => true,

            // Pure operations (no side effects)
            IrLocalDecl _ => false,
            IrStore _ => false,  // Store to local variable in SSA
            IrBinaryOp _ => false,
            IrIndexAccess _ => false,
            IrMemberAccess _ => false,
            IrExtractTag _ => false,
            IrExtractVariantData _ => false,
            IrPhi _ => false,
            IrCreateClosure _ => false,
            IrLoadCapture _ => false,

            // Conservative: unknown instructions have side effects
            _ => true
        };
    }

    /// <summary>
    /// Get a list of dead stores without removing them (for diagnostics).
    /// </summary>
    public List<IrInstruction> GetDeadStores()
    {
        CollectDefinitionsAndUses();
        FindLocallyDeadStores();
        FindGloballyDeadStores();
        return _deadStores.ToList();
    }
}
