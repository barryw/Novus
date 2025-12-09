#ifndef _INLINE_BITMAP_H
#define _INLINE_BITMAP_H

#ifndef CLIB_BITMAP_PROTOS_H
#define CLIB_BITMAP_PROTOS_H
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
#ifndef  IMAGES_BITMAP_H
#include <images/bitmap.h>
#endif

#ifndef BITMAP_BASE_NAME
#define BITMAP_BASE_NAME BitMapBase
#endif

#define BITMAP_GetClass() ({ \
  register char * _BITMAP_GetClass__bn __asm("a6") = (char *) (BITMAP_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_BITMAP_GetClass__bn - 30))(_BITMAP_GetClass__bn); \
})

#endif /*  _INLINE_BITMAP_H  */
