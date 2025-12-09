#ifndef PROTO_GETFONT_H
#define PROTO_GETFONT_H

/*
**	$VER: getfont.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/getfont_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * GetFontBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_GETFONT_PRAGMAS_H
#include <pragmas/getfont_pragmas.h>
#endif /* PRAGMAS_GETFONT_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_GETFONT_LIB_H
#include <pragma/getfont_lib.h>
#endif /* PRAGMA_GETFONT_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/getfont_protos.h>

#ifndef _VBCCINLINE_GETFONT_H
#include <inline/getfont_protos.h>
#endif /* _VBCCINLINE_GETFONT_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_GETFONT_H
#include <inline/getfont.h>
#endif /* _INLINE_GETFONT_H */
#else
#include <clib/getfont_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/getfont_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_GETFONT_H */
