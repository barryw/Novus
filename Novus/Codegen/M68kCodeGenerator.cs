using System.Text;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Generates Motorola 68000 assembly code from IR
/// Targets VBCC's vasm assembler syntax
/// </summary>
public partial class M68kCodeGenerator
{
    private readonly StringBuilder _output = new();
    private readonly IrModule _module;
    private readonly string _cpuTarget;
    private readonly M68kCpuFeatures _cpuFeatures;
    private readonly string _fpuMode;
    private readonly Dictionary<string, int> _registerAllocation = new();
    private int _labelCounter = 0;
    private IrFunction? _currentFunction;
    private bool _currentFunctionHasPrologue = false;
    private bool _generatingFpuVersion = false; // true when generating _fpu version of function
    private string _currentFunctionSuffix = ""; // suffix for current function (e.g., "_68000", "_68020", "_68060")
    private readonly bool _isOriginallyFatBinary; // true if original cpuTarget was "auto", persists across function generation

    // Track last comparison for optimization
    private string? _lastComparisonResult;
    private string? _lastComparisonCondition;

    // Track local variable stack offsets
    private readonly Dictionary<string, int> _localVariableOffsets = new();

    // Track temp variables saved on stack (in order of saving)
    private readonly List<string> _savedTemps = new();
    private readonly Dictionary<string, int> _savedTempSizes = new(); // Track size of each saved temp
    private int _tempStackOffset = 0; // Total bytes used for temps
    // Map temp names to global variables they should reload from (for match on globals)
    private readonly Dictionary<string, string> _globalTagTemps = new();

    // Track which functions use floating point operations
    private readonly HashSet<string> _floatFunctions = new();

    // Track which functions benefit from CPU-specific optimizations
    private readonly HashSet<string> _cpuOptimizableFunctions = new();

    // Track floating point constants for data section
    private readonly Dictionary<string, double> _floatConstants = new();
    private int _floatConstCounter = 0;

    // Track struct member access locations (for chained access like a.b.c)
    // Maps temp variable name to (base variable name, cumulative offset)
    private readonly Dictionary<string, (string baseVar, int cumulativeOffset)> _structMemberLocations = new();

    // Track string literals for data section
    private List<IrStringLiteral> _stringLiterals = new();

    public M68kCodeGenerator(IrModule module, List<IrStringLiteral> stringLiterals, string cpuTarget = "68000", string fpuMode = "auto")
    {
        _module = module;
        _stringLiterals = stringLiterals;
        _cpuTarget = cpuTarget.ToLower();
        _cpuFeatures = new M68kCpuFeatures(_cpuTarget);
        _fpuMode = fpuMode.ToLower();
        _isOriginallyFatBinary = _cpuTarget == "auto"; // Remember if we started as fat binary
    }

    private bool Is68020Plus => _cpuFeatures.IsAtLeast(2);

    // Check if we're building fat binaries with runtime FPU detection
    private bool IsFatBinary => _fpuMode == "auto";

    // Check if we're building fat binaries with runtime CPU detection
    private bool IsCpuFatBinary => _cpuTarget == "auto";

    // Check if a function uses true floating point operations (not fixed-point)
    // Fixed-point uses integer operations, so it doesn't need hardware FPU
    private bool UsesFPU(IrFunction function)
    {
        return function.Parameters.Any(p => p.Type is IrFloatType) ||
               function.ReturnType is IrFloatType ||
               function.LocalVariables.Any(v => v.Type is IrFloatType) ||
               function.BasicBlocks.Any(bb => bb.Instructions.Any(IsFloatInstruction));
    }

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

    public string Generate()
    {
        _output.Clear();

        // Scan all functions to identify which use floating point
        foreach (var function in _module.Functions)
        {
            if (UsesFPU(function))
            {
                _floatFunctions.Add(function.Name);
            }
        }

        // Scan all functions to identify which benefit from CPU-specific optimizations
        if (IsCpuFatBinary)
        {
            foreach (var function in _module.Functions)
            {
                if (UsesCpuOptimizations(function))
                {
                    _cpuOptimizableFunctions.Add(function.Name);
                }
            }
        }

        // Emit file header
        EmitHeader();

        // Emit extern function references first
        var externFunctions = _module.Functions.Where(f => f.IsExtern).ToList();
        if (externFunctions.Any())
        {
            foreach (var extFunc in externFunctions)
            {
                EmitComment($"External function: {extFunc.Name}");
                Emit($"\txref\t_{extFunc.Name}");
            }
            Emit("");
        }

        // Emit _exit reference if we have a main function (needed for proper exit code handling)
        if (_module.Functions.Any(f => f.Name == "main"))
        {
            EmitComment("C library exit function (for main return)");
            Emit("\txref\t_exit");
            Emit("");
        }

        // Emit DOS init/cleanup references if DOS functions are used
        // These will be called automatically via constructor/destructor lists
        var dosFunctionNames = new[] { "Write", "Read", "Open", "Close", "Output", "Input", "Error" };
        var usesDOS = _module.Functions.Any(f => f.IsExtern && dosFunctionNames.Contains(f.Name));
        if (usesDOS)
        {
            EmitComment("DOS library auto-initialization (called by VBCC startup)");
            Emit("\txref\t___dos_init");
            Emit("\txref\t___dos_cleanup");
            Emit("");
        }

        // Always generate CPU detection for fat binaries (needed for system.novus variables)
        if (IsCpuFatBinary)
        {
            GenerateCpuDetection();

            // Only generate dispatch stubs if we have CPU-optimizable functions
            if (_cpuOptimizableFunctions.Any())
            {
                GenerateCpuDispatchStubs();
            }
        }

        // Always generate optimized runtime library primitives for assembly programmers
        // These are exported and usable by any assembly code
        if (IsCpuFatBinary)
        {
            GenerateOptimizedRuntimePrimitives();
        }

        // If building fat binary and we have float functions, generate FPU detection
        if (IsFatBinary && _floatFunctions.Any())
        {
            GenerateFpuDetection();
            GenerateFunctionDispatchTable();
        }

        // Generate code for each non-extern function, with main first if it exists
        // (vamos executes the first function in the binary as the entry point)
        var mainFunction = _module.Functions.FirstOrDefault(f => f.Name == "main" && !f.IsExtern);
        var otherFunctions = _module.Functions.Where(f => f.Name != "main" && !f.IsExtern);

        if (mainFunction != null)
        {
            if (IsCpuFatBinary && _cpuOptimizableFunctions.Contains("main"))
            {
                // Generate CPU-specific versions
                GenerateFunctionWithCpuTarget(mainFunction, "68000", "_68000");
                GenerateFunctionWithCpuTarget(mainFunction, "68020", "_68020");
                GenerateFunctionWithCpuTarget(mainFunction, "68060", "_68060");
            }
            else if (IsFatBinary && _floatFunctions.Contains("main"))
            {
                // Generate both soft and FPU versions
                GenerateFunction(mainFunction, "_soft");
                GenerateFunction(mainFunction, "_fpu");
            }
            else
            {
                GenerateFunction(mainFunction);
            }
        }

        foreach (var function in otherFunctions)
        {
            if (IsCpuFatBinary && _cpuOptimizableFunctions.Contains(function.Name))
            {
                // Generate CPU-specific versions
                GenerateFunctionWithCpuTarget(function, "68000", "_68000");
                GenerateFunctionWithCpuTarget(function, "68020", "_68020");
                GenerateFunctionWithCpuTarget(function, "68060", "_68060");
            }
            else if (IsFatBinary && _floatFunctions.Contains(function.Name))
            {
                // Generate both soft and FPU versions
                GenerateFunction(function, "_soft");
                GenerateFunction(function, "_fpu");
            }
            else
            {
                GenerateFunction(function);
            }
        }

        // Emit floating point constants in data section (for hardware FPU)
        if (_floatConstants.Any())
        {
            EmitFloatConstants();
        }

        // Emit string literals in data section
        if (_stringLiterals.Any())
        {
            EmitStringLiterals();
        }

        return _output.ToString();
    }

    private void EmitFloatConstants()
    {
        Emit("");
        Emit("\tsection\tdata,data");
        Emit("");

        foreach (var (label, value) in _floatConstants)
        {
            EmitComment($"Float constant: {value}");
            Emit($"{label}:");

            // Emit as IEEE-754 single precision (4 bytes)
            uint bits = BitConverter.SingleToUInt32Bits((float)value);
            Emit($"\tdc.l\t${bits:X8}");
        }

        Emit("");
        Emit("\tsection\ttext,code");
        Emit("");
    }

    private void EmitStringLiterals()
    {
        Emit("");
        Emit("\tsection\tdata,data");
        Emit("");

        foreach (var str in _stringLiterals)
        {
            EmitComment($"String literal: \"{EscapeStringForComment(str.Value)}\"");
            Emit($"{str.Label}:");

            // Emit string as null-terminated byte array
            foreach (char c in str.Value)
            {
                Emit($"\tdc.b\t{(byte)c}");
            }
            Emit($"\tdc.b\t0\t; null terminator");
            Emit($"\teven\t; ensure word alignment for 68000");
            Emit("");
        }

        Emit("\tsection\ttext,code");
        Emit("");
    }

    private string EscapeStringForComment(string s)
    {
        return s.Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\r", "\\r")
                .Replace("\"", "\\\"");
    }

    private void EmitHeader()
    {
        EmitComment("Generated by Novus compiler");
        EmitComment($"Target: Motorola {_cpuTarget.ToUpper()}");
        Emit("");

        // Declare external soft-float library functions if needed
        if (IsFatBinary || _fpuMode == "soft")
        {
            Emit("\t; External VBCC soft-float library functions");
            Emit("\txref\t__ieeeaddl");   // f32 addition
            Emit("\txref\t__ieeesubl");   // f32 subtraction
            Emit("\txref\t__ieeemull");   // f32 multiplication
            Emit("\txref\t__ieeedivl");   // f32 division
            Emit("\txref\t__ieeeaddd");   // f64 addition
            Emit("\txref\t__ieeesubd");   // f64 subtraction
            Emit("\txref\t__ieeemuld");   // f64 multiplication
            Emit("\txref\t__ieeedivd");   // f64 division
            Emit("");
        }

        Emit("\tsection\ttext,code");
        Emit("");

        // Provide C++ constructor/destructor list symbols (required by VBCC startup code)
        // Only emit these in the main module (module with main function)
        var hasMainFunction = _module.Functions.Any(f => f.Name == "main" && !f.IsExtern);

        if (hasMainFunction)
        {
            // Check if we need DOS library initialization
            var dosFunctionNames = new[] { "Write", "Read", "Open", "Close", "Output", "Input", "Error" };
            var usesDOS = _module.Functions.Any(f => f.IsExtern && dosFunctionNames.Contains(f.Name));

            Emit("\t; C++ constructor/destructor lists (main module only)");
            Emit("\tsection\tdata,data");
            Emit("\txdef\t___CTOR_LIST__");
            Emit("\txdef\t___DTOR_LIST__");
            Emit("___CTOR_LIST__:");

            if (usesDOS)
            {
                EmitComment("Automatic DOS library initialization");
                Emit("\tdc.l\t1\t; Count of constructors");
                Emit("\tdc.l\t___dos_init\t; Initialize dos.library before main");
                Emit("\tdc.l\t0\t; Terminator");
            }
            else
            {
                Emit("\tdc.l\t0\t; Count of constructors");
                Emit("\tdc.l\t0\t; Terminator");
            }

            Emit("___DTOR_LIST__:");

            if (usesDOS)
            {
                EmitComment("Automatic DOS library cleanup");
                Emit("\tdc.l\t1\t; Count of destructors");
                Emit("\tdc.l\t___dos_cleanup\t; Close dos.library after main");
                Emit("\tdc.l\t0\t; Terminator");
            }
            else
            {
                Emit("\tdc.l\t0\t; Count of destructors");
                Emit("\tdc.l\t0\t; Terminator");
            }

            Emit("");
            Emit("\tsection\ttext,code");
            Emit("");
        }
    }

    /// <summary>
    /// Generate a function with a specific CPU target and suffix
    /// Used for CPU fat binaries to generate 68000/68020/68060 versions
    /// </summary>
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

        // Handle extern functions - just emit xref
        // Also treat functions with no body (imported declarations) as extern
        if (function.IsExtern || function.BasicBlocks.Count == 0)
        {
            EmitComment($"External function: {function.Name}");
            Emit($"\txref\t_{function.Name}");
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

        var functionLabel = $"_{function.Name}{suffix}";
        EmitComment($"Function: {function.Name}{suffix}");

        // Export symbol only if function is public (using xdef for vasm Motorola syntax)
        // For fat binaries, only export the base name (dispatch stub will be generated)
        if (function.IsPublic && suffix == "")
        {
            // Note: VBCC's startup code provides ___main, which calls _main
            // We only need to export _main, not ___main
            Emit($"\txdef\t_{function.Name}");
        }
        Emit($"{functionLabel}:");

        // Function prologue
        if (NeedsPrologue(function))
        {
            EmitPrologue(function);
            _currentFunctionHasPrologue = true;
        }

        // Generate code for basic blocks
        foreach (var block in function.BasicBlocks)
        {
            GenerateBasicBlock(block);
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
        int paramOffset = 8; // Parameters start at 8(a6) after return address and saved a6
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

        // Standard 68k function prologue
        if (stackSpace > 0)
        {
            Emit($"\tlink\ta6,#-{stackSpace}");
        }
        else
        {
            Emit("\tlink\ta6,#0");
        }

        // Save registers if needed (we'll implement register allocation later)
        // For now, we'll keep it minimal
    }

    private void EmitEpilogue()
    {
        // Clean up any temp variables saved on the stack
        if (_tempStackOffset > 0)
        {
            Emit($"\tlea\t{_tempStackOffset}(sp),sp");
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
            foreach (var instruction in deferBlock.Instructions)
            {
                GenerateInstruction(instruction);
            }
        }
    }

    private void GenerateBasicBlock(IrBasicBlock block)
    {
        // Emit block label if it's not the entry block
        if (block.Label != "entry")
        {
            // Include suffix for CPU-specific versions to avoid label conflicts
            Emit($"{block.Label}{_currentFunctionSuffix}:");
        }

        foreach (var instruction in block.Instructions)
        {
            GenerateInstruction(instruction);
        }
    }

    private void GenerateInstruction(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrReturn ret:
                GenerateReturn(ret);
                break;
            case IrBinaryOp binOp:
                GenerateBinaryOp(binOp);
                break;
            case IrLabel label:
                GenerateLabel(label);
                break;
            case IrBranch branch:
                GenerateBranch(branch);
                break;
            case IrConditionalBranch condBranch:
                GenerateConditionalBranch(condBranch);
                break;
            case IrCall call:
                GenerateCall(call);
                break;
            case IrIndirectCall indirectCall:
                GenerateIndirectCall(indirectCall);
                break;
            case IrLocalDecl localDecl:
                GenerateLocalDecl(localDecl);
                break;
            case IrStore store:
                GenerateStore(store);
                break;
            case IrDereferenceStore derefStore:
                GenerateDereferenceStore(derefStore);
                break;
            case IrIndexAccess indexAccess:
                GenerateIndexAccess(indexAccess);
                break;
            case IrIndexStore indexStore:
                GenerateIndexStore(indexStore);
                break;
            case IrMemberAccess memberAccess:
                GenerateMemberAccess(memberAccess);
                break;
            case IrMemberStore memberStore:
                GenerateMemberStore(memberStore);
                break;
            case IrExtractTag extractTag:
                GenerateExtractTag(extractTag);
                // For global variables (CPU, FPU, Chipset), don't save - we'll reload from global each time
                // For local enum variables, save to stack since they can't be reloaded
                if (extractTag.EnumValue is IrVariable enumVar &&
                    (enumVar.Name == "CPU" || enumVar.Name == "FPU" || enumVar.Name == "Chipset"))
                {
                    // Don't save - we'll reload from global for each comparison
                    // Track that this temp should reload from the global
                    _globalTagTemps[extractTag.ResultName] = enumVar.Name;
                }
                else
                {
                    // Save the extracted tag to stack since it will be used multiple times in match
                    Emit($"\tmove.l\td0,-(sp)\t\t; Save extracted tag for match");
                    _savedTemps.Add(extractTag.ResultName);
                    _tempStackOffset += 4;
                }
                break;
            case IrExtractVariantData extractData:
                GenerateExtractVariantData(extractData);
                break;
            case IrDefer defer:
                // Defer instructions are markers only - actual code is in DeferredBlocks
                // which are emitted at function exit points
                break;
            default:
                throw new Exception($"Unknown instruction type: {instruction.GetType().Name}");
        }
    }

    private void GenerateLabel(IrLabel label)
    {
        // Include suffix for CPU-specific versions to avoid label conflicts
        Emit($"{label.Name}{_currentFunctionSuffix}:");
    }

    private void GenerateBranch(IrBranch branch)
    {
        // Include suffix for CPU-specific versions to avoid label conflicts
        Emit($"\tbra\t{branch.Target}{_currentFunctionSuffix}");
    }

    private void GenerateConditionalBranch(IrConditionalBranch condBranch)
    {
        // The condition value should be in d0 (materialized as 0 or 1)
        // Test it and branch accordingly
        LoadOperand(condBranch.Condition, "d0");
        Emit($"\ttst.l\td0");
        // Include suffix for CPU-specific versions to avoid label conflicts
        Emit($"\tbne\t{condBranch.TrueTarget}{_currentFunctionSuffix}");
        Emit($"\tbra\t{condBranch.FalseTarget}{_currentFunctionSuffix}");

        // Clear comparison tracking
        _lastComparisonResult = null;
        _lastComparisonCondition = null;
    }

    private void GenerateReturn(IrReturn ret)
    {
        if (ret.Value != null)
        {
            // Check if we're returning a true floating point value (not fixed-point) in FPU mode
            // Fixed-point uses integer representation, so it always goes in D0
            bool isTrueFloatReturn = ret.Value.Type is IrFloatType;

            // Check if we're returning an enum constructor (simple enum variant)
            if (ret.Value is IrEnumConstructor enumCtor)
            {
                // Simple enum variant - just load the tag value into D0
                EmitComment($"Return enum {enumCtor.Type.Name}::{enumCtor.VariantName} = {enumCtor.VariantTag}");
                Emit($"\tmoveq\t#{enumCtor.VariantTag},d0\t\t; Enum tag");
            }
            // Check if we're returning an enum value with associated data
            else if (ret.Value is IrEnumValue enumValue)
            {
                EmitComment($"Return enum value {enumValue.Type.Name}::{enumValue.VariantName}");

                // For small enums (8 bytes), we can return in D0+D1
                // For now, load enum into address register and then copy to D0+D1
                LoadOperand(ret.Value, "a0");  // This will push enum to stack and load address
                Emit("\tmove.l\t(a0),d0\t\t; Load enum tag");

                if (enumValue.Type.SizeInBytes > 4)
                {
                    Emit("\tmove.l\t4(a0),d1\t\t; Load enum data");
                }
            }
            // Check if we're returning an enum type (composite value)
            else if (ret.Value.Type is IrEnumType enumType && ret.Value is IrVariable enumVar)
            {
                // For enum types, load from stack location into D0+D1 (8 bytes max)
                EmitComment($"Return enum {enumType.EnumName} ({enumType.SizeInBytes} bytes)");

                int offset;

                // Check if it's a local variable or a temporary
                if (_localVariableOffsets.ContainsKey(enumVar.Name))
                {
                    offset = _localVariableOffsets[enumVar.Name];
                }
                else if (enumVar.Name.StartsWith("%t"))
                {
                    // It's a temporary - check if it's saved on the stack
                    var tempIndex = _savedTemps.IndexOf(enumVar.Name);
                    if (tempIndex >= 0)
                    {
                        // Calculate offset from top of stack
                        offset = (_savedTemps.Count - 1 - tempIndex) * 4;
                        // Convert to frame pointer offset
                        offset = -(_tempStackOffset - offset);

                        // Load first 4 bytes (tag) into D0
                        Emit($"\tmove.l\t{offset}(a6),d0\t\t; Load enum tag");

                        // If enum has data (size > 4), load next 4 bytes into D1
                        if (enumType.SizeInBytes > 4)
                        {
                            Emit($"\tmove.l\t{offset + 4}(a6),d1\t\t; Load enum data");
                        }
                    }
                    else
                    {
                        // Temporary not saved - try to load it with LoadOperand
                        EmitComment($"Load enum temporary {enumVar.Name}");
                        LoadOperand(ret.Value, "a0");  // Load into address register

                        // Load from address into D0+D1
                        Emit("\tmove.l\t(a0),d0\t\t; Load enum tag");
                        if (enumType.SizeInBytes > 4)
                        {
                            Emit("\tmove.l\t4(a0),d1\t\t; Load enum data");
                        }
                    }
                }
                else
                {
                    offset = _localVariableOffsets[enumVar.Name];

                    // Load first 4 bytes (tag) into D0
                    Emit($"\tmove.l\t{offset}(a6),d0\t\t; Load enum tag");

                    // If enum has data (size > 4), load next 4 bytes into D1
                    if (enumType.SizeInBytes > 4)
                    {
                        Emit($"\tmove.l\t{offset + 4}(a6),d1\t\t; Load enum data");
                    }
                }
            }
            else if (_generatingFpuVersion && isTrueFloatReturn)
            {
                // Hardware FPU mode - return value in FP0 (for true floating point only)
                // LoadOperand will place float constants in FP0
                LoadOperand(ret.Value, "fp0");
            }
            else
            {
                // Standard ABI - return value in D0
                // This includes: integers, fixed-point, and soft-float mode
                LoadOperand(ret.Value, "d0");
            }
        }

        // Execute deferred blocks in LIFO order (reverse of insertion order)
        EmitDeferredBlocks();

        // Special handling for main function - call _exit() instead of returning
        // This ensures the exit code is properly passed to AmigaOS
        if (_currentFunction != null && _currentFunction.Name == "main")
        {
            EmitComment("Exit with return code (main function)");

            // Push return value (already in d0) as argument to _exit()
            if (ret.Value != null)
            {
                Emit("\tmove.l\td0,-(sp)\t\t; Push exit code");
            }
            else
            {
                Emit("\tmoveq\t#0,d0\t\t; Default exit code 0");
                Emit("\tmove.l\td0,-(sp)");
            }

            // Call VBCC's exit function - never returns
            Emit("\tjsr\t_exit\t\t; Terminate process");

            // These instructions never execute, but keep epilogue for consistency
            if (ret.Value != null)
            {
                Emit("\taddq.l\t#4,sp\t\t; (never reached)");
            }
        }

        // Emit epilogue and return (for non-main functions, or unreachable code after exit)
        EmitEpilogue();
    }

    private void GenerateBinaryOp(IrBinaryOp binOp)
    {
        var size = GetSizeSuffix(binOp.Type);
        bool isFloatOp = binOp.Type is IrFloatType;

        // Simplified code generation - load operands and perform operation
        // Real implementation would do proper register allocation

        switch (binOp.Operation)
        {
            case IrBinaryOp.OpKind.Add:
                EmitComment($"{binOp.ResultName} = add");
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
                else
                {
                    // Integer addition
                    LoadOperand(binOp.Left, "d0");
                    LoadOperand(binOp.Right, "d1");
                    Emit($"\tadd{size}\td1,d0");
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
                GenerateComparison(binOp);
                break;
        }

        // Result is in d0 - don't save to stack to avoid stack overflow in loops
        // If the temp is used later, LoadOperand will use d0 directly if temp not in _savedTemps
        // This works because most temps are used immediately in the next instruction
    }

    private void GenerateComparison(IrBinaryOp binOp)
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
            LoadOperand(binOp.Left, "d0");
            LoadOperand(binOp.Right, "d1");

            // Compare: cmp.l d1,d0 computes (d0 - d1) and sets condition codes
            Emit($"\tcmp{size}\td1,d0");

            // Materialize boolean result in d0 (for cases where it's used as a value)
            // Scc sets byte to $FF if condition true, $00 if false
            Emit($"\ts{condition}\td0");

            // Extend byte result to longword (sign extend $FF to $FFFFFFFF, $00 to $00000000)
            // Note: extb.l doesn't exist on 68000, use ext.w + ext.l sequence
            Emit($"\text.w\td0");   // Extend byte to word: $FF → $FFFF or $00 → $0000
            Emit($"\text.l\td0");   // Extend word to long: $FFFF → $FFFFFFFF or $0000 → $00000000

            // Convert $FFFFFFFF to $00000001 for true, $00000000 stays $00000000
            Emit($"\tneg.l\td0");
        }

        // Track this comparison for potential optimization in conditional branch
        _lastComparisonResult = binOp.ResultName;
        _lastComparisonCondition = condition;
    }

    private void GenerateCall(IrCall call)
    {
        EmitComment($"Call {call.FunctionName}");

        // Look up the function to get parameter types
        var function = _module.Functions.FirstOrDefault(f => f.Name == call.FunctionName);

        int totalBytesPushed = 0;

        // Push arguments onto stack in reverse order (right to left, per C calling convention)
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            var arg = call.Arguments[i];

            // Get the expected parameter type from the function signature
            IrType? paramType = null;
            if (function != null && i < function.Parameters.Count)
            {
                paramType = function.Parameters[i].Type;
            }

            // Handle String (and other struct) parameters
            if (paramType is IrStringType)
            {
                EmitComment($"Push String argument (8 bytes: ptr + len)");

                // For String variable, we need to push both fields
                // String layout: {ptr: *u8 at offset 0, len: i32 at offset 4}

                if (arg is IrVariable stringVar)
                {
                    // Get the stack location of the String variable
                    if (_localVariableOffsets.TryGetValue(stringVar.Name, out int offset))
                    {
                        // Push len field (offset 4) first - it will be at higher address
                        Emit($"\tmove.l\t{offset + 4}(a6),-(sp)");
                        // Push ptr field (offset 0) second - it will be at lower address
                        Emit($"\tmove.l\t{offset}(a6),-(sp)");
                        totalBytesPushed += 8;
                    }
                    else
                    {
                        throw new Exception($"Unknown String variable: {stringVar.Name}");
                    }
                }
                else if (arg is IrStringLiteral stringLiteral)
                {
                    // Push string literal as String argument (ptr + len)
                    // Load address of string literal
                    Emit($"\tlea\t{stringLiteral.Label},a0");
                    // Push length first
                    Emit($"\tmove.l\t#{stringLiteral.Value.Length},-(sp)");
                    // Push pointer second
                    Emit($"\tmove.l\ta0,-(sp)");
                    totalBytesPushed += 8;
                }
                else
                {
                    throw new Exception($"Unsupported String argument type: {arg.GetType().Name}");
                }
            }
            else
            {
                // Regular 4-byte argument (i32, pointers, etc.)
                var argsPushedSoFar = totalBytesPushed;

                // Special handling for temp variables to account for changing SP
                if (arg is IrVariable variable && variable.Name.StartsWith("%t"))
                {
                    var savedIndex = _savedTemps.IndexOf(variable.Name);
                    if (savedIndex >= 0)
                    {
                        // Calculate offset including arguments already pushed for this call
                        var baseOffset = (_savedTemps.Count - 1 - savedIndex) * 4;
                        var adjustedOffset = baseOffset + argsPushedSoFar;
                        var tempSize = GetSizeSuffix(variable.Type);
                        Emit($"\tmove{tempSize}\t{adjustedOffset}(sp),d0");
                        Emit("\tmove.l\td0,-(sp)");
                        totalBytesPushed += 4;
                        continue;
                    }
                }

                // Load argument into d0 first
                LoadOperand(arg, "d0");

                // Push d0 onto stack
                Emit("\tmove.l\td0,-(sp)");
                totalBytesPushed += 4;
            }
        }

        // Call the function using JSR (Jump to Subroutine)
        Emit($"\tjsr\t_{call.FunctionName}");

        // Clean up stack (remove arguments)
        if (totalBytesPushed > 0)
        {
            Emit($"\tlea\t{totalBytesPushed}(sp),sp");
        }

        // Handle return value based on type
        if (call.ResultName != null)
        {
            // Check if this is a composite type (enum with size > 4 bytes)
            // Use call.ReturnType which was set during IR building
            if (call.ReturnType is IrEnumType enumType && enumType.SizeInBytes > 4)
            {
                // For composite types, result is in D0+D1
                // Save both registers to stack
                EmitComment($"Save composite return value ({enumType.SizeInBytes} bytes)");
                Emit("\tmove.l\td1,-(sp)\t\t; Save enum data (D1)");
                Emit("\tmove.l\td0,-(sp)\t\t; Save enum tag (D0)");
                _savedTemps.Add(call.ResultName);
                _savedTempSizes[call.ResultName] = 8;  // Track that this temp is 8 bytes
                _tempStackOffset += 8;
            }
            else
            {
                // Result is in d0 - don't save to stack to avoid stack overflow in loops
                // If the temp is used later, LoadOperand will use d0 directly
            }
        }
    }

    /// <summary>
    /// Check if a temp variable is actually used/referenced in any instruction
    /// </summary>
    private bool IsTempUsed(string tempName)
    {
        foreach (var block in _currentFunction!.BasicBlocks)
        {
            foreach (var instr in block.Instructions)
            {
                if (InstructionUsesTemp(instr, tempName))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Check if an instruction references a specific temp variable
    /// </summary>
    private bool InstructionUsesTemp(IrInstruction instr, string tempName)
    {
        return instr switch
        {
            IrBinaryOp binOp => UsesTemp(binOp.Left, tempName) || UsesTemp(binOp.Right, tempName),
            IrReturn ret => ret.Value != null && UsesTemp(ret.Value, tempName),
            IrCall call => call.Arguments.Any(arg => UsesTemp(arg, tempName)),
            IrIndirectCall indCall => UsesTemp(indCall.FunctionPointer, tempName) || indCall.Arguments.Any(arg => UsesTemp(arg, tempName)),
            IrStore store => UsesTemp(store.Value, tempName),
            IrIndexStore idxStore => UsesTemp(idxStore.Array, tempName) || UsesTemp(idxStore.Index, tempName) || UsesTemp(idxStore.Value, tempName),
            IrIndexAccess idxAccess => UsesTemp(idxAccess.Array, tempName) || UsesTemp(idxAccess.Index, tempName),
            IrMemberAccess memAccess => UsesTemp(memAccess.Struct, tempName),
            IrMemberStore memStore => UsesTemp(memStore.Struct, tempName) || UsesTemp(memStore.Value, tempName),
            IrConditionalBranch condBr => UsesTemp(condBr.Condition, tempName),
            _ => false
        };
    }

    private bool UsesTemp(IrValue value, string tempName)
    {
        return value is IrVariable var && var.Name == tempName;
    }

    private void GenerateIndirectCall(IrIndirectCall call)
    {
        EmitComment("Indirect call through function pointer");

        // Push arguments onto stack in reverse order (right to left)
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            var arg = call.Arguments[i];
            var argsPushedSoFar = call.Arguments.Count - 1 - i;

            // Special handling for temp variables to account for changing SP
            if (arg is IrVariable variable && variable.Name.StartsWith("%t"))
            {
                var savedIndex = _savedTemps.IndexOf(variable.Name);
                if (savedIndex >= 0)
                {
                    // Calculate offset including arguments already pushed for this call
                    var baseOffset = (_savedTemps.Count - 1 - savedIndex) * 4;
                    var adjustedOffset = baseOffset + (argsPushedSoFar * 4);
                    var tempSize = GetSizeSuffix(variable.Type);
                    Emit($"\tmove{tempSize}\t{adjustedOffset}(sp),d0");
                    Emit("\tmove.l\td0,-(sp)");
                    continue;
                }
            }

            // Load argument into d0 first
            LoadOperand(arg, "d0");

            // Push d0 onto stack
            Emit("\tmove.l\td0,-(sp)");
        }

        // Load function pointer into a0
        LoadOperand(call.FunctionPointer, "a0");

        // Call through the function pointer using JSR (a0)
        Emit("\tjsr\t(a0)");

        // Clean up stack (remove arguments)
        if (call.Arguments.Count > 0)
        {
            var stackCleanup = call.Arguments.Count * 4;
            Emit($"\tlea\t{stackCleanup}(sp),sp");
        }

        // Result is in d0 - don't save to stack
    }

    private void GenerateLocalDecl(IrLocalDecl localDecl)
    {
        EmitComment($"{(localDecl.IsMutable ? "var" : "let")} {localDecl.Name}: {localDecl.Type.Name}");

        // Special handling for array initialization
        if (localDecl.InitialValue is IrArrayLiteral arrayLiteral)
        {
            var arrayType = (IrArrayType)localDecl.Type;
            var elementSize = arrayType.ElementType.SizeInBytes;
            var elementSizeSuffix = GetSizeSuffix(arrayType.ElementType);
            var arrayBaseOffset = _localVariableOffsets[localDecl.Name];

            // Initialize each element
            for (int i = 0; i < arrayLiteral.Elements.Count; i++)
            {
                var element = arrayLiteral.Elements[i];

                // Load element value into d0
                LoadOperand(element, "d0");

                // Calculate offset for this element (arrays grow downward on stack)
                var elementOffset = arrayBaseOffset - (i * elementSize);

                // Store to array location
                Emit($"\tmove{elementSizeSuffix}\td0,{elementOffset}(a6)");
            }
        }
        // Special handling for struct initialization
        else if (localDecl.InitialValue is IrStructLiteral structLiteral)
        {
            var structType = (IrStructType)localDecl.Type;
            var structBaseOffset = _localVariableOffsets[localDecl.Name];

            // Initialize each field
            InitializeStructFields(structLiteral, structType, structBaseOffset);
        }
        // Special handling for enum value initialization
        else if (localDecl.InitialValue is IrEnumValue enumValue)
        {
            var enumType = (IrEnumType)localDecl.Type;
            var enumBaseOffset = _localVariableOffsets[localDecl.Name];

            // Store tag at offset 0
            Emit($"\tmove.l\t#{enumValue.VariantTag},{enumBaseOffset}(a6)\t\t; Store enum tag");

            // Store associated values starting at offset 4
            int dataOffset = 4;
            for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
            {
                var assocValue = enumValue.AssociatedValues[i];
                LoadOperand(assocValue, "d0");

                var valueSize = GetSizeSuffix(assocValue.Type);
                Emit($"\tmove{valueSize}\td0,{enumBaseOffset + dataOffset}(a6)\t\t; Store associated value {i}");
                dataOffset += assocValue.Type.SizeInBytes;
            }
        }
        // Special handling for String literal initialization
        else if (localDecl.InitialValue is IrStringLiteral stringLiteral)
        {
            var stringBaseOffset = _localVariableOffsets[localDecl.Name];

            // Escape newlines and other special chars in comments to avoid breaking assembly
            var escapedValue = stringLiteral.Value
                .Replace("\\", "\\\\")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
            EmitComment($"Initialize String from literal \"{escapedValue}\"");

            // Store ptr at offset 0 (4 bytes)
            Emit($"\tlea\t{stringLiteral.Label},a0");
            Emit($"\tmove.l\ta0,{stringBaseOffset}(a6)\t\t; Store string ptr");

            // Store len at offset 4 (4 bytes)
            Emit($"\tmove.l\t#{stringLiteral.Length},{stringBaseOffset + 4}(a6)\t\t; Store string len");
        }
        // Special handling for composite types (8-byte enums) from temporaries
        else if (localDecl.Type is IrEnumType enumType && enumType.SizeInBytes > 4 &&
                 localDecl.InitialValue is IrVariable tempVar && tempVar.Name.StartsWith("%t"))
        {
            EmitComment($"Initialize composite type from temp {tempVar.Name}");
            var destOffset = _localVariableOffsets[localDecl.Name];

            // Load address of temp on stack
            LoadOperand(localDecl.InitialValue, "a0");

            // Copy enum tag (4 bytes)
            Emit($"\tmove.l\t(a0),d0");
            Emit($"\tmove.l\td0,{destOffset}(a6)\t\t; Store enum tag");

            // Copy enum data (4 bytes)
            Emit($"\tmove.l\t4(a0),d0");
            Emit($"\tmove.l\td0,{destOffset + 4}(a6)\t\t; Store enum data");
        }
        else
        {
            // Regular scalar initialization
            // Load initial value into d0
            LoadOperand(localDecl.InitialValue, "d0");

            // Store to local variable's stack location
            var offset = _localVariableOffsets[localDecl.Name];
            var size = GetSizeSuffix(localDecl.Type);
            Emit($"\tmove{size}\td0,{offset}(a6)");
        }
    }

    private void GenerateStore(IrStore store)
    {
        EmitComment($"Store to {store.VariableName}");

        // Load value into d0
        LoadOperand(store.Value, "d0");

        // Store to local variable's stack location
        var offset = _localVariableOffsets[store.VariableName];
        var size = GetSizeSuffix(store.Value.Type);
        Emit($"\tmove{size}\td0,{offset}(a6)");
    }

    private void GenerateDereferenceStore(IrDereferenceStore derefStore)
    {
        EmitComment("Store to dereferenced pointer/reference");

        // Load the value to store into d1 (save it before loading the pointer)
        LoadOperand(derefStore.Value, "d1");

        // Load the pointer/reference into a0
        LoadOperand(derefStore.Pointer, "a0");

        // Store the value through the pointer
        var size = GetSizeSuffix(derefStore.Value.Type);
        Emit($"\tmove{size}\td1,(a0)");
    }

    private void GenerateIndexAccess(IrIndexAccess indexAccess)
    {
        EmitComment($"{indexAccess.ResultName} = array[index]");

        // Get array variable
        if (indexAccess.Array is not IrVariable arrayVar)
        {
            throw new Exception("Array must be a variable");
        }

        // Get base address of array (its stack offset)
        if (!_localVariableOffsets.ContainsKey(arrayVar.Name))
        {
            throw new Exception($"Array variable {arrayVar.Name} not found");
        }

        var arrayBaseOffset = _localVariableOffsets[arrayVar.Name];
        var elementSize = indexAccess.ElementType.SizeInBytes;
        var elementSizeSuffix = GetSizeSuffix(indexAccess.ElementType);

        // Load index into d1
        LoadOperand(indexAccess.Index, "d1");

        // Calculate byte offset: index * element_size
        // Use CPU-specific optimization for multiplication
        if (elementSize == 1)
        {
            // No multiplication needed for byte arrays
        }
        else if (elementSize == 2)
        {
            // index * 2 = index << 1
            if (_cpuFeatures.HasBarrelShifter)
            {
                Emit("\tlsl.l\t#1,d1");  // 68020+: Use barrel shifter
            }
            else
            {
                Emit("\tadd.l\td1,d1");  // 68000: add is faster for single shift
            }
        }
        else if (elementSize == 4)
        {
            // index * 4 = index << 2
            if (_cpuFeatures.HasBarrelShifter)
            {
                Emit("\tlsl.l\t#2,d1");  // 68020+: Single shift instruction
            }
            else
            {
                Emit("\tadd.l\td1,d1");  // 68000: Two adds
                Emit("\tadd.l\td1,d1");
            }
        }
        else if (elementSize == 8)
        {
            // index * 8 = index << 3
            if (_cpuFeatures.HasBarrelShifter)
            {
                Emit("\tlsl.l\t#3,d1");  // 68020+: Single shift instruction
            }
            else
            {
                Emit("\tadd.l\td1,d1");  // 68000: Three adds
                Emit("\tadd.l\td1,d1");
                Emit("\tadd.l\td1,d1");
            }
        }
        else
        {
            // For other sizes, use multiplication
            Emit($"\tmulu.w\t#{elementSize},d1");
        }

        // Calculate address: a6 + arrayBaseOffset - index_offset
        // (arrays grow downward on stack)
        // Load base address into a0
        Emit($"\tlea\t{arrayBaseOffset}(a6),a0");

        // Subtract index offset to get final address
        Emit("\tsuba.l\td1,a0");

        // Load the element value into d0
        Emit($"\tmove{elementSizeSuffix}\t(a0),d0");

        // Result is in d0 - don't save to stack
    }

    private void GenerateIndexStore(IrIndexStore indexStore)
    {
        EmitComment($"array[index] = value");

        // Get array variable
        if (indexStore.Array is not IrVariable arrayVar)
        {
            throw new Exception("Array must be a variable");
        }

        // Get base address of array (its stack offset)
        if (!_localVariableOffsets.ContainsKey(arrayVar.Name))
        {
            throw new Exception($"Array variable {arrayVar.Name} not found");
        }

        var arrayBaseOffset = _localVariableOffsets[arrayVar.Name];
        var elementSize = indexStore.Value.Type.SizeInBytes;
        var elementSizeSuffix = GetSizeSuffix(indexStore.Value.Type);

        // Load value to store into d2 (save it before we calculate address)
        LoadOperand(indexStore.Value, "d2");

        // Load index into d1
        LoadOperand(indexStore.Index, "d1");

        // Calculate byte offset: index * element_size
        // Use CPU-specific optimization for multiplication
        if (elementSize == 1)
        {
            // No multiplication needed for byte arrays
        }
        else if (elementSize == 2)
        {
            // index * 2 = index << 1
            if (_cpuFeatures.HasBarrelShifter)
            {
                Emit("\tlsl.l\t#1,d1");  // 68020+: Use barrel shifter
            }
            else
            {
                Emit("\tadd.l\td1,d1");  // 68000: add is faster for single shift
            }
        }
        else if (elementSize == 4)
        {
            // index * 4 = index << 2
            if (_cpuFeatures.HasBarrelShifter)
            {
                Emit("\tlsl.l\t#2,d1");  // 68020+: Single shift instruction
            }
            else
            {
                Emit("\tadd.l\td1,d1");  // 68000: Two adds
                Emit("\tadd.l\td1,d1");
            }
        }
        else if (elementSize == 8)
        {
            // index * 8 = index << 3
            if (_cpuFeatures.HasBarrelShifter)
            {
                Emit("\tlsl.l\t#3,d1");  // 68020+: Single shift instruction
            }
            else
            {
                Emit("\tadd.l\td1,d1");  // 68000: Three adds
                Emit("\tadd.l\td1,d1");
                Emit("\tadd.l\td1,d1");
            }
        }
        else
        {
            // For other sizes, use multiplication
            Emit($"\tmulu.w\t#{elementSize},d1");
        }

        // Calculate address: a6 + arrayBaseOffset - index_offset
        // (arrays grow downward on stack)
        // Load base address into a0
        Emit($"\tlea\t{arrayBaseOffset}(a6),a0");

        // Subtract index offset to get final address
        Emit("\tsuba.l\td1,a0");

        // Store the value from d2 to the array element
        Emit($"\tmove{elementSizeSuffix}\td2,(a0)");
    }

    private void GenerateMemberAccess(IrMemberAccess memberAccess)
    {
        EmitComment($"{memberAccess.ResultName} = {memberAccess.Struct}.{memberAccess.FieldName}");

        // Get struct variable
        if (memberAccess.Struct is not IrVariable structVar)
        {
            throw new Exception("Struct member access base must be a variable");
        }

        string baseVarName;
        int cumulativeFieldOffset;

        // Check if this is a chained access (base is a temp from previous member access)
        if (_structMemberLocations.ContainsKey(structVar.Name))
        {
            // This is chained access like a.b.c
            // Get the actual base variable and cumulative offset
            var (prevBaseVar, prevOffset) = _structMemberLocations[structVar.Name];
            baseVarName = prevBaseVar;
            cumulativeFieldOffset = prevOffset + memberAccess.FieldOffset;
        }
        else
        {
            // This is direct access like a.b
            baseVarName = structVar.Name;
            cumulativeFieldOffset = memberAccess.FieldOffset;
        }

        // Get base address of the actual struct variable
        if (!_localVariableOffsets.ContainsKey(baseVarName))
        {
            throw new Exception($"Struct variable {baseVarName} not found");
        }

        var structBaseOffset = _localVariableOffsets[baseVarName];
        int fieldOffset;

        // Struct fields always grow upward (toward higher addresses) regardless of
        // whether the struct is a parameter or local variable. The struct is laid out
        // in memory with field 0 at the base address, field 1 at base+size1, etc.
        //
        // For a String at -12(a6):
        //   -12(a6): ptr (offset 0)
        //   -8(a6):  len (offset 4)  ← base + 4, not base - 4!
        fieldOffset = structBaseOffset + cumulativeFieldOffset;

        // Check if the result is a struct (intermediate access in chain)
        if (memberAccess.FieldType is IrStructType)
        {
            // Don't load the struct - just record its location for potential chained access
            _structMemberLocations[memberAccess.ResultName] = (baseVarName, cumulativeFieldOffset);
            EmitComment($"Struct member location tracked: {memberAccess.ResultName} at offset {cumulativeFieldOffset} from {baseVarName}");
        }
        else
        {
            // Scalar field - load it
            var fieldSizeSuffix = GetSizeSuffix(memberAccess.FieldType);

            // Load field value into d0
            Emit($"\tmove{fieldSizeSuffix}\t{fieldOffset}(a6),d0");

            // Check if this temp is used later (e.g., as function argument)
            // If so, save it to the stack so it's not lost when d0 is reused
            if (IsTempUsed(memberAccess.ResultName))
            {
                EmitComment($"Save {memberAccess.ResultName} to stack (used later)");
                Emit("\tmove.l\td0,-(sp)");
                _savedTemps.Add(memberAccess.ResultName);
                _tempStackOffset += 4;
            }
        }
    }

    private void GenerateMemberStore(IrMemberStore memberStore)
    {
        EmitComment($"{memberStore.Struct}.{memberStore.FieldName} = value");

        // Get struct variable
        if (memberStore.Struct is not IrVariable structVar)
        {
            throw new Exception("Struct member store base must be a variable");
        }

        // Get base address of struct (its stack offset)
        if (!_localVariableOffsets.ContainsKey(structVar.Name))
        {
            throw new Exception($"Struct variable {structVar.Name} not found");
        }

        var structBaseOffset = _localVariableOffsets[structVar.Name];
        var fieldOffset = structBaseOffset - memberStore.FieldOffset;
        var fieldSizeSuffix = GetSizeSuffix(memberStore.Value.Type);

        // Load value to store into d0
        LoadOperand(memberStore.Value, "d0");

        // Store to field location
        Emit($"\tmove{fieldSizeSuffix}\td0,{fieldOffset}(a6)");
    }

    /// <summary>
    /// Recursively initialize struct fields, handling nested structs
    /// </summary>
    private void InitializeStructFields(IrStructLiteral structLiteral, IrStructType structType, int structBaseOffset)
    {
        foreach (var field in structType.Fields)
        {
            if (!structLiteral.FieldValues.ContainsKey(field.Name))
            {
                throw new Exception($"Struct field '{field.Name}' not initialized");
            }

            var fieldValue = structLiteral.FieldValues[field.Name];
            var fieldOffset = structBaseOffset - field.Offset;

            // Check if this field is itself a struct literal
            if (fieldValue is IrStructLiteral nestedStructLiteral && field.Type is IrStructType nestedStructType)
            {
                // Recursively initialize nested struct from literal
                EmitComment($"Initialize nested struct field {field.Name}");
                InitializeStructFields(nestedStructLiteral, nestedStructType, fieldOffset);
            }
            // Check if this field is a struct variable (struct-to-struct copy)
            else if (field.Type is IrStructType sourceStructType && fieldValue is IrVariable structVar)
            {
                // Copy struct field-by-field
                EmitComment($"Copy struct to field {field.Name}");
                CopyStruct(structVar.Name, fieldOffset, sourceStructType);
            }
            else
            {
                // Scalar field - load and store normally
                LoadOperand(fieldValue, "d0");
                var fieldSizeSuffix = GetSizeSuffix(field.Type);
                Emit($"\tmove{fieldSizeSuffix}\td0,{fieldOffset}(a6)");
            }
        }
    }

    /// <summary>
    /// Copy a struct from one location to another, field by field
    /// </summary>
    private void CopyStruct(string sourceVarName, int destOffset, IrStructType structType)
    {
        if (!_localVariableOffsets.ContainsKey(sourceVarName))
        {
            throw new Exception($"Source struct variable '{sourceVarName}' not found");
        }

        var sourceBaseOffset = _localVariableOffsets[sourceVarName];

        foreach (var field in structType.Fields)
        {
            int sourceFieldOffset;
            int destFieldOffset;

            // Calculate source offset
            if (sourceBaseOffset >= 8)
            {
                // Parameter
                sourceFieldOffset = sourceBaseOffset + field.Offset;
            }
            else
            {
                // Local variable
                sourceFieldOffset = sourceBaseOffset - field.Offset;
            }

            // Destination is always a local (negative offset)
            destFieldOffset = destOffset - field.Offset;

            // Copy field
            var fieldSizeSuffix = GetSizeSuffix(field.Type);
            Emit($"\tmove{fieldSizeSuffix}\t{sourceFieldOffset}(a6),d0");
            Emit($"\tmove{fieldSizeSuffix}\td0,{destFieldOffset}(a6)");
        }
    }

    private void LoadOperand(IrValue value, string targetReg)
    {
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
                // Check if this is a parameter (use _localVariableOffsets for correct offset)
                if (_currentFunction != null)
                {
                    var paramIndex = _currentFunction.Parameters.FindIndex(p => p.Name == variable.Name);
                    if (paramIndex >= 0 && _localVariableOffsets.ContainsKey(variable.Name))
                    {
                        // Parameters are on the stack after link frame
                        // Offsets are calculated in EmitPrologue based on actual parameter sizes
                        var baseOffset = _localVariableOffsets[variable.Name];

                        // Adjust offset for big-endian when loading smaller than longword
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
                    var offset = _localVariableOffsets[variable.Name];

                    if (targetReg.StartsWith("fp"))
                    {
                        // Loading into FPU register - use fmove.l
                        Emit($"\tfmove.l\t{offset}(a6),{targetReg}");
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
                        // Check if this is a composite type (8-byte enum)
                        if (variable.Type is IrEnumType enumType && enumType.SizeInBytes > 4)
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
                                // Load address of enum on stack
                                Emit($"\tlea\t{stackOffset}(sp),{targetReg}");
                            }
                            else
                            {
                                throw new Exception("Composite types cannot be loaded into data registers - use address registers");
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
                            Emit($"\tfmove.l\t{baseOffset}(sp),{targetReg}");
                        }
                        else
                        {
                            var tempSize = GetSizeSuffix(variable.Type);
                            Emit($"\tmove{tempSize}\t{offset}(sp),{targetReg}");

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
                else
                {
                    // Unknown variable - shouldn't happen if semantic analysis passed
                    throw new Exception($"Unknown variable: {variable.Name}");
                }
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
                }

                // Store tag at offset 0 (sp)
                Emit($"\tmove.l\t#{enumValue.VariantTag},(sp)\t\t; Store variant tag");

                // Store associated values starting at offset 4
                int dataOffset = 4;
                for (int i = 0; i < enumValue.AssociatedValues.Count; i++)
                {
                    var assocValue = enumValue.AssociatedValues[i];
                    LoadOperand(assocValue, "d0");

                    var valueSize = GetSizeSuffix(assocValue.Type);
                    Emit($"\tmove{valueSize}\td0,{dataOffset}(sp)\t\t; Store associated value {i}");
                    dataOffset += assocValue.Type.SizeInBytes;
                }

                // If targetReg is an address register, load the address
                // If it's a data register, we can't load the whole struct - error
                if (targetReg.StartsWith('a'))
                {
                    Emit($"\tmove.l\tsp,{targetReg}\t\t; Load enum address");
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

    private void GenerateFpuDetection()
    {
        EmitComment("FPU Detection and Initialization");
        Emit("");
        Emit("\tsection\tdata,data");
        Emit("");

        // Global flag: 0 = soft-float, 1 = hardware FPU
        Emit("__has_fpu:");
        Emit("\tdc.l\t0");
        Emit("");

        // Initialization flag to ensure detection runs only once
        Emit("__fpu_initialized:");
        Emit("\tdc.l\t0");
        Emit("");

        Emit("\tsection\ttext,code");
        Emit("");

        EmitComment("FPU detection routine - uses Amiga ExecBase AttnFlags");
        Emit("__detect_fpu:");
        Emit("\t; Check if already initialized");
        Emit("\ttst.l\t__fpu_initialized");
        Emit("\tbne\t.done");
        Emit("");

        Emit("\t; Mark as initialized");
        Emit("\tmove.l\t#1,__fpu_initialized");
        Emit("");

        Emit("\t; Get ExecBase (at absolute address 4)");
        Emit("\tmovea.l\t4.w,a0");
        Emit("");

        Emit("\t; Read AttnFlags from ExecBase+296");
        Emit("\tmove.w\t296(a0),d0");
        Emit("");

        Emit("\t; Check for FPU flags:");
        Emit("\t; Bit 4 ($10) = AFB_68881 (68881/68882)");
        Emit("\t; Bit 6 ($40) = AFB_68882");
        Emit("\t; Bit 7 ($80) = AFB_FPU40 (68040/68060 FPU)");
        Emit("\tandi.w\t#$00D0,d0\t; Mask FPU bits");
        Emit("\tbeq\t.no_fpu");
        Emit("");

        Emit("\t; FPU detected!");
        Emit("\tmove.l\t#1,__has_fpu");
        Emit("");

        Emit(".no_fpu:");
        Emit(".done:");
        Emit("\trts");
        Emit("");
    }

    private void GenerateCpuDetection()
    {
        EmitComment("CPU Detection and Initialization");
        EmitComment("Exported for use by external assembly code");
        Emit("");
        Emit("\tsection\tdata,data");
        Emit("");

        // Global flag: 0 = 68000, 1 = 68020+, 2 = 68060
        // Export for external use
        Emit("\txdef\t__detected_cpu");
        Emit("__detected_cpu:");
        Emit("\tdc.l\t0\t; 0=68000, 1=68020+, 2=68060");
        Emit("");

        // System hardware detection variables for system.novus
        // These are the public-facing variables that user code accesses
        EmitComment("system.novus global variables (CPU, FPU, Chipset)");
        Emit("\txdef\t_system_CPU");
        Emit("_system_CPU:");
        Emit("\tdc.l\t0\t; SystemCPU enum value");
        Emit("");

        Emit("\txdef\t_system_FPU");
        Emit("_system_FPU:");
        Emit("\tdc.l\t0\t; SystemFPU enum value");
        Emit("");

        Emit("\txdef\t_system_Chipset");
        Emit("_system_Chipset:");
        Emit("\tdc.l\t0\t; SystemChipset enum value");
        Emit("");

        // Initialization flag to ensure detection runs only once
        Emit("__cpu_initialized:");
        Emit("\tdc.l\t0");
        Emit("");

        Emit("\tsection\ttext,code");
        Emit("");

        EmitComment("CPU detection routine - uses Amiga ExecBase AttnFlags");
        EmitComment("Callable from external assembly: bsr __detect_cpu");
        EmitComment("Result available in __detected_cpu (0=68000, 1=68020+, 2=68060)");
        Emit("\txdef\t__detect_cpu");
        Emit("__detect_cpu:");
        Emit("\t; Check if already initialized");
        Emit("\ttst.l\t__cpu_initialized");
        Emit("\tbne\t.done");
        Emit("");

        Emit("\t; Mark as initialized");
        Emit("\tmove.l\t#1,__cpu_initialized");
        Emit("");

        Emit("\t; Get ExecBase (at absolute address 4)");
        Emit("\tmovea.l\t4.w,a0");
        Emit("");

        Emit("\t; Read AttnFlags from ExecBase+296");
        Emit("\tmove.w\t296(a0),d0");
        Emit("\tmove.w\td0,d1\t; Save for FPU detection");
        Emit("");

        Emit("\t; Check for CPU flags:");
        Emit("\t; Bit 0 ($01) = AFB_68010");
        Emit("\t; Bit 1 ($02) = AFB_68020");
        Emit("\t; Bit 2 ($04) = AFB_68030");
        Emit("\t; Bit 3 ($08) = AFB_68040");
        Emit("\t; Bit 7 ($80) = AFB_68060");
        Emit("");

        // Check for 68060 first (most specific)
        Emit("\t; Check for 68060");
        Emit("\tbtst\t#7,d0");
        Emit("\tbeq.s\t.not_68060");
        Emit("\tmove.l\t#2,__detected_cpu\t; 68060 detected");
        Emit("\tmove.l\t#5,_system_CPU\t; SystemCPU::M68060");
        Emit("\tbra\t.check_fpu");
        Emit("");

        Emit(".not_68060:");
        // Check for 68040
        Emit("\t; Check for 68040");
        Emit("\tbtst\t#3,d0");
        Emit("\tbeq.s\t.not_68040");
        Emit("\tmove.l\t#1,__detected_cpu\t; 68020+ detected");
        Emit("\tmove.l\t#4,_system_CPU\t; SystemCPU::M68040");
        Emit("\tbra\t.check_fpu");
        Emit("");

        Emit(".not_68040:");
        // Check for 68030
        Emit("\t; Check for 68030");
        Emit("\tbtst\t#2,d0");
        Emit("\tbeq.s\t.not_68030");
        Emit("\tmove.l\t#1,__detected_cpu\t; 68020+ detected");
        Emit("\tmove.l\t#3,_system_CPU\t; SystemCPU::M68030");
        Emit("\tbra\t.check_fpu");
        Emit("");

        Emit(".not_68030:");
        // Check for 68020
        Emit("\t; Check for 68020");
        Emit("\tbtst\t#1,d0");
        Emit("\tbeq.s\t.not_68020");
        Emit("\tmove.l\t#1,__detected_cpu\t; 68020+ detected");
        Emit("\tmove.l\t#2,_system_CPU\t; SystemCPU::M68020");
        Emit("\tbra\t.check_fpu");
        Emit("");

        Emit(".not_68020:");
        // Check for 68010
        Emit("\t; Check for 68010");
        Emit("\tbtst\t#0,d0");
        Emit("\tbeq.s\t.is_68000");
        Emit("\tmove.l\t#0,__detected_cpu\t; 68000 category");
        Emit("\tmove.l\t#1,_system_CPU\t; SystemCPU::M68010");
        Emit("\tbra\t.check_fpu");
        Emit("");

        Emit(".is_68000:");
        Emit("\t; Default to 68000");
        Emit("\tmove.l\t#0,__detected_cpu");
        Emit("\tmove.l\t#0,_system_CPU\t; SystemCPU::M68000");
        Emit("");

        // Now detect FPU
        Emit(".check_fpu:");
        Emit("\t; Check for FPU flags (d1 has AttnFlags):");
        Emit("\t; Bit 4 ($10) = AFB_68881");
        Emit("\t; Bit 5 ($20) = AFB_68882");
        Emit("\t; Bit 6 ($40) = AFB_FPU40 (68040/68060 FPU)");
        Emit("");

        // Check for 68060 FPU (based on CPU)
        Emit("\t; Check for 68060 FPU");
        Emit("\tmove.l\t_system_CPU,d2");
        Emit("\tcmpi.l\t#5,d2\t; M68060?");
        Emit("\tbne.s\t.not_68060_fpu");
        Emit("\tbtst\t#6,d1\t; AFB_FPU40?");
        Emit("\tbeq\t.no_fpu");
        Emit("\tmove.l\t#4,_system_FPU\t; SystemFPU::M68060");
        Emit("\tbra\t.done");
        Emit("");

        Emit(".not_68060_fpu:");
        // Check for 68040 FPU
        Emit("\t; Check for 68040 FPU");
        Emit("\tcmpi.l\t#4,d2\t; M68040?");
        Emit("\tbne.s\t.not_68040_fpu");
        Emit("\tbtst\t#6,d1\t; AFB_FPU40?");
        Emit("\tbeq\t.no_fpu");
        Emit("\tmove.l\t#3,_system_FPU\t; SystemFPU::M68040");
        Emit("\tbra\t.done");
        Emit("");

        Emit(".not_68040_fpu:");
        // Check for 68882
        Emit("\t; Check for 68882");
        Emit("\tbtst\t#5,d1");
        Emit("\tbeq.s\t.not_68882");
        Emit("\tmove.l\t#2,_system_FPU\t; SystemFPU::M68882");
        Emit("\tbra\t.done");
        Emit("");

        Emit(".not_68882:");
        // Check for 68881
        Emit("\t; Check for 68881");
        Emit("\tbtst\t#4,d1");
        Emit("\tbeq\t.no_fpu");
        Emit("\tmove.l\t#1,_system_FPU\t; SystemFPU::M68881");
        Emit("\tbra\t.done");
        Emit("");

        Emit(".no_fpu:");
        Emit("\t; No FPU (already 0 = SystemFPU::None)");
        Emit("");

        Emit(".done:");
        Emit("\t; Chipset detection would go here (requires GfxBase)");
        Emit("\t; For now, default to OCS (0)");
        Emit("\trts");
        Emit("");
    }

    private void GenerateOptimizedRuntimePrimitives()
    {
        EmitComment("========================================");
        EmitComment("Optimized Runtime Library for Assembly");
        EmitComment("========================================");
        EmitComment("These functions are exported for use by assembly code");
        EmitComment("They automatically dispatch to the optimal CPU version");
        EmitComment("");
        EmitComment("Available functions:");
        EmitComment("  __mul_i32(d0, d1) -> d0    : Signed 32-bit multiply");
        EmitComment("  __mul_u32(d0, d1) -> d0    : Unsigned 32-bit multiply");
        EmitComment("  __div_u32(d0, d1) -> d0    : Unsigned 32-bit divide");
        EmitComment("  __shl_i32(d0, d1) -> d0    : Shift left");
        EmitComment("  __shr_i32(d0, d1) -> d0    : Signed shift right");
        EmitComment("  __shr_u32(d0, d1) -> d0    : Unsigned shift right");
        EmitComment("");
        EmitComment("Convention: d0=operand1, d1=operand2/count, result=d0");
        EmitComment("All registers except d0 are preserved");
        Emit("");

        // Generate multiply (signed)
        GenerateRuntimePrimitive("__mul_i32", "Signed 32-bit multiply",
            GenerateMulI32_68000, GenerateMulI32_68020, GenerateMulI32_68060);

        // Generate multiply (unsigned)
        GenerateRuntimePrimitive("__mul_u32", "Unsigned 32-bit multiply",
            GenerateMulU32_68000, GenerateMulU32_68020, GenerateMulU32_68060);

        // Generate divide (unsigned) - signed division is complex, skip for now
        GenerateRuntimePrimitive("__div_u32", "Unsigned 32-bit divide",
            GenerateDivU32_68000, GenerateDivU32_68020, GenerateDivU32_68060);

        // Generate shift left
        GenerateRuntimePrimitive("__shl_i32", "Shift left",
            GenerateShlI32_68000, GenerateShlI32_68020, GenerateShlI32_68060);

        // Generate shift right (signed)
        GenerateRuntimePrimitive("__shr_i32", "Signed shift right",
            GenerateShrI32_68000, GenerateShrI32_68020, GenerateShrI32_68060);

        // Generate shift right (unsigned)
        GenerateRuntimePrimitive("__shr_u32", "Unsigned shift right",
            GenerateShrU32_68000, GenerateShrU32_68020, GenerateShrU32_68060);
    }

    private void GenerateRuntimePrimitive(string name, string description,
        Action gen68000, Action gen68020, Action gen68060)
    {
        EmitComment($"{description}");
        Emit($"\txdef\t{name}");
        Emit($"{name}:");
        Emit("\tbsr\t__detect_cpu");
        Emit("\tmove.l\t__detected_cpu,d2");  // Save in d2 to preserve d0/d1
        Emit("\tcmpi.l\t#2,d2");
        Emit($"\tbeq.s\t{name}_68060");
        Emit("\tcmpi.l\t#1,d2");
        Emit($"\tbeq.s\t{name}_68020");
        Emit($"\tbra.s\t{name}_68000");
        Emit("");

        // 68000 version
        Emit($"{name}_68000:");
        gen68000();
        Emit("");

        // 68020 version
        Emit($"{name}_68020:");
        gen68020();
        Emit("");

        // 68060 version
        Emit($"{name}_68060:");
        gen68060();
        Emit("");
    }

    // Multiply implementations
    private void GenerateMulI32_68000()
    {
        EmitComment("68000: 32-bit signed multiply using 16x16");
        Emit("\tmovem.l\td2-d4,-(sp)");
        Emit("\tmoveq\t#0,d4");  // Sign tracker
        Emit("\ttst.l\td0");
        Emit("\tbpl.s\t.pos_d0");
        Emit("\tneg.l\td0");
        Emit("\tnot.l\td4");
        Emit(".pos_d0:");
        Emit("\ttst.l\td1");
        Emit("\tbpl.s\t.pos_d1");
        Emit("\tneg.l\td1");
        Emit("\tnot.l\td4");
        Emit(".pos_d1:");
        Emit("\tmove.l\td0,d2");
        Emit("\tmove.l\td1,d3");
        Emit("\tmulu.w\td1,d0");
        Emit("\tmove.l\td2,d1");
        Emit("\tswap\td1");
        Emit("\tmulu.w\td3,d1");
        Emit("\tswap\td3");
        Emit("\tmulu.w\td3,d2");
        Emit("\tadd.l\td2,d1");
        Emit("\tswap\td1");
        Emit("\tclr.w\td1");
        Emit("\tadd.l\td1,d0");
        Emit("\ttst.l\td4");
        Emit("\tbeq.s\t.done");
        Emit("\tneg.l\td0");
        Emit(".done:");
        Emit("\tmovem.l\t(sp)+,d2-d4");
        Emit("\trts");
    }

    private void GenerateMulI32_68020()
    {
        EmitComment("68020: Native 32-bit multiply");
        Emit("\tmuls.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateMulI32_68060()
    {
        EmitComment("68060: Check if constant, optimize if possible");
        EmitComment("For now, fall back to 68020 version");
        EmitComment("TODO: Optimize for known small constants");
        Emit("\tmuls.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateMulU32_68000()
    {
        EmitComment("68000: 32-bit unsigned multiply using 16x16");
        Emit("\tmovem.l\td2-d3,-(sp)");
        Emit("\tmove.l\td0,d2");
        Emit("\tmove.l\td1,d3");
        Emit("\tmulu.w\td1,d0");
        Emit("\tmove.l\td2,d1");
        Emit("\tswap\td1");
        Emit("\tmulu.w\td3,d1");
        Emit("\tswap\td3");
        Emit("\tmulu.w\td3,d2");
        Emit("\tadd.l\td2,d1");
        Emit("\tswap\td1");
        Emit("\tclr.w\td1");
        Emit("\tadd.l\td1,d0");
        Emit("\tmovem.l\t(sp)+,d2-d3");
        Emit("\trts");
    }

    private void GenerateMulU32_68020()
    {
        EmitComment("68020: Native 32-bit multiply");
        Emit("\tmulu.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateMulU32_68060()
    {
        EmitComment("68060: Use native multiply (runtime constants can't be optimized)");
        Emit("\tmulu.l\td1,d0");
        Emit("\trts");
    }

    // Divide implementations
    private void GenerateDivU32_68000()
    {
        EmitComment("68000: No 32-bit divide, use 16-bit (lossy!)");
        EmitComment("TODO: Call proper 32-bit divide routine");
        Emit("\tdivu.w\td1,d0");
        Emit("\trts");
    }

    private void GenerateDivU32_68020()
    {
        EmitComment("68020: Native 32-bit divide");
        Emit("\tdivu.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateDivU32_68060()
    {
        EmitComment("68060: Very slow divide (>70 cycles)");
        EmitComment("Consider alternatives if possible");
        Emit("\tdivu.l\td1,d0");
        Emit("\trts");
    }

    // Shift implementations
    private void GenerateShlI32_68000()
    {
        EmitComment("68000: Shift left (max 8 bits immediate)");
        Emit("\tcmpi.l\t#8,d1");
        Emit("\tble.s\t.small");
        Emit("\tlsl.l\td1,d0\t; Use register shift for >8");
        Emit("\trts");
        Emit(".small:");
        Emit("\tlsl.l\td1,d0\t; Use immediate shift");
        Emit("\trts");
    }

    private void GenerateShlI32_68020()
    {
        EmitComment("68020: Barrel shifter handles any count");
        Emit("\tlsl.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateShlI32_68060()
    {
        EmitComment("68060: Barrel shifter (dual-issue friendly)");
        Emit("\tlsl.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateShrI32_68000()
    {
        EmitComment("68000: Signed shift right");
        Emit("\tasr.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateShrI32_68020()
    {
        EmitComment("68020: Barrel shifter");
        Emit("\tasr.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateShrI32_68060()
    {
        EmitComment("68060: Barrel shifter");
        Emit("\tasr.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateShrU32_68000()
    {
        EmitComment("68000: Unsigned shift right");
        Emit("\tlsr.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateShrU32_68020()
    {
        EmitComment("68020: Barrel shifter");
        Emit("\tlsr.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateShrU32_68060()
    {
        EmitComment("68060: Barrel shifter");
        Emit("\tlsr.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateCpuDispatchStubs()
    {
        EmitComment("CPU dispatch stubs for optimized functions");
        Emit("");

        foreach (var funcName in _cpuOptimizableFunctions)
        {
            var function = _module.Functions.First(f => f.Name == funcName);

            EmitComment($"Dispatch stub for {funcName}");
            if (function.IsPublic)
            {
                Emit($"\txdef\t_{funcName}");
            }
            Emit($"_{funcName}:");

            // Call CPU detection on first invocation
            Emit("\tbsr\t__detect_cpu");
            Emit("");

            // Check CPU flag and jump to appropriate version
            Emit("\tmove.l\t__detected_cpu,d0");
            Emit("\tcmpi.l\t#2,d0");
            Emit($"\tbeq.s\t.use_68060_{funcName}");
            Emit("\tcmpi.l\t#1,d0");
            Emit($"\tbeq.s\t.use_68020_{funcName}");
            Emit("");

            // 68000 version (baseline)
            Emit($"\tjmp\t_{funcName}_68000");
            Emit("");

            Emit($".use_68020_{funcName}:");
            Emit($"\tjmp\t_{funcName}_68020");
            Emit("");

            Emit($".use_68060_{funcName}:");
            Emit($"\tjmp\t_{funcName}_68060");
            Emit("");
        }
    }

    private void GenerateFunctionDispatchTable()
    {
        EmitComment("Function dispatch table for fat binary");
        Emit("");

        foreach (var funcName in _floatFunctions)
        {
            var function = _module.Functions.First(f => f.Name == funcName);

            EmitComment($"Dispatch stub for {funcName}");
            if (function.IsPublic)
            {
                Emit($"\txdef\t_{funcName}");
            }
            Emit($"_{funcName}:");

            // Call FPU detection on first invocation
            Emit("\tbsr\t__detect_fpu");
            Emit("");

            // Check FPU flag and jump to appropriate version
            Emit("\ttst.l\t__has_fpu");
            Emit($"\tbne.s\t.use_fpu_{funcName}");
            Emit("");

            // No FPU - use soft-float version
            Emit($"\tjmp\t_{funcName}_soft");
            Emit("");

            Emit($".use_fpu_{funcName}:");
            // Has FPU - use hardware version
            Emit($"\tjmp\t_{funcName}_fpu");
            Emit("");
        }
    }

    private void GenerateExtractTag(IrExtractTag extractTag)
    {
        // Extract the discriminant tag from an enum value
        // Enum memory layout: [tag (4 bytes)][data (variable)]
        // Tag is always at offset 0

        EmitComment($"Extract tag from enum value");

        // Get the address of the enum value
        var enumValue = extractTag.EnumValue;

        if (enumValue is IrVariable enumVar)
        {
            // Check if it's a global system variable (CPU, FPU, Chipset)
            if (enumVar.Name == "CPU" || enumVar.Name == "FPU" || enumVar.Name == "Chipset")
            {
                // Load tag directly from global label
                var globalLabel = $"_system_{enumVar.Name}";
                Emit($"\tmove.l\t{globalLabel},d0\t\t; Load enum tag from global");
            }
            else if (_localVariableOffsets.ContainsKey(enumVar.Name))
            {
                // Load tag from local variable location (offset 0)
                var offset = _localVariableOffsets[enumVar.Name];
                Emit($"\tmove.l\t{offset}(a6),d0\t\t; Load enum tag");
            }
            else
            {
                throw new Exception($"Variable '{enumVar.Name}' not found in local variables or globals");
            }
        }
        else if (enumValue is IrEnumValue enumVal)
        {
            // Direct enum value - just load the tag
            Emit($"\tmove.l\t#{enumVal.VariantTag},d0\t\t; Load enum tag");
        }
        else
        {
            throw new Exception($"Unsupported enum value type for tag extraction: {enumValue.GetType().Name}");
        }

        // Result is in d0
    }

    private void GenerateExtractVariantData(IrExtractVariantData extractData)
    {
        // Extract associated data from an enum variant
        // Enum memory layout: [tag (4 bytes)][data (variable)]
        // Data starts at offset 4

        EmitComment($"Extract variant data[{extractData.DataIndex}]");

        var enumValue = extractData.EnumValue;
        int dataOffset = 4; // Tag is 4 bytes

        // Calculate offset for the specific data index
        if (enumValue.Type is IrEnumType enumType)
        {
            // Find the variant to get data types
            // We need to calculate the offset based on previous data items
            // For now, assume each data item is 4 bytes (simplification)
            dataOffset += extractData.DataIndex * 4;
        }

        if (enumValue is IrVariable enumVar)
        {
            // Load data from variable location
            var varOffset = _localVariableOffsets[enumVar.Name];
            var dataSize = GetSizeSuffix(extractData.DataType);
            Emit($"\tmove{dataSize}\t{varOffset + dataOffset}(a6),d0\t\t; Load variant data");
        }
        else
        {
            throw new Exception($"Unsupported enum value type for data extraction: {enumValue.GetType().Name}");
        }

        // Result is in d0
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
