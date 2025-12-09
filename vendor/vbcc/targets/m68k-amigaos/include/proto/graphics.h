#ifndef PROTO_GRAPHICS_H
#define PROTO_GRAPHICS_H

/*
**	$VER: graphics.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/graphics_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef GRAPHICS_GFXBASE_H
#include <graphics/gfxbase.h>
#endif /* GRAPHICS_GFXBASE_H */

extern struct GfxBase * GfxBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_GRAPHICS_PRAGMAS_H
#include <pragmas/graphics_pragmas.h>
#endif /* PRAGMAS_GRAPHICS_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_GRAPHICS_LIB_H
#include <pragma/graphics_lib.h>
#endif /* PRAGMA_GRAPHICS_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/graphics_protos.h>

#ifndef _VBCCINLINE_GRAPHICS_H
#include <inline/graphics_protos.h>
#endif /* _VBCCINLINE_GRAPHICS_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_GRAPHICS_H
#include <inline/graphics.h>
#endif /* _INLINE_GRAPHICS_H */
#else
#include <clib/graphics_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/graphics_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_GRAPHICS_H */
