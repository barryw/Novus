#ifndef CLIB_TIMER_PROTOS_H
#define CLIB_TIMER_PROTOS_H

/*
**	$VER: timer_protos.h 47.1 (30.11.2021)
**
**	'C' prototypes. For use with 32 bit integers only.
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

#ifndef  EXEC_DEVICES_H
#include <exec/devices.h>
#endif
#ifndef  DEVICES_TIMER_H
#include <devices/timer.h>
#endif
VOID AddTime( TimeVal_Type *dest, CONST TimeVal_Type *src );
VOID SubTime( TimeVal_Type *dest, CONST TimeVal_Type *src );
LONG CmpTime( CONST TimeVal_Type *dest, CONST TimeVal_Type *src );
ULONG ReadEClock( struct EClockVal *dest );
VOID GetSysTime( TimeVal_Type *dest );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_TIMER_PROTOS_H */
