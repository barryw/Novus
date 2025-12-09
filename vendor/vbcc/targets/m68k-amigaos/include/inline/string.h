#ifndef _INLINE_STRING_H
#define _INLINE_STRING_H

#ifndef CLIB_STRING_PROTOS_H
#define CLIB_STRING_PROTOS_H
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
#ifndef  GADGETS_STRING_H
#include <gadgets/string.h>
#endif

#ifndef STRING_BASE_NAME
#define STRING_BASE_NAME StringBase
#endif

#define STRING_GetClass() ({ \
  register char * _STRING_GetClass__bn __asm("a6") = (char *) (STRING_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_STRING_GetClass__bn - 30))(_STRING_GetClass__bn); \
})

#endif /*  _INLINE_STRING_H  */
