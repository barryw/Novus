#ifndef _INLINE_SLIDER_H
#define _INLINE_SLIDER_H

#ifndef CLIB_SLIDER_PROTOS_H
#define CLIB_SLIDER_PROTOS_H
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
#ifndef  GADGETS_SLIDER_H
#include <gadgets/slider.h>
#endif

#ifndef SLIDER_BASE_NAME
#define SLIDER_BASE_NAME SliderBase
#endif

#define SLIDER_GetClass() ({ \
  register char * _SLIDER_GetClass__bn __asm("a6") = (char *) (SLIDER_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_SLIDER_GetClass__bn - 30))(_SLIDER_GetClass__bn); \
})

#endif /*  _INLINE_SLIDER_H  */
