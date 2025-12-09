#ifndef _INLINE_GETFILE_H
#define _INLINE_GETFILE_H

#ifndef CLIB_GETFILE_PROTOS_H
#define CLIB_GETFILE_PROTOS_H
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
#ifndef  GADGETS_GETFILE_H
#include <gadgets/getfile.h>
#endif

#ifndef GETFILE_BASE_NAME
#define GETFILE_BASE_NAME GetFileBase
#endif

#define GETFILE_GetClass() ({ \
  register char * _GETFILE_GetClass__bn __asm("a6") = (char *) (GETFILE_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_GETFILE_GetClass__bn - 30))(_GETFILE_GetClass__bn); \
})

#endif /*  _INLINE_GETFILE_H  */
