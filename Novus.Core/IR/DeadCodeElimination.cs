namespace Novus.IR;

/// <summary>
/// Dead Code Elimination (DCE) optimization pass
/// Removes instructions whose results are never used
///
/// This pass is most effective on SSA form where:
/// - Every variable is defined exactly once
/// - It's trivial to find all uses of a definition
/// - We can easily track which definitions have zero uses
/// </summary>
public class DeadCodeElimination(IrFunction function)
{
    /// <summary>
    /// Set of instructions that have been marked as dead
    /// </summary>
    private HashSet<IrInstruction> _deadInstructions = new();

    /// <summary>
    /// Set of instructions that are known to be live (have side effects or are used)
    /// </summary>
    private HashSet<IrInstruction> _liveInstructions = new();

    /// <summary>
    /// Map from variable names to the instruction that defines them
    /// </summary>
    private Dictionary<string, IrInstruction> _definitions = new();

    /// <summary>
    /// Run the dead code elimination pass
    /// Returns the number of instructions eliminated
    /// </summary>
    public int Eliminate()
    {
        // Step 1: Build definition map and identify critical instructions
        BuildDefinitionMap();

        // Step 2: Mark live instructions (worklist algorithm)
        MarkLiveInstructions();

        // Step 3: Remove dead instructions
        return RemoveDeadInstructions();
    }

    /// <summary>
    /// Build a map from variable names to their defining instructions
    /// Also identify instructions that are always live (have side effects)
    /// </summary>
    private void BuildDefinitionMap()
    {
        _definitions.Clear();
        _liveInstructions.Clear();

        foreach (var block in function.BasicBlocks)
        {
            // Phi functions can define variables
            foreach (var phi in block.PhiFunctions)
            {
                if (phi.Destination != null)
                {
                    var varName = phi.Destination.SsaName;
                    _definitions[varName] = phi;
                }
            }

            foreach (var instruction in block.Instructions)
            {
                // Instructions that always have side effects are always live
                if (HasSideEffects(instruction))
                {
                    _liveInstructions.Add(instruction);
                }

                // Track definitions
                var definedVar = GetDefinedVariable(instruction);
                if (definedVar != null)
                {
                    _definitions[definedVar] = instruction;
                }
            }
        }
    }

    /// <summary>
    /// Check if an instruction has side effects and must be kept
    /// </summary>
    private bool HasSideEffects(IrInstruction instruction)
    {
        return instruction switch
        {
            // Control flow instructions are always live
            IrReturn _ => true,
            IrBranch _ => true,
            IrConditionalBranch _ => true,
            IrLabel _ => true,

            // Calls may have side effects (unless proven pure)
            IrCall _ => true,
            IrIndirectCall _ => true,

            // Memory operations have side effects
            // Note: IrStore to local variables is treated as pure (no side effects)
            // since it's just defining a new SSA version. Actual memory operations
            // like IrDereferenceStore, IrMemberStore, IrIndexStore DO have side effects.
            IrStore _ => false,  // Local variable assignment in SSA form
            IrDereferenceStore _ => true,
            IrMemberStore _ => true,
            IrIndexStore _ => true,

            // Assertions and panics have side effects
            IrAssert _ => true,
            IrPanic _ => true,

            // Defer has side effects
            IrDefer _ => true,

            // Pure operations (no side effects)
            IrLocalDecl _ => false,
            IrBinaryOp _ => false,
            IrIndexAccess _ => false,
            IrMemberAccess _ => false,
            IrExtractTag _ => false,
            IrExtractVariantData _ => false,
            IrPhi _ => false,

            // Conservative: unknown instructions are assumed to have side effects
            _ => true
        };
    }

    /// <summary>
    /// Get the variable name defined by an instruction (if any)
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
            _ => null
        };
    }

    /// <summary>
    /// Mark instructions as live using a worklist algorithm
    /// An instruction is live if:
    /// 1. It has side effects, OR
    /// 2. It defines a variable that is used by a live instruction
    /// </summary>
    private void MarkLiveInstructions()
    {
        var worklist = new Queue<IrInstruction>(_liveInstructions);

        while (worklist.Count > 0)
        {
            var instruction = worklist.Dequeue();

            // Find all variables used by this instruction
            var usedVars = GetUsedVariables(instruction);

            // Mark the definitions of those variables as live
            foreach (var varName in usedVars)
            {
                if (_definitions.TryGetValue(varName, out var definingInstr))
                {
                    // If not already marked live, mark it and add to worklist
                    if (_liveInstructions.Add(definingInstr))
                    {
                        worklist.Enqueue(definingInstr);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get all variables used by an instruction
    /// </summary>
    private HashSet<string> GetUsedVariables(IrInstruction instruction)
    {
        var used = new HashSet<string>();

        switch (instruction)
        {
            case IrPhi phi:
                foreach (var value in phi.IncomingValues)
                {
                    CollectVariablesFromValue(value, used);
                }
                break;

            case IrBinaryOp binOp:
                CollectVariablesFromValue(binOp.Left, used);
                CollectVariablesFromValue(binOp.Right, used);
                break;

            case IrStore store:
                CollectVariablesFromValue(store.Value, used);
                break;

            case IrReturn ret:
                if (ret.Value != null)
                    CollectVariablesFromValue(ret.Value, used);
                break;

            case IrConditionalBranch condBranch:
                CollectVariablesFromValue(condBranch.Condition, used);
                break;

            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    CollectVariablesFromValue(arg, used);
                }
                break;

            case IrIndirectCall indirectCall:
                CollectVariablesFromValue(indirectCall.FunctionPointer, used);
                foreach (var arg in indirectCall.Arguments)
                {
                    CollectVariablesFromValue(arg, used);
                }
                break;

            case IrLocalDecl decl:
                if (decl.InitialValue != null)
                {
                    CollectVariablesFromValue(decl.InitialValue, used);
                }
                break;

            case IrIndexAccess indexAccess:
                CollectVariablesFromValue(indexAccess.Array, used);
                CollectVariablesFromValue(indexAccess.Index, used);
                break;

            case IrMemberAccess memberAccess:
                CollectVariablesFromValue(memberAccess.Struct, used);
                break;

            case IrMemberStore memberStore:
                CollectVariablesFromValue(memberStore.Struct, used);
                CollectVariablesFromValue(memberStore.Value, used);
                break;

            case IrIndexStore indexStore:
                CollectVariablesFromValue(indexStore.Array, used);
                CollectVariablesFromValue(indexStore.Index, used);
                CollectVariablesFromValue(indexStore.Value, used);
                break;

            case IrDereferenceStore derefStore:
                CollectVariablesFromValue(derefStore.Pointer, used);
                CollectVariablesFromValue(derefStore.Value, used);
                break;

            case IrAssert assert:
                CollectVariablesFromValue(assert.Condition, used);
                break;

            case IrExtractTag extractTag:
                CollectVariablesFromValue(extractTag.EnumValue, used);
                break;

            case IrExtractVariantData extractData:
                CollectVariablesFromValue(extractData.EnumValue, used);
                break;
        }

        return used;
    }

    /// <summary>
    /// Collect variable names from a value expression
    /// </summary>
    private void CollectVariablesFromValue(IrValue value, HashSet<string> variables)
    {
        if (value is IrVariable variable)
        {
            variables.Add(variable.SsaName);
        }
    }

    /// <summary>
    /// Remove all dead instructions from the function
    /// Returns the number of instructions removed
    /// </summary>
    private int RemoveDeadInstructions()
    {
        int removedCount = 0;

        foreach (var block in function.BasicBlocks)
        {
            // Remove dead phi functions
            var livePhis = block.PhiFunctions.Where(phi => _liveInstructions.Contains(phi)).ToList();
            removedCount += block.PhiFunctions.Count - livePhis.Count;
            block.PhiFunctions.Clear();
            foreach (var phi in livePhis)
            {
                block.PhiFunctions.Add(phi);
            }

            // Remove dead instructions
            var liveInstructions = block.Instructions.Where(instr => _liveInstructions.Contains(instr)).ToList();
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
    /// Get a list of all dead instructions (for reporting/diagnostics)
    /// </summary>
    public List<IrInstruction> GetDeadInstructions()
    {
        BuildDefinitionMap();
        MarkLiveInstructions();

        var deadInstructions = new List<IrInstruction>();

        foreach (var block in function.BasicBlocks)
        {
            foreach (var phi in block.PhiFunctions)
            {
                if (!_liveInstructions.Contains(phi))
                {
                    deadInstructions.Add(phi);
                }
            }

            foreach (var instruction in block.Instructions)
            {
                if (!_liveInstructions.Contains(instruction) && !HasSideEffects(instruction))
                {
                    deadInstructions.Add(instruction);
                }
            }
        }

        return deadInstructions;
    }
}
