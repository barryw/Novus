#ifndef _INLINE_RAMDRIVE_H
#define _INLINE_RAMDRIVE_H

#ifndef CLIB_RAMDRIVE_PROTOS_H
#define CLIB_RAMDRIVE_PROTOS_H
#endif

#ifndef  EXEC_DEVICES_H
#include <exec/devices.h>
#endif

#ifndef RAMDRIVE_BASE_NAME
#define RAMDRIVE_BASE_NAME RamdriveDevice
#endif

#define KillRAD0() ({ \
  register char * _KillRAD0__bn __asm("a6") = (char *) (RAMDRIVE_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"))) \
  (_KillRAD0__bn - 42))(_KillRAD0__bn); \
})

#define KillRAD(unit) ({ \
  ULONG _KillRAD_unit = (unit); \
  ({ \
  register char * _KillRAD__bn __asm("a6") = (char *) (RAMDRIVE_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_KillRAD__bn - 48))(_KillRAD__bn, _KillRAD_unit); \
});})

#endif /*  _INLINE_RAMDRIVE_H  */
