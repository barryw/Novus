using Novus.IR;

namespace Novus.Codegen.M68k;

/// <summary>
/// Simple register allocator for 68k code generation.
///
/// Strategy:
/// - D0-D1: Temporaries and return values (volatile)
/// - D2-D7: Local variables (preserved)
/// - A0-A1: Temporaries and address calculations (volatile)
/// - A2-A4: Local pointers (preserved)
/// - A5: Frame pointer
/// - A6: Library base (for system calls)
/// - A7: Stack pointer
///
/// For this prototype, we use a simple strategy:
/// 1. Temporaries use volatile registers
/// 2. Local variables spill to stack
/// 3. No advanced optimizations (no graph coloring, no liveness analysis)
/// </summary>
public class RegisterAllocator
{
    private readonly IrFunction _function;
    private readonly Dictionary<string, M68kRegister> _variableToRegister = new();
    private readonly Dictionary<string, int> _variableToStackOffset = new();
    private readonly HashSet<M68kRegister> _allocatedRegisters = new();
    private int _stackFrameSize = 0;

    // Available registers for allocation (preserved registers only for locals)
    private static readonly M68kRegister[] DataRegisters = new[]
    {
        M68kRegister.D2, M68kRegister.D3, M68kRegister.D4,
        M68kRegister.D5, M68kRegister.D6, M68kRegister.D7
    };

    private static readonly M68kRegister[] AddressRegisters = new[]
    {
        M68kRegister.A2, M68kRegister.A3, M68kRegister.A4
    };

    public RegisterAllocator(IrFunction function)
    {
        _function = function;
    }

    /// <summary>
    /// Allocate registers and stack space for all local variables
    /// </summary>
    public void AllocateRegisters()
    {
        // Allocate stack slots for parameters (they're passed above frame pointer)
        // but we'll map them to their offsets for easy lookup
        int paramOffset = 8; // Base offset for first parameter
        foreach (var param in _function.Parameters)
        {
            _variableToStackOffset[param.Name] = paramOffset;
            int paramSize = param.Type.SizeInBytes;
            // Align to word boundary
            if (paramSize % 2 != 0)
                paramSize++;
            paramOffset += paramSize;
        }

        // Allocate all locals to stack
        // We can optimize this later with liveness analysis
        foreach (var local in _function.LocalVariables)
        {
            AllocateStackSlot(local.Name, local.Type);
        }

        // Scan IR for any additional variable references that need stack slots
        // (e.g., temporaries created during code generation)
        ScanIRForVariables();

        // Parameters are passed on stack (above frame pointer)
        // [param N]
        // ...
        // [param 1]
        // [param 0]
        // [return address]  <- 0(a5)
        // [saved FP]        <- -4(a5)
        // [locals...]       <- -8(a5) and below
    }

    /// <summary>
    /// Scan IR for variable references and allocate them if not already allocated
    /// </summary>
    private void ScanIRForVariables()
    {
        foreach (var block in _function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Look for store instructions that define variables
                if (instruction is IrStore store)
                {
                    if (!_variableToStackOffset.ContainsKey(store.VariableName))
                    {
                        AllocateStackSlot(store.VariableName, store.Value?.Type ?? IrIntType.I32);
                    }
                }
                // Look for binary ops that define temporaries
                else if (instruction is IrBinaryOp binOp)
                {
                    if (!string.IsNullOrEmpty(binOp.ResultName) && !_variableToStackOffset.ContainsKey(binOp.ResultName))
                    {
                        AllocateStackSlot(binOp.ResultName, binOp.Type);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Allocate a stack slot for a variable
    /// </summary>
    private void AllocateStackSlot(string varName, IrType type)
    {
        int size = type.SizeInBytes;

        // Align to word boundary (68k requirement)
        if (size % 2 != 0)
            size++;

        _stackFrameSize += size;
        _variableToStackOffset[varName] = -_stackFrameSize - 4; // -4 for saved FP
    }

    /// <summary>
    /// Get the stack offset for a variable
    /// Returns offset relative to A5 (frame pointer)
    /// </summary>
    public int GetStackOffset(string varName)
    {
        if (_variableToStackOffset.TryGetValue(varName, out int offset))
            return offset;

        throw new InvalidOperationException($"Variable '{varName}' not allocated");
    }

    /// <summary>
    /// Get the parameter offset for a function parameter
    /// Parameters are above the frame pointer
    /// </summary>
    public int GetParameterOffset(int paramIndex)
    {
        // Layout:
        // [param N]     <- 8 + (N-1)*4 + ... (a5)
        // [param 1]     <- 12(a5)
        // [param 0]     <- 8(a5)
        // [return addr] <- 4(a5)
        // [saved FP]    <- 0(a5)

        int offset = 8; // Base offset for first parameter

        // Add size of previous parameters
        for (int i = 0; i < paramIndex; i++)
        {
            int paramSize = _function.Parameters[i].Type.SizeInBytes;
            // Align to word boundary
            if (paramSize % 2 != 0)
                paramSize++;
            offset += paramSize;
        }

        return offset;
    }

    /// <summary>
    /// Get total stack frame size (for LINK instruction)
    /// </summary>
    public int GetStackFrameSize()
    {
        return _stackFrameSize;
    }

    /// <summary>
    /// Get set of preserved registers that need saving in prologue
    /// </summary>
    public IEnumerable<M68kRegister> GetPreservedRegisters()
    {
        return _allocatedRegisters.Where(r => r.GetRegisterClass() == RegisterClass.Preserved);
    }

    /// <summary>
    /// Allocate a temporary register for an expression
    /// For prototype, always use D0 for data, A0 for addresses
    /// </summary>
    public M68kRegister AllocateTemporary(IrType type)
    {
        // Use D0 for scalar values, A0 for pointers
        if (type is IrPointerType or IrReferenceType or IrMutReferenceType)
            return M68kRegister.A0;

        return M68kRegister.D0;
    }

    /// <summary>
    /// Get the register to use for a variable access
    /// For prototype, loads from stack to D0/A0
    /// </summary>
    public M68kRegister GetRegisterForVariable(string varName, IrType type)
    {
        // For now, always load to temporary register
        return AllocateTemporary(type);
    }
}
