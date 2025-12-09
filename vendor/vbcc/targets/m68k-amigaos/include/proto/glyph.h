#ifndef PROTO_GLYPH_H
#define PROTO_GLYPH_H

/*
**	$VER: glyph.h 47.5 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/glyph_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * GlyphBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_GLYPH_PRAGMAS_H
#include <pragmas/glyph_pragmas.h>
#endif /* PRAGMAS_GLYPH_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_GLYPH_LIB_H
#include <pragma/glyph_lib.h>
#endif /* PRAGMA_GLYPH_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/glyph_protos.h>

#ifndef _VBCCINLINE_GLYPH_H
#include <inline/glyph_protos.h>
#endif /* _VBCCINLINE_GLYPH_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_GLYPH_H
#include <inline/glyph.h>
#endif /* _INLINE_GLYPH_H */
#else
#include <clib/glyph_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/glyph_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_GLYPH_H */
