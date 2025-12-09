#ifndef PROTO_DRAWLIST_H
#define PROTO_DRAWLIST_H

/*
**	$VER: drawlist.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/drawlist_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * DrawListBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_DRAWLIST_PRAGMAS_H
#include <pragmas/drawlist_pragmas.h>
#endif /* PRAGMAS_DRAWLIST_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_DRAWLIST_LIB_H
#include <pragma/drawlist_lib.h>
#endif /* PRAGMA_DRAWLIST_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/drawlist_protos.h>

#ifndef _VBCCINLINE_DRAWLIST_H
#include <inline/drawlist_protos.h>
#endif /* _VBCCINLINE_DRAWLIST_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_DRAWLIST_H
#include <inline/drawlist.h>
#endif /* _INLINE_DRAWLIST_H */
#else
#include <clib/drawlist_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/drawlist_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_DRAWLIST_H */
