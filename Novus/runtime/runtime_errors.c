// Novus Runtime - Error Handlers
// Assert, panic, bounds check, division check, and try operator handlers

#include "novus_runtime.h"

// Test mode globals for should_panic tests
int32_t __novus_test_mode = 0;
int32_t __novus_test_panic_occurred = 0;
const char* __novus_test_panic_message = NULL;

// Test mode control functions
void __novus_test_set_mode(int32_t enabled) {
    __novus_test_mode = enabled;
}

void __novus_test_reset_panic(void) {
    __novus_test_panic_occurred = 0;
    __novus_test_panic_message = NULL;
}

int32_t __novus_test_did_panic(void) {
    return __novus_test_panic_occurred;
}

const char* __novus_test_get_panic_message(void) {
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
void __novus_assert_failed(const char* file, int32_t line, int32_t col, const char* message)
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
void __novus_try_failed(const char* type_name, int32_t tag, const char* variant_names)
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
void __novus_panic(const char* message, const char* file, int32_t line, int32_t col)
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
void __novus_bounds_check_failed(int32_t index, int32_t length, const char* file, int32_t line)
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
void __novus_div_check(int32_t divisor, const char* file, int32_t line)
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
