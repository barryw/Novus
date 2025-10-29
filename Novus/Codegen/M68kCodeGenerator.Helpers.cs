using System.Text;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Helper methods for M68kCodeGenerator (partial class)
/// Contains utility methods for operand loading, size suffix generation, and output
/// </summary>
public partial class M68kCodeGenerator
{
    private bool IsFloatInstruction(IrInstruction instr)
    {
        return instr switch
        {
            IrLocalDecl decl => decl.Type is IrFloatType,
            IrBinaryOp binOp => binOp.Type is IrFloatType,
            IrReturn ret => ret.Value?.Type is IrFloatType,
            _ => false
        };
    }

    // Check if a function benefits from CPU-specific optimizations
    // Functions with multiply, divide, or shift operations benefit from:
    // - 68060 shift/add optimizations for small constant multiplies
    // - Division by power-of-2 optimization
    // - Barrel shifter on 68020+
    private bool UsesCpuOptimizations(IrFunction function)
    {
        return function.BasicBlocks.Any(bb => bb.Instructions.Any(IsCpuOptimizableInstruction));
    }

    private bool IsCpuOptimizableInstruction(IrInstruction instr)
    {
        return instr switch
        {
            IrBinaryOp binOp => binOp.Operation switch
            {
                IrBinaryOp.OpKind.Mul => true,   // Multiply benefits from 68060 optimization
                IrBinaryOp.OpKind.Div => true,   // Divide benefits from power-of-2 optimization
                IrBinaryOp.OpKind.Shl => true,   // Shifts benefit from barrel shifter
                IrBinaryOp.OpKind.Shr => true,   // Shifts benefit from barrel shifter
                _ => false
            },
            IrIndexAccess _ => true,  // Array indexing benefits from barrel shifter
            IrIndexStore _ => true,   // Array indexing benefits from barrel shifter
            _ => false
        };
    }

    private void LoadOperand(IrValue value, string targetReg)
    {
        // Clear d0 tracking if we're loading something different into d0
        if (targetReg == "d0")
        {
            // Check if this value is the one currently tracked in d0
            if (value is IrVariable v && v.Name == _lastResultInD0)
            {
                // Same value already in d0, don't clear tracking
            }
            else
            {
                // Loading something new into d0, clear tracking
                _lastResultInD0 = null;
            }
        }

        switch (value)
        {
            case IrFunctionAddress funcAddr:
                // Load address of function (for function pointers)
                // LEA can only load into address registers, so use a0 as intermediate if target is data register
                if (targetReg.StartsWith('d'))
                {
                    Emit($"\tlea\t_{funcAddr.FunctionName},a0");
                    Emit($"\tmove.l\ta0,{targetReg}");
                }
                else
                {
                    Emit($"\tlea\t_{funcAddr.FunctionName},{targetReg}");
                }
                break;
            case IrStringLiteral stringLiteral:
                // Load address of string literal
                // LEA can only load into address registers, so use a0 as intermediate if target is data register
                if (targetReg.StartsWith('d'))
                {
                    Emit($"\tlea\t{stringLiteral.Label},a0");
                    Emit($"\tmove.l\ta0,{targetReg}");
                }
                else
                {
                    Emit($"\tlea\t{stringLiteral.Label},{targetReg}");
                }
                break;
            case IrArrayLiteral arrayLiteral:
                // Array literals can't be loaded into a register
                // They should only appear in initialization contexts
                throw new Exception("Array literals cannot be loaded into registers");
            case IrBoolConstant boolConstant:
                // Boolean constants: true = 1, false = 0
                var boolValue = boolConstant.Value ? 1 : 0;
                Emit($"\tmoveq\t#{boolValue},{targetReg}");
                break;
            case IrConstant constant:
                var size = GetSizeSuffix(constant.Type);

                // Optimization: Use moveq for small longword immediates
                // moveq is faster (4 cycles vs 8) and smaller (2 bytes vs 6)
                if (size == ".l" && constant.Value >= -128 && constant.Value <= 127)
                {
                    Emit($"\tmoveq\t#{constant.Value},{targetReg}");
                }
                else
                {
                    Emit($"\tmove{size}\t#{constant.Value},{targetReg}");
                }
                break;
            case IrFloatConstant floatConstant:
            {
                if (_generatingFpuVersion)
                {
                    // Hardware FPU mode - use FPU instructions
                    // Create a unique label for this constant
                    var constLabel = $"__fc{_floatConstCounter++}";
                    _floatConstants[constLabel] = floatConstant.Value;

                    // Determine target FPU register (default to fp0)
                    var fpReg = targetReg.StartsWith("fp") ? targetReg : "fp0";

                    // Load constant from memory into FPU register
                    if (floatConstant.Type is IrFloatType ft && ft.BitWidth == 32)
                    {
                        Emit($"\tfmove.s\t{constLabel},{fpReg}");
                    }
                    else // f64
                    {
                        Emit($"\tfmove.d\t{constLabel},{fpReg}");
                    }

                    // If target is a data register (for storage), convert from FPU to integer bits
                    if (targetReg.StartsWith('d'))
                    {
                        Emit($"\tfmove.l\t{fpReg},{targetReg}");
                    }
                }
                else
                {
                    // Soft-float mode - store the IEEE-754 bit representation as an integer
                    uint bits;
                    if (floatConstant.Type is IrFloatType ft && ft.BitWidth == 32)
                    {
                        bits = BitConverter.SingleToUInt32Bits((float)floatConstant.Value);
                        Emit($"\tmove.l\t#${bits:X8},{targetReg}");
                    }
                    else // f64
                    {
                        // For f64, we need 64 bits which won't fit in a single register
                        // This is a limitation - for now just convert to f32
                        bits = BitConverter.SingleToUInt32Bits((float)floatConstant.Value);
                        Emit($"\tmove.l\t#${bits:X8},{targetReg}");
                    }
                }
                break;
            }
            case IrFixedConstant fixedConstant:
            {
                // Convert fixed-point: multiply by 2^fractional_bits
                int fractionalBits = fixedConstant.Type is IrFixedType ft && ft.BitWidth == 16 ? 8 : 16;
                int fixedValue = (int)(fixedConstant.Value * (1 << fractionalBits));

                if (fixedConstant.Type is IrFixedType fixedType && fixedType.BitWidth == 16)
                {
                    Emit($"\tmove.w\t#{fixedValue},{targetReg}");
                }
                else
                {
                    Emit($"\tmove.l\t#{fixedValue},{targetReg}");
                }
                break;
            }
            case IrVariable variable:
            {
                // REGISTER ALLOCATION: Check if variable is allocated to a register
                if (_currentFunctionRegAlloc != null)
                {
                    var allocatedReg = _currentFunctionRegAlloc.GetRegister(variable.Name);
                    if (allocatedReg != null)
                    {
                        // Variable is in a register - move from allocated register to target
                        if (allocatedReg != targetReg)
                        {
                            EmitComment($"Load {variable.Name} from allocated register {allocatedReg}");
                            Emit($"\tmove.l\t{allocatedReg},{targetReg}");
                        }
                        // else: already in target register, no move needed
                        return;
                    }
                    // If spilled or not allocated, fall through to stack loading
                }

                // Check if this value is currently in d0 (from previous instruction like MemberAccess)
                if (_lastResultInD0 == variable.Name)
                {
                    // Value is in d0, move to target register
                    if (targetReg != "d0")
                    {
                        if (targetReg.StartsWith('a'))
                        {
                            Emit($"\tmovea.l\td0,{targetReg}\t; From d0 (previous result)");
                        }
                        else
                        {
                            Emit($"\tmove.l\td0,{targetReg}\t; From d0 (previous result)");
                        }
                    }
                    _lastResultInD0 = null; // Consumed
                    return;
                }

                // Check if this is a parameter (use _localVariableOffsets for correct offset)
                if (_currentFunction != null)
                {
                    var paramIndex = _currentFunction.Parameters.FindIndex(p => p.Name == variable.Name);
                    if (paramIndex >= 0 && _localVariableOffsets.ContainsKey(variable.Name))
                    {
                        // Parameters are on the stack after link frame
                        // Offsets are calculated in EmitPrologue based on actual parameter sizes
                        var baseOffset = _localVariableOffsets[variable.Name];

                        // NOTE: Do NOT adjust for big-endian on stack variables!
                        // Parameters are passed on the stack and accessed via (a6) relative addressing.
                        // The stack frame layout already accounts for proper alignment.
                        var offset = baseOffset;

                        // Check if this is a composite type (> 4 bytes)
                        if (variable.Type.SizeInBytes > 4)
                        {
                            if (targetReg.StartsWith('a'))
                            {
                                // Load address of composite type parameter
                                Emit($"\tlea\t{baseOffset}(a6),{targetReg}");
                            }
                            else
                            {
                                throw new Exception($"Composite parameter types (size {variable.Type.SizeInBytes} bytes) cannot be loaded into data registers - use address registers");
                            }
                            return;
                        }

                        if (targetReg.StartsWith("fp"))
                        {
                            // Loading into FPU register - always use base offset for floats
                            Emit($"\tfmove.l\t{baseOffset}(a6),{targetReg}");
                        }
                        else
                        {
                            var varSize = GetSizeSuffix(variable.Type);
                            Emit($"\tmove{varSize}\t{offset}(a6),{targetReg}");

                            // Clear upper bits for byte/word loads to data registers
                            if (variable.Type.SizeInBytes == 1 && targetReg.StartsWith('d'))
                            {
                                Emit($"\tand.l\t#$FF,{targetReg}");
                            }
                            else if (variable.Type.SizeInBytes == 2 && targetReg.StartsWith('d'))
                            {
                                Emit($"\tand.l\t#$FFFF,{targetReg}");
                            }
                        }
                        return;
                    }
                }

                // Check if it's a local variable
                if (_localVariableOffsets.ContainsKey(variable.Name))
                {
                    var baseOffset = _localVariableOffsets[variable.Name];

                    // NOTE: Do NOT adjust for big-endian on stack variables!
                    // The stack frame layout already accounts for proper alignment.
                    // Big-endian adjustment is only needed for register operations,
                    // not for stack-based (a6) relative addressing.
                    var offset = baseOffset;

                    // Check if this is a composite type (> 4 bytes)
                    if (variable.Type.SizeInBytes > 4)
                    {
                        if (targetReg.StartsWith('a'))
                        {
                            // Load address of composite type local variable
                            Emit($"\tlea\t{baseOffset}(a6),{targetReg}");
                        }
                        else
                        {
                            throw new Exception($"Composite local variable types (size {variable.Type.SizeInBytes} bytes) cannot be loaded into data registers - use address registers");
                        }
                        return;
                    }

                    if (targetReg.StartsWith("fp"))
                    {
                        // Loading into FPU register - use fmove.l (always use base offset)
                        Emit($"\tfmove.l\t{baseOffset}(a6),{targetReg}");
                    }
                    else
                    {
                        var varSize = GetSizeSuffix(variable.Type);
                        Emit($"\tmove{varSize}\t{offset}(a6),{targetReg}");

                        // Clear upper bits after loading byte or word to avoid garbage
                        if (variable.Type.SizeInBytes == 1 && targetReg.StartsWith('d'))
                        {
                            Emit($"\tand.l\t#$FF,{targetReg}");
                        }
                        else if (variable.Type.SizeInBytes == 2 && targetReg.StartsWith('d'))
                        {
                            Emit($"\tand.l\t#$FFFF,{targetReg}");
                        }
                    }
                    return;
                }

                // If it's a temporary variable (%tN), load it from the stack if saved
                if (variable.Name.StartsWith("%t"))
                {
                    // Check if this temp should reload from a global variable (for match on globals)
                    if (_globalTagTemps.ContainsKey(variable.Name))
                    {
                        var globalName = _globalTagTemps[variable.Name];
                        var globalLabel = $"_system_{globalName}";
                        Emit($"\tmove.l\t{globalLabel},{targetReg}\t\t; Reload tag from global");
                        return;
                    }

                    var savedIndex = _savedTemps.IndexOf(variable.Name);
                    if (savedIndex >= 0)
                    {
                        // Check if this is a composite type (> 4 bytes)
                        if (variable.Type.SizeInBytes > 4)
                        {
                            // For composite types, we need to calculate the proper offset
                            // Calculate offset by summing sizes of all temps saved after this one
                            int stackOffset = 0;
                            for (int i = _savedTemps.Count - 1; i > savedIndex; i--)
                            {
                                var tempName = _savedTemps[i];
                                // Get the size of this temp (default to 4 if not tracked)
                                var tempSize = _savedTempSizes.GetValueOrDefault(tempName, 4);
                                stackOffset += tempSize;
                            }

                            if (targetReg.StartsWith('a'))
                            {
                                // Load address of composite type on stack
                                Emit($"\tlea\t{stackOffset}(sp),{targetReg}");
                            }
                            else
                            {
                                throw new Exception($"Composite types (size {variable.Type.SizeInBytes} bytes) cannot be loaded into data registers - use address registers");
                            }
                            return;
                        }

                        // Calculate offset: most recent temp is at 0(sp), earlier ones at higher offsets
                        // Account for variable-sized temps
                        int baseOffset = 0;
                        for (int i = _savedTemps.Count - 1; i > savedIndex; i--)
                        {
                            var tempName = _savedTemps[i];
                            var tempSize = _savedTempSizes.GetValueOrDefault(tempName, 4);
                            baseOffset += tempSize;
                        }

                        // Adjust offset for big-endian byte ordering when loading smaller than longword
                        // Temps are always stored as longwords (4 bytes)
                        var offset = baseOffset;
                        if (variable.Type.SizeInBytes == 1)
                        {
                            offset += 3;  // Byte is at highest address in big-endian longword
                        }
                        else if (variable.Type.SizeInBytes == 2)
                        {
                            offset += 2;  // Word is at highest address in big-endian longword
                        }

                        if (targetReg.StartsWith("fp"))
                        {
                            // Loading into FPU register - use fmove.l (always longword)
                            var fpuOffsetStr = baseOffset == 0 ? "(sp)" : $"{baseOffset}(sp)";
                            Emit($"\tfmove.l\t{fpuOffsetStr},{targetReg}");
                        }
                        else
                        {
                            var tempSize = GetSizeSuffix(variable.Type);
                            // Use (sp) when offset is 0, otherwise use offset(sp)
                            var offsetStr = offset == 0 ? "(sp)" : $"{offset}(sp)";
                            Emit($"\tmove{tempSize}\t{offsetStr},{targetReg}");

                            // Clear upper bits after loading byte or word to avoid garbage
                            if (variable.Type.SizeInBytes == 1 && targetReg.StartsWith('d'))
                            {
                                Emit($"\tand.l\t#$FF,{targetReg}");
                            }
                            else if (variable.Type.SizeInBytes == 2 && targetReg.StartsWith('d'))
                            {
                                Emit($"\tand.l\t#$FFFF,{targetReg}");
                            }
                        }
                    }
                    else
                    {
                        // Result already in d0 from immediately previous operation
                        // Composite types (> 4 bytes) should never be in d0
                        if (variable.Type.SizeInBytes > 4)
                        {
                            throw new Exception($"Composite types (size {variable.Type.SizeInBytes} bytes) cannot be in d0 - this is a codegen error");
                        }

                        if (targetReg != "d0")
                        {
                            if (targetReg.StartsWith("fp"))
                            {
                                // Loading into FPU register from d0
                                Emit($"\tfmove.l\td0,{targetReg}");
                            }
                            else
                            {
                                var tempSize = GetSizeSuffix(variable.Type);
                                Emit($"\tmove{tempSize}\td0,{targetReg}");
                            }
                        }
                    }
                }
                // Check if it's a global variable from system module (CPU, FPU, Chipset)
                else if (variable.Name == "CPU" || variable.Name == "FPU" || variable.Name == "Chipset")
                {
                    // These globals are always simple types (i32, etc.), but check anyway
                    if (variable.Type.SizeInBytes > 4)
                    {
                        throw new Exception($"System global variables cannot be composite types (size {variable.Type.SizeInBytes} bytes)");
                    }

                    // Load from global variable label (e.g., _system_CPU)
                    var globalLabel = $"_system_{variable.Name}";
                    var varSize = GetSizeSuffix(variable.Type);
                    Emit($"\tmove{varSize}\t{globalLabel},{targetReg}");

                    // Clear upper bits after loading byte or word to avoid garbage
                    if (variable.Type.SizeInBytes == 1 && targetReg.StartsWith('d'))
                    {
                        Emit($"\tand.l\t#$FF,{targetReg}");
                    }
                    else if (variable.Type.SizeInBytes == 2 && targetReg.StartsWith('d'))
                    {
                        Emit($"\tand.l\t#$FFFF,{targetReg}");
                    }
                }

                // Check if variable is in saved temps (fallback for any variable name)
                var savedTempIndex = _savedTemps.IndexOf(variable.Name);
                if (savedTempIndex >= 0)
                {
                    // Calculate offset: most recent temp is at 0(sp), earlier ones at higher offsets
                    int baseOffset = 0;
                    for (int i = _savedTemps.Count - 1; i > savedTempIndex; i--)
                    {
                        var tempName = _savedTemps[i];
                        var tempSize = _savedTempSizes.GetValueOrDefault(tempName, 4);
                        baseOffset += tempSize;
                    }

                    // Adjust offset for big-endian byte ordering when loading smaller than longword
                    var offset = baseOffset;
                    if (variable.Type.SizeInBytes == 1)
                    {
                        offset += 3;  // Byte is at highest address in big-endian longword
                    }
                    else if (variable.Type.SizeInBytes == 2)
                    {
                        offset += 2;  // Word is at highest address in big-endian longword
                    }

                    // Load saved temp without popping (we'll clean up at label boundaries)
                    if (targetReg.StartsWith("fp"))
                    {
                        Emit($"\tfmove.l\t{baseOffset}(sp),{targetReg}\t; Load saved temp {variable.Name}");
                    }
                    else if (targetReg.StartsWith('a'))
                    {
                        Emit($"\tmovea.l\t{offset}(sp),{targetReg}\t; Load saved temp {variable.Name}");
                    }
                    else
                    {
                        var varSize = GetSizeSuffix(variable.Type);
                        Emit($"\tmove{varSize}\t{offset}(sp),{targetReg}\t; Load saved temp {variable.Name}");
                    }
                    return;
                }

                // Unknown variable - shouldn't happen if semantic analysis passed
                var funcName = _currentFunction?.Name ?? "unknown";
                throw new Exception($"Unknown variable: {variable.Name} in function {funcName}");
                break;
            }
            case IrEnumValue enumValue:
            {
                // For simple enums (no associated data), just load the tag value
                // For enums with data, construct the full value on the stack

                if (enumValue.AssociatedValues.Count == 0)
                {
                    // Simple enum - just load the tag value directly
                    EmitComment($"Load enum tag {enumValue.Type.Name}::{enumValue.VariantName} = {enumValue.VariantTag}");
                    if (targetReg.StartsWith("d"))
                    {
                        Emit($"\tmoveq\t#{enumValue.VariantTag},{targetReg}\t\t; Enum tag");
                    }
                    else
                    {
                        Emit($"\tmove.l\t#{enumValue.VariantTag},{targetReg}\t\t; Enum tag");
                    }
                    break;
                }

                // Enum with associated data - construct full value on stack
                EmitComment($"Constructing enum {enumValue.Type.Name}::{enumValue.VariantName}");

                var enumType = enumValue.Type as IrEnumType;
                if (enumType == null)
                {
                    throw new Exception("Enum value must have enum type");
                }

                // Calculate total size needed
                var enumSize = enumType.SizeInBytes;

                // Allocate space on stack (subtract from sp)
                if (enumSize > 0)
                {
                    Emit($"\tsub.l\t#{enumSize},sp\t\t; Allocate space for enum");
                    _tempStackOffset += enumSize;  // Track stack allocation for cleanup
                }

                // Save base address in a2 to handle nested allocations correctly
                Emit($"\tmove.l\tsp,a2\t\t; Save enum base address");

                // Store tag at offset 0
                Emit($"\tmove.l\t#{enumValue.VariantTag},(a2)\t\t; Store variant tag");

                // Store associated values starting at offset 4
                int dataOffset = 4;
                for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
                {
                    var assocValue = enumValue.AssociatedValues[i];

                    // Check if associated value is a simple enum constructor (no associated values)
                    if (assocValue is IrEnumValue nestedEnumVal && nestedEnumVal.AssociatedValues.Count == 0)
                    {
                        // Simple enum constructor - just store the tag and zero padding
                        EmitComment($"Store simple enum {nestedEnumVal.Type.Name}::{nestedEnumVal.VariantName}");
                        Emit($"\tmove.l\t#{nestedEnumVal.VariantTag},{dataOffset}(a2)\t\t; Store simple enum tag");

                        // Zero out remaining bytes for this enum type
                        var nestedEnumType = nestedEnumVal.Type as IrEnumType;
                        int remainingBytes = nestedEnumType.SizeInBytes - 4;
                        if (remainingBytes > 0)
                        {
                            Emit($"\tmoveq\t#0,d0");
                            for (int offset = 4; offset < nestedEnumType.SizeInBytes; offset += 4)
                            {
                                Emit($"\tmove.l\td0,{dataOffset + offset}(a2)\t\t; Zero data portion");
                            }
                        }
                        dataOffset += nestedEnumType.SizeInBytes;
                    }
                    // Check if associated value is a nested IrEnumValue - build it inline
                    else if (assocValue is IrEnumValue nestedEnumValue && nestedEnumValue.Type is IrEnumType nestedEnumType)
                    {
                        // Build nested enum inline at current offset to avoid stack pointer issues
                        EmitComment($"Build nested enum {nestedEnumType.Name} inline at offset {dataOffset}");

                        // Store nested enum tag
                        Emit($"\tmove.l\t#{nestedEnumValue.VariantTag},{dataOffset}(a2)");
                        int nestedOffset = dataOffset + 4;

                        // Store nested enum's associated values
                        for (int k = 0; k < nestedEnumValue.AssociatedValues.Count; k++)
                        {
                            var nestedAssocValue = nestedEnumValue.AssociatedValues[k];
                            LoadOperand(nestedAssocValue, "d0");
                            var nestedValueSize = GetSizeSuffix(nestedAssocValue.Type);
                            Emit($"\tmove{nestedValueSize}\td0,{nestedOffset}(a2)");
                            nestedOffset += nestedAssocValue.Type.SizeInBytes;
                        }

                        dataOffset += nestedEnumType.SizeInBytes;
                    }
                    // Check if associated value is a nested composite enum variable (> 4 bytes)
                    else if (assocValue.Type is IrEnumType enumType2 && enumType2.SizeInBytes > 4)
                    {
                        // Nested composite enum variable - load it and get address in a1
                        LoadOperand(assocValue, "a1");

                        // Copy it longword-by-longword from nested enum to current enum
                        // Use a2 (base address) instead of sp since sp may have moved
                        int numLongwords = (enumType2.SizeInBytes + 3) / 4;
                        for (int j = 0; j < numLongwords; j++)
                        {
                            Emit($"\tmove.l\t{j * 4}(a1),d0");
                            Emit($"\tmove.l\td0,{dataOffset + (j * 4)}(a2)\t\t; Store nested enum longword {j}");
                        }
                        dataOffset += enumType2.SizeInBytes;
                    }
                    else
                    {
                        // Simple value - load into d0
                        LoadOperand(assocValue, "d0");

                        var valueSize = GetSizeSuffix(assocValue.Type);
                        Emit($"\tmove{valueSize}\td0,{dataOffset}(a2)\t\t; Store associated value {i}");
                        dataOffset += assocValue.Type.SizeInBytes;
                    }
                }

                // If targetReg is an address register, load the address
                // If it's a data register, we can't load the whole struct - error
                if (targetReg.StartsWith('a'))
                {
                    if (targetReg != "a2")
                    {
                        Emit($"\tmove.l\ta2,{targetReg}\t\t; Load enum address");
                    }
                }
                else
                {
                    // For data register, this is likely being stored somewhere - leave it on stack
                    // The calling code will need to handle copying it
                    throw new Exception("Enum values cannot be loaded directly into data registers - use address registers");
                }
                break;
            }
            case IrEnumConstructor enumConstructor:
            {
                // IrEnumConstructor represents an enum variant constructor (e.g., SystemCPU::M68000)
                // For simple enums (no associated data), just load the tag value
                EmitComment($"Load enum constructor {enumConstructor.Type.Name}::{enumConstructor.VariantName} = {enumConstructor.VariantTag}");
                if (targetReg.StartsWith("d"))
                {
                    Emit($"\tmoveq\t#{enumConstructor.VariantTag},{targetReg}\t\t; Enum tag");
                }
                else
                {
                    Emit($"\tmove.l\t#{enumConstructor.VariantTag},{targetReg}\t\t; Enum tag");
                }
                break;
            }
            case IrBorrowValue borrowValue:
            {
                // Borrow creates a reference (address) to a value
                // For variables, load the address using LEA
                if (borrowValue.BorrowedValue is IrVariable variable)
                {
                    if (_localVariableOffsets.ContainsKey(variable.Name))
                    {
                        var offset = _localVariableOffsets[variable.Name];

                        // LEA can only target address registers, so use a0 as intermediate
                        Emit($"\tlea\t{offset}(a6),a0");

                        // If target is a data register, move from a0
                        if (targetReg.StartsWith('d'))
                        {
                            Emit($"\tmove.l\ta0,{targetReg}");
                        }
                        else if (targetReg != "a0")
                        {
                            Emit($"\tmove.l\ta0,{targetReg}");
                        }
                    }
                    else
                    {
                        throw new Exception($"Cannot borrow unknown variable: {variable.Name}");
                    }
                }
                else
                {
                    throw new Exception($"Cannot borrow non-variable: {borrowValue.BorrowedValue.GetType().Name}");
                }
                break;
            }
            case IrDereferenceValue derefValue:
            {
                // Dereference: load the value pointed to by a pointer/reference
                // First, load the pointer/reference into an address register
                LoadOperand(derefValue.PointerValue, "a0");

                // Then load the value from that address
                var valueSize = GetSizeSuffix(derefValue.Type);
                Emit($"\tmove{valueSize}\t(a0),{targetReg}");
                break;
            }
        }
    }

    /// <summary>
    /// Mangles a function name for assembly output by replacing :: with _
    /// </summary>
    private string MangleName(string name)
    {
        return name.Replace("::", "_");
    }

    private string GetSizeSuffix(IrType type)
    {
        return type.SizeInBytes switch
        {
            1 => ".b",  // byte
            2 => ".w",  // word
            4 => ".l",  // long
            _ => throw new Exception($"Unsupported type size: {type.SizeInBytes}")
        };
    }

    private void Emit(string line)
    {
        _output.AppendLine(line);
    }

    private void EmitComment(string comment)
    {
        _output.AppendLine($"\t; {comment}");
    }
}
