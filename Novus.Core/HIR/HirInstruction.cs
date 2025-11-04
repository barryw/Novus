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
    /// List of Copper operations (WAIT, MOVE, SKIP)
    /// </summary>
    public List<CopperOperation> Operations { get; } = new();

    /// <summary>
    /// Whether this copper list should be validated at compile time
    /// </summary>
    public bool ValidateAtCompileTime { get; set; } = true;

    public override List<IrInstruction> Lower()
    {
        // TODO: Implement Copper list lowering
        //
        // Algorithm:
        // 1. Validate copper list operations (if enabled)
        //    - Check register addresses are valid ($dff000-$dff1ff)
        //    - Ensure WAIT positions are within screen bounds
        //    - Verify timing constraints (beam position, wait times)
        //
        // 2. Allocate memory for copper list (chip RAM required!)
        //    - Calculate total size (4 bytes per instruction)
        //    - Allocate in chip RAM (Type Allocator parameter)
        //
        // 3. Generate copper list data in memory
        //    - Each operation is 2 longwords (address, data) or (wait position, mask)
        //    - Terminate with $fffffffe
        //
        // 4. Return address of copper list
        //
        // Example Copper DSL:
        //   copper {
        //     wait(0, 100)              // Wait for line 100
        //     move(COLOR00, 0xF00)      // Set background to red
        //     wait(0, 150)              // Wait for line 150
        //     move(COLOR00, 0x00F)      // Set background to blue
        //   }
        //
        // Lowered to:
        //   ; Allocate chip RAM for copper list
        //   lea __copper_list_0,a0
        //   ; Copper list data (generated at compile time):
        //   __copper_list_0:
        //     dc.w $6401,$fffe  ; WAIT(0,100)
        //     dc.w $0180,$0f00  ; COLOR00 = red
        //     dc.w $9601,$fffe  ; WAIT(0,150)
        //     dc.w $0180,$000f  ; COLOR00 = blue
        //     dc.w $ffff,$fffe  ; END

        throw new NotImplementedException("Copper list lowering not yet implemented");
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
/// Represents a Blitter operation (Amiga custom chip)
/// The Blitter is a co-processor for fast memory copying and bit manipulation
/// </summary>
public class HirBlitterJob : HirInstruction
{
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
        // TODO: Implement Blitter job lowering
        //
        // Algorithm:
        // 1. Validate blitter operation
        //    - Check source/destination pointers are in chip RAM
        //    - Verify minterm is valid
        //    - Ensure width/height within bounds
        //
        // 2. Wait for blitter to be idle
        //    - Check DMACONR bit 14 (BLTDONE)
        //    - Spin-wait or use interrupt
        //
        // 3. Set up blitter registers
        //    - BLTCON0/BLTCON1: Control flags
        //    - BLTxPT: Source/destination pointers
        //    - BLTxMOD: Modulos for next line
        //    - BLTSIZE: Width and height
        //
        // 4. Start blitter
        //    - Write to BLTSIZE triggers operation
        //
        // 5. Optionally wait for completion
        //
        // Example Blitter DSL:
        //   blitter {
        //     operation: Copy
        //     source: sprite_data
        //     dest: screen_buffer
        //     width: 16
        //     height: 16
        //     minterm: 0xF0  // Copy source
        //   }
        //
        // Lowered to:
        //   ; Wait for blitter idle
        //   .wait_blit:
        //     btst #6,$dff002  ; DMACONR bit 14
        //     bne.s .wait_blit
        //   ; Set up pointers
        //   move.l sprite_data,$dff050  ; BLTAPTH/L
        //   move.l screen_buffer,$dff054  ; BLTDPTH/L
        //   ; Set control
        //   move.w #$09f0,$dff040  ; BLTCON0
        //   move.w #$0000,$dff042  ; BLTCON1
        //   ; Set size and start
        //   move.w #$1001,$dff058  ; BLTSIZE (16x16)

        throw new NotImplementedException("Blitter job lowering not yet implemented");
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
        // TODO: Implement async function lowering
        //
        // Algorithm:
        // 1. Create state machine struct
        //    - Add state field (u32)
        //    - Add fields for all local variables
        //    - Add fields for parameters
        //
        // 2. Transform function body into switch on state
        //    - Each await point becomes a case
        //    - Save state before await
        //    - Return Pending if not ready
        //    - Resume at next state when signaled
        //
        // 3. Generate resume function
        //    - Takes &mut state_machine
        //    - Returns AsyncResult<T>
        //    - Can be called multiple times
        //
        // 4. Integrate with Exec signals
        //    - AllocSignal() for async notification
        //    - Wait() when async work pending
        //    - Signal() to resume
        //
        // Example async function:
        //   async fn fetch_data(url: &str) -> Result<Data, Error> {
        //     let request = create_request(url);
        //     let response = await send_request(request);
        //     let data = await parse_response(response);
        //     Ok(data)
        //   }
        //
        // Lowered to state machine:
        //   struct fetch_data_state {
        //     state: u32,
        //     url: *const u8,
        //     request: Request,
        //     response: Response,
        //     data: Data,
        //   }
        //
        //   fn fetch_data_resume(s: &mut fetch_data_state) -> AsyncResult<Data> {
        //     match s.state {
        //       0 => { /* create_request */ s.state = 1; goto state_1; }
        //       1 => { /* await send_request */ return Pending or continue; }
        //       2 => { /* await parse_response */ return Pending or continue; }
        //       3 => { /* return Ok(data) */ return Ready(s.data); }
        //     }
        //   }

        throw new NotImplementedException("Async function lowering not yet implemented");
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
