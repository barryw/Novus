#ifndef _INLINE_TEXTEDITOR_H
#define _INLINE_TEXTEDITOR_H

#ifndef CLIB_TEXTEDITOR_PROTOS_H
#define CLIB_TEXTEDITOR_PROTOS_H
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
#ifndef  GADGETS_TEXTEDITOR_H
#include <gadgets/texteditor.h>
#endif

#ifndef TEXTEDITOR_BASE_NAME
#define TEXTEDITOR_BASE_NAME TextFieldBase
#endif

#define TEXTEDITOR_GetClass() ({ \
  register char * _TEXTEDITOR_GetClass__bn __asm("a6") = (char *) (TEXTEDITOR_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_TEXTEDITOR_GetClass__bn - 30))(_TEXTEDITOR_GetClass__bn); \
})

#define HighlightSetFormat(object, pos, end, style) ({ \
  APTR _HighlightSetFormat_object = (object); \
  ULONG _HighlightSetFormat_pos = (pos); \
  ULONG _HighlightSetFormat_end = (end); \
  ULONG _HighlightSetFormat_style = (style); \
  ({ \
  register char * _HighlightSetFormat__bn __asm("a6") = (char *) (TEXTEDITOR_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"))) \
  (_HighlightSetFormat__bn - 36))(_HighlightSetFormat__bn, _HighlightSetFormat_object, _HighlightSetFormat_pos, _HighlightSetFormat_end, _HighlightSetFormat_style); \
});})

#endif /*  _INLINE_TEXTEDITOR_H  */
