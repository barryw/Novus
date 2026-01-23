namespace Novus.IR;

/// <summary>
/// Common Subexpression Elimination (CSE) optimization pass.
/// Eliminates redundant computation of the same expression.
///
/// CSE finds expressions that are computed multiple times with the same operands
/// and replaces all but the first with a reference to the computed value.
///
/// Algorithm:
/// 1. Build a table of available expressions at each program point
/// 2. For each expression, check if an equivalent one has already been computed
/// 3. If found, replace the expression with a reference to the previous result
/// 4. If not found, add it to the available expressions table
///
/// Example:
///   a = x + y
///   b = x + y    // Redundant: same as 'a'
///   c = a + b
///
/// After CSE:
///   a = x + y
///   b = a        // Reuse 'a' instead of recomputing
///   c = a + a    // b replaced with a
///
/// Benefits:
/// - Reduces redundant computation
/// - Fewer instructions to execute
/// - Better register utilization
/// - Particularly valuable for 68k where operations are expensive
///
/// Limitations:
/// - Only works within a basic block (local CSE)
/// - Does not handle commutative operations (x+y vs y+x)
/// - Does not track across function calls or memory writes
/// </summary>
public class CommonSubexpressionElimination(IrFunction function)
{
    private bool _madeChanges = false;

    /// <summary>
    /// Maps expression signatures to the variable holding the result
    /// </summary>
    private Dictionary<string, string> _availableExpressions = new();

    /// <summary>
    /// Run CSE until no more changes can be made.
    /// Returns the number of eliminations performed.
    /// </summary>
    public int Eliminate()
    {
        int totalEliminations = 0;

        do
        {
            _madeChanges = false;
            int eliminations = EliminatePass();
            totalEliminations += eliminations;
        } while (_madeChanges);

        return totalEliminations;
    }

    private int EliminatePass()
    {
        int eliminationCount = 0;

        foreach (var block in function.BasicBlocks)
        {
            _availableExpressions.Clear();

            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];

                // Check if this instruction computes an expression we've seen before
                if (instruction is IrBinaryOp binOp)
                {
                    var signature = GetExpressionSignature(binOp);
                    if (signature != null)
                    {
                        if (_availableExpressions.TryGetValue(signature, out var existingResult))
                        {
                            // Found a common subexpression - replace with a copy
                            var copy = new IrStore(binOp.ResultName, new IrVariable(existingResult, binOp.Type));
                            block.Instructions[i] = copy;
                            eliminationCount++;
                            _madeChanges = true;
                        }
                        else
                        {
                            // New expression - record it
                            _availableExpressions[signature] = binOp.ResultName;
                        }
                    }
                }
                // Memory writes and function calls invalidate available expressions
                else if (IsInvalidatingInstruction(instruction))
                {
                    _availableExpressions.Clear();
                }
            }
        }

        return eliminationCount;
    }

    /// <summary>
    /// Generate a unique signature for an expression.
    /// Two expressions with the same signature compute the same value.
    /// Returns null if the expression cannot be safely eliminated.
    /// </summary>
    private string? GetExpressionSignature(IrBinaryOp binOp)
    {
        // Only eliminate pure operations (no side effects)
        if (!IsPureOperation(binOp.Operation))
            return null;

        // Build signature: "operation:left:right"
        var leftSig = GetValueSignature(binOp.Left);
        var rightSig = GetValueSignature(binOp.Right);

        if (leftSig == null || rightSig == null)
            return null;

        // For commutative operations, normalize the order
        if (IsCommutative(binOp.Operation))
        {
            // Ensure consistent ordering for commutative ops
            if (string.Compare(leftSig, rightSig, StringComparison.Ordinal) > 0)
            {
                (leftSig, rightSig) = (rightSig, leftSig);
            }
        }

        return $"{binOp.Operation}:{leftSig}:{rightSig}";
    }

    /// <summary>
    /// Get a unique signature for a value (variable or constant).
    /// </summary>
    private string? GetValueSignature(IrValue value)
    {
        return value switch
        {
            IrConstant constant => $"const:{constant.Value}",
            IrVariable variable => $"var:{variable.Name}",
            _ => null // Unknown value type - can't generate signature
        };
    }

    /// <summary>
    /// Check if an operation is pure (has no side effects).
    /// </summary>
    private bool IsPureOperation(IrBinaryOp.OpKind operation)
    {
        return operation switch
        {
            // Arithmetic
            IrBinaryOp.OpKind.Add => true,
            IrBinaryOp.OpKind.Sub => true,
            IrBinaryOp.OpKind.Mul => true,
            IrBinaryOp.OpKind.Div => false, // Can trap on divide by zero
            IrBinaryOp.OpKind.Mod => false, // Can trap on divide by zero

            // Bitwise
            IrBinaryOp.OpKind.And => true,
            IrBinaryOp.OpKind.Or => true,
            IrBinaryOp.OpKind.Xor => true,
            IrBinaryOp.OpKind.Shl => true,
            IrBinaryOp.OpKind.Shr => true,

            // Comparison
            IrBinaryOp.OpKind.Eq => true,
            IrBinaryOp.OpKind.Ne => true,
            IrBinaryOp.OpKind.Lt => true,
            IrBinaryOp.OpKind.Le => true,
            IrBinaryOp.OpKind.Gt => true,
            IrBinaryOp.OpKind.Ge => true,

            _ => false
        };
    }

    /// <summary>
    /// Check if an operation is commutative (a op b == b op a).
    /// </summary>
    private bool IsCommutative(IrBinaryOp.OpKind operation)
    {
        return operation switch
        {
            IrBinaryOp.OpKind.Add => true,
            IrBinaryOp.OpKind.Mul => true,
            IrBinaryOp.OpKind.And => true,
            IrBinaryOp.OpKind.Or => true,
            IrBinaryOp.OpKind.Xor => true,
            IrBinaryOp.OpKind.Eq => true,
            IrBinaryOp.OpKind.Ne => true,
            _ => false
        };
    }

    /// <summary>
    /// Check if an instruction invalidates available expressions.
    /// Memory writes and function calls can change values, so we must be conservative.
    /// </summary>
    private bool IsInvalidatingInstruction(IrInstruction instruction)
    {
        return instruction switch
        {
            IrCall _ => true,                  // Function calls may have side effects
            IrStore _ => true,                 // Variable stores change memory
            IrMemberStore _ => true,           // Field stores change memory
            IrIndexStore _ => true,            // Array stores change memory
            IrIndexedFieldStore _ => true,     // Indexed field stores change memory
            IrDereferenceStore _ => true,      // Pointer stores change memory
            IrHardwareWrite _ => true,         // Hardware writes have side effects
            _ => false
        };
    }
}
