#ifndef _INLINE_BUTTON_H
#define _INLINE_BUTTON_H

#ifndef CLIB_BUTTON_PROTOS_H
#define CLIB_BUTTON_PROTOS_H
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
#ifndef  GADGETS_BUTTON_H
#include <gadgets/button.h>
#endif

#ifndef BUTTON_BASE_NAME
#define BUTTON_BASE_NAME ButtonBase
#endif

#define BUTTON_GetClass() ({ \
  register char * _BUTTON_GetClass__bn __asm("a6") = (char *) (BUTTON_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_BUTTON_GetClass__bn - 30))(_BUTTON_GetClass__bn); \
})

#endif /*  _INLINE_BUTTON_H  */
