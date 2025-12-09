#ifndef _INLINE_PALETTE_H
#define _INLINE_PALETTE_H

#ifndef CLIB_PALETTE_PROTOS_H
#define CLIB_PALETTE_PROTOS_H
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
#ifndef  GADGETS_PALETTE_H
#include <gadgets/palette.h>
#endif

#ifndef PALETTE_BASE_NAME
#define PALETTE_BASE_NAME PaletteBase
#endif

#define PALETTE_GetClass() ({ \
  register char * _PALETTE_GetClass__bn __asm("a6") = (char *) (PALETTE_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_PALETTE_GetClass__bn - 30))(_PALETTE_GetClass__bn); \
})

#endif /*  _INLINE_PALETTE_H  */
