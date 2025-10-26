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
    private readonly string _fpuMode;
    private readonly Dictionary<string, int> _registerAllocation = new();
    private int _labelCounter = 0;
    private IrFunction? _currentFunction;
    private bool _currentFunctionHasPrologue = false;
    private bool _generatingFpuVersion = false; // true when generating _fpu version of function

    // Track last comparison for optimization
    private string? _lastComparisonResult;
    private string? _lastComparisonCondition;

    // Track local variable stack offsets
    private readonly Dictionary<string, int> _localVariableOffsets = new();

    // Track temp variables saved on stack (in order of saving)
    private readonly List<string> _savedTemps = new();
    private int _tempStackOffset = 0; // Total bytes used for temps

    // Track which functions use floating point operations
    private readonly HashSet<string> _floatFunctions = new();

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
        _fpuMode = fpuMode.ToLower();
    }

    private bool Is68020Plus => _cpuTarget != "68000" && _cpuTarget != "68010";

    // Check if we're building fat binaries with runtime FPU detection
    private bool IsFatBinary => _fpuMode == "auto";

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
            if (IsFatBinary && _floatFunctions.Contains("main"))
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
            if (IsFatBinary && _floatFunctions.Contains(function.Name))
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
        Emit("\t; C++ constructor/destructor lists (empty for Novus)");
        Emit("\tsection\tdata,data");
        Emit("\txdef\t___CTOR_LIST__");
        Emit("\txdef\t___DTOR_LIST__");
        Emit("___CTOR_LIST__:");
        Emit("\tdc.l\t0\t; Count of constructors");
        Emit("\tdc.l\t0\t; Terminator");
        Emit("___DTOR_LIST__:");
        Emit("\tdc.l\t0\t; Count of destructors");
        Emit("\tdc.l\t0\t; Terminator");
        Emit("");
        Emit("\tsection\ttext,code");
        Emit("");
    }

    private void GenerateFunction(IrFunction function, string suffix = "")
    {
        _currentFunction = function;
        _currentFunctionHasPrologue = false;

        // Handle extern functions - just emit xref
        if (function.IsExtern)
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
        _tempStackOffset = 0;             // Reset temp stack offset
        _structMemberLocations.Clear();   // Clear struct member locations for new function

        var functionLabel = $"_{function.Name}{suffix}";
        EmitComment($"Function: {function.Name}{suffix}");

        // Export symbol only if function is public (using xdef for vasm Motorola syntax)
        // For fat binaries, only export the base name (dispatch stub will be generated)
        if (function.IsPublic && suffix == "")
        {
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
            // Parameters are passed as longwords (4 bytes minimum)
            paramOffset += 4;
        }

        // Calculate stack space needed for local variables
        int stackSpace = 0;
        int currentOffset = 0; // Local variables grow downward from a6

        foreach (var localVar in function.LocalVariables)
        {
            var size = localVar.Type.SizeInBytes;
            // Align to 2 bytes for 68000
            if (size % 2 != 0)
                size += 1;

            currentOffset -= size;
            _localVariableOffsets[localVar.Name] = currentOffset;
            stackSpace += size;
        }

        // Align stack space to 2 bytes
        if (stackSpace % 2 != 0)
            stackSpace += 1;

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

    private void GenerateBasicBlock(IrBasicBlock block)
    {
        // Emit block label if it's not the entry block
        if (block.Label != "entry")
        {
            Emit($"{block.Label}:");
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
                break;
            case IrExtractVariantData extractData:
                GenerateExtractVariantData(extractData);
                break;
            default:
                throw new Exception($"Unknown instruction type: {instruction.GetType().Name}");
        }
    }

    private void GenerateLabel(IrLabel label)
    {
        Emit($"{label.Name}:");
    }

    private void GenerateBranch(IrBranch branch)
    {
        Emit($"\tbra\t{branch.Target}");
    }

    private void GenerateConditionalBranch(IrConditionalBranch condBranch)
    {
        // The condition value should be in d0 (materialized as 0 or 1)
        // Test it and branch accordingly
        LoadOperand(condBranch.Condition, "d0");
        Emit($"\ttst.l\td0");
        Emit($"\tbne\t{condBranch.TrueTarget}");
        Emit($"\tbra\t{condBranch.FalseTarget}");

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

            if (_generatingFpuVersion && isTrueFloatReturn)
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

        // Emit epilogue and return
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
                LoadOperand(binOp.Right, "d0");  // Shift amount
                LoadOperand(binOp.Left, "d1");   // Value to shift
                Emit($"\tlsl{size}\td0,d1");
                Emit($"\tmove{size}\td1,d0");
                break;

            case IrBinaryOp.OpKind.Shr:
                EmitComment($"{binOp.ResultName} = shr");
                LoadOperand(binOp.Right, "d0");  // Shift amount
                LoadOperand(binOp.Left, "d1");   // Value to shift
                Emit($"\tlsr{size}\td0,d1");
                Emit($"\tmove{size}\td1,d0");
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

        // Save the result on the stack for later use if it's actually used
        if (binOp.ResultName != null && IsTempUsed(binOp.ResultName))
        {
            Emit($"\tmove.l\td0,-(sp)");
            _savedTemps.Add(binOp.ResultName);
            _tempStackOffset += 4;
        }
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

        // Push arguments onto stack in reverse order (right to left, per C calling convention)
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

        // Call the function using JSR (Jump to Subroutine)
        Emit($"\tjsr\t_{call.FunctionName}");

        // Clean up stack (remove arguments)
        if (call.Arguments.Count > 0)
        {
            var stackCleanup = call.Arguments.Count * 4; // 4 bytes per argument
            Emit($"\tlea\t{stackCleanup}(sp),sp");
        }

        // If the result has a name (%tN) AND is actually used later, save it on the stack
        if (call.ResultName != null && !(call.ReturnType is IrVoidType))
        {
            // Check if this temp is actually referenced elsewhere in the function
            bool isUsed = IsTempUsed(call.ResultName);

            if (isUsed)
            {
                Emit($"\tmove.l\td0,-(sp)");
                _savedTemps.Add(call.ResultName); // Add to list of saved temps
                _tempStackOffset += 4; // Track that we've used 4 more bytes on the stack
            }
            // Otherwise, result is in d0 but will be discarded (expression statement)
        }
        // Otherwise, result is already in d0 for immediate use
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

        // If the result has a name and is actually used, save it on the stack for later use
        if (call.ResultName != null && !(call.ReturnType is IrVoidType) && IsTempUsed(call.ResultName))
        {
            Emit($"\tmove.l\td0,-(sp)");
            _savedTemps.Add(call.ResultName);
            _tempStackOffset += 4;
        }
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
        if (elementSize == 1)
        {
            // No multiplication needed for byte arrays
        }
        else if (elementSize == 2)
        {
            // index * 2 = index << 1
            Emit("\tadd.l\td1,d1");
        }
        else if (elementSize == 4)
        {
            // index * 4 = index << 2
            Emit("\tadd.l\td1,d1");
            Emit("\tadd.l\td1,d1");
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

        // Save result on stack for later use if it's actually used
        if (IsTempUsed(indexAccess.ResultName))
        {
            Emit("\tmove.l\td0,-(sp)");
            _savedTemps.Add(indexAccess.ResultName);
            _tempStackOffset += 4;
        }
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
        if (elementSize == 1)
        {
            // No multiplication needed for byte arrays
        }
        else if (elementSize == 2)
        {
            // index * 2 = index << 1
            Emit("\tadd.l\td1,d1");
        }
        else if (elementSize == 4)
        {
            // index * 4 = index << 2
            Emit("\tadd.l\td1,d1");
            Emit("\tadd.l\td1,d1");
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

        // Parameters are at positive offsets, locals at negative
        if (structBaseOffset >= 8)
        {
            // This is a parameter (passed by value on stack)
            // Fields are at base + field.Offset
            fieldOffset = structBaseOffset + cumulativeFieldOffset;
        }
        else
        {
            // This is a local variable
            // Fields grow downward, so base - field.Offset
            fieldOffset = structBaseOffset - cumulativeFieldOffset;
        }

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

            // Save result on the stack for later use if it's actually used
            if (IsTempUsed(memberAccess.ResultName))
            {
                Emit($"\tmove.l\td0,-(sp)");
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
                // Check if this is a parameter
                if (_currentFunction != null)
                {
                    var paramIndex = _currentFunction.Parameters.FindIndex(p => p.Name == variable.Name);
                    if (paramIndex >= 0)
                    {
                        // Parameters are on the stack after link frame
                        // Stack layout: [return addr][old a6][param1][param2]...
                        // After link a6,#0: params start at 8(a6)
                        // Parameters are passed as longwords (4 bytes each)
                        var baseOffset = 8 + (paramIndex * 4);

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
                    var savedIndex = _savedTemps.IndexOf(variable.Name);
                    if (savedIndex >= 0)
                    {
                        // Calculate offset: most recent temp is at 0(sp), earlier ones at higher offsets
                        // If saved temps are [%t1, %t2, %t3], then %t3 is at 0(sp), %t2 at 4(sp), %t1 at 8(sp)
                        var baseOffset = (_savedTemps.Count - 1 - savedIndex) * 4;

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
                else
                {
                    // Unknown variable - shouldn't happen if semantic analysis passed
                    throw new Exception($"Unknown variable: {variable.Name}");
                }
                break;
            }
            case IrEnumValue enumValue:
            {
                // Construct an enum value on the stack
                // Enum layout: [tag (4 bytes)][data (variable)]
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
        Emit("\tbne.s\t.done");
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
        Emit("\tbeq.s\t.no_fpu");
        Emit("");

        Emit("\t; FPU detected!");
        Emit("\tmove.l\t#1,__has_fpu");
        Emit("");

        Emit(".no_fpu:");
        Emit(".done:");
        Emit("\trts");
        Emit("");
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
            // Load tag from variable location (offset 0)
            var offset = _localVariableOffsets[enumVar.Name];
            Emit($"\tmove.l\t{offset}(a6),d0\t\t; Load enum tag");
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

        // Store result in temp
        _savedTemps.Add(extractTag.ResultName);
        _tempStackOffset += 4;
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

        // Store result in temp
        _savedTemps.Add(extractData.ResultName);
        _tempStackOffset += 4;
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
