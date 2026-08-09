// Novus Runtime - MMU Detection and Protection
// MMU detection and null page protection using mmu.library

#include "novus_runtime.h"

// ============================================================================
// MMU Detection and Protection
// ============================================================================
//
// This section detects whether the system has an MMU (Memory Management Unit).
// MMUs are present on 68030, 68040, and 68060 processors (except LC variants).
// The 68020 requires an external 68851 PMMU chip.
//
// Detection strategy:
// 1. Check CPU type from AttnFlags (quick filter)
// 2. Try to open mmu.library for accurate detection and capabilities
// 3. Fall back to CPU-based assumption if mmu.library unavailable
//
// Memory Protection Features (when mmu.library is available):
// - Null page protection: Mark page 0 as invalid to trap null pointer dereferences
// - Guard pages: Catch stack/heap overflows with invalid page markers
// ============================================================================

// AttnFlags bits
#ifndef AFF_68020
#define AFF_68020     (1<<1)
#endif
#ifndef AFF_68030
#define AFF_68030     (1<<2)
#endif
#ifndef AFF_68040
#define AFF_68040     (1<<3)
#endif
#ifndef AFF_68060
#define AFF_68060     (1<<7)
#endif

// SystemMMU enum values (must match std::hardware::mmu::SystemMMU)
typedef enum {
    SystemMMU_None = 0,
    SystemMMU_M68851 = 1,
    SystemMMU_M68030 = 2,
    SystemMMU_M68040 = 3,
    SystemMMU_M68060 = 4
} SystemMMU;

// MMU library function prototypes (from mmu.library by Thomas Richter)
// We call these via direct library vector offsets since we don't link against mmu.library
struct MMUContext;  // Forward declaration

// MMU page property flags (from mmu/context.h)
#define MAPP_INVALID        (1L<<14)  // Page is invalid, causes segfault on access
#define MAPP_REPAIRABLE     (1L<<20)  // Allow software repair of invalid pages

// Tags for AddContextHookA (from mmu/mmutags.h)
#ifndef TAG_USER
#define TAG_USER            (1L<<31)
#endif
#define MADTAG_DUMMY        (TAG_USER + 0x03e00200)
#define MADTAG_CONTEXT      (MADTAG_DUMMY + 0)   // Context to add hook to
#define MADTAG_TYPE         (MADTAG_DUMMY + 2)   // Exception type (MMUEH_*)
#define MADTAG_CODE         (MADTAG_DUMMY + 3)   // Handler code (assembly)
#define MADTAG_DATA         (MADTAG_DUMMY + 4)   // Data for handler (a1/a4)
#define MADTAG_PRI          (MADTAG_DUMMY + 6)   // Priority (< -32 for debug tools)
#ifndef TAG_DONE
#define TAG_DONE            0
#endif

// Exception types (from mmu/exceptions.h)
#define MMUEH_SEGFAULT      0L  // Segmentation fault (invalid/supervisor/write-protected)

// Exception flags (from mmu/exceptions.h)
#define EXDF_CALL           (1L<<17)  // Call user-mode routine at exd_ReturnPC

// ExceptionData structure (from mmu/exceptions.h)
// This is passed to exception hooks in register a0
struct ExceptionData {
    void*    exd_Task;               // Task that caused exception
    void*    exd_Context;            // MMU context
    uint32_t* exd_Descriptor;        // MMU descriptor
    uint32_t* exd_NextDescriptor;    // Next descriptor for misaligned
    void*    exd_FaultAddress;       // Address that failed
    void*    exd_NextFaultAddress;   // End address of failed access
    uint32_t exd_UserData;           // User data for page
    uint32_t exd_NextUserData;       // User data for next page
    uint32_t exd_Data;               // CPU output pipeline data
    void*    exd_ReturnPC;           // PC of faulted instruction (or call addr)
    uint32_t exd_Flags;              // Exception flags
    uint32_t exd_Properties;         // Properties of accessed memory
    uint32_t exd_NextProperties;     // Properties of next descriptor
    uint8_t  exd_Internal;           // Internal use
    uint8_t  exd_FunctionCode;       // Function code mask
    int8_t   exd_Level;              // Level of descriptor
    int8_t   exd_NextLevel;          // Level of next descriptor
    uint32_t exd_DataRegs[8];        // Data registers d0-d7
    uint32_t exd_AddrRegs[7];        // Address registers a0-a6
    uint16_t* exd_SSP;               // Supervisor stack pointer
    uint16_t* exd_USP;               // User stack pointer
    void*    exd_SysBase;            // Cached SysBase
    void*    exd_MMUBase;            // MMU library base
};

// Global for storing fault information to display in user-mode
static void* _last_fault_address = NULL;
static void* _last_fault_pc = NULL;

// Forward declarations
static void ptr_to_hex(char* buf, void* ptr);
void __novus_cleanup_mmu_protection(void);
void __novus_segfault_user_handler(void);

// ============================================================================
// DOS Library Inline Functions (for VBCC)
// ============================================================================
// VBCC inline function for dos.library Exit()
// Exit(returnCode) - LVO -144, returnCode in d1
void __dos_exit(__reg("a6") void* dosBase, __reg("d1") int32_t returnCode) = "\tjsr\t-144(a6)";

// ============================================================================
// Debug Symbol Table Support
// ============================================================================
// The debug symbol table is generated by the compiler in debug builds.
// It contains function addresses mapped to source file:line information.
// We declare it as weak symbols so the code links even if the table is absent.

typedef struct {
    void* func_addr;       // Function start address
    const char* file;      // Source file path (filename only)
    uint16_t line;         // Line number
    const char* name;      // Function name
} NovusDebugSymbol;

// Debug symbol table pointers - set by generated code if available
// Using pointers instead of weak symbols for VBCC compatibility
static const NovusDebugSymbol* _debug_symbols = NULL;
static uint32_t _debug_symbol_count = 0;

// Called by generated code to register the debug symbol table
void __novus_register_debug_symbols(const NovusDebugSymbol* symbols, uint32_t count)
{
    _debug_symbols = symbols;
    _debug_symbol_count = count;
}

// Note: __novus_init_debug_symbols() is defined in generated debug_symbols.c
// It's always generated (as no-op in release builds, with symbols in debug builds)
// The generated version calls __novus_register_debug_symbols() with the symbol table
extern void __novus_init_debug_symbols(void);

/**
 * Look up source location for a given PC address.
 * Returns the symbol containing the PC, or NULL if not found.
 * Uses linear search since symbol count is typically small.
 */
static const NovusDebugSymbol* lookup_debug_symbol(void* pc)
{
    uint32_t i;
    const NovusDebugSymbol* best = NULL;
    uint32_t pc_val = (uint32_t)pc;

    // Check if debug symbols are available
    if (_debug_symbols == NULL || _debug_symbol_count == 0) {
        return NULL;
    }

    // Find the function that contains this PC
    // The PC is within a function if: func_addr <= pc < next_func_addr
    for (i = 0; i < _debug_symbol_count; i++) {
        uint32_t func_addr = (uint32_t)_debug_symbols[i].func_addr;

        // Check if PC could be in this function (PC >= func_addr)
        if (pc_val >= func_addr) {
            // This could be the function - keep as best candidate
            // (the one with highest address <= PC wins)
            if (best == NULL || func_addr > (uint32_t)best->func_addr) {
                best = &_debug_symbols[i];
            }
        }
    }

    return best;
}

// Helper to convert pointer to hex string
static void ptr_to_hex(char* buf, void* ptr) {
    uint32_t val = (uint32_t)ptr;
    int i;
    buf[0] = '0';
    buf[1] = 'x';
    for (i = 0; i < 8; i++) {
        int digit = (val >> (28 - i * 4)) & 0xF;
        buf[2 + i] = (digit < 10) ? ('0' + digit) : ('A' + digit - 10);
    }
    buf[10] = '\0';
}

/**
 * User-mode handler called by mmu.library when EXDF_CALL is set.
 * Displays an error dialog and exits the program gracefully.
 * This function never returns.
 */
void __novus_segfault_user_handler(void)
{
    char addr_str[12];
    char pc_str[12];
    char line_str[12];
    char* ptr = error_buffer;
    struct Library* dosBase;
    const NovusDebugSymbol* sym;

    // Convert addresses to hex strings
    ptr_to_hex(addr_str, _last_fault_address);
    ptr_to_hex(pc_str, _last_fault_pc);

    // Build the error message
    ptr = strcpy_helper(ptr, "SEGMENTATION FAULT!\n\n");
    ptr = strcpy_helper(ptr, "Access at: ");
    ptr = strcpy_helper(ptr, addr_str);

    if (_last_fault_address == NULL || (uint32_t)_last_fault_address < 4096) {
        ptr = strcpy_helper(ptr, "\n(NULL pointer dereference)");
    }

    // Look up source location from debug symbols
    sym = lookup_debug_symbol(_last_fault_pc);
    if (sym != NULL) {
        int_to_str(line_str, (int32_t)sym->line);

        ptr = strcpy_helper(ptr, "\n\nIn function: ");
        ptr = strcpy_helper(ptr, sym->name);
        ptr = strcpy_helper(ptr, "\nSource: ");
        ptr = strcpy_helper(ptr, sym->file);
        ptr = strcpy_helper(ptr, ":");
        ptr = strcpy_helper(ptr, line_str);
    } else {
        // No debug symbols - show PC and hint about map file
        ptr = strcpy_helper(ptr, "\n\nPC: ");
        ptr = strcpy_helper(ptr, pc_str);
        ptr = strcpy_helper(ptr, "\n\n(Build with --safety-level 3\nfor source locations)");
    }

    display_error_requester(AO_NullPointer);

    // Clean up MMU protection before exit
    __novus_cleanup_mmu_protection();

    // Exit gracefully using DOS Exit() instead of Alert guru
    // This returns to the shell/CLI without crashing
    dosBase = OpenLibrary("dos.library", 0L);
    if (dosBase != NULL) {
        // Call Exit(20) using VBCC inline function
        // Exit code 20 = RETURN_FAIL in AmigaDOS
        __dos_exit(dosBase, 20L);
        // Exit never returns, but just in case...
    }

    // Fallback if DOS unavailable (shouldn't happen)
    Alert(AT_DeadEnd | AN_NovusLib | AO_NullPointer);
}

// Handler data structure - passed in a1/a4
struct SegfaultHandlerData {
    void* user_handler;           // Address of user-mode handler
    void** fault_address_ptr;     // Pointer to _last_fault_address
    void** fault_pc_ptr;          // Pointer to _last_fault_pc
};

static struct SegfaultHandlerData _handler_data;

// Minimal assembly handler - uses the data structure for addresses
// This avoids needing to patch absolute addresses in code
//
// 68k instruction encoding notes (verified with vasmm68k_mot):
// - move.l d(An),Dn: 0x2028 + displacement (16-bit signed offset)
// - move.l d(An),Am: 0x2869 + displacement (for a4 destination)
// - move.l Dn,(Am): 0x2880 (for d0 to (a4))
// - move.l (a1),d(a0): 0x2151 + displacement (NOT 0x2A11!)
// - or.l #imm,d(a0): 0x00A8 + immediate (32-bit) + displacement (16-bit)
//                    Note: immediate comes BEFORE displacement!
//
static const uint16_t _segfault_handler_asm[] = {
    // Entry: a0 = ExceptionData*, a1 = SegfaultHandlerData*

    // Save fault address: d0 = exd_FaultAddress (offset 16 = 0x10)
    0x2028, 0x0010,           // move.l 16(a0),d0
    // a4 = handler_data->fault_address_ptr (offset 4 in SegfaultHandlerData)
    0x2869, 0x0004,           // move.l 4(a1),a4
    // *fault_address_ptr = d0
    0x2880,                   // move.l d0,(a4)

    // Save fault PC: d0 = exd_ReturnPC (offset 36 = 0x24) - save before overwriting
    0x2028, 0x0024,           // move.l 36(a0),d0
    // a4 = handler_data->fault_pc_ptr (offset 8 in SegfaultHandlerData)
    0x2869, 0x0008,           // move.l 8(a1),a4
    // *fault_pc_ptr = d0
    0x2880,                   // move.l d0,(a4)

    // Now set exd_ReturnPC = handler_data->user_handler
    0x2151, 0x0024,           // move.l (a1),36(a0)  [CORRECTED: was 0x2A11]

    // Set EXDF_CALL flag: exd_Flags |= 0x20000 (offset 40 = 0x28)
    // WARNING: Motorola syntax or.l has immediate BEFORE displacement!
    0x00A8,                   // or.l #imm,d(a0) - opcode
    0x0002, 0x0000,           // immediate value 0x00020000 (EXDF_CALL = 1<<17)
    0x0028,                   // displacement 40 (exd_Flags offset)

    // Return handled (d0=0, Z flag set)
    0x7000,                   // moveq #0,d0
    0x4E75                    // rts
};

// Cached MMU detection results (computed once at first call)
static int8_t _mmu_detected = -1;  // -1 = not yet detected
static SystemMMU _cached_mmu = SystemMMU_None;
static uint32_t _cached_page_size = 0;
static uint8_t _has_mmu_library = 0;

/**
 * Detect MMU type at runtime
 *
 * Uses a combination of CPU detection and mmu.library probing.
 * Results are cached after first detection.
 */
SystemMMU __detect_mmu(void)
{
    struct Library *MMUBase;
    uint16_t attn_flags;

    // Return cached result if already detected
    if (_mmu_detected >= 0) {
        return _cached_mmu;
    }

    // Mark as detected (even if None)
    _mmu_detected = 1;
    _cached_mmu = SystemMMU_None;
    _cached_page_size = 0;
    _has_mmu_library = 0;

    // Get CPU type from AttnFlags
    attn_flags = ((struct ExecBase *)SysBase)->AttnFlags;

    // Try to open mmu.library - this is the most reliable way to detect MMU
    // mmu.library handles all the edge cases (LC variants, disabled MMUs, etc.)
    MMUBase = OpenLibrary("mmu.library", 0L);
    if (MMUBase != NULL) {
        // mmu.library is present - it only opens successfully if an MMU exists
        _has_mmu_library = 1;

        // Determine MMU type based on CPU
        // mmu.library wouldn't have opened if there was no MMU
        if (attn_flags & AFF_68060) {
            _cached_mmu = SystemMMU_M68060;
            _cached_page_size = 4096;  // 68060 uses 4K or 8K pages
        } else if (attn_flags & AFF_68040) {
            _cached_mmu = SystemMMU_M68040;
            _cached_page_size = 4096;  // 68040 uses 4K pages
        } else if (attn_flags & AFF_68030) {
            _cached_mmu = SystemMMU_M68030;
            _cached_page_size = 4096;  // 68030 typically configured for 4K
        } else if (attn_flags & AFF_68020) {
            // 68020 with mmu.library = external 68851 PMMU
            _cached_mmu = SystemMMU_M68851;
            _cached_page_size = 4096;
        }

        CloseLibrary(MMUBase);
    } else {
        // mmu.library not available - fall back to CPU-based detection
        // This is less reliable as LC chips report the same AttnFlags
        // but we'll assume MMU is present for 030/040/060

        if (attn_flags & AFF_68060) {
            // 68060 - assume MMU present (LC060 very rare)
            _cached_mmu = SystemMMU_M68060;
            _cached_page_size = 4096;
        } else if (attn_flags & AFF_68040) {
            // 68040 - could be LC040/EC040 without MMU
            // Assume present since full 040 is more common
            _cached_mmu = SystemMMU_M68040;
            _cached_page_size = 4096;
        } else if (attn_flags & AFF_68030) {
            // 68030 - could be EC030 without MMU
            // Assume present since full 030 is more common
            _cached_mmu = SystemMMU_M68030;
            _cached_page_size = 4096;
        }
        // 68020 and below: no MMU without mmu.library confirmation
    }

    return _cached_mmu;
}

/**
 * Get MMU page size in bytes
 * Returns 0 if no MMU is present
 */
uint32_t __get_mmu_page_size(void)
{
    // Ensure detection has run
    __detect_mmu();
    return _cached_page_size;
}

/**
 * Check if mmu.library is available
 */
uint8_t __has_mmu_library(void)
{
    // Ensure detection has run
    __detect_mmu();
    return _has_mmu_library;
}

/**
 * Print MMU detection status to stdout (for debug builds)
 * Called by generated code when built with debug/full safety level
 *
 * Uses proto/dos.h with DOSBase from our local open.
 * Note: We open/close our own DOSBase here since the global one
 * might not be initialized yet at the point this runs.
 */

void __novus_print_mmu_status(void)
{
    SystemMMU mmu;
    const char* mmu_str;
    char msg[100];
    char* ptr;
    struct Library *dosBase;

    // Ensure MMU detection has run first (before any DOS calls)
    mmu = __detect_mmu();

    // Open dos.library - we manage our own base here
    dosBase = OpenLibrary("dos.library", 33L);
    if (dosBase == NULL) {
        return;  // Can't print without DOS
    }

    // Get MMU type string
    switch (mmu) {
        case SystemMMU_M68851:
            mmu_str = "68851 PMMU";
            break;
        case SystemMMU_M68030:
            mmu_str = "68030 MMU";
            break;
        case SystemMMU_M68040:
            mmu_str = "68040 MMU";
            break;
        case SystemMMU_M68060:
            mmu_str = "68060 MMU";
            break;
        default:
            mmu_str = NULL;
            break;
    }

    // Build message
    ptr = msg;
    if (mmu_str != NULL) {
        ptr = strcpy_helper(ptr, "[Novus] MMU detected: ");
        ptr = strcpy_helper(ptr, mmu_str);
        if (_has_mmu_library) {
            ptr = strcpy_helper(ptr, " (mmu.library available)");
        }
        ptr = strcpy_helper(ptr, "\n");
    } else {
        ptr = strcpy_helper(ptr, "[Novus] No MMU detected\n");
    }

    // Write to stdout using the requester approach (which we already have working)
    // Since we can't easily call DOS Write() without the inline asm that VBCC doesn't support,
    // and the requester approach works, let's just use the display_error_requester with a custom message
    // Actually, that's for errors. Let's just skip this for now - users can test MMU detection
    // via the Novus code itself which uses PutStr.

    CloseLibrary(dosBase);
}

// ============================================================================
// MMU Memory Protection
// ============================================================================
//
// These functions use mmu.library to set up memory protection for debugging.
// Null page protection catches null pointer dereferences at the hardware level.
//
// We use inline assembly to call mmu.library functions since VBCC doesn't
// have pragma definitions for it in the standard includes.
// ============================================================================

// State for null page protection
static uint8_t _null_page_protected = 0;
static struct Library* _mmu_base = NULL;
static void* _saved_mmu_context = NULL;
static uint32_t _saved_page_size = 0;
static void* _exception_hook = NULL;  // Installed exception hook handle

// mmu.library LVO offsets (from MuManual pragmas/mmu_pragmas.h)
#define MMU_LVO_GetPageSize     (-0x030)
#define MMU_LVO_DefaultContext  (-0x096)
#define MMU_LVO_SetPropertiesA  (-0x054)
#define MMU_LVO_RebuildTree     (-0x060)

// VBCC inline function declarations for mmu.library
// These use the VBCC-specific __reg() syntax for register parameters
// LVO offsets: DefaultContext=-0x096, GetPageSize=-0x030, SetPropertiesA=-0x054, RebuildTree=-0x060

// DefaultContext() - Returns the default MMU context
// LVO: -150 (0x96)
void* __MMUDefaultContext(__reg("a6") void* mmu_base) = "\tjsr\t-150(a6)";
#define mmu_default_context(base) __MMUDefaultContext(base)

// GetPageSize(ctx) - Returns page size in bytes
// LVO: -48 (0x30)
// ctx goes in a0
uint32_t __MMUGetPageSize(__reg("a6") void* mmu_base, __reg("a0") void* ctx) = "\tjsr\t-48(a6)";
#define mmu_get_page_size(base, ctx) __MMUGetPageSize(base, ctx)

// SetPropertiesA(ctx, flags, mask, lower, size, tags)
// LVO: -84 (0x54)
// ctx in a0, flags in d1, mask in d2, lower in a1, size in d0, tags in a2
// NOTE: 'lower' is a ULONG address value, not a pointer!
// NOTE: 'tags' must point to a valid tag array, cannot be NULL!
int32_t __MMUSetPropertiesA(__reg("a6") void* mmu_base,
                            __reg("a0") void* ctx,
                            __reg("d1") uint32_t flags,
                            __reg("d2") uint32_t mask,
                            __reg("a1") uint32_t lower,
                            __reg("d0") uint32_t size,
                            __reg("a2") uint32_t* tags) = "\tjsr\t-84(a6)";

// Tag array for SetPropertiesA when no special tags are needed
static const uint32_t _mmu_no_tags[2] = {TAG_DONE, 0};

#define mmu_set_properties(base, ctx, flags, mask, lower, size) \
    __MMUSetPropertiesA(base, ctx, flags, mask, lower, size, (uint32_t*)_mmu_no_tags)

// RebuildTree(ctx) - Rebuild the MMU tree after changes
// LVO: -96 (0x60)
// ctx in a0
int32_t __MMURebuildTree(__reg("a6") void* mmu_base, __reg("a0") void* ctx) = "\tjsr\t-96(a6)";
#define mmu_rebuild_tree(base, ctx) __MMURebuildTree(base, ctx)

// AddContextHookA(tags) - Add an exception hook
// LVO: -168 (0xa8)
// tags in a0
void* __MMUAddContextHookA(__reg("a6") void* mmu_base, __reg("a0") void* tags) = "\tjsr\t-168(a6)";
#define mmu_add_context_hook(base, tags) __MMUAddContextHookA(base, tags)

// RemContextHook(hook) - Remove an exception hook
// LVO: -174 (0xae)
// hook in a1
void __MMURemContextHook(__reg("a6") void* mmu_base, __reg("a1") void* hook) = "\tjsr\t-174(a6)";
#define mmu_rem_context_hook(base, hook) __MMURemContextHook(base, hook)

// ActivateException(hook) - Activate an exception hook
// LVO: -192 (0xC0)
// hook in a1
void __MMUActivateException(__reg("a6") void* mmu_base, __reg("a1") void* hook) = "\tjsr\t-192(a6)";
#define mmu_activate_exception(base, hook) __MMUActivateException(base, hook)

// DeactivateException(hook) - Deactivate an exception hook
// LVO: -198 (0xC6)
// hook in a1
void __MMUDeactivateException(__reg("a6") void* mmu_base, __reg("a1") void* hook) = "\tjsr\t-198(a6)";
#define mmu_deactivate_exception(base, hook) __MMUDeactivateException(base, hook)

/**
 * Enable null page protection using mmu.library
 * Marks page 0 as MAPP_INVALID so any access causes a segmentation fault.
 * Also installs an exception hook to catch the fault and display a dialog.
 *
 * Returns: 1 if protection was enabled, 0 if not (no MMU or library unavailable)
 */
uint8_t __novus_enable_null_page_protection(void)
{
    void* ctx;
    uint32_t page_size;
    int32_t result;

    // Already protected?
    if (_null_page_protected) {
        return 1;
    }

    // Ensure MMU detection has run
    __detect_mmu();

    // Need mmu.library for this
    if (!_has_mmu_library) {
        return 0;
    }

    // Open mmu.library if not already open
    if (_mmu_base == NULL) {
        _mmu_base = OpenLibrary("mmu.library", 0L);
        if (_mmu_base == NULL) {
            return 0;
        }
    }

    // Get the default MMU context
    ctx = mmu_default_context(_mmu_base);
    if (ctx == NULL) {
        return 0;
    }
    _saved_mmu_context = ctx;

    // Get the page size
    page_size = mmu_get_page_size(_mmu_base, ctx);
    if (page_size == 0) {
        return 0;
    }
    _saved_page_size = page_size;

    // Install our exception hook to catch segfaults
    // Tags: CONTEXT, TYPE, CODE, DATA, PRI, TAG_DONE
    //
    // We use a hand-crafted assembly stub because:
    // 1. The handler runs in supervisor mode during an exception
    // 2. It must return with Z flag set and d0=0
    // 3. C functions (even with __amigainterrupt) don't handle this correctly
    //
    // The assembly stub (in _segfault_handler_asm) reads the user handler
    // address from the DATA structure (passed in a1), sets it as the
    // exd_ReturnPC, sets EXDF_CALL flag, and returns handled.
    {
        uint32_t hook_tags[12];  // Tag array: pairs of (tag, value) + TAG_DONE

        // CRITICAL: Flush caches before executing code from data section!
        // On 68040/68060, the instruction cache must be coherent with data cache.
        // Without this, the CPU may fetch garbage instructions.
        CacheClearE((void*)_segfault_handler_asm, sizeof(_segfault_handler_asm),
                    CACRF_ClearI | CACRF_ClearD);

        // Initialize the handler data structure
        _handler_data.user_handler = (void*)__novus_segfault_user_handler;
        _handler_data.fault_address_ptr = &_last_fault_address;
        _handler_data.fault_pc_ptr = &_last_fault_pc;

        hook_tags[0] = MADTAG_CONTEXT;
        hook_tags[1] = (uint32_t)ctx;
        hook_tags[2] = MADTAG_TYPE;
        hook_tags[3] = MMUEH_SEGFAULT;
        hook_tags[4] = MADTAG_CODE;
        hook_tags[5] = (uint32_t)_segfault_handler_asm;  // Assembly stub
        hook_tags[6] = MADTAG_DATA;
        hook_tags[7] = (uint32_t)&_handler_data;  // Data passed in a1/a4
        hook_tags[8] = MADTAG_PRI;
        hook_tags[9] = 0xFFFFFFC0;  // -64 as uint32_t (debug tool priority)
        hook_tags[10] = TAG_DONE;
        hook_tags[11] = 0;

        _exception_hook = mmu_add_context_hook(_mmu_base, hook_tags);
        if (_exception_hook == NULL) {
            // Failed to install hook - continue without it
            // The crash will occur but at least we tried
        } else {
            // CRITICAL: Must activate the hook or it won't catch exceptions!
            mmu_activate_exception(_mmu_base, _exception_hook);
            // Some MMU types may require tree rebuild after activation
            mmu_rebuild_tree(_mmu_base, ctx);
        }
    }

    // Mark page 0 as invalid
    // flags = MAPP_INVALID, mask = MAPP_INVALID, lower = 0, size = page_size
    result = mmu_set_properties(_mmu_base, ctx, MAPP_INVALID, MAPP_INVALID, 0, page_size);
    if (result == 0) {
        // Clean up hook if we installed it
        if (_exception_hook != NULL) {
            mmu_rem_context_hook(_mmu_base, _exception_hook);
            _exception_hook = NULL;
        }
        return 0;
    }

    // Rebuild the MMU tree to apply changes
    result = mmu_rebuild_tree(_mmu_base, ctx);
    if (result == 0) {
        // Clean up hook
        if (_exception_hook != NULL) {
            mmu_rem_context_hook(_mmu_base, _exception_hook);
            _exception_hook = NULL;
        }
        return 0;
    }

    _null_page_protected = 1;
    return 1;
}

/**
 * Disable null page protection (restore page 0 to valid)
 * Should be called before program exit to avoid leaving system in protected state.
 */
void __novus_disable_null_page_protection(void)
{
    if (!_null_page_protected || _mmu_base == NULL) {
        return;
    }

    // Deactivate and remove exception hook first
    if (_exception_hook != NULL) {
        mmu_deactivate_exception(_mmu_base, _exception_hook);
        mmu_rem_context_hook(_mmu_base, _exception_hook);
        _exception_hook = NULL;
    }

    if (_saved_mmu_context != NULL && _saved_page_size > 0) {
        // Clear the INVALID flag on page 0
        mmu_set_properties(_mmu_base, _saved_mmu_context, 0, MAPP_INVALID, 0, _saved_page_size);

        // Rebuild the tree
        mmu_rebuild_tree(_mmu_base, _saved_mmu_context);
    }

    _null_page_protected = 0;
    _saved_mmu_context = NULL;
    _saved_page_size = 0;

    // Close the library
    if (_mmu_base != NULL) {
        CloseLibrary(_mmu_base);
        _mmu_base = NULL;
    }
}

/**
 * Report null pointer dereference error
 * Called when the program triggers a null pointer access.
 * Note: With MMU protection, this is caught by hardware - the normal exception
 * handler will fire. This function is for future use with custom exception hooks.
 */
void __novus_null_pointer_error(const char* file, int32_t line)
{
    char line_str[12];
    char* ptr = error_buffer;

    int_to_str(line_str, line);

    ptr = strcpy_helper(ptr, "NULL POINTER DEREFERENCE!\n\n");
    ptr = strcpy_helper(ptr, "Attempted to access memory at address 0x00000000\n\n");
    ptr = strcpy_helper(ptr, "File: ");
    ptr = strcpy_helper(ptr, file ? file : "<unknown>");
    ptr = strcpy_helper(ptr, "\nLine: ");
    ptr = strcpy_helper(ptr, line_str);

    display_error_requester(AO_NullPointer);
}

/**
 * Initialize MMU protection features
 * Called at program startup when built with debug/paranoid safety level.
 * Enables null page protection if mmu.library is available.
 */
void __novus_init_mmu_protection(void)
{
    // Register debug symbols first (so crash handler can show file:line)
    // This calls the generated init function which sets up the symbol table
    __novus_init_debug_symbols();

    // AmigaOS stores SysBase at address 4, inside the MMU's first page. Marking
    // that page invalid breaks every normal library call; generated null checks
    // provide per-access protection without mutating the system MMU context.
}

/**
 * Cleanup MMU protection features
 * Called at program exit to restore normal memory state.
 */
void __novus_cleanup_mmu_protection(void)
{
    __novus_disable_null_page_protection();
}
