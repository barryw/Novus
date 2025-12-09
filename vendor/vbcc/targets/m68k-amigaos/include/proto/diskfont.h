#ifndef PROTO_DISKFONT_H
#define PROTO_DISKFONT_H

/*
**	$VER: diskfont.h 47.3 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/diskfont_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * DiskfontBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_DISKFONT_PRAGMAS_H
#include <pragmas/diskfont_pragmas.h>
#endif /* PRAGMAS_DISKFONT_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_DISKFONT_LIB_H
#include <pragma/diskfont_lib.h>
#endif /* PRAGMA_DISKFONT_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/diskfont_protos.h>

#ifndef _VBCCINLINE_DISKFONT_H
#include <inline/diskfont_protos.h>
#endif /* _VBCCINLINE_DISKFONT_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_DISKFONT_H
#include <inline/diskfont.h>
#endif /* _INLINE_DISKFONT_H */
#else
#include <clib/diskfont_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/diskfont_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_DISKFONT_H */
