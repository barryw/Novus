// Novus Runtime Library - I/O Support
// This file provides runtime support functions for std::io

#include <proto/dos.h>
#include <proto/exec.h>
#include <stdint.h>
#include <stdarg.h>

// PutCh callback for RawDoFmt
// This function is called by exec.library/RawDoFmt for each character
// Register convention: D0 = character, A3 = PutChData (file handle)
__reg("d0") static void putch_to_handle(__reg("d0") uint8_t ch, __reg("a3") BPTR handle) {
    // Write single character to the file handle
    Write(handle, &ch, 1);
}

// Formatted output using AmigaOS RawDoFmt
// This is a variadic function that accepts printf-style format strings
int32_t write(const char* format, ...) {
    BPTR stdout_handle = Output();
    if (!stdout_handle) {
        return -1;
    }

    // Get pointer to variadic arguments on the stack
    // RawDoFmt expects a pointer to the argument array
    // The arguments are laid out sequentially after 'format' on the stack
    va_list args;
    va_start(args, format);

    // RawDoFmt expects a pointer to sequential arguments on stack
    // On 68k, va_list points to the first variadic argument
    RawDoFmt((STRPTR)format, (APTR)args, (void (*)())putch_to_handle, (APTR)stdout_handle);

    va_end(args);
    return 0;
}
