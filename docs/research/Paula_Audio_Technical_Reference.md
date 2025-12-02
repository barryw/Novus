# Paula Audio Hardware - Technical Reference for Novus API Design

## Executive Summary

Paula is the Amiga's audio and I/O chip, providing four independent 8-bit DMA-driven PCM audio channels with hardware mixing, stereo output, and advanced modulation capabilities. This document provides comprehensive technical details to inform the design of Novus's audio API.

## 1. Paula Chip Architecture

### Overview
- **Designer**: Glenn Keller (MOS Technology)
- **Name Origin**: "Ports, Audio, UART and Logic" (contrived acronym)
- **Revision History**: Never revised - functionally identical across all Amiga models from Commodore
- **Functions**: Audio playback, interrupt controller, floppy disk control, serial I/O, joystick/mouse input

### Audio Capabilities
- **Channels**: 4 independent DMA-driven 8-bit PCM channels
- **Stereo Mixing**: Channels 0 & 3 → Left, Channels 1 & 2 → Right
- **Sample Format**: Signed linear 8-bit two's complement (only supported hardware format)
- **Volume Range**: 0-64 (65 levels: 0 = silence, 64 = maximum)
- **Frequency Range**: ~20 samples/second to ~29,000 samples/second
- **DMA Integration**: Requires DMA controller (Agnus/Alice chip)

### State Machine Implementation
Internally, each audio channel is implemented as a state machine with 8 different states. The hardware also allows channel pairing for amplitude or period modulation.

## 2. Hardware Registers and Memory Map

### Custom Chip Base Address
- **Base Address**: `$DFF000` (custom chip register base)
- **CPU Access Formula**: `680x0_address = chip_offset + $DFF000`
- **Example**: ADKCON register at offset `$09E` → CPU address `$DFF09E`

### Audio Channel Registers (Per Channel)

Each of the 4 channels has identical register layout at offsets:
- Channel 0: `$DFF0A0` - `$DFF0AA`
- Channel 1: `$DFF0B0` - `$DFF0BA`
- Channel 2: `$DFF0C0` - `$DFF0CA`
- Channel 3: `$DFF0D0` - `$DFF0DA`

#### Register Definitions (Channel x = 0-3)

| Register | Offset | Access | Description |
|----------|--------|--------|-------------|
| AUDxLCH  | +$00   | W      | Audio location (high 5 bits of 20-bit address) |
| AUDxLCL  | +$02   | W      | Audio location (low 15 bits of 20-bit address) |
| AUDxLEN  | +$04   | W      | Audio length in words (16-bit words) |
| AUDxPER  | +$06   | W      | Audio period (sample rate divisor, 16-bit) |
| AUDxVOL  | +$08   | W      | Audio volume (0-64, only low 7 bits used) |
| AUDxDAT  | +$0A   | W      | Audio data (for manual/direct output) |

**Important Notes:**
- **AUDxLCH/AUDxLCL**: Combined 20-bit sample buffer starting address. Not a pointer register - only reload when changing memory location.
- **AUDxLEN**: Length in WORDS (2 bytes each), not bytes. Maximum = 131,070 bytes (65,535 words × 2).
- **AUDxPER**: Period value (clock divisor). Valid range: 123-65,535. Lower = faster playback.
- **AUDxVOL**: Volume levels 0-63 use DAC approximation mode; level 64 disables resampling for exact sample rate.
- **AUDxDAT**: Used for direct (non-DMA) audio output, rarely used in practice.

### Global Control Registers

| Register | Address  | Access | Description |
|----------|----------|--------|-------------|
| DMACON   | $DFF096  | W      | DMA control write (set or clear bits) |
| DMACONR  | $DFF002  | R      | DMA control and blitter status read |
| ADKCON   | $DFF09E  | W      | Audio/disk control write |
| ADKCONR  | $DFF010  | R      | Audio/disk control read |
| INTENA   | $DFF09A  | W      | Interrupt enable write |
| INTENAR  | $DFF01C  | R      | Interrupt enable read |
| INTREQ   | $DFF09C  | W      | Interrupt request write (clear/set) |
| INTREQR  | $DFF01E  | R      | Interrupt request read |

### DMACON Bit Layout

| Bit | Name   | Description |
|-----|--------|-------------|
| 15  | SET/CLR| Set (1) or clear (0) control bit |
| 14  | BBUSY  | Blitter busy status (read-only) |
| 13  | BZERO  | Blitter zero output status (read-only) |
| 12-11 | -    | Unassigned |
| 10  | BLTPRI | Blitter priority (blitter-nasty mode) |
| 9   | DMAEN  | Enable all DMA below (master bit) |
| 8   | BPLEN  | Bitplane DMA enable |
| 7   | COPEN  | Copper DMA enable |
| 6   | BLTEN  | Blitter DMA enable |
| 5   | SPREN  | Sprite DMA enable |
| 4   | DSKEN  | Disk DMA enable |
| 3   | AUD3EN | Audio channel 3 DMA enable |
| 2   | AUD2EN | Audio channel 2 DMA enable |
| 1   | AUD1EN | Audio channel 1 DMA enable |
| 0   | AUD0EN | Audio channel 0 DMA enable |

**Usage Pattern for Setting/Clearing:**
- To **set** bits: Write with bit 15 = 1 and desired bits = 1
- To **clear** bits: Write with bit 15 = 0 and desired bits = 1
- Example: `DMACON = $8001` enables audio channel 0 DMA
- Example: `DMACON = $0001` disables audio channel 0 DMA

### ADKCON Bit Layout

| Bit | Name    | Description |
|-----|---------|-------------|
| 15  | SET/CLR | Set (1) or clear (0) control bit |
| 14-11 | -     | Unassigned |
| 10  | PRECOMP1| Disk precompensation bit 1 |
| 9   | PRECOMP0| Disk precompensation bit 0 |
| 8   | MFMPREC | MFM disk encode/decode |
| 7   | UARTBRK | UART break |
| 6   | WORDSYNC| Disk word sync enable |
| 5   | MSBSYNC | Disk sync on MSB |
| 4   | FAST    | Fast disk mode |
| 3   | USE3PN  | Audio channel 3 period modulation by channel 2 |
| 2   | USE2PN  | Audio channel 2 period modulation by channel 1 |
| 1   | USE1VN  | Audio channel 1 amplitude modulation by channel 0 |
| 0   | USE0VN  | Audio channel 0 amplitude modulation by channel 3 |

**Modulation Bits:**
- Bits 0-3 control channel attachment for modulation effects
- When set, the modulating channel is silenced and its data modulates the target channel
- See Section 7 for detailed modulation behavior

## 3. DMA-Driven Sample Playback

### DMA Architecture
- **DMA Slots**: One DMA slot per scan line per channel
- **Maximum Rate**: Limited by horizontal scan line timing
  - **PAL**: 28,837 samples/second per channel (57,674/sec total stereo)
  - **NTSC**: 28,867 samples/second per channel (57,734/sec total stereo)
- **Sample Access**: 2 data samples retrieved per horizontal scan line per channel
- **Theoretical Maximum**: 31,469 samples/second (NTSC calculation: 2 samples/line × 262.5 lines/frame × 59.94 fps)
- **Hardware Design Limit**: 28,867 samples/second (to conserve buffer memory)

### DMA Playback Process

1. **Setup Phase:**
   - Write sample buffer address to AUDxLCH/AUDxLCL (20-bit address)
   - Write sample length to AUDxLEN (in words, not bytes)
   - Write period (sample rate divisor) to AUDxPER
   - Write volume (0-64) to AUDxVOL
   - Enable channel DMA in DMACON (set AUDxEN bit)

2. **Playback Phase:**
   - DMA fetches samples automatically from memory
   - Samples converted to analog voltage via DAC
   - Playback continues until length exhausted
   - **Default Behavior**: All channels loop automatically

3. **Interrupt Phase:**
   - Interrupt fires when DMA reads location/length registers
   - Interrupt occurs AFTER values stored in backup registers
   - At this point, registers can be rewritten for next segment
   - **Level 4 Interrupt**: Signals "audio block done" when last word accessed in auto mode

### Memory Requirements
- **Maximum Single Sample**: 128KB (65,535 words × 2 bytes)
- **Sample Buffer Alignment**: No special alignment required for audio DMA
- **Chip RAM Requirement**: Sample data must reside in Chip RAM (DMA accessible)

## 4. Audio Interrupts and Double-Buffering

### Interrupt Timing
**Critical Timing Detail**: Interrupts occur immediately AFTER the audio DMA channel has read the location and length registers and stored their values in backup registers. This means:
- You can safely rewrite AUDxLCH/AUDxLCL/AUDxLEN in the interrupt handler
- The new values will be used for the NEXT playback cycle
- The current playback uses the backup register values

### Level 4 Audio Interrupt
- **Trigger**: Fires when last word in audio data stream accessed (in automatic mode)
- **Purpose**: Signals "audio block done"
- **Use Case**: Double-buffering, streaming audio, effect chaining

### Double-Buffering Pattern

The canonical approach for smooth long-form playback:

1. **Allocate two buffers** (Buffer A, Buffer B) in Chip RAM
2. **Start playback** of Buffer A
3. **Wait for interrupt** signaling Buffer A complete
4. **In interrupt handler:**
   - Queue Buffer B for playback (write to AUDxLCH/AUDxLCL/AUDxLEN)
   - Start filling Buffer A with new data in background
5. **Wait for next interrupt** (Buffer B complete)
6. **Repeat**: Queue Buffer A, fill Buffer B
7. **Continue alternating** buffers

**Benefits:**
- Smooth playback without gaps
- Overlapped computation and playback
- Supports streaming from disk or synthesis

### IORequest Queueing (audio.device)
When using audio.device rather than direct hardware access:
- Queue multiple IORequests for seamless transitions
- Device handles interrupt servicing and buffer switching
- Allows runtime control of volume, balance, panning
- Proper OS-friendly channel allocation/arbitration

## 5. Volume Control

### Volume Register (AUDxVOL)
- **Bit Width**: 7 bits (only low 7 bits used, bit 6-0)
- **Range**: 0-64 (65 distinct levels)
  - **0**: Complete silence
  - **1-63**: Progressively louder
  - **64**: Maximum volume
- **DAC Behavior**:
  - **Levels 0-63**: DAC operates in periodic approximation mode
  - **Level 64**: Exact sample rate maintained (no DAC resampling)

### DAC Approximation Mode (Volumes 0-63)
Paula uses a clever pulse-width modulation scheme to achieve 65 volume levels with an 8-bit DAC:
- DAC works in a raster of pulse sequences
- Pulse raster period: 64 ticks apart
- Volume value determines how many cycles output is forced to 0
- Formula: For volume V, output is 0 for (64 - V) cycles per 64-tick window
- This achieves relative signal amplitude without true 7-bit DAC

### Volume 64 Special Case
- **Purpose**: Disable DAC approximation raster
- **Effect**: Actual sample rate maintained without resampling
- **Use Case**: When precise timing matters more than fine volume control
- **Trade-off**: No volume attenuation possible at this level

## 6. Period Calculation and Sample Rates

### System Clock Constants
- **PAL Master Clock**: 14.18758 MHz
- **NTSC Master Clock**: 14.31818 MHz
- **Paula Clock (PAL)**: 3.546895 MHz (color carrier × 4/5)
- **Paula Clock (NTSC)**: 3.579545 MHz (color carrier × 4/5)

### Period Formula
The period register (AUDxPER) acts as a clock divisor:

```
Sample Rate = Paula Clock Frequency / Period Value
Period Value = Paula Clock Frequency / Desired Sample Rate
```

**PAL Examples:**
```
Period = 3,546,895 / Sample_Rate

Sample Rate  | Period Value
-------------|-------------
28,837 Hz    | 123 (minimum legal value)
14,000 Hz    | 253
8,000 Hz     | 443
```

**NTSC Examples:**
```
Period = 3,579,545 / Sample_Rate

Sample Rate  | Period Value
-------------|-------------
28,867 Hz    | 124
14,000 Hz    | 256
8,000 Hz     | 447
```

### Period Register Constraints
- **Bit Width**: 16 bits
- **Valid Range**: 123 to 65,535 cycles
- **Minimum Period**: 123 (hardware DMA throughput limit)
- **Recommended Range**: 124-256 for best quality
  - Corresponds to 14-28 kHz sample rates
  - Makes effective use of 7 kHz low-pass filter to prevent aliasing noise
- **Lower Limit Rationale**: 123 cycles = minimum time for DMA to stream sample data from memory

### Anti-Aliasing Considerations
Paula includes a 7 kHz cutoff low-pass filter to prevent aliasing distortion. To use this filter effectively:
- Keep sample rates in 14-28 kHz range (periods 124-256)
- Higher sample rates waste bandwidth without quality improvement
- Lower sample rates produce audible aliasing artifacts

### Timing Calculations
- **System Timing Interval**: 279.365 nanoseconds (0.279365 microseconds)
- **Maximum Sample Rate Period**: 34.642 microseconds per sample (28,867 Hz NTSC)
- **PAL vs NTSC**: Always detect system type and use appropriate clock constant

## 7. Modulation Features (Advanced)

### Channel Pairing for Modulation
Paula allows one channel to modulate another channel's amplitude or period. This is controlled via ADKCON register bits 0-3.

#### Modulation Pairs
- **Channel 0 ← modulated by Channel 3** (ADKCON bit 0: USE0VN for amplitude)
- **Channel 1 ← modulated by Channel 0** (ADKCON bit 1: USE1VN for amplitude)
- **Channel 2 ← modulated by Channel 1** (ADKCON bit 2: USE2PN for period)
- **Channel 3 ← modulated by Channel 2** (ADKCON bit 3: USE3PN for period)

### How Attachment Works
When an "attach" bit is set in ADKCON:
1. **Modulator channel is silenced** (ceases audio output)
2. **Data is reinterpreted** as modulation parameters:
   - **Amplitude Modulation**: 8-bit samples become volume values
   - **Period Modulation**: Two consecutive 8-bit samples form 16-bit period value (big-endian)
3. **Modulated channel updates** occur each time modulator's period register times out
4. **Target channel parameters change** dynamically based on modulator data

### Amplitude Modulation (Volume)
- **Mechanism**: Modulator data written to target channel's volume register
- **Data Format**: Only second sample significant (7-bit volume: 0-64)
- **Effect**: Dynamic volume changes (tremolo, vibrato, envelope)
- **Use Cases**: Tremolo effects, amplitude envelopes, dynamic mixing

### Period Modulation (Frequency)
- **Mechanism**: Modulator data written to target channel's period register
- **Data Format**: Two 8-bit samples → 16-bit big-endian period value
  - First sample: High byte
  - Second sample: Low byte
- **Effect**: Dynamic pitch changes (frequency modulation, vibrato)
- **Use Cases**: Vibrato, pitch sweeps, FM-like effects

### Practical Usage Notes
- **Rarely Used**: Modulation features are poorly documented and complex
- **Limited Applications**: Mostly experimental or novelty effects
- **Performance Cost**: Ties up two channels to modulate one
- **Alternative Approaches**: Software modulation via buffer manipulation often simpler
- **Documentation**: Sparse official docs make this feature difficult to use reliably

### Example Scenario
To create tremolo on Channel 1 using Channel 0 as modulator:
1. Set ADKCON bit 1 (USE1VN) to enable amplitude modulation
2. Channel 0 data becomes volume values for Channel 1
3. Channel 0 plays low-frequency waveform (e.g., sine wave)
4. Channel 1's volume oscillates, creating tremolo effect
5. Channel 0 produces no audio output (silenced)

## 8. Common Playback Patterns

### One-Shot Playback
**Use Case**: Sound effects, single samples

**Method 1: Let it finish naturally**
```
1. Write sample location/length/period/volume
2. Enable DMA
3. Wait for interrupt (audio block done)
4. Disable DMA (optional cleanup)
```

**Method 2: Explicit termination**
```
1. Write sample location/length/period/volume
2. Enable DMA
3. When playback should stop: Clear AUDxEN in DMACON
```

### Looping Playback
**Use Case**: Background music, ambient sounds

**Default Behavior**: All channels loop automatically by default

**Method 1: Infinite loop (hardware default)**
```
1. Write sample location/length/period/volume
2. Enable DMA
3. Hardware loops automatically
4. To stop: Clear AUDxEN in DMACON
```

**Method 2: Managed loop points**
```
1. Start playing sample
2. Wait for interrupt (end of sample)
3. In interrupt handler: Rewrite AUDxLCH/AUDxLCL for loop point
4. Optionally adjust AUDxLEN for loop length
5. Repeat
```

### Streaming Playback
**Use Case**: Long music files, speech, dynamic synthesis

**Double-Buffer Streaming Pattern:**
```
1. Allocate Buffer A and Buffer B in Chip RAM
2. Fill Buffer A with initial data
3. Start DMA playback of Buffer A
4. Start filling Buffer B in background
5. Wait for interrupt (Buffer A complete)
6. In interrupt handler:
   - Write Buffer B location/length to AUDxLCH/AUDxLCL/AUDxLEN
   - Signal background task to fill Buffer A
7. Wait for next interrupt (Buffer B complete)
8. In interrupt handler:
   - Write Buffer A location/length
   - Signal background task to fill Buffer B
9. Repeat steps 5-8 indefinitely
```

**Buffer Size Considerations:**
- Smaller buffers: Lower latency, more CPU overhead
- Larger buffers: Higher latency, less CPU overhead
- Typical sizes: 4KB-16KB per buffer
- Must fit in Chip RAM and allow time to fill between interrupts

### Synchronized Multi-Channel Playback
**Use Case**: Stereo samples, multi-track music

**Method 1: Simultaneous DMA enable**
```
1. Set up all channels (location/length/period/volume)
2. Enable all channel DMAs simultaneously with single DMACON write
   Example: DMACON = $800F (enable channels 0-3)
3. Channels start in sync
```

**Method 2: Copper-based sync**
```
1. Set up all channels
2. Use Copper list to enable DMAs at exact scanline
3. Guarantees sample-accurate synchronization
```

### Dynamic Effects (Volume/Pitch Changes)
**Use Case**: Fade in/out, pitch bends, dynamic mixing

**Method 1: Interrupt-driven**
```
1. Set up interrupt at desired rate (e.g., 50 Hz via timer.device)
2. In each interrupt:
   - Calculate new volume or period
   - Write to AUDxVOL or AUDxPER
3. Changes apply immediately (within current sample)
```

**Method 2: Copper-driven**
```
1. Build Copper list with WAIT and MOVE instructions
2. WAIT for specific scanline
3. MOVE new value to AUDxVOL or AUDxPER
4. Perfectly synchronized to display timing
```

## 9. API Design Recommendations for Novus

### Safety and Abstraction Layers

#### Layer 1: Direct Hardware Access (unsafe)
```novus
unsafe {
    // Direct register access
    PAULA_AUD0LCH.write(sample_addr_high)
    PAULA_AUD0LCL.write(sample_addr_low)
    PAULA_AUD0LEN.write(sample_len_words)
    PAULA_AUD0PER.write(period)
    PAULA_AUD0VOL.write(volume)
    DMACON.set_bits(DMACON_AUD0EN | DMACON_DMAEN)
}
```

**Use Case**: Expert users, demo scene, maximum control
**Characteristics**: Zero overhead, full hardware control, no safety

#### Layer 2: Safe Hardware Wrapper
```novus
struct AudioChannel {
    channel_id: u8,  // 0-3
}

impl AudioChannel {
    fn play_sample(&mut self, sample: &Sample, period: u16, volume: u8) -> Result[(), AudioError] {
        // Validate parameters
        if volume > 64 { return Err(AudioError::InvalidVolume) }
        if period < 123 { return Err(AudioError::PeriodTooLow) }

        unsafe {
            // Safe wrapper around hardware access
            self.write_registers(sample.chip_addr(), sample.len_words(), period, volume)
            self.enable_dma()
        }
        Ok(())
    }
}
```

**Use Case**: Direct hardware control with compile-time safety
**Characteristics**: Minimal overhead, bounds checking, explicit lifetimes

#### Layer 3: audio.device Wrapper (OS-Friendly)
```novus
struct AudioDevice {
    device: DeviceHandle,
    allocated_channels: u8,  // Bitmask
}

impl AudioDevice {
    fn allocate_channels(&mut self, channels: u8) -> Result[(), AudioError] {
        // OS arbitration via ADCMD_ALLOCATE
    }

    fn play(&mut self, request: &mut AudioRequest) -> Result[(), AudioError] {
        // Queue IORequest, handle interrupts via Exec signals
    }
}
```

**Use Case**: Multitasking apps, system-friendly operation
**Characteristics**: OS overhead, proper channel arbitration, coexistence

#### Layer 4: High-Level Audio API
```novus
struct AudioPlayer {
    mixer: SoftwareMixer,
    streamer: StreamingEngine,
}

impl AudioPlayer {
    fn play_sound(&mut self, sound: &Sound, options: PlayOptions) -> SoundHandle {
        // High-level: software mixing, streaming, effects
    }

    fn set_volume(&mut self, handle: SoundHandle, volume: f32) {
        // Normalized 0.0-1.0 volume
    }
}
```

**Use Case**: Game engines, application development
**Characteristics**: Software mixing, multiple sounds per channel, high CPU cost

### Key Type Definitions

```novus
// Sample buffer in Chip RAM
struct Sample {
    data: ChipPtr[i8],      // Pointer to chip RAM
    len_bytes: u32,
    len_words: u16,         // Cached for hardware
}

// Playback request
struct AudioRequest {
    sample: Sample,
    period: u16,            // 123-65535
    volume: u8,             // 0-64
    mode: PlaybackMode,
}

enum PlaybackMode {
    OneShot,
    Loop,
    Stream(StreamConfig),
}

// Streaming config for double-buffering
struct StreamConfig {
    buffer_size: u32,
    callback: fn(&mut [i8]) -> Result[usize, StreamError],
}

// Result types
enum AudioError {
    InvalidVolume,
    PeriodTooLow,
    NoChipRAM,
    ChannelBusy,
    DeviceOpenFailed,
    AllocationFailed,
}
```

### Period Calculation Helpers

```novus
// Auto-detect PAL vs NTSC
fn paula_clock_hz() -> u32 {
    if system_is_pal() {
        3_546_895
    } else {
        3_579_545
    }
}

// Calculate period from sample rate
fn sample_rate_to_period(sample_rate_hz: u32) -> Result[u16, AudioError] {
    let period = paula_clock_hz() / sample_rate_hz
    if period < 123 {
        Err(AudioError::PeriodTooLow)
    } else if period > 65535 {
        Err(AudioError::PeriodTooHigh)
    } else {
        Ok(period as u16)
    }
}

// Calculate sample rate from period
fn period_to_sample_rate(period: u16) -> u32 {
    paula_clock_hz() / (period as u32)
}

// Common sample rates
const PAULA_8KHZ: u16 = 443   // PAL
const PAULA_11KHZ: u16 = 322
const PAULA_16KHZ: u16 = 222
const PAULA_22KHZ: u16 = 161
```

### Interrupt Handling (async/await Integration)

```novus
// Using Novus async/await built on Exec signals
async fn play_with_notification(channel: &mut AudioChannel, sample: &Sample) {
    channel.play_sample(sample, PAULA_8KHZ, 64).unwrap()

    // Await interrupt via Exec signal
    await audio_interrupt_signal(channel.channel_id)

    // Continue after playback complete
}

// Double-buffered streaming
async fn stream_audio(channel: &mut AudioChannel, source: &mut AudioSource) {
    let mut buffer_a = allocate_chip_buffer(4096)
    let mut buffer_b = allocate_chip_buffer(4096)

    source.fill_buffer(&mut buffer_a)
    channel.play_buffer(&buffer_a, PAULA_16KHZ, 64)

    loop {
        // Fill next buffer while current plays
        source.fill_buffer(&mut buffer_b)

        // Wait for current buffer to finish
        await audio_interrupt_signal(channel.channel_id)

        // Swap buffers
        channel.play_buffer(&buffer_b, PAULA_16KHZ, 64)
        swap(&mut buffer_a, &mut buffer_b)
    }
}
```

### Hardware Register Definitions

```novus
// Memory-mapped register definitions
const PAULA_BASE: u32 = 0xDFF000

// Audio channel 0
const PAULA_AUD0LCH: VolatilePtr[u16] = (PAULA_BASE + 0x0A0) as VolatilePtr[u16]
const PAULA_AUD0LCL: VolatilePtr[u16] = (PAULA_BASE + 0x0A2) as VolatilePtr[u16]
const PAULA_AUD0LEN: VolatilePtr[u16] = (PAULA_BASE + 0x0A4) as VolatilePtr[u16]
const PAULA_AUD0PER: VolatilePtr[u16] = (PAULA_BASE + 0x0A6) as VolatilePtr[u16]
const PAULA_AUD0VOL: VolatilePtr[u16] = (PAULA_BASE + 0x0A8) as VolatilePtr[u16]
const PAULA_AUD0DAT: VolatilePtr[u16] = (PAULA_BASE + 0x0AA) as VolatilePtr[u16]

// ... (repeat for channels 1-3)

// Control registers
const PAULA_DMACON:  VolatilePtr[u16] = (PAULA_BASE + 0x096) as VolatilePtr[u16]
const PAULA_DMACONR: VolatilePtr[u16] = (PAULA_BASE + 0x002) as VolatilePtr[u16]
const PAULA_ADKCON:  VolatilePtr[u16] = (PAULA_BASE + 0x09E) as VolatilePtr[u16]
const PAULA_ADKCONR: VolatilePtr[u16] = (PAULA_BASE + 0x010) as VolatilePtr[u16]

// DMACON bit masks
const DMACON_SETCLR: u16 = 1 << 15
const DMACON_DMAEN:  u16 = 1 << 9
const DMACON_AUD3EN: u16 = 1 << 3
const DMACON_AUD2EN: u16 = 1 << 2
const DMACON_AUD1EN: u16 = 1 << 1
const DMACON_AUD0EN: u16 = 1 << 0

// ADKCON modulation bits
const ADKCON_SETCLR: u16 = 1 << 15
const ADKCON_USE3PN: u16 = 1 << 3  // Ch3 period modulation
const ADKCON_USE2PN: u16 = 1 << 2  // Ch2 period modulation
const ADKCON_USE1VN: u16 = 1 << 1  // Ch1 amplitude modulation
const ADKCON_USE0VN: u16 = 1 << 0  // Ch0 amplitude modulation
```

### Modulation API (Advanced)

```novus
struct ModulationConfig {
    modulator_channel: u8,
    target_channel: u8,
    mode: ModulationMode,
}

enum ModulationMode {
    Amplitude,  // Volume modulation
    Period,     // Frequency modulation
}

impl AudioChannel {
    fn enable_modulation(&mut self, config: ModulationConfig) -> Result[(), AudioError] {
        // Validate channel pairing rules
        let adkcon_bit = match (config.modulator_channel, config.target_channel, config.mode) {
            (3, 0, ModulationMode::Amplitude) => ADKCON_USE0VN,
            (0, 1, ModulationMode::Amplitude) => ADKCON_USE1VN,
            (1, 2, ModulationMode::Period) => ADKCON_USE2PN,
            (2, 3, ModulationMode::Period) => ADKCON_USE3PN,
            _ => return Err(AudioError::InvalidModulationPair),
        }

        unsafe {
            PAULA_ADKCON.write(ADKCON_SETCLR | adkcon_bit)
        }

        Ok(())
    }
}
```

### Chip RAM Allocation

```novus
// Sample buffers MUST be in Chip RAM (DMA accessible)
fn allocate_sample_buffer(size_bytes: u32) -> Result[ChipPtr[i8], AudioError] {
    let ptr = allocate_chip_ram(size_bytes, MEMF_CHIP | MEMF_CLEAR)
    if ptr.is_null() {
        Err(AudioError::NoChipRAM)
    } else {
        Ok(ptr as ChipPtr[i8])
    }
}

// RAII wrapper for auto-cleanup
struct SampleBuffer {
    data: ChipPtr[i8],
    size: u32,
}

impl Drop for SampleBuffer {
    fn drop(&mut self) {
        free_chip_ram(self.data, self.size)
    }
}
```

## 10. Testing and Emulation Considerations

### Testing on Real Hardware
- **Target Machine**: A4000 with 68040 at `/Users/barry/Emulation/Amiga/A4000-DH0/Barry`
- **Copy Process**: Build executable, copy to shared drive, run on real Amiga
- **Validation**: Test all sample rates, verify period calculations match PAL/NTSC

### Emulation Testing
- **WinUAE/FS-UAE**: Cycle-accurate Paula emulation
- **Configuration**: Enable audio emulation, verify correct Paula timing
- **Debugging**: Use audio visualization tools, oscilloscope output

### Common Pitfalls to Test
1. **Period too low**: Values < 123 cause hardware malfunction
2. **Volume out of range**: Values > 64 may cause unexpected behavior
3. **Non-Chip RAM**: DMA cannot access Fast RAM - causes silence or crash
4. **Interrupt storms**: Failing to clear INTREQ causes repeated interrupts
5. **Channel conflicts**: Multiple tasks accessing same channel without arbitration
6. **Buffer alignment**: Though not strictly required, word-aligned buffers are safer
7. **Length in words**: Common mistake to use byte length instead of word length

### Validation Checklist
- [ ] Period calculation matches PAL/NTSC clock
- [ ] Volume clamped to 0-64 range
- [ ] Sample buffers allocated in Chip RAM
- [ ] Length specified in words, not bytes
- [ ] DMA channels properly enabled/disabled
- [ ] Interrupts acknowledged in handlers
- [ ] Double-buffering seamless (no clicks/pops)
- [ ] Modulation pairs correctly configured
- [ ] Clean shutdown (DMA disabled, channels freed)

## 11. Performance Considerations

### CPU Overhead
- **DMA Playback**: Virtually zero CPU overhead (hardware-driven)
- **Interrupt Handling**: Minimal if handlers are efficient
- **Software Mixing**: High CPU cost - avoid unless necessary
- **Period/Volume Changes**: Single word write - negligible cost

### Memory Bandwidth
- **DMA Priority**: Audio DMA competes with blitter, bitplane, copper
- **Slot Allocation**: One slot per scanline per channel
- **Bandwidth Impact**: 4 channels × 2 bytes/line ≈ 8 bytes/scanline
- **Conflict Resolution**: Audio typically has lower priority than video

### Optimization Strategies
1. **Precompute period values**: Build lookup table for common sample rates
2. **Batch register writes**: Minimize volatile access overhead
3. **Use audio.device for multitasking**: Let OS handle arbitration
4. **Pre-allocate buffers**: Avoid runtime Chip RAM allocation
5. **Efficient interrupt handlers**: Minimize work in Level 4 ISR
6. **Consider hardware limits**: Don't exceed 28 kHz sample rate

## 12. References and Sources

### Primary Documentation
- [Amiga Hardware Reference Manual: Audio Registers (Alphabetical)](http://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node0011.html)
- [Amiga Hardware Reference Manual: Audio Registers (Address Order)](http://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node0060.html)
- [Amiga Hardware Reference Manual: Data Output Rate / Limitations](http://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node00DE.html)
- [Amiga Hardware Reference Manual: Modulating Sound](http://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node00E7.html)
- [Amiga Hardware Reference Manual: ADKCON/ADKCONR](http://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node0194.html)
- [Amiga Hardware Reference Manual: DMA Control](http://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node0170.html)

### Device Documentation
- [Audio Device - AmigaOS Documentation Wiki](https://wiki.amigaos.net/wiki/Audio_Device)
- [Audio Device Manual: Double Buffered Sound Example](http://amiga.nvg.org/amiga/reference/Devices_Manual_guide/node003C.html)
- [Audio Device Manual: Allocation and Arbitration](http://amiga.nvg.org/amiga/reference/Devices_Manual_guide/node0028.html)
- [Audio Device Manual (ADCD 2.1)](http://amigadev.elowar.com/read/ADCD_2.1/Devices_Manual_guide/node001A.html)

### Technical Articles
- [Henryk Richter: Paula vs. System Theory (PDF)](http://bax.comlab.uni-rostock.de/dl/Paula_SystemTheoretic.pdf)
- [Sample Rates Deep Dive - GitHub Wiki](https://github.com/echolevel/open-amiga-sampler/wiki/Appendix-A:-Sample-rates-deep-dive)
- [Amiga Machine Code Letter VIII - Audio](https://www.markwrobel.dk/post/amiga-machine-code-letter8/)
- [The Paulimba (Musical Instrument Project)](https://www.linusakesson.net/music/paulimba/index.php)

### Hardware References
- [Amiga Original Chip Set - Wikipedia](https://en.wikipedia.org/wiki/Amiga_Original_Chip_Set)
- [8364 Paula Chip - VGMPF Wiki](https://www.vgmpf.com/Wiki/index.php?title=8364)
- [Custom Chip Register List - Coppershade](http://coppershade.org/articles/Code/Reference/Custom_Chip_Register_List/)
- [Amiga Memory Map](https://oscomp.hu/depot/amiga_memory_map.html)

### Community Resources
- [Paula Audio DMA - English Amiga Board](https://eab.abime.net/showthread.php?p=1679516)
- [Paula Interrupts - pouët.net](https://www.pouet.net/topic.php?which=10403&page=1)
- [14-bit Audio Mode Discussion - EAB](https://eab.abime.net/showthread.php?t=114426)

---

## Document Status
- **Created**: 2025-12-01
- **Purpose**: Inform Novus audio API design and implementation
- **Maintenance**: Update as implementation progresses and edge cases discovered
- **Next Steps**: Design concrete Novus API, implement hardware wrapper layer, create example programs
