// Novus Runtime - Core Functions
// Minimal runtime functions that most programs need

#include "novus_runtime.h"
#include <exec/execbase.h>

// Buffer for building error messages (shared by error handlers)
char error_buffer[512];

// ============================================================================
// Interrupt Context Detection
// ============================================================================

/**
 * Check if currently in interrupt or exception context.
 *
 * This function checks ExecBase fields to determine if it's safe to call
 * Intuition functions. In interrupt context, we must NOT:
 *   - Open libraries (OpenLibrary)
 *   - Allocate memory (AllocMem, AllocVec)
 *   - Use Intuition (EasyRequest, OpenWindow, etc.)
 *   - Use any function that might Wait() or call Forbid()/Permit()
 *
 * Detection method:
 *   1. IDNestCnt >= 0: Inside Disable() - interrupts disabled, very restricted
 *   2. TDNestCnt >= 0: Inside Forbid() - task switching disabled
 *   3. ThisTask == NULL: Not in task context (interrupt handler)
 *
 * @return Non-zero if in interrupt context, 0 if safe to use Intuition
 */
int32_t __novus_in_interrupt_context(void)
{
    struct ExecBase* SysBase = *(struct ExecBase**)4L;

    // Check if interrupts are disabled (IDNestCnt >= 0 means inside Disable())
    // This is the most critical check - if interrupts are disabled, we're
    // almost certainly in an interrupt handler or critical section
    if (SysBase->IDNestCnt >= 0) {
        return 1;  // In Disable() context - NOT safe
    }

    // Check if we're in Forbid() context (task switching disabled)
    // While technically some operations work in Forbid(), it's safer to
    // avoid Intuition calls here too since they may need to reschedule
    if (SysBase->TDNestCnt >= 0) {
        return 1;  // In Forbid() context - NOT safe for Intuition
    }

    // Check if there's a valid current task
    // If ThisTask is NULL, we're in system context (supervisor mode)
    if (SysBase->ThisTask == NULL) {
        return 1;  // No current task - NOT safe
    }

    // Additional check: If we're not a Process (just a Task), we might
    // be in a context where DOS/Intuition isn't fully available.
    // However, this is less critical since most Novus programs are processes.
    // We allow Tasks to try error display but they may fail gracefully.

    return 0;  // Safe to use Intuition
}

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
 * Display error using Alert() - safe for interrupt context.
 *
 * This is the fallback when we can't use Intuition (interrupt context,
 * library open failure, etc.). Alert() is always safe to call.
 *
 * @param alert_code Alert code specifying the error type
 */
static void display_error_alert(uint32_t alert_code)
{
    // Use AT_Recovery (not AT_DeadEnd) so the system can continue
    // AT_DeadEnd would halt the system entirely
    // The alert will display on screen and user can acknowledge
    Alert(AN_NovusLib | AG_NovusError | alert_code);
}

/**
 * Common error display function - shows error requester with formatted message
 * Centralizes the pattern used by all error handlers
 *
 * INTERRUPT SAFETY: This function checks if we're in interrupt context
 * and falls back to Alert() if so. Never call this with Disable() active!
 *
 * @param alert_code Alert code to use if IntuitionBase is not available
 */
void display_error_requester(uint32_t alert_code)
{
    struct Library *IntuitionBase;
    struct EasyStruct es;

    // CRITICAL: Check if we're in interrupt/exception context
    // If so, we MUST NOT call OpenLibrary or EasyRequest - use Alert instead
    if (__novus_in_interrupt_context()) {
        display_error_alert(alert_code);
        return;
    }

    // Try to open intuition.library
    IntuitionBase = OpenLibrary("intuition.library", 33L);
    if (IntuitionBase == NULL) {
        // Can't show requester - use Alert() as fallback
        display_error_alert(alert_code);
        return;
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
 * Safe error display that explicitly handles interrupt context.
 * This is the preferred function for new code.
 *
 * @param alert_code Alert code specifying the error type
 */
void display_error_safe(uint32_t alert_code)
{
    // Delegate to display_error_requester which now handles interrupt safety
    display_error_requester(alert_code);
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

// Note: __novus_alloc_raw and __novus_free_raw are in runtime_alloc.c
// to avoid pulling in error handling dependencies
