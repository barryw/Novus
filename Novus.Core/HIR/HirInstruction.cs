using Novus.IR;

namespace Novus.HIR;

/// <summary>
/// Base class for High-level IR (HIR) instructions
/// HIR represents language features that require special lowering to LIR/assembly:
/// - Copper display lists (hardware-specific DSL)
/// - Blitter operations (hardware-specific DSL)
/// - Async/await state machines (control flow transformation)
/// </summary>
public abstract class HirInstruction
{
    /// <summary>
    /// Name of the result variable (if any)
    /// </summary>
    public string? ResultName { get; set; }

    /// <summary>
    /// Type of the result (if any)
    /// </summary>
    public IrType? ResultType { get; set; }

    /// <summary>
    /// Lower this HIR instruction to LIR (low-level IR)
    /// Returns a list of LIR instructions that implement this HIR instruction
    /// </summary>
    public abstract List<IrInstruction> Lower();
}

/// <summary>
/// Represents a Copper display list (Amiga custom chip)
/// The Copper is a co-processor that can modify chip registers during display
/// </summary>
public class HirCopperList : HirInstruction
{
    /// <summary>
    /// List of Copper instructions (WAIT, MOVE, SKIP) with IR values
    /// </summary>
    public List<HirCopperInstruction> Instructions { get; }

    public HirCopperList(List<HirCopperInstruction> instructions)
    {
        Instructions = instructions;
    }

    /// <summary>
    /// List of Copper operations (WAIT, MOVE, SKIP) - legacy, use Instructions instead
    /// </summary>
    public List<CopperOperation> Operations { get; } = new();

    /// <summary>
    /// Whether this copper list should be validated at compile time
    /// </summary>
    public bool ValidateAtCompileTime { get; set; } = true;

    public override List<IrInstruction> Lower()
    {
        // Copper lowering is handled by CopperLoweringPass, not this method.
        // The pass operates on the IrModule and creates static data or runtime code.
        //
        // For constant copper lists: static data in chip RAM is generated
        // For non-constant copper lists: runtime building code is generated
        //
        // This method returns an empty list since the actual lowering adds
        // instructions directly to the IR module via the pass.
        return new List<IrInstruction>();
    }
}

/// <summary>
/// Copper operation types
/// </summary>
public abstract class CopperOperation
{
    public abstract void Validate();
}

/// <summary>
/// WAIT - Wait for beam to reach position (Y, X)
/// </summary>
public class CopperWait : CopperOperation
{
    public int VerticalPosition { get; set; }
    public int HorizontalPosition { get; set; }

    public override void Validate()
    {
        // PAL: 0-312 lines, NTSC: 0-262 lines
        if (VerticalPosition < 0 || VerticalPosition > 312)
        {
            throw new InvalidOperationException($"Copper WAIT vertical position {VerticalPosition} out of range (0-312)");
        }
        if (HorizontalPosition < 0 || HorizontalPosition > 226)
        {
            throw new InvalidOperationException($"Copper WAIT horizontal position {HorizontalPosition} out of range (0-226)");
        }
    }
}

/// <summary>
/// MOVE - Write value to custom chip register
/// </summary>
public class CopperMove : CopperOperation
{
    public uint RegisterAddress { get; set; }
    public ushort Value { get; set; }

    public override void Validate()
    {
        // Custom chip registers: $dff000-$dff1ff
        if (RegisterAddress < 0xdff000 || RegisterAddress > 0xdff1ff)
        {
            throw new InvalidOperationException($"Copper MOVE register ${RegisterAddress:X6} out of custom chip range ($dff000-$dff1ff)");
        }

        // Check if register is safe to write (some are read-only or dangerous)
        if (RegisterAddress == 0xdff080) // COP1LC - Copper 1 location (can cause infinite loop)
        {
            throw new InvalidOperationException("Copper MOVE to COP1LC is dangerous (can cause infinite loop)");
        }
    }
}

/// <summary>
/// HIR-level Copper instruction with IR values (for use during IR building).
/// These instructions hold IrValue references that will be lowered to actual
/// copper list data during the CopperLoweringPass.
/// </summary>
public abstract class HirCopperInstruction
{
    /// <summary>
    /// WAIT instruction - wait for beam to reach position (X, Y)
    /// </summary>
    public class Wait : HirCopperInstruction
    {
        public IrValue X { get; }
        public IrValue Y { get; }

        public Wait(IrValue x, IrValue y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// MOVE instruction - write value to custom chip register
    /// </summary>
    public class Move : HirCopperInstruction
    {
        public IrValue Register { get; }
        public IrValue Value { get; }

        public Move(IrValue register, IrValue value)
        {
            Register = register;
            Value = value;
        }
    }

    /// <summary>
    /// SKIP instruction - skip next instruction if beam past position (X, Y)
    /// </summary>
    public class Skip : HirCopperInstruction
    {
        public IrValue X { get; }
        public IrValue Y { get; }

        public Skip(IrValue x, IrValue y)
        {
            X = x;
            Y = y;
        }
    }
}

/// <summary>
/// Represents a Blitter operation (Amiga custom chip)
/// The Blitter is a co-processor for fast memory copying and bit manipulation
/// </summary>
public class HirBlitterJob : HirInstruction
{
    /// <summary>
    /// DSL fields from the blitter block (source, dest, width, height, minterm, etc.)
    /// Keys are lowercased field names, values are the IR values.
    /// </summary>
    public Dictionary<string, IrValue> Fields { get; }

    /// <summary>
    /// Create a blitter job from DSL fields.
    /// </summary>
    public HirBlitterJob(Dictionary<string, IrValue> fields)
    {
        Fields = fields;

        // Extract commonly used fields for convenience
        if (fields.TryGetValue("source", out var source) || fields.TryGetValue("sourcea", out source))
            SourceA = source;
        if (fields.TryGetValue("sourceb", out var sourceB))
            SourceB = sourceB;
        if (fields.TryGetValue("sourcec", out var sourceC))
            SourceC = sourceC;
        if (fields.TryGetValue("dest", out var dest) || fields.TryGetValue("destination", out dest))
            Destination = dest;
    }

    /// <summary>
    /// Blitter operation type (copy, fill, line drawing, etc.)
    /// </summary>
    public BlitterOperation Operation { get; set; }

    /// <summary>
    /// Source A pointer (optional, depends on operation)
    /// </summary>
    public IrValue? SourceA { get; set; }

    /// <summary>
    /// Source B pointer (optional, depends on operation)
    /// </summary>
    public IrValue? SourceB { get; set; }

    /// <summary>
    /// Source C pointer (optional, depends on operation)
    /// </summary>
    public IrValue? SourceC { get; set; }

    /// <summary>
    /// Destination pointer (required)
    /// </summary>
    public IrValue? Destination { get; set; }

    /// <summary>
    /// Blitter control register value (minterm, channels)
    /// </summary>
    public uint ControlFlags { get; set; }

    public override List<IrInstruction> Lower()
    {
        // Blitter DSL is lowered inline during IR building in IrBuilder.cs.
        // The blitter block generates direct register writes to hardware registers,
        // not an HIR instruction that needs separate lowering.
        //
        // The generated code:
        // 1. Waits for blitter idle (polling DMACONR)
        // 2. Sets up blitter registers (pointers, modulos, control)
        // 3. Writes BLTSIZE to trigger the operation
        //
        // See IrBuilder.VisitBlitterExpression for the implementation.
        return new List<IrInstruction>();
    }
}

/// <summary>
/// Blitter operation types
/// </summary>
public enum BlitterOperation
{
    Copy,           // Copy from source to dest
    Fill,           // Area fill
    Line,           // Draw line
    Mask,           // Masked copy (with mask channel)
    LogicOp,        // Arbitrary boolean operation (minterm)
}

/// <summary>
/// Represents an async function (state machine)
/// Async functions are lowered to state machines backed by Exec signals
/// </summary>
public class HirAsyncFunction : HirInstruction
{
    /// <summary>
    /// Name of the async function
    /// </summary>
    public string FunctionName { get; set; } = "";

    /// <summary>
    /// Parameters to the async function
    /// </summary>
    public List<IrParameter> Parameters { get; } = new();

    /// <summary>
    /// Return type of the async function
    /// Always wrapped in Result<T, E> or AsyncResult<T>
    /// </summary>
    public IrType ReturnType { get; set; } = IrVoidType.Instance;

    /// <summary>
    /// Await points in the function (where it can suspend)
    /// </summary>
    public List<AwaitPoint> AwaitPoints { get; } = new();

    /// <summary>
    /// Local variables that need to be preserved across await points
    /// </summary>
    public List<IrLocalVariable> StateMachineFields { get; } = new();

    public override List<IrInstruction> Lower()
    {
        // Async function lowering is a complex transformation that will be handled
        // by a dedicated AsyncLoweringPass. This creates a state machine struct
        // and transforms the function body into a resumable coroutine.
        //
        // The state machine:
        // 1. Has a state field (u32) tracking the current execution point
        // 2. Stores all local variables that live across await points
        // 3. Has a poll() method that advances execution until the next await
        //
        // Integration with AmigaOS:
        // - Uses Exec signals for async notification
        // - AllocSignal() to get a signal bit for each async operation
        // - Wait() to suspend until a signal arrives
        // - Signal() to wake up a waiting task
        //
        // This method returns an empty list since the actual lowering is done
        // by the AsyncLoweringPass operating on the full function.
        return new List<IrInstruction>();
    }
}

/// <summary>
/// Represents an await point in an async function
/// </summary>
public class AwaitPoint
{
    /// <summary>
    /// State number for this await point
    /// </summary>
    public int StateNumber { get; set; }

    /// <summary>
    /// The async expression being awaited
    /// </summary>
    public IrValue AwaitedExpression { get; set; } = null!;

    /// <summary>
    /// Variable to store the result
    /// </summary>
    public string ResultVariable { get; set; } = "";
}
