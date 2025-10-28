using Novus.IR;

namespace Novus.Optimizer;

/// <summary>
/// Live range information for a variable
/// </summary>
public class LiveRange
{
    public string VariableName { get; set; }
    public int Start { get; set; } // Instruction index where variable is first defined
    public int End { get; set; }   // Instruction index where variable is last used
    public string? AssignedRegister { get; set; } // Physical register assigned (e.g., "d2", "a2")

    public LiveRange(string variableName, int start, int end)
    {
        VariableName = variableName;
        Start = start;
        End = end;
    }

    public bool OverlapsWith(LiveRange other)
    {
        return !(End < other.Start || other.End < Start);
    }
}

/// <summary>
/// Register allocation result for a function
/// </summary>
public class RegisterAllocation
{
    public Dictionary<string, string> VariableToRegister { get; } = new();
    public HashSet<string> SpilledVariables { get; } = new();
    public List<LiveRange> LiveRanges { get; } = new();

    public string? GetRegister(string variable)
    {
        return VariableToRegister.GetValueOrDefault(variable);
    }

    public bool IsSpilled(string variable)
    {
        return SpilledVariables.Contains(variable);
    }
}

/// <summary>
/// Basic register allocator using linear scan algorithm.
/// Allocates variables to physical registers when possible, spills to stack otherwise.
///
/// Available registers:
/// - Data registers: d2-d7 (d0-d1 reserved for temporaries/returns)
/// - Address registers: a2-a5 (a0-a1 reserved, a6=frame pointer, a7=stack pointer)
/// </summary>
public class RegisterAllocator
{
    private static readonly string[] DataRegisters = { "d2", "d3", "d4", "d5", "d6", "d7" };
    private static readonly string[] AddressRegisters = { "a2", "a3", "a4", "a5" };

    /// <summary>
    /// Perform register allocation on a function
    /// </summary>
    public RegisterAllocation AllocateRegisters(IrFunction function)
    {
        var allocation = new RegisterAllocation();

        // Build live ranges for all variables in the function
        var liveRanges = BuildLiveRanges(function);
        allocation.LiveRanges.AddRange(liveRanges);

        // Sort live ranges by start point (linear scan requirement)
        liveRanges.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Active intervals (currently allocated variables)
        var active = new List<LiveRange>();

        // Available registers
        var availableDataRegs = new Queue<string>(DataRegisters);
        var availableAddrRegs = new Queue<string>(AddressRegisters);

        foreach (var range in liveRanges)
        {
            // Expire old intervals (free up registers)
            ExpireOldIntervals(range, active, availableDataRegs, availableAddrRegs);

            // Determine if this variable needs a data or address register
            bool needsAddressReg = IsAddressVariable(range.VariableName);

            // Try to allocate a register
            string? reg = null;
            if (needsAddressReg && availableAddrRegs.Count > 0)
            {
                reg = availableAddrRegs.Dequeue();
            }
            else if (!needsAddressReg && availableDataRegs.Count > 0)
            {
                reg = availableDataRegs.Dequeue();
            }

            if (reg != null)
            {
                // Successfully allocated
                range.AssignedRegister = reg;
                allocation.VariableToRegister[range.VariableName] = reg;
                active.Add(range);
            }
            else
            {
                // Spill to stack
                allocation.SpilledVariables.Add(range.VariableName);
            }
        }

        return allocation;
    }

    /// <summary>
    /// Build live ranges for all variables in a function using CFG-based liveness analysis
    /// This properly handles control flow including loops and branches
    /// </summary>
    private List<LiveRange> BuildLiveRanges(IrFunction function)
    {
        if (function.BasicBlocks.Count == 0)
            return new List<LiveRange>();

        // Build CFG
        var cfg = new ControlFlowGraph(function);

        // Compute global liveness information using dataflow analysis
        var liveness = ComputeGlobalLiveness(cfg);

        // Map blocks and instructions to indices for live range computation
        var blockToStartIndex = new Dictionary<IrBasicBlock, int>();
        var blockToEndIndex = new Dictionary<IrBasicBlock, int>();
        int instructionIndex = 0;

        foreach (var block in function.BasicBlocks)
        {
            blockToStartIndex[block] = instructionIndex;
            instructionIndex += block.Instructions.Count;
            blockToEndIndex[block] = instructionIndex - 1;
        }

        // Build live ranges from liveness information with instruction-level precision
        var ranges = new Dictionary<string, LiveRange>();
        var blockDefs = new Dictionary<IrBasicBlock, HashSet<string>>();

        // First pass: collect all definitions per block
        foreach (var block in function.BasicBlocks)
        {
            var defs = new HashSet<string>();
            foreach (var instruction in block.Instructions)
            {
                var definedVar = GetDefinedVariable(instruction);
                if (definedVar != null) defs.Add(definedVar);
            }
            blockDefs[block] = defs;
        }

        instructionIndex = 0;

        foreach (var block in function.BasicBlocks)
        {
            var node = cfg.Nodes.FirstOrDefault(n => n.Block == block);
            if (node == null) continue;

            var liveInfo = liveness[node];

            // For variables live at block entry but NOT defined in this block,
            // they must come from a previous block or be parameters
            foreach (var varName in liveInfo.LiveIn)
            {
                if (!blockDefs[block].Contains(varName) && !ranges.ContainsKey(varName))
                {
                    // Variable live-in but not defined here - must be parameter or from earlier block
                    ranges[varName] = new LiveRange(varName, 0, blockToEndIndex[block]);
                }
                else if (ranges.ContainsKey(varName))
                {
                    // Variable from earlier block - extend its range
                    ranges[varName].End = Math.Max(ranges[varName].End, blockToEndIndex[block]);
                }
            }

            // Process instructions to track definitions and uses with instruction-level precision
            foreach (var instruction in block.Instructions)
            {
                // Track variable definitions first
                var definedVar = GetDefinedVariable(instruction);
                if (definedVar != null)
                {
                    if (!ranges.ContainsKey(definedVar))
                    {
                        // New definition - start range here
                        ranges[definedVar] = new LiveRange(definedVar, instructionIndex, instructionIndex);
                    }
                    else
                    {
                        // Redefinition - ensure range covers this point
                        ranges[definedVar].End = Math.Max(ranges[definedVar].End, instructionIndex);
                    }
                }

                // Track variable uses - extend live range to this point
                var usedVars = GetUsedVariables(instruction);
                foreach (var usedVar in usedVars)
                {
                    if (ranges.ContainsKey(usedVar))
                    {
                        // Extend existing range
                        ranges[usedVar].End = Math.Max(ranges[usedVar].End, instructionIndex);
                    }
                    else
                    {
                        // Used before defined in this pass - must be parameter
                        ranges[usedVar] = new LiveRange(usedVar, 0, instructionIndex);
                    }
                }

                instructionIndex++;
            }

            // Extend ranges for variables live-out of this block
            foreach (var varName in liveInfo.LiveOut)
            {
                if (ranges.ContainsKey(varName))
                {
                    ranges[varName].End = Math.Max(ranges[varName].End, blockToEndIndex[block]);
                }
            }
        }

        // Filter out temporaries for now - they have complex control flow patterns
        // that can cause register allocation conflicts
        // TODO: Improve liveness analysis to properly handle temporaries in loops
        return ranges.Values
            .Where(r => !r.VariableName.StartsWith("%"))
            .ToList();
    }

    /// <summary>
    /// Compute global liveness using CFG dataflow analysis
    /// </summary>
    private Dictionary<CFGNode, LivenessInfo> ComputeGlobalLiveness(ControlFlowGraph cfg)
    {
        var liveness = new Dictionary<CFGNode, LivenessInfo>();

        // Initialize
        foreach (var node in cfg.Nodes)
        {
            liveness[node] = new LivenessInfo();
        }

        // Iterative dataflow until fixpoint
        bool changed = true;
        int maxIterations = 100;
        int iteration = 0;

        while (changed && iteration < maxIterations)
        {
            changed = false;
            iteration++;

            foreach (var node in cfg.Nodes)
            {
                if (node.Block == null) continue;

                var info = liveness[node];
                var oldLiveIn = new HashSet<string>(info.LiveIn);
                var oldLiveOut = new HashSet<string>(info.LiveOut);

                // LiveOut = Union of LiveIn of all successors
                info.LiveOut.Clear();
                foreach (var edge in node.Successors)
                {
                    if (liveness.ContainsKey(edge.Destination))
                    {
                        info.LiveOut.UnionWith(liveness[edge.Destination].LiveIn);
                    }
                }

                // LiveIn = Use ∪ (LiveOut - Def)
                var use = GetBlockUses(node.Block);
                var def = GetBlockDefs(node.Block);

                info.LiveIn.Clear();
                info.LiveIn.UnionWith(use);
                info.LiveIn.UnionWith(info.LiveOut.Except(def));

                if (!oldLiveIn.SetEquals(info.LiveIn) || !oldLiveOut.SetEquals(info.LiveOut))
                {
                    changed = true;
                }
            }
        }

        return liveness;
    }

    private HashSet<string> GetBlockUses(IrBasicBlock block)
    {
        var uses = new HashSet<string>();
        foreach (var instruction in block.Instructions)
        {
            CollectUses(instruction, uses);
        }
        return uses;
    }

    private HashSet<string> GetBlockDefs(IrBasicBlock block)
    {
        var defs = new HashSet<string>();
        foreach (var instruction in block.Instructions)
        {
            var def = GetDefinedVariable(instruction);
            if (def != null) defs.Add(def);
        }
        return defs;
    }

    private void CollectUses(IrInstruction instruction, HashSet<string> uses)
    {
        var usedVars = GetUsedVariables(instruction);
        uses.UnionWith(usedVars);
    }

    /// <summary>
    /// Free registers for intervals that have expired
    /// </summary>
    private void ExpireOldIntervals(LiveRange current, List<LiveRange> active,
        Queue<string> availableDataRegs, Queue<string> availableAddrRegs)
    {
        // Remove expired intervals and return their registers
        active.RemoveAll(interval =>
        {
            if (interval.End < current.Start)
            {
                // This interval has expired, return its register
                if (interval.AssignedRegister != null)
                {
                    if (interval.AssignedRegister.StartsWith("d"))
                    {
                        availableDataRegs.Enqueue(interval.AssignedRegister);
                    }
                    else if (interval.AssignedRegister.StartsWith("a"))
                    {
                        availableAddrRegs.Enqueue(interval.AssignedRegister);
                    }
                }
                return true; // Remove from active
            }
            return false; // Keep in active
        });
    }

    /// <summary>
    /// Determine if a variable needs an address register
    /// </summary>
    private bool IsAddressVariable(string variableName)
    {
        // Heuristic: variables containing "ptr", "addr", or ending in "_p" need address registers
        // TODO: Use actual type information when available
        return variableName.Contains("ptr") ||
               variableName.Contains("addr") ||
               variableName.EndsWith("_p");
    }

    /// <summary>
    /// Get the variable defined by an instruction
    /// </summary>
    private string? GetDefinedVariable(IrInstruction instruction)
    {
        return instruction switch
        {
            IrBinaryOp binOp => binOp.ResultName,
            IrCall call => call.ResultName,
            IrLocalDecl decl => decl.Name,
            IrStore store => store.VariableName,
            _ => null
        };
    }

    /// <summary>
    /// Get all variables used by an instruction
    /// </summary>
    private HashSet<string> GetUsedVariables(IrInstruction instruction)
    {
        var vars = new HashSet<string>();

        switch (instruction)
        {
            case IrBinaryOp binOp:
                CollectVars(binOp.Left, vars);
                CollectVars(binOp.Right, vars);
                break;
            case IrReturn ret when ret.Value != null:
                CollectVars(ret.Value, vars);
                break;
            case IrStore store:
                CollectVars(store.Value, vars);
                // Note: Don't add store.VariableName as a "use" - it's a definition
                break;
            case IrConditionalBranch condBr:
                CollectVars(condBr.Condition, vars);
                break;
            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    CollectVars(arg, vars);
                }
                break;
            case IrDereferenceStore derefStore:
                CollectVars(derefStore.Value, vars);
                CollectVars(derefStore.Pointer, vars);
                break;
        }

        return vars;
    }

    private void CollectVars(IrValue value, HashSet<string> vars)
    {
        if (value is IrVariable var)
        {
            vars.Add(var.Name);
        }
    }
}

/// <summary>
/// Liveness information for a basic block
/// </summary>
internal class LivenessInfo
{
    public HashSet<string> LiveIn { get; } = new();
    public HashSet<string> LiveOut { get; } = new();
}
