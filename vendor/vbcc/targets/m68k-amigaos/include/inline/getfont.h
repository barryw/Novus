#ifndef _INLINE_GETFONT_H
#define _INLINE_GETFONT_H

#ifndef CLIB_GETFONT_PROTOS_H
#define CLIB_GETFONT_PROTOS_H
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
#ifndef  GADGETS_GETFONT_H
#include <gadgets/getfont.h>
#endif

#ifndef GETFONT_BASE_NAME
#define GETFONT_BASE_NAME GetFontBase
#endif

#define GETFONT_GetClass() ({ \
  register char * _GETFONT_GetClass__bn __asm("a6") = (char *) (GETFONT_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_GETFONT_GetClass__bn - 30))(_GETFONT_GetClass__bn); \
})

#endif /*  _INLINE_GETFONT_H  */
