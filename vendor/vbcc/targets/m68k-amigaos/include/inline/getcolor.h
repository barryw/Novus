#ifndef _INLINE_GETCOLOR_H
#define _INLINE_GETCOLOR_H

#ifndef CLIB_GETCOLOR_PROTOS_H
#define CLIB_GETCOLOR_PROTOS_H
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
#ifndef  GADGETS_GETCOLOR_H
#include <gadgets/getcolor.h>
#endif

#ifndef GETCOLOR_BASE_NAME
#define GETCOLOR_BASE_NAME GetColorBase
#endif

#define GETCOLOR_GetClass() ({ \
  register char * _GETCOLOR_GetClass__bn __asm("a6") = (char *) (GETCOLOR_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_GETCOLOR_GetClass__bn - 30))(_GETCOLOR_GetClass__bn); \
})

#endif /*  _INLINE_GETCOLOR_H  */
