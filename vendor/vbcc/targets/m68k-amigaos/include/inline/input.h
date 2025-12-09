#ifndef _INLINE_INPUT_H
#define _INLINE_INPUT_H

#ifndef CLIB_INPUT_PROTOS_H
#define CLIB_INPUT_PROTOS_H
#endif

#ifndef  EXEC_DEVICES_H
#include <exec/devices.h>
#endif

#ifndef INPUT_BASE_NAME
#define INPUT_BASE_NAME InputBase
#endif

#define PeekQualifier() ({ \
  register char * _PeekQualifier__bn __asm("a6") = (char *) (INPUT_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"))) \
  (_PeekQualifier__bn - 42))(_PeekQualifier__bn); \
})

#endif /*  _INLINE_INPUT_H  */
