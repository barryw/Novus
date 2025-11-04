// Novus Runtime Library
// Assert failure handler using AmigaOS EasyRequest

#include <exec/types.h>
#include <exec/libraries.h>
#include <exec/alerts.h>
#include <intuition/intuition.h>
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
 * Assert failure handler - displays error using EasyRequest
 *
 * @param file Source file where assertion failed
 * @param line Line number
 * @param col Column number
 * @param message Optional error message (can be NULL)
 */
void __novus_assert_failed(const char* file, int32_t line, int32_t col, const char* message)
{
    struct Library *IntuitionBase;
    struct EasyStruct es;
    char line_str[12];
    char col_str[12];
    char* ptr = error_buffer;

    // Open intuition.library ourselves (don't rely on global)
    IntuitionBase = OpenLibrary("intuition.library", 33L);
    if (IntuitionBase == NULL) {
        // Can't show requester - use Alert() as fallback
        // This displays a system-level Guru Meditation-style alert
        Alert(AT_DeadEnd | AN_NovusLib | AG_NovusError | AO_Assert);
        // Alert never returns for AT_DeadEnd
        return;  // Should never reach here
    }

    // Convert numbers to strings
    int_to_str(line_str, line);
    int_to_str(col_str, col);

    // Build the error message manually (strcpy_helper returns pointer to end)
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

    // Set up EasyRequest structure
    es.es_StructSize   = sizeof(struct EasyStruct);
    es.es_Flags        = 0;
    es.es_Title        = "Novus Runtime Error";
    es.es_TextFormat   = error_buffer;
    es.es_GadgetFormat = "OK";

    // Display the requester
    // NULL window means it appears on default public screen
    EasyRequest(NULL, &es, NULL);

    // Close the library
    CloseLibrary(IntuitionBase);
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
    struct Library *IntuitionBase;
    struct EasyStruct es;
    char line_str[12];
    char col_str[12];
    char* ptr = error_buffer;

    // Open intuition.library ourselves (don't rely on global)
    IntuitionBase = OpenLibrary("intuition.library", 33L);
    if (IntuitionBase == NULL) {
        // Can't show requester - use Alert() as fallback
        // This displays a system-level Guru Meditation-style alert
        Alert(AT_DeadEnd | AN_NovusLib | AG_NovusError | AO_Panic);
        // Alert never returns for AT_DeadEnd
        return;  // Should never reach here
    }

    // Convert numbers to strings
    int_to_str(line_str, line);
    int_to_str(col_str, col);

    // Build the error message manually (strcpy_helper returns pointer to end)
    ptr = strcpy_helper(ptr, "PANIC: ");
    ptr = strcpy_helper(ptr, message);
    ptr = strcpy_helper(ptr, "\n\nFile: ");
    ptr = strcpy_helper(ptr, file);
    ptr = strcpy_helper(ptr, "\nLine: ");
    ptr = strcpy_helper(ptr, line_str);
    ptr = strcpy_helper(ptr, ", Column: ");
    ptr = strcpy_helper(ptr, col_str);

    // Set up EasyRequest structure
    es.es_StructSize   = sizeof(struct EasyStruct);
    es.es_Flags        = 0;
    es.es_Title        = "Novus Runtime Error";
    es.es_TextFormat   = error_buffer;
    es.es_GadgetFormat = "OK";

    // Display the requester
    // NULL window means it appears on default public screen
    EasyRequest(NULL, &es, NULL);

    // Close the library
    CloseLibrary(IntuitionBase);

    // Return (codegen will handle function exit and defer cleanup)
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
    struct Library *IntuitionBase;
    struct EasyStruct es;
    char index_str[12];
    char length_str[12];
    char line_str[12];
    char* ptr = error_buffer;

    // Open intuition.library
    IntuitionBase = OpenLibrary("intuition.library", 33L);
    if (IntuitionBase == NULL) {
        // Can't show requester - use Alert() as fallback
        // This displays a system-level Guru Meditation-style alert
        Alert(AT_DeadEnd | AN_NovusLib | AG_NovusError | AO_BoundsCheck);
        // Alert never returns for AT_DeadEnd
        return;  // Should never reach here
    }

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
        struct Library *IntuitionBase;
        struct EasyStruct es;
        char line_str[12];
        char* ptr = error_buffer;

        // Open intuition.library
        IntuitionBase = OpenLibrary("intuition.library", 33L);
        if (IntuitionBase == NULL) {
            // Can't show requester - use Alert() as fallback
            // This displays a system-level Guru Meditation-style alert
            Alert(AT_DeadEnd | AN_NovusLib | AG_NovusError | AO_DivByZero);
            // Alert never returns for AT_DeadEnd
            return;  // Should never reach here
        }

        // Convert line number to string
        int_to_str(line_str, line);

        // Build the error message
        ptr = strcpy_helper(ptr, "PANIC: Division by zero!\n\n");
        ptr = strcpy_helper(ptr, "File: ");
        ptr = strcpy_helper(ptr, file);
        ptr = strcpy_helper(ptr, "\nLine: ");
        ptr = strcpy_helper(ptr, line_str);

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
}
