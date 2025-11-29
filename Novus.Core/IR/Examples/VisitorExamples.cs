namespace Novus.IR.Examples;

/// <summary>
/// Example usage of IrVisitor for analysis passes
/// </summary>
public class VariableUsageCounter : IrVisitor<int, object?>
{
    private readonly Dictionary<string, int> _usageCounts = new();

    public Dictionary<string, int> GetUsageCounts() => _usageCounts;

    public override int VisitVariable(IrVariable variable, object? context)
    {
        if (_usageCounts.ContainsKey(variable.Name))
            _usageCounts[variable.Name]++;
        else
            _usageCounts[variable.Name] = 1;

        return base.VisitVariable(variable, context);
    }
}

/// <summary>
/// Example usage of IrRewriter for transformation passes
/// </summary>
public class ConstantFolder : IrRewriter
{
    /// <summary>
    /// Fold constant binary operations at compile time
    /// </summary>
    public override IrInstruction RewriteBinaryOp(IrBinaryOp binaryOp)
    {
        // First rewrite child values
        var rewritten = base.RewriteBinaryOp(binaryOp);
        if (rewritten is not IrBinaryOp rewrittenBinaryOp)
        {
            return rewritten!; // If rewriting changed the type, return as-is
        }
        binaryOp = rewrittenBinaryOp;

        // Check if both operands are constants
        if (binaryOp.Left is IrConstant leftConst && binaryOp.Right is IrConstant rightConst)
        {
            long result = binaryOp.Operation switch
            {
                IrBinaryOp.OpKind.Add => leftConst.Value + rightConst.Value,
                IrBinaryOp.OpKind.Sub => leftConst.Value - rightConst.Value,
                IrBinaryOp.OpKind.Mul => leftConst.Value * rightConst.Value,
                IrBinaryOp.OpKind.Div => rightConst.Value != 0 ? leftConst.Value / rightConst.Value : leftConst.Value,
                IrBinaryOp.OpKind.Mod => rightConst.Value != 0 ? leftConst.Value % rightConst.Value : leftConst.Value,
                IrBinaryOp.OpKind.And => leftConst.Value & rightConst.Value,
                IrBinaryOp.OpKind.Or => leftConst.Value | rightConst.Value,
                IrBinaryOp.OpKind.Xor => leftConst.Value ^ rightConst.Value,
                IrBinaryOp.OpKind.Shl => leftConst.Value << (int)rightConst.Value,
                IrBinaryOp.OpKind.Shr => leftConst.Value >> (int)rightConst.Value,
                _ => throw new NotSupportedException($"Cannot fold comparison operation: {binaryOp.Operation}")
            };

            // Replace the binary operation with a store of the folded constant
            return new IrStore(binaryOp.ResultName, new IrConstant(result, binaryOp.Type));
        }

        return binaryOp;
    }
}

/// <summary>
/// Example usage of IrVisitor for dead code detection
/// </summary>
public class DeadCodeDetector : IrVisitor<bool, HashSet<string>>
{
    private readonly HashSet<string> _deadCode = new();

    public HashSet<string> GetDeadCode() => _deadCode;

    public override bool VisitFunction(IrFunction function, HashSet<string> liveVariables)
    {
        // First pass: collect all defined variables
        var definedVariables = new HashSet<string>();
        foreach (var param in function.Parameters)
        {
            definedVariables.Add(param.Name);
        }
        foreach (var local in function.LocalVariables)
        {
            definedVariables.Add(local.Name);
        }

        // Second pass: collect all used variables
        var usedVariables = new HashSet<string>();
        base.VisitFunction(function, usedVariables);

        // Dead variables are defined but never used
        foreach (var definedVar in definedVariables)
        {
            if (!usedVariables.Contains(definedVar))
            {
                _deadCode.Add(definedVar);
            }
        }

        return true;
    }

    public override bool VisitVariable(IrVariable variable, HashSet<string> liveVariables)
    {
        liveVariables.Add(variable.Name);
        return base.VisitVariable(variable, liveVariables);
    }
}
