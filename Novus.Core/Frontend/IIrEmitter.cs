using Novus.Diagnostics;
using Novus.IR;

namespace Novus.Frontend;

/// <summary>
/// Interface for emitting IR instructions.
/// This represents the third phase: generating IR from validated AST nodes.
/// </summary>
public interface IIrEmitter
{
    /// <summary>
    /// Emit an instruction to the current basic block.
    /// </summary>
    void Emit(IrInstruction instruction);

    /// <summary>
    /// Create a new basic block with the given label.
    /// </summary>
    IrBasicBlock CreateBlock(string label);

    /// <summary>
    /// Switch to emitting to the given basic block.
    /// </summary>
    void SwitchToBlock(IrBasicBlock block);

    /// <summary>
    /// Get the current basic block being emitted to.
    /// </summary>
    IrBasicBlock? CurrentBlock { get; }

    /// <summary>
    /// Generate a unique temporary variable name.
    /// </summary>
    string GenerateTemp();

    /// <summary>
    /// Generate a unique label name.
    /// </summary>
    string GenerateLabel(string prefix = "L");

    /// <summary>
    /// Emit a local variable declaration.
    /// </summary>
    void EmitLocalDecl(string name, IrType type, bool isMutable, IrValue? initialValue = null);

    /// <summary>
    /// Emit a store to a variable.
    /// </summary>
    void EmitStore(string name, IrValue value);

    /// <summary>
    /// Emit a branch to a label.
    /// </summary>
    void EmitBranch(string targetLabel);

    /// <summary>
    /// Emit a conditional branch.
    /// </summary>
    void EmitConditionalBranch(IrValue condition, string trueLabel, string falseLabel);

    /// <summary>
    /// Emit a return instruction.
    /// </summary>
    void EmitReturn(IrValue? value = null);

    /// <summary>
    /// Emit a function call.
    /// </summary>
    string? EmitCall(string functionName, IrType returnType, List<IrValue> arguments);
}

/// <summary>
/// Default implementation of IIrEmitter that emits to an IrFunction.
/// </summary>
public class DefaultIrEmitter : IIrEmitter
{
    private readonly IrFunction _function;
    private IrBasicBlock? _currentBlock;
    private int _tempCounter = 0;
    private int _labelCounter = 0;
    private SourceLocation? _currentLocation;

    public DefaultIrEmitter(IrFunction function)
    {
        _function = function;
    }

    public IrBasicBlock? CurrentBlock => _currentBlock;

    public void SetLocation(SourceLocation? location)
    {
        _currentLocation = location;
    }

    public void Emit(IrInstruction instruction)
    {
        if (_currentBlock == null)
        {
            throw new InvalidOperationException("Cannot emit instruction: no current block");
        }

        if (_currentLocation != null)
        {
            instruction.Location = _currentLocation;
        }

        _currentBlock.AddInstruction(instruction);
    }

    public IrBasicBlock CreateBlock(string label)
    {
        var block = new IrBasicBlock(label);
        _function.BasicBlocks.Add(block);
        return block;
    }

    public void SwitchToBlock(IrBasicBlock block)
    {
        _currentBlock = block;
    }

    public string GenerateTemp()
    {
        return $"%t{_tempCounter++}";
    }

    public string GenerateLabel(string prefix = "L")
    {
        return $"{prefix}{_labelCounter++}";
    }

    public void EmitLocalDecl(string name, IrType type, bool isMutable, IrValue? initialValue = null)
    {
        Emit(new IrLocalDecl(name, type, isMutable, initialValue ?? CreateDefaultValue(type)));
    }

    public void EmitStore(string name, IrValue value)
    {
        Emit(new IrStore(name, value));
    }

    public void EmitBranch(string targetLabel)
    {
        Emit(new IrBranch(targetLabel));
    }

    public void EmitConditionalBranch(IrValue condition, string trueLabel, string falseLabel)
    {
        Emit(new IrConditionalBranch(condition, trueLabel, falseLabel));
    }

    public void EmitReturn(IrValue? value = null)
    {
        Emit(new IrReturn(value));
    }

    public string? EmitCall(string functionName, IrType returnType, List<IrValue> arguments)
    {
        string? resultName = null;
        if (returnType is not IrVoidType)
        {
            resultName = GenerateTemp();
        }

        var call = new IrCall(functionName, returnType, resultName);
        foreach (var arg in arguments)
        {
            call.Arguments.Add(arg);
        }
        Emit(call);

        return resultName;
    }

    private IrValue CreateDefaultValue(IrType type)
    {
        return type switch
        {
            IrIntType intType => new IrConstant(0, intType),
            IrBoolType => new IrBoolConstant(false),
            IrFloatType floatType => new IrFloatConstant(0.0, floatType),
            IrPointerType pt => new IrConstant(0, IrIntType.U32), // null pointer
            _ => new IrConstant(0, IrIntType.I32) // fallback
        };
    }
}
