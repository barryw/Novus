# Paula Audio DSL Implementation Guide

This document provides a comprehensive roadmap for implementing Paula audio support in the Novus compiler, based on analysis of existing Copper and Blitter DSL implementations.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Existing DSL Pattern Analysis](#existing-dsl-pattern-analysis)
3. [Paula Audio Hardware](#paula-audio-hardware)
4. [Implementation Plan](#implementation-plan)
5. [File-by-File Changes](#file-by-file-changes)
6. [Testing Strategy](#testing-strategy)
7. [Example Usage](#example-usage)

---

## Architecture Overview

### Compilation Pipeline for Hardware DSLs

```
Source Code (.novus)
    ↓
Lexer/Parser (ANTLR Grammar)
    ↓
AST → IrBuilder
    ↓
HIR (High-Level IR) Instructions
    ↓
Lowering Passes (HIR → LIR)
    ↓
IR Optimization
    ↓
C Code Generator
    ↓
Assembly (.s) via VBCC
```

### Three Approaches for Hardware DSLs

Based on existing implementations, there are three patterns:

1. **Static Data Approach (Copper)**: DSL generates compile-time data structures in chip RAM
2. **Inline Register Writes (Blitter)**: DSL expands to direct hardware register writes
3. **Hybrid Approach**: Mix of both (recommended for Paula)

---

## Existing DSL Pattern Analysis

### 1. Copper DSL Architecture

**File Locations:**
- Grammar: `/Users/barry/RiderProjects/Novus/Novus.Core/Novus.g4` (lines 413-427)
- IrBuilder: `/Users/barry/RiderProjects/Novus/Novus.Core/Frontend/IrBuilder.Expressions.cs` (lines 5128-5200)
- HIR Types: `/Users/barry/RiderProjects/Novus/Novus.Core/HIR/HirInstruction.cs` (lines 31-205)
- Lowering Pass: `/Users/barry/RiderProjects/Novus/Novus/Transforms/Passes/CopperLoweringPass.cs`
- Validator: `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/Validators/CopperDslValidator.cs`

**Pattern:**
```novus
copper {
    wait(0, 100)           // Wait for beam position
    move(COLOR00, $F00)    // Write to hardware register
}
```

**Implementation Flow:**
1. Parser recognizes `copper { }` block
2. IrBuilder creates `HirCopperList` with `HirCopperInstruction` entries
3. CopperLoweringPass converts to static data:
   - Creates `IrStaticVariable` with copper list words in chip RAM
   - Each instruction becomes 2 words (4 bytes)
   - Returns pointer to static data
4. C codegen emits static array in `.chip` section

**Key Design Points:**
- Compile-time validation of register addresses and beam positions
- All data is constant and pre-computed
- Requires chip RAM (enforced by `MemorySection.Chip`)
- No runtime overhead—just data

---

### 2. Blitter DSL Architecture

**File Locations:**
- Grammar: `/Users/barry/RiderProjects/Novus/Novus.Core/Novus.g4` (lines 429-437)
- IrBuilder: `/Users/barry/RiderProjects/Novus/Novus.Core/Frontend/IrBuilder.Expressions.cs` (lines 5211-5240)
- HIR Types: `/Users/barry/RiderProjects/Novus/Novus.Core/HIR/HirInstruction.cs` (lines 207-331)
- Lowering Pass: `/Users/barry/RiderProjects/Novus/Novus/Transforms/Passes/BlitterLoweringPass.cs`
- Validator: `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/Validators/BlitterDslValidator.cs`

**Pattern:**
```novus
blitter {
    source: ptr,
    dest: screen,
    width: 16,
    height: 16,
    minterm: $F0
}
```

**Implementation Flow:**
1. Parser recognizes `blitter { }` block with field assignments
2. IrBuilder creates `HirBlitterJob` with field dictionary
3. BlitterLoweringPass extracts parameters and computes register values:
   - Calculates BLTCON0, BLTCON1, BLTSIZE
   - Determines channel usage from minterm
   - Validates sizes and pointers
4. **Current approach**: Inline register writes (may change to HIR lowering)

**Key Design Points:**
- Field-based syntax (key: value pairs)
- Runtime pointer values allowed
- Register writes generated inline
- Wait-for-completion optional

---

### 3. Hardware Register Access Pattern

**File Location:** `/Users/barry/RiderProjects/Novus/Novus/Codegen/CCodeGenerator.cs` (line 3076)

**External Variables with Fixed Addresses:**
```c
#define REGNAME (*(volatile TYPE*)ADDRESS)
```

**Usage in Novus:**
```novus
extern var COLOR00: u16 at $DFF180
```

**Generated C:**
```c
#define COLOR00 (*(volatile uint16_t*)0xDFF180)
```

This pattern is perfect for Paula audio registers.

---

## Paula Audio Hardware

### Paula Chip Overview

Paula is the Amiga's audio/disk controller with **4 independent 8-bit DMA audio channels**.

**Key Features:**
- 4 channels (0-3)
- Each channel: volume (0-64), period (sample rate), length (in words), pointer (chip RAM)
- Maximum sample rate: ~28 kHz
- Minimum sample rate: ~20 Hz
- 8-bit signed PCM audio
- DMA-driven (requires chip RAM)
- Interrupt support on playback complete

### Hardware Registers

**Base Address:** `$DFF000` (custom chip base)

**Per-Channel Registers** (pattern for channel N):
```
AUDxLC   ($A0 + N*16)  Audio channel x location (pointer to sample data)
AUDxLEN  ($A4 + N*16)  Audio channel x length (in words, max 65535)
AUDxPER  ($A6 + N*16)  Audio channel x period (sample rate)
AUDxVOL  ($A8 + N*16)  Audio channel x volume (0-64)
AUDxDAT  ($AA + N*16)  Audio channel x data (for non-DMA mode)
```

**Global Audio Registers:**
```
ADKCONR  ($010)  Audio/disk control read
ADKCON   ($09E)  Audio/disk control write
```

**DMA Control Bits** (in DMACON register):
```
DMAEN    (bit 9)   Master DMA enable
AUD0EN   (bit 0)   Audio channel 0 DMA
AUD1EN   (bit 1)   Audio channel 1 DMA
AUD2EN   (bit 2)   Audio channel 2 DMA
AUD3EN   (bit 3)   Audio channel 3 DMA
```

**Audio Interrupt Bits** (in INTENA/INTREQ):
```
AUD0     (bit 7)   Audio channel 0 interrupt
AUD1     (bit 8)   Audio channel 1 interrupt
AUD2     (bit 9)   Audio channel 2 interrupt
AUD3     (bit 10)  Audio channel 3 interrupt
```

### Period Calculation

```
Period = 3579545 / Sample_Rate    (NTSC: 3579545 Hz)
Period = 3546895 / Sample_Rate    (PAL: 3546895 Hz)
```

**Examples:**
- 8000 Hz: period ≈ 447 (PAL)
- 11025 Hz: period ≈ 322
- 16000 Hz: period ≈ 222
- 22050 Hz: period ≈ 161

---

## Implementation Plan

### Phase 1: Grammar and Parsing

**Goal:** Add `paula` keyword and DSL syntax to grammar

**Files to Modify:**
1. `/Users/barry/RiderProjects/Novus/Novus.Core/Novus.g4`

**Grammar Changes:**
```antlr
// Add to primaryExpression rule (around line 392)
primaryExpression
    : /* ... existing rules ... */
    | paulaChannel                                 # PaulaExpr
    ;

// Paula DSL - hardware audio programming
// Example: paula channel(0) { sample: ptr, length: 1024, period: 322, volume: 64 }
paulaChannel
    : KW_PAULA KW_CHANNEL '(' expression ')' '{' NEWLINE* paulaField (',' NEWLINE* paulaField)* ','? NEWLINE* '}'
    ;

paulaField
    : IDENTIFIER ':' expression
    ;

// Add to lexer keywords (around line 480)
KW_PAULA    : 'paula';
KW_CHANNEL  : 'channel';
```

**Design Decisions:**
- Use field syntax like Blitter: `paula channel(N) { field: value, ... }`
- Channel number as parameter: `channel(0)` through `channel(3)`
- Fields: `sample`, `length`, `period`, `volume`, `loop` (optional)

---

### Phase 2: HIR Types and IrBuilder

**Goal:** Create HIR representation and visitor method

**Files to Modify:**
1. `/Users/barry/RiderProjects/Novus/Novus.Core/HIR/HirInstruction.cs`
2. `/Users/barry/RiderProjects/Novus/Novus.Core/Frontend/IrBuilder.Expressions.cs`

#### 2.1 Add HIR Types (HirInstruction.cs)

Insert after `HirBlitterJob` class (around line 330):

```csharp
/// <summary>
/// Represents a Paula audio channel setup (Amiga audio chip)
/// Paula has 4 independent 8-bit DMA audio channels
/// </summary>
public class HirPaulaChannel : HirInstruction
{
    /// <summary>
    /// DSL fields from the paula block (sample, length, period, volume, etc.)
    /// Keys are lowercased field names, values are the IR values.
    /// </summary>
    public Dictionary<string, IrValue> Fields { get; }

    /// <summary>
    /// Channel number (0-3)
    /// </summary>
    public IrValue ChannelNumber { get; set; }

    /// <summary>
    /// Create a paula channel setup from DSL fields
    /// </summary>
    public HirPaulaChannel(IrValue channelNumber, Dictionary<string, IrValue> fields)
    {
        ChannelNumber = channelNumber;
        Fields = fields;
    }

    public override List<IrInstruction> Lower()
    {
        // TODO: Implement Paula channel setup lowering
        //
        // Algorithm:
        // 1. Validate channel parameters
        //    - Channel number: 0-3
        //    - Sample pointer: must be in chip RAM
        //    - Length: 1-65535 words (sample length in words)
        //    - Period: 124-65535 (higher = lower sample rate)
        //    - Volume: 0-64
        //
        // 2. Compute register addresses for channel N:
        //    - AUDxLC  = $DFF0A0 + (N * 16)
        //    - AUDxLEN = $DFF0A4 + (N * 16)
        //    - AUDxPER = $DFF0A6 + (N * 16)
        //    - AUDxVOL = $DFF0A8 + (N * 16)
        //
        // 3. Generate register write sequence:
        //    a. Stop channel DMA (clear AUDxEN bit in DMACON)
        //    b. Wait for any pending DMA to complete
        //    c. Write AUDxLC (sample pointer)
        //    d. Write AUDxLEN (sample length in words)
        //    e. Write AUDxPER (period = clock / sample_rate)
        //    f. Write AUDxVOL (volume 0-64)
        //    g. Enable channel DMA (set AUDxEN bit in DMACON)
        //
        // 4. Optionally set up interrupt handler
        //
        // Example Paula DSL:
        //   paula channel(0) {
        //     sample: audio_data,
        //     length: 1024,
        //     period: 322,    // ~11025 Hz
        //     volume: 64
        //   }
        //
        // Lowered to register writes:
        //   ; Disable DMA for channel 0
        //   move.w #$0001,$dff096  ; DMACON = clear AUD0EN
        //   ; Wait for DMA
        //   .wait0: btst #0,$dff002 ; DMACONR bit 0
        //         bne.s .wait0
        //   ; Write registers
        //   move.l audio_data,$dff0a0  ; AUD0LC
        //   move.w #1024,$dff0a4       ; AUD0LEN
        //   move.w #322,$dff0a6        ; AUD0PER
        //   move.w #64,$dff0a8         ; AUD0VOL
        //   ; Enable DMA
        //   move.w #$8001,$dff096      ; DMACON = set AUD0EN

        throw new NotImplementedException("Paula channel setup lowering not yet implemented");
    }
}
```

#### 2.2 Add IrBuilder Visitor (IrBuilder.Expressions.cs)

Insert after `VisitBlitterExpr` (around line 5240):

```csharp
// ===========================
// Paula DSL Expression
// ===========================

// Paula audio register offsets
private const ushort AUD0LC  = 0x0A0;   // Audio channel 0 location (pointer)
private const ushort AUD0LEN = 0x0A4;   // Audio channel 0 length (words)
private const ushort AUD0PER = 0x0A6;   // Audio channel 0 period (sample rate)
private const ushort AUD0VOL = 0x0A8;   // Audio channel 0 volume (0-64)
private const ushort AUD1LC  = 0x0B0;   // Audio channel 1 location
private const ushort AUD1LEN = 0x0B4;   // Audio channel 1 length
private const ushort AUD1PER = 0x0B6;   // Audio channel 1 period
private const ushort AUD1VOL = 0x0B8;   // Audio channel 1 volume
private const ushort AUD2LC  = 0x0C0;   // Audio channel 2 location
private const ushort AUD2LEN = 0x0C4;   // Audio channel 2 length
private const ushort AUD2PER = 0x0C6;   // Audio channel 2 period
private const ushort AUD2VOL = 0x0C8;   // Audio channel 2 volume
private const ushort AUD3LC  = 0x0D0;   // Audio channel 3 location
private const ushort AUD3LEN = 0x0D4;   // Audio channel 3 length
private const ushort AUD3PER = 0x0D6;   // Audio channel 3 period
private const ushort AUD3VOL = 0x0D8;   // Audio channel 3 volume
private const ushort DMACON  = 0x096;   // DMA control write
private const ushort DMACONR = 0x002;   // DMA control read

/// <summary>
/// Handle Paula DSL expression: paula channel(0) { sample: ptr, length: 1024, period: 322, volume: 64 }
/// Generates direct hardware register writes for audio playback.
/// </summary>
public override object? VisitPaulaExpr([NotNull] NovusParser.PaulaExprContext context)
{
    var paulaChannel = context.paulaChannel();

    // Get channel number
    var channelNumExpr = (IrValue?)Visit(paulaChannel.expression());
    if (channelNumExpr == null)
    {
        var errorLocation = GetLocation(context);
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            "Paula channel number expression is invalid",
            errorLocation
        );
        return null;
    }

    // Parse fields
    var fields = new Dictionary<string, IrValue>();
    foreach (var fieldContext in paulaChannel.paulaField())
    {
        var fieldName = fieldContext.IDENTIFIER().GetText().ToLowerInvariant();
        var fieldValue = (IrValue?)Visit(fieldContext.expression());

        if (fieldValue == null)
        {
            var errorLocation = GetLocation(fieldContext);
            _diagnostics.ReportError(
                ErrorCodes.InvalidExpressionType,
                $"Paula field '{fieldName}' has invalid value",
                errorLocation
            );
            continue;
        }

        fields[fieldName] = fieldValue;
    }

    // Validate required fields
    if (!fields.ContainsKey("sample") || !fields.ContainsKey("length") ||
        !fields.ContainsKey("period") || !fields.ContainsKey("volume"))
    {
        var errorLocation = GetLocation(context);
        _diagnostics.ReportError(
            ErrorCodes.InvalidExpressionType,
            "Paula channel requires fields: sample, length, period, volume",
            errorLocation
        );
        return null;
    }

    // Create HIR instruction
    var hirPaula = new HirPaulaChannel(channelNumExpr, fields);
    _module.HirInstructions.Add(hirPaula);

    // Return unit value (paula channel setup doesn't return anything)
    return new IrTupleLiteral(IrTupleType.Unit, new List<IrValue>());
}
```

---

### Phase 3: Lowering Pass

**Goal:** Transform HIR Paula instructions to low-level register writes

**File to Create:** `/Users/barry/RiderProjects/Novus/Novus/Transforms/Passes/PaulaLoweringPass.cs`

```csharp
using Novus.IR;
using Novus.HIR;

namespace Novus.Transforms.Passes;

/// <summary>
/// Lowers HIR Paula audio channel setups to LIR (68k register access)
/// Paula is the Amiga audio chip with 4 independent DMA channels
/// </summary>
public class PaulaLoweringPass : TransformPassBase
{
    public override string Name => "Paula Lowering";

    // Custom chip register base
    private const uint CUSTOM_BASE = 0xDFF000;

    // DMA control bits
    private const ushort DMAF_SETCLR = 0x8000;  // Set/clear control bit
    private const ushort DMAF_AUD0   = 0x0001;  // Audio channel 0 DMA enable
    private const ushort DMAF_AUD1   = 0x0002;  // Audio channel 1 DMA enable
    private const ushort DMAF_AUD2   = 0x0004;  // Audio channel 2 DMA enable
    private const ushort DMAF_AUD3   = 0x0008;  // Audio channel 3 DMA enable

    public override bool Transform(IrModule module)
    {
        bool changed = false;

        // Find HIR instructions that need lowering
        foreach (var hirInstruction in module.HirInstructions.ToList())
        {
            if (hirInstruction is HirPaulaChannel paulaChannel)
            {
                // Lower this paula channel
                LowerPaulaChannel(module, paulaChannel);

                module.HirInstructions.Remove(hirInstruction);
                changed = true;
            }
        }

        return changed;
    }

    private void LowerPaulaChannel(IrModule module, HirPaulaChannel paula)
    {
        // Validate the channel
        ValidatePaulaChannel(paula);

        // Extract channel number
        if (!TryEvaluateConstant(paula.ChannelNumber, out int channelNum))
        {
            Console.WriteLine($"Warning: Paula channel number is not constant, runtime channel selection not yet implemented");
            return;
        }

        if (channelNum < 0 || channelNum > 3)
        {
            throw new InvalidOperationException($"Paula channel number {channelNum} out of range (0-3)");
        }

        // Compute register offsets for this channel
        ushort audLC  = (ushort)(0x0A0 + (channelNum * 16));  // AUDxLC
        ushort audLEN = (ushort)(0x0A4 + (channelNum * 16));  // AUDxLEN
        ushort audPER = (ushort)(0x0A6 + (channelNum * 16));  // AUDxPER
        ushort audVOL = (ushort)(0x0A8 + (channelNum * 16));  // AUDxVOL

        // Compute DMA enable bit
        ushort dmaEnableBit = (ushort)(1 << channelNum);  // DMAF_AUDx

        // Extract field values
        var samplePtr = paula.Fields["sample"];
        var length = paula.Fields["length"];
        var period = paula.Fields["period"];
        var volume = paula.Fields["volume"];

        // TODO: Generate actual register write instructions
        // This will require:
        // 1. Creating IrDereferenceStore instructions for each register
        // 2. Computing absolute addresses from offsets
        // 3. Generating DMA control sequence
        //
        // For now, this is a placeholder that demonstrates the structure
        Console.WriteLine($"Paula channel {channelNum} setup:");
        Console.WriteLine($"  AUDxLC  = ${audLC:X3}");
        Console.WriteLine($"  AUDxLEN = ${audLEN:X3}");
        Console.WriteLine($"  AUDxPER = ${audPER:X3}");
        Console.WriteLine($"  AUDxVOL = ${audVOL:X3}");
    }

    private void ValidatePaulaChannel(HirPaulaChannel paula)
    {
        // Validate channel number
        if (TryEvaluateConstant(paula.ChannelNumber, out int channel))
        {
            if (channel < 0 || channel > 3)
            {
                throw new InvalidOperationException($"Paula channel {channel} out of range (0-3)");
            }
        }

        // Validate length
        if (paula.Fields.TryGetValue("length", out var lengthVal) &&
            TryEvaluateConstant(lengthVal, out int length))
        {
            if (length < 1 || length > 65535)
            {
                throw new InvalidOperationException($"Paula sample length {length} out of range (1-65535 words)");
            }
        }

        // Validate period
        if (paula.Fields.TryGetValue("period", out var periodVal) &&
            TryEvaluateConstant(periodVal, out int period))
        {
            if (period < 124 || period > 65535)
            {
                Console.WriteLine($"Warning: Paula period {period} may be out of practical range (124-65535)");
            }
        }

        // Validate volume
        if (paula.Fields.TryGetValue("volume", out var volumeVal) &&
            TryEvaluateConstant(volumeVal, out int volume))
        {
            if (volume < 0 || volume > 64)
            {
                throw new InvalidOperationException($"Paula volume {volume} out of range (0-64)");
            }
        }

        // TODO: Validate sample pointer is in chip RAM
        // This requires type analysis or runtime check
    }

    /// <summary>
    /// Try to evaluate an IrValue to a compile-time constant integer
    /// </summary>
    private bool TryEvaluateConstant(IrValue value, out int result)
    {
        result = 0;

        switch (value)
        {
            case IrConstant constant:
                result = (int)constant.Value;
                return true;

            case IrVariable variable:
                // Could look up constant values from module.Constants
                // For now, treat variables as non-constant
                return false;

            case IrCastValue cast:
                // Recursively evaluate the cast source
                return TryEvaluateConstant(cast.Value, out result);

            default:
                return false;
        }
    }
}
```

---

### Phase 4: Semantic Validation

**Goal:** Compile-time validation of Paula DSL usage

**File to Create:** `/Users/barry/RiderProjects/Novus/Novus.Core/SemanticAnalysis/Validators/PaulaDslValidator.cs`

```csharp
using Novus.Diagnostics;
using Novus.Parser;

namespace Novus.SemanticAnalysis.Validators;

/// <summary>
/// Validates Paula DSL usage (audio chip programming)
/// Paula is the Amiga audio chip with 4 independent DMA channels
/// </summary>
public class PaulaDslValidator : ValidatorBase
{
    public override string Name => "Paula DSL Validator";

    public override bool Validate(NovusParser.CompilationUnitContext context, DiagnosticBag diagnostics)
    {
        // TODO: Implement Paula DSL validation
        //
        // Validation checks:
        // 1. Channel number is 0-3 (if constant)
        //
        // 2. Sample pointer
        //    - Must be in chip RAM (Paula requires DMA access)
        //    - Should be word-aligned for best performance
        //
        // 3. Sample length
        //    - Range: 1-65535 words (not bytes!)
        //    - Actual sample size = length * 2 bytes
        //
        // 4. Period value
        //    - Range: 124-65535 (hardware limits)
        //    - Lower period = higher sample rate
        //    - Should validate against PAL/NTSC clock
        //
        // 5. Volume value
        //    - Range: 0-64 (hardware limit)
        //    - 0 = silent, 64 = maximum
        //
        // 6. Loop flag (optional)
        //    - Boolean: true = one-shot, false = continuous
        //
        // Example errors to catch:
        //
        // Invalid channel:
        //   paula channel(5) { ... }  // Error: Channel 5 doesn't exist (max 3)
        //
        // Invalid volume:
        //   paula channel(0) { volume: 100 }  // Error: Volume max is 64
        //
        // Invalid length:
        //   paula channel(0) { length: 0 }  // Error: Minimum length is 1 word
        //
        // Fast RAM pointer:
        //   paula channel(0) { sample: fast_ram_buffer }  // Error: Paula requires chip RAM
        //
        // Invalid period (too low):
        //   paula channel(0) { period: 50 }  // Error: Period min is 124

        // For now, no validation (skeleton)
        return true;
    }

    /// <summary>
    /// Calculate sample rate from period value
    /// </summary>
    private int PeriodToSampleRate(int period, bool isPAL = true)
    {
        int clock = isPAL ? 3546895 : 3579545;  // PAL vs NTSC clock
        return clock / period;
    }

    /// <summary>
    /// Calculate period from sample rate
    /// </summary>
    private int SampleRateToPeriod(int sampleRate, bool isPAL = true)
    {
        int clock = isPAL ? 3546895 : 3579545;  // PAL vs NTSC clock
        return clock / sampleRate;
    }

    /// <summary>
    /// Common sample rates and their period values (PAL)
    /// </summary>
    private static readonly Dictionary<int, int> CommonSampleRates = new()
    {
        { 8000, 443 },    // 8 kHz
        { 11025, 322 },   // 11.025 kHz (CD quality / 4)
        { 16000, 222 },   // 16 kHz
        { 22050, 161 },   // 22.05 kHz (CD quality / 2)
    };
}
```

---

### Phase 5: Hardware Register Definitions

**Goal:** Add Paula register constants

**File to Modify:** `/Users/barry/RiderProjects/Novus/Novus/std/hardware/registers.novus`

Add after Blitter registers (around line 140):

```novus
//
// Paula Audio Registers
//

// Audio channel 0
pub const AUD0LC: u32 = $0A0   // Audio channel 0 location (pointer to sample data)
pub const AUD0LEN: u32 = $0A4  // Audio channel 0 length (sample length in words)
pub const AUD0PER: u32 = $0A6  // Audio channel 0 period (sample rate = clock / period)
pub const AUD0VOL: u32 = $0A8  // Audio channel 0 volume (0-64)
pub const AUD0DAT: u32 = $0AA  // Audio channel 0 data (for non-DMA writes)

// Audio channel 1
pub const AUD1LC: u32 = $0B0   // Audio channel 1 location
pub const AUD1LEN: u32 = $0B4  // Audio channel 1 length
pub const AUD1PER: u32 = $0B6  // Audio channel 1 period
pub const AUD1VOL: u32 = $0B8  // Audio channel 1 volume
pub const AUD1DAT: u32 = $0BA  // Audio channel 1 data

// Audio channel 2
pub const AUD2LC: u32 = $0C0   // Audio channel 2 location
pub const AUD2LEN: u32 = $0C4  // Audio channel 2 length
pub const AUD2PER: u32 = $0C6  // Audio channel 2 period
pub const AUD2VOL: u32 = $0C8  // Audio channel 2 volume
pub const AUD2DAT: u32 = $0CA  // Audio channel 2 data

// Audio channel 3
pub const AUD3LC: u32 = $0D0   // Audio channel 3 location
pub const AUD3LEN: u32 = $0D4  // Audio channel 3 length
pub const AUD3PER: u32 = $0D6  // Audio channel 3 period
pub const AUD3VOL: u32 = $0D8  // Audio channel 3 volume
pub const AUD3DAT: u32 = $0DA  // Audio channel 3 data

// Audio/Disk Control
pub const ADKCONR: u32 = $010  // Audio/disk control read
pub const ADKCON: u32 = $09E   // Audio/disk control write

//
// Audio Helper Functions
//

/// Calculate period value from sample rate
/// Uses PAL clock (3546895 Hz) by default
pub fn sample_rate_to_period(sample_rate: u32) -> u16 {
    const PAL_CLOCK: u32 = 3546895
    let period: u32 = PAL_CLOCK / sample_rate
    return (u16)period
}

/// Calculate sample rate from period value
/// Uses PAL clock (3546895 Hz) by default
pub fn period_to_sample_rate(period: u16) -> u32 {
    const PAL_CLOCK: u32 = 3546895
    return PAL_CLOCK / ((u32)period)
}

/// Get audio channel register offset for channel number (0-3)
pub fn audio_channel_base(channel: u8) -> u32 {
    if channel > 3 {
        panic("Audio channel must be 0-3")
    }
    return $0A0 + (((u32)channel) * 16)
}

/// Get DMA enable bit for audio channel (0-3)
pub fn audio_dma_bit(channel: u8) -> u16 {
    if channel > 3 {
        panic("Audio channel must be 0-3")
    }
    return 1 << ((u16)channel)
}
```

---

### Phase 6: Standard Library Wrapper

**Goal:** Safe, high-level Paula audio API

**File to Create:** `/Users/barry/RiderProjects/Novus/Novus/std/audio/paula.novus`

```novus
// Paula Audio API
// Module: amiga::sys::hardware::audio
//
// Safe wrapper around Paula audio hardware for playing 8-bit samples.
// Paula has 4 independent DMA channels supporting 8-bit signed PCM audio.

use amiga::sys::hardware::registers::{
    AUD0LC, AUD1LC, AUD2LC, AUD3LC,
    AUD0LEN, AUD1LEN, AUD2LEN, AUD3LEN,
    AUD0PER, AUD1PER, AUD2PER, AUD3PER,
    AUD0VOL, AUD1VOL, AUD2VOL, AUD3VOL,
    DMACON, DMACONR,
    audio_channel_base, audio_dma_bit,
    sample_rate_to_period
}

/// Paula audio channel (0-3)
pub enum Channel {
    Channel0,
    Channel1,
    Channel2,
    Channel3
}

impl Channel {
    pub fn to_u8(self) -> u8 {
        return match self {
            Channel::Channel0 => 0,
            Channel::Channel1 => 1,
            Channel::Channel2 => 2,
            Channel::Channel3 => 3
        }
    }
}

/// Audio sample in chip RAM
pub struct AudioSample {
    /// Pointer to 8-bit signed PCM data (must be in chip RAM)
    data: *u8,

    /// Length of sample in words (bytes / 2)
    length_words: u16,

    /// Sample rate in Hz
    sample_rate: u32
}

impl AudioSample {
    /// Create a new audio sample from chip RAM data
    ///
    /// # Safety
    /// - `data` must point to valid chip RAM
    /// - `length_bytes` must be accurate
    /// - Data must remain valid for lifetime of sample
    pub fn new(data: *u8, length_bytes: u32, sample_rate: u32) -> AudioSample {
        return AudioSample {
            data: data,
            length_words: (u16)(length_bytes / 2),
            sample_rate: sample_rate
        }
    }

    /// Get period value for this sample's rate
    pub fn period(&self) -> u16 {
        return sample_rate_to_period(self.sample_rate)
    }
}

/// Play an audio sample on a Paula channel
///
/// # Arguments
/// - `channel`: Audio channel (0-3)
/// - `sample`: Sample to play (must be in chip RAM)
/// - `volume`: Volume level (0-64, where 64 is maximum)
///
/// # Safety
/// This function writes to hardware registers and requires `unsafe` block.
pub fn play_sample(channel: Channel, sample: &AudioSample, volume: u8) {
    let channel_num: u8 = channel.to_u8()
    let volume_clamped: u8 = if volume > 64 { 64 } else { volume }

    unsafe {
        // Use Paula DSL for hardware access
        paula channel((u32)channel_num) {
            sample: sample.data,
            length: (u32)sample.length_words,
            period: (u32)sample.period(),
            volume: (u32)volume_clamped
        }
    }
}

/// Stop audio playback on a channel
///
/// # Safety
/// This function writes to hardware registers and requires `unsafe` block.
pub fn stop_channel(channel: Channel) {
    let channel_num: u8 = channel.to_u8()
    let dma_bit: u16 = audio_dma_bit(channel_num)

    unsafe {
        // Clear DMA enable bit for this channel
        extern var DMACON_REG: u16 at $DFF096
        DMACON_REG = dma_bit  // Write without SETCLR bit clears
    }
}

/// Set volume for a channel (affects currently playing sound)
///
/// # Safety
/// This function writes to hardware registers and requires `unsafe` block.
pub fn set_volume(channel: Channel, volume: u8) {
    let channel_num: u8 = channel.to_u8()
    let volume_clamped: u8 = if volume > 64 { 64 } else { volume }
    let reg_offset: u32 = audio_channel_base(channel_num) + 8  // AUDxVOL offset

    unsafe {
        // Direct register write using extern var
        // TODO: Use generated per-channel constants instead
        extern var VOL_REG: u16 at ($DFF000 + reg_offset)
        VOL_REG = (u16)volume_clamped
    }
}

/// Common sample rates for convenience
pub const SAMPLE_RATE_8KHZ: u32 = 8000
pub const SAMPLE_RATE_11KHZ: u32 = 11025
pub const SAMPLE_RATE_16KHZ: u32 = 16000
pub const SAMPLE_RATE_22KHZ: u32 = 22050
```

---

## File-by-File Changes

### Summary of Files to Modify

| File Path | Changes | Lines | Complexity |
|-----------|---------|-------|------------|
| `Novus.Core/Novus.g4` | Add `paula` and `channel` keywords, `paulaChannel` and `paulaField` rules | ~20 | Low |
| `Novus.Core/HIR/HirInstruction.cs` | Add `HirPaulaChannel` class | ~80 | Medium |
| `Novus.Core/Frontend/IrBuilder.Expressions.cs` | Add `VisitPaulaExpr` method, register constants | ~100 | Medium |
| `Novus/Transforms/Passes/PaulaLoweringPass.cs` | **NEW FILE** - Implement lowering pass | ~250 | High |
| `Novus.Core/SemanticAnalysis/Validators/PaulaDslValidator.cs` | **NEW FILE** - Validation logic | ~100 | Low |
| `Novus/std/hardware/registers.novus` | Add Paula register constants and helpers | ~60 | Low |
| `Novus/std/audio/paula.novus` | **NEW FILE** - High-level audio API | ~150 | Medium |
| `Novus.Tests/PaulaDslTests.cs` | **NEW FILE** - Unit tests | ~300 | Medium |

**Total Estimated Lines:** ~1060
**Estimated Effort:** 2-3 days

---

## Testing Strategy

### Unit Tests

**File to Create:** `/Users/barry/RiderProjects/Novus/Novus.Tests/PaulaDslTests.cs`

```csharp
using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.HIR;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Novus.Transforms;
using Novus.Transforms.Passes;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for Paula audio DSL parsing, semantic analysis, and lowering.
/// Paula is the Amiga audio chip with 4 DMA channels.
/// </summary>
public class PaulaDslTests
{
    private IrModule BuildIR(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    [Fact]
    public void PaulaDsl_SimplePlayback_Parses()
    {
        var source = @"
fn test(sample_data: *u8) {
    unsafe {
        paula channel(0) {
            sample: sample_data,
            length: 1024,
            period: 322,
            volume: 64
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        Assert.Single(module.Functions);
        Assert.Single(module.HirInstructions);
        Assert.IsType<HirPaulaChannel>(module.HirInstructions[0]);
    }

    [Fact]
    public void PaulaDsl_AllChannels_Parse()
    {
        var source = @"
fn test(data: *u8) {
    unsafe {
        paula channel(0) { sample: data, length: 100, period: 322, volume: 64 }
        paula channel(1) { sample: data, length: 100, period: 322, volume: 32 }
        paula channel(2) { sample: data, length: 100, period: 161, volume: 48 }
        paula channel(3) { sample: data, length: 100, period: 443, volume: 16 }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        Assert.Equal(4, module.HirInstructions.Count);
    }

    [Fact]
    public void PaulaDsl_VariableChannelNumber_Parses()
    {
        var source = @"
fn test(chan: u8, data: *u8) {
    unsafe {
        paula channel((u32)chan) {
            sample: data,
            length: 1024,
            period: 322,
            volume: 64
        }
    }
}";
        var module = BuildIR(source);
        Assert.NotNull(module);
        Assert.Single(module.HirInstructions);
    }

    [Fact]
    public void PaulaDsl_LoweringPass_ValidatesChannel()
    {
        var source = @"
fn test(data: *u8) {
    unsafe {
        paula channel(0) {
            sample: data,
            length: 1024,
            period: 322,
            volume: 64
        }
    }
}";
        var module = BuildIR(source);

        // Run lowering pass
        var pass = new PaulaLoweringPass();
        var changed = pass.Transform(module);

        Assert.True(changed);
        Assert.Empty(module.HirInstructions);  // Should be lowered
    }

    [Fact]
    public void PaulaDsl_RequiresUnsafeBlock()
    {
        var source = @"
fn test(data: *u8) {
    paula channel(0) {
        sample: data,
        length: 1024,
        period: 322,
        volume: 64
    }
}";
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);

        // Should report error about requiring unsafe
        Assert.True(analyzer.Diagnostics.HasErrors);
    }
}
```

### Integration Tests

**Example Files to Create:**

1. **Simple playback test:**
   `/Users/barry/RiderProjects/Novus/Novus.Tests/Examples/paula_simple_beep.novus`

2. **Multi-channel test:**
   `/Users/barry/RiderProjects/Novus/Novus.Tests/Examples/paula_music.novus`

3. **High-level API test:**
   `/Users/barry/RiderProjects/Novus/Novus.Tests/Examples/paula_wrapper_test.novus`

---

## Example Usage

### Low-Level DSL Example

```novus
// Direct hardware access with Paula DSL
use std::memory::allocate_chip

fn play_beep() {
    // Generate 440 Hz square wave (A4 note)
    let sample_size: u32 = 256
    let sample: *u8 = allocate_chip(sample_size)

    // Fill sample with square wave
    let mut i: u32 = 0
    while i < sample_size {
        unsafe {
            if i < sample_size / 2 {
                *sample.offset(i as i32) = 127  // High
            } else {
                *sample.offset(i as i32) = -127  // Low (signed)
            }
        }
        i = i + 1
    }

    // Play on channel 0
    unsafe {
        paula channel(0) {
            sample: sample,
            length: sample_size / 2,  // Length in words
            period: 322,              // ~11025 Hz sample rate
            volume: 64                // Maximum volume
        }
    }
}
```

### High-Level API Example

```novus
// Safe wrapper API
use amiga::sys::hardware::audio::{Channel, AudioSample, play_sample, SAMPLE_RATE_11KHZ}
use std::memory::allocate_chip

fn play_sound_effect() {
    // Load or generate sample data in chip RAM
    let sample_data: *u8 = load_explosion_sound()
    let sample_length: u32 = 2048

    // Create audio sample
    let explosion: AudioSample = AudioSample::new(
        sample_data,
        sample_length,
        SAMPLE_RATE_11KHZ
    )

    // Play on channel 2 at 50% volume
    unsafe {
        play_sample(Channel::Channel2, &explosion, 32)
    }
}

fn load_explosion_sound() -> *u8 {
    // TODO: Load from disk or embed in binary
    return allocate_chip(2048)
}
```

---

## Next Steps

### Phase 1 (Immediate)
1. Add grammar rules for `paula channel` syntax
2. Create HIR types (`HirPaulaChannel`)
3. Implement IrBuilder visitor
4. Write basic parsing tests

### Phase 2 (Core Implementation)
5. Implement `PaulaLoweringPass`
6. Add hardware register definitions
7. Create semantic validator
8. Write lowering tests

### Phase 3 (Polish)
9. Create high-level wrapper API (`amiga::sys::hardware::audio`)
10. Write integration tests and examples
11. Add documentation and code comments
12. Performance testing on real hardware

### Phase 4 (Advanced Features)
13. Interrupt-driven playback completion
14. Sample looping support
15. Audio mixer for multiple sounds
16. MOD/tracker file format support

---

## Additional Resources

### Amiga Hardware Reference

- **Amiga Hardware Reference Manual** (chapters on Paula)
- **Hardware register map:** `$DFF000-$DFF1FF`
- **DMA channels:** 4 audio + blitter + copper + disk + sprites

### Existing Patterns to Study

- **Copper DSL:** `/Users/barry/RiderProjects/Novus/Novus.Core/HIR/HirInstruction.cs` (lines 31-205)
- **Blitter DSL:** `/Users/barry/RiderProjects/Novus/Novus.Core/HIR/HirInstruction.cs` (lines 207-331)
- **Test examples:** `/Users/barry/RiderProjects/Novus/Novus.Tests/CopperBlitterDslTests.cs`

### Community Resources

- **Amiga Dev Forum:** https://eab.abime.net/forumdisplay.php?f=112
- **Paula Audio Guide:** http://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node00D9.html

---

## Conclusion

This guide provides a complete roadmap for adding Paula audio support to the Novus compiler. The implementation follows proven patterns from existing hardware DSLs (Copper and Blitter) and integrates cleanly with the compiler's HIR/LIR architecture.

Key advantages of this design:

1. **Type-safe hardware access** - Compile-time validation prevents many bugs
2. **Clean separation** - DSL syntax → HIR → lowering → codegen
3. **Flexible abstraction** - Both low-level DSL and high-level API
4. **Consistent patterns** - Follows Copper/Blitter architecture
5. **68k-optimized** - Generates efficient register writes

The phased approach allows incremental development and testing, starting with basic parsing and building up to a complete audio system.
