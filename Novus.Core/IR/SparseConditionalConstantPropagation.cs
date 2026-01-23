namespace Novus.IR;

/// <summary>
/// Sparse Conditional Constant Propagation (SCCP) optimization pass.
///
/// SCCP is a more powerful version of constant propagation that:
/// 1. Propagates constants through the program
/// 2. Evaluates conditional branches when possible
/// 3. Marks unreachable code for elimination
/// 4. Discovers more constants than simple constant propagation
///
/// The "sparse" part means it only processes reachable code and propagates
/// constants only when needed, making it more efficient than exhaustive analysis.
///
/// Algorithm (Wegman-Zadeck):
/// 1. Start with all variables as "unknown" (⊤)
/// 2. Mark entry block as reachable
/// 3. Iterate:
///    - Process reachable blocks
///    - For each instruction, compute its lattice value:
///      * Constant if all operands are constant
///      * Overdefined (⊥) if operands conflict
///      * Unknown (⊤) if not yet evaluated
///    - Update SSA definitions
///    - Mark successor blocks as reachable based on branch evaluation
/// 4. Replace uses with constants
/// 5. Mark unreachable code for DCE
///
/// Example:
///   x = 5
///   if (x > 3)  // Always true! Can eliminate false branch
///     y = 10
///   else
///     y = 20   // UNREACHABLE
///   z = y      // y is always 10
///
/// After SCCP:
///   x = 5
///   y = 10
///   z = 10
///   // false branch eliminated
///
/// Benefits:
/// - More aggressive than simple constant propagation
/// - Can eliminate unreachable code
/// - Particularly effective after inlining
/// - Critical for 68k where every instruction counts
/// </summary>
public class SparseConditionalConstantPropagation(IrFunction function)
{
    /// <summary>
    /// Lattice values for SSA analysis
    /// </summary>
    private enum LatticeValue
    {
        Top,          // Unknown - no information yet
        Constant,     // Known constant value
        Bottom        // Overdefined - multiple different values
    }

    /// <summary>
    /// Lattice cell combining value type and constant
    /// </summary>
    private class LatticeCell
    {
        public LatticeValue Type { get; set; } = LatticeValue.Top;
        public long ConstantValue { get; set; }

        public static LatticeCell MakeConstant(long value)
        {
            return new LatticeCell { Type = LatticeValue.Constant, ConstantValue = value };
        }

        public static LatticeCell MakeBottom()
        {
            return new LatticeCell { Type = LatticeValue.Bottom };
        }

        public static LatticeCell MakeTop()
        {
            return new LatticeCell { Type = LatticeValue.Top };
        }
    }

    /// <summary>
    /// Maps variable names to their lattice values
    /// </summary>
    private Dictionary<string, LatticeCell> _lattice = new();

    /// <summary>
    /// Set of reachable blocks
    /// </summary>
    private HashSet<IrBasicBlock> _reachableBlocks = new();

    /// <summary>
    /// Work list of blocks to process
    /// </summary>
    private Queue<IrBasicBlock> _workList = new();

    /// <summary>
    /// Run SCCP analysis and optimization.
    /// Returns number of constants propagated.
    /// </summary>
    public int Propagate()
    {
        int propagationCount = 0;

        // Initialize: all variables unknown, only entry block reachable
        Initialize();

        // Iterate until fixpoint
        while (_workList.Count > 0)
        {
            var block = _workList.Dequeue();
            ProcessBlock(block);
        }

        // Replace variables with constants where possible
        propagationCount = ReplaceWithConstants();

        return propagationCount;
    }

    /// <summary>
    /// Initialize the SCCP algorithm
    /// </summary>
    private void Initialize()
    {
        _lattice.Clear();
        _reachableBlocks.Clear();
        _workList.Clear();

        // Mark entry block as reachable
        if (function.BasicBlocks.Count > 0)
        {
            var entryBlock = function.BasicBlocks[0];
            MarkReachable(entryBlock);
        }
    }

    /// <summary>
    /// Mark a block as reachable and add to work list
    /// </summary>
    private void MarkReachable(IrBasicBlock block)
    {
        if (!_reachableBlocks.Contains(block))
        {
            _reachableBlocks.Add(block);
            _workList.Enqueue(block);
        }
    }

    /// <summary>
    /// Process a single basic block
    /// </summary>
    private void ProcessBlock(IrBasicBlock block)
    {
        foreach (var instruction in block.Instructions)
        {
            ProcessInstruction(instruction, block);
        }
    }

    /// <summary>
    /// Process a single instruction
    /// </summary>
    private void ProcessInstruction(IrInstruction instruction, IrBasicBlock block)
    {
        switch (instruction)
        {
            case IrLocalDecl decl when decl.InitialValue != null:
                UpdateLattice(decl.Name, EvaluateValue(decl.InitialValue));
                break;

            case IrStore store:
                UpdateLattice(store.VariableName, EvaluateValue(store.Value));
                break;

            case IrBinaryOp binOp:
                var result = EvaluateBinaryOp(binOp);
                UpdateLattice(binOp.ResultName, result);
                break;

            case IrConditionalBranch condBr:
                ProcessConditionalBranch(condBr, block);
                break;

            case IrBranch br:
                // Unconditional branch - mark target as reachable
                var target = FindBlockByLabel(br.Target);
                if (target != null)
                {
                    MarkReachable(target);
                }
                break;
        }
    }

    /// <summary>
    /// Process conditional branch - may discover unreachable branches
    /// </summary>
    private void ProcessConditionalBranch(IrConditionalBranch branch, IrBasicBlock block)
    {
        var conditionCell = EvaluateValue(branch.Condition);

        if (conditionCell.Type == LatticeValue.Constant)
        {
            // Condition is constant - only one branch is reachable!
            bool takeTrueBranch = conditionCell.ConstantValue != 0;

            var targetLabel = takeTrueBranch ? branch.TrueTarget : branch.FalseTarget;
            var target = FindBlockByLabel(targetLabel);

            if (target != null)
            {
                MarkReachable(target);
            }
            // Note: The other branch becomes unreachable and will be eliminated by DCE
        }
        else
        {
            // Condition is not constant - both branches might be reachable
            var trueTarget = FindBlockByLabel(branch.TrueTarget);
            var falseTarget = FindBlockByLabel(branch.FalseTarget);

            if (trueTarget != null) MarkReachable(trueTarget);
            if (falseTarget != null) MarkReachable(falseTarget);
        }
    }

    /// <summary>
    /// Evaluate a binary operation using lattice values
    /// </summary>
    private LatticeCell EvaluateBinaryOp(IrBinaryOp binOp)
    {
        var leftCell = EvaluateValue(binOp.Left);
        var rightCell = EvaluateValue(binOp.Right);

        // If either operand is Bottom (overdefined), result is Bottom
        if (leftCell.Type == LatticeValue.Bottom || rightCell.Type == LatticeValue.Bottom)
            return LatticeCell.MakeBottom();

        // If either operand is Top (unknown), result is Top
        if (leftCell.Type == LatticeValue.Top || rightCell.Type == LatticeValue.Top)
            return LatticeCell.MakeTop();

        // Both are constants - evaluate!
        return EvaluateConstantBinaryOp(binOp.Operation, leftCell.ConstantValue, rightCell.ConstantValue);
    }

    /// <summary>
    /// Evaluate a binary operation on two constants
    /// </summary>
    private LatticeCell EvaluateConstantBinaryOp(IrBinaryOp.OpKind op, long left, long right)
    {
        long result = op switch
        {
            IrBinaryOp.OpKind.Add => left + right,
            IrBinaryOp.OpKind.Sub => left - right,
            IrBinaryOp.OpKind.Mul => left * right,
            IrBinaryOp.OpKind.Div => right != 0 ? left / right : 0, // Avoid divide by zero
            IrBinaryOp.OpKind.Mod => right != 0 ? left % right : 0,
            IrBinaryOp.OpKind.And => left & right,
            IrBinaryOp.OpKind.Or => left | right,
            IrBinaryOp.OpKind.Xor => left ^ right,
            IrBinaryOp.OpKind.Shl => left << (int)right,
            IrBinaryOp.OpKind.Shr => left >> (int)right,
            IrBinaryOp.OpKind.Eq => left == right ? 1 : 0,
            IrBinaryOp.OpKind.Ne => left != right ? 1 : 0,
            IrBinaryOp.OpKind.Lt => left < right ? 1 : 0,
            IrBinaryOp.OpKind.Le => left <= right ? 1 : 0,
            IrBinaryOp.OpKind.Gt => left > right ? 1 : 0,
            IrBinaryOp.OpKind.Ge => left >= right ? 1 : 0,
            _ => 0
        };

        return LatticeCell.MakeConstant(result);
    }

    /// <summary>
    /// Evaluate an IR value to get its lattice cell
    /// </summary>
    private LatticeCell EvaluateValue(IrValue value)
    {
        return value switch
        {
            IrConstant constant => LatticeCell.MakeConstant(constant.Value),
            IrVariable variable => GetLatticeValue(variable.Name),
            _ => LatticeCell.MakeBottom()
        };
    }

    /// <summary>
    /// Get the lattice value for a variable
    /// </summary>
    private LatticeCell GetLatticeValue(string variableName)
    {
        if (_lattice.TryGetValue(variableName, out var cell))
            return cell;

        // Unknown variable - could be parameter or from unreachable code
        return LatticeCell.MakeTop();
    }

    /// <summary>
    /// Update lattice value for a variable (monotonic - can only go down the lattice)
    /// </summary>
    private void UpdateLattice(string variableName, LatticeCell newValue)
    {
        if (!_lattice.TryGetValue(variableName, out var currentValue))
        {
            // First time seeing this variable
            _lattice[variableName] = newValue;
            return;
        }

        // Meet operation: combine old and new values
        var meetResult = Meet(currentValue, newValue);

        if (meetResult.Type != currentValue.Type ||
            (meetResult.Type == LatticeValue.Constant && meetResult.ConstantValue != currentValue.ConstantValue))
        {
            _lattice[variableName] = meetResult;
        }
    }

    /// <summary>
    /// Meet operation for lattice values (greatest lower bound)
    /// </summary>
    private LatticeCell Meet(LatticeCell a, LatticeCell b)
    {
        // Bottom is the lowest value - always wins
        if (a.Type == LatticeValue.Bottom || b.Type == LatticeValue.Bottom)
            return LatticeCell.MakeBottom();

        // Top is the highest - other value wins
        if (a.Type == LatticeValue.Top)
            return b;
        if (b.Type == LatticeValue.Top)
            return a;

        // Both are constants
        if (a.ConstantValue == b.ConstantValue)
            return a; // Same constant

        // Different constants - conflict - go to Bottom
        return LatticeCell.MakeBottom();
    }

    /// <summary>
    /// Replace variable uses with constants where possible
    /// </summary>
    private int ReplaceWithConstants()
    {
        int replacementCount = 0;

        foreach (var block in function.BasicBlocks)
        {
            // Skip unreachable blocks
            if (!_reachableBlocks.Contains(block))
                continue;

            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];

                // Try to replace operands with constants
                var replaced = TryReplaceOperands(instruction);
                if (replaced != null)
                {
                    block.Instructions[i] = replaced;
                    replacementCount++;
                }
            }
        }

        return replacementCount;
    }

    /// <summary>
    /// Try to replace variable operands with constants in an instruction
    /// </summary>
    private IrInstruction? TryReplaceOperands(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrBinaryOp binOp:
                var newLeft = TryGetConstant(binOp.Left);
                var newRight = TryGetConstant(binOp.Right);

                if (newLeft != null || newRight != null)
                {
                    return new IrBinaryOp(
                        binOp.ResultName,
                        binOp.Operation,
                        newLeft ?? binOp.Left,
                        newRight ?? binOp.Right,
                        binOp.Type);
                }
                break;

            case IrStore store:
                var newValue = TryGetConstant(store.Value);
                if (newValue != null)
                {
                    return new IrStore(store.VariableName, newValue);
                }
                break;

            case IrReturn ret when ret.Value != null:
                var newRetValue = TryGetConstant(ret.Value);
                if (newRetValue != null)
                {
                    return new IrReturn(newRetValue);
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Try to get a constant value for an IR value
    /// </summary>
    private IrValue? TryGetConstant(IrValue value)
    {
        if (value is IrVariable variable)
        {
            var cell = GetLatticeValue(variable.Name);
            if (cell.Type == LatticeValue.Constant)
            {
                return new IrConstant(cell.ConstantValue, variable.Type);
            }
        }
        return null;
    }

    /// <summary>
    /// Find a basic block by its label
    /// </summary>
    private IrBasicBlock? FindBlockByLabel(string label)
    {
        foreach (var block in function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrLabel labelInst && labelInst.Name == label)
                    return block;
            }
        }
        return null;
    }

    /// <summary>
    /// Get the set of unreachable blocks (for DCE)
    /// </summary>
    public HashSet<IrBasicBlock> GetUnreachableBlocks()
    {
        var unreachable = new HashSet<IrBasicBlock>();
        foreach (var block in function.BasicBlocks)
        {
            if (!_reachableBlocks.Contains(block))
            {
                unreachable.Add(block);
            }
        }
        return unreachable;
    }
}
