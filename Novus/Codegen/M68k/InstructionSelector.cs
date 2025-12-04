using System.Text;
using Novus.IR;

namespace Novus.Codegen.M68k;

/// <summary>
/// Selects 68k instructions for IR operations.
/// Handles instruction selection and operand sizing (byte/word/long).
/// </summary>
public class InstructionSelector
{
    private readonly RegisterAllocator _allocator;
    private readonly StringBuilder _output;

    public InstructionSelector(RegisterAllocator allocator, StringBuilder output)
    {
        _allocator = allocator;
        _output = output;
    }

    /// <summary>
    /// Get the size suffix for a type (.b, .w, .l)
    /// </summary>
    public static string GetSizeSuffix(IrType type)
    {
        int size = type.SizeInBytes;
        return size switch
        {
            1 => ".b",      // byte
            2 => ".w",      // word
            4 => ".l",      // long
            _ => ".l"       // default to long for larger types
        };
    }

    /// <summary>
    /// Emit a binary operation
    /// </summary>
    public void EmitBinaryOp(IrBinaryOp op, string resultVar)
    {
        string suffix = GetSizeSuffix(op.Type);

        // Load left operand to D0
        EmitLoadValue(op.Left, M68kRegister.D0, op.Type);

        // For commutative ops, we can operate directly
        // For non-commutative, we need D1 as scratch
        switch (op.Operation)
        {
            case IrBinaryOp.OpKind.Add:
                EmitAddOp(op.Right, suffix);
                break;

            case IrBinaryOp.OpKind.Sub:
                EmitSubOp(op.Right, suffix);
                break;

            case IrBinaryOp.OpKind.Mul:
                EmitMulOp(op.Right, suffix, op.Type);
                break;

            case IrBinaryOp.OpKind.Div:
                EmitDivOp(op.Right, suffix, op.Type);
                break;

            case IrBinaryOp.OpKind.Mod:
                EmitModOp(op.Right, suffix, op.Type);
                break;

            case IrBinaryOp.OpKind.And:
                EmitLogicalOp("and", op.Right, suffix);
                break;

            case IrBinaryOp.OpKind.Or:
                EmitLogicalOp("or", op.Right, suffix);
                break;

            case IrBinaryOp.OpKind.Xor:
                EmitLogicalOp("eor", op.Right, suffix);
                break;

            case IrBinaryOp.OpKind.Shl:
                EmitShiftOp("lsl", op.Right, suffix);
                break;

            case IrBinaryOp.OpKind.Shr:
                // Use logical shift right (unsigned) or arithmetic shift right (signed)
                bool isSigned = op.Type is IrIntType intType && intType.IsSigned;
                EmitShiftOp(isSigned ? "asr" : "lsr", op.Right, suffix);
                break;

            case IrBinaryOp.OpKind.Eq:
            case IrBinaryOp.OpKind.Ne:
            case IrBinaryOp.OpKind.Lt:
            case IrBinaryOp.OpKind.Le:
            case IrBinaryOp.OpKind.Gt:
            case IrBinaryOp.OpKind.Ge:
                EmitComparisonOp(op, resultVar);
                return; // Comparison handles result store itself

            default:
                throw new NotImplementedException($"Binary operation {op.Operation} not implemented");
        }

        // Store result from D0 to result variable
        EmitStoreRegister(M68kRegister.D0, resultVar, op.Type);
    }

    private void EmitAddOp(IrValue right, string suffix)
    {
        if (right is IrConstant constant && constant.Value >= -128 && constant.Value <= 127)
        {
            // Use ADDQ for small constants (faster)
            if (constant.Value >= 1 && constant.Value <= 8)
            {
                _output.AppendLine($"    addq{suffix}  #{constant.Value},d0");
                return;
            }
        }

        // Load right operand to D1 and add
        EmitLoadValue(right, M68kRegister.D1, right.Type);
        _output.AppendLine($"    add{suffix}   d1,d0");
    }

    private void EmitSubOp(IrValue right, string suffix)
    {
        if (right is IrConstant constant && constant.Value >= -128 && constant.Value <= 127)
        {
            // Use SUBQ for small constants
            if (constant.Value >= 1 && constant.Value <= 8)
            {
                _output.AppendLine($"    subq{suffix}  #{constant.Value},d0");
                return;
            }
        }

        // Load right operand to D1 and subtract
        EmitLoadValue(right, M68kRegister.D1, right.Type);
        _output.AppendLine($"    sub{suffix}   d1,d0");
    }

    private void EmitMulOp(IrValue right, string suffix, IrType type)
    {
        // 68000 only has 16x16->32 multiply (MULS/MULU)
        // For 32-bit multiply, we need library routine or inline expansion
        bool isSigned = type is IrIntType intType && intType.IsSigned;

        if (type.SizeInBytes <= 2)
        {
            // Use hardware multiply for 16-bit
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    {(isSigned ? "muls" : "mulu")}{suffix}  d1,d0");
        }
        else
        {
            // For 32-bit multiply, call library routine
            // TODO: Implement __mulsi3 or inline expansion
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    ; TODO: 32-bit multiply d0 * d1 -> d0");
            _output.AppendLine($"    ; For now, use software multiply routine");
        }
    }

    private void EmitDivOp(IrValue right, string suffix, IrType type)
    {
        // 68000 only has 32/16->16 divide (DIVS/DIVU)
        bool isSigned = type is IrIntType intType && intType.IsSigned;

        if (type.SizeInBytes <= 2)
        {
            // Use hardware divide for 16-bit
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    {(isSigned ? "divs" : "divu")}{suffix}  d1,d0");
        }
        else
        {
            // For 32-bit divide, call library routine
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    ; TODO: 32-bit divide d0 / d1 -> d0");
        }
    }

    private void EmitModOp(IrValue right, string suffix, IrType type)
    {
        // Modulo: use divide and extract remainder
        bool isSigned = type is IrIntType intType && intType.IsSigned;

        if (type.SizeInBytes <= 2)
        {
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    {(isSigned ? "divs" : "divu")}{suffix}  d1,d0");
            _output.AppendLine($"    swap      d0              ; Remainder in lower word");
        }
        else
        {
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    ; TODO: 32-bit modulo d0 % d1 -> d0");
        }
    }

    private void EmitLogicalOp(string op, IrValue right, string suffix)
    {
        if (right is IrConstant constant)
        {
            _output.AppendLine($"    {op}{suffix}    #{constant.Value},d0");
        }
        else
        {
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    {op}{suffix}    d1,d0");
        }
    }

    private void EmitShiftOp(string op, IrValue right, string suffix)
    {
        if (right is IrConstant constant && constant.Value >= 1 && constant.Value <= 8)
        {
            // Use immediate shift
            _output.AppendLine($"    {op}{suffix}    #{constant.Value},d0");
        }
        else
        {
            // Load shift count to D1 and use register shift
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _output.AppendLine($"    {op}{suffix}    d1,d0");
        }
    }

    private void EmitComparisonOp(IrBinaryOp op, string resultVar)
    {
        string suffix = GetSizeSuffix(op.Type);

        // Load left to D0, right to D1
        EmitLoadValue(op.Left, M68kRegister.D0, op.Type);
        EmitLoadValue(op.Right, M68kRegister.D1, op.Type);

        // Compare
        _output.AppendLine($"    cmp{suffix}   d1,d0");

        // Set result based on condition
        // Use Scc instruction (Set according to condition)
        string condition = op.Operation switch
        {
            IrBinaryOp.OpKind.Eq => "eq",  // Equal
            IrBinaryOp.OpKind.Ne => "ne",  // Not equal
            IrBinaryOp.OpKind.Lt => GetLtCondition(op.Type),
            IrBinaryOp.OpKind.Le => GetLeCondition(op.Type),
            IrBinaryOp.OpKind.Gt => GetGtCondition(op.Type),
            IrBinaryOp.OpKind.Ge => GetGeCondition(op.Type),
            _ => throw new InvalidOperationException($"Invalid comparison: {op.Operation}")
        };

        // Clear D0 and set to 0xFF if condition is true
        _output.AppendLine($"    s{condition}     d0              ; Set byte if {condition}");
        _output.AppendLine($"    neg.b     d0              ; Convert 0xFF to 0x01");

        // Store result (boolean)
        EmitStoreRegister(M68kRegister.D0, resultVar, IrBoolType.Instance);
    }

    private static string GetLtCondition(IrType type)
    {
        return (type is IrIntType intType && intType.IsSigned) ? "lt" : "cs"; // Signed: less than, Unsigned: carry set
    }

    private static string GetLeCondition(IrType type)
    {
        return (type is IrIntType intType && intType.IsSigned) ? "le" : "ls"; // Signed: less or equal, Unsigned: lower or same
    }

    private static string GetGtCondition(IrType type)
    {
        return (type is IrIntType intType && intType.IsSigned) ? "gt" : "hi"; // Signed: greater than, Unsigned: higher
    }

    private static string GetGeCondition(IrType type)
    {
        return (type is IrIntType intType && intType.IsSigned) ? "ge" : "cc"; // Signed: greater or equal, Unsigned: carry clear
    }

    /// <summary>
    /// Load a value into a register
    /// </summary>
    public void EmitLoadValue(IrValue value, M68kRegister destReg, IrType type)
    {
        string suffix = GetSizeSuffix(type);
        string reg = destReg.ToAsmString();

        switch (value)
        {
            case IrConstant constant:
                // Load immediate constant
                if (constant.Value >= -128 && constant.Value <= 127 && destReg.IsDataRegister())
                {
                    _output.AppendLine($"    moveq     #{constant.Value},{reg}");
                }
                else
                {
                    _output.AppendLine($"    move{suffix}   #{constant.Value},{reg}");
                }
                break;

            case IrBoolConstant boolConstant:
                _output.AppendLine($"    moveq     #{(boolConstant.Value ? 1 : 0)},{reg}");
                break;

            case IrVariable variable:
                // Load from stack
                int offset = _allocator.GetStackOffset(variable.Name);
                _output.AppendLine($"    move{suffix}   {offset}(a5),{reg}");
                break;

            case IrStringLiteral stringLit:
                // Load address of string literal
                _output.AppendLine($"    lea       {stringLit.Label}(pc),{reg}");
                break;

            default:
                throw new NotImplementedException($"Load of {value.GetType().Name} not implemented");
        }
    }

    /// <summary>
    /// Store a register to a variable
    /// </summary>
    public void EmitStoreRegister(M68kRegister srcReg, string varName, IrType type)
    {
        string suffix = GetSizeSuffix(type);
        string reg = srcReg.ToAsmString();
        int offset = _allocator.GetStackOffset(varName);

        _output.AppendLine($"    move{suffix}   {reg},{offset}(a5)");
    }

    /// <summary>
    /// Emit a conditional branch
    /// </summary>
    public void EmitConditionalBranch(IrValue condition, string trueLabel, string falseLabel)
    {
        // Load condition to D0
        EmitLoadValue(condition, M68kRegister.D0, condition.Type);

        // Test and branch
        _output.AppendLine($"    tst.b     d0");
        _output.AppendLine($"    bne       {trueLabel}");
        _output.AppendLine($"    bra       {falseLabel}");
    }

    /// <summary>
    /// Emit an unconditional branch
    /// </summary>
    public void EmitBranch(string label)
    {
        _output.AppendLine($"    bra       {label}");
    }

    /// <summary>
    /// Emit a label
    /// </summary>
    public void EmitLabel(string label)
    {
        _output.AppendLine($"{label}:");
    }

    /// <summary>
    /// Emit a function call
    /// </summary>
    public void EmitCall(IrCall call)
    {
        // For prototype: push arguments right-to-left on stack
        // TODO: Use registers for first few arguments per Amiga ABI

        int stackAdjust = 0;

        // Push arguments in reverse order
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            var arg = call.Arguments[i];
            EmitLoadValue(arg, M68kRegister.D0, arg.Type);

            string suffix = GetSizeSuffix(arg.Type);
            _output.AppendLine($"    move{suffix}   d0,-(sp)");
            stackAdjust += arg.Type.SizeInBytes;

            // Align to word boundary
            if (arg.Type.SizeInBytes % 2 != 0)
            {
                _output.AppendLine($"    subq.l    #1,sp           ; Align to word");
                stackAdjust++;
            }
        }

        // Call function
        _output.AppendLine($"    jsr       {call.FunctionName}");

        // Clean up stack
        if (stackAdjust > 0)
        {
            if (stackAdjust <= 8)
            {
                _output.AppendLine($"    addq.l    #{stackAdjust},sp");
            }
            else
            {
                _output.AppendLine($"    add.l     #{stackAdjust},sp");
            }
        }

        // Result is in D0 - store if needed
        if (call.ResultName != null)
        {
            EmitStoreRegister(M68kRegister.D0, call.ResultName, call.ReturnType);
        }
    }

    /// <summary>
    /// Emit a return instruction
    /// </summary>
    public void EmitReturn(IrReturn ret)
    {
        if (ret.Value != null)
        {
            // Load return value to D0
            EmitLoadValue(ret.Value, M68kRegister.D0, ret.Value.Type);
        }

        // Epilogue will be emitted by function generator
    }
}
