#ifndef PROTO_REALTIME_H
#define PROTO_REALTIME_H

/*
**	$VER: realtime.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/realtime_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * RealTimeBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_REALTIME_PRAGMAS_H
#include <pragmas/realtime_pragmas.h>
#endif /* PRAGMAS_REALTIME_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_REALTIME_LIB_H
#include <pragma/realtime_lib.h>
#endif /* PRAGMA_REALTIME_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/realtime_protos.h>

#ifndef _VBCCINLINE_REALTIME_H
#include <inline/realtime_protos.h>
#endif /* _VBCCINLINE_REALTIME_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_REALTIME_H
#include <inline/realtime.h>
#endif /* _INLINE_REALTIME_H */
#else
#include <clib/realtime_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/realtime_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_REALTIME_H */
