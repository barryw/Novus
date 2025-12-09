#ifndef PROTO_BITMAP_H
#define PROTO_BITMAP_H

/*
**	$VER: bitmap.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/bitmap_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * BitMapBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_BITMAP_PRAGMAS_H
#include <pragmas/bitmap_pragmas.h>
#endif /* PRAGMAS_BITMAP_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_BITMAP_LIB_H
#include <pragma/bitmap_lib.h>
#endif /* PRAGMA_BITMAP_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/bitmap_protos.h>

#ifndef _VBCCINLINE_BITMAP_H
#include <inline/bitmap_protos.h>
#endif /* _VBCCINLINE_BITMAP_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_BITMAP_H
#include <inline/bitmap.h>
#endif /* _INLINE_BITMAP_H */
#else
#include <clib/bitmap_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/bitmap_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_BITMAP_H */
