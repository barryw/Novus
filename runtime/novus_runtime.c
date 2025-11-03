// Novus Runtime Library
// Assert failure handler using AmigaOS EasyRequest

#include <exec/types.h>
#include <exec/libraries.h>
#include <intuition/intuition.h>
#include <proto/exec.h>
#include <proto/intuition.h>
#include <stdint.h>

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
        // Can't show requester, just return
        return;
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
