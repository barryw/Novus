using System.Text;
using Novus.IR;

namespace Novus.Codegen.M68k;

/// <summary>
/// Exception thrown when attempting to use 64-bit operations on a CPU that doesn't support them.
/// </summary>
public class M68k64BitNotSupportedException : NotSupportedException
{
    public string CpuTarget { get; }
    public string Operation { get; }
    public string TypeName { get; }

    public M68k64BitNotSupportedException(string cpuTarget, string operation, string typeName)
        : base($"64-bit {operation} for type '{typeName}' is not supported on {cpuTarget}. " +
               $"The 68000/68010/68020/68030/68040/68060 processors do not have native 64-bit arithmetic. " +
               $"Consider using i32/u32 types or implementing software 64-bit routines.")
    {
        CpuTarget = cpuTarget;
        Operation = operation;
        TypeName = typeName;
    }
}

/// <summary>
/// Selects 68k instructions for IR operations.
/// Handles instruction selection and operand sizing (byte/word/long).
///
/// 64-bit Type Support:
/// ====================
/// The 68000 family does NOT have native 64-bit arithmetic instructions.
/// - 68000/68010: Only 16x16->32 multiply, 32/16->16 divide
/// - 68020+: 32x32->64 multiply, 64/32->32 divide (partial support)
///
/// For full 64-bit arithmetic (i64/u64), software routines from runtime_softmath64.c are used:
/// - __adddi3/__subdi3: 64-bit add/subtract
/// - __muldi3: 64-bit multiply
/// - __udivdi3/__divdi3: Unsigned/signed 64-bit divide
/// - __umoddi3/__moddi3: Unsigned/signed 64-bit modulo
/// - __ashldi3/__lshrdi3/__ashrdi3: 64-bit shifts
/// - __ucmpdi2/__cmpdi2: 64-bit comparisons
///
/// 64-bit values are passed as register pairs (D0:D1 for first arg, D2:D3 for second, etc.)
/// or on the stack as two consecutive 32-bit values (high word first).
/// </summary>
public class InstructionSelector
{
    private readonly RegisterAllocator _allocator;
    private readonly StringBuilder _output;
    private readonly string _cpuTarget;
    private readonly HashSet<string> _externalReferences = new();

    /// <summary>
    /// External symbols referenced by the generated code (e.g., runtime routines like __mulsi3)
    /// </summary>
    public IReadOnlySet<string> ExternalReferences => _externalReferences;

    public InstructionSelector(RegisterAllocator allocator, StringBuilder output, string cpuTarget = "68020")
    {
        _allocator = allocator;
        _output = output;
        _cpuTarget = cpuTarget;
    }

    /// <summary>
    /// Get the size suffix for a type (.b, .w, .l)
    /// Note: 64-bit types (8 bytes) return ".l" but operations on them
    /// require special handling with register pairs or software routines.
    /// </summary>
    public static string GetSizeSuffix(IrType type)
    {
        int size = type.SizeInBytes;
        return size switch
        {
            1 => ".b",      // byte
            2 => ".w",      // word
            4 => ".l",      // long
            8 => ".l",      // 64-bit: use .l but operations need special handling
            _ => ".l"       // default to long for larger types
        };
    }

    /// <summary>
    /// Check if a type is 64-bit (i64 or u64) and throw if 64-bit ops aren't supported.
    /// </summary>
    private void Check64BitSupport(IrType type, string operation)
    {
        if (type.SizeInBytes == 8)
        {
            throw new M68k64BitNotSupportedException(_cpuTarget, operation, type.Name);
        }
    }

    /// <summary>
    /// Check if a type is 64-bit and emit a warning comment if so.
    /// Returns true if the type is 64-bit.
    /// </summary>
    private bool Is64BitType(IrType type) => type.SizeInBytes == 8;

    /// <summary>
    /// Emit a binary operation
    /// </summary>
    public void EmitBinaryOp(IrBinaryOp op, string resultVar)
    {
        string suffix = GetSizeSuffix(op.Type);

        // Handle 64-bit operations via runtime library calls
        if (Is64BitType(op.Type))
        {
            Emit64BitBinaryOp(op, resultVar);
            return;
        }

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
            // 32-bit multiply: d0 * d1 -> d0
            // Use __mulsi3 runtime routine (GCC convention)
            // Args: d0 = multiplicand, d1 = multiplier
            // Result: d0 = product (low 32 bits)
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            _externalReferences.Add("__mulsi3");
            _output.AppendLine($"    jsr       __mulsi3        ; d0 = d0 * d1 (32-bit)");
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
            // 32-bit divide: d0 / d1 -> d0
            // Use __divsi3 (signed) or __udivsi3 (unsigned) runtime routine
            // Args: d0 = dividend, d1 = divisor
            // Result: d0 = quotient
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            var routine = isSigned ? "__divsi3" : "__udivsi3";
            _externalReferences.Add(routine);
            _output.AppendLine($"    jsr       {routine}        ; d0 = d0 / d1 (32-bit)");
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
            // 32-bit modulo: d0 % d1 -> d0
            // Use __modsi3 (signed) or __umodsi3 (unsigned) runtime routine
            // Args: d0 = dividend, d1 = divisor
            // Result: d0 = remainder
            EmitLoadValue(right, M68kRegister.D1, right.Type);
            var routine = isSigned ? "__modsi3" : "__umodsi3";
            _externalReferences.Add(routine);
            _output.AppendLine($"    jsr       {routine}        ; d0 = d0 %% d1 (32-bit)");
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
    /// Emit a function call using Amiga ABI register convention.
    ///
    /// Amiga ABI for function calls:
    /// - First 4 integer/pointer args go in d0, d1, a0, a1 (in that order)
    /// - Additional arguments go on stack right-to-left
    /// - Return value in d0 (d0/d1 for 64-bit)
    /// </summary>
    public void EmitCall(IrCall call)
    {
        // Register assignment order per Amiga ABI
        var dataRegs = new[] { M68kRegister.D0, M68kRegister.D1 };
        var addrRegs = new[] { M68kRegister.A0, M68kRegister.A1 };

        int dataRegIndex = 0;
        int addrRegIndex = 0;
        int stackAdjust = 0;

        // Track which args go in registers vs stack
        var regAssignments = new List<(int argIndex, M68kRegister reg)>();
        var stackArgs = new List<int>();

        for (int i = 0; i < call.Arguments.Count; i++)
        {
            var arg = call.Arguments[i];
            bool isPointer = arg.Type is IrPointerType or IrReferenceType or IrMutReferenceType;

            if (isPointer && addrRegIndex < addrRegs.Length)
            {
                // Pointer/reference goes in address register
                regAssignments.Add((i, addrRegs[addrRegIndex++]));
            }
            else if (!isPointer && dataRegIndex < dataRegs.Length)
            {
                // Integer/other goes in data register
                regAssignments.Add((i, dataRegs[dataRegIndex++]));
            }
            else
            {
                // No more registers - goes on stack
                stackArgs.Add(i);
            }
        }

        // Push stack arguments in reverse order (right-to-left)
        for (int i = stackArgs.Count - 1; i >= 0; i--)
        {
            var argIndex = stackArgs[i];
            var arg = call.Arguments[argIndex];
            EmitLoadValue(arg, M68kRegister.D0, arg.Type);

            string suffix = GetSizeSuffix(arg.Type);
            _output.AppendLine($"    move{suffix}   d0,-(sp)");
            stackAdjust += Math.Max(arg.Type.SizeInBytes, 2); // Minimum 2-byte push

            // Ensure word alignment
            if (arg.Type.SizeInBytes == 1)
            {
                stackAdjust++; // Already pushed word, count the padding
            }
        }

        // Load register arguments (in order, to avoid clobbering)
        // We need to be careful: loading into d1 before d0 if d0 is needed for d1's load
        foreach (var (argIndex, reg) in regAssignments)
        {
            var arg = call.Arguments[argIndex];
            EmitLoadValue(arg, reg, arg.Type);
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

    // ============================================================================
    // 64-BIT SOFT-MATH SUPPORT
    // ============================================================================

    /// <summary>
    /// Emit a 64-bit binary operation using runtime library calls.
    /// 64-bit values are passed as two 32-bit values (high, low) on the stack.
    /// Result is returned in D0:D1 (D0=high, D1=low).
    /// </summary>
    private void Emit64BitBinaryOp(IrBinaryOp op, string resultVar)
    {
        bool isSigned = op.Type is IrIntType intType && intType.IsSigned;

        // Get the runtime function name for this operation
        string? funcName = op.Operation switch
        {
            IrBinaryOp.OpKind.Add => "__adddi3",
            IrBinaryOp.OpKind.Sub => "__subdi3",
            IrBinaryOp.OpKind.Mul => "__muldi3",
            IrBinaryOp.OpKind.Div => isSigned ? "__divdi3" : "__udivdi3",
            IrBinaryOp.OpKind.Mod => isSigned ? "__moddi3" : "__umoddi3",
            IrBinaryOp.OpKind.Shl => "__ashldi3",
            IrBinaryOp.OpKind.Shr => isSigned ? "__ashrdi3" : "__lshrdi3",
            IrBinaryOp.OpKind.And => null,  // Inline implementation
            IrBinaryOp.OpKind.Or => null,   // Inline implementation
            IrBinaryOp.OpKind.Xor => null,  // Inline implementation
            IrBinaryOp.OpKind.Eq or IrBinaryOp.OpKind.Ne or
            IrBinaryOp.OpKind.Lt or IrBinaryOp.OpKind.Le or
            IrBinaryOp.OpKind.Gt or IrBinaryOp.OpKind.Ge => isSigned ? "__cmpdi2" : "__ucmpdi2",
            _ => throw new NotImplementedException($"64-bit operation {op.Operation} not implemented")
        };

        // Handle logical operations inline (no function call needed)
        if (funcName == null)
        {
            Emit64BitLogicalOp(op, resultVar);
            return;
        }

        // Handle comparisons specially
        if (op.Operation is IrBinaryOp.OpKind.Eq or IrBinaryOp.OpKind.Ne or
            IrBinaryOp.OpKind.Lt or IrBinaryOp.OpKind.Le or
            IrBinaryOp.OpKind.Gt or IrBinaryOp.OpKind.Ge)
        {
            Emit64BitComparison(op, resultVar, funcName);
            return;
        }

        // Handle shift operations (second operand is just shift count, not 64-bit)
        if (op.Operation is IrBinaryOp.OpKind.Shl or IrBinaryOp.OpKind.Shr)
        {
            Emit64BitShift(op, resultVar, funcName);
            return;
        }

        _output.AppendLine($"    ; 64-bit {op.Operation}: call {funcName}");

        // Push right operand (b_hi, b_lo) - in reverse order for stack
        Emit64BitPush(op.Right);

        // Push left operand (a_hi, a_lo)
        Emit64BitPush(op.Left);

        // Call the runtime function
        _output.AppendLine($"    jsr       {funcName}");

        // Clean up stack (4 x 4 bytes = 16 bytes for two 64-bit values)
        _output.AppendLine($"    lea       16(sp),sp");

        // Store result from D0:D1 to result variable
        // D0 = high 32 bits, D1 = low 32 bits
        Emit64BitStore(resultVar);
    }

    /// <summary>
    /// Push a 64-bit value onto the stack (high word first, then low word).
    /// </summary>
    private void Emit64BitPush(IrValue value)
    {
        if (value is IrConstant constant)
        {
            // Push 64-bit constant as two 32-bit immediates
            long val = constant.Value;
            uint hi = (uint)(val >> 32);
            uint lo = (uint)(val & 0xFFFFFFFF);

            _output.AppendLine($"    move.l    #${lo:X8},-(sp)     ; low");
            _output.AppendLine($"    move.l    #${hi:X8},-(sp)     ; high");
        }
        else if (value is IrVariable variable)
        {
            // Load from stack - 64-bit vars occupy 8 bytes
            int offset = _allocator.GetStackOffset(variable.Name);

            // Push low word first (will be at higher address)
            _output.AppendLine($"    move.l    {offset + 4}(a5),-(sp)  ; low");
            _output.AppendLine($"    move.l    {offset}(a5),-(sp)      ; high");
        }
        else
        {
            throw new NotImplementedException($"64-bit push of {value.GetType().Name} not implemented");
        }
    }

    /// <summary>
    /// Store D0:D1 (64-bit result) to a variable.
    /// </summary>
    private void Emit64BitStore(string varName)
    {
        int offset = _allocator.GetStackOffset(varName);

        // D0 = high, D1 = low
        _output.AppendLine($"    move.l    d0,{offset}(a5)        ; high");
        _output.AppendLine($"    move.l    d1,{offset + 4}(a5)    ; low");
    }

    /// <summary>
    /// Emit 64-bit logical operations (AND, OR, XOR) inline without function call.
    /// These can be done with two 32-bit operations.
    /// </summary>
    private void Emit64BitLogicalOp(IrBinaryOp op, string resultVar)
    {
        string opName = op.Operation switch
        {
            IrBinaryOp.OpKind.And => "and",
            IrBinaryOp.OpKind.Or => "or",
            IrBinaryOp.OpKind.Xor => "eor",
            _ => throw new InvalidOperationException()
        };

        _output.AppendLine($"    ; 64-bit {op.Operation} (inline)");

        // Load left operand to D0:D1
        Emit64BitLoad(op.Left, "d0", "d1");

        // Load right operand to D2:D3
        Emit64BitLoad(op.Right, "d2", "d3");

        // Perform operation on both halves
        _output.AppendLine($"    {opName}.l   d2,d0        ; high");
        _output.AppendLine($"    {opName}.l   d3,d1        ; low");

        // Store result
        Emit64BitStore(resultVar);
    }

    /// <summary>
    /// Load a 64-bit value into a register pair.
    /// </summary>
    private void Emit64BitLoad(IrValue value, string regHi, string regLo)
    {
        if (value is IrConstant constant)
        {
            long val = constant.Value;
            uint hi = (uint)(val >> 32);
            uint lo = (uint)(val & 0xFFFFFFFF);

            if (hi <= 127)
                _output.AppendLine($"    moveq     #{hi},{regHi}");
            else
                _output.AppendLine($"    move.l    #${hi:X8},{regHi}");

            if (lo <= 127)
                _output.AppendLine($"    moveq     #{lo},{regLo}");
            else
                _output.AppendLine($"    move.l    #${lo:X8},{regLo}");
        }
        else if (value is IrVariable variable)
        {
            int offset = _allocator.GetStackOffset(variable.Name);
            _output.AppendLine($"    move.l    {offset}(a5),{regHi}      ; high");
            _output.AppendLine($"    move.l    {offset + 4}(a5),{regLo}  ; low");
        }
        else
        {
            throw new NotImplementedException($"64-bit load of {value.GetType().Name} not implemented");
        }
    }

    /// <summary>
    /// Emit 64-bit shift operation. Shift count is 32-bit, not 64-bit.
    /// </summary>
    private void Emit64BitShift(IrBinaryOp op, string resultVar, string funcName)
    {
        _output.AppendLine($"    ; 64-bit shift: call {funcName}");

        // Push shift count (just 32-bit value)
        if (op.Right is IrConstant constant)
        {
            _output.AppendLine($"    move.l    #{constant.Value},-(sp)   ; shift count");
        }
        else
        {
            EmitLoadValue(op.Right, M68kRegister.D0, IrIntType.I32);
            _output.AppendLine($"    move.l    d0,-(sp)                  ; shift count");
        }

        // Push 64-bit value to shift
        Emit64BitPush(op.Left);

        // Call shift function
        _output.AppendLine($"    jsr       {funcName}");

        // Clean up stack (8 bytes for 64-bit + 4 bytes for shift count = 12)
        _output.AppendLine($"    lea       12(sp),sp");

        // Store result
        Emit64BitStore(resultVar);
    }

    /// <summary>
    /// Emit 64-bit comparison. Returns boolean (0 or 1).
    /// </summary>
    private void Emit64BitComparison(IrBinaryOp op, string resultVar, string funcName)
    {
        _output.AppendLine($"    ; 64-bit comparison: call {funcName}");

        // Push right operand
        Emit64BitPush(op.Right);

        // Push left operand
        Emit64BitPush(op.Left);

        // Call comparison function (returns -1, 0, or 1 in D0)
        _output.AppendLine($"    jsr       {funcName}");

        // Clean up stack
        _output.AppendLine($"    lea       16(sp),sp");

        // Convert comparison result to boolean based on operation
        switch (op.Operation)
        {
            case IrBinaryOp.OpKind.Eq:
                _output.AppendLine($"    tst.l     d0");
                _output.AppendLine($"    seq       d0          ; Set if equal (result was 0)");
                break;
            case IrBinaryOp.OpKind.Ne:
                _output.AppendLine($"    tst.l     d0");
                _output.AppendLine($"    sne       d0          ; Set if not equal (result was non-zero)");
                break;
            case IrBinaryOp.OpKind.Lt:
                _output.AppendLine($"    tst.l     d0");
                _output.AppendLine($"    smi       d0          ; Set if minus (result was -1)");
                break;
            case IrBinaryOp.OpKind.Le:
                _output.AppendLine($"    cmpi.l    #1,d0");
                _output.AppendLine($"    sne       d0          ; Set if not greater (result was not 1)");
                break;
            case IrBinaryOp.OpKind.Gt:
                _output.AppendLine($"    cmpi.l    #1,d0");
                _output.AppendLine($"    seq       d0          ; Set if greater (result was 1)");
                break;
            case IrBinaryOp.OpKind.Ge:
                _output.AppendLine($"    tst.l     d0");
                _output.AppendLine($"    spl       d0          ; Set if plus or zero (result was 0 or 1)");
                break;
        }

        // Normalize to 0 or 1
        _output.AppendLine($"    neg.b     d0          ; Convert 0xFF to 0x01");

        // Store boolean result (only low byte matters)
        EmitStoreRegister(M68kRegister.D0, resultVar, IrBoolType.Instance);
    }

    // ============================================================================
    // ARRAY AND STRUCT ACCESS
    // ============================================================================

    /// <summary>
    /// Emit array index access: result = array[index]
    /// </summary>
    public void EmitIndexAccess(IrIndexAccess indexAccess)
    {
        _output.AppendLine($"    ; Index access: {indexAccess.ResultName} = array[index]");

        // Load array base address to A0
        EmitLoadValue(indexAccess.Array, M68kRegister.A0, indexAccess.Array.Type);

        // Load index to D1
        EmitLoadValue(indexAccess.Index, M68kRegister.D1, indexAccess.Index.Type);

        // Calculate element size
        var elemSize = indexAccess.ElementType.SizeInBytes;

        // Compute offset: index * element_size
        if (elemSize == 1)
        {
            // No multiplication needed
        }
        else if (elemSize == 2)
        {
            _output.AppendLine($"    add.l     d1,d1           ; index * 2");
        }
        else if (elemSize == 4)
        {
            _output.AppendLine($"    lsl.l     #2,d1           ; index * 4");
        }
        else
        {
            // General case: multiply
            _output.AppendLine($"    move.l    #{elemSize},d0");
            _externalReferences.Add("__mulsi3");
            _output.AppendLine($"    jsr       __mulsi3        ; d0 = index * size");
            _output.AppendLine($"    move.l    d0,d1");
        }

        // Load element from array[index]
        var suffix = GetSizeSuffix(indexAccess.ElementType);
        _output.AppendLine($"    move{suffix}   (a0,d1.l),d0   ; Load array[index]");

        // Store result
        EmitStoreRegister(M68kRegister.D0, indexAccess.ResultName, indexAccess.ElementType);
    }

    /// <summary>
    /// Emit array index store: array[index] = value
    /// </summary>
    public void EmitIndexStore(IrIndexStore indexStore)
    {
        _output.AppendLine($"    ; Index store: array[index] = value");

        // Load value to D0
        EmitLoadValue(indexStore.Value, M68kRegister.D0, indexStore.Value.Type);

        // Load array base address to A0
        EmitLoadValue(indexStore.Array, M68kRegister.A0, indexStore.Array.Type);

        // Load index to D1
        EmitLoadValue(indexStore.Index, M68kRegister.D1, indexStore.Index.Type);

        // Get element type from array type
        var arrayType = indexStore.Array.Type as IrArrayType;
        var elemType = arrayType?.ElementType ?? indexStore.Value.Type;
        var elemSize = elemType.SizeInBytes;

        // Compute offset
        if (elemSize == 2)
        {
            _output.AppendLine($"    add.l     d1,d1           ; index * 2");
        }
        else if (elemSize == 4)
        {
            _output.AppendLine($"    lsl.l     #2,d1           ; index * 4");
        }
        else if (elemSize > 4)
        {
            _output.AppendLine($"    move.l    d0,d2           ; Save value");
            _output.AppendLine($"    move.l    #{elemSize},d0");
            _externalReferences.Add("__mulsi3");
            _output.AppendLine($"    jsr       __mulsi3");
            _output.AppendLine($"    move.l    d0,d1");
            _output.AppendLine($"    move.l    d2,d0           ; Restore value");
        }

        // Store value to array[index]
        var suffix = GetSizeSuffix(elemType);
        _output.AppendLine($"    move{suffix}   d0,(a0,d1.l)   ; Store array[index]");
    }

    /// <summary>
    /// Emit struct member access: result = struct.field
    /// </summary>
    public void EmitMemberAccess(IrMemberAccess memberAccess)
    {
        _output.AppendLine($"    ; Member access: {memberAccess.ResultName} = struct.{memberAccess.FieldName}");

        // Load struct address to A0
        EmitLoadValue(memberAccess.Struct, M68kRegister.A0, memberAccess.Struct.Type);

        // Get field offset
        var offset = GetFieldOffset(memberAccess.Struct.Type, memberAccess.FieldName);

        // Load field value
        var suffix = GetSizeSuffix(memberAccess.FieldType);
        if (offset == 0)
        {
            _output.AppendLine($"    move{suffix}   (a0),d0        ; Load field {memberAccess.FieldName}");
        }
        else
        {
            _output.AppendLine($"    move{suffix}   {offset}(a0),d0  ; Load field {memberAccess.FieldName}");
        }

        // Store result
        EmitStoreRegister(M68kRegister.D0, memberAccess.ResultName, memberAccess.FieldType);
    }

    /// <summary>
    /// Emit struct member store: struct.field = value
    /// </summary>
    public void EmitMemberStore(IrMemberStore memberStore)
    {
        _output.AppendLine($"    ; Member store: struct.{memberStore.FieldName} = value");

        // Load value to D0
        EmitLoadValue(memberStore.Value, M68kRegister.D0, memberStore.Value.Type);

        // Load struct address to A0
        EmitLoadValue(memberStore.Struct, M68kRegister.A0, memberStore.Struct.Type);

        // Get field offset
        var offset = GetFieldOffset(memberStore.Struct.Type, memberStore.FieldName);

        // Store value to field
        var suffix = GetSizeSuffix(memberStore.Value.Type);
        if (offset == 0)
        {
            _output.AppendLine($"    move{suffix}   d0,(a0)        ; Store field {memberStore.FieldName}");
        }
        else
        {
            _output.AppendLine($"    move{suffix}   d0,{offset}(a0)  ; Store field {memberStore.FieldName}");
        }
    }

    /// <summary>
    /// Get the byte offset of a field within a struct type.
    /// </summary>
    private int GetFieldOffset(IrType structType, string fieldName)
    {
        if (structType is IrStructType st)
        {
            int offset = 0;
            foreach (var field in st.Fields)
            {
                // Align to field's natural alignment
                int alignment = Math.Min(field.Type.SizeInBytes, 4);
                if (alignment > 0)
                {
                    offset = (offset + alignment - 1) / alignment * alignment;
                }

                if (field.Name == fieldName)
                {
                    return offset;
                }
                offset += field.Type.SizeInBytes;
            }
        }
        // If we can't determine offset, emit a comment
        _output.AppendLine($"    ; WARNING: Could not determine offset for field {fieldName}");
        return 0;
    }

    // ============================================================================
    // ASSERT AND PANIC
    // ============================================================================

    /// <summary>
    /// Emit assertion check: if (!condition) call __novus_assert_failed
    /// </summary>
    public void EmitAssert(IrAssert assertInst)
    {
        _output.AppendLine($"    ; Assert");

        // Load condition to D0
        EmitLoadValue(assertInst.Condition, M68kRegister.D0, assertInst.Condition.Type);

        // Test condition
        _output.AppendLine($"    tst.b     d0");

        // Generate unique label
        var okLabel = $"_assert_ok_{_labelCounter++}";

        // Branch if condition is true (non-zero)
        _output.AppendLine($"    bne       {okLabel}");

        // Condition is false - call assert handler
        // Push file, line, column, message (null for now)
        _output.AppendLine($"    pea       0               ; message = NULL");
        _output.AppendLine($"    move.l    #0,-(sp)        ; column");
        _output.AppendLine($"    move.l    #0,-(sp)        ; line");
        _output.AppendLine($"    pea       _str_file       ; file");
        _externalReferences.Add("__novus_assert_failed");
        _output.AppendLine($"    jsr       __novus_assert_failed");
        _output.AppendLine($"    lea       16(sp),sp");

        _output.AppendLine($"{okLabel}:");
    }

    /// <summary>
    /// Emit panic call
    /// </summary>
    public void EmitPanic(IrPanic panicInst)
    {
        _output.AppendLine($"    ; Panic: {panicInst.Message}");

        // Push message, file, line, column
        _output.AppendLine($"    move.l    #0,-(sp)        ; column");
        _output.AppendLine($"    move.l    #0,-(sp)        ; line");
        _output.AppendLine($"    pea       _str_file       ; file");

        // Push message string (need to emit string literal)
        _output.AppendLine($"    pea       _panic_msg      ; message");

        _externalReferences.Add("__novus_panic");
        _output.AppendLine($"    jsr       __novus_panic");
        _output.AppendLine($"    lea       16(sp),sp");
    }

    private int _labelCounter = 0;

    // ============================================================================
    // INDIRECT CALLS
    // ============================================================================

    /// <summary>
    /// Emit indirect function call through function pointer
    /// </summary>
    public void EmitIndirectCall(IrIndirectCall indirectCall)
    {
        _output.AppendLine($"    ; Indirect call through function pointer");

        // Load function pointer to A0
        EmitLoadValue(indirectCall.FunctionPointer, M68kRegister.A0, indirectCall.FunctionPointer.Type);

        // Push arguments (right to left)
        int stackAdjust = 0;
        for (int i = indirectCall.Arguments.Count - 1; i >= 0; i--)
        {
            var arg = indirectCall.Arguments[i];
            EmitLoadValue(arg, M68kRegister.D0, arg.Type);
            var suffix = GetSizeSuffix(arg.Type);
            _output.AppendLine($"    move{suffix}   d0,-(sp)");
            stackAdjust += Math.Max(arg.Type.SizeInBytes, 2);
        }

        // Call through function pointer
        _output.AppendLine($"    jsr       (a0)");

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

        // Store result if needed
        if (indirectCall.ResultName != null)
        {
            EmitStoreRegister(M68kRegister.D0, indirectCall.ResultName, indirectCall.ReturnType);
        }
    }
}
