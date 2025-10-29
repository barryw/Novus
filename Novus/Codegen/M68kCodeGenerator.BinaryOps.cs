using System.Text;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Binary and unary operations for M68kCodeGenerator (partial class)
/// Contains code generation for arithmetic, logical, and comparison operations
/// </summary>
public partial class M68kCodeGenerator
{
    private void GenerateBinaryOp(IrBinaryOp binOp, IList<IrInstruction> instructions, int index)
    {
        var size = GetSizeSuffix(binOp.Type);
        bool isFloatOp = binOp.Type is IrFloatType;

        // Simplified code generation - load operands and perform operation
        // Real implementation would do proper register allocation

        switch (binOp.Operation)
        {
            case IrBinaryOp.OpKind.Add:
                EmitComment($"{binOp.ResultName} = add");

                // Save previous d0 value if it will be overwritten and not used by this operation
                {
                    bool leftInD0 = binOp.Left is IrVariable leftVar && leftVar.Name == _lastResultInD0;
                    bool rightInD0 = binOp.Right is IrVariable rightVar && rightVar.Name == _lastResultInD0;
                    if (_lastResultInD0 != null && !leftInD0 && !rightInD0)
                    {
                        EmitComment($"Save previous d0 value ({_lastResultInD0}) before arithmetic operation");
                        Emit("\tmove.l\td0,-(sp)");
                        _savedTemps.Add(_lastResultInD0);
                        _tempStackOffset += 4;
                        _lastResultInD0 = null;
                    }
                }

                if (_generatingFpuVersion && isFloatOp)
                {
                    // Hardware FPU addition
                    LoadOperand(binOp.Left, "fp0");
                    LoadOperand(binOp.Right, "fp1");
                    Emit("\tfadd.x\tfp1,fp0");
                    // Convert to d0 for storage
                    Emit("\tfmove.l\tfp0,d0");
                }
                else if (isFloatOp)
                {
                    // Soft-float addition - call VBCC library function
                    // Arguments: float a, float b (as IEEE-754 bit patterns in d0, d1)
                    // Returns: float result in d0
                    LoadOperand(binOp.Right, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push right operand
                    LoadOperand(binOp.Left, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push left operand

                    // Call VBCC soft-float functions: __ieeeaddl for f32, __ieeeaddd for f64
                    var funcName = binOp.Type is IrFloatType ft && ft.BitWidth == 32 ? "__ieeeaddl" : "__ieeeaddd";
                    Emit($"\tjsr\t{funcName}");
                    Emit("\taddq.l\t#8,sp");     // Clean up stack (2 * 4 bytes)
                    // Result is in d0
                }
                else if (binOp.Left.Type is IrPointerType ptrType)
                {
                    // Pointer arithmetic: ptr + offset
                    // Need to scale offset by element size
                    var elementSize = ptrType.PointeeType.SizeInBytes;

                    EmitComment($"Pointer arithmetic: ptr + offset (element size = {elementSize})");

                    // Load pointer into a0
                    LoadOperand(binOp.Left, "a0");

                    // Load and scale the offset
                    if (binOp.Right is IrConstant constOffset)
                    {
                        // Constant offset: compute scaled value at compile time
                        var scaledOffset = constOffset.Value * elementSize;
                        if (scaledOffset >= -32768 && scaledOffset <= 32767)
                        {
                            // Use LEA for small displacements
                            Emit($"\tlea\t{scaledOffset}(a0),a0\t; ptr + {constOffset.Value} * {elementSize}");
                        }
                        else
                        {
                            // Large displacement: use add
                            Emit($"\tmove.l\t#{scaledOffset},d1");
                            Emit($"\tadd.l\td1,a0");
                        }
                    }
                    else
                    {
                        // Variable offset: scale at runtime
                        LoadOperand(binOp.Right, "d1");

                        if (elementSize == 1)
                        {
                            // No scaling needed for byte pointers
                            Emit($"\tadd.l\td1,a0");
                        }
                        else if (elementSize == 2)
                        {
                            Emit($"\tadd.l\td1,d1\t; offset * 2");
                            Emit($"\tadd.l\td1,a0");
                        }
                        else if (elementSize == 4)
                        {
                            Emit($"\tadd.l\td1,d1\t; offset * 2");
                            Emit($"\tadd.l\td1,d1\t; offset * 4");
                            Emit($"\tadd.l\td1,a0");
                        }
                        else if (elementSize == 8)
                        {
                            Emit($"\tadd.l\td1,d1\t; offset * 2");
                            Emit($"\tadd.l\td1,d1\t; offset * 4");
                            Emit($"\tadd.l\td1,d1\t; offset * 8");
                            Emit($"\tadd.l\td1,a0");
                        }
                        else
                        {
                            // Use multiplication for other sizes
                            Emit($"\tmulu.w\t#{elementSize},d1");
                            Emit($"\tadd.l\td1,a0");
                        }
                    }

                    // Move result to d0
                    Emit($"\tmove.l\ta0,d0");
                }
                else
                {
                    // Regular integer addition
                    // OPTIMIZATION: If either operand is already in d0, don't reload it
                    bool leftInD0 = binOp.Left is IrVariable leftVar && leftVar.Name == _lastResultInD0;
                    bool rightInD0 = binOp.Right is IrVariable rightVar && rightVar.Name == _lastResultInD0;

                    if (leftInD0)
                    {
                        // Left operand already in d0, just load Right into d1
                        EmitComment($"Optimized: left operand already in d0");
                        LoadOperand(binOp.Right, "d1");
                        Emit($"\tadd{size}\td1,d0");
                    }
                    else if (rightInD0)
                    {
                        // Right operand already in d0, load Left into d1 and swap
                        EmitComment($"Optimized: right operand already in d0");
                        LoadOperand(binOp.Left, "d1");
                        Emit($"\tadd{size}\td1,d0");  // add is commutative
                    }
                    else
                    {
                        LoadOperand(binOp.Left, "d0");
                        LoadOperand(binOp.Right, "d1");
                        Emit($"\tadd{size}\td1,d0");
                    }
                }
                break;

            case IrBinaryOp.OpKind.Sub:
                EmitComment($"{binOp.ResultName} = sub");
                if (_generatingFpuVersion && isFloatOp)
                {
                    // Hardware FPU subtraction
                    LoadOperand(binOp.Left, "fp0");
                    LoadOperand(binOp.Right, "fp1");
                    Emit("\tfsub.x\tfp1,fp0");
                    // Convert to d0 for storage
                    Emit("\tfmove.l\tfp0,d0");
                }
                else if (isFloatOp)
                {
                    // Soft-float subtraction - call VBCC library function
                    LoadOperand(binOp.Right, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push right operand
                    LoadOperand(binOp.Left, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push left operand

                    var funcName = binOp.Type is IrFloatType ft && ft.BitWidth == 32 ? "__ieeesubl" : "__ieeesubd";
                    Emit($"\tjsr\t{funcName}");
                    Emit("\taddq.l\t#8,sp");
                }
                else if (binOp.Left.Type is IrPointerType ptrType)
                {
                    // Pointer arithmetic: ptr - offset
                    // Need to scale offset by element size
                    var elementSize = ptrType.PointeeType.SizeInBytes;

                    EmitComment($"Pointer arithmetic: ptr - offset (element size = {elementSize})");

                    // Load pointer into a0
                    LoadOperand(binOp.Left, "a0");

                    // Load and scale the offset
                    if (binOp.Right is IrConstant constOffset)
                    {
                        // Constant offset: compute scaled value at compile time
                        var scaledOffset = constOffset.Value * elementSize;
                        if (scaledOffset >= -32768 && scaledOffset <= 32767)
                        {
                            // Use LEA for small displacements (negative offset)
                            Emit($"\tlea\t{-scaledOffset}(a0),a0\t; ptr - {constOffset.Value} * {elementSize}");
                        }
                        else
                        {
                            // Large displacement: use sub
                            Emit($"\tmove.l\t#{scaledOffset},d1");
                            Emit($"\tsub.l\td1,a0");
                        }
                    }
                    else
                    {
                        // Variable offset: scale at runtime
                        LoadOperand(binOp.Right, "d1");

                        if (elementSize == 1)
                        {
                            // No scaling needed for byte pointers
                            Emit($"\tsub.l\td1,a0");
                        }
                        else if (elementSize == 2)
                        {
                            Emit($"\tadd.l\td1,d1\t; offset * 2");
                            Emit($"\tsub.l\td1,a0");
                        }
                        else if (elementSize == 4)
                        {
                            Emit($"\tadd.l\td1,d1\t; offset * 2");
                            Emit($"\tadd.l\td1,d1\t; offset * 4");
                            Emit($"\tsub.l\td1,a0");
                        }
                        else if (elementSize == 8)
                        {
                            Emit($"\tadd.l\td1,d1\t; offset * 2");
                            Emit($"\tadd.l\td1,d1\t; offset * 4");
                            Emit($"\tadd.l\td1,d1\t; offset * 8");
                            Emit($"\tsub.l\td1,a0");
                        }
                        else
                        {
                            // Use multiplication for other sizes
                            Emit($"\tmulu.w\t#{elementSize},d1");
                            Emit($"\tsub.l\td1,a0");
                        }
                    }

                    // Move result to d0
                    Emit($"\tmove.l\ta0,d0");
                }
                else
                {
                    // Integer subtraction
                    LoadOperand(binOp.Left, "d0");
                    LoadOperand(binOp.Right, "d1");
                    Emit($"\tsub{size}\td1,d0");
                }
                break;

            case IrBinaryOp.OpKind.Mul:
                EmitComment($"{binOp.ResultName} = mul");
                if (_generatingFpuVersion && isFloatOp)
                {
                    // Hardware FPU multiplication
                    LoadOperand(binOp.Left, "fp0");
                    LoadOperand(binOp.Right, "fp1");
                    Emit("\tfmul.x\tfp1,fp0");
                    // Convert to d0 for storage
                    Emit("\tfmove.l\tfp0,d0");
                }
                else if (isFloatOp)
                {
                    // Soft-float multiplication - call VBCC library function
                    LoadOperand(binOp.Right, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push right operand
                    LoadOperand(binOp.Left, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push left operand

                    var funcName = binOp.Type is IrFloatType ft && ft.BitWidth == 32 ? "__ieeemull" : "__ieeemuld";
                    Emit($"\tjsr\t{funcName}");
                    Emit("\taddq.l\t#8,sp");
                }
                else
                {
                    // Integer multiplication
                    LoadOperand(binOp.Left, "d0");
                    LoadOperand(binOp.Right, "d1");
                    GenerateMultiply(binOp);
                }
                break;

            case IrBinaryOp.OpKind.Div:
                EmitComment($"{binOp.ResultName} = div");
                if (_generatingFpuVersion && isFloatOp)
                {
                    // Hardware FPU division
                    LoadOperand(binOp.Left, "fp0");
                    LoadOperand(binOp.Right, "fp1");
                    Emit("\tfdiv.x\tfp1,fp0");
                    // Convert to d0 for storage
                    Emit("\tfmove.l\tfp0,d0");
                }
                else if (isFloatOp)
                {
                    // Soft-float division - call VBCC library function
                    LoadOperand(binOp.Right, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push right operand
                    LoadOperand(binOp.Left, "d0");
                    Emit("\tmove.l\td0,-(sp)");  // Push left operand

                    var funcName = binOp.Type is IrFloatType ft && ft.BitWidth == 32 ? "__ieeedivl" : "__ieeedivd";
                    Emit($"\tjsr\t{funcName}");
                    Emit("\taddq.l\t#8,sp");
                }
                else
                {
                    // Integer division
                    LoadOperand(binOp.Left, "d0");
                    LoadOperand(binOp.Right, "d1");
                    GenerateDivide(binOp);
                }
                break;

            case IrBinaryOp.OpKind.Mod:
                EmitComment($"{binOp.ResultName} = mod");
                if (isFloatOp)
                {
                    throw new NotImplementedException("Modulo operation not supported for floating-point types");
                }
                else
                {
                    // Integer modulo - use divs.l/divu.l which returns quotient:remainder in d0:d1
                    LoadOperand(binOp.Left, "d0");
                    LoadOperand(binOp.Right, "d1");

                    var isSigned = binOp.Type is IrIntType intType && intType.IsSigned;
                    var divOp = isSigned ? "divsl" : "divul";

                    if (_cpuTarget == "68000")
                    {
                        // 68000 doesn't have 32-bit divide with remainder
                        // We need to implement modulo using: a % b = a - (a / b) * b
                        Emit("\tmovem.l\td2-d3,-(sp)");
                        Emit("\tmove.l\td0,d2\t; Save dividend");
                        Emit("\tmove.l\td1,d3\t; Save divisor");

                        if (isSigned)
                        {
                            // Call division: d0 / d1 -> d0 (quotient in d0, d1 preserved)
                            Emit("\tjsr\t__div_i32\t; d0 = d0 / d1");
                            // Now: d0 = quotient, d2 = original dividend, d3 = divisor
                            // Multiply quotient * divisor
                            Emit("\tmove.l\td3,d1\t; d1 = divisor");
                            Emit("\tjsr\t__mul_i32\t; d0 = quotient * divisor");
                            // Subtract from original dividend
                            Emit("\tmove.l\td2,d1\t; d1 = original dividend");
                            Emit("\tsub.l\td0,d1\t; remainder = dividend - (quotient * divisor)");
                            Emit("\tmove.l\td1,d0\t; Move remainder to d0");
                        }
                        else
                        {
                            // Call division: d0 / d1 -> d0 (quotient in d0, d1 preserved)
                            Emit("\tjsr\t__div_u32\t; d0 = d0 / d1");
                            // Now: d0 = quotient, d2 = original dividend, d3 = divisor
                            // Multiply quotient * divisor
                            Emit("\tmove.l\td3,d1\t; d1 = divisor");
                            Emit("\tjsr\t__mul_u32\t; d0 = quotient * divisor");
                            // Subtract from original dividend
                            Emit("\tmove.l\td2,d1\t; d1 = original dividend");
                            Emit("\tsub.l\td0,d1\t; remainder = dividend - (quotient * divisor)");
                            Emit("\tmove.l\td1,d0\t; Move remainder to d0");
                        }

                        Emit("\tmovem.l\t(sp)+,d2-d3");
                    }
                    else
                    {
                        // 68020+ has divsl.l/divul.l which returns remainder in d1
                        Emit($"\t{divOp}.l\td1,d1:d0\t; d0 = quotient, d1 = remainder");
                        Emit("\tmove.l\td1,d0\t; Move remainder to d0");
                    }
                }
                break;

            case IrBinaryOp.OpKind.Or:
                EmitComment($"{binOp.ResultName} = or");
                LoadOperand(binOp.Left, "d0");
                LoadOperand(binOp.Right, "d1");
                Emit($"\tor{size}\td1,d0");
                break;

            case IrBinaryOp.OpKind.And:
                EmitComment($"{binOp.ResultName} = and");
                LoadOperand(binOp.Left, "d0");
                LoadOperand(binOp.Right, "d1");
                Emit($"\tand{size}\td1,d0");
                break;

            case IrBinaryOp.OpKind.Xor:
                EmitComment($"{binOp.ResultName} = xor");
                LoadOperand(binOp.Left, "d0");
                LoadOperand(binOp.Right, "d1");
                Emit($"\teor{size}\td1,d0");
                // For boolean XOR, ensure upper bits are cleared after byte operation
                if (binOp.Type is IrBoolType)
                {
                    Emit($"\tand.l\t#1,d0\t\t; Clear upper bits after boolean XOR");
                }
                break;

            case IrBinaryOp.OpKind.Shl:
                EmitComment($"{binOp.ResultName} = shl");
                LoadOperand(binOp.Left, "d0");   // Value to shift

                // Check if shift amount is a constant - can optimize
                if (binOp.Right is IrConstant leftShiftConst)
                {
                    int shiftAmount = (int)leftShiftConst.Value;
                    GenerateLeftShift(shiftAmount, size);
                }
                else
                {
                    // Variable shift amount
                    LoadOperand(binOp.Right, "d1");  // Shift amount into d1
                    Emit($"\tlsl{size}\td1,d0");
                }
                break;

            case IrBinaryOp.OpKind.Shr:
                EmitComment($"{binOp.ResultName} = shr");
                LoadOperand(binOp.Left, "d0");   // Value to shift

                // Check if shift amount is a constant - can optimize
                if (binOp.Right is IrConstant rightShiftConst)
                {
                    int shiftAmount = (int)rightShiftConst.Value;
                    bool isSigned = binOp.Left.Type is IrIntType intType && intType.IsSigned;
                    GenerateRightShift(shiftAmount, isSigned);
                }
                else
                {
                    // Variable shift amount
                    bool isSigned = binOp.Left.Type is IrIntType intType && intType.IsSigned;
                    var shiftOp = isSigned ? "asr" : "lsr";
                    LoadOperand(binOp.Right, "d1");  // Shift amount into d1
                    Emit($"\t{shiftOp}{size}\td1,d0");
                }
                break;

            case IrBinaryOp.OpKind.Eq:
            case IrBinaryOp.OpKind.Ne:
            case IrBinaryOp.OpKind.Lt:
            case IrBinaryOp.OpKind.Le:
            case IrBinaryOp.OpKind.Gt:
            case IrBinaryOp.OpKind.Ge:
                GenerateComparison(binOp, instructions, index);
                // Comparisons use separate tracking (_lastComparisonResult)
                return;
        }

        // Result is in d0
        // REGISTER ALLOCATION: Check if result has an allocated register
        if (_currentFunctionRegAlloc != null)
        {
            var allocatedReg = _currentFunctionRegAlloc.GetRegister(binOp.ResultName);
            if (allocatedReg != null && allocatedReg != "d0")
            {
                // Move result from d0 to allocated register
                EmitComment($"Store result {binOp.ResultName} to allocated register {allocatedReg}");
                Emit($"\tmove.l\td0,{allocatedReg}");
                _lastResultInD0 = null; // Result no longer in d0
            }
            else
            {
                // Result stays in d0 (either not allocated or allocated to d0)
                _lastResultInD0 = binOp.ResultName;
            }
        }
        else
        {
            // No register allocation - track that result is in d0
            _lastResultInD0 = binOp.ResultName;
        }
    }

    private void GenerateComparison(IrBinaryOp binOp, IList<IrInstruction> instructions, int index)
    {
        // Use operand type for comparison size, not result type (result is always bool = 1 byte)
        var size = GetSizeSuffix(binOp.Left.Type);
        bool isFloatCompare = binOp.Left.Type is IrFloatType;

        EmitComment($"{binOp.ResultName} = {binOp.Operation}");

        // Map IR comparison to 68k condition code
        var condition = binOp.Operation switch
        {
            IrBinaryOp.OpKind.Eq => "eq",  // Equal (Z=1)
            IrBinaryOp.OpKind.Ne => "ne",  // Not equal (Z=0)
            IrBinaryOp.OpKind.Lt => "lt",  // Less than (N=1, signed)
            IrBinaryOp.OpKind.Le => "le",  // Less than or equal (Z=1 or N=1, signed)
            IrBinaryOp.OpKind.Gt => "gt",  // Greater than (Z=0 and N=0, signed)
            IrBinaryOp.OpKind.Ge => "ge",  // Greater than or equal (N=0, signed)
            _ => throw new Exception($"Unknown comparison: {binOp.Operation}")
        };

        // Look ahead to see if this comparison is immediately used in a conditional branch
        // Pattern: BinaryOp (comparison) -> [optional Store] -> ConditionalBranch
        bool willBranchDirectly = false;
        int nextIndex = index + 1;

        // Skip over optional store instruction
        if (nextIndex < instructions.Count &&
            instructions[nextIndex] is IrStore store &&
            store.Value is IrVariable storeVar &&
            storeVar.Name == binOp.ResultName)
        {
            nextIndex++;
        }

        // Check if next instruction is a conditional branch using this comparison
        if (nextIndex < instructions.Count &&
            instructions[nextIndex] is IrConditionalBranch condBranch &&
            condBranch.Condition is IrVariable condVar &&
            condVar.Name == binOp.ResultName)
        {
            willBranchDirectly = true;
        }

        if (_generatingFpuVersion && isFloatCompare)
        {
            // Hardware FPU comparison
            LoadOperand(binOp.Left, "fp0");
            LoadOperand(binOp.Right, "fp1");

            // FCMP computes (fp0 - fp1) and sets FPU condition codes
            Emit("\tfcmp.x\tfp1,fp0");

            // FBcc uses FPU condition codes (same mnemonics as integer)
            // Note: We need to materialize the result to d0 for later use
            // Unfortunately there's no FScc instruction, so we use FBcc with labels
            var trueLabel = $".cmp_true_{_floatConstCounter++}";
            var doneLabel = $".cmp_done_{_floatConstCounter++}";

            Emit($"\tfb{condition}\t{trueLabel}");
            Emit("\tmoveq\t#0,d0");  // False
            Emit($"\tbra.s\t{doneLabel}");
            Emit($"{trueLabel}:");
            Emit("\tmoveq\t#1,d0");  // True
            Emit($"{doneLabel}:");
        }
        else
        {
            // Integer or soft-float comparison

            // OPTIMIZATION: Use TST instead of CMP when comparing a variable against zero
            // TST is faster and smaller than CMP #0
            // Only apply if one operand is a variable (not both constants)
            bool leftIsZero = binOp.Left is IrConstant leftConst && leftConst.Value == 0;
            bool rightIsZero = binOp.Right is IrConstant rightConst && rightConst.Value == 0;
            bool leftIsVariable = binOp.Left is not IrConstant;
            bool rightIsVariable = binOp.Right is not IrConstant;

            // Handle operands that might be in d0 already
            bool leftInD0 = binOp.Left is IrVariable leftVar && leftVar.Name == _lastResultInD0;
            bool rightInD0 = binOp.Right is IrVariable rightVar && rightVar.Name == _lastResultInD0;

            // If right operand is in d0, save it to d1 first before loading left
            if (rightInD0 && !leftInD0)
            {
                EmitComment($"Right operand already in d0, move to d1 first");
                Emit("\tmove.l\td0,d1");
                _lastResultInD0 = null; // Consumed
                LoadOperand(binOp.Left, "d0");
                Emit($"\tcmp{size}\td1,d0");
            }
            // If left operand is in d0, just load right to d1
            else if (leftInD0)
            {
                EmitComment($"Left operand already in d0");
                _lastResultInD0 = null; // Will be consumed
                LoadOperand(binOp.Right, "d1");
                Emit($"\tcmp{size}\td1,d0");
            }
            // Save previous d0 value if it will be overwritten and not used by this comparison
            else if (_lastResultInD0 != null)
            {
                EmitComment($"Save previous d0 value ({_lastResultInD0}) before comparison");
                Emit("\tmove.l\td0,-(sp)");
                _savedTemps.Add(_lastResultInD0);
                _tempStackOffset += 4;
                _lastResultInD0 = null;

                // Now do the normal comparison
                if (rightIsZero && leftIsVariable)
                {
                    // x CMP 0 => TST x (where x is a variable)
                    LoadOperand(binOp.Left, "d0");
                    Emit($"\ttst{size}\td0");
                    EmitComment($"Optimized: compare against zero using TST instead of CMP");
                }
                else if (leftIsZero && rightIsVariable &&
                         (binOp.Operation == IrBinaryOp.OpKind.Eq || binOp.Operation == IrBinaryOp.OpKind.Ne))
                {
                    // 0 == x or 0 != x => TST x (where x is a variable)
                    LoadOperand(binOp.Right, "d0");
                    Emit($"\ttst{size}\td0");
                    EmitComment($"Optimized: compare against zero using TST instead of CMP");
                }
                else
                {
                    LoadOperand(binOp.Left, "d0");
                    LoadOperand(binOp.Right, "d1");
                    Emit($"\tcmp{size}\td1,d0");
                }
            }
            else
            {
                // No value in d0, normal comparison
                if (rightIsZero && leftIsVariable)
                {
                    // x CMP 0 => TST x (where x is a variable)
                    LoadOperand(binOp.Left, "d0");
                    Emit($"\ttst{size}\td0");
                    EmitComment($"Optimized: compare against zero using TST instead of CMP");
                }
                else if (leftIsZero && rightIsVariable &&
                         (binOp.Operation == IrBinaryOp.OpKind.Eq || binOp.Operation == IrBinaryOp.OpKind.Ne))
                {
                    // 0 == x or 0 != x => TST x (where x is a variable)
                    LoadOperand(binOp.Right, "d0");
                    Emit($"\ttst{size}\td0");
                    EmitComment($"Optimized: compare against zero using TST instead of CMP");
                }
                else
                {
                    LoadOperand(binOp.Left, "d0");
                    LoadOperand(binOp.Right, "d1");
                    Emit($"\tcmp{size}\td1,d0");
                }
            }

            // OPTIMIZATION: If this comparison will be used directly for branching,
            // skip materialization and keep condition codes alive
            if (!willBranchDirectly)
            {
                // Materialize boolean result in d0 (for cases where it's used as a value)
                // Scc sets byte to $FF if condition true, $00 if false
                Emit($"\ts{condition}\td0");

                // Convert $FF to $00000001, $00 to $00000000
                // Simpler and more reliable than sign-extend + negate
                Emit($"\tand.l\t#1,d0");   // Mask to single bit: $FF → $01, $00 → $00

                // Track that result is in d0
                _lastResultInD0 = binOp.ResultName;

                // Clear comparison tracking since condition codes were clobbered by and.l
                _lastComparisonResult = null;
                _lastComparisonCondition = null;
            }
            else
            {
                EmitComment("Comparison result will be used directly for branching - keeping condition codes");
                // Result not materialized in d0, so don't track it
                _lastResultInD0 = null;

                // Track this comparison for potential optimization in conditional branch
                // Only safe to do this when condition codes are still valid
                _lastComparisonResult = binOp.ResultName;
                _lastComparisonCondition = condition;
            }
        }
    }

}
