#ifndef _INLINE_TIMER_H
#define _INLINE_TIMER_H

#ifndef CLIB_TIMER_PROTOS_H
#define CLIB_TIMER_PROTOS_H
#endif

#ifndef  EXEC_DEVICES_H
#include <exec/devices.h>
#endif
#ifndef  DEVICES_TIMER_H
#include <devices/timer.h>
#endif

#ifndef TIMER_BASE_NAME
#define TIMER_BASE_NAME TimerBase
#endif

#define AddTime(dest, src) ({ \
  TimeVal_Type * _AddTime_dest = (dest); \
  CONST TimeVal_Type * _AddTime_src = (src); \
  ({ \
  register char * _AddTime__bn __asm("a6") = (char *) (TIMER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), TimeVal_Type * __asm("a0"), CONST TimeVal_Type * __asm("a1"))) \
  (_AddTime__bn - 42))(_AddTime__bn, _AddTime_dest, _AddTime_src); \
});})

#define SubTime(dest, src) ({ \
  TimeVal_Type * _SubTime_dest = (dest); \
  CONST TimeVal_Type * _SubTime_src = (src); \
  ({ \
  register char * _SubTime__bn __asm("a6") = (char *) (TIMER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), TimeVal_Type * __asm("a0"), CONST TimeVal_Type * __asm("a1"))) \
  (_SubTime__bn - 48))(_SubTime__bn, _SubTime_dest, _SubTime_src); \
});})

#define CmpTime(dest, src) ({ \
  CONST TimeVal_Type * _CmpTime_dest = (dest); \
  CONST TimeVal_Type * _CmpTime_src = (src); \
  ({ \
  register char * _CmpTime__bn __asm("a6") = (char *) (TIMER_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST TimeVal_Type * __asm("a0"), CONST TimeVal_Type * __asm("a1"))) \
  (_CmpTime__bn - 54))(_CmpTime__bn, _CmpTime_dest, _CmpTime_src); \
});})

#define ReadEClock(dest) ({ \
  struct EClockVal * _ReadEClock_dest = (dest); \
  ({ \
  register char * _ReadEClock__bn __asm("a6") = (char *) (TIMER_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct EClockVal * __asm("a0"))) \
  (_ReadEClock__bn - 60))(_ReadEClock__bn, _ReadEClock_dest); \
});})

#define GetSysTime(dest) ({ \
  TimeVal_Type * _GetSysTime_dest = (dest); \
  ({ \
  register char * _GetSysTime__bn __asm("a6") = (char *) (TIMER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), TimeVal_Type * __asm("a0"))) \
  (_GetSysTime__bn - 66))(_GetSysTime__bn, _GetSysTime_dest); \
});})

#endif /*  _INLINE_TIMER_H  */
