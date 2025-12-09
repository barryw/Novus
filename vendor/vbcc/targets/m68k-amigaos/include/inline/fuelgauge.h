#ifndef _INLINE_FUELGAUGE_H
#define _INLINE_FUELGAUGE_H

#ifndef CLIB_FUELGAUGE_PROTOS_H
#define CLIB_FUELGAUGE_PROTOS_H
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
#ifndef  GADGETS_FUELGAUGE_H
#include <gadgets/fuelgauge.h>
#endif

#ifndef FUELGAUGE_BASE_NAME
#define FUELGAUGE_BASE_NAME FuelGaugeBase
#endif

#define FUELGAUGE_GetClass() ({ \
  register char * _FUELGAUGE_GetClass__bn __asm("a6") = (char *) (FUELGAUGE_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_FUELGAUGE_GetClass__bn - 30))(_FUELGAUGE_GetClass__bn); \
})

#endif /*  _INLINE_FUELGAUGE_H  */
