// Novus Runtime - Core Functions
// Minimal runtime functions that most programs need

#include "novus_runtime.h"

// Buffer for building error messages (shared by error handlers)
char error_buffer[512];

// Simple string copy helper - returns pointer to end of copied string
char* strcpy_helper(char* dest, const char* src) {
    while (*src) {
        *dest++ = *src++;
    }
    *dest = '\0';
    return dest;
}

// Simple integer to string helper
void int_to_str(char* buf, int32_t num) {
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
void display_error_requester(uint32_t alert_code)
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
