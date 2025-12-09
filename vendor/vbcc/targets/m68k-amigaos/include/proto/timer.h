#ifndef PROTO_TIMER_H
#define PROTO_TIMER_H

/*
**	$VER: timer.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/timer_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_DEVICES_H
#include <exec/devices.h>
#endif /* EXEC_DEVICES_H */

extern struct Device * TimerBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_TIMER_PRAGMAS_H
#include <pragmas/timer_pragmas.h>
#endif /* PRAGMAS_TIMER_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_TIMER_LIB_H
#include <pragma/timer_lib.h>
#endif /* PRAGMA_TIMER_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/timer_protos.h>

#ifndef _VBCCINLINE_TIMER_H
#include <inline/timer_protos.h>
#endif /* _VBCCINLINE_TIMER_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_TIMER_H
#include <inline/timer.h>
#endif /* _INLINE_TIMER_H */
#else
#include <clib/timer_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/timer_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_TIMER_H */
