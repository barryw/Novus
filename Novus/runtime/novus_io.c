// Novus Runtime Library - I/O Support
// This file provides runtime support functions for std::io

#include <proto/dos.h>
#include <proto/exec.h>
#include <stdint.h>
#include <stdarg.h>

// Character buffer structure for RawDoFmt callback
struct PutChData {
    char* buffer;      // Buffer to write to
    uint32_t pos;      // Current position
    uint32_t max;      // Maximum buffer size
};

// Declare the callback type that RawDoFmt expects
// RawDoFmt passes: D0=char, A3=data pointer
typedef void __saveds (*PutChFunc)(__reg("d0") uint8_t ch, __reg("a3") APTR data);

// PutCh callback for RawDoFmt
// This function is called by exec.library/RawDoFmt for each character
// Register convention: D0 = character, A3 = PutChData pointer
//
// CRITICAL: Cannot take address of register variables!
// Must write directly to buffer without creating temporary stack variables.
static void __saveds putch_to_buffer(
    __reg("d0") uint8_t ch,
    __reg("a3") struct PutChData* data
) {
    // Store character directly in buffer (no address-taking needed)
    if (data->pos < data->max) {
        data->buffer[data->pos++] = ch;
    }
}

// Formatted output using AmigaOS RawDoFmt
// This is a variadic function that accepts printf-style format strings
int32_t write(const char* format, ...) {
    BPTR stdout_handle = Output();
    if (!stdout_handle) {
        return -1;
    }

    // Check if format string contains format specifiers
    // If not, write directly without RawDoFmt to avoid va_list issues
    const char* p = format;
    int has_format = 0;
    while (*p) {
        if (*p == '%') {
            has_format = 1;
            break;
        }
        p++;
    }

    if (!has_format) {
        // No format specifiers - write directly
        uint32_t len = 0;
        p = format;
        while (*p++) len++;
        int32_t written = Write(stdout_handle, (CONST_STRPTR)format, len);
        return (written == len) ? 0 : -1;
    }

    // Stack buffer for formatted output (512 bytes should be sufficient)
    char buffer[512];
    struct PutChData data = {
        .buffer = buffer,
        .pos = 0,
        .max = sizeof(buffer) - 1  // Leave room for null terminator
    };

    // Get pointer to variadic arguments on the stack
    // RawDoFmt expects a pointer to the argument array
    va_list args;
    va_start(args, format);

    // RawDoFmt fills the buffer via callback
    // Arguments: format string, args pointer, callback function, callback data
    // CRITICAL: Cast to PutChFunc to preserve register calling convention
    RawDoFmt((STRPTR)format, (APTR)args, (PutChFunc)putch_to_buffer, (APTR)&data);

    va_end(args);

    // Null-terminate the buffer
    buffer[data.pos] = '\0';

    // Write the complete formatted string in one call
    int32_t written = Write(stdout_handle, buffer, data.pos);

    // Return 0 on success, -1 on failure
    return (written == data.pos) ? 0 : -1;
}
