#ifndef _INLINE_GETSCREENMODE_H
#define _INLINE_GETSCREENMODE_H

#ifndef CLIB_GETSCREENMODE_PROTOS_H
#define CLIB_GETSCREENMODE_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  INTUITION_CLASSES_H
#include <intuition/classes.h>
#endif
#ifndef  GADGETS_GETSCREENMODE_H
#include <gadgets/getscreenmode.h>
#endif

#ifndef GETSCREENMODE_BASE_NAME
#define GETSCREENMODE_BASE_NAME GetScreenModeBase
#endif

#define GETSCREENMODE_GetClass() ({ \
  register char * _GETSCREENMODE_GetClass__bn __asm("a6") = (char *) (GETSCREENMODE_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_GETSCREENMODE_GetClass__bn - 30))(_GETSCREENMODE_GetClass__bn); \
})

#endif /*  _INLINE_GETSCREENMODE_H  */
