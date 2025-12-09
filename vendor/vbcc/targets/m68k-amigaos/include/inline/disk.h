#ifndef _INLINE_DISK_H
#define _INLINE_DISK_H

#ifndef CLIB_DISK_PROTOS_H
#define CLIB_DISK_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  RESOURCES_DISK_H
#include <resources/disk.h>
#endif

#ifndef DISK_BASE_NAME
#define DISK_BASE_NAME DiskBase
#endif

#define AllocUnit(unitNum) ({ \
  LONG _AllocUnit_unitNum = (unitNum); \
  ({ \
  register char * _AllocUnit__bn __asm("a6") = (char *) (DISK_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_AllocUnit__bn - 6))(_AllocUnit__bn, _AllocUnit_unitNum); \
});})

#define FreeUnit(unitNum) ({ \
  LONG _FreeUnit_unitNum = (unitNum); \
  ({ \
  register char * _FreeUnit__bn __asm("a6") = (char *) (DISK_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_FreeUnit__bn - 12))(_FreeUnit__bn, _FreeUnit_unitNum); \
});})

#define GetUnit(unitPointer) ({ \
  struct DiskResourceUnit * _GetUnit_unitPointer = (unitPointer); \
  ({ \
  register char * _GetUnit__bn __asm("a6") = (char *) (DISK_BASE_NAME);\
  ((struct DiskResourceUnit * (*)(char * __asm("a6"), struct DiskResourceUnit * __asm("a1"))) \
  (_GetUnit__bn - 18))(_GetUnit__bn, _GetUnit_unitPointer); \
});})

#define GiveUnit() ({ \
  register char * _GiveUnit__bn __asm("a6") = (char *) (DISK_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_GiveUnit__bn - 24))(_GiveUnit__bn); \
})

#define GetUnitID(unitNum) ({ \
  LONG _GetUnitID_unitNum = (unitNum); \
  ({ \
  register char * _GetUnitID__bn __asm("a6") = (char *) (DISK_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_GetUnitID__bn - 30))(_GetUnitID__bn, _GetUnitID_unitNum); \
});})

#define ReadUnitID(unitNum) ({ \
  LONG _ReadUnitID_unitNum = (unitNum); \
  ({ \
  register char * _ReadUnitID__bn __asm("a6") = (char *) (DISK_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_ReadUnitID__bn - 36))(_ReadUnitID__bn, _ReadUnitID_unitNum); \
});})

#endif /*  _INLINE_DISK_H  */
