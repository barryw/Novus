// Novus Runtime - Error Handlers
// Assert, panic, bounds check, division check, and try operator handlers

#include "novus_runtime.h"

// Test mode globals for should_panic tests
int32_t __novus_test_mode = 0;
int32_t __novus_test_panic_occurred = 0;
const char* __novus_test_panic_message = NULL;

// Test mode control functions
NOVUS_RUNTIME_SECTION(__novus_test_set_mode) void __novus_test_set_mode(int32_t enabled) {
    __novus_test_mode = enabled;
}

NOVUS_RUNTIME_SECTION(__novus_test_reset_panic) void __novus_test_reset_panic(void) {
    __novus_test_panic_occurred = 0;
    __novus_test_panic_message = NULL;
}

NOVUS_RUNTIME_SECTION(__novus_test_did_panic) int32_t __novus_test_did_panic(void) {
    return __novus_test_panic_occurred;
}

NOVUS_RUNTIME_SECTION(__novus_test_get_panic_message) const char* __novus_test_get_panic_message(void) {
    return __novus_test_panic_message;
}

// DOS inline functions for try_failed
// Output() - LVO -60, returns file handle in d0
int32_t __dos_output(__reg("a6") void* dosBase) = "\tjsr\t-60(a6)";
// Write(file, buffer, length) - LVO -48, file in d1, buffer in d2, length in d3
int32_t __dos_write(__reg("a6") void* dosBase, __reg("d1") int32_t file, __reg("d2") const char* buffer, __reg("d3") int32_t length) = "\tjsr\t-48(a6)";

/**
 * Assert failure handler - displays error using EasyRequest
 *
 * In test mode (for @test(should_panic)), sets flags instead of showing dialog.
 *
 * @param file Source file where assertion failed
 * @param line Line number
 * @param col Column number
 * @param message Optional error message (can be NULL)
 */
NOVUS_RUNTIME_SECTION(__novus_assert_failed) void __novus_assert_failed(const char* file, int32_t line, int32_t col, const char* message)
{
    // In test mode, record the panic and return without showing dialog
    if (__novus_test_mode) {
        __novus_test_panic_occurred = 1;
        __novus_test_panic_message = message;
        return;
    }

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
 * Try operator error handler - prints error and returns
 * Called when ? operator fails in a function returning i32 (like main).
 * Prints "Error: TypeName::VariantName\n" to the console.
 *
 * @param type_name The error type name (e.g., "AudioError")
 * @param tag The enum variant tag (0, 1, 2, ...)
 * @param variant_names Comma-separated variant names (e.g., "Ok,Err,Other")
 */
NOVUS_RUNTIME_SECTION(__novus_try_failed) void __novus_try_failed(const char* type_name, int32_t tag, const char* variant_names)
{
    struct Library *dosBase;
    char msg[128];
    char* ptr = msg;
    const char* variant = variant_names;
    int current_tag = 0;
    int32_t len;
    int32_t fh;

    // Find the variant name by counting commas
    while (current_tag < tag && *variant) {
        if (*variant == ',') {
            current_tag++;
        }
        variant++;
    }

    // Build: "Error: TypeName::VariantName\n"
    ptr = strcpy_helper(ptr, "Error: ");
    ptr = strcpy_helper(ptr, type_name);
    ptr = strcpy_helper(ptr, "::");

    // Copy variant name until comma or end
    while (*variant && *variant != ',') {
        *ptr++ = *variant++;
    }
    *ptr++ = '\n';
    *ptr = '\0';

    // Calculate length
    len = (int32_t)(ptr - msg);

    // Print to stdout using DOS Write
    dosBase = OpenLibrary("dos.library", 0L);
    if (dosBase != NULL) {
        fh = __dos_output(dosBase);
        if (fh != 0) {
            __dos_write(dosBase, fh, msg, len);
        }
        CloseLibrary(dosBase);
    }
}

/**
 * Panic handler - displays error using EasyRequest and halts execution
 * This is for unrecoverable runtime errors (never elided, even in release)
 *
 * In test mode (for @test(should_panic)), sets flags instead of showing dialog.
 *
 * @param message Error message to display
 * @param file Source file where panic occurred
 * @param line Line number
 * @param col Column number
 */
NOVUS_RUNTIME_SECTION(__novus_panic) void __novus_panic(const char* message, const char* file, int32_t line, int32_t col)
{
    // In test mode, record the panic and return without showing dialog
    if (__novus_test_mode) {
        __novus_test_panic_occurred = 1;
        __novus_test_panic_message = message;
        return;
    }

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
 * In test mode (for @test(should_panic)), sets flags instead of showing dialog.
 *
 * @param index Array index that was out of bounds
 * @param length Array length
 * @param file Source file where access occurred
 * @param line Line number
 */
NOVUS_RUNTIME_SECTION(__novus_bounds_check_failed) void __novus_bounds_check_failed(int32_t index, int32_t length, const char* file, int32_t line)
{
    // In test mode, record the panic and return without showing dialog
    if (__novus_test_mode) {
        __novus_test_panic_occurred = 1;
        __novus_test_panic_message = "Array index out of bounds";
        return;
    }

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
 * In test mode (for @test(should_panic)), sets flags instead of showing dialog.
 *
 * @param divisor The divisor value
 * @param file Source file where division occurred
 * @param line Line number
 */
NOVUS_RUNTIME_SECTION(__novus_div_check) void __novus_div_check(int32_t divisor, const char* file, int32_t line)
{
    if (divisor == 0) {
        // In test mode, record the panic and return without showing dialog
        if (__novus_test_mode) {
            __novus_test_panic_occurred = 1;
            __novus_test_panic_message = "Division by zero";
            return;
        }

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
// Stack Overflow Detection
// ============================================================================

// VBCC inline assembly function to get current stack pointer
// Returns SP (A7) in D0, the return register for uint32_t
uint32_t __get_stack_pointer(void) = "\tmove.l\tsp,d0";

// Stack bounds - set during initialization
uint32_t __novus_stack_base = 0;   // Top of stack (highest address)
uint32_t __novus_stack_limit = 0;  // Bottom of stack (lowest address)
uint32_t __novus_stack_guard = 256; // Guard zone (256 bytes default)

/**
 * Initialize stack bounds from AmigaOS task structure.
 * Called once at program startup.
 */
NOVUS_RUNTIME_SECTION(__novus_init_stack_bounds) void __novus_init_stack_bounds(void)
{
    struct Task* task;
    struct Library* SysBase = *(struct Library**)4L;

    // Get current task
    task = (struct Task*)FindTask(NULL);
    if (task == NULL) {
        return; // Can't detect stack bounds
    }

    // Get stack bounds from Task structure
    // tc_SPLower = bottom of stack (lowest address)
    // tc_SPUpper = top of stack (highest address)
    __novus_stack_limit = (uint32_t)task->tc_SPLower;
    __novus_stack_base = (uint32_t)task->tc_SPUpper;
}

/**
 * Check if there's enough stack space remaining.
 * Called at function entry in debug builds.
 *
 * On 68k, the stack grows downward (from high addresses to low).
 * We check if: current_sp - required_bytes > stack_limit + guard
 *
 * @param required_bytes Bytes needed for this function's stack frame
 * @param func_name Name of the function being entered
 * @param line Line number of function definition
 */
NOVUS_RUNTIME_SECTION(__novus_check_stack) void __novus_check_stack(uint32_t required_bytes, const char* func_name, int32_t line)
{
    uint32_t current_sp;
    uint32_t safe_limit;
    uint32_t bytes_remaining;
    char* ptr;
    char line_str[12];
    char remaining_str[12];
    char required_str[12];

    // Get current stack pointer using VBCC inline assembly function
    current_sp = __get_stack_pointer();

    // If stack bounds not initialized, skip check
    if (__novus_stack_limit == 0) {
        return;
    }

    // Calculate safe limit (stack_limit + guard zone)
    safe_limit = __novus_stack_limit + __novus_stack_guard;

    // Check if we have enough space
    if (current_sp > safe_limit && (current_sp - safe_limit) >= required_bytes) {
        return; // OK - enough space
    }

    // In test mode, record and return
    if (__novus_test_mode) {
        __novus_test_panic_occurred = 1;
        __novus_test_panic_message = "Stack overflow";
        return;
    }

    // Stack overflow detected!
    bytes_remaining = (current_sp > __novus_stack_limit) ?
                      (current_sp - __novus_stack_limit) : 0;

    // Convert numbers to strings
    int_to_str(line_str, line);
    int_to_str(remaining_str, (int32_t)bytes_remaining);
    int_to_str(required_str, (int32_t)required_bytes);

    // Build error message
    ptr = error_buffer;
    ptr = strcpy_helper(ptr, "PANIC: Stack Overflow!\n\n");
    ptr = strcpy_helper(ptr, "Function: ");
    ptr = strcpy_helper(ptr, func_name);
    ptr = strcpy_helper(ptr, "\nLine: ");
    ptr = strcpy_helper(ptr, line_str);
    ptr = strcpy_helper(ptr, "\n\nStack remaining: ");
    ptr = strcpy_helper(ptr, remaining_str);
    ptr = strcpy_helper(ptr, " bytes\nRequired: ");
    ptr = strcpy_helper(ptr, required_str);
    ptr = strcpy_helper(ptr, " bytes\n\nIncrease stack size with:\n");
    ptr = strcpy_helper(ptr, "  #[stack_size(65536)]");

    display_error_requester(AO_StackOverflow);
}

// ============================================================================
// dbg!() Macro Support
// ============================================================================
// These functions print debug information to stdout for the dbg!() macro.
// Format: [file:line:col] expr = value
//
// In debug builds, dbg!(expr) expands to:
//   __novus_dbg_i32("source.novus:10:5", "my_var", my_var)
// and returns the value unchanged.
//
// In release builds, dbg!(expr) should be a no-op (just returns expr).
// ============================================================================

/**
 * Debug print for i32 values
 * Prints: [file:line] expr = value\n
 */
NOVUS_RUNTIME_SECTION(__novus_dbg_i32) void __novus_dbg_i32(const char* location, const char* expr, int32_t value)
{
    struct Library *dosBase;
    char msg[256];
    char value_str[12];
    char* ptr = msg;
    int32_t len;
    int32_t fh;

    int_to_str(value_str, value);

    ptr = strcpy_helper(ptr, "[");
    ptr = strcpy_helper(ptr, location);
    ptr = strcpy_helper(ptr, "] ");
    ptr = strcpy_helper(ptr, expr);
    ptr = strcpy_helper(ptr, " = ");
    ptr = strcpy_helper(ptr, value_str);
    *ptr++ = '\n';
    *ptr = '\0';

    len = (int32_t)(ptr - msg);

    dosBase = OpenLibrary("dos.library", 0L);
    if (dosBase != NULL) {
        fh = __dos_output(dosBase);
        if (fh != 0) {
            __dos_write(dosBase, fh, msg, len);
        }
        CloseLibrary(dosBase);
    }
}

/**
 * Debug print for u32 values
 */
NOVUS_RUNTIME_SECTION(__novus_dbg_u32) void __novus_dbg_u32(const char* location, const char* expr, uint32_t value)
{
    // For now, just cast to i32 and print (works for values < 2^31)
    __novus_dbg_i32(location, expr, (int32_t)value);
}

/**
 * Debug print for bool values
 */
NOVUS_RUNTIME_SECTION(__novus_dbg_bool) void __novus_dbg_bool(const char* location, const char* expr, int32_t value)
{
    struct Library *dosBase;
    char msg[256];
    char* ptr = msg;
    int32_t len;
    int32_t fh;

    ptr = strcpy_helper(ptr, "[");
    ptr = strcpy_helper(ptr, location);
    ptr = strcpy_helper(ptr, "] ");
    ptr = strcpy_helper(ptr, expr);
    ptr = strcpy_helper(ptr, " = ");
    ptr = strcpy_helper(ptr, value ? "true" : "false");
    *ptr++ = '\n';
    *ptr = '\0';

    len = (int32_t)(ptr - msg);

    dosBase = OpenLibrary("dos.library", 0L);
    if (dosBase != NULL) {
        fh = __dos_output(dosBase);
        if (fh != 0) {
            __dos_write(dosBase, fh, msg, len);
        }
        CloseLibrary(dosBase);
    }
}

/**
 * Debug print for pointer values (prints as hex)
 */
NOVUS_RUNTIME_SECTION(__novus_dbg_ptr) void __novus_dbg_ptr(const char* location, const char* expr, void* value)
{
    struct Library *dosBase;
    char msg[256];
    char hex_str[12];
    char* ptr = msg;
    int32_t len;
    int32_t fh;
    uint32_t addr = (uint32_t)value;
    int i;

    // Convert to hex string
    hex_str[0] = '0';
    hex_str[1] = 'x';
    for (i = 0; i < 8; i++) {
        int nibble = (addr >> (28 - i * 4)) & 0xF;
        hex_str[2 + i] = nibble < 10 ? '0' + nibble : 'a' + (nibble - 10);
    }
    hex_str[10] = '\0';

    ptr = strcpy_helper(ptr, "[");
    ptr = strcpy_helper(ptr, location);
    ptr = strcpy_helper(ptr, "] ");
    ptr = strcpy_helper(ptr, expr);
    ptr = strcpy_helper(ptr, " = ");
    ptr = strcpy_helper(ptr, hex_str);
    *ptr++ = '\n';
    *ptr = '\0';

    len = (int32_t)(ptr - msg);

    dosBase = OpenLibrary("dos.library", 0L);
    if (dosBase != NULL) {
        fh = __dos_output(dosBase);
        if (fh != 0) {
            __dos_write(dosBase, fh, msg, len);
        }
        CloseLibrary(dosBase);
    }
}

/**
 * Debug print for string values (*u8)
 */
NOVUS_RUNTIME_SECTION(__novus_dbg_str) void __novus_dbg_str(const char* location, const char* expr, const char* value)
{
    struct Library *dosBase;
    char msg[512];
    char* ptr = msg;
    int32_t len;
    int32_t fh;

    ptr = strcpy_helper(ptr, "[");
    ptr = strcpy_helper(ptr, location);
    ptr = strcpy_helper(ptr, "] ");
    ptr = strcpy_helper(ptr, expr);
    ptr = strcpy_helper(ptr, " = \"");
    if (value != NULL) {
        // Copy up to 200 chars to avoid buffer overflow
        int count = 0;
        while (*value && count < 200) {
            *ptr++ = *value++;
            count++;
        }
        if (*value) {
            ptr = strcpy_helper(ptr, "...");
        }
    } else {
        ptr = strcpy_helper(ptr, "(null)");
    }
    *ptr++ = '"';
    *ptr++ = '\n';
    *ptr = '\0';

    len = (int32_t)(ptr - msg);

    dosBase = OpenLibrary("dos.library", 0L);
    if (dosBase != NULL) {
        fh = __dos_output(dosBase);
        if (fh != 0) {
            __dos_write(dosBase, fh, msg, len);
        }
        CloseLibrary(dosBase);
    }
}
