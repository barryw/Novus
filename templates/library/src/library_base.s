; library_base.s - ROMTag and library initialization structures
;
; This file contains the low-level assembly code that makes an AmigaOS library:
; - ROMTag (Resident structure) for library identification
; - AutoInit structure for automatic initialization
; - Function vector table for library functions
;
; MEMORY LAYOUT:
; +-------------------+
; | ROMTag            | <- Scanned by exec.library during boot
; +-------------------+
; | LibName string    |
; | LibIDString       |
; +-------------------+
; | AutoInit struct   | <- Points to InitTable
; +-------------------+
; | InitTable         | <- Describes library structure
; +-------------------+
; | Function vectors  | <- Jump table for library functions
; +-------------------+

    XDEF    _LibStart       ; Library entry point (for linker)
    XDEF    _LibName        ; Library name (for reference)
    XDEF    _LibIDString    ; ID string (for version command)

    XREF    _LibInit        ; From lib.novus (Novus code)
    XREF    _LibOpen        ; From wrappers.s (assembly wrapper)
    XREF    _LibClose       ; From wrappers.s (assembly wrapper)
    XREF    _LibExpunge     ; From wrappers.s (assembly wrapper)
    XREF    _LibReserved    ; From wrappers.s (assembly wrapper)
    XREF    _GetVersion     ; From wrappers.s (assembly wrapper)
    XREF    _Multiply       ; From wrappers.s (assembly wrapper)

; ============================================================================
; Library Entry Point - The ROMTag (Resident Structure)
; ============================================================================
; The ROMTag is a special structure that exec.library scans for during system
; initialization. It identifies this as a loadable module and provides
; information needed to initialize it.
;
; STRUCTURE (14 bytes):
; +0: UWORD  MatchWord   (0x4AFC = RTC_MATCHWORD)
; +2: APTR   MatchTag    (points back to +0, for validation)
; +4: APTR   EndSkip     (end of ROMTag, exec skips to here)
; +6: UBYTE  Flags       (RTF_AUTOINIT = automatic initialization)
; +7: UBYTE  Version     (library version)
; +8: UBYTE  Type        (NT_LIBRARY = 9)
; +9: BYTE   Priority    (initialization priority, 0 = normal)
; +10: APTR  Name        (pointer to library name string)
; +12: APTR  IDString    (pointer to version string)
; +14: APTR  Init        (pointer to AutoInit structure)

_LibStart:
ROMTag:
    dc.w    $4AFC           ; RT_MATCHWORD (magic cookie, identifies ROMTag)
                           ; exec.library scans memory for this value

    dc.l    ROMTag          ; RT_MATCHTAG (points to self for validation)
                           ; exec verifies: if (*MatchTag == MatchWord) -> valid

    dc.l    EndCode         ; RT_ENDSKIP (end of resident structure)
                           ; exec resumes scanning here after processing ROMTag

    dc.b    $81             ; RT_FLAGS = RTF_AUTOINIT (0x80) | RTF_COLDSTART (0x01)
                           ; RTF_AUTOINIT: use AutoInit structure for initialization
                           ; RTF_COLDSTART: initialize during cold boot

    dc.b    1               ; RT_VERSION (library version = 1)
                           ; Must match version in MakeFunctions

    dc.b    9               ; RT_TYPE = NT_LIBRARY (9)
                           ; Other types: NT_DEVICE (3), NT_RESOURCE (8)

    dc.b    0               ; RT_PRI (priority = 0, normal)
                           ; Higher priority libraries init first

    dc.l    _LibName        ; RT_NAME (pointer to library name)
                           ; Used by exec to identify the library

    dc.l    _LibIDString    ; RT_IDSTRING (pointer to ID string)
                           ; Shown by "version example.library" command

    dc.l    AutoInit        ; RT_INIT (pointer to AutoInit structure)
                           ; exec.library uses this to initialize the library

; ============================================================================
; Library Name and Version String
; ============================================================================

_LibName:
    dc.b    'example.library',0  ; Library name (null-terminated)
                                 ; MUST end with ".library" for LoadLibrary to find it

    CNOP    0,2                  ; Word-align for efficiency

_LibIDString:
    dc.b    'example.library 1.0 (01 Nov 2025)',13,10,0
                                 ; Version string format:
                                 ; "name version.revision (date)"
                                 ; Used by "version example.library" command

    CNOP    0,2                  ; Word-align for next structure

; ============================================================================
; AutoInit Structure
; ============================================================================
; The AutoInit structure tells exec.library how to automatically initialize
; the library. It's used when RT_FLAGS has RTF_AUTOINIT set.
;
; STRUCTURE (16 bytes):
; +0: ULONG  DataSize    (size of library base structure)
; +4: APTR   Vectors     (pointer to function vector table)
; +8: APTR   Structure   (pointer to InitTable, for MakeLibrary)
; +12: APTR  InitFunc    (pointer to LibInit function)

AutoInit:
    dc.l    ExampleLibraryBase_SIZEOF  ; DataSize (size of library base)
                                       ; exec allocates this much memory
                                       ; Must be large enough for Library + custom data

    dc.l    FuncTable                  ; Vectors (pointer to function table)
                                       ; Contains the library's function vectors

    dc.l    InitTable                  ; Structure (pointer to InitTable)
                                       ; Describes library structure for MakeLibrary

    dc.l    _LibInit                   ; InitFunc (pointer to init function)
                                       ; Called after MakeLibrary creates base
                                       ; Returns library base or 0 on failure

; ============================================================================
; InitTable - Library Structure Description
; ============================================================================
; This table describes the library base structure for MakeLibrary.
; It's used to automatically initialize the Library header fields.
;
; FORMAT:
; ULONG: Initialization type (INITBYTE, INITWORD, INITLONG)
; ULONG: Offset in structure
; ULONG/UWORD/UBYTE: Value to initialize
; ULONG: 0 (terminator)

InitTable:
    ; Library node type
    dc.b    0xA0            ; INITBYTE (initialize one byte)
    dc.b    8               ; Offset: ln_Type (Library.lib_Node.ln_Type)
    dc.b    9               ; Value: NT_LIBRARY
    dc.b    0               ; Padding

    ; Library version
    dc.b    0xA0            ; INITBYTE
    dc.b    20              ; Offset: lib_Version
    dc.b    1               ; Value: Version 1
    dc.b    0               ; Padding

    ; Library revision
    dc.b    0xA0            ; INITBYTE
    dc.b    21              ; Offset: lib_Revision
    dc.b    0               ; Value: Revision 0
    dc.b    0               ; Padding

    ; Library ID string
    dc.b    0x80            ; INITLONG (initialize a longword)
    dc.b    24              ; Offset: lib_IdString
    dc.l    _LibIDString    ; Value: pointer to ID string

    ; Terminator
    dc.l    0               ; End of InitTable

; ============================================================================
; Function Vector Table
; ============================================================================
; This table defines the library's function vectors. Each entry points to
; a wrapper function that handles the A6-relative calling convention.
;
; LAYOUT IN MEMORY (negative offsets from library base):
; Base-4:  Points to FuncTable[-1] (start of table, for safety)
; Base-6:  Open function
; Base-12: Close function
; Base-18: Expunge function
; Base-24: Reserved function
; Base-30: GetVersion function (first user function)
; Base-36: Multiply function (second user function)
;
; ADDING NEW FUNCTIONS:
; To add a new function at offset -42:
; 1. Add "dc.l _YourFunction" before the terminator
; 2. Implement _YourFunction in wrappers.s
; 3. Increment NegSize below by 6

FuncTable:
    dc.l    _LibOpen        ; Vector 0: Open (-6)
                           ; Called by OpenLibrary()

    dc.l    _LibClose       ; Vector 1: Close (-12)
                           ; Called by CloseLibrary()

    dc.l    _LibExpunge     ; Vector 2: Expunge (-18)
                           ; Called by RemLibrary() or during expunge

    dc.l    _LibReserved    ; Vector 3: Reserved (-24)
                           ; Reserved for future use, must return 0

    ; User-callable functions start here
    dc.l    _GetVersion     ; Vector 4: GetVersion (-30)
                           ; Returns library version

    dc.l    _Multiply       ; Vector 5: Multiply (-36)
                           ; Multiplies two numbers

    dc.l    -1              ; Terminator (0xFFFFFFFF)
                           ; Marks end of function table

; ============================================================================
; Constants
; ============================================================================

; Size of ExampleLibraryBase structure
; This MUST match the actual structure size from lib.novus
; Library = 34 bytes (standard header)
; + seglist (4 bytes)
; + open_count (4 bytes)
; = 42 bytes total
ExampleLibraryBase_SIZEOF equ 42

; Negative size (for MakeLibrary)
; This is the size of the function vector table
; 6 functions * 6 bytes per vector = 36 bytes
; Format: (number_of_vectors * 6)
; Update this when adding new functions!
NegSize equ 36

EndCode:
    ; End of library code
    ; exec.library resumes scanning here after processing ROMTag

    END
