#ifndef _INLINE_LABEL_H
#define _INLINE_LABEL_H

#ifndef CLIB_LABEL_PROTOS_H
#define CLIB_LABEL_PROTOS_H
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
#ifndef  IMAGES_LABEL_H
#include <images/label.h>
#endif

#ifndef LABEL_BASE_NAME
#define LABEL_BASE_NAME LabelBase
#endif

#define LABEL_GetClass() ({ \
  register char * _LABEL_GetClass__bn __asm("a6") = (char *) (LABEL_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_LABEL_GetClass__bn - 30))(_LABEL_GetClass__bn); \
})

#endif /*  _INLINE_LABEL_H  */
