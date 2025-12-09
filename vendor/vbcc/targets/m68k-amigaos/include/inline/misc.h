#ifndef _INLINE_MISC_H
#define _INLINE_MISC_H

#ifndef CLIB_MISC_PROTOS_H
#define CLIB_MISC_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif

#ifndef MISC_BASE_NAME
#define MISC_BASE_NAME MiscBase
#endif

#define AllocMiscResource(unitNum, name) ({ \
  ULONG _AllocMiscResource_unitNum = (unitNum); \
  CONST_STRPTR _AllocMiscResource_name = (name); \
  ({ \
  register char * _AllocMiscResource__bn __asm("a6") = (char *) (MISC_BASE_NAME);\
  ((UBYTE * (*)(char * __asm("a6"), ULONG __asm("d0"), CONST_STRPTR __asm("a1"))) \
  (_AllocMiscResource__bn - 6))(_AllocMiscResource__bn, _AllocMiscResource_unitNum, _AllocMiscResource_name); \
});})

#define FreeMiscResource(unitNum) ({ \
  ULONG _FreeMiscResource_unitNum = (unitNum); \
  ({ \
  register char * _FreeMiscResource__bn __asm("a6") = (char *) (MISC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_FreeMiscResource__bn - 12))(_FreeMiscResource__bn, _FreeMiscResource_unitNum); \
});})

#endif /*  _INLINE_MISC_H  */
