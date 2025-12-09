#ifndef _INLINE_BATTMEM_H
#define _INLINE_BATTMEM_H

#ifndef CLIB_BATTMEM_PROTOS_H
#define CLIB_BATTMEM_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif

#ifndef BATTMEM_BASE_NAME
#define BATTMEM_BASE_NAME BattMemBase
#endif

#define ObtainBattSemaphore() ({ \
  register char * _ObtainBattSemaphore__bn __asm("a6") = (char *) (BATTMEM_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_ObtainBattSemaphore__bn - 6))(_ObtainBattSemaphore__bn); \
})

#define ReleaseBattSemaphore() ({ \
  register char * _ReleaseBattSemaphore__bn __asm("a6") = (char *) (BATTMEM_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_ReleaseBattSemaphore__bn - 12))(_ReleaseBattSemaphore__bn); \
})

#define ReadBattMem(buffer, offset, length) ({ \
  APTR _ReadBattMem_buffer = (buffer); \
  ULONG _ReadBattMem_offset = (offset); \
  ULONG _ReadBattMem_length = (length); \
  ({ \
  register char * _ReadBattMem__bn __asm("a6") = (char *) (BATTMEM_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), APTR __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_ReadBattMem__bn - 18))(_ReadBattMem__bn, _ReadBattMem_buffer, _ReadBattMem_offset, _ReadBattMem_length); \
});})

#define WriteBattMem(buffer, offset, length) ({ \
  CONST_APTR _WriteBattMem_buffer = (buffer); \
  ULONG _WriteBattMem_offset = (offset); \
  ULONG _WriteBattMem_length = (length); \
  ({ \
  register char * _WriteBattMem__bn __asm("a6") = (char *) (BATTMEM_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST_APTR __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_WriteBattMem__bn - 24))(_WriteBattMem__bn, _WriteBattMem_buffer, _WriteBattMem_offset, _WriteBattMem_length); \
});})

#endif /*  _INLINE_BATTMEM_H  */
