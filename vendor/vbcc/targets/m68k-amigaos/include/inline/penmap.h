#ifndef _INLINE_PENMAP_H
#define _INLINE_PENMAP_H

#ifndef CLIB_PENMAP_PROTOS_H
#define CLIB_PENMAP_PROTOS_H
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
#ifndef  IMAGES_PENMAP_H
#include <images/penmap.h>
#endif

#ifndef PENMAP_BASE_NAME
#define PENMAP_BASE_NAME PenMapBase
#endif

#define PENMAP_GetClass() ({ \
  register char * _PENMAP_GetClass__bn __asm("a6") = (char *) (PENMAP_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_PENMAP_GetClass__bn - 30))(_PENMAP_GetClass__bn); \
})

#endif /*  _INLINE_PENMAP_H  */
