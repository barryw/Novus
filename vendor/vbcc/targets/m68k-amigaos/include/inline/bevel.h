#ifndef _INLINE_BEVEL_H
#define _INLINE_BEVEL_H

#ifndef CLIB_BEVEL_PROTOS_H
#define CLIB_BEVEL_PROTOS_H
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
#ifndef  IMAGES_BEVEL_H
#include <images/bevel.h>
#endif

#ifndef BEVEL_BASE_NAME
#define BEVEL_BASE_NAME BevelBase
#endif

#define BEVEL_GetClass() ({ \
  register char * _BEVEL_GetClass__bn __asm("a6") = (char *) (BEVEL_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_BEVEL_GetClass__bn - 30))(_BEVEL_GetClass__bn); \
})

#endif /*  _INLINE_BEVEL_H  */
