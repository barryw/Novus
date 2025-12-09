#ifndef _INLINE_GLYPH_H
#define _INLINE_GLYPH_H

#ifndef CLIB_GLYPH_PROTOS_H
#define CLIB_GLYPH_PROTOS_H
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
#ifndef  IMAGES_GLYPH_H
#include <images/glyph.h>
#endif

#ifndef GLYPH_BASE_NAME
#define GLYPH_BASE_NAME GlyphBase
#endif

#define GLYPH_GetClass() ({ \
  register char * _GLYPH_GetClass__bn __asm("a6") = (char *) (GLYPH_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_GLYPH_GetClass__bn - 30))(_GLYPH_GetClass__bn); \
})

#endif /*  _INLINE_GLYPH_H  */
