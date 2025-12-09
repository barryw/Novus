#ifndef _INLINE_DRAWLIST_H
#define _INLINE_DRAWLIST_H

#ifndef CLIB_DRAWLIST_PROTOS_H
#define CLIB_DRAWLIST_PROTOS_H
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
#ifndef  IMAGES_DRAWLIST_H
#include <images/drawlist.h>
#endif

#ifndef DRAWLIST_BASE_NAME
#define DRAWLIST_BASE_NAME DrawListBase
#endif

#define DRAWLIST_GetClass() ({ \
  register char * _DRAWLIST_GetClass__bn __asm("a6") = (char *) (DRAWLIST_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_DRAWLIST_GetClass__bn - 30))(_DRAWLIST_GetClass__bn); \
})

#endif /*  _INLINE_DRAWLIST_H  */
