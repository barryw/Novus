#ifndef _INLINE_INTEGER_H
#define _INLINE_INTEGER_H

#ifndef CLIB_INTEGER_PROTOS_H
#define CLIB_INTEGER_PROTOS_H
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
#ifndef  GADGETS_INTEGER_H
#include <gadgets/integer.h>
#endif

#ifndef INTEGER_BASE_NAME
#define INTEGER_BASE_NAME IntegerBase
#endif

#define INTEGER_GetClass() ({ \
  register char * _INTEGER_GetClass__bn __asm("a6") = (char *) (INTEGER_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_INTEGER_GetClass__bn - 30))(_INTEGER_GetClass__bn); \
})

#endif /*  _INLINE_INTEGER_H  */
