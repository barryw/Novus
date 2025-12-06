// Novus Runtime Library - Common Header
// Shared definitions for split runtime modules

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

// Core memory functions (always included)
void __novus_memset(void* dest, int value, uint32_t n);
void __novus_memcpy(uint8_t* dest, const uint8_t* src, uint32_t n);
uint32_t strlen(const char* str);

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

#endif // NOVUS_RUNTIME_H
