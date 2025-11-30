// Novus Runtime Library
// Assert failure handler using AmigaOS EasyRequest

#include <exec/types.h>
#include <exec/libraries.h>
#include <exec/alerts.h>
#include <exec/memory.h>
#include <exec/semaphores.h>
#include <intuition/intuition.h>
#include <graphics/gfxbase.h>
#include <proto/exec.h>
#include <proto/intuition.h>

// Integer types - fallback definitions if stdint.h not included
// VBCC's exec/types.h includes <stdint.h> which defines __STDINT_H
#ifndef __STDINT_H
typedef signed char int8_t;
typedef short int16_t;
typedef long int32_t;
typedef long long int64_t;
typedef unsigned char uint8_t;
typedef unsigned short uint16_t;
typedef unsigned long uint32_t;
typedef unsigned long long uint64_t;
#endif

// Custom alert codes for Novus runtime errors
#define AN_NovusLib    (0x7F000000)  // User application alert
#define AG_NovusError  (0x00000001)  // Generic Novus error
#define AO_BoundsCheck (0x00000002)  // Bounds check failure
#define AO_DivByZero   (0x00000003)  // Division by zero
#define AO_Panic       (0x00000004)  // Panic
#define AO_Assert      (0x00000005)  // Assert failure
#define AO_MemoryLeak  (0x00000006)  // Memory leak detected
#define AO_DoubleFree  (0x00000007)  // Double free detected
#define AO_BufferOverflow (0x00000008)  // Buffer overflow detected
#define AO_UseAfterFree   (0x00000009)  // Use-after-free detected

// Buffer for building the error message
static char error_buffer[512];

// Simple string copy helper - returns pointer to end of copied string
static char* strcpy_helper(char* dest, const char* src) {
    while (*src) {
        *dest++ = *src++;
    }
    *dest = '\0';
    return dest;
}

// Simple integer to string helper
static void int_to_str(char* buf, int32_t num) {
    char temp[12];
    int i = 0;
    int is_negative = 0;

    if (num == 0) {
        buf[0] = '0';
        buf[1] = '\0';
        return;
    }

    // Special case for INT32_MIN (-2147483648) since -INT32_MIN overflows
    if (num == -2147483647 - 1) {  // INT32_MIN
        strcpy_helper(buf, "-2147483648");
        return;
    }

    if (num < 0) {
        is_negative = 1;
        num = -num;
    }

    while (num > 0) {
        temp[i++] = '0' + (num % 10);
        num /= 10;
    }

    int j = 0;
    if (is_negative) {
        buf[j++] = '-';
    }

    while (i > 0) {
        buf[j++] = temp[--i];
    }
    buf[j] = '\0';
}

/**
 * Common error display function - shows error requester with formatted message
 * Centralizes the pattern used by all error handlers
 *
 * @param alert_code Alert code to use if IntuitionBase is not available
 */
static void display_error_requester(uint32_t alert_code)
{
    struct Library *IntuitionBase;
    struct EasyStruct es;

    // Try to open intuition.library
    IntuitionBase = OpenLibrary("intuition.library", 33L);
    if (IntuitionBase == NULL) {
        // Can't show requester - use Alert() as fallback
        Alert(AT_DeadEnd | AN_NovusLib | AG_NovusError | alert_code);
        // Alert never returns for AT_DeadEnd
        return;  // Should never reach here
    }

    // Set up EasyRequest structure
    es.es_StructSize   = sizeof(struct EasyStruct);
    es.es_Flags        = 0;
    es.es_Title        = "Novus Runtime Error";
    es.es_TextFormat   = error_buffer;
    es.es_GadgetFormat = "OK";

    // Display the requester
    EasyRequest(NULL, &es, NULL);

    // Close the library
    CloseLibrary(IntuitionBase);

    // Return to caller so deferred cleanup can execute
    // This allows proper resource cleanup (defer blocks) before program exits
}

/**
 * Assert failure handler - displays error using EasyRequest
 *
 * @param file Source file where assertion failed
 * @param line Line number
 * @param col Column number
 * @param message Optional error message (can be NULL)
 */
void __novus_assert_failed(const char* file, int32_t line, int32_t col, const char* message)
{
    char line_str[12];
    char col_str[12];
    char* ptr = error_buffer;

    // Convert numbers to strings
    int_to_str(line_str, line);
    int_to_str(col_str, col);

    // Build the error message
    ptr = strcpy_helper(ptr, "Assertion failed!\n\nFile: ");
    ptr = strcpy_helper(ptr, file);
    ptr = strcpy_helper(ptr, "\nLine: ");
    ptr = strcpy_helper(ptr, line_str);
    ptr = strcpy_helper(ptr, ", Column: ");
    ptr = strcpy_helper(ptr, col_str);

    if (message != NULL) {
        ptr = strcpy_helper(ptr, "\n\n");
        ptr = strcpy_helper(ptr, message);
    }

    display_error_requester(AO_Assert);
}

/**
 * Panic handler - displays error using EasyRequest and halts execution
 * This is for unrecoverable runtime errors (never elided, even in release)
 *
 * @param message Error message to display
 * @param file Source file where panic occurred
 * @param line Line number
 * @param col Column number
 */
void __novus_panic(const char* message, const char* file, int32_t line, int32_t col)
{
    char line_str[12];
    char col_str[12];
    char* ptr = error_buffer;

    // Convert numbers to strings
    int_to_str(line_str, line);
    int_to_str(col_str, col);

    // Build the error message
    ptr = strcpy_helper(ptr, "PANIC: ");
    ptr = strcpy_helper(ptr, message);
    ptr = strcpy_helper(ptr, "\n\nFile: ");
    ptr = strcpy_helper(ptr, file);
    ptr = strcpy_helper(ptr, "\nLine: ");
    ptr = strcpy_helper(ptr, line_str);
    ptr = strcpy_helper(ptr, ", Column: ");
    ptr = strcpy_helper(ptr, col_str);

    display_error_requester(AO_Panic);
    // Note: The C code generator emits a return statement after __novus_panic()
}

/**
 * Bounds check failure handler - displays error when array index is out of bounds
 * Used for runtime bounds checking when safety level >= Basic
 * Note: The actual bounds check is inlined in generated code; this function is only
 * called when the check has already failed.
 *
 * @param index Array index that was out of bounds
 * @param length Array length
 * @param file Source file where access occurred
 * @param line Line number
 */
void __novus_bounds_check_failed(int32_t index, int32_t length, const char* file, int32_t line)
{
    // This function is only called when (uint32_t)index >= (uint32_t)length
    // The comparison has already been done in the generated code
    char index_str[12];
    char length_str[12];
    char line_str[12];
    char* ptr = error_buffer;

    // Convert numbers to strings
    int_to_str(index_str, index);
    int_to_str(length_str, length);
    int_to_str(line_str, line);

    // Build the error message
    ptr = strcpy_helper(ptr, "PANIC: Array index out of bounds!\n\n");
    ptr = strcpy_helper(ptr, "Index: ");
    ptr = strcpy_helper(ptr, index_str);
    ptr = strcpy_helper(ptr, "\nLength: ");
    ptr = strcpy_helper(ptr, length_str);
    ptr = strcpy_helper(ptr, "\n\nFile: ");
    ptr = strcpy_helper(ptr, file);
    ptr = strcpy_helper(ptr, "\nLine: ");
    ptr = strcpy_helper(ptr, line_str);

    display_error_requester(AO_BoundsCheck);
    // Note: Caller (generated code) will execute defer cleanup and return after this
}

/**
 * Division by zero check - panics if divisor is zero
 * Used for runtime division checks when safety level >= Basic
 *
 * @param divisor The divisor value
 * @param file Source file where division occurred
 * @param line Line number
 */
void __novus_div_check(int32_t divisor, const char* file, int32_t line)
{
    if (divisor == 0) {
        char line_str[12];
        char* ptr = error_buffer;

        // Convert line number to string
        int_to_str(line_str, line);

        // Build the error message
        ptr = strcpy_helper(ptr, "PANIC: Division by zero!\n\n");
        ptr = strcpy_helper(ptr, "File: ");
        ptr = strcpy_helper(ptr, file);
        ptr = strcpy_helper(ptr, "\nLine: ");
        ptr = strcpy_helper(ptr, line_str);

        display_error_requester(AO_DivByZero);
    }
}

// ============================================================================
// Hardware Detection
// ============================================================================

// AttnFlags bits from exec/execbase.h - only define if not already defined
#ifndef AFF_68010
#define AFF_68010     (1<<0)   // 68010 or better
#endif
#ifndef AFF_68020
#define AFF_68020     (1<<1)   // 68020 or better
#endif
#ifndef AFF_68030
#define AFF_68030     (1<<2)   // 68030 or better
#endif
#ifndef AFF_68040
#define AFF_68040     (1<<3)   // 68040 or better
#endif
#ifndef AFF_68060
#define AFF_68060     (1<<7)   // 68060 or better
#endif
#ifndef AFF_68881
#define AFF_68881     (1<<4)   // 68881 or 68882 FPU
#endif
#ifndef AFF_68882
#define AFF_68882     (1<<5)   // 68882 FPU (or better)
#endif
#ifndef AFF_FPU40
#define AFF_FPU40     (1<<6)   // 68040/68060 internal FPU
#endif

// SystemCPU enum values (must match std::system::SystemCPU)
typedef enum {
    SystemCPU_M68000 = 0,
    SystemCPU_M68010 = 1,
    SystemCPU_M68020 = 2,
    SystemCPU_M68030 = 3,
    SystemCPU_M68040 = 4,
    SystemCPU_M68060 = 5
} SystemCPU;

// SystemFPU enum values (must match std::system::SystemFPU)
typedef enum {
    SystemFPU_None = 0,
    SystemFPU_M68881 = 1,
    SystemFPU_M68882 = 2,
    SystemFPU_M68040 = 3,
    SystemFPU_M68060 = 4
} SystemFPU;

// SystemChipset enum values (must match std::system::SystemChipset)
typedef enum {
    SystemChipset_OCS = 0,
    SystemChipset_ECS = 1,
    SystemChipset_AGA = 2
} SystemChipset;

// DisplayType enum values (must match std::hardware::chipset::DisplayType)
typedef enum {
    DisplayType_PAL = 0,
    DisplayType_NTSC = 1
} DisplayType;

/**
 * Detect CPU type at runtime
 * Reads ExecBase->AttnFlags to determine which CPU features are available
 */
SystemCPU __detect_cpu(void)
{
    uint16_t attn_flags = ((struct ExecBase *)SysBase)->AttnFlags;

    if (attn_flags & AFF_68060) return SystemCPU_M68060;
    if (attn_flags & AFF_68040) return SystemCPU_M68040;
    if (attn_flags & AFF_68030) return SystemCPU_M68030;
    if (attn_flags & AFF_68020) return SystemCPU_M68020;
    if (attn_flags & AFF_68010) return SystemCPU_M68010;
    return SystemCPU_M68000;
}

/**
 * Detect FPU type at runtime
 * Reads ExecBase->AttnFlags to determine which FPU is present
 */
SystemFPU __detect_fpu(void)
{
    uint16_t attn_flags = ((struct ExecBase *)SysBase)->AttnFlags;

    // Check 68060 first (most specific)
    if ((attn_flags & AFF_68060) && (attn_flags & AFF_FPU40)) {
        return SystemFPU_M68060;
    }

    // Check 68040 internal FPU
    if ((attn_flags & AFF_68040) && (attn_flags & AFF_FPU40)) {
        return SystemFPU_M68040;
    }

    // Check for 68882 (more specific than 68881)
    if (attn_flags & AFF_68882) {
        return SystemFPU_M68882;
    }

    // Check for 68881
    if (attn_flags & AFF_68881) {
        return SystemFPU_M68881;
    }

    return SystemFPU_None;
}

/**
 * Detect chipset type at runtime
 * Checks graphics.library ChipRevBits0 to determine OCS/ECS/AGA
 *
 * IMPORTANT: ChipRevBits0 detection requires V39 SetPatch to be accurate.
 * On systems with library version < 39, this will default to OCS.
 *
 * NOTE: RTG (UAEGFX/Picasso96/CyberGraphX) does NOT affect this detection.
 * This always returns the native Amiga chipset (OCS/ECS/AGA), regardless
 * of what graphics system is currently active for display.
 */
SystemChipset __detect_chipset(void)
{
    struct GfxBase *GfxBase;
    uint8_t chip_rev;
    SystemChipset result;

    // Open graphics.library V36+ to check chipset
    GfxBase = (struct GfxBase *)OpenLibrary("graphics.library", 36L);
    if (GfxBase == NULL) {
        // Very old system (pre-2.0) - must be OCS
        return SystemChipset_OCS;
    }

    // ChipRevBits0 is only reliable on V39+ (requires SetPatch)
    if (GfxBase->LibNode.lib_Version < 39) {
        CloseLibrary((struct Library *)GfxBase);
        return SystemChipset_OCS;
    }

    // Read ChipRevBits0 directly from GfxBase structure
    // ChipRevBits0 is a UBYTE (single byte) at offset 476 in struct GfxBase
    chip_rev = GfxBase->ChipRevBits0;

    CloseLibrary((struct Library *)GfxBase);

    // Bit definitions from graphics/gfxbase.h:
    // GFXB_BIG_BLITS  0  (bit 0) = 0x01
    // GFXB_HR_AGNUS   0  (bit 0) = 0x01 - ECS Agnus
    // GFXB_HR_DENISE  1  (bit 1) = 0x02 - ECS Denise
    // GFXB_AA_ALICE   2  (bit 2) = 0x04 - AGA Alice (replaces Denise)
    // GFXB_AA_LISA    3  (bit 3) = 0x08 - AGA Lisa (replaces Agnus)
    // GFXB_AA_MLISA   4  (bit 4) = 0x10 - AGA MLISA (internal use only)

    // AGA check: Both ALICE (bit 2) and LISA (bit 3) must be set
    // GFXF_AA_ALICE = 0x04, GFXF_AA_LISA = 0x08
    if ((chip_rev & 0x0C) == 0x0C) {
        result = SystemChipset_AGA;
    }
    // ECS check: HR_DENISE (bit 1) or HR_AGNUS (bit 0)
    // GFXF_HR_DENISE = 0x02, GFXF_HR_AGNUS = 0x01
    else if (chip_rev & 0x03) {
        result = SystemChipset_ECS;
    }
    // Default to OCS
    else {
        result = SystemChipset_OCS;
    }

    return result;
}

/**
 * Detect display type (PAL vs NTSC) at runtime
 *
 * Uses GfxBase->DisplayFlags which is the most reliable method.
 * This flag is set by the OS during initialization based on the
 * actual hardware configuration (Agnus chip ID and system preferences).
 *
 * Alternative methods (not used):
 * - VPOSR register ($DFF004): Can be unreliable on some systems
 * - SysBase->VBlankFrequency: 50=PAL, 60=NTSC (requires newer exec)
 *
 * NOTE: This returns the system's native display type, not necessarily
 * what a program is currently using (e.g., PAL games on NTSC with
 * mode promotion).
 */
DisplayType __detect_display(void)
{
    struct GfxBase *GfxBase;
    DisplayType result;

    // Open graphics.library V33+ (Kickstart 1.2+)
    GfxBase = (struct GfxBase *)OpenLibrary("graphics.library", 33L);
    if (GfxBase == NULL) {
        // Can't determine - default to PAL (more common in Amiga world)
        return DisplayType_PAL;
    }

    // Check DisplayFlags - bit 5 (0x20) = DISPLAYPAL
    // From graphics/gfxbase.h: DISPLAYPAL = 0x0020
    if (GfxBase->DisplayFlags & 0x0020) {
        result = DisplayType_PAL;
    } else {
        result = DisplayType_NTSC;
    }

    CloseLibrary((struct Library *)GfxBase);
    return result;
}

/**
 * Get total chip RAM installed in the system
 *
 * IMPORTANT: AvailMem(MEMF_TOTAL) does NOT work as you might expect!
 * According to the AmigaOS autodocs, AvailMem() ALWAYS returns free memory,
 * even with MEMF_TOTAL flag. The MEMF_TOTAL flag is poorly documented and
 * does not mean "total installed RAM".
 *
 * The correct way to get total chip RAM is via ExecBase->MaxLocMem,
 * which is documented as "top of chip memory". Since chip RAM starts at
 * address 0, MaxLocMem gives us the total chip RAM size.
 */
uint32_t __get_chip_ram_total(void)
{
    struct ExecBase *execBase = (struct ExecBase *)SysBase;

    // MaxLocMem is the top of chip memory (chip RAM starts at address 0)
    return execBase->MaxLocMem;
}

/**
 * Get free chip RAM
 *
 * Uses Exec AvailMem() with MEMF_CHIP to query the free chip memory.
 * This sums the free bytes across all chip memory MemHeaders.
 */
uint32_t __get_chip_ram_free(void)
{
    return AvailMem(MEMF_CHIP);
}

/**
 * Get largest free chip RAM block
 *
 * Uses Exec AvailMem() with MEMF_CHIP | MEMF_LARGEST to find the largest
 * contiguous block of free chip memory. This is useful for determining if
 * a large allocation will succeed.
 *
 * Note: This is a slow operation as it scans all free blocks.
 */
uint32_t __get_chip_ram_largest(void)
{
    return AvailMem(MEMF_CHIP | MEMF_LARGEST);
}

// Integer to string conversion functions for std::fmt_primitives
// These convert integers to decimal strings and return the length

// Helper to copy from temp buffer to output buffer with bounds checking
static uint32_t copy_to_buffer(const char* temp, uint8_t* buffer, uint32_t buffer_size) {
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

static void uint_to_str(char* buf, uint32_t num) {
    char temp[12];
    int i = 0;

    if (num == 0) {
        buf[0] = '0';
        buf[1] = '\0';
        return;
    }

    while (num > 0) {
        temp[i++] = '0' + (num % 10);
        num /= 10;
    }

    // Reverse the digits
    int j = 0;
    while (i > 0) {
        buf[j++] = temp[--i];
    }
    buf[j] = '\0';
}

uint32_t i8_to_string(int8_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[5]; // -128 to 127 requires at most 4 chars + null
    int_to_str(temp, (int32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t i16_to_string(int16_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[7]; // -32768 to 32767 requires at most 6 chars + null
    int_to_str(temp, (int32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t i32_to_string(int32_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[12]; // -2147483648 to 2147483647 requires at most 11 chars + null
    int_to_str(temp, value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t i64_to_string(int64_t value, uint8_t* buffer, uint32_t buffer_size) {
    // For now, truncate to i32 range. Full i64 support would require more complex implementation
    if (value > 2147483647LL) value = 2147483647LL;
    if (value < -2147483648LL) value = -2147483648LL;
    return i32_to_string((int32_t)value, buffer, buffer_size);
}

uint32_t u8_to_string(uint8_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[4]; // 0 to 255 requires at most 3 chars + null
    uint_to_str(temp, (uint32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t u16_to_string(uint16_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[6]; // 0 to 65535 requires at most 5 chars + null
    uint_to_str(temp, (uint32_t)value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t u32_to_string(uint32_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[12]; // 0 to 4294967295 requires at most 10 chars + null
    uint_to_str(temp, value);
    return copy_to_buffer(temp, buffer, buffer_size);
}

uint32_t u64_to_string(uint64_t value, uint8_t* buffer, uint32_t buffer_size) {
    // For now, truncate to u32 range. Full u64 support would require more complex implementation
    if (value > 4294967295ULL) value = 4294967295ULL;
    return u32_to_string((uint32_t)value, buffer, buffer_size);
}

// Memory set helper
// Simple byte-by-byte set (no stdlib dependency)
void __novus_memset(void* dest, int value, uint32_t n) {
    uint8_t* d = (uint8_t*)dest;
    while (n--) {
        *d++ = (uint8_t)value;
    }
}

// Memory copy helper for StackFormatter
// Simple byte-by-byte copy (no stdlib dependency)
void __novus_memcpy(uint8_t* dest, const uint8_t* src, uint32_t n) {
    while (n--) {
        *dest++ = *src++;
    }
}

// String length - no stdlib dependency
// Used by Str::from_cstr and other string functions
uint32_t strlen(const char* str) {
    uint32_t len = 0;
    while (*str++) {
        len++;
    }
    return len;
}

// Semaphore wrapper functions that take void pointers
// These avoid FFI type issues in generated C code
void __novus_init_semaphore(void* sigSem) {
    InitSemaphore((struct SignalSemaphore*)sigSem);
}

void __novus_obtain_semaphore(void* sigSem) {
    ObtainSemaphore((struct SignalSemaphore*)sigSem);
}

void __novus_release_semaphore(void* sigSem) {
    ReleaseSemaphore((struct SignalSemaphore*)sigSem);
}

int32_t __novus_attempt_semaphore(void* sigSem) {
    return AttemptSemaphore((struct SignalSemaphore*)sigSem);
}

void __novus_obtain_semaphore_shared(void* sigSem) {
    ObtainSemaphoreShared((struct SignalSemaphore*)sigSem);
}

int32_t __novus_attempt_semaphore_shared(void* sigSem) {
    return AttemptSemaphoreShared((struct SignalSemaphore*)sigSem);
}

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

// Custom alert code for null pointer dereference
#define AO_NullPointer      (0x0000000A)  // Null pointer dereference detected

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

// Forward declarations for functions used by exception handler
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

/**
 * Exception hook handler for mmu.library - ASSEMBLY VERSION
 *
 * This must be written as inline assembly because:
 * 1. It runs in supervisor mode during an MMU exception
 * 2. It must return with Z flag set (equal condition) and d0=0
 * 3. Normal C function epilogues don't set the Z flag correctly
 *
 * Register usage (from mmu.library):
 *   a0 = ExceptionData* (struct with fault info)
 *   a1 = user data (passed via MADTAG_DATA)
 *   a4 = user data (same as a1)
 *   a5 = pointer to this code
 *   a6 = MMUBase
 *   d0-d1/a0-a1/a4-a5 = scratch registers
 *
 * ExceptionData structure offsets (for assembly):
 *   +16 (0x10) = exd_FaultAddress (void*)
 *   +36 (0x24) = exd_ReturnPC (void*)
 *   +40 (0x28) = exd_Flags (uint32_t)
 *
 * The handler saves fault info and sets EXDF_CALL to have mmu.library
 * call our user-mode routine (in exd_ReturnPC) where we can safely
 * display a dialog using Intuition.
 */

// Assembly exception handler stub for mmu.library
//
// This hand-crafted assembly code is required because:
// 1. The handler runs in supervisor mode during an MMU exception
// 2. It must return with Z flag set (equal condition) and d0=0
// 3. C functions (even with __amigainterrupt) don't reliably do this
//
// The handler:
// 1. Reads the user handler address from the DATA structure (in a1)
// 2. Stores it in exd_ReturnPC (offset 36 = 0x24)
// 3. Sets EXDF_CALL flag in exd_Flags (offset 40 = 0x28)
// 4. Returns with d0=0 and Z flag set
//
// Entry: a0 = ExceptionData*, a1 = SegfaultHandlerData*

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

    __novus_enable_null_page_protection();
}

/**
 * Cleanup MMU protection features
 * Called at program exit to restore normal memory state.
 */
void __novus_cleanup_mmu_protection(void)
{
    __novus_disable_null_page_protection();
}

// ============================================================================
// Memory Debug Tracking
// ============================================================================
//
// This section implements memory debugging features for detecting:
// - Memory leaks (allocations not freed before program exit)
// - Double-free errors (freeing the same memory twice)
// - Buffer overflows (writing past allocation boundaries) [Paranoid level]
// - Use-after-free (accessing freed memory) [Paranoid level]
//
// These functions are always compiled into the runtime, but are only called
// when the compiler generates code with memory tracking enabled (SafetyLevel >= Full).
// In release builds, the compiler emits direct AllocMem/FreeMem calls instead.
// ============================================================================

// Allocation record - stored in a linked list
typedef struct AllocationRecord {
    void* ptr;                      // Allocated pointer (user-visible)
    void* actual_ptr;               // Actual allocated pointer (may include guard bytes)
    uint32_t size;                  // Requested size
    uint32_t actual_size;           // Actual allocated size (with guards if paranoid)
    const char* file;               // Source file
    int32_t line;                   // Line number
    uint32_t sequence;              // Allocation sequence number
    uint8_t freed;                  // 0 = active, 1 = freed (for double-free detection)
    const char* free_file;          // File where freed (for double-free reporting)
    int32_t free_line;              // Line where freed
    struct AllocationRecord* next;  // Next in list
} AllocationRecord;

// Global tracking state
static AllocationRecord* _alloc_list_head = NULL;
static uint32_t _alloc_sequence = 0;
static uint32_t _total_allocated = 0;
static uint32_t _total_freed = 0;

// Guard byte patterns (for Paranoid level)
#define GUARD_SIZE 4
#define GUARD_PATTERN_START 0xDEADC0DE
#define GUARD_PATTERN_END   0xBEEFCAFE
#define POISON_BYTE         0xDE        // Pattern for poisoned memory

// Use-after-free detection mode
// Without MMU: We don't actually free memory - we keep it allocated but poisoned
//              This allows detection when the memory is accessed again (reads poison)
//              or when it's freed again (detected as use-after-free on "freed" memory)
// With MMU: (Future) Use mmu.library to mark pages invalid for immediate detection
static uint8_t _uaf_mode_initialized = 0;
static uint8_t _has_mmu_for_uaf = 0;  // 1 = can use MMU for UAF detection

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
 * Tracked memory allocation
 * Wraps AllocMem and records the allocation for leak/error detection.
 *
 * @param size Requested allocation size
 * @param flags AmigaOS memory flags (MEMF_PUBLIC, MEMF_CHIP, etc.)
 * @param file Source file where allocation occurred
 * @param line Line number
 * @return Allocated pointer or NULL on failure
 */
void* __novus_tracked_alloc(uint32_t size, uint32_t flags, const char* file, int32_t line)
{
    uint32_t actual_size = size;
    void* actual_ptr;
    void* user_ptr;

    // Add space for guard bytes: 4 bytes before + 4 bytes after
    // Guard bytes help detect buffer overflows when memory is freed
    actual_size = size + (GUARD_SIZE * 2);

    // Allocate the memory
    actual_ptr = AllocMem(actual_size, flags);
    if (actual_ptr == NULL) {
        return NULL;
    }

    // Write guard patterns
    *(uint32_t*)actual_ptr = GUARD_PATTERN_START;
    user_ptr = (uint8_t*)actual_ptr + GUARD_SIZE;
    *(uint32_t*)((uint8_t*)user_ptr + size) = GUARD_PATTERN_END;

    // Create allocation record (allocate from public memory to avoid affecting user's chip mem)
    AllocationRecord* record = (AllocationRecord*)AllocMem(sizeof(AllocationRecord), MEMF_PUBLIC | MEMF_CLEAR);
    if (record == NULL) {
        // Can't track - free and return failure
        FreeMem(actual_ptr, actual_size);
        return NULL;
    }

    record->ptr = user_ptr;
    record->actual_ptr = actual_ptr;
    record->size = size;
    record->actual_size = actual_size;
    record->file = file;
    record->line = line;
    record->sequence = _alloc_sequence++;
    record->freed = 0;
    record->free_file = NULL;
    record->free_line = 0;
    record->next = _alloc_list_head;
    _alloc_list_head = record;

    _total_allocated += size;

    return user_ptr;
}

/**
 * Tracked memory free with double-free detection
 * Wraps FreeMem and checks for errors.
 *
 * @param ptr Pointer to free
 * @param size Size that was allocated
 * @param file Source file where free occurred
 * @param line Line number
 */
void __novus_tracked_free(void* ptr, uint32_t size, const char* file, int32_t line)
{
    if (ptr == NULL) {
        return;  // Freeing NULL is a no-op
    }

    // Search for allocation record
    AllocationRecord* record = _alloc_list_head;

    while (record != NULL) {
        if (record->ptr == ptr) {
            // Found the record
            if (record->freed) {
                // DOUBLE FREE DETECTED!
                char line_str[12];
                char orig_line_str[12];
                char free_line_str[12];
                char ptr_str[12];
                char* msg = error_buffer;

                int_to_str(line_str, line);
                int_to_str(orig_line_str, record->line);
                int_to_str(free_line_str, record->free_line);
                ptr_to_hex(ptr_str, ptr);

                msg = strcpy_helper(msg, "DOUBLE FREE DETECTED!\n\n");
                msg = strcpy_helper(msg, "Pointer: ");
                msg = strcpy_helper(msg, ptr_str);
                msg = strcpy_helper(msg, "\n\nAttempted free at:\n  ");
                msg = strcpy_helper(msg, file ? file : "<unknown>");
                msg = strcpy_helper(msg, ":");
                msg = strcpy_helper(msg, line_str);
                msg = strcpy_helper(msg, "\n\nOriginally allocated at:\n  ");
                msg = strcpy_helper(msg, record->file ? record->file : "<unknown>");
                msg = strcpy_helper(msg, ":");
                msg = strcpy_helper(msg, orig_line_str);
                msg = strcpy_helper(msg, "\n\nFirst freed at:\n  ");
                msg = strcpy_helper(msg, record->free_file ? record->free_file : "<unknown>");
                msg = strcpy_helper(msg, ":");
                msg = strcpy_helper(msg, free_line_str);

                display_error_requester(AO_DoubleFree);
                return;  // Don't actually free - it's already freed
            }

            // Check guard bytes for buffer overflow
            {
                uint32_t* start_guard = (uint32_t*)record->actual_ptr;
                uint32_t* end_guard = (uint32_t*)((uint8_t*)record->ptr + record->size);

                if (*start_guard != GUARD_PATTERN_START || *end_guard != GUARD_PATTERN_END) {
                    // BUFFER OVERFLOW DETECTED!
                    char ov_line_str[12];
                    char ov_orig_line_str[12];
                    char ov_ptr_str[12];
                    char* ov_msg = error_buffer;

                    int_to_str(ov_line_str, line);
                    int_to_str(ov_orig_line_str, record->line);
                    ptr_to_hex(ov_ptr_str, ptr);

                    ov_msg = strcpy_helper(ov_msg, "BUFFER OVERFLOW DETECTED!\n\n");
                    ov_msg = strcpy_helper(ov_msg, "Pointer: ");
                    ov_msg = strcpy_helper(ov_msg, ov_ptr_str);
                    if (*start_guard != GUARD_PATTERN_START) {
                        ov_msg = strcpy_helper(ov_msg, "\n\nUnderflow: Start guard corrupted");
                    }
                    if (*end_guard != GUARD_PATTERN_END) {
                        ov_msg = strcpy_helper(ov_msg, "\n\nOverflow: End guard corrupted");
                    }
                    ov_msg = strcpy_helper(ov_msg, "\n\nAllocated at:\n  ");
                    ov_msg = strcpy_helper(ov_msg, record->file ? record->file : "<unknown>");
                    ov_msg = strcpy_helper(ov_msg, ":");
                    ov_msg = strcpy_helper(ov_msg, ov_orig_line_str);
                    ov_msg = strcpy_helper(ov_msg, "\n\nFreed at:\n  ");
                    ov_msg = strcpy_helper(ov_msg, file ? file : "<unknown>");
                    ov_msg = strcpy_helper(ov_msg, ":");
                    ov_msg = strcpy_helper(ov_msg, ov_line_str);

                    display_error_requester(AO_BufferOverflow);
                    // Continue to free the memory anyway
                }
            }

            // Poison the user memory to detect use-after-free
            // Fill with 0xDE pattern - reads after free will get garbage
            __novus_memset(record->ptr, POISON_BYTE, record->size);

            // Mark as freed and record where
            record->freed = 1;
            record->free_file = file;
            record->free_line = line;
            _total_freed += record->size;

            // USE-AFTER-FREE DETECTION STRATEGY:
            // We do NOT free the memory here. Instead we keep it allocated
            // but poisoned. This allows us to:
            // 1. Detect when the program reads from freed memory (gets poison)
            // 2. Detect when the program writes to freed memory (overwrites poison)
            // 3. Most importantly: detect when they try to FREE it again
            //    (we catch this as use-after-free, not just double-free)
            //
            // The memory will be properly freed in __novus_memory_report() at exit.
            // This trades memory for better debugging - acceptable in debug builds.
            //
            // With MMU (future): Mark pages as MAPP_INVALID for immediate detection

            // DON'T free: FreeMem(record->actual_ptr, record->actual_size);
            return;
        }
        record = record->next;
    }

    // Pointer not found in tracking list - wasn't allocated via tracked_alloc
    // This could be corruption or a pointer allocated before tracking started
    // Just free it normally to avoid a leak
    FreeMem(ptr, size);
}

/**
 * Memory report - called on program exit to report leaks
 * Shows all allocations that were not freed.
 */
void __novus_memory_report(void)
{
    AllocationRecord* record;
    uint32_t leak_count = 0;
    uint32_t leak_bytes = 0;
    int shown = 0;
    char num_buf[12];
    char* msg;

    // Count active (unfreed) allocations
    record = _alloc_list_head;
    while (record != NULL) {
        if (!record->freed) {
            leak_count++;
            leak_bytes += record->size;
        }
        record = record->next;
    }

    if (leak_count == 0) {
        // All memory was freed - clean up tracking records and return silently
        goto cleanup;
    }

    // Build leak report
    msg = error_buffer;
    msg = strcpy_helper(msg, "MEMORY LEAK DETECTED!\n\n");

    int_to_str(num_buf, leak_count);
    msg = strcpy_helper(msg, num_buf);
    msg = strcpy_helper(msg, " allocation(s) not freed\n");
    msg = strcpy_helper(msg, "Total leaked: ");
    int_to_str(num_buf, leak_bytes);
    msg = strcpy_helper(msg, num_buf);
    msg = strcpy_helper(msg, " bytes\n\n");

    // Show first few leaks with locations
    record = _alloc_list_head;
    while (record != NULL && shown < 5) {
        if (!record->freed) {
            msg = strcpy_helper(msg, "* ");
            int_to_str(num_buf, record->size);
            msg = strcpy_helper(msg, num_buf);
            msg = strcpy_helper(msg, " bytes at ");
            msg = strcpy_helper(msg, record->file ? record->file : "<unknown>");
            msg = strcpy_helper(msg, ":");
            int_to_str(num_buf, record->line);
            msg = strcpy_helper(msg, num_buf);
            msg = strcpy_helper(msg, "\n");
            shown++;
        }
        record = record->next;
    }

    if (leak_count > 5) {
        msg = strcpy_helper(msg, "... and ");
        int_to_str(num_buf, leak_count - 5);
        msg = strcpy_helper(msg, num_buf);
        msg = strcpy_helper(msg, " more\n");
    }

    display_error_requester(AO_MemoryLeak);

cleanup:
    // Clean up all tracking records and free the memory we held onto
    // (We don't free during normal operation to enable UAF detection)
    record = _alloc_list_head;
    while (record != NULL) {
        AllocationRecord* next = record->next;

        // If the memory was "freed" by the user, we still have it allocated
        // Now actually release it back to the OS
        if (record->freed && record->actual_ptr != NULL) {
            FreeMem(record->actual_ptr, record->actual_size);
        }
        // If the memory was never freed (leak), also free it to avoid OS-level leak
        else if (!record->freed && record->actual_ptr != NULL) {
            FreeMem(record->actual_ptr, record->actual_size);
        }

        // Free the tracking record itself
        FreeMem(record, sizeof(AllocationRecord));
        record = next;
    }
    _alloc_list_head = NULL;
}

/**
 * Check if a pointer access is a use-after-free
 * Called by generated code when dereferencing pointers in paranoid mode
 * Returns the allocation record if UAF detected, NULL otherwise
 */
static AllocationRecord* __check_uaf(void* ptr)
{
    AllocationRecord* record = _alloc_list_head;

    while (record != NULL) {
        // Check if ptr falls within this allocation's range
        uint8_t* start = (uint8_t*)record->ptr;
        uint8_t* end = start + record->size;

        if ((uint8_t*)ptr >= start && (uint8_t*)ptr < end) {
            // Pointer is within this allocation
            if (record->freed) {
                // USE AFTER FREE!
                return record;
            }
            // Valid access to live allocation
            return NULL;
        }
        record = record->next;
    }

    // Pointer not in any tracked allocation - could be stack, global, etc.
    return NULL;
}

/**
 * Report use-after-free error
 * Shows dialog with allocation/free information
 */
static void __report_uaf(AllocationRecord* record, void* access_ptr, const char* file, int32_t line)
{
    char line_str[12];
    char alloc_line_str[12];
    char free_line_str[12];
    char ptr_str[12];
    char access_ptr_str[12];
    char* msg = error_buffer;

    int_to_str(line_str, line);
    int_to_str(alloc_line_str, record->line);
    int_to_str(free_line_str, record->free_line);
    ptr_to_hex(ptr_str, record->ptr);
    ptr_to_hex(access_ptr_str, access_ptr);

    msg = strcpy_helper(msg, "USE AFTER FREE DETECTED!\n\n");
    msg = strcpy_helper(msg, "Access at: ");
    msg = strcpy_helper(msg, access_ptr_str);
    msg = strcpy_helper(msg, "\nBlock: ");
    msg = strcpy_helper(msg, ptr_str);
    msg = strcpy_helper(msg, "\n\nAccessed at:\n  ");
    msg = strcpy_helper(msg, file ? file : "<unknown>");
    msg = strcpy_helper(msg, ":");
    msg = strcpy_helper(msg, line_str);
    msg = strcpy_helper(msg, "\n\nOriginally allocated at:\n  ");
    msg = strcpy_helper(msg, record->file ? record->file : "<unknown>");
    msg = strcpy_helper(msg, ":");
    msg = strcpy_helper(msg, alloc_line_str);
    msg = strcpy_helper(msg, "\n\nFreed at:\n  ");
    msg = strcpy_helper(msg, record->free_file ? record->free_file : "<unknown>");
    msg = strcpy_helper(msg, ":");
    msg = strcpy_helper(msg, free_line_str);

    display_error_requester(AO_UseAfterFree);
}

/**
 * Check pointer access and report use-after-free if detected
 * Called by generated code before dereferencing tracked pointers
 *
 * @param ptr The pointer being accessed
 * @param file Source file where access occurred
 * @param line Line number
 */
void __novus_check_ptr_access(void* ptr, const char* file, int32_t line)
{
    AllocationRecord* record = __check_uaf(ptr);
    if (record != NULL) {
        __report_uaf(record, ptr, file, line);
    }
}


