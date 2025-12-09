#ifndef PROTO_SKETCHBOARD_H
#define PROTO_SKETCHBOARD_H

/*
**	$VER: sketchboard.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/sketchboard_protos.h>

/***************************************************************************/

#else

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * SketchBoardBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_SKETCHBOARD_PRAGMAS_H
#include <pragmas/sketchboard_pragmas.h>
#endif /* PRAGMAS_SKETCHBOARD_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_SKETCHBOARD_LIB_H
#include <pragma/sketchboard_lib.h>
#endif /* PRAGMA_SKETCHBOARD_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/sketchboard_protos.h>

#ifndef _VBCCINLINE_SKETCHBOARD_H
#include <inline/sketchboard_protos.h>
#endif /* _VBCCINLINE_SKETCHBOARD_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_SKETCHBOARD_H
#include <inline/sketchboard.h>
#endif /* _INLINE_SKETCHBOARD_H */
#else
#include <clib/sketchboard_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/sketchboard_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_SKETCHBOARD_H */
