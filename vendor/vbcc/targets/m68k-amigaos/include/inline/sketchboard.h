#ifndef _INLINE_SKETCHBOARD_H
#define _INLINE_SKETCHBOARD_H

#ifndef CLIB_SKETCHBOARD_PROTOS_H
#define CLIB_SKETCHBOARD_PROTOS_H
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
#ifndef  GADGETS_SKETCHBOARD_H
#include <gadgets/sketchboard.h>
#endif

#ifndef SKETCHBOARD_BASE_NAME
#define SKETCHBOARD_BASE_NAME SketchBoardBase
#endif

#define SKETCHBOARD_GetClass() ({ \
  register char * _SKETCHBOARD_GetClass__bn __asm("a6") = (char *) (SKETCHBOARD_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_SKETCHBOARD_GetClass__bn - 30))(_SKETCHBOARD_GetClass__bn); \
})

#endif /*  _INLINE_SKETCHBOARD_H  */
