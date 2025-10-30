using System.Text;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Function generation for M68kCodeGenerator (partial class)
/// Contains function prologue, epilogue, and calling convention code
/// </summary>
public partial class M68kCodeGenerator
{
    private void GenerateFunctionWithCpuTarget(IrFunction function, string cpuTarget, string suffix)
    {
        // Save current CPU features
        var savedCpuFeatures = _cpuFeatures;
        var savedCpuTarget = _cpuTarget;

        // Temporarily switch to target CPU
        var tempCpuTarget = cpuTarget;
        var tempCpuFeatures = new M68kCpuFeatures(cpuTarget);

        // Use reflection to update the readonly fields (hack, but necessary for fat binaries)
        var cpuTargetField = typeof(M68kCodeGenerator).GetField("_cpuTarget",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cpuFeaturesField = typeof(M68kCodeGenerator).GetField("_cpuFeatures",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        cpuTargetField?.SetValue(this, tempCpuTarget);
        cpuFeaturesField?.SetValue(this, tempCpuFeatures);

        // Generate the function with the new CPU target
        GenerateFunction(function, suffix);

        // Restore original CPU features
        cpuTargetField?.SetValue(this, savedCpuTarget);
        cpuFeaturesField?.SetValue(this, savedCpuFeatures);
    }

    private void GenerateFunction(IrFunction function, string suffix = "")
    {
        _currentFunction = function;
        _currentFunctionHasPrologue = false;
        _currentFunctionSuffix = suffix; // Track suffix for label generation

        // Set register allocation for this function (if available)
        _currentFunctionRegAlloc = _registerAllocations.GetValueOrDefault(function.Name);

        // Skip generic function templates - they're not concrete code, only templates for monomorphization
        // Check if any parameter or local variable has a generic type
        bool hasGenericParams = function.Parameters.Any(p => p.Type is IrGenericType);
        bool hasGenericLocals = function.LocalVariables.Any(v => v.Type is IrGenericType);
        bool hasGenericReturn = function.ReturnType is IrGenericType;
        bool isGeneric = hasGenericParams || hasGenericLocals || hasGenericReturn;

        if (isGeneric)
        {
            EmitComment($"Skipping generic template: {function.Name} (params:{hasGenericParams}, locals:{hasGenericLocals}, return:{hasGenericReturn})");
            return;
        }

        // Handle extern functions - just emit xref
        // Also treat functions with no body (imported declarations) as extern
        if (function.IsExtern || function.BasicBlocks.Count == 0)
        {
            EmitComment($"External function: {function.Name}");
            Emit($"\txref\t_{MangleName(function.Name)}");
            Emit("");
            return;
        }

        // Determine if we should use FPU instructions:
        // - In fat binary mode: only for "_fpu" suffix
        // - In explicit FPU mode (68881, 68040): always
        // - In soft mode: never
        _generatingFpuVersion = suffix == "_fpu" || (_fpuMode != "soft" && !IsFatBinary);

        _localVariableOffsets.Clear();    // Clear local variable offsets for new function
        _savedTemps.Clear();              // Clear saved temps for new function
        _savedTempSizes.Clear();          // Clear saved temp sizes for new function
        _tempStackOffset = 0;             // Reset temp stack offset
        _structMemberLocations.Clear();   // Clear struct member locations for new function
        _labelStackDepths.Clear();        // Clear label stack depths for new function
        _currentLabel = null;              // Reset current label

        var mangledName = MangleName(function.Name);
        var functionLabel = $"_{mangledName}{suffix}";
        EmitComment($"Function: {function.Name}{suffix}");

        // Export symbol if function is public OR if it's main (always export main)
        // For fat binaries, only export the base name (dispatch stub will be generated)
        if ((function.IsPublic || function.Name == "main") && suffix == "")
        {
            // Note: VBCC's startup code provides ___main, which calls _main
            // We only need to export _main, not ___main
            Emit($"\txdef\t_{mangledName}");
        }
        Emit($"{functionLabel}:");

        // Function prologue
        if (NeedsPrologue(function))
        {
            EmitPrologue(function);
            _currentFunctionHasPrologue = true;
        }

        // For main function in fat binary mode, initialize CPU detection at startup
        if (function.Name == "main" && IsCpuFatBinary && suffix == "")
        {
            EmitComment("Initialize runtime: detect CPU type once at startup");
            Emit("\tbsr\t__detect_cpu");
            Emit("");
        }

        // Generate code for basic blocks
        foreach (var block in function.BasicBlocks)
        {
            GenerateBasicBlock(block);
        }

        // IMPORTANT: If the function doesn't end with an explicit return statement,
        // we need to emit an epilogue for void functions.
        // Check if the last instruction in the last block is NOT a return
        if (function.BasicBlocks.Count > 0)
        {
            var lastBlock = function.BasicBlocks[^1];
            var lastInstruction = lastBlock.Instructions.Count > 0 ? lastBlock.Instructions[^1] : null;

            // If the last instruction is not a return, emit epilogue for void function
            if (lastInstruction is not IrReturn)
            {
                EmitComment("Implicit return for void function");
                EmitEpilogue(_tempStackOffset);
            }
        }

        Emit("");
    }

    private bool NeedsPrologue(IrFunction function)
    {
        // For this simple proof-of-concept, we'll use minimal prologues
        // Real implementation would analyze the function to determine this
        return function.Parameters.Count > 0 || HasLocalVariables(function);
    }

    private bool HasLocalVariables(IrFunction function)
    {
        return function.LocalVariables.Count > 0;
    }

    private void EmitPrologue(IrFunction function)
    {
        // Track parameter locations (positive offsets from a6)
        // Parameters start at 8(a6) after return address and saved a6
        // BUT if return type > 8 bytes, caller allocates return space on stack,
        // so parameters are offset by sizeof(return_type)
        int paramOffset = 8;
        if (function.ReturnType is IrEnumType enumType && enumType.SizeInBytes > 8)
        {
            paramOffset += enumType.SizeInBytes; // Account for hidden return pointer space
        }

        foreach (var param in function.Parameters)
        {
            _localVariableOffsets[param.Name] = paramOffset;

            // String and other struct parameters are passed as full structs on stack
            // Regular parameters (i32, pointers, etc.) are 4 bytes
            int paramSize = param.Type.SizeInBytes;
            paramOffset += paramSize;
        }

        // Calculate stack space needed for local variables
        // Use CPU-specific alignment: 2 bytes for 68000-68030, 4 for 68040, 8 for 68060/68080
        int stackSpace = 0;
        int currentOffset = 0; // Local variables grow downward from a6
        int alignment = _cpuFeatures.PreferredAlignment;

        foreach (var localVar in function.LocalVariables)
        {
            var size = localVar.Type.SizeInBytes;
            // Align each variable to CPU's preferred alignment
            if (size % alignment != 0)
                size += alignment - (size % alignment);

            currentOffset -= size;
            _localVariableOffsets[localVar.Name] = currentOffset;
            stackSpace += size;
        }

        // Align total stack space to CPU's preferred alignment
        if (stackSpace % alignment != 0)
            stackSpace += alignment - (stackSpace % alignment);

        // CRITICAL: Ensure stack frame is always longword-aligned (multiple of 4)
        // This prevents address errors on 68000 when accessing parameters/locals
        // Even though 68000 only requires word alignment (2), longword alignment
        // is safer and matches AmigaOS conventions
        if (stackSpace % 4 != 0)
            stackSpace += 4 - (stackSpace % 4);

        // Standard 68k function prologue
        if (stackSpace > 0)
        {
            Emit($"\tlink\ta6,#-{stackSpace}");
            _currentFunctionHasPrologue = true;
        }
        else
        {
            Emit("\tlink\ta6,#0");
            _currentFunctionHasPrologue = true;
        }

        // REGISTER ALLOCATION: Save callee-saved registers that are used
        var usedCalleeSavedRegs = GetUsedCalleeSavedRegisters();
        if (usedCalleeSavedRegs.Count > 0)
        {
            EmitComment($"Save callee-saved registers: {string.Join(", ", usedCalleeSavedRegs)}");
            Emit($"\tmovem.l\t{string.Join("/", usedCalleeSavedRegs)},-(sp)");
        }

        // REGISTER ALLOCATION: Move parameters from ABI locations to allocated registers
        if (_currentFunctionRegAlloc != null)
        {
            // ABI calling convention: d0, d1, a0, a1, then stack
            var abiRegs = new[] { "d0", "d1", "a0", "a1" };
            int paramIndex = 0;

            foreach (var param in function.Parameters)
            {
                var allocatedReg = _currentFunctionRegAlloc.GetRegister(param.Name);
                if (allocatedReg != null)
                {
                    string sourceReg;
                    if (paramIndex < 4)
                    {
                        sourceReg = abiRegs[paramIndex];
                    }
                    else
                    {
                        // Parameter is on stack - load it first to d0, then move to allocated register
                        int stackOffset = _localVariableOffsets[param.Name];
                        sourceReg = "d0";
                        if (allocatedReg != "d0")
                        {
                            EmitComment($"Load parameter {param.Name} from stack to {allocatedReg}");
                            Emit($"\tmove.l\t{stackOffset}(a6),{allocatedReg}");
                        }
                        paramIndex++;
                        continue;
                    }

                    if (sourceReg != allocatedReg)
                    {
                        EmitComment($"Move parameter {param.Name} from {sourceReg} to {allocatedReg}");
                        Emit($"\tmove.l\t{sourceReg},{allocatedReg}");
                    }
                }
                paramIndex++;
            }
        }
    }

    /// <summary>
    /// Get list of callee-saved registers used by current function (d2-d7, a2-a5)
    /// </summary>
    private List<string> GetUsedCalleeSavedRegisters()
    {
        var used = new List<string>();
        if (_currentFunctionRegAlloc == null) return used;

        // Callee-saved registers we can use
        var calleeSaved = new[] { "d2", "d3", "d4", "d5", "d6", "d7", "a2", "a3", "a4", "a5" };

        // Check which ones are actually used in this function
        foreach (var reg in calleeSaved)
        {
            // Check if any variable is allocated to this register
            foreach (var kvp in _currentFunctionRegAlloc.VariableToRegister)
            {
                if (kvp.Value == reg)
                {
                    used.Add(reg);
                    break;
                }
            }
        }

        // Sort: data registers first (d2-d7), then address registers (a2-a5)
        used.Sort((a, b) =>
        {
            if (a[0] == b[0]) return string.Compare(a, b, StringComparison.Ordinal);
            return a[0] == 'd' ? -1 : 1;
        });

        return used;
    }

    private void EmitEpilogue(int? stackCleanup = null)
    {
        // Clean up any temp variables saved on the stack
        // Use provided cleanup amount if specified, otherwise use current _tempStackOffset
        int cleanup = stackCleanup ?? _tempStackOffset;
        if (cleanup > 0)
        {
            Emit($"\tlea\t{cleanup}(sp),sp");
        }

        // REGISTER ALLOCATION: Restore callee-saved registers that were saved
        var usedCalleeSavedRegs = GetUsedCalleeSavedRegisters();
        if (usedCalleeSavedRegs.Count > 0)
        {
            EmitComment($"Restore callee-saved registers: {string.Join(", ", usedCalleeSavedRegs)}");
            Emit($"\tmovem.l\t(sp)+,{string.Join("/", usedCalleeSavedRegs)}");
        }

        // Only emit unlk if we emitted link in the prologue
        if (_currentFunctionHasPrologue)
        {
            Emit("\tunlk\ta6");
        }
        Emit("\trts");
    }

    private void EmitDeferredBlocks()
    {
        if (_currentFunction == null || _currentFunction.DeferredBlocks.Count == 0)
        {
            return;
        }

        EmitComment("Execute deferred blocks (LIFO order)");

        // Execute deferred blocks in LIFO order (reverse order of insertion)
        for (int i = _currentFunction.DeferredBlocks.Count - 1; i >= 0; i--)
        {
            var deferBlock = _currentFunction.DeferredBlocks[i];
            EmitComment($"Deferred block: {deferBlock.Label}");

            // Generate code for each instruction in the deferred block
            for (int j = 0; j < deferBlock.Instructions.Count; j++)
            {
                GenerateInstruction(deferBlock.Instructions[j], deferBlock.Instructions, j);
            }
        }
    }

}
