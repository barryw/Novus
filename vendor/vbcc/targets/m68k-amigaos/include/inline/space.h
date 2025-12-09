#ifndef _INLINE_SPACE_H
#define _INLINE_SPACE_H

#ifndef CLIB_SPACE_PROTOS_H
#define CLIB_SPACE_PROTOS_H
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
#ifndef  GADGETS_SPACE_H
#include <gadgets/space.h>
#endif

#ifndef SPACE_BASE_NAME
#define SPACE_BASE_NAME SpaceBase
#endif

#define SPACE_GetClass() ({ \
  register char * _SPACE_GetClass__bn __asm("a6") = (char *) (SPACE_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_SPACE_GetClass__bn - 30))(_SPACE_GetClass__bn); \
})

#endif /*  _INLINE_SPACE_H  */
