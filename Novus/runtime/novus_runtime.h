// Novus Runtime Library - Common Header
// Shared definitions for split runtime modules
//
// ============================================================================
// INTERRUPT SAFETY WARNING
// ============================================================================
// The Novus runtime is NOT safe to call from interrupt context!
//
// The following functions allocate memory or use Intuition, which are not
// interrupt-safe:
//   - __novus_panic() - shows GUI requester
//   - __novus_assert_fail() - shows GUI requester
//   - __novus_bounds_check_fail() - shows GUI requester
//   - display_error_requester() - opens Intuition window
//   - Any memory allocation (uses Forbid/Permit, not Disable/Enable)
//   - String formatting functions (may allocate)
//
// If you need to handle errors in interrupt context, use raw AmigaOS calls
// within `unsafe` blocks, or signal a task to handle the error.
//
// ============================================================================
// MMU LIBRARY FALLBACK BEHAVIOR
// ============================================================================
// Null page protection uses mmu.library (by Thomas Richter) when available.
//
// Detection strategy:
// 1. Check CPU type from AttnFlags (68030/040/060 have built-in MMU)
// 2. Try to open mmu.library for accurate detection and capabilities
// 3. If mmu.library unavailable, fall back gracefully:
//    - __has_mmu_library() returns 0
//    - __novus_enable_null_page_protection() is a no-op
//    - Null pointer dereferences will not be caught by the runtime
//    - The program will crash at the hardware level instead
//
// mmu.library is available from Aminet: util/libs/MMULib.lha
// ============================================================================

#ifndef NOVUS_RUNTIME_H
#define NOVUS_RUNTIME_H

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
#define AN_NovusLib    (0x7F000000)
#define AG_NovusError  (0x00000001)
#define AO_BoundsCheck (0x00000002)
#define AO_DivByZero   (0x00000003)
#define AO_Panic       (0x00000004)
#define AO_Assert      (0x00000005)
#define AO_MemoryLeak  (0x00000006)
#define AO_DoubleFree  (0x00000007)
#define AO_BufferOverflow (0x00000008)
#define AO_UseAfterFree   (0x00000009)
#define AO_NullPointer    (0x0000000A)
#define AO_StackOverflow  (0x0000000B)
#define AO_InterruptPanic (0x0000000C)

// ============================================================================
// Interrupt Context Detection
// ============================================================================
// These functions check if we're in interrupt/exception context where it's
// unsafe to call certain AmigaOS functions (Intuition, memory allocation, etc.)
//
// __novus_in_interrupt_context() returns non-zero if:
//   - IDNestCnt >= 0 (inside Disable() context)
//   - TDNestCnt >= 0 (inside Forbid() context) - optional, less strict
//   - We're not running as a regular task (interrupt handler, exception)
//
// When in interrupt context, error handlers use Alert() instead of
// EasyRequest() to avoid deadlocks and crashes.
// ============================================================================

// Check if currently in interrupt/exception context
// Returns non-zero if it's unsafe to call Intuition functions
int32_t __novus_in_interrupt_context(void);

// Safe error display that works in any context
// Uses Alert() in interrupt context, EasyRequest() otherwise
void display_error_safe(uint32_t alert_code);

// ============================================================================
// Core Memory Functions
// ============================================================================
// These functions are always included and are used for safe memory operations.
//
// __novus_memcpy is used for STRICT ALIASING SAFE type punning:
//   Instead of: *(uint32_t*)some_u8_ptr = value;  // UNSAFE - strict aliasing violation
//   Use:        __novus_memcpy((uint8_t*)dest, (uint8_t*)&value, sizeof(value));
//
// The code generator uses this pattern for struct assignments, array element
// copies, and any operation that could trigger strict aliasing issues. This
// ensures the generated code is safe with -fstrict-aliasing optimizations.
// ============================================================================

void __novus_memset(void* dest, int value, uint32_t n);
void __novus_memcpy(uint8_t* dest, const uint8_t* src, uint32_t n);
uint32_t strlen(const char* str);

// VBCC WORKAROUND: Comparison functions that force sequence points.
// VBCC's optimizer can move stack cleanup between a comparison and its branch,
// clobbering condition flags. Using function calls creates sequence points
// that prevent this reordering.
int32_t __novus_is_null(void* ptr);     // Returns 1 if ptr is NULL
int32_t __novus_cmp_eq_i32(int32_t a, int32_t b);  // Returns 1 if a == b
int32_t __novus_cmp_ne_i32(int32_t a, int32_t b);  // Returns 1 if a != b

// Raw memory allocation (bypasses tracking)
// Use for stdlib internals where tracking would interfere
void* __novus_alloc_raw(uint32_t size, uint32_t flags);
void __novus_free_raw(void* ptr, uint32_t size);

// Error display (shared by error handlers)
extern char error_buffer[512];
char* strcpy_helper(char* dest, const char* src);
void int_to_str(char* buf, int32_t num);
void display_error_requester(uint32_t alert_code);

// Test mode support for should_panic tests
// When test mode is enabled, __novus_panic() sets flags instead of showing dialog
extern int32_t __novus_test_mode;
extern int32_t __novus_test_panic_occurred;
extern const char* __novus_test_panic_message;

// Test mode control functions (called from Novus test code)
void __novus_test_set_mode(int32_t enabled);
void __novus_test_reset_panic(void);
int32_t __novus_test_did_panic(void);
const char* __novus_test_get_panic_message(void);

// ============================================================================
// Stack Overflow Detection (Debug Builds Only)
// ============================================================================
// In debug builds, each function prologue checks if there's enough stack space
// remaining before allocating its local variables. This catches stack overflow
// before it corrupts memory.
//
// Usage in generated code:
//   __novus_check_stack(local_size, __FILE__, __LINE__);
//
// The function checks:
//   1. If stack pointer is approaching the stack limit
//   2. If there's enough space for the requested local variables
//
// Stack limits are determined at startup from the AmigaOS CLI stack cookie
// or the Task structure's stack bounds.
// ============================================================================

// Initialize stack bounds (called at program startup)
void __novus_init_stack_bounds(void);

// Check if stack has enough space for 'required_bytes' more
// If not, displays error and aborts
void __novus_check_stack(uint32_t required_bytes, const char* func_name, int32_t line);

// Stack bound tracking (set during initialization)
extern uint32_t __novus_stack_base;   // Top of stack (highest address)
extern uint32_t __novus_stack_limit;  // Bottom of stack (lowest address)
extern uint32_t __novus_stack_guard;  // Guard zone size (default 256 bytes)

// ============================================================================
// dbg!() Macro Support
// ============================================================================
// Debug print functions for the dbg!() macro.
// These print to stdout in the format: [file:line:col] expr = value
// Each function returns nothing - the compiler generates code to return
// the original expression value after calling the debug function.
// ============================================================================

void __novus_dbg_i32(const char* location, const char* expr, int32_t value);
void __novus_dbg_u32(const char* location, const char* expr, uint32_t value);
void __novus_dbg_bool(const char* location, const char* expr, int32_t value);
void __novus_dbg_ptr(const char* location, const char* expr, void* value);
void __novus_dbg_str(const char* location, const char* expr, const char* value);

#endif // NOVUS_RUNTIME_H
