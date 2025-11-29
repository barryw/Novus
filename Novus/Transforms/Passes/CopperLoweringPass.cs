using Novus.IR;
using Novus.HIR;

namespace Novus.Transforms.Passes;

/// <summary>
/// Lowers HIR Copper lists to LIR (68k assembly data structures)
/// Copper lists are Amiga-specific display list instructions for the Copper co-processor
/// </summary>
public class CopperLoweringPass : TransformPassBase
{
    public override string Name => "Copper Lowering";

    private int _copperListCounter = 0;

    public override bool Transform(IrModule module)
    {
        bool changed = false;

        // Find HIR instructions that need lowering
        foreach (var hirInstruction in module.HirInstructions.ToList())
        {
            if (hirInstruction is HirCopperList copperList)
            {
                // Lower this copper list
                LowerCopperList(module, copperList);

                module.HirInstructions.Remove(hirInstruction);
                changed = true;
            }
        }

        return changed;
    }

    private void LowerCopperList(IrModule module, HirCopperList copperList)
    {
        // Generate copper list words from HIR instructions
        var words = new List<ushort>();
        bool allConstant = true;

        foreach (var instruction in copperList.Instructions)
        {
            switch (instruction)
            {
                case HirCopperInstruction.Wait wait:
                    if (TryEvaluateConstant(wait.X, out int waitX) &&
                        TryEvaluateConstant(wait.Y, out int waitY))
                    {
                        // Generate WAIT instruction
                        // Format: VP<<8 | HP<<1 | 1 (vertical position, horizontal position, wait bit)
                        // The second word is the compare enable mask ($fffe means compare all bits except HP LSB)
                        ushort waitWord1 = (ushort)(((waitY & 0xFF) << 8) | ((waitX & 0x7F) << 1) | 0x01);
                        ushort waitWord2 = 0xFFFE; // Standard wait mask
                        words.Add(waitWord1);
                        words.Add(waitWord2);
                    }
                    else
                    {
                        allConstant = false;
                    }
                    break;

                case HirCopperInstruction.Move move:
                    if (TryEvaluateConstant(move.Register, out int regAddr) &&
                        TryEvaluateConstant(move.Value, out int moveValue))
                    {
                        // Generate MOVE instruction
                        // Format: register_offset (low 9 bits of address from $DFF000), value
                        // The register address must be in custom chip range $DFF000-$DFF1FF
                        ushort regOffset = (ushort)(regAddr & 0x1FE); // Extract offset, clear bit 0
                        ushort dataWord = (ushort)(moveValue & 0xFFFF);
                        words.Add(regOffset);
                        words.Add(dataWord);
                    }
                    else
                    {
                        allConstant = false;
                    }
                    break;

                case HirCopperInstruction.Skip skip:
                    if (TryEvaluateConstant(skip.X, out int skipX) &&
                        TryEvaluateConstant(skip.Y, out int skipY))
                    {
                        // Generate SKIP instruction (same as WAIT but with $FFFF mask)
                        ushort skipWord1 = (ushort)(((skipY & 0xFF) << 8) | ((skipX & 0x7F) << 1) | 0x01);
                        ushort skipWord2 = 0xFFFF; // Skip mask (bit 0 = 1 distinguishes from WAIT)
                        words.Add(skipWord1);
                        words.Add(skipWord2);
                    }
                    else
                    {
                        allConstant = false;
                    }
                    break;
            }
        }

        // Add terminator: END instruction ($FFFF, $FFFE)
        words.Add(0xFFFF);
        words.Add(0xFFFE);

        if (allConstant && copperList.ResultName != null)
        {
            // Generate static copper list data
            var label = $"__copper_list_{_copperListCounter++}";
            var copperData = new IrCopperListData(label, words);

            // Create a static variable for the copper list
            // Note: This needs chip RAM attribute, but for now we use regular static
            var arrayType = new IrArrayType(IrIntType.U16, words.Count);
            var arrayLiteral = new IrArrayLiteral(arrayType);
            foreach (var word in words)
            {
                arrayLiteral.Elements.Add(new IrConstant(word, IrIntType.U16));
            }

            var staticVar = new IrStaticVariable(
                label,
                arrayType,
                Visibility.Private,
                isMutable: false,
                arrayLiteral
            );
            module.StaticVariables.Add(staticVar);

            // Find the function containing the local variable and update the initialization
            // The copper expression created a local variable initialized to 0
            // We need to find that and replace it with the address of our static data
            UpdateCopperVariableInit(module, copperList.ResultName, label);
        }
        else if (!allConstant)
        {
            // TODO: Generate runtime copper list building code
            // For now, emit a warning comment in the output
            Console.WriteLine($"Warning: Copper list contains non-constant values, runtime generation not yet implemented");
        }
    }

    /// <summary>
    /// Try to evaluate an IrValue to a compile-time constant integer
    /// </summary>
    private bool TryEvaluateConstant(IrValue value, out int result)
    {
        result = 0;

        switch (value)
        {
            case IrConstant constant:
                result = (int)constant.Value;
                return true;

            case IrVariable variable:
                // Could look up constant values from module.Constants
                // For now, treat variables as non-constant
                return false;

            case IrCastValue cast:
                // Recursively evaluate the cast source
                return TryEvaluateConstant(cast.Value, out result);

            default:
                return false;
        }
    }

    /// <summary>
    /// Update the copper variable initialization to point to static data
    /// </summary>
    private void UpdateCopperVariableInit(IrModule module, string varName, string staticLabel)
    {
        foreach (var function in module.Functions)
        {
            foreach (var block in function.BasicBlocks)
            {
                for (int i = 0; i < block.Instructions.Count; i++)
                {
                    if (block.Instructions[i] is IrLocalDecl localDecl && localDecl.Name == varName)
                    {
                        // Replace the initialization (which is 0) with address of static data
                        // Create a borrow of the static array's first element
                        var staticVarRef = new IrGlobalVariable(staticLabel, new IrArrayType(IrIntType.U16, 0));
                        var ptrType = new IrPointerType(IrIntType.U16);

                        // Create a new local declaration with proper initialization
                        // The C codegen will handle the array-to-pointer decay
                        var newDecl = new IrLocalDecl(
                            varName,
                            ptrType,
                            localDecl.IsMutable,
                            new IrBorrowValue(staticVarRef, ptrType, false)
                        );

                        block.Instructions[i] = newDecl;
                        return;
                    }
                }
            }
        }
    }
}
