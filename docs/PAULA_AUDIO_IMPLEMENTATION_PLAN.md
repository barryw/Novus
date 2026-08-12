# Paula Audio Implementation Plan

## Overview

This document outlines the plan for adding comprehensive Paula audio support to Novus, including:
1. Low-level hardware access API
2. High-level RAII-based audio API
3. audio.device integration for OS-friendly apps
4. Compile-time audio asset conversion (`@audio` attribute)
5. MOD playback support via ptplayer

## Design Decisions

### No DSL
Unlike Copper (static data structures) or Blitter (complex register setup), Paula audio is fundamentally runtime-driven. A DSL would add compiler complexity without meaningful benefit. We use a pure library approach.

### Deferred Interrupt Support
Initial implementation covers one-shot and looping playback. Interrupt-driven streaming/double-buffering will be added later when needed for MOD playback or streaming audio.

### ptplayer for MOD Playback
Rather than implementing a MOD player in Novus (a significant undertaking), we wrap Frank Wille's ptplayer - a battle-tested 68k assembly library used by most modern Amiga demos/games.

---

## Phase 1: FFI Layer

### Files to Modify

#### `Novus/std/ffi/amiga_structs.novus`

Add NDK struct definitions:

```novus
/// Paula audio channel hardware registers (from hardware/custom.h)
/// Each channel is 16 bytes, 4 channels total
pub struct AudChannel {
    ac_ptr: *u16,      // Pointer to waveform data (word-aligned)
    ac_len: u16,       // Length in words (1-65535)
    ac_per: u16,       // Period (sample rate divisor, 124-65535)
    ac_vol: u16,       // Volume (0-64)
    ac_dat: u16,       // Sample pair (hardware use only)
    ac_pad: [u16; 2],  // Unused padding
}

/// IORequest for audio.device (from devices/audio.h)
pub struct IOAudio {
    ioa_Request: IORequest,
    ioa_AllocKey: i16,     // Allocation key returned by ADCMD_ALLOCATE
    ioa_Data: *u8,         // Pointer to sample data
    ioa_Length: u32,       // Length in bytes
    ioa_Period: u16,       // Sample period
    ioa_Volume: u16,       // Volume (0-64)
    ioa_Cycles: u16,       // Number of times to play (0 = infinite)
    ioa_WriteMsg: Message, // Secondary message port for finished notification
}
```

#### `Novus/std/ffi/amiga_consts.novus`

Add audio-related constants from NDK headers:

```novus
// ============================================================================
// Audio Device Constants (from devices/audio.h)
// ============================================================================

pub const AUDIONAME: &str = "audio.device"
pub const ADHARD_CHANNELS: u32 = 4

// Allocation priority range
pub const ADALLOC_MINPREC: i8 = -128
pub const ADALLOC_MAXPREC: i8 = 127

// Audio device commands
pub const ADCMD_FREE: u16 = 9        // CMD_NONSTD + 0
pub const ADCMD_SETPREC: u16 = 10    // CMD_NONSTD + 1
pub const ADCMD_FINISH: u16 = 11     // CMD_NONSTD + 2
pub const ADCMD_PERVOL: u16 = 12     // CMD_NONSTD + 3
pub const ADCMD_LOCK: u16 = 13       // CMD_NONSTD + 4
pub const ADCMD_WAITCYCLE: u16 = 14  // CMD_NONSTD + 5
pub const ADCMD_ALLOCATE: u16 = 32

// IOAudio flags
pub const ADIOB_PERVOL: u8 = 4
pub const ADIOF_PERVOL: u8 = (1 << 4)
pub const ADIOB_SYNCCYCLE: u8 = 5
pub const ADIOF_SYNCCYCLE: u8 = (1 << 5)
pub const ADIOB_NOWAIT: u8 = 6
pub const ADIOF_NOWAIT: u8 = (1 << 6)
pub const ADIOB_WRITEMESSAGE: u8 = 7
pub const ADIOF_WRITEMESSAGE: u8 = (1 << 7)

// IOAudio errors
pub const ADIOERR_NOALLOCATION: i8 = -10
pub const ADIOERR_ALLOCFAILED: i8 = -11
pub const ADIOERR_CHANNELSTOLEN: i8 = -12

// ============================================================================
// DMA Control Bits (from hardware/dmabits.h)
// ============================================================================

pub const DMAF_SETCLR: u16 = $8000
pub const DMAF_AUDIO: u16 = $000F    // All 4 audio channels
pub const DMAF_AUD0: u16 = $0001
pub const DMAF_AUD1: u16 = $0002
pub const DMAF_AUD2: u16 = $0004
pub const DMAF_AUD3: u16 = $0008
pub const DMAF_MASTER: u16 = $0200

pub const DMAB_AUD0: u8 = 0
pub const DMAB_AUD1: u8 = 1
pub const DMAB_AUD2: u8 = 2
pub const DMAB_AUD3: u8 = 3
pub const DMAB_MASTER: u8 = 9

// ============================================================================
// Interrupt Bits (from hardware/intbits.h)
// ============================================================================

pub const INTB_AUD0: u8 = 7    // Audio channel 0 block finished
pub const INTB_AUD1: u8 = 8    // Audio channel 1 block finished
pub const INTB_AUD2: u8 = 9    // Audio channel 2 block finished
pub const INTB_AUD3: u8 = 10   // Audio channel 3 block finished

pub const INTF_AUD0: u16 = $0080
pub const INTF_AUD1: u16 = $0100
pub const INTF_AUD2: u16 = $0200
pub const INTF_AUD3: u16 = $0400

// ============================================================================
// ADKCON Bits - Audio Modulation (from hardware/adkbits.h)
// ============================================================================

pub const ADKB_USE0V1: u8 = 0   // Use channel 0 to modulate volume of 1
pub const ADKB_USE1V2: u8 = 1   // Use channel 1 to modulate volume of 2
pub const ADKB_USE2V3: u8 = 2   // Use channel 2 to modulate volume of 3
pub const ADKB_USE3VN: u8 = 3   // Use channel 3 to modulate volume of next
pub const ADKB_USE0P1: u8 = 4   // Use channel 0 to modulate period of 1
pub const ADKB_USE1P2: u8 = 5   // Use channel 1 to modulate period of 2
pub const ADKB_USE2P3: u8 = 6   // Use channel 2 to modulate period of 3
pub const ADKB_USE3PN: u8 = 7   // Use channel 3 to modulate period of next

pub const ADKF_USE0V1: u16 = $0001
pub const ADKF_USE1V2: u16 = $0002
pub const ADKF_USE2V3: u16 = $0004
pub const ADKF_USE3VN: u16 = $0008
pub const ADKF_USE0P1: u16 = $0010
pub const ADKF_USE1P2: u16 = $0020
pub const ADKF_USE2P3: u16 = $0040
pub const ADKF_USE3PN: u16 = $0080
pub const ADKF_SETCLR: u16 = $8000
```

#### `Novus/std/ffi/audio_device.novus` (New File)

```novus
// Generated from SFD file by Novus SFD Parser
// Library: audio.device
// Base: _AudioBase
//
// NOTE: Constants are in std::ffi::amiga_consts
// NOTE: Structs are in std::ffi::amiga_structs

// audio.device has no library functions - it's purely IORequest-based
// All operations use standard exec.library device I/O:
// - OpenDevice() to open
// - DoIO() / SendIO() / WaitIO() for commands
// - CloseDevice() to close
```

---

## Phase 2: Hardware Register Layer

### `Novus/std/hardware/paula.novus` (New File)

```novus
// Paula Audio Hardware Registers
// Module: std::hardware::paula
//
// Direct hardware register access for games and demos.
// For OS-friendly applications, use std::audio::device instead.

from std::core import Option
from std::hardware::registers import CUSTOM_BASE

// ============================================================================
// Audio Channel Register Offsets (from CUSTOM_BASE = $DFF000)
// ============================================================================

// Channel 0 registers
pub const AUD0LCH: u32 = $0A0   // Location pointer high word
pub const AUD0LCL: u32 = $0A2   // Location pointer low word
pub const AUD0LEN: u32 = $0A4   // Length in words
pub const AUD0PER: u32 = $0A6   // Period (sample rate divisor)
pub const AUD0VOL: u32 = $0A8   // Volume (0-64)
pub const AUD0DAT: u32 = $0AA   // Data register (write triggers DMA)

// Channel 1 registers (+$10 from channel 0)
pub const AUD1LCH: u32 = $0B0
pub const AUD1LCL: u32 = $0B2
pub const AUD1LEN: u32 = $0B4
pub const AUD1PER: u32 = $0B6
pub const AUD1VOL: u32 = $0B8
pub const AUD1DAT: u32 = $0BA

// Channel 2 registers (+$20 from channel 0)
pub const AUD2LCH: u32 = $0C0
pub const AUD2LCL: u32 = $0C2
pub const AUD2LEN: u32 = $0C4
pub const AUD2PER: u32 = $0C6
pub const AUD2VOL: u32 = $0C8
pub const AUD2DAT: u32 = $0CA

// Channel 3 registers (+$30 from channel 0)
pub const AUD3LCH: u32 = $0D0
pub const AUD3LCL: u32 = $0D2
pub const AUD3LEN: u32 = $0D4
pub const AUD3PER: u32 = $0D6
pub const AUD3VOL: u32 = $0D8
pub const AUD3DAT: u32 = $0DA

// ============================================================================
// Paula Clock Frequencies
// ============================================================================

/// PAL Paula clock frequency (Hz)
pub const PAULA_PAL_CLOCK: u32 = 3546895

/// NTSC Paula clock frequency (Hz)
pub const PAULA_NTSC_CLOCK: u32 = 3579545

// ============================================================================
// Hardware Limits
// ============================================================================

/// Minimum valid period (maximum sample rate ~28.6 kHz)
pub const PAULA_MIN_PERIOD: u16 = 124

/// Maximum valid period
pub const PAULA_MAX_PERIOD: u16 = 65535

/// Maximum volume level
pub const PAULA_MAX_VOLUME: u16 = 64

/// Maximum sample length in words (128KB)
pub const PAULA_MAX_LENGTH_WORDS: u16 = 65535

// ============================================================================
// Helper Functions
// ============================================================================

/// Calculate period value from sample rate (Hz)
///
/// # Arguments
/// * `sample_rate` - Desired sample rate in Hz
/// * `pal` - True for PAL (50Hz), false for NTSC (60Hz)
///
/// # Returns
/// Period value, or None if sample rate would result in invalid period
///
/// # Example
/// ```novus
/// // 11025 Hz on PAL = period 322
/// let period = period_from_hz(11025, true)
/// ```
pub fn period_from_hz(sample_rate: u32, pal: bool) -> Option<u16> {
    if sample_rate == 0 {
        return Option::None
    }

    let clock = if pal { PAULA_PAL_CLOCK } else { PAULA_NTSC_CLOCK }
    let period = clock / sample_rate

    if period < (u32)PAULA_MIN_PERIOD {
        return Option::None  // Sample rate too high
    }
    if period > (u32)PAULA_MAX_PERIOD {
        return Option::None  // Sample rate too low
    }

    return Option::Some((u16)period)
}

/// Calculate sample rate (Hz) from period value
///
/// # Arguments
/// * `period` - Paula period value (124-65535)
/// * `pal` - True for PAL, false for NTSC
///
/// # Returns
/// Sample rate in Hz
pub fn hz_from_period(period: u16, pal: bool) -> u32 {
    if period == 0 {
        return 0
    }
    let clock = if pal { PAULA_PAL_CLOCK } else { PAULA_NTSC_CLOCK }
    return clock / (u32)period
}

/// Common sample rate periods (PAL)
pub const PERIOD_8000_PAL: u16 = 443    // 8000 Hz (telephone quality)
pub const PERIOD_11025_PAL: u16 = 322   // 11025 Hz (quarter CD)
pub const PERIOD_22050_PAL: u16 = 161   // 22050 Hz (half CD)
pub const PERIOD_28000_PAL: u16 = 127   // ~28 kHz (near max quality)

/// Common sample rate periods (NTSC)
pub const PERIOD_8000_NTSC: u16 = 447
pub const PERIOD_11025_NTSC: u16 = 325
pub const PERIOD_22050_NTSC: u16 = 162
pub const PERIOD_28000_NTSC: u16 = 128

/// Stereo channel assignment (hardware-fixed)
pub enum StereoChannel {
    Left,   // Channels 0 and 3
    Right,  // Channels 1 and 2
}

/// Get the stereo assignment for a channel
///
/// Paula has fixed stereo routing:
/// - Channels 0 and 3 output to LEFT speaker
/// - Channels 1 and 2 output to RIGHT speaker
pub fn channel_stereo(channel: u8) -> StereoChannel {
    match channel {
        0 => return StereoChannel::Left,
        1 => return StereoChannel::Right,
        2 => return StereoChannel::Right,
        3 => return StereoChannel::Left,
        _ => return StereoChannel::Left,
    }
}

/// Get the base register offset for a channel (0-3)
pub fn channel_base_offset(channel: u8) -> u32 {
    return AUD0LCH + ((u32)(channel & 3) * $10)
}

/// Get the DMA enable bit for a channel (0-3)
pub fn channel_dma_bit(channel: u8) -> u16 {
    return 1 << (channel & 3)
}

/// Get the interrupt bit for a channel (0-3)
pub fn channel_int_bit(channel: u8) -> u16 {
    return $0080 << (channel & 3)
}
```

---

## Phase 3: High-Level Audio API

### `Novus/std/audio/paula.novus` (New File)

```novus
// Paula Audio API
// Module: std::audio::paula
//
// High-level RAII-based API for Paula audio playback.
// Provides safe wrappers around hardware registers with automatic cleanup.
//
// For OS-friendly applications that need to coexist with other programs,
// use std::audio::device instead.
//
// # Example
// ```novus
// from std::audio::paula import AudioChannel, SampleHandle, PlaybackMode
//
// // Load sample into chip RAM
// let sample = SampleHandle::from_data(raw_pcm_data, length)?
//
// // Acquire a channel and play
// let mut channel = AudioChannel::acquire(0)?
// channel.set_volume(64)?
// channel.play(&sample, PlaybackMode::OneShot)
//
// // Channel automatically stops and releases on drop
// ```

from std::core import Option, Result, Drop
from std::ffi::exec import AllocVec, FreeVec, CopyMem
from std::ffi::amiga_consts import MEMF_CHIP, MEMF_CLEAR, DMAF_SETCLR, DMAF_MASTER
from std::hardware::registers import CUSTOM_BASE, DMACON
from std::hardware::paula import (
    period_from_hz, channel_base_offset, channel_dma_bit,
    PAULA_MAX_VOLUME, PAULA_MIN_PERIOD, PAULA_MAX_PERIOD,
    PAULA_MAX_LENGTH_WORDS, PAULA_PAL_CLOCK, AUD0LCH
)

// ============================================================================
// Error Types
// ============================================================================

/// Errors that can occur during audio operations
pub enum AudioError {
    /// Channel index must be 0-3
    InvalidChannel,
    /// Volume must be 0-64
    InvalidVolume,
    /// Period must be 124-65535
    InvalidPeriod,
    /// Sample rate would result in invalid period
    InvalidSampleRate,
    /// Sample data must be in Chip RAM (DMA-accessible)
    SampleNotChipRam,
    /// Sample pointer must be word-aligned (even address)
    SampleMisaligned,
    /// Sample exceeds 128KB (65535 words)
    SampleTooLong,
    /// Failed to allocate chip RAM for sample
    AllocationFailed,
}

// ============================================================================
// Sample Handle
// ============================================================================

/// RAII wrapper for sample data in chip RAM
///
/// Manages the lifecycle of sample data:
/// - Allocates chip RAM on creation
/// - Copies sample data (can be from any memory)
/// - Frees chip RAM on drop
///
/// # Example
/// ```novus
/// let sample = SampleHandle::from_data(my_pcm_data, 1024)?
/// // Use sample...
/// // Chip RAM automatically freed when sample goes out of scope
/// ```
pub struct SampleHandle {
    data: *u8,
    length_bytes: u32,
    length_words: u16,
    sample_rate: u32,      // Original sample rate (for metadata)
    period_pal: u16,       // Pre-calculated PAL period
    period_ntsc: u16,      // Pre-calculated NTSC period
}

impl SampleHandle {
    /// Create a sample handle from raw PCM data
    ///
    /// Data is copied to chip RAM. The original data can be in any memory type.
    /// Sample format must be signed 8-bit PCM (the only format Paula supports).
    ///
    /// # Arguments
    /// * `data` - Pointer to signed 8-bit PCM sample data
    /// * `length_bytes` - Length in bytes
    ///
    /// # Returns
    /// SampleHandle on success, AudioError on failure
    pub fn from_data(data: *u8, length_bytes: u32) -> Result<SampleHandle, AudioError> {
        // Validate length
        if length_bytes > 131070 {  // 65535 words * 2 bytes
            return Result::Err(AudioError::SampleTooLong)
        }

        // Round up to word boundary
        let padded_length = (length_bytes + 1) & $FFFFFFFE
        let length_words = (u16)(padded_length / 2)

        unsafe {
            // Allocate chip RAM
            let chip_mem = AllocVec(padded_length, MEMF_CHIP | MEMF_CLEAR)
            if !chip_mem {
                return Result::Err(AudioError::AllocationFailed)
            }

            // Copy data
            CopyMem(data, chip_mem, length_bytes)

            return Result::Ok(SampleHandle {
                data: chip_mem,
                length_bytes: length_bytes,
                length_words: length_words,
                sample_rate: 0,        // Unknown, will be set by @audio
                period_pal: 322,       // Default to 11025 Hz
                period_ntsc: 325,
            })
        }
    }

    /// Create a sample handle with known sample rate
    ///
    /// Like from_data(), but also records the sample rate for automatic
    /// period calculation.
    pub fn from_data_with_rate(data: *u8, length_bytes: u32, sample_rate: u32) -> Result<SampleHandle, AudioError> {
        var handle = SampleHandle::from_data(data, length_bytes)?
        handle.sample_rate = sample_rate

        // Pre-calculate periods
        match period_from_hz(sample_rate, true) {
            Option::Some(p) => handle.period_pal = p,
            Option::None => {},
        }
        match period_from_hz(sample_rate, false) {
            Option::Some(p) => handle.period_ntsc = p,
            Option::None => {},
        }

        return Result::Ok(handle)
    }

    /// Get pointer to sample data in chip RAM
    pub fn ptr(&self) -> *u8 {
        return self.data
    }

    /// Get length in words (for AUDxLEN register)
    pub fn words(&self) -> u16 {
        return self.length_words
    }

    /// Get length in bytes
    pub fn bytes(&self) -> u32 {
        return self.length_bytes
    }

    /// Get the sample rate (if known)
    pub fn sample_rate(&self) -> u32 {
        return self.sample_rate
    }

    /// Get the PAL period for this sample's native rate
    pub fn period_pal(&self) -> u16 {
        return self.period_pal
    }

    /// Get the NTSC period for this sample's native rate
    pub fn period_ntsc(&self) -> u16 {
        return self.period_ntsc
    }
}

impl Drop for SampleHandle {
    fn drop(&mut self) {
        if self.data {
            unsafe {
                FreeVec(self.data)
                self.data = null
            }
        }
    }
}

// ============================================================================
// Playback Mode
// ============================================================================

/// How to play a sample
pub enum PlaybackMode {
    /// Play once, then stop
    OneShot,
    /// Loop forever until stopped
    Loop,
}

// ============================================================================
// Audio Channel
// ============================================================================

/// RAII handle for a Paula audio channel
///
/// Directly controls hardware registers. For OS-friendly audio that
/// cooperates with other applications, use AudioDeviceHandle instead.
///
/// # Example
/// ```novus
/// let mut ch = AudioChannel::acquire(0)?
/// ch.set_volume(64)?
/// ch.set_period(322)?  // ~11 kHz
/// ch.play(&sample, PlaybackMode::Loop)
///
/// // Later...
/// ch.stop()
/// // Or just let it drop - channel stops automatically
/// ```
pub struct AudioChannel {
    channel: u8,
    period: u16,
    volume: u16,
    active: bool,
}

impl AudioChannel {
    /// Acquire a hardware audio channel
    ///
    /// This directly takes the hardware channel without going through
    /// audio.device. Only use this for games/demos that take over the system.
    ///
    /// # Arguments
    /// * `channel` - Channel number (0-3)
    ///
    /// # Returns
    /// AudioChannel on success, AudioError::InvalidChannel if channel > 3
    pub fn acquire(channel: u8) -> Result<AudioChannel, AudioError> {
        if channel > 3 {
            return Result::Err(AudioError::InvalidChannel)
        }

        return Result::Ok(AudioChannel {
            channel: channel,
            period: 322,      // Default ~11 kHz
            volume: 64,       // Max volume
            active: false,
        })
    }

    /// Set the playback period (lower = higher pitch)
    ///
    /// Period must be 124-65535. Lower periods = higher sample rate = higher pitch.
    ///
    /// Common values (PAL):
    /// - 443 = 8000 Hz
    /// - 322 = 11025 Hz
    /// - 161 = 22050 Hz
    /// - 127 = ~28000 Hz (near maximum)
    pub fn set_period(&mut self, period: u16) -> Result<(), AudioError> {
        if period < PAULA_MIN_PERIOD {
            return Result::Err(AudioError::InvalidPeriod)
        }

        self.period = period

        if self.active {
            self.write_period()
        }

        return Result::Ok(())
    }

    /// Set sample rate in Hz (convenience method)
    ///
    /// Automatically calculates the correct period value.
    ///
    /// # Arguments
    /// * `hz` - Sample rate in Hz (e.g., 11025, 22050)
    /// * `pal` - True for PAL system, false for NTSC
    pub fn set_sample_rate(&mut self, hz: u32, pal: bool) -> Result<(), AudioError> {
        match period_from_hz(hz, pal) {
            Option::Some(period) => {
                self.period = period
                if self.active {
                    self.write_period()
                }
                return Result::Ok(())
            },
            Option::None => {
                return Result::Err(AudioError::InvalidSampleRate)
            },
        }
    }

    /// Set volume (0-64)
    ///
    /// 0 = silent, 64 = maximum volume
    pub fn set_volume(&mut self, volume: u16) -> Result<(), AudioError> {
        if volume > PAULA_MAX_VOLUME {
            return Result::Err(AudioError::InvalidVolume)
        }

        self.volume = volume

        if self.active {
            self.write_volume()
        }

        return Result::Ok(())
    }

    /// Get current volume
    pub fn get_volume(&self) -> u16 {
        return self.volume
    }

    /// Get current period
    pub fn get_period(&self) -> u16 {
        return self.period
    }

    /// Check if channel is currently playing
    pub fn is_playing(&self) -> bool {
        return self.active
    }

    /// Play a sample
    ///
    /// Starts playback immediately using the current period and volume settings.
    ///
    /// # Arguments
    /// * `sample` - Sample to play (must remain valid during playback)
    /// * `mode` - OneShot or Loop
    pub fn play(&mut self, sample: &SampleHandle, mode: PlaybackMode) {
        unsafe {
            let base = channel_base_offset(self.channel)
            let addr = (u32)sample.ptr()

            // Write sample pointer (high word, then low word)
            let lch = (*u16)(CUSTOM_BASE + base)
            let lcl = (*u16)(CUSTOM_BASE + base + 2)
            *lch = (u16)(addr >> 16)
            *lcl = (u16)(addr & $FFFF)

            // Write length in words
            let len_reg = (*u16)(CUSTOM_BASE + base + 4)
            *len_reg = sample.words()

            // Write period and volume
            self.write_period()
            self.write_volume()

            // Enable DMA for this channel
            let dma_bit = channel_dma_bit(self.channel)
            let dmacon = (*u16)(CUSTOM_BASE + DMACON)
            *dmacon = DMAF_SETCLR | DMAF_MASTER | dma_bit

            self.active = true
        }
    }

    /// Play a sample at its native sample rate
    ///
    /// Uses the sample's pre-calculated period value.
    ///
    /// # Arguments
    /// * `sample` - Sample to play
    /// * `mode` - OneShot or Loop
    /// * `pal` - True for PAL, false for NTSC
    pub fn play_native(&mut self, sample: &SampleHandle, mode: PlaybackMode, pal: bool) {
        if pal {
            self.period = sample.period_pal()
        } else {
            self.period = sample.period_ntsc()
        }
        self.play(sample, mode)
    }

    /// Stop playback
    pub fn stop(&mut self) {
        if self.active {
            unsafe {
                // Disable DMA for this channel
                let dma_bit = channel_dma_bit(self.channel)
                let dmacon = (*u16)(CUSTOM_BASE + DMACON)
                *dmacon = dma_bit  // No SETCLR = clear bit

                // Set volume to 0 to prevent any residual noise
                self.volume = 0
                self.write_volume()
            }
            self.active = false
        }
    }

    // Internal: write period to hardware
    fn write_period(&self) {
        unsafe {
            let base = channel_base_offset(self.channel)
            let reg = (*u16)(CUSTOM_BASE + base + 6)
            *reg = self.period
        }
    }

    // Internal: write volume to hardware
    fn write_volume(&self) {
        unsafe {
            let base = channel_base_offset(self.channel)
            let reg = (*u16)(CUSTOM_BASE + base + 8)
            *reg = self.volume
        }
    }
}

impl Drop for AudioChannel {
    fn drop(&mut self) {
        self.stop()
    }
}
```

---

## Phase 4: audio.device Integration

### `Novus/std/audio/device.novus` (New File)

```novus
// Audio Device API
// Module: std::audio::device
//
// OS-friendly audio playback using audio.device.
// Properly allocates channels through AmigaOS, allowing coexistence
// with other applications.
//
// Use this instead of std::audio::paula when:
// - Running as a Workbench application
// - Need to play sounds without disturbing other programs
// - Want automatic channel allocation
//
// # Example
// ```novus
// from std::audio::device import AudioDeviceHandle
//
// // Request any available channel with medium priority
// var audio = AudioDeviceHandle::open_any(0)?
//
// // Play a sound
// audio.play(&sample, 322, 64, 1)?  // period, volume, cycles
//
// // Device automatically closed on drop
// ```

from std::core import Option, Result, Drop
from std::ffi::exec import (
    OpenDevice, CloseDevice, DoIO, SendIO, WaitIO, AbortIO,
    CreateIORequest, DeleteIORequest, CreateMsgPort, DeleteMsgPort,
    CopyMem
)
from std::ffi::amiga_structs import IOAudio, IORequest, MsgPort, Message
from std::ffi::amiga_consts import (
    AUDIONAME, ADCMD_ALLOCATE, ADCMD_FREE, ADCMD_PERVOL,
    ADIOF_PERVOL, CMD_WRITE, CMD_STOP, CMD_START,
    ADIOERR_NOALLOCATION, ADIOERR_ALLOCFAILED, ADIOERR_CHANNELSTOLEN
)
from std::audio::paula import SampleHandle

// ============================================================================
// Error Types
// ============================================================================

/// Errors from audio.device operations
pub enum AudioDeviceError {
    /// Failed to create message port
    NoMsgPort,
    /// Failed to create IORequest
    NoIORequest,
    /// Failed to open audio.device
    OpenFailed,
    /// No channels available at requested priority
    NoAllocation,
    /// Channel allocation failed
    AllocFailed,
    /// Channel was stolen by higher-priority request
    ChannelStolen,
    /// I/O command failed
    IOError(i8),
    /// Invalid parameter
    InvalidParameter,
}

// ============================================================================
// Channel Preference
// ============================================================================

/// Which channels to request
pub enum ChannelPreference {
    /// Any single channel
    Any,
    /// Specific channel (0-3)
    Specific(u8),
    /// Left channel (0 or 3)
    Left,
    /// Right channel (1 or 2)
    Right,
    /// Stereo pair (left + right)
    Stereo,
    /// All four channels
    All,
}

impl ChannelPreference {
    /// Convert to channel allocation mask
    fn to_mask(&self) -> u8 {
        match self {
            ChannelPreference::Any => return $0F,        // Any of 0,1,2,3
            ChannelPreference::Specific(ch) => return 1 << ch,
            ChannelPreference::Left => return $09,       // 0 or 3
            ChannelPreference::Right => return $06,      // 1 or 2
            ChannelPreference::Stereo => return $0F,     // Need 2 channels
            ChannelPreference::All => return $0F,        // All 4
        }
    }
}

// ============================================================================
// Audio Device Handle
// ============================================================================

/// RAII wrapper for audio.device
///
/// Manages the complete lifecycle:
/// - Message port for I/O replies
/// - IOAudio request structure
/// - Device open/close
/// - Channel allocation/deallocation
///
/// Automatically releases channels and closes device on drop.
pub struct AudioDeviceHandle {
    io_audio: *IOAudio,
    port: *MsgPort,
    allocated_channels: u8,   // Bitmask of channels we own
    alloc_key: i16,           // Key for our allocation
}

impl AudioDeviceHandle {
    /// Open audio.device and allocate channels
    ///
    /// # Arguments
    /// * `preference` - Which channels to request
    /// * `priority` - Allocation priority (-128 to 127, higher = more important)
    ///
    /// # Returns
    /// AudioDeviceHandle on success, or error
    pub fn open(preference: ChannelPreference, priority: i8) -> Result<AudioDeviceHandle, AudioDeviceError> {
        // Create message port
        let port = CreateMsgPort()
        if (u32)port == 0 {
            return Result::Err(AudioDeviceError::NoMsgPort)
        }

        // Create IORequest
        let req = CreateIORequest(port, @sizeof(IOAudio))
        if (u32)req == 0 {
            DeleteMsgPort(port)
            return Result::Err(AudioDeviceError::NoIORequest)
        }

        let io_audio = (*IOAudio)req

        // Set up allocation request
        let channel_mask = preference.to_mask()

        // The allocation array is a list of acceptable channel combinations
        // We put our mask in the data field for ADCMD_ALLOCATE
        var alloc_map: [u8; 4] = [channel_mask, 0, 0, 0]

        io_audio.ioa_Request.io_Message.mn_ReplyPort = port
        io_audio.ioa_Request.io_Message.mn_Node.ln_Pri = priority
        io_audio.ioa_Data = &alloc_map[0]
        io_audio.ioa_Length = 4

        // Open device (this also allocates)
        unsafe {
            let ioreq = (*IORequest)&io_audio.ioa_Request
            let error = OpenDevice(AUDIONAME, 0, ioreq, 0)

            if error != 0 {
                DeleteIORequest(req)
                DeleteMsgPort(port)

                match error {
                    ADIOERR_NOALLOCATION => return Result::Err(AudioDeviceError::NoAllocation),
                    ADIOERR_ALLOCFAILED => return Result::Err(AudioDeviceError::AllocFailed),
                    _ => return Result::Err(AudioDeviceError::OpenFailed),
                }
            }
        }

        // Get allocation key and channels
        let alloc_key = io_audio.ioa_AllocKey
        let allocated = io_audio.ioa_Request.io_Unit  // Unit field contains channel mask

        return Result::Ok(AudioDeviceHandle {
            io_audio: io_audio,
            port: port,
            allocated_channels: (u8)(u32)allocated,
            alloc_key: alloc_key,
        })
    }

    /// Open and request any available channel
    pub fn open_any(priority: i8) -> Result<AudioDeviceHandle, AudioDeviceError> {
        return AudioDeviceHandle::open(ChannelPreference::Any, priority)
    }

    /// Open and request a specific channel
    pub fn open_channel(channel: u8, priority: i8) -> Result<AudioDeviceHandle, AudioDeviceError> {
        if channel > 3 {
            return Result::Err(AudioDeviceError::InvalidParameter)
        }
        return AudioDeviceHandle::open(ChannelPreference::Specific(channel), priority)
    }

    /// Play a sample on our allocated channel(s)
    ///
    /// # Arguments
    /// * `sample` - Sample data (must be in chip RAM)
    /// * `period` - Playback period (124-65535)
    /// * `volume` - Volume (0-64)
    /// * `cycles` - Number of times to play (0 = infinite loop)
    pub fn play(&self, sample: &SampleHandle, period: u16, volume: u16, cycles: u16) -> Result<(), AudioDeviceError> {
        self.io_audio.ioa_Request.io_Command = CMD_WRITE
        self.io_audio.ioa_Request.io_Flags = ADIOF_PERVOL
        self.io_audio.ioa_Data = sample.ptr()
        self.io_audio.ioa_Length = sample.bytes()
        self.io_audio.ioa_Period = period
        self.io_audio.ioa_Volume = volume
        self.io_audio.ioa_Cycles = cycles

        let ioreq = (*IORequest)&self.io_audio.ioa_Request
        let error = DoIO(ioreq)

        if error != 0 {
            return Result::Err(AudioDeviceError::IOError(error))
        }

        return Result::Ok(())
    }

    /// Play a sample at its native sample rate
    pub fn play_native(&self, sample: &SampleHandle, volume: u16, cycles: u16, pal: bool) -> Result<(), AudioDeviceError> {
        let period = if pal { sample.period_pal() } else { sample.period_ntsc() }
        return self.play(sample, period, volume, cycles)
    }

    /// Stop playback on our channel(s)
    pub fn stop(&self) -> Result<(), AudioDeviceError> {
        self.io_audio.ioa_Request.io_Command = CMD_STOP

        let ioreq = (*IORequest)&self.io_audio.ioa_Request
        let error = DoIO(ioreq)

        if error != 0 {
            return Result::Err(AudioDeviceError::IOError(error))
        }

        return Result::Ok(())
    }

    /// Resume playback after stop
    pub fn start(&self) -> Result<(), AudioDeviceError> {
        self.io_audio.ioa_Request.io_Command = CMD_START

        let ioreq = (*IORequest)&self.io_audio.ioa_Request
        let error = DoIO(ioreq)

        if error != 0 {
            return Result::Err(AudioDeviceError::IOError(error))
        }

        return Result::Ok(())
    }

    /// Get the channels we have allocated (bitmask)
    pub fn channels(&self) -> u8 {
        return self.allocated_channels
    }

    /// Check if we have a specific channel
    pub fn has_channel(&self, channel: u8) -> bool {
        if channel > 3 {
            return false
        }
        return (self.allocated_channels & (1 << channel)) != 0
    }
}

impl Drop for AudioDeviceHandle {
    fn drop(&mut self) {
        if let io = self.io_audio {
            unsafe {
                // Free our channel allocation
                io.ioa_Request.io_Command = ADCMD_FREE
                let ioreq = (*IORequest)&io.ioa_Request
                let _ = DoIO(ioreq)

                // Close device
                CloseDevice(ioreq)

                // Free IORequest
                DeleteIORequest((*u8)io)
            }
            self.io_audio = (*IOAudio)0
        }

        if let port = self.port {
            DeleteMsgPort(port)
            self.port = (*MsgPort)0
        }
    }
}
```

---

## Phase 5: Compile-Time Audio Conversion

### Compiler Changes

#### Add NAudio NuGet Package

```xml
<!-- In Novus.Core.csproj -->
<PackageReference Include="NAudio" Version="2.2.1" />
```

#### New Files

1. **`Novus.Core/Audio/AudioConverter.cs`**
   - WAV parsing and conversion
   - AIFF parsing
   - IFF-8SVX parsing (custom implementation)
   - Resampling (sinc or linear)
   - Bit depth conversion (16→8 bit with dithering)
   - Normalization

2. **`Novus.Core/Audio/IFF8SVXReader.cs`**
   - Native Amiga audio format parser
   - Handles VHDR, BODY, NAME chunks
   - Returns raw 8-bit signed PCM

3. **`Novus.Core/Attributes/AudioAttributeHandler.cs`**
   - Parses `@audio("file", options...)` attributes
   - Invokes AudioConverter at compile time
   - Generates static byte array in Chip RAM section
   - Creates AudioSample struct with metadata

#### Attribute Syntax

```novus
// Minimal - just the file
@audio("sounds/explosion.wav")
static EXPLOSION: AudioSample

// With options
@audio("sounds/laser.wav", sample_rate: 11025, normalize: true)
static LASER: AudioSample

// IFF-8SVX (native Amiga format)
@audio("sounds/boing.8svx")
static BOING: AudioSample

// Raw PCM (already converted)
@audio_raw("sounds/click.raw", sample_rate: 8000)
static CLICK: AudioSample
```

#### Attribute Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `sample_rate` | u32 | Keep original | Target sample rate in Hz |
| `channels` | enum | Mono | Mono, Left, Right |
| `normalize` | bool | false | Normalize to max amplitude |
| `trim_silence` | bool | false | Remove leading/trailing silence |
| `loop_start` | u32 | 0 | Loop start point in samples |
| `loop_end` | u32 | null | Loop end point (null = end) |

#### Generated Code

The `@audio` attribute generates:

```novus
// Input:
@audio("sounds/explosion.wav", sample_rate: 11025, normalize: true)
static EXPLOSION: AudioSample

// Generated (conceptually):
#[chip_ram]
#[align(2)]
static __EXPLOSION_DATA: [u8; 2048] = [ /* converted PCM bytes */ ]

static EXPLOSION: AudioSample = AudioSample {
    data: &__EXPLOSION_DATA[0],
    length_bytes: 2048,
    length_words: 1024,
    sample_rate: 11025,
    period_pal: 322,
    period_ntsc: 325,
}
```

---

## Phase 6: MOD Playback (ptplayer Integration)

### Files

1. **`vendor/ptplayer/`**
   - `ptplayer.asm` - Frank Wille's ptplayer source
   - `ptplayer.h` - C header
   - Pre-assembled object files for different CPUs

2. **`Novus/std/audio/mod.novus`**

```novus
// MOD File Playback
// Module: std::audio::mod
//
// Wrapper for ptplayer - the standard ProTracker MOD player for Amiga.
//
// # Example
// ```novus
// @mod("music/intro.mod")
// static INTRO_MUSIC: ModFile
//
// from std::audio::mod import ModPlayer
//
// let player = ModPlayer::new()?
// player.play(&INTRO_MUSIC)
//
// // In your main loop:
// player.update()  // Call every frame
//
// player.stop()
// ```

from std::core import Option, Result, Drop

/// A loaded MOD file
pub struct ModFile {
    data: *u8,
    size: u32,
    // Metadata extracted at compile time
    title: [u8; 20],
    num_channels: u8,
    num_patterns: u8,
}

/// MOD player state
pub struct ModPlayer {
    current_mod: *ModFile,
    playing: bool,
    master_volume: u8,
}

impl ModPlayer {
    /// Create a new MOD player
    pub fn new() -> Result<ModPlayer, AudioError> {
        // Initialize ptplayer
        unsafe {
            _mt_install_cia()  // Install CIA timer interrupt
        }

        return Result::Ok(ModPlayer {
            current_mod: null,
            playing: false,
            master_volume: 64,
        })
    }

    /// Start playing a MOD file
    pub fn play(&mut self, mod_file: &ModFile) {
        unsafe {
            _mt_init(mod_file.data, null, 0)
            _mt_enable = 1
        }
        self.current_mod = mod_file
        self.playing = true
    }

    /// Stop playback
    pub fn stop(&mut self) {
        unsafe {
            _mt_enable = 0
            _mt_end()
        }
        self.playing = false
    }

    /// Set master volume (0-64)
    pub fn set_volume(&mut self, volume: u8) {
        self.master_volume = volume
        unsafe {
            _mt_mastervol(volume)
        }
    }

    /// Check if currently playing
    pub fn is_playing(&self) -> bool {
        return self.playing
    }
}

impl Drop for ModPlayer {
    fn drop(&mut self) {
        self.stop()
        unsafe {
            _mt_remove_cia()
        }
    }
}

// External ptplayer functions (linked from ptplayer.o)
extern fn _mt_install_cia()
extern fn _mt_remove_cia()
extern fn _mt_init(data: *u8, samples: *u8, song_pos: u8)
extern fn _mt_end()
extern fn _mt_mastervol(vol: u8)
extern var _mt_enable: u8
```

### `@mod` Attribute

```novus
// Include MOD file at compile time
@mod("music/ingame.mod")
static GAME_MUSIC: ModFile

// The compiler:
// 1. Validates it's a valid MOD file
// 2. Extracts metadata (title, channels, patterns)
// 3. Embeds the raw data in chip RAM
// 4. Generates ModFile struct with metadata
```

---

## Implementation Order

1. **Phase 1: FFI Layer** (~1 day)
   - Add structs to amiga_structs.novus
   - Add constants to amiga_consts.novus
   - Create empty audio_device.novus

2. **Phase 2: Hardware Layer** (~1 day)
   - Create std/hardware/paula.novus
   - Period/frequency helpers
   - Register constants

3. **Phase 3: High-Level API** (~2 days)
   - Create std/audio/paula.novus
   - SampleHandle with chip RAM management
   - AudioChannel with RAII

4. **Phase 4: audio.device** (~2 days)
   - Create std/audio/device.novus
   - Channel allocation
   - OS-friendly playback

5. **Phase 5: @audio Attribute** (~3-4 days)
   - Add NAudio package
   - Implement AudioConverter
   - IFF-8SVX parser
   - Attribute handler
   - Tests

6. **Phase 6: MOD Support** (~2 days)
   - Integrate ptplayer
   - Create std/audio/mod.novus
   - @mod attribute
   - Tests

---

## Test Files to Create

```
Novus.Tests/Examples/
├── audio_simple_play.novus      # Basic SampleHandle + AudioChannel
├── audio_volume_fade.novus      # Volume manipulation
├── audio_multi_channel.novus    # All 4 channels
├── audio_device_test.novus      # audio.device integration
├── audio_sample_import.novus    # @audio attribute test
└── audio_mod_player.novus       # MOD playback test
```

---

## Questions Resolved

1. **audio.device**: Yes, implement in Phase 4
2. **DSL**: No, pure library approach
3. **Interrupts**: Deferred, start with one-shot/loop
4. **Asset conversion**: `@audio` attribute with compile-time conversion
5. **Formats**: WAV, IFF-8SVX, AIFF, RAW, MOD
6. **External deps**: Self-contained (NAudio for WAV, custom for 8SVX)
7. **Resampling**: Sinc (NAudio supports this)
8. **Syntax**: `@audio("file", options...)` on static declaration
