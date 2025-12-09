#ifndef PROTO_SCROLLER_H
#define PROTO_SCROLLER_H

/*
**	$VER: scroller.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/scroller_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * ScrollerBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_SCROLLER_PRAGMAS_H
#include <pragmas/scroller_pragmas.h>
#endif /* PRAGMAS_SCROLLER_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_SCROLLER_LIB_H
#include <pragma/scroller_lib.h>
#endif /* PRAGMA_SCROLLER_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/scroller_protos.h>

#ifndef _VBCCINLINE_SCROLLER_H
#include <inline/scroller_protos.h>
#endif /* _VBCCINLINE_SCROLLER_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_SCROLLER_H
#include <inline/scroller.h>
#endif /* _INLINE_SCROLLER_H */
#else
#include <clib/scroller_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/scroller_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_SCROLLER_H */
