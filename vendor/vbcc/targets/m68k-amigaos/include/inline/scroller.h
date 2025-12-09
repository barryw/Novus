#ifndef _INLINE_SCROLLER_H
#define _INLINE_SCROLLER_H

#ifndef CLIB_SCROLLER_PROTOS_H
#define CLIB_SCROLLER_PROTOS_H
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
#ifndef  GADGETS_SCROLLER_H
#include <gadgets/scroller.h>
#endif

#ifndef SCROLLER_BASE_NAME
#define SCROLLER_BASE_NAME ScrollerBase
#endif

#define SCROLLER_GetClass() ({ \
  register char * _SCROLLER_GetClass__bn __asm("a6") = (char *) (SCROLLER_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_SCROLLER_GetClass__bn - 30))(_SCROLLER_GetClass__bn); \
})

#endif /*  _INLINE_SCROLLER_H  */
