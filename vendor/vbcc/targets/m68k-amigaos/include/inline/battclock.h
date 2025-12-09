#ifndef _INLINE_BATTCLOCK_H
#define _INLINE_BATTCLOCK_H

#ifndef CLIB_BATTCLOCK_PROTOS_H
#define CLIB_BATTCLOCK_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif

#ifndef BATTCLOCK_BASE_NAME
#define BATTCLOCK_BASE_NAME BattClockBase
#endif

#define ResetBattClock() ({ \
  register char * _ResetBattClock__bn __asm("a6") = (char *) (BATTCLOCK_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_ResetBattClock__bn - 6))(_ResetBattClock__bn); \
})

#define ReadBattClock() ({ \
  register char * _ReadBattClock__bn __asm("a6") = (char *) (BATTCLOCK_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_ReadBattClock__bn - 12))(_ReadBattClock__bn); \
})

#define WriteBattClock(time) ({ \
  ULONG _WriteBattClock_time = (time); \
  ({ \
  register char * _WriteBattClock__bn __asm("a6") = (char *) (BATTCLOCK_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_WriteBattClock__bn - 18))(_WriteBattClock__bn, _WriteBattClock_time); \
});})

#endif /*  _INLINE_BATTCLOCK_H  */
