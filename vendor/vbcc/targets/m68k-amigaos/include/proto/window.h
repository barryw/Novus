#ifndef PROTO_WINDOW_H
#define PROTO_WINDOW_H

/*
**	$VER: window.h 47.5 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/window_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * WindowBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_WINDOW_PRAGMAS_H
#include <pragmas/window_pragmas.h>
#endif /* PRAGMAS_WINDOW_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_WINDOW_LIB_H
#include <pragma/window_lib.h>
#endif /* PRAGMA_WINDOW_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/window_protos.h>

#ifndef _VBCCINLINE_WINDOW_H
#include <inline/window_protos.h>
#endif /* _VBCCINLINE_WINDOW_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_WINDOW_H
#include <inline/window.h>
#endif /* _INLINE_WINDOW_H */
#else
#include <clib/window_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/window_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_WINDOW_H */
