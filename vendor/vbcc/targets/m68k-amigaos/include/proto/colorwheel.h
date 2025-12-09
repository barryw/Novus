#ifndef PROTO_COLORWHEEL_H
#define PROTO_COLORWHEEL_H

/*
**	$VER: colorwheel.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/colorwheel_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * ColorWheelBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_COLORWHEEL_PRAGMAS_H
#include <pragmas/colorwheel_pragmas.h>
#endif /* PRAGMAS_COLORWHEEL_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_COLORWHEEL_LIB_H
#include <pragma/colorwheel_lib.h>
#endif /* PRAGMA_COLORWHEEL_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/colorwheel_protos.h>

#ifndef _VBCCINLINE_COLORWHEEL_H
#include <inline/colorwheel_protos.h>
#endif /* _VBCCINLINE_COLORWHEEL_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_COLORWHEEL_H
#include <inline/colorwheel.h>
#endif /* _INLINE_COLORWHEEL_H */
#else
#include <clib/colorwheel_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/colorwheel_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_COLORWHEEL_H */
