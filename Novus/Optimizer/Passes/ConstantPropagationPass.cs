using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>
/// Constant propagation pass
/// Replaces uses of variables with known constant values
/// Example: let x = 5; let y = x + 1; => let y = 5 + 1;
/// </summary>
public class ConstantPropagationPass : BasicBlockPassBase
{
    public override string Name => "Constant Propagation";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        bool changed = false;
        var constantValues = new Dictionary<string, IrConstant>();

        foreach (var instruction in block.Instructions)
        {
            // Track assignments of constants to variables
            if (instruction is IrLocalDecl localDecl)
            {
                if (localDecl.InitialValue is IrConstant constant)
                {
                    constantValues[localDecl.Name] = constant;
                }
                else if (localDecl.InitialValue is IrVariable sourceVar && constantValues.ContainsKey(sourceVar.Name))
                {
                    // x = y where y is constant => x is also constant
                    localDecl.InitialValue = constantValues[sourceVar.Name];
                    constantValues[localDecl.Name] = constantValues[sourceVar.Name];
                    changed = true;
                }
                else
                {
                    // Non-constant assignment, remove from tracking
                    constantValues.Remove(localDecl.Name);
                }
            }
            else if (instruction is IrStore store)
            {
                // Store invalidates the variable's constant status
                constantValues.Remove(store.VariableName);

                // But if storing a constant, track it
                if (store.Value is IrConstant constant)
                {
                    constantValues[store.VariableName] = constant;
                }
                else if (store.Value is IrVariable sourceVar && constantValues.ContainsKey(sourceVar.Name))
                {
                    store.Value = constantValues[sourceVar.Name];
                    constantValues[store.VariableName] = constantValues[sourceVar.Name];
                    changed = true;
                }
            }
            else if (instruction is IrBinaryOp binOp)
            {
                // Replace left operand if it's a known constant
                if (binOp.Left is IrVariable leftVar && constantValues.ContainsKey(leftVar.Name))
                {
                    binOp.Left = constantValues[leftVar.Name];
                    changed = true;
                }

                // Replace right operand if it's a known constant
                if (binOp.Right is IrVariable rightVar && constantValues.ContainsKey(rightVar.Name))
                {
                    binOp.Right = constantValues[rightVar.Name];
                    changed = true;
                }

                // Track if this binary op assigns a result
                if (!string.IsNullOrEmpty(binOp.ResultName))
                {
                    // Invalidate the result variable (may be reassigned)
                    constantValues.Remove(binOp.ResultName);
                }
            }
            else if (instruction is IrReturn ret)
            {
                // Propagate constants into return statements
                if (ret.Value is IrVariable retVar && constantValues.ContainsKey(retVar.Name))
                {
                    ret.Value = constantValues[retVar.Name];
                    changed = true;
                }
            }
            else if (instruction is IrCall)
            {
                // Function calls may have side effects - clear all tracked constants
                constantValues.Clear();
            }
        }

        return changed;
    }
}
