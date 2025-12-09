#ifndef _INLINE_AREXX_H
#define _INLINE_AREXX_H

#ifndef CLIB_AREXX_PROTOS_H
#define CLIB_AREXX_PROTOS_H
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
#ifndef  CLASSES_AREXX_H
#include <classes/arexx.h>
#endif

#ifndef AREXX_BASE_NAME
#define AREXX_BASE_NAME ARexxBase
#endif

#define AREXX_GetClass() ({ \
  register char * _AREXX_GetClass__bn __asm("a6") = (char *) (AREXX_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_AREXX_GetClass__bn - 30))(_AREXX_GetClass__bn); \
})

#endif /*  _INLINE_AREXX_H  */
