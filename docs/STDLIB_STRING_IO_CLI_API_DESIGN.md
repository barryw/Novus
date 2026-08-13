# Novus Standard Library: String, I/O, and CLI Argument APIs

**Design Document**
**Version:** 1.0
**Date:** 2025-11-29
**Status:** Design Review

---

## Table of Contents

1. [Overview](#overview)
2. [Design Philosophy](#design-philosophy)
3. [String Manipulation API](#string-manipulation-api)
4. [File I/O API](#file-io-api)
5. [CLI Argument Parsing API](#cli-argument-parsing-api)
6. [Complete Example Program](#complete-example-program)
7. [Implementation Notes](#implementation-notes)
8. [Migration Guide](#migration-guide)

---

## Overview

This document specifies ergonomic, Amiga-native APIs for three critical stdlib areas:

1. **String Manipulation** - Building, formatting, parsing, and transforming strings
2. **File I/O** - Reading, writing, and buffering file operations with proper error handling
3. **CLI Arguments** - Parsing command-line arguments the Amiga way (ReadArgs-based)

### Goals

- Safe by default with Result/Option everywhere
- RAII resource management via Drop trait
- Amiga-aware (chip/fast RAM, DOS conventions, BCPL strings)
- Zero-cost abstractions where possible
- Consistent naming and error handling patterns
- Professional CLI tool ergonomics

### Non-Goals

- UTF-8 support (Latin-1/ASCII only for now)
- Async I/O (synchronous DOS calls)
- Cross-platform compatibility (Amiga-first design)

---

## Design Philosophy

### 1. The String Type Hierarchy

**Existing foundation:**
- `Str` - borrowed slice (fat pointer: ptr + len), zero-copy views
- `String` - owned, heap-allocated, growable (backed by Vec&lt;u8&gt;)
- `NameString`, `GadgetString`, `PathString` - stack-allocated fixed sizes
- `BStr` - BCPL string for DOS compatibility (length-prefixed)

**Encoding:**
- Latin-1 (ISO 8859-1) - single byte per character
- Compatible with AmigaOS text.datatype and locale.library
- String literals are null-terminated C strings in assembly output

**Memory semantics:**
- Str is Copy (just a fat pointer)
- String is not Copy (owns heap memory, has Drop impl)
- All allocations use Vec's strategy (fast RAM by default)

### 2. Error Handling Patterns

**Consistent use of Result:**
```novus
pub fn read_file(path: Str) -> Result&lt;String, DosError&gt;
pub fn parse_int(s: Str) -> Result&lt;i32, ParseError&gt;
```

**Option for benign absence:**
```novus
pub fn find(haystack: Str, needle: Str) -> Option&lt;u32&gt;
```

**Never panic in stdlib - always return errors:**
- Out of bounds → Result::Err
- Allocation failure → Result::Err
- Invalid input → Result::Err

### 3. AmigaDOS Integration

**Path conventions:**
- Forward slash `/` for path separators (not backslash)
- Colon `:` for device/volume names (e.g., `SYS:`, `RAM:`)
- No case sensitivity (DOS is case-insensitive)
- Maximum path length: 255 bytes (BCPL string limit)

**File handle semantics:**
- BPTR file handles (i32 values, 0 = invalid)
- RAII wrappers auto-close via Drop
- Respect DOS's ~20 file handle limit per process

---

## String Manipulation API

### Namespace: `std::string`

### 1. String Building and Formatting

#### 1.1 StringBuilder - Heap-Allocated Builder

```novus
/// StringBuilder - efficient string construction with heap allocation
///
/// Use when building strings dynamically where final size is unknown
/// or when returning owned strings from functions.
///
/// Example:
/// ```novus
/// var builder = StringBuilder::new()
/// builder.push_str("Hello, ")
/// builder.push_str("World!")
/// let result: String = builder.build()
/// ```
pub struct StringBuilder {
    string: String,
}

impl StringBuilder {
    /// Create new builder with default capacity (256 bytes)
    pub fn new() -> Option&lt;StringBuilder&gt;

    /// Create builder with specific capacity
    pub fn with_capacity(capacity: u32) -> Option&lt;StringBuilder&gt;

    /// Append a string slice
    pub fn push_str(&mut self, s: Str) -> bool

    /// Append a single byte
    pub fn push_byte(&mut self, byte: u8) -> bool

    /// Append a character (Latin-1)
    pub fn push_char(&mut self, ch: u8) -> bool

    /// Append formatted integer (signed)
    pub fn push_i32(&mut self, value: i32) -> bool

    /// Append formatted integer (unsigned)
    pub fn push_u32(&mut self, value: u32) -> bool

    /// Append formatted hex (lowercase)
    pub fn push_hex(&mut self, value: u32) -> bool

    /// Append formatted hex (uppercase)
    pub fn push_hex_upper(&mut self, value: u32) -> bool

    /// Append formatted boolean ("true" or "false")
    pub fn push_bool(&mut self, value: bool) -> bool

    /// Get current length
    pub fn len(&self) -> u32

    /// Check if empty
    pub fn is_empty(&self) -> bool

    /// Get as Str slice (borrows)
    pub fn as_str(&self) -> Str

    /// Clear contents (keeps capacity)
    pub fn clear(&mut self)

    /// Build final String (consumes builder)
    pub fn build(consuming self) -> String
}
```

**Implementation notes:**
- Wraps String internally
- All `push_*` methods return `bool` for allocation failure
- Integer formatting uses stack buffer, then copies to builder
- Hex formatting uses nibble lookup table

#### 1.2 Format Functions

```novus
/// Format a string using template syntax
///
/// Template uses `{}` for placeholders (simplified, no format specs yet)
///
/// Example:
/// ```novus
/// let name = "Amiga"
/// let version = 3
/// let result = format("Welcome to {} version {}", &[name, version])?
/// ```
///
/// Returns Err if:
/// - Allocation fails
/// - Placeholder count doesn't match args
pub fn format(template: Str, args: &[FormatArg]) -> Result&lt;String, StringError&gt;

/// Argument type for format function
pub enum FormatArg {
    Str(Str),
    I32(i32),
    U32(u32),
    Bool(bool),
    Hex(u32),
}
```

**Alternative: macro-based formatting (future):**
```novus
// Future syntax with compiler macro support:
let s = format!("Welcome to {} version {}", name, version)
```

### 2. String Parsing

#### 2.1 Parse Error Type

```novus
/// Errors during string parsing operations
pub enum ParseError {
    /// Empty string where value expected
    Empty,
    /// Invalid character at position
    InvalidChar(u32),
    /// Number overflow (value too large)
    Overflow,
    /// Number underflow (value too small for signed)
    Underflow,
    /// Invalid boolean (not "true" or "false")
    InvalidBool,
}
```

#### 2.2 Integer Parsing

```novus
impl Str {
    /// Parse signed 32-bit integer
    ///
    /// Accepts optional leading '-' or '+', then digits 0-9
    /// Handles overflow/underflow
    ///
    /// Example:
    /// ```novus
    /// let s = Str::from_cstr("-12345")
    /// let value = s.parse_i32()?  // Returns Ok(-12345)
    /// ```
    pub fn parse_i32(&self) -> Result&lt;i32, ParseError&gt;

    /// Parse unsigned 32-bit integer
    ///
    /// Accepts digits 0-9, no sign
    ///
    /// Example:
    /// ```novus
    /// let s = Str::from_cstr("42")
    /// let value = s.parse_u32()?  // Returns Ok(42)
    /// ```
    pub fn parse_u32(&self) -> Result&lt;u32, ParseError&gt;

    /// Parse hexadecimal (case-insensitive, optional 0x prefix)
    ///
    /// Example:
    /// ```novus
    /// let s1 = Str::from_cstr("0xFF00")
    /// let s2 = Str::from_cstr("ff00")
    /// let v1 = s1.parse_hex()?  // Ok(0xFF00)
    /// let v2 = s2.parse_hex()?  // Ok(0xFF00)
    /// ```
    pub fn parse_hex(&self) -> Result&lt;u32, ParseError&gt;

    /// Parse boolean ("true" or "false", case-insensitive)
    pub fn parse_bool(&self) -> Result&lt;bool, ParseError&gt;
}
```

#### 2.3 String Splitting and Iteration

```novus
/// Iterator over string split by delimiter
///
/// Example:
/// ```novus
/// let path = Str::from_cstr("SYS:C/Dir")
/// let split = path.split($2F)  // '/' = 0x2F
/// for part in split {
///     // part is Str for each segment
/// }
/// ```
pub struct SplitIter {
    remaining: Str,
    delimiter: u8,
    done: bool,
}

impl Str {
    /// Split string by byte delimiter
    pub fn split(&self, delimiter: u8) -> SplitIter

    /// Split into lines (by LF, CR, or CRLF)
    pub fn lines(&self) -> SplitIter
}

impl SplitIter {
    /// Get next substring
    pub fn next(&mut self) -> Option&lt;Str&gt;
}
```

### 3. String Transformations

```novus
impl Str {
    /// Convert to uppercase (Latin-1)
    ///
    /// Returns new String with uppercase characters
    /// Allocates heap memory
    pub fn to_uppercase(&self) -> Result&lt;String, StringError&gt;

    /// Convert to lowercase (Latin-1)
    ///
    /// Returns new String with lowercase characters
    /// Allocates heap memory
    pub fn to_lowercase(&self) -> Result&lt;String, StringError&gt;

    /// Replace all occurrences of pattern with replacement
    ///
    /// Example:
    /// ```novus
    /// let s = Str::from_cstr("foo bar foo")
    /// let result = s.replace("foo", "baz")?
    /// // result is "baz bar baz"
    /// ```
    pub fn replace(&self, pattern: Str, replacement: Str) -> Result&lt;String, StringError&gt;

    /// Join array of strings with separator
    ///
    /// Example:
    /// ```novus
    /// let parts = ["SYS:", "C", "Dir"]
    /// let path = Str::join(&parts, "/")?
    /// // path is "SYS:/C/Dir"
    /// ```
    pub fn join(parts: &[Str], separator: Str) -> Result&lt;String, StringError&gt;
}
```

### 4. Path Manipulation (AmigaDOS-specific)

```novus
/// Namespace: std::string::path
///
/// AmigaDOS path manipulation utilities

/// Split AmigaDOS path into components
///
/// Example:
/// ```novus
/// let path = Str::from_cstr("Work:Projects/Novus/main.novus")
/// let parts = split_path(path)?
/// // parts.volume = Some("Work:")
/// // parts.directory = Some("Projects/Novus")
/// // parts.filename = Some("main.novus")
/// ```
pub struct PathComponents {
    pub volume: Option&lt;Str&gt;,      // "SYS:", "RAM:", etc.
    pub directory: Option&lt;Str&gt;,   // parent directories
    pub filename: Option&lt;Str&gt;,    // final component
}

pub fn split_path(path: Str) -> Result&lt;PathComponents, StringError&gt;

/// Join path components
pub fn join_path(volume: Option&lt;Str&gt;, directory: Option&lt;Str&gt;, filename: Str) -> Result&lt;String, StringError&gt;

/// Get filename from path (last component)
pub fn filename(path: Str) -> Option&lt;Str&gt;

/// Get directory from path (all but last component)
pub fn dirname(path: Str) -> Option&lt;Str&gt;

/// Check if path is absolute (has volume/device)
pub fn is_absolute(path: Str) -> bool

/// Normalize path (resolve "..", ".", etc.)
pub fn normalize(path: Str) -> Result&lt;String, StringError&gt;
```

---

## File I/O API

### Namespace: `std::io`

### 1. Error Types

```novus
/// File I/O errors (extends DosError)
pub enum IoError {
    /// DOS library error (wrapped)
    Dos(DosError),
    /// Unexpected EOF during read
    UnexpectedEof,
    /// Invalid UTF-8 sequence (for text reads)
    InvalidEncoding(u32),
    /// Buffer too small
    BufferTooSmall,
}

impl From&lt;DosError&gt; for IoError {
    fn convert(err: DosError) -> IoError {
        return IoError::Dos(err)
    }
}
```

### 2. File Opening and Closing

**Existing foundation:**
- `OwnedFileHandle` in `amiga::sys::dos` provides RAII wrapper
- Basic `read()`, `write()`, `seek()` methods

**Enhancement: File struct with more ergonomics**

```novus
/// High-level file handle with buffering and convenience methods
///
/// Wraps OwnedFileHandle with additional features:
/// - Buffered reads for efficiency
/// - Line-by-line reading
/// - Automatic DOS error checking
///
/// Example:
/// ```novus
/// let file = File::open("S:Startup-Sequence")?
/// let contents = file.read_to_string()?
/// ```
pub struct File {
    handle: OwnedFileHandle,
    path: String,  // Store for error messages
}

impl File {
    /// Open file for reading
    ///
    /// Mode: MODE_OLDFILE (read existing file)
    pub fn open(path: Str) -> Result&lt;File, IoError&gt;

    /// Create new file (truncate if exists)
    ///
    /// Mode: MODE_NEWFILE
    pub fn create(path: Str) -> Result&lt;File, IoError&gt;

    /// Open file for append
    ///
    /// Opens existing file and seeks to end
    pub fn append(path: Str) -> Result&lt;File, IoError&gt;

    /// Open with specific mode
    ///
    /// Modes: MODE_OLDFILE, MODE_NEWFILE, MODE_READWRITE
    pub fn open_mode(path: Str, mode: i32) -> Result&lt;File, IoError&gt;

    /// Get file path (for error messages)
    pub fn path(&self) -> Str

    /// Get underlying BPTR handle (for FFI)
    pub fn handle(&self) -> i32
}
```

### 3. Reading Operations

```novus
impl File {
    /// Read bytes into buffer
    ///
    /// Returns number of bytes actually read (may be less than buffer size)
    /// Returns 0 on EOF
    ///
    /// Example:
    /// ```novus
    /// var buffer: [u8; 1024]
    /// let bytes_read = file.read(&buffer)?
    /// ```
    pub fn read(&self, buffer: *u8, len: u32) -> Result&lt;u32, IoError&gt;

    /// Read exact number of bytes (error if EOF before completion)
    ///
    /// Unlike read(), this guarantees the full buffer is filled or returns error
    pub fn read_exact(&self, buffer: *u8, len: u32) -> Result&lt;(), IoError&gt;

    /// Read entire file into String
    ///
    /// Efficient for small-medium files (&lt;1MB)
    /// Allocates heap memory for contents
    ///
    /// Example:
    /// ```novus
    /// let contents = File::read_to_string("S:Startup-Sequence")?
    /// ```
    pub fn read_to_string(&self) -> Result&lt;String, IoError&gt;

    /// Read entire file into Vec&lt;u8&gt;
    pub fn read_to_vec(&self) -> Result&lt;Vec&lt;u8&gt;, IoError&gt;

    /// Read single line (up to LF or CRLF)
    ///
    /// Returns line without line ending
    /// Returns None on EOF
    ///
    /// Example:
    /// ```novus
    /// while let Some(line) = file.read_line()? {
    ///     // process line
    /// }
    /// ```
    pub fn read_line(&mut self) -> Result&lt;Option&lt;String&gt;, IoError&gt;
}
```

### 4. Writing Operations

```novus
impl File {
    /// Write bytes to file
    ///
    /// Returns number of bytes written
    /// Error if write fails or disk full
    pub fn write(&self, buffer: *u8, len: u32) -> Result&lt;u32, IoError&gt;

    /// Write all bytes (error if any bytes not written)
    pub fn write_all(&self, buffer: *u8, len: u32) -> Result&lt;(), IoError&gt;

    /// Write string
    ///
    /// Example:
    /// ```novus
    /// file.write_str("Hello, Amiga!\n")?
    /// ```
    pub fn write_str(&self, s: Str) -> Result&lt;(), IoError&gt;

    /// Write string with newline
    pub fn write_line(&self, s: Str) -> Result&lt;(), IoError&gt;

    /// Write formatted output (like Printf)
    ///
    /// Uses FPrintf under the hood
    /// Example:
    /// ```novus
    /// file.write_fmt("Version: {}.{}\n", &[
    ///     FormatArg::I32(1),
    ///     FormatArg::I32(0)
    /// ])?
    /// ```
    pub fn write_fmt(&self, template: Str, args: &[FormatArg]) -> Result&lt;(), IoError&gt;

    /// Flush buffered writes to disk
    ///
    /// AmigaDOS doesn't have explicit flush, but we can simulate with Seek
    pub fn flush(&self) -> Result&lt;(), IoError&gt;
}
```

### 5. Seeking Operations

```novus
/// Seek position modes
pub enum SeekFrom {
    Start(i32),     // OFFSET_BEGINNING
    End(i32),       // OFFSET_END
    Current(i32),   // OFFSET_CURRENT
}

impl File {
    /// Seek to position
    ///
    /// Returns old position
    ///
    /// Example:
    /// ```novus
    /// file.seek(SeekFrom::Start(0))?  // Rewind to beginning
    /// let size = file.seek(SeekFrom::End(0))?  // Get file size
    /// ```
    pub fn seek(&self, pos: SeekFrom) -> Result&lt;i32, IoError&gt;

    /// Get current file position
    pub fn tell(&self) -> Result&lt;i32, IoError&gt;

    /// Rewind to beginning
    pub fn rewind(&self) -> Result&lt;(), IoError&gt;
}
```

### 6. File Metadata

```novus
/// File information
pub struct FileInfo {
    pub size: u32,
    pub is_file: bool,
    pub is_directory: bool,
    pub protection: u32,  // FIBF_* flags
}

impl File {
    /// Get file metadata
    ///
    /// Uses ExamineFH() under the hood
    pub fn metadata(&self) -> Result&lt;FileInfo, IoError&gt;

    /// Get file size without seeking
    pub fn size(&self) -> Result&lt;u32, IoError&gt;
}
```

### 7. Convenience Functions

```novus
/// Read entire file into String (one-liner)
///
/// Example:
/// ```novus
/// let config = read_to_string("S:User-Startup")?
/// ```
pub fn read_to_string(path: Str) -> Result&lt;String, IoError&gt;

/// Read entire file into Vec&lt;u8&gt;
pub fn read_to_vec(path: Str) -> Result&lt;Vec&lt;u8&gt;, IoError&gt;

/// Write string to file (create/truncate)
///
/// Example:
/// ```novus
/// write_string("RAM:test.txt", "Hello, World!")?
/// ```
pub fn write_string(path: Str, contents: Str) -> Result&lt;(), IoError&gt;

/// Append string to file
pub fn append_string(path: Str, contents: Str) -> Result&lt;(), IoError&gt;

/// Copy file
pub fn copy(from: Str, to: Str) -> Result&lt;(), IoError&gt;
```

### 8. Buffered I/O

```novus
/// Buffered reader for efficient line-by-line reading
///
/// Uses 4KB internal buffer to minimize DOS Read() calls
///
/// Example:
/// ```novus
/// let file = File::open("S:Startup-Sequence")?
/// var reader = BufReader::new(file)
/// while let Some(line) = reader.read_line()? {
///     // process line
/// }
/// ```
pub struct BufReader {
    file: File,
    buffer: [u8; 4096],
    pos: u32,      // Current position in buffer
    filled: u32,   // How many bytes valid in buffer
}

impl BufReader {
    /// Create buffered reader
    pub fn new(file: File) -> BufReader

    /// Read line into String
    pub fn read_line(&mut self) -> Result&lt;Option&lt;String&gt;, IoError&gt;

    /// Read bytes into buffer (buffered)
    pub fn read(&mut self, buffer: *u8, len: u32) -> Result&lt;u32, IoError&gt;

    /// Get underlying file
    pub fn into_file(consuming self) -> File
}

/// Buffered writer for efficient small writes
///
/// Batches writes into 4KB chunks to minimize DOS Write() calls
pub struct BufWriter {
    file: File,
    buffer: [u8; 4096],
    filled: u32,
}

impl BufWriter {
    /// Create buffered writer
    pub fn new(file: File) -> BufWriter

    /// Write bytes (buffered)
    pub fn write(&mut self, buffer: *u8, len: u32) -> Result&lt;(), IoError&gt;

    /// Write string
    pub fn write_str(&mut self, s: Str) -> Result&lt;(), IoError&gt;

    /// Flush buffer to disk
    pub fn flush(&mut self) -> Result&lt;(), IoError&gt;

    /// Get underlying file (flushes first)
    pub fn into_file(consuming mut self) -> Result&lt;File, IoError&gt;
}

impl Drop for BufWriter {
    fn drop(&mut self) {
        // Auto-flush on drop (ignore errors since we can't return them)
        let _ = self.flush()
    }
}
```

---

## CLI Argument Parsing API

### Namespace: `amiga::workbench`

### Design: Wrapper around AmigaDOS ReadArgs()

AmigaDOS ReadArgs() is the professional way to parse CLI arguments on Amiga. It provides:
- Template-based parsing (declarative)
- Automatic type checking
- Built-in help generation
- Keyword arguments
- Multi-value arguments

### 1. Error Types

```novus
/// Argument parsing errors
pub enum ArgsError {
    /// DOS error during ReadArgs
    Dos(DosError),
    /// Template syntax error
    InvalidTemplate,
    /// Required argument missing
    MissingRequired(String),
    /// Invalid argument value
    InvalidValue(String),
    /// Too many arguments
    TooManyArgs,
}

impl From&lt;DosError&gt; for ArgsError {
    fn convert(err: DosError) -> ArgsError {
        return ArgsError::Dos(err)
    }
}
```

### 2. Core API

```novus
/// Parsed command-line arguments
///
/// Wraps ReadArgs() with safe, typed access
///
/// Example:
/// ```novus
/// let args = Args::parse("FROM/A,TO/A,VERBOSE/S")?
/// let from = args.get_str("FROM")
/// let to = args.get_str("TO")
/// let verbose = args.get_switch("VERBOSE")
/// ```
pub struct Args {
    rdargs: *RDArgs,      // Owned RDArgs from ReadArgs
    results: Vec&lt;i32&gt;,    // Result array (one slot per template item)
    template: String,     // Template string (for error messages)
}

impl Args {
    /// Parse command-line arguments using template
    ///
    /// Template syntax:
    /// - `NAME` - positional argument
    /// - `NAME/A` - required argument
    /// - `NAME/S` - switch (boolean flag)
    /// - `NAME/K` - keyword argument (NAME=value)
    /// - `NAME/N` - numeric argument
    /// - `NAME/M` - multiple arguments
    /// - `NAME/F` - rest of line
    ///
    /// Example templates:
    /// ```
    /// "FILE/A"                  - Required filename
    /// "FROM/A,TO/A,VERBOSE/S"   - Two required + one switch
    /// "FILES/M"                 - Multiple files
    /// "COUNT/N/A"               - Required number
    /// ```
    pub fn parse(template: Str) -> Result&lt;Args, ArgsError&gt;

    /// Get string argument value
    ///
    /// Returns None if argument wasn't provided (only for optional args)
    pub fn get_str(&self, name: Str) -> Option&lt;Str&gt;

    /// Get switch value (true if present, false if not)
    pub fn get_switch(&self, name: Str) -> bool

    /// Get numeric argument value
    pub fn get_number(&self, name: Str) -> Option&lt;i32&gt;

    /// Get multiple string arguments
    ///
    /// For `/M` arguments - returns array of strings
    pub fn get_multi(&self, name: Str) -> Option&lt;Vec&lt;Str&gt;&gt;

    /// Check if argument was provided
    pub fn is_present(&self, name: Str) -> bool
}

impl Drop for Args {
    fn drop(&mut self) {
        // Free RDArgs structure
        if (u32)self.rdargs != 0 {
            unsafe {
                FreeArgs(self.rdargs)
            }
            self.rdargs = (*RDArgs)0
        }
    }
}
```

### 3. Builder API (Alternative)

For more complex scenarios, provide a builder:

```novus
/// ArgParser builder - programmatic template construction
///
/// Example:
/// ```novus
/// var parser = ArgParser::new()
/// parser.arg("FROM", ArgType::Required)
/// parser.arg("TO", ArgType::Required)
/// parser.arg("VERBOSE", ArgType::Switch)
/// let args = parser.parse()?
/// ```
pub struct ArgParser {
    template: String,
    arg_names: Vec&lt;String&gt;,
}

pub enum ArgType {
    Required,      // /A
    Optional,      // (none)
    Switch,        // /S
    Keyword,       // /K
    Number,        // /N
    Multi,         // /M
    RestOfLine,    // /F
}

impl ArgParser {
    /// Create new parser
    pub fn new() -> ArgParser

    /// Add argument to template
    pub fn arg(&mut self, name: Str, arg_type: ArgType) -> bool

    /// Parse arguments
    pub fn parse(&self) -> Result&lt;Args, ArgsError&gt;

    /// Get generated template string
    pub fn template(&self) -> Str
}
```

### 4. Workbench Support

```novus
/// Check if launched from Workbench
///
/// Returns true if launched from icon, false from CLI
pub fn is_workbench() -> bool

/// Get Workbench startup message
///
/// Returns None if launched from CLI
///
/// Example:
/// ```novus
/// if let Some(wb) = get_workbench_startup() {
///     // Process icon tooltypes and clicked files
///     for i in 0..wb.num_args() {
///         let arg = wb.arg(i)
///         // arg.lock() and arg.name() give file info
///     }
/// }
/// ```
pub fn get_workbench_startup() -> Option&lt;WBStartup&gt;

/// Workbench startup message wrapper
pub struct WBStartup {
    msg: *WBStartupMsg,  // Raw pointer (borrowed, don't free!)
}

impl WBStartup {
    /// Get number of arguments (files/icons clicked)
    pub fn num_args(&self) -> u32

    /// Get argument at index
    pub fn arg(&self, index: u32) -> Option&lt;WBArg&gt;

    /// Get tool window specification
    pub fn tool_window(&self) -> Option&lt;Str&gt;
}

/// Workbench argument (file or icon)
pub struct WBArg {
    lock: i32,    // DOS lock on directory
    name: Str,    // Filename
}

impl WBArg {
    /// Get directory lock
    pub fn lock(&self) -> i32

    /// Get filename (not full path!)
    pub fn name(&self) -> Str

    /// Get full path by combining lock and name
    pub fn full_path(&self) -> Result&lt;String, DosError&gt;
}
```

### 5. Help Text Generation

```novus
impl Args {
    /// Print help text to stdout
    ///
    /// Generates help from template:
    /// Template: "FROM/A,TO/A,VERBOSE/S"
    /// Output:
    /// ```
    /// Usage: myprogram FROM/A TO/A [VERBOSE/S]
    ///   FROM/A      - Required source file
    ///   TO/A        - Required destination file
    ///   VERBOSE/S   - Enable verbose output
    /// ```
    pub fn print_help(&self, program_name: Str, descriptions: &[(Str, Str)])
}
```

---

## Complete Example Program

This example demonstrates all three APIs working together to build a real CLI tool:

```novus
// filecat - Concatenate files and print to stdout
//
// Usage: filecat FILES/M OUTPUT/K VERBOSE/S
//
// Example:
//   filecat file1.txt file2.txt OUTPUT=combined.txt VERBOSE

from amiga::workbench import Args, is_workbench
from std::io import File, BufReader, write_string, IoError
from std::string import StringBuilder
from amiga::sys::dos import DosError
from std::io import write

pub fn main() -> i32 {
    // Check if launched from Workbench
    if is_workbench() {
        write("ERROR: filecat must be run from CLI\n")
        return 10
    }

    // Parse command-line arguments
    let args = Args::parse("FILES/M,OUTPUT/K,VERBOSE/S") or {
        write("Usage: filecat FILES/M [OUTPUT/K] [VERBOSE/S]\n")
        write("  FILES/M   - Input files to concatenate\n")
        write("  OUTPUT/K  - Output file (default: stdout)\n")
        write("  VERBOSE/S - Show progress\n")
        return 10
    }

    // Get arguments
    let files_opt = args.get_multi("FILES")
    let output_path = args.get_str("OUTPUT")
    let verbose = args.get_switch("VERBOSE")

    // Validate: FILES is required
    let files = files_opt or {
        write("ERROR: No input files specified\n")
        return 10
    }

    if verbose {
        write("Concatenating files...\n")
    }

    // Build output string
    var builder = StringBuilder::with_capacity(8192) or {
        write("ERROR: Out of memory\n")
        return 20
    }

    // Process each input file
    for file_path in files {
        if verbose {
            write("Reading: ")
            write(file_path.as_cstr())
            write("\n")
        }

        // Read file contents
        let contents = read_file_to_string(file_path) or {
            write("ERROR: Failed to read ")
            write(file_path.as_cstr())
            write("\n")
            return 20
        }

        // Append to builder
        if !builder.push_str(contents.as_str()) {
            write("ERROR: Out of memory\n")
            return 20
        }
    }

    let result = builder.build()

    // Write output
    match output_path {
        Some(path) => {
            // Write to file
            if verbose {
                write("Writing to: ")
                write(path.as_cstr())
                write("\n")
            }

            write_string(path, result.as_str()) or {
                write("ERROR: Failed to write output file\n")
                return 20
            }

            if verbose {
                write("Done!\n")
            }
        },
        None => {
            // Write to stdout
            write(result.as_cstr())
        }
    }

    return 0
}

// Helper: read file to string with error handling
fn read_file_to_string(path: Str) -> Result&lt;String, IoError&gt; {
    let file = File::open(path)?
    return file.read_to_string()
}
```

### More Examples

#### Example 1: Simple File Copy

```novus
from amiga::workbench import Args
from std::io import copy, IoError

pub fn main() -> i32 {
    let args = Args::parse("FROM/A,TO/A") or {
        return 10
    }

    let from = args.get_str("FROM") or { return 10 }
    let to = args.get_str("TO") or { return 10 }

    copy(from, to) or {
        return 20
    }

    return 0
}
```

#### Example 2: Line Counter

```novus
from amiga::workbench import Args
from std::io import File, BufReader
from std::io import write

pub fn main() -> i32 {
    let args = Args::parse("FILE/A") or { return 10 }
    let filepath = args.get_str("FILE") or { return 10 }

    let file = File::open(filepath) or {
        write("ERROR: Cannot open file\n")
        return 20
    }

    var reader = BufReader::new(file)
    var line_count: u32 = 0

    while let Some(line) = reader.read_line() or {
        write("ERROR: Read failed\n")
        return 20
    } {
        line_count++
    }

    // Print result using io::write with formatted args
    let args_array: [i32; 1] = [line_count as i32]
    write_array("Lines: %ld\n", &args_array)

    return 0
}
```

#### Example 3: Config File Parser

```novus
from amiga::workbench import Args
from std::io import read_to_string
from std::string import Str

pub fn main() -> i32 {
    let args = Args::parse("CONFIG/A") or { return 10 }
    let config_path = args.get_str("CONFIG") or { return 10 }

    // Read config file
    let contents = read_to_string(config_path) or {
        return 20
    }

    // Parse line by line
    let lines = contents.as_str().lines()
    while let Some(line) = lines.next() {
        let trimmed = line.trim()

        // Skip empty lines and comments
        if trimmed.is_empty() || trimmed.starts_with("#") {
            continue
        }

        // Parse key=value
        if let Some(eq_pos) = trimmed.find_byte($3D) {  // '=' = 0x3D
            let key = trimmed.slice_to(eq_pos) or { continue }
            let value = trimmed.slice_from(eq_pos + 1) or { continue }

            // Process key/value pair
            process_config(key.trim(), value.trim())
        }
    }

    return 0
}

fn process_config(key: Str, value: Str) {
    // Handle configuration
}
```

---

## Implementation Notes

### 1. String Formatting Implementation

Integer formatting uses stack-allocated scratch buffer:

```novus
impl StringBuilder {
    pub fn push_i32(&mut self, value: i32) -> bool {
        var buffer: [u8; 12]  // Max: "-2147483648" = 11 chars + null
        let len = format_i32(value, &buffer)
        let s = Str::borrow_raw(&buffer, len)
        return self.push_str(s)
    }
}

// Helper: format i32 to buffer, return length
fn format_i32(value: i32, buffer: *u8) -> u32 {
    // Implementation: convert to string manually
    // Handle negative, digit-by-digit conversion
    // Return length (don't null-terminate for internal use)
}
```

### 2. File I/O Buffering

BufReader uses circular buffer strategy:
- 4KB buffer in struct (stack or heap depending on usage)
- Track `pos` (read position) and `filled` (valid bytes)
- On `read_line()`, scan buffer for LF
- If LF found, return line; else refill buffer from file
- Minimize DOS Read() syscalls (expensive on floppy!)

### 3. ReadArgs Integration

Args struct owns RDArgs pointer:
- `ReadArgs()` allocates RDArgs internally
- Store results in Vec&lt;i32&gt; (one slot per template argument)
- Map template arg names to indices for `get_str()` etc.
- `Drop` implementation calls `FreeArgs()`

Template parsing:
```novus
// Template: "FROM/A,TO/A,VERBOSE/S"
// Results:  [ptr_to_from, ptr_to_to, -1_if_verbose_else_0]
//
// get_str("FROM") -> results[0] as *u8
// get_switch("VERBOSE") -> results[2] != 0
```

### 4. Memory Management

**String allocations:**
- String and StringBuilder use Vec&lt;u8&gt; (heap, fast RAM)
- format() returns new String (caller owns)
- All parse functions return Result (no silent failures)

**File handles:**
- File wraps OwnedFileHandle (RAII)
- Drop auto-closes file
- BufReader/BufWriter own File, auto-flush on drop

**Args cleanup:**
- Drop calls FreeArgs() (critical - ReadArgs allocates!)
- Results Vec cleaned up by Vec's Drop

### 5. Error Propagation

Use `?` operator for clean error bubbling:

```novus
pub fn copy(from: Str, to: Str) -> Result&lt;(), IoError&gt; {
    let src = File::open(from)?       // DosError auto-converts to IoError
    let dst = File::create(to)?

    var buffer: [u8; 8192]
    loop {
        let bytes_read = src.read(&buffer, 8192)?
        if bytes_read == 0 {
            break  // EOF
        }
        dst.write_all(&buffer, bytes_read)?
    }

    return Result::Ok(())
}
```

From&lt;DosError&gt; for IoError enables automatic conversion.

---

## Migration Guide

### From Current stdlib to New API

#### String Operations

**Before:**
```novus
var s = String::new()
s.push_str("Hello")
s.push_str(" World")
```

**After (same - no change needed):**
```novus
var builder = StringBuilder::new()
builder.push_str("Hello")
builder.push_str(" World")
let result = builder.build()
```

**New capability - formatting:**
```novus
var builder = StringBuilder::new()
builder.push_str("Count: ")
builder.push_i32(42)
let s = builder.build()
```

#### File I/O

**Before (low-level):**
```novus
from amiga::sys::dos import open_file, read_file, close_file

let fh = open_file("S:Startup-Sequence", MODE_OLDFILE) or { return 1 }
defer { close_file(fh) }

var buffer: [u8; 1024]
let bytes = read_file(fh, &buffer, 1024)
```

**After (high-level):**
```novus
from std::io import File

let file = File::open("S:Startup-Sequence") or { return 1 }
// Auto-closes via Drop, no defer needed!

var buffer: [u8; 1024]
let bytes = file.read(&buffer, 1024) or { return 1 }
```

**New capability - one-liners:**
```novus
let contents = read_to_string("S:Startup-Sequence") or { return 1 }
```

#### CLI Arguments

**Before (manual):**
```novus
// No high-level API yet
// Would need to use ReadArgs FFI directly
```

**After:**
```novus
let args = Args::parse("FROM/A,TO/A") or { return 10 }
let from = args.get_str("FROM") or { return 10 }
let to = args.get_str("TO") or { return 10 }
```

### Backward Compatibility

All existing APIs remain:
- `Str`, `String`, fixed-size string types unchanged
- `OwnedFileHandle` still available (File wraps it)
- Low-level `open_file()`, `read_file()` still work

New APIs are additive, not breaking.

---

## Next Steps for Implementation

### Phase 1: String Enhancements (Week 1)
- [ ] Implement `StringBuilder` as wrapper around `String`
- [ ] Add `parse_i32()`, `parse_u32()`, `parse_hex()` to Str
- [ ] Add `format_i32()`, `format_u32()` helpers
- [ ] Implement `SplitIter` for string splitting
- [ ] Add path manipulation functions

### Phase 2: File I/O Enhancements (Week 2)
- [ ] Implement `File` wrapper around `OwnedFileHandle`
- [ ] Add `read_to_string()` and `read_to_vec()` convenience functions
- [ ] Implement `BufReader` with line-by-line reading
- [ ] Implement `BufWriter` with auto-flush on drop
- [ ] Add `SeekFrom` enum and ergonomic seek methods
- [ ] Add convenience functions: `copy()`, `write_string()`

### Phase 3: CLI Arguments (Week 3)
- [ ] Implement `Args` wrapper around ReadArgs
- [ ] Add template parsing and name-to-index mapping
- [ ] Implement `get_str()`, `get_switch()`, `get_number()`
- [ ] Add Workbench detection and `WBStartup` wrapper
- [ ] Implement help text generation
- [ ] Add `ArgParser` builder API

### Phase 4: Testing and Documentation (Week 4)
- [ ] Write unit tests for all new APIs
- [ ] Test with real Amiga binaries (FS-UAE)
- [ ] Write comprehensive examples
- [ ] Update stdlib reference docs
- [ ] Create migration guide for existing code

---

## Appendices

### A. Complete Type Reference

```novus
// std::string
pub struct StringBuilder { ... }
pub enum ParseError { ... }
pub struct SplitIter { ... }
pub struct PathComponents { ... }
pub enum FormatArg { ... }

// std::io
pub struct File { ... }
pub struct BufReader { ... }
pub struct BufWriter { ... }
pub enum IoError { ... }
pub enum SeekFrom { ... }
pub struct FileInfo { ... }

// amiga::workbench
pub struct Args { ... }
pub struct ArgParser { ... }
pub enum ArgType { ... }
pub enum ArgsError { ... }
pub struct WBStartup { ... }
pub struct WBArg { ... }
```

### B. Complete Function Reference

**std::string:**
- `format(template: Str, args: &[FormatArg]) -> Result&lt;String, StringError&gt;`
- `split_path(path: Str) -> Result&lt;PathComponents, StringError&gt;`
- `join_path(...) -> Result&lt;String, StringError&gt;`
- `filename(path: Str) -> Option&lt;Str&gt;`
- `dirname(path: Str) -> Option&lt;Str&gt;`
- `is_absolute(path: Str) -> bool`
- `normalize(path: Str) -> Result&lt;String, StringError&gt;`

**std::io:**
- `read_to_string(path: Str) -> Result&lt;String, IoError&gt;`
- `read_to_vec(path: Str) -> Result&lt;Vec&lt;u8&gt;, IoError&gt;`
- `write_string(path: Str, contents: Str) -> Result&lt;(), IoError&gt;`
- `append_string(path: Str, contents: Str) -> Result&lt;(), IoError&gt;`
- `copy(from: Str, to: Str) -> Result&lt;(), IoError&gt;`

**amiga::workbench:**
- `is_workbench() -> bool`
- `get_workbench_startup() -> Option&lt;WBStartup&gt;`

---

**End of Design Document**
