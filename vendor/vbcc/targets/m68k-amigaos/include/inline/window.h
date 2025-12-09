#ifndef _INLINE_WINDOW_H
#define _INLINE_WINDOW_H

#ifndef CLIB_WINDOW_PROTOS_H
#define CLIB_WINDOW_PROTOS_H
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
#ifndef  CLASSES_WINDOW_H
#include <classes/window.h>
#endif

#ifndef WINDOW_BASE_NAME
#define WINDOW_BASE_NAME WindowBase
#endif

#define WINDOW_GetClass() ({ \
  register char * _WINDOW_GetClass__bn __asm("a6") = (char *) (WINDOW_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_WINDOW_GetClass__bn - 30))(_WINDOW_GetClass__bn); \
})

#endif /*  _INLINE_WINDOW_H  */
