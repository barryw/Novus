using Novus.IR;

namespace Novus.Optimizer.Passes;

/// <summary>Removes checks dominated by an exclusive range-loop condition.</summary>
public sealed class BoundsCheckEliminationPass : BasicBlockPassBase
{
    public override string Name => "Bounds Check Elimination";

    public override bool RunOnBasicBlock(IrBasicBlock block)
    {
        var changed = false;
        foreach (var hint in block.Instructions.OfType<IrStructuredForLoopHint>())
        {
            var body = block.Instructions.FindIndex(instruction =>
                instruction is IrLabel label && label.Name == hint.BodyLabel);
            var end = block.Instructions.FindIndex(instruction =>
                instruction is IrLabel label && label.Name == hint.EndLabel);
            if (body < 0 || end <= body)
                continue;

            for (var index = body + 1; index < end; index++)
            {
                switch (block.Instructions[index])
                {
                    case IrIndexAccess access when IsProven(access.Index, access.Length, hint):
                        access.BoundsCheck = IrBoundsCheckMode.Proven;
                        changed = true;
                        break;
                    case IrIndexStore store when IsProven(store.Index, store.Length, hint):
                        store.BoundsCheck = IrBoundsCheckMode.Proven;
                        changed = true;
                        break;
                }
            }
        }
        return changed;
    }

    private static bool IsProven(
        IrValue index,
        IrValue? length,
        IrStructuredForLoopHint hint) =>
        index is IrVariable indexVariable && indexVariable.Name == hint.LoopVarName &&
        length is IrVariable lengthVariable && lengthVariable.Name == hint.LengthVarName;
}
