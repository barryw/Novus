#ifndef _INLINE_REQUESTER_H
#define _INLINE_REQUESTER_H

#ifndef CLIB_REQUESTER_PROTOS_H
#define CLIB_REQUESTER_PROTOS_H
#endif

#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  INTUITION_CLASSES_H
#include <intuition/classes.h>
#endif
#ifndef  CLASSES_REQUESTER_H
#include <classes/requester.h>
#endif

#ifndef REQUESTER_BASE_NAME
#define REQUESTER_BASE_NAME RequesterBase
#endif

#define REQUESTER_GetClass() ({ \
  register char * _REQUESTER_GetClass__bn __asm("a6") = (char *) (REQUESTER_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_REQUESTER_GetClass__bn - 30))(_REQUESTER_GetClass__bn); \
})

#endif /*  _INLINE_REQUESTER_H  */
