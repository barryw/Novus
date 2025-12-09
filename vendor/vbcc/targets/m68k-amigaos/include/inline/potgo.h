#ifndef _INLINE_POTGO_H
#define _INLINE_POTGO_H

#ifndef CLIB_POTGO_PROTOS_H
#define CLIB_POTGO_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif

#ifndef POTGO_BASE_NAME
#define POTGO_BASE_NAME PotgoBase
#endif

#define AllocPotBits(bits) ({ \
  ULONG _AllocPotBits_bits = (bits); \
  ({ \
  register char * _AllocPotBits__bn __asm("a6") = (char *) (POTGO_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_AllocPotBits__bn - 6))(_AllocPotBits__bn, _AllocPotBits_bits); \
});})

#define FreePotBits(bits) ({ \
  ULONG _FreePotBits_bits = (bits); \
  ({ \
  register char * _FreePotBits__bn __asm("a6") = (char *) (POTGO_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_FreePotBits__bn - 12))(_FreePotBits__bn, _FreePotBits_bits); \
});})

#define WritePotgo(word, mask) ({ \
  ULONG _WritePotgo_word = (word); \
  ULONG _WritePotgo_mask = (mask); \
  ({ \
  register char * _WritePotgo__bn __asm("a6") = (char *) (POTGO_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_WritePotgo__bn - 18))(_WritePotgo__bn, _WritePotgo_word, _WritePotgo_mask); \
});})

#endif /*  _INLINE_POTGO_H  */
