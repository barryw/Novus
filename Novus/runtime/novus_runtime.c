// Novus Runtime Library
// Assert failure handler using AmigaOS EasyRequest

#include <exec/types.h>
#include <exec/libraries.h>
#include <exec/alerts.h>
#include <exec/memory.h>
#include <intuition/intuition.h>
#include <graphics/gfxbase.h>
#include <proto/exec.h>
#include <proto/intuition.h>
#include <stdint.h>

// Custom alert codes for Novus runtime errors
#define AN_NovusLib    (0x7F000000)  // User application alert
#define AG_NovusError  (0x00000001)  // Generic Novus error
#define AO_BoundsCheck (0x00000002)  // Bounds check failure
#define AO_DivByZero   (0x00000003)  // Division by zero
#define AO_Panic       (0x00000004)  // Panic
#define AO_Assert      (0x00000005)  // Assert failure

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

uint32_t i8_to_string(int8_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[5]; // -128 to 127 requires at most 4 chars + null
    int_to_str(temp, (int32_t)value);
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

uint32_t i16_to_string(int16_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[7]; // -32768 to 32767 requires at most 6 chars + null
    int_to_str(temp, (int32_t)value);
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

uint32_t i32_to_string(int32_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[12]; // -2147483648 to 2147483647 requires at most 11 chars + null
    int_to_str(temp, value);
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

uint32_t i64_to_string(int64_t value, uint8_t* buffer, uint32_t buffer_size) {
    // For now, truncate to i32 range. Full i64 support would require more complex implementation
    if (value > 2147483647LL) value = 2147483647LL;
    if (value < -2147483648LL) value = -2147483648LL;
    return i32_to_string((int32_t)value, buffer, buffer_size);
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

uint32_t u8_to_string(uint8_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[4]; // 0 to 255 requires at most 3 chars + null
    uint_to_str(temp, (uint32_t)value);
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

uint32_t u16_to_string(uint16_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[6]; // 0 to 65535 requires at most 5 chars + null
    uint_to_str(temp, (uint32_t)value);
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

uint32_t u32_to_string(uint32_t value, uint8_t* buffer, uint32_t buffer_size) {
    char temp[12]; // 0 to 4294967295 requires at most 10 chars + null
    uint_to_str(temp, value);
    uint32_t len = 0;
    while (temp[len] && len < buffer_size - 1) {
        buffer[len] = temp[len];
        len++;
    }
    buffer[len] = '\0';
    return len;
}

uint32_t u64_to_string(uint64_t value, uint8_t* buffer, uint32_t buffer_size) {
    // For now, truncate to u32 range. Full u64 support would require more complex implementation
    if (value > 4294967295ULL) value = 4294967295ULL;
    return u32_to_string((uint32_t)value, buffer, buffer_size);
}

