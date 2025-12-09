#ifndef PROTO_BATTCLOCK_H
#define PROTO_BATTCLOCK_H

/*
**	$VER: battclock.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/battclock_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * BattClockBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_BATTCLOCK_PRAGMAS_H
#include <pragmas/battclock_pragmas.h>
#endif /* PRAGMAS_BATTCLOCK_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_BATTCLOCK_LIB_H
#include <pragma/battclock_lib.h>
#endif /* PRAGMA_BATTCLOCK_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/battclock_protos.h>

#ifndef _VBCCINLINE_BATTCLOCK_H
#include <inline/battclock_protos.h>
#endif /* _VBCCINLINE_BATTCLOCK_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_BATTCLOCK_H
#include <inline/battclock.h>
#endif /* _INLINE_BATTCLOCK_H */
#else
#include <clib/battclock_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/battclock_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_BATTCLOCK_H */
