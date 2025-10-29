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
    private Optimizer.RegisterAllocation? _currentFunctionRegAlloc = null;
    private bool _currentFunctionHasPrologue = false;
    private bool _generatingFpuVersion = false; // true when generating _fpu version of function
    private string _currentFunctionSuffix = ""; // suffix for current function (e.g., "_68000", "_68020", "_68060")
    private readonly bool _isOriginallyFatBinary; // true if original cpuTarget was "auto", persists across function generation

    // Track last comparison for optimization
    private string? _lastComparisonResult;
    private string? _lastComparisonCondition;
    private string? _lastResultInD0;  // Track which temporary is currently in d0

    // Track local variable stack offsets
    private readonly Dictionary<string, int> _localVariableOffsets = new();

    // Track temp variables saved on stack (in order of saving)
    private readonly List<string> _savedTemps = new();
    private readonly Dictionary<string, int> _savedTempSizes = new(); // Track size of each saved temp
    private int _tempStackOffset = 0; // Total bytes used for temps
    // Map temp names to global variables they should reload from (for match on globals)
    private readonly Dictionary<string, string> _globalTagTemps = new();
    // Track stack depth at each label (for correct cleanup in branches)
    private readonly Dictionary<string, int> _labelStackDepths = new();
    private string? _currentLabel = null;  // Track the most recent label for stack depth lookup

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

    // Store register allocations per function
    private readonly Dictionary<string, Optimizer.RegisterAllocation> _registerAllocations = new();

    public M68kCodeGenerator(IrModule module, List<IrStringLiteral> stringLiterals, string cpuTarget = "68000", string fpuMode = "auto")
    {
        _module = module;
        _stringLiterals = stringLiterals;
        _cpuTarget = cpuTarget.ToLower();
        _cpuFeatures = new M68kCpuFeatures(_cpuTarget);
        _fpuMode = fpuMode.ToLower();
        _isOriginallyFatBinary = _cpuTarget == "auto"; // Remember if we started as fat binary
    }

    /// <summary>
    /// Set register allocations for all functions (called by optimizer)
    /// </summary>
    public void SetRegisterAllocations(Dictionary<string, Optimizer.RegisterAllocation> allocations)
    {
        foreach (var kvp in allocations)
        {
            _registerAllocations[kvp.Key] = kvp.Value;
        }
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

        // Only generate CPU detection and runtime primitives in the main module (with main function)
        // Other modules will reference these symbols via xref
        var hasMainFunction = _module.Functions.Any(f => f.Name == "main" && !f.IsExtern);

        // Generate CPU detection for fat binaries (needed for system.novus variables)
        if (IsCpuFatBinary && hasMainFunction)
        {
            GenerateCpuDetection();

            // Only generate dispatch stubs if we have CPU-optimizable functions
            if (_cpuOptimizableFunctions.Any())
            {
                GenerateCpuDispatchStubs();
            }
        }

        // Generate optimized runtime library primitives for assembly programmers
        // These are exported and usable by any assembly code
        if (IsCpuFatBinary && hasMainFunction)
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

        // Declare external integer math library functions for fat binaries
        if (IsCpuFatBinary)
        {
            Emit("\t; External VBCC integer math library functions");
            Emit("\txref\t__divu");    // Unsigned 32-bit divide (for 68000)
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

            // VBCC's _main.c always requires these symbols when linking with -lvc
            // We must provide them, but keep them empty when not needed
            Emit("\t; C++ constructor/destructor lists (required by VBCC startup)");
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
    private void GenerateBasicBlock(IrBasicBlock block)
    {
        // Emit block label if it's not the entry block
        if (block.Label != "entry")
        {
            // Include suffix for CPU-specific versions to avoid label conflicts
            Emit($"{block.Label}{_currentFunctionSuffix}:");
        }

        for (int i = 0; i < block.Instructions.Count; i++)
        {
            GenerateInstruction(block.Instructions[i], block.Instructions, i);
        }
    }

    private void GenerateInstruction(IrInstruction instruction, IList<IrInstruction> instructions, int index)
    {
        var instrName = instruction.GetType().Name;
        Console.WriteLine($"[DEBUG Instruction] {instrName}, _lastResultInD0='{_lastResultInD0 ?? "NULL"}'");

        switch (instruction)
        {
            case IrReturn ret:
                GenerateReturn(ret);
                break;
            case IrBinaryOp binOp:
                GenerateBinaryOp(binOp, instructions, index);
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
        var fullLabelName = $"{label.Name}{_currentFunctionSuffix}";
        Emit($"{fullLabelName}:");

        // Record the stack depth at this label for correct cleanup on returns
        // Don't overwrite if it was already set by a branch instruction
        if (!_labelStackDepths.ContainsKey(label.Name))
        {
            _labelStackDepths[label.Name] = _tempStackOffset;
        }
        else
        {
            // This label was already recorded by a branch - restore _tempStackOffset to that depth
            // This handles match arms where different paths have different runtime stack depths
            _tempStackOffset = _labelStackDepths[label.Name];
        }
        _currentLabel = label.Name;
    }

    private void GenerateBranch(IrBranch branch)
    {
        // Record stack depth for branch target
        if (!_labelStackDepths.ContainsKey(branch.Target))
        {
            _labelStackDepths[branch.Target] = _tempStackOffset;
        }

        // Include suffix for CPU-specific versions to avoid label conflicts
        Emit($"\tbra\t{branch.Target}{_currentFunctionSuffix}");
    }

    private void GenerateConditionalBranch(IrConditionalBranch condBranch)
    {
        // Record stack depth for branch targets
        // This is the stack depth that will be active when the branch is taken
        if (!_labelStackDepths.ContainsKey(condBranch.TrueTarget))
        {
            _labelStackDepths[condBranch.TrueTarget] = _tempStackOffset;
        }
        if (!_labelStackDepths.ContainsKey(condBranch.FalseTarget))
        {
            _labelStackDepths[condBranch.FalseTarget] = _tempStackOffset;
        }

        // Optimization: If the condition is the result of the last comparison,
        // we can branch directly on condition codes instead of materializing to 0/1
        if (condBranch.Condition is IrVariable condVar &&
            condVar.Name == _lastComparisonResult &&
            _lastComparisonCondition != null)
        {
            // Branch directly using the condition codes from the last comparison
            EmitComment("Optimized: branch directly on comparison result");
            // Include suffix for CPU-specific versions to avoid label conflicts
            Emit($"\tb{_lastComparisonCondition}\t{condBranch.TrueTarget}{_currentFunctionSuffix}");
            Emit($"\tbra\t{condBranch.FalseTarget}{_currentFunctionSuffix}");
        }
        else
        {
            // General case: load and test the condition value
            LoadOperand(condBranch.Condition, "d0");
            Emit($"\ttst.l\td0");
            // Include suffix for CPU-specific versions to avoid label conflicts
            Emit($"\tbne\t{condBranch.TrueTarget}{_currentFunctionSuffix}");
            Emit($"\tbra\t{condBranch.FalseTarget}{_currentFunctionSuffix}");
        }

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
                if (enumValue.Type.SizeInBytes > 8)
                {
                    EmitComment($"Return large enum value {enumValue.Type.Name}::{enumValue.VariantName} ({enumValue.Type.SizeInBytes} bytes) via hidden pointer");

                    // Load enum into address register (this will construct it on stack)
                    LoadOperand(ret.Value, "a1");  // This will push enum to stack and load address into a1

                    // Copy from (a1) to (a0) - a0 is the hidden return pointer
                    for (int i = 0; i < enumValue.Type.SizeInBytes; i += 4)
                    {
                        Emit($"\tmove.l\t{i}(a1),d0");
                        Emit($"\tmove.l\td0,{i}(a0)");
                    }
                }
                else
                {
                    EmitComment($"Return enum value {enumValue.Type.Name}::{enumValue.VariantName}");

                    // For small enums (8 bytes or less), we can return in D0+D1
                    // Load enum into address register and then copy to D0+D1
                    LoadOperand(ret.Value, "a0");  // This will push enum to stack and load address
                    Emit("\tmove.l\t(a0),d0\t\t; Load enum tag");

                    if (enumValue.Type.SizeInBytes > 4)
                    {
                        Emit("\tmove.l\t4(a0),d1\t\t; Load enum data");
                    }
                }
            }
            // Check if we're returning an enum type (composite value)
            else if (ret.Value.Type is IrEnumType enumType && ret.Value is IrVariable enumVar)
            {
                // For enum types larger than 8 bytes, use hidden return pointer
                if (enumType.SizeInBytes > 8)
                {
                    EmitComment($"Return large enum {enumType.EnumName} ({enumType.SizeInBytes} bytes) via hidden pointer");

                    int offset;

                    // Find where the enum is stored
                    if (_localVariableOffsets.ContainsKey(enumVar.Name))
                    {
                        offset = _localVariableOffsets[enumVar.Name];
                    }
                    else if (enumVar.Name.StartsWith("%t"))
                    {
                        var tempIndex = _savedTemps.IndexOf(enumVar.Name);
                        if (tempIndex >= 0)
                        {
                            offset = (_savedTemps.Count - 1 - tempIndex) * 4;
                            offset = -(_tempStackOffset - offset);
                        }
                        else
                        {
                            throw new Exception($"Large enum temp {enumVar.Name} not found on stack");
                        }
                    }
                    else
                    {
                        throw new Exception($"Large enum variable {enumVar.Name} not found");
                    }

                    // Copy the enum to the location pointed to by A0 (hidden return pointer)
                    // A0 was passed by caller and points to where result should be stored
                    for (int i = 0; i < enumType.SizeInBytes; i += 4)
                    {
                        Emit($"\tmove.l\t{offset + i}(a6),d0");
                        Emit($"\tmove.l\td0,{i}(a0)");
                    }
                }
                else
                {
                    // For enum types 8 bytes or less, load from stack location into D0+D1
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
            }
            // Check if we're returning a struct literal
            else if (ret.Value is IrStructLiteral structLiteral && structLiteral.Type is IrStructType litStructType)
            {
                if (litStructType.SizeInBytes > 8)
                {
                    EmitComment($"Return large struct literal {litStructType.StructName} ({litStructType.SizeInBytes} bytes) via hidden pointer");

                    // Initialize the struct directly at the location pointed to by A0 (hidden return pointer)
                    // A0 was passed by caller and points to where result should be stored
                    InitializeStructFields(structLiteral, litStructType, 0, useAddressRegister: true);
                }
                else
                {
                    // For small structs (≤8 bytes), load fields directly into D0+D1
                    EmitComment($"Return small struct literal {litStructType.StructName} ({litStructType.SizeInBytes} bytes)");

                    // Load first 4 bytes (first field or part of it) into D0
                    if (litStructType.Fields.Count > 0)
                    {
                        LoadOperand(litStructType.Fields[0].Type is IrPointerType
                            ? structLiteral.FieldValues[litStructType.Fields[0].Name]
                            : structLiteral.FieldValues[litStructType.Fields[0].Name], "d0");
                    }

                    // Load second 4 bytes (second field or rest of first field) into D1 if needed
                    if (litStructType.SizeInBytes > 4 && litStructType.Fields.Count > 1)
                    {
                        LoadOperand(structLiteral.FieldValues[litStructType.Fields[1].Name], "d1");
                    }
                }
            }
            // Check if we're returning a struct type (composite value)
            else if (ret.Value.Type is IrStructType structType && ret.Value is IrVariable structVar)
            {
                // For struct types larger than 8 bytes, use hidden return pointer
                if (structType.SizeInBytes > 8)
                {
                    EmitComment($"Return large struct {structType.StructName} ({structType.SizeInBytes} bytes) via hidden pointer");

                    int offset;

                    // Find where the struct is stored
                    if (_localVariableOffsets.ContainsKey(structVar.Name))
                    {
                        offset = _localVariableOffsets[structVar.Name];
                    }
                    else if (structVar.Name.StartsWith("%t"))
                    {
                        var tempIndex = _savedTemps.IndexOf(structVar.Name);
                        if (tempIndex >= 0)
                        {
                            offset = (_savedTemps.Count - 1 - tempIndex) * 4;
                            offset = -(_tempStackOffset - offset);
                        }
                        else
                        {
                            throw new Exception($"Large struct temp {structVar.Name} not found on stack");
                        }
                    }
                    else
                    {
                        throw new Exception($"Large struct variable {structVar.Name} not found");
                    }

                    // Copy the struct to the location pointed to by A0 (hidden return pointer)
                    // A0 was passed by caller and points to where result should be stored
                    for (int i = 0; i < structType.SizeInBytes; i += 4)
                    {
                        Emit($"\tmove.l\t{offset + i}(a6),d0");
                        Emit($"\tmove.l\td0,{i}(a0)");
                    }
                }
                else
                {
                    // For struct types 8 bytes or less, load from stack location into D0+D1
                    EmitComment($"Return struct {structType.StructName} ({structType.SizeInBytes} bytes)");

                    int offset;

                    // Check if it's a local variable or a temporary
                    if (_localVariableOffsets.ContainsKey(structVar.Name))
                    {
                        offset = _localVariableOffsets[structVar.Name];
                    }
                    else if (structVar.Name.StartsWith("%t"))
                    {
                        // It's a temporary - check if it's saved on the stack
                        var tempIndex = _savedTemps.IndexOf(structVar.Name);
                        if (tempIndex >= 0)
                        {
                            // Calculate offset from top of stack
                            offset = (_savedTemps.Count - 1 - tempIndex) * 4;
                            // Convert to frame pointer offset
                            offset = -(_tempStackOffset - offset);

                            // Load first 4 bytes into D0
                            Emit($"\tmove.l\t{offset}(a6),d0\t\t; Load struct bytes 0-3");

                            // If struct has more data (size > 4), load next 4 bytes into D1
                            if (structType.SizeInBytes > 4)
                            {
                                Emit($"\tmove.l\t{offset + 4}(a6),d1\t\t; Load struct bytes 4-7");
                            }
                        }
                        else
                        {
                            // Temporary not saved - try to load it with LoadOperand
                            EmitComment($"Load struct temporary {structVar.Name}");
                            LoadOperand(ret.Value, "a0");  // Load into address register

                            // Load from address into D0+D1
                            Emit("\tmove.l\t(a0),d0\t\t; Load struct bytes 0-3");
                            if (structType.SizeInBytes > 4)
                            {
                                Emit("\tmove.l\t4(a0),d1\t\t; Load struct bytes 4-7");
                            }
                        }
                    }
                    else
                    {
                        offset = _localVariableOffsets[structVar.Name];

                        // Load first 4 bytes into D0
                        Emit($"\tmove.l\t{offset}(a6),d0\t\t; Load struct bytes 0-3");

                        // If struct has more data (size > 4), load next 4 bytes into D1
                        if (structType.SizeInBytes > 4)
                        {
                            Emit($"\tmove.l\t{offset + 4}(a6),d1\t\t; Load struct bytes 4-7");
                        }
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

        // Emit normal epilogue and return for all functions (including main)
        // The return value is already in D0, and AmigaOS will receive it when main returns
        // IMPORTANT: _tempStackOffset is restored to the label's entry depth when entering a label
        // So it always represents the correct runtime stack depth for cleanup
        EmitEpilogue(_tempStackOffset);
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
            // Handle composite types > 4 bytes (structs, large enums)
            else if (arg.Type.SizeInBytes > 4)
            {
                EmitComment($"Push composite argument (size: {arg.Type.SizeInBytes} bytes)");

                if (arg is IrVariable argVar)
                {
                    // Check if it's a temp on stack
                    var savedIndex = _savedTemps.IndexOf(argVar.Name);
                    if (savedIndex >= 0)
                    {
                        // Load address of temp on stack
                        LoadOperand(arg, "a0");

                        // Push composite type longword-by-longword (reverse order)
                        int numLongwords = (arg.Type.SizeInBytes + 3) / 4;
                        for (int j = numLongwords - 1; j >= 0; j--)
                        {
                            Emit($"\tmove.l\t{j * 4}(a0),d0");
                            Emit("\tmove.l\td0,-(sp)");
                        }
                        totalBytesPushed += numLongwords * 4;
                    }
                    else if (_localVariableOffsets.TryGetValue(argVar.Name, out int offset))
                    {
                        // Local variable - push longword-by-longword (reverse order for stack growth)
                        int numLongwords = (arg.Type.SizeInBytes + 3) / 4;
                        for (int j = numLongwords - 1; j >= 0; j--)
                        {
                            Emit($"\tmove.l\t{offset + (j * 4)}(a6),d0");
                            Emit("\tmove.l\td0,-(sp)");
                        }
                        totalBytesPushed += numLongwords * 4;
                    }
                    else
                    {
                        throw new Exception($"Unknown variable: {argVar.Name}");
                    }
                }
                else
                {
                    throw new Exception($"Unsupported composite argument type: {arg.GetType().Name}");
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
                        var offsetStr = adjustedOffset == 0 ? "(sp)" : $"{adjustedOffset}(sp)";
                        Emit($"\tmove{tempSize}\t{offsetStr},d0");
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

        // Handle return value based on type - must allocate space BEFORE pushing arguments for large returns
        int returnSpaceAllocated = 0;
        if (call.ResultName != null && call.ReturnType.SizeInBytes > 8)
        {
            // For composite types > 8 bytes (structs or enums), allocate return space AFTER arguments are pushed
            // This way the return value ends up at the correct location on stack after cleanup
            returnSpaceAllocated = call.ReturnType.SizeInBytes;
        }

        // Allocate return space if needed (AFTER arguments)
        if (returnSpaceAllocated > 0)
        {
            var typeName = call.ReturnType is IrEnumType ? "enum" :
                          call.ReturnType is IrStructType ? "struct" : "composite";
            EmitComment($"Allocate space for large {typeName} return value ({returnSpaceAllocated} bytes)");
            Emit($"\tsub.l\t#{returnSpaceAllocated},sp");
            Emit("\tmove.l\tsp,a0\t\t; Pass return pointer in A0");
        }

        // Call the function using JSR (Jump to Subroutine)
        Emit($"\tjsr\t_{MangleName(call.FunctionName)}");

        // Clean up stack (remove arguments)
        // For large returns, arguments are ABOVE return space, so we need to skip over return space
        if (totalBytesPushed > 0)
        {
            if (returnSpaceAllocated > 0)
            {
                // Arguments are at SP+returnSize, so we need to remove them differently
                // Move the return value down to cover the arguments, then adjust SP
                // IMPORTANT: Copy from high to low addresses to avoid overwriting
                EmitComment("Move return value down over arguments");
                for (int i = returnSpaceAllocated - 4; i >= 0; i -= 4)
                {
                    Emit($"\tmove.l\t{i}(sp),{i + totalBytesPushed}(sp)");
                }
                Emit($"\tlea\t{totalBytesPushed}(sp),sp");
            }
            else
            {
                // Normal cleanup for non-large returns
                Emit($"\tlea\t{totalBytesPushed}(sp),sp");
            }
        }

        // Handle return value based on type
        if (call.ResultName != null)
        {
            // Check if this is a composite type (struct or enum with size > 4 bytes)
            // Use call.ReturnType which was set during IR building
            if (call.ReturnType.SizeInBytes > 8)
            {
                // For composite types > 8 bytes, result was stored at (A0) via hidden pointer
                // The space is already on the stack, just track it
                var typeName = call.ReturnType is IrEnumType ? "enum" :
                              call.ReturnType is IrStructType ? "struct" : "composite";
                EmitComment($"Large {typeName} return value ({call.ReturnType.SizeInBytes} bytes) already on stack");
                _savedTemps.Add(call.ResultName);
                _savedTempSizes[call.ResultName] = call.ReturnType.SizeInBytes;
                _tempStackOffset += call.ReturnType.SizeInBytes;
            }
            else if (call.ReturnType.SizeInBytes > 4)
            {
                // For composite types 5-8 bytes, result is in D0+D1
                // Save both registers to stack
                var typeName = call.ReturnType is IrEnumType ? "enum" :
                              call.ReturnType is IrStructType ? "struct" : "composite";
                EmitComment($"Save {typeName} return value ({call.ReturnType.SizeInBytes} bytes)");
                Emit("\tmove.l\td1,-(sp)\t\t; Save data (D1)");
                Emit("\tmove.l\td0,-(sp)\t\t; Save data (D0)");
                _savedTemps.Add(call.ResultName);
                _savedTempSizes[call.ResultName] = 8;  // Track that this temp is 8 bytes
                _tempStackOffset += 8;
            }
            else
            {
                // Result is in d0 - track it so next instruction can use it directly
                _lastResultInD0 = call.ResultName;
                EmitComment($"Result {call.ResultName} in d0");
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
                    var offsetStr = adjustedOffset == 0 ? "(sp)" : $"{adjustedOffset}(sp)";
                    Emit($"\tmove{tempSize}\t{offsetStr},d0");
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

                // Check if associated value is a nested composite enum (any enum type > 4 bytes)
                bool isCompositeEnum = false;
                IrEnumType compositeEnumType = null;

                if (assocValue.Type is IrEnumType enumType2 && enumType2.SizeInBytes > 4)
                {
                    // Any enum > 4 bytes needs special handling (IrEnumValue or IrVariable)
                    isCompositeEnum = true;
                    compositeEnumType = enumType2;
                }

                if (isCompositeEnum)
                {
                    EmitComment($"Copy nested enum associated value (size: {compositeEnumType.SizeInBytes} bytes)");

                    // Build/load the nested enum using address register
                    LoadOperand(assocValue, "a1");

                    // Copy longword-by-longword from nested enum to local enum
                    int numLongwords = (compositeEnumType.SizeInBytes + 3) / 4;
                    for (int j = 0; j < numLongwords; j++)
                    {
                        Emit($"\tmove.l\t{j * 4}(a1),d0");
                        Emit($"\tmove.l\td0,{enumBaseOffset + dataOffset + (j * 4)}(a6)");
                    }

                    // Clean up temporary enum space allocated by LoadOperand
                    Emit($"\tlea\t{compositeEnumType.SizeInBytes}(sp),sp\t\t; Free temporary enum space");

                    dataOffset += compositeEnumType.SizeInBytes;
                }
                else
                {
                    // Simple value - check if it's a simple enum that needs special handling
                    if (assocValue is IrEnumValue simpleEnumValue && assocValue.Type is IrEnumType simpleEnumType && simpleEnumType.SizeInBytes <= 4)
                    {
                        // Simple enum value (tag only, fits in 4 bytes) - just store the tag
                        Emit($"\tmove.l\t#{simpleEnumValue.VariantTag},{enumBaseOffset + dataOffset}(a6)\t\t; Store simple enum tag");
                        dataOffset += 4;
                    }
                    else
                    {
                        // Regular scalar value - load into d0 and store
                        LoadOperand(assocValue, "d0");

                        var valueSize = GetSizeSuffix(assocValue.Type);
                        Emit($"\tmove{valueSize}\td0,{enumBaseOffset + dataOffset}(a6)\t\t; Store associated value {i}");
                        dataOffset += assocValue.Type.SizeInBytes;
                    }
                }
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
        // Special handling for composite types (> 4 bytes) from temporaries or other composite values
        else if (localDecl.Type.SizeInBytes > 4)
        {
            var typeName = localDecl.Type is IrEnumType ? "enum" :
                          localDecl.Type is IrStructType ? "struct" : "composite";
            EmitComment($"Initialize {typeName} {localDecl.Name} ({localDecl.Type.SizeInBytes} bytes)");
            var destOffset = _localVariableOffsets[localDecl.Name];

            // Load address of source value on stack
            LoadOperand(localDecl.InitialValue, "a0");

            // Copy all bytes of the composite type (in 4-byte chunks, word-aligned)
            int bytesToCopy = localDecl.Type.SizeInBytes;
            // Round up to nearest word (2 bytes) for proper alignment
            if (bytesToCopy % 2 != 0)
            {
                bytesToCopy++;
            }

            for (int i = 0; i < bytesToCopy; i += 4)
            {
                if (i + 4 <= bytesToCopy)
                {
                    // Full longword
                    Emit($"\tmove.l\t{i}(a0),d0");
                    Emit($"\tmove.l\td0,{destOffset + i}(a6)\t\t; Store {typeName} bytes {i}-{i+3}");
                }
                else if (i + 2 <= bytesToCopy)
                {
                    // Final word
                    Emit($"\tmove.w\t{i}(a0),d0");
                    Emit($"\tmove.w\td0,{destOffset + i}(a6)\t\t; Store {typeName} bytes {i}-{i+1}");
                }
            }

            // Clean up the stack space used by the return value
            // If the initializer was a function call that returned a large struct,
            // the struct is still on the stack and needs to be popped
            if (localDecl.InitialValue is IrVariable initVar && initVar.Name.StartsWith("%t"))
            {
                // This is a temporary from a function return - clean up stack
                Emit($"\tlea\t{localDecl.Type.SizeInBytes}(sp),sp\t\t; Pop {typeName} return value from stack");
                // Update _tempStackOffset to reflect that we've cleaned up this space
                _tempStackOffset -= localDecl.Type.SizeInBytes;
            }
        }
        else
        {
            // Regular scalar initialization
            // REGISTER ALLOCATION: Check if variable is allocated to a register
            if (_currentFunctionRegAlloc != null)
            {
                var allocatedReg = _currentFunctionRegAlloc.GetRegister(localDecl.Name);
                if (allocatedReg != null)
                {
                    // Variable is in a register - load value directly to that register
                    EmitComment($"Initialize {localDecl.Name} in allocated register {allocatedReg}");
                    LoadOperand(localDecl.InitialValue, allocatedReg);
                    return;
                }
                // If spilled or not allocated, fall through to stack storage
            }

            // Load initial value into d0
            LoadOperand(localDecl.InitialValue, "d0");

            // Store to local variable's stack location
            var baseOffset = _localVariableOffsets[localDecl.Name];

            // NOTE: Do NOT adjust for big-endian on stack variables!
            // The stack frame layout already accounts for proper alignment.
            // Big-endian adjustment is only needed for register operations,
            // not for stack-based (a6) relative addressing.
            var offset = baseOffset;

            var size = GetSizeSuffix(localDecl.Type);
            Emit($"\tmove{size}\td0,{offset}(a6)");
        }
    }

    private void GenerateStore(IrStore store)
    {
        EmitComment($"Store to {store.VariableName}");

        // REGISTER ALLOCATION: Check if variable is allocated to a register
        if (_currentFunctionRegAlloc != null)
        {
            var allocatedReg = _currentFunctionRegAlloc.GetRegister(store.VariableName);
            if (allocatedReg != null)
            {
                // Variable is in a register - load value directly to that register
                EmitComment($"Store to {store.VariableName} in allocated register {allocatedReg}");
                LoadOperand(store.Value, allocatedReg);
                return;
            }
            // If spilled or not allocated, fall through to stack storage
        }

        // Load value into d0
        LoadOperand(store.Value, "d0");

        // Store to local variable's stack location
        var baseOffset = _localVariableOffsets[store.VariableName];

        // NOTE: Do NOT adjust for big-endian on stack variables!
        // The stack frame layout already accounts for proper alignment.
        var offset = baseOffset;

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
        var elementSize = indexAccess.ElementType.SizeInBytes;
        var elementSizeSuffix = GetSizeSuffix(indexAccess.ElementType);

        // Check if this is pointer indexing or array indexing
        if (indexAccess.Array is not IrVariable arrayVar)
        {
            throw new Exception("Index base must be a variable");
        }

        // Get the type directly from the variable
        var baseType = arrayVar.Type;

        // Check if we can use scaled indexing (used by both pointer and array paths)
        bool canUseScaling = _cpuFeatures.HasBarrelShifter && (elementSize == 1 || elementSize == 2 || elementSize == 4 || elementSize == 8);

        // Handle pointer indexing: ptr[index] = *(ptr + index * sizeof(T))
        if (baseType is IrPointerType)
        {
            EmitComment($"{indexAccess.ResultName} = ptr[index]");

            // Load the pointer into a0 using LoadOperand (handles local vars, params, and saved temps)
            LoadOperand(indexAccess.Array, "a0");

            // Load index into d1
            LoadOperand(indexAccess.Index, "d1");

            if (canUseScaling && elementSize > 1)
            {
                // 68020+: Use scaled indexed addressing
                EmitComment($"Pointer indexing with scale *{elementSize}");
                Emit($"\tmove{elementSizeSuffix}\t(a0,d1.l*{elementSize}),d0");
            }
            else if (elementSize == 1)
            {
                // Byte: no scaling needed
                Emit($"\tmove{elementSizeSuffix}\t(a0,d1.l),d0");
            }
            else
            {
                // Manual scaling for other sizes or 68000
                if (elementSize == 2)
                {
                    Emit("\tadd.l\td1,d1\t; index * 2");
                }
                else if (elementSize == 4)
                {
                    Emit("\tadd.l\td1,d1\t; index * 2");
                    Emit("\tadd.l\td1,d1\t; index * 4");
                }
                else if (elementSize == 8)
                {
                    Emit("\tadd.l\td1,d1\t; index * 2");
                    Emit("\tadd.l\td1,d1\t; index * 4");
                    Emit("\tadd.l\td1,d1\t; index * 8");
                }
                else
                {
                    // Use multiplication for odd sizes
                    Emit($"\tmulu.w\t#{elementSize},d1");
                }
                Emit($"\tmove{elementSizeSuffix}\t(a0,d1.l),d0");
            }

            // Track that result is in d0
            _lastResultInD0 = indexAccess.ResultName;
            return;
        }

        // Handle array indexing (stack-based arrays)
        EmitComment($"{indexAccess.ResultName} = array[index]");

        if (!_localVariableOffsets.ContainsKey(arrayVar.Name))
        {
            throw new Exception($"Array variable {arrayVar.Name} not found");
        }

        var arrayBaseOffset = _localVariableOffsets[arrayVar.Name];

        // OPTIMIZATION: Use 68000 indexed addressing modes for array access
        // These modes allow base + index in a single instruction
        // Format: d(An,Di.size*scale) where:
        //   d = displacement (array base offset)
        //   An = base register (frame pointer a6)
        //   Di = index register
        //   size = .w or .l (we use .l for full 32-bit index)
        //   scale = 1,2,4,8 (68020+ only, 68000 needs pre-scaled index)

        // Load index into d1
        LoadOperand(indexAccess.Index, "d1");

        if (canUseScaling && elementSize > 1)
        {
            // 68020+ with scaling: move.x arrayOffset(a6,d1.l*scale),d0
            EmitComment($"Optimized: indexed addressing with scale *{elementSize}");
            Emit($"\tmove{elementSizeSuffix}\t{arrayBaseOffset}(a6,d1.l*{elementSize}),d0");
        }
        else if (elementSize == 1)
        {
            // Byte arrays: no scaling needed
            // Use indexed addressing: move.b arrayOffset(a6,d1.l),d0
            EmitComment("Optimized: indexed addressing for byte array");
            Emit($"\tmove{elementSizeSuffix}\t{arrayBaseOffset}(a6,d1.l),d0");
        }
        else if (elementSize == 2 && !_cpuFeatures.HasBarrelShifter)
        {
            // 68000: scale manually then use indexed addressing
            Emit("\tadd.l\td1,d1\t; index * 2");
            EmitComment("Optimized: indexed addressing with pre-scaled index");
            Emit($"\tmove{elementSizeSuffix}\t{arrayBaseOffset}(a6,d1.l),d0");
        }
        else if (elementSize == 4 && !_cpuFeatures.HasBarrelShifter)
        {
            // 68000: scale manually then use indexed addressing
            Emit("\tadd.l\td1,d1\t; index * 2");
            Emit("\tadd.l\td1,d1\t; index * 4");
            EmitComment("Optimized: indexed addressing with pre-scaled index");
            Emit($"\tmove{elementSizeSuffix}\t{arrayBaseOffset}(a6,d1.l),d0");
        }
        else if (elementSize == 8 && !_cpuFeatures.HasBarrelShifter)
        {
            // 68000: scale manually then use indexed addressing
            Emit("\tadd.l\td1,d1\t; index * 2");
            Emit("\tadd.l\td1,d1\t; index * 4");
            Emit("\tadd.l\td1,d1\t; index * 8");
            EmitComment("Optimized: indexed addressing with pre-scaled index");
            Emit($"\tmove{elementSizeSuffix}\t{arrayBaseOffset}(a6,d1.l),d0");
        }
        else
        {
            // For other sizes, use multiplication and traditional addressing
            Emit($"\tmulu.w\t#{elementSize},d1");
            Emit($"\tlea\t{arrayBaseOffset}(a6),a0");
            Emit("\tsuba.l\td1,a0");
            Emit($"\tmove{elementSizeSuffix}\t(a0),d0");
        }

        // Result is in d0 - track it
        _lastResultInD0 = indexAccess.ResultName;
    }

    private void GenerateIndexStore(IrIndexStore indexStore)
    {
        var elementSize = indexStore.Value.Type.SizeInBytes;
        var elementSizeSuffix = GetSizeSuffix(indexStore.Value.Type);

        // Check if this is pointer or array indexing
        if (indexStore.Array is not IrVariable arrayVar)
        {
            throw new Exception("Index base must be a variable");
        }

        // Get the type directly from the variable
        var baseType = arrayVar.Type;

        // Check if we can use scaled indexing (used by both pointer and array paths)
        bool canUseScaling = _cpuFeatures.HasBarrelShifter && (elementSize == 1 || elementSize == 2 || elementSize == 4 || elementSize == 8);

        // Handle pointer indexing: ptr[index] = value
        if (baseType is IrPointerType)
        {
            EmitComment($"ptr[index] = value");

            // Load the pointer into a0 using LoadOperand (handles local vars, params, and saved temps)
            LoadOperand(indexStore.Array, "a0");

            // Load value to store into d2
            LoadOperand(indexStore.Value, "d2");

            // Load index into d1
            LoadOperand(indexStore.Index, "d1");

            if (canUseScaling && elementSize > 1)
            {
                // 68020+: Use scaled indexed addressing
                EmitComment($"Pointer store with scale *{elementSize}");
                Emit($"\tmove{elementSizeSuffix}\td2,(a0,d1.l*{elementSize})");
            }
            else if (elementSize == 1)
            {
                // Byte: no scaling needed
                Emit($"\tmove{elementSizeSuffix}\td2,(a0,d1.l)");
            }
            else
            {
                // Manual scaling for other sizes or 68000
                if (elementSize == 2)
                {
                    Emit("\tadd.l\td1,d1\t; index * 2");
                }
                else if (elementSize == 4)
                {
                    Emit("\tadd.l\td1,d1\t; index * 2");
                    Emit("\tadd.l\td1,d1\t; index * 4");
                }
                else if (elementSize == 8)
                {
                    Emit("\tadd.l\td1,d1\t; index * 2");
                    Emit("\tadd.l\td1,d1\t; index * 4");
                    Emit("\tadd.l\td1,d1\t; index * 8");
                }
                else
                {
                    // Use multiplication for odd sizes
                    Emit($"\tmulu.w\t#{elementSize},d1");
                }
                Emit($"\tmove{elementSizeSuffix}\td2,(a0,d1.l)");
            }

            return;
        }

        // Handle array indexing (stack-based arrays)
        EmitComment($"array[index] = value");

        if (!_localVariableOffsets.ContainsKey(arrayVar.Name))
        {
            throw new Exception($"Array variable {arrayVar.Name} not found");
        }

        var arrayBaseOffset = _localVariableOffsets[arrayVar.Name];

        // Load value to store into d2 (save it before we calculate address)
        LoadOperand(indexStore.Value, "d2");

        // Load index into d1
        LoadOperand(indexStore.Index, "d1");

        // OPTIMIZATION: Use 68000 indexed addressing modes for array stores
        // Same optimization as GenerateIndexAccess but for stores

        if (canUseScaling && elementSize > 1)
        {
            // 68020+ with scaling: move.x d2,arrayOffset(a6,d1.l*scale)
            EmitComment($"Optimized: indexed addressing with scale *{elementSize}");
            Emit($"\tmove{elementSizeSuffix}\td2,{arrayBaseOffset}(a6,d1.l*{elementSize})");
        }
        else if (elementSize == 1)
        {
            // Byte arrays: no scaling needed
            // Use indexed addressing: move.b d2,arrayOffset(a6,d1.l)
            EmitComment("Optimized: indexed addressing for byte array");
            Emit($"\tmove{elementSizeSuffix}\td2,{arrayBaseOffset}(a6,d1.l)");
        }
        else if (elementSize == 2 && !_cpuFeatures.HasBarrelShifter)
        {
            // 68000: scale manually then use indexed addressing
            Emit("\tadd.l\td1,d1\t; index * 2");
            EmitComment("Optimized: indexed addressing with pre-scaled index");
            Emit($"\tmove{elementSizeSuffix}\td2,{arrayBaseOffset}(a6,d1.l)");
        }
        else if (elementSize == 4 && !_cpuFeatures.HasBarrelShifter)
        {
            // 68000: scale manually then use indexed addressing
            Emit("\tadd.l\td1,d1\t; index * 2");
            Emit("\tadd.l\td1,d1\t; index * 4");
            EmitComment("Optimized: indexed addressing with pre-scaled index");
            Emit($"\tmove{elementSizeSuffix}\td2,{arrayBaseOffset}(a6,d1.l)");
        }
        else if (elementSize == 8 && !_cpuFeatures.HasBarrelShifter)
        {
            // 68000: scale manually then use indexed addressing
            Emit("\tadd.l\td1,d1\t; index * 2");
            Emit("\tadd.l\td1,d1\t; index * 4");
            Emit("\tadd.l\td1,d1\t; index * 8");
            EmitComment("Optimized: indexed addressing with pre-scaled index");
            Emit($"\tmove{elementSizeSuffix}\td2,{arrayBaseOffset}(a6,d1.l)");
        }
        else
        {
            // For other sizes, use multiplication and traditional addressing
            Emit($"\tmulu.w\t#{elementSize},d1");
            Emit($"\tlea\t{arrayBaseOffset}(a6),a0");
            Emit("\tsuba.l\td1,a0");
            Emit($"\tmove{elementSizeSuffix}\td2,(a0)");
        }
    }

    private void GenerateMemberAccess(IrMemberAccess memberAccess)
    {
        EmitComment($"{memberAccess.ResultName} = {memberAccess.Struct}.{memberAccess.FieldName}");

        // Check if the base is a dereferenced pointer (auto-dereferenced for member access)
        if (memberAccess.Struct is IrDereferenceValue derefValue)
        {
            // Get the pointer variable
            if (derefValue.PointerValue is not IrVariable ptrVar)
            {
                throw new Exception("Dereference base must be a variable");
            }

            // Load the pointer into a0
            if (!_localVariableOffsets.ContainsKey(ptrVar.Name))
            {
                throw new Exception($"Pointer variable {ptrVar.Name} not found");
            }

            var ptrOffset = _localVariableOffsets[ptrVar.Name];
            Emit($"\tmovea.l\t{ptrOffset}(a6),a0");
            EmitComment($"Load pointer {ptrVar.Name} for member access");

            // Access the field through the pointer
            var fieldSizeSuffix = GetSizeSuffix(memberAccess.FieldType);

            // If d0 already has a value that hasn't been consumed, save it first
            if (_lastResultInD0 != null)
            {
                EmitComment($"Save previous d0 value ({_lastResultInD0}) before overwriting");
                Emit("\tmove.l\td0,-(sp)");
                _savedTemps.Add(_lastResultInD0);
                _tempStackOffset += 4;
            }

            Emit($"\tmove{fieldSizeSuffix}\t{memberAccess.FieldOffset}(a0),d0");

            // Track that this result is in d0 for the next instruction
            _lastResultInD0 = memberAccess.ResultName;
            // Field access temps stay in d0 to be consumed by next instruction
            // (tracked via _lastResultInD0)

            return;
        }

        // Get struct variable
        if (memberAccess.Struct is not IrVariable structVar)
        {
            throw new Exception("Struct member access base must be a variable or dereference");
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

            // If d0 already has a value that hasn't been consumed, save it first
            if (_lastResultInD0 != null)
            {
                EmitComment($"Save previous d0 value ({_lastResultInD0}) before overwriting");
                Emit("\tmove.l\td0,-(sp)");
                _savedTemps.Add(_lastResultInD0);
                _tempStackOffset += 4;
            }

            // Load field value into d0
            Emit($"\tmove{fieldSizeSuffix}\t{fieldOffset}(a6),d0");

            // Track that this result is in d0 for the next instruction
            _lastResultInD0 = memberAccess.ResultName;
            // Field access temps stay in d0 to be consumed by next instruction
        }
    }

    private void GenerateMemberStore(IrMemberStore memberStore)
    {
        EmitComment($"{memberStore.Struct}.{memberStore.FieldName} = value");

        var fieldSizeSuffix = GetSizeSuffix(memberStore.Value.Type);

        // Check if the base is a dereferenced pointer (auto-dereferenced for member access)
        if (memberStore.Struct is IrDereferenceValue derefValue)
        {
            // Get the pointer variable
            if (derefValue.PointerValue is not IrVariable ptrVar)
            {
                throw new Exception("Dereference base must be a variable");
            }

            // Load value to store into d0
            LoadOperand(memberStore.Value, "d0");

            // Load the pointer into a0
            if (!_localVariableOffsets.ContainsKey(ptrVar.Name))
            {
                throw new Exception($"Pointer variable {ptrVar.Name} not found");
            }

            var ptrOffset = _localVariableOffsets[ptrVar.Name];
            Emit($"\tmovea.l\t{ptrOffset}(a6),a0");
            EmitComment($"Load pointer {ptrVar.Name} for member store");

            // Store the value to the field through the pointer
            Emit($"\tmove{fieldSizeSuffix}\td0,{memberStore.FieldOffset}(a0)");

            return;
        }

        // Get struct variable
        if (memberStore.Struct is not IrVariable structVar)
        {
            throw new Exception("Struct member store base must be a variable or dereference");
        }

        // Get base address of struct (its stack offset)
        if (!_localVariableOffsets.ContainsKey(structVar.Name))
        {
            throw new Exception($"Struct variable {structVar.Name} not found");
        }

        var structBaseOffset = _localVariableOffsets[structVar.Name];
        var fieldOffset = structBaseOffset + memberStore.FieldOffset;

        // Load value to store into d0
        LoadOperand(memberStore.Value, "d0");

        // Store to field location
        Emit($"\tmove{fieldSizeSuffix}\td0,{fieldOffset}(a6)");
    }

    /// <summary>
    /// Recursively initialize struct fields, handling nested structs
    /// </summary>
    private void InitializeStructFields(IrStructLiteral structLiteral, IrStructType structType, int structBaseOffset, bool useAddressRegister = false)
    {
        // useAddressRegister: if true, write to (a0) instead of (a6)
        // This is used for initializing large struct return values directly at the hidden pointer location
        string baseRegister = useAddressRegister ? "a0" : "a6";

        foreach (var field in structType.Fields)
        {
            if (!structLiteral.FieldValues.ContainsKey(field.Name))
            {
                throw new Exception($"Struct field '{field.Name}' not initialized");
            }

            var fieldValue = structLiteral.FieldValues[field.Name];
            int fieldOffset;

            if (useAddressRegister)
            {
                // For A0: offset is positive from base (hidden return pointer)
                fieldOffset = structBaseOffset + field.Offset;
            }
            else
            {
                // For A6: offset is negative from base (frame pointer)
                fieldOffset = structBaseOffset - field.Offset;
            }

            // Check if this field is itself a struct literal
            if (fieldValue is IrStructLiteral nestedStructLiteral && field.Type is IrStructType nestedStructType)
            {
                // Recursively initialize nested struct from literal
                EmitComment($"Initialize nested struct field {field.Name}");
                InitializeStructFields(nestedStructLiteral, nestedStructType, fieldOffset, useAddressRegister);
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
                Emit($"\tmove{fieldSizeSuffix}\td0,{fieldOffset}({baseRegister})");
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
        EmitComment("  __div_i32(d0, d1) -> d0    : Signed 32-bit divide");
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

        // Generate divide (signed)
        GenerateRuntimePrimitive("__div_i32", "Signed 32-bit divide",
            GenerateDivI32_68000, GenerateDivI32_68020, GenerateDivI32_68060);

        // Generate divide (unsigned)
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

        // Generate division helper (shared by signed and unsigned division on 68000)
        GenerateDivisionHelper();
    }

    private void GenerateRuntimePrimitive(string name, string description,
        Action gen68000, Action gen68020, Action gen68060)
    {
        EmitComment($"{description}");
        Emit($"\txdef\t{name}");
        Emit($"{name}:");
        EmitComment("CPU already detected at startup, just read the flag");
        Emit("\tmove.l\t__detected_cpu,d2");  // Load CPU flag (d2 preserves d0/d1)
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
    private void GenerateDivI32_68000()
    {
        EmitComment("68000: Signed 32-bit divide using 16-bit divs");
        EmitComment("Uses repeated subtraction for high-order bits");
        Emit("\tmovem.l\td2-d4,-(sp)\t; Save registers");

        // Handle sign: convert to unsigned, divide, fix sign
        Emit("\tmoveq\t#0,d4\t\t; d4 = sign tracker");
        Emit("\ttst.l\td0");
        Emit("\tbpl.s\t.divsi_pos_dividend");
        Emit("\tneg.l\td0");
        Emit("\tnot.l\td4");
        Emit(".divsi_pos_dividend:");
        Emit("\ttst.l\td1");
        Emit("\tbpl.s\t.divsi_pos_divisor");
        Emit("\tneg.l\td1");
        Emit("\tnot.l\td4");
        Emit(".divsi_pos_divisor:");

        // Call unsigned division helper
        Emit("\tbsr\t__divu32_helper_internal");

        // Fix sign of result
        Emit("\ttst.l\td4");
        Emit("\tbeq.s\t.divsi_done");
        Emit("\tneg.l\td0");
        Emit(".divsi_done:");
        Emit("\tmovem.l\t(sp)+,d2-d4");
        Emit("\trts");
    }

    private void GenerateDivI32_68020()
    {
        EmitComment("68020: Native 32-bit signed divide");
        Emit("\tdivs.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateDivI32_68060()
    {
        EmitComment("68060: Very slow divide (>70 cycles)");
        EmitComment("Consider alternatives if possible");
        Emit("\tdivs.l\td1,d0");
        Emit("\trts");
    }

    private void GenerateDivU32_68000()
    {
        EmitComment("68000: Unsigned 32-bit divide");
        Emit("\tmovem.l\td2-d3,-(sp)");
        Emit("\tbsr\t__divu32_helper_internal");
        Emit("\tmovem.l\t(sp)+,d2-d3");
        Emit("\trts");
    }

    // Helper function for unsigned 32-bit division (used by both signed and unsigned)
    private void GenerateDivisionHelper()
    {
        // Unsigned 32÷32→32 division helper
        // Input: d0=dividend, d1=divisor
        // Output: d0=quotient
        // Uses: d2=counter, d3=quotient
        Emit("__divu32_helper_internal:");
        EmitComment("Unsigned 32-bit division helper (used internally)");
        Emit("\tmoveq\t#0,d3\t\t; quotient = 0");
        Emit("\tmoveq\t#31,d2\t\t; bit counter");
        Emit("\ttst.l\td1");
        Emit("\tbne.s\t.divu_loop");
        Emit("\tmoveq\t#-1,d0\t\t; division by zero");
        Emit("\trts");

        Emit(".divu_loop:");
        // Shift dividend left, test if divisor fits
        Emit("\tadd.l\td0,d0\t\t; dividend <<= 1");
        Emit("\tbcc.s\t.divu_no_carry\t; branch if no carry");
        // Carry set: high bit was 1
        Emit("\tsub.l\td1,d0\t\t; subtract divisor");
        Emit("\taddq.l\t#1,d3\t\t; quotient++");
        Emit("\tdbf\td2,.divu_loop");
        Emit("\tmove.l\td3,d0\t\t; return quotient");
        Emit("\trts");

        Emit(".divu_no_carry:");
        Emit("\tcmp.l\td1,d0\t\t; dividend >= divisor?");
        Emit("\tbcs.s\t.divu_next\t\t; branch if less");
        Emit("\tsub.l\td1,d0\t\t; subtract divisor");
        Emit("\taddq.l\t#1,d3\t\t; quotient++");
        Emit(".divu_next:");
        Emit("\tdbf\td2,.divu_loop");
        Emit("\tmove.l\td3,d0\t\t; return quotient");
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

            EmitComment("CPU already detected at startup, just read the flag");
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

            // Check if extracting composite data (> 4 bytes)
            if (extractData.DataType.SizeInBytes > 4)
            {
                var typeName = extractData.DataType is IrEnumType ? "enum" : "struct";
                EmitComment($"Extract {typeName} (size: {extractData.DataType.SizeInBytes} bytes) to stack");

                // Copy composite data to stack longword-by-longword
                int numLongwords = (extractData.DataType.SizeInBytes + 3) / 4;
                Emit($"\tsub.l\t#{extractData.DataType.SizeInBytes},sp\t\t; Allocate space for {typeName}");

                for (int i = 0; i < numLongwords; i++)
                {
                    Emit($"\tmove.l\t{varOffset + dataOffset + (i * 4)}(a6),d0");
                    Emit($"\tmove.l\td0,{i * 4}(sp)\t\t; Copy longword {i}");
                }

                // Save as temp on stack
                _savedTemps.Add(extractData.ResultName);
                _savedTempSizes[extractData.ResultName] = extractData.DataType.SizeInBytes;
                _tempStackOffset += extractData.DataType.SizeInBytes;
            }
            else
            {
                // Simple data - load into d0
                var dataSize = GetSizeSuffix(extractData.DataType);
                Emit($"\tmove{dataSize}\t{varOffset + dataOffset}(a6),d0\t\t; Load variant data");

                // Track that result is in d0
                _lastResultInD0 = extractData.ResultName;
            }
        }
        else
        {
            throw new Exception($"Unsupported enum value type for data extraction: {enumValue.GetType().Name}");
        }

        // Result is in d0 (for simple types) or on stack (for composite types)
    }

}
