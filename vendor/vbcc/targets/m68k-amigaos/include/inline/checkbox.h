#ifndef _INLINE_CHECKBOX_H
#define _INLINE_CHECKBOX_H

#ifndef CLIB_CHECKBOX_PROTOS_H
#define CLIB_CHECKBOX_PROTOS_H
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
#ifndef  GADGETS_CHECKBOX_H
#include <gadgets/checkbox.h>
#endif

#ifndef CHECKBOX_BASE_NAME
#define CHECKBOX_BASE_NAME CheckBoxBase
#endif

#define CHECKBOX_GetClass() ({ \
  register char * _CHECKBOX_GetClass__bn __asm("a6") = (char *) (CHECKBOX_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_CHECKBOX_GetClass__bn - 30))(_CHECKBOX_GetClass__bn); \
})

#endif /*  _INLINE_CHECKBOX_H  */
