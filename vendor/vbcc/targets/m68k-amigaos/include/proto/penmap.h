#ifndef PROTO_PENMAP_H
#define PROTO_PENMAP_H

/*
**	$VER: penmap.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/penmap_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * PenMapBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_PENMAP_PRAGMAS_H
#include <pragmas/penmap_pragmas.h>
#endif /* PRAGMAS_PENMAP_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_PENMAP_LIB_H
#include <pragma/penmap_lib.h>
#endif /* PRAGMA_PENMAP_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/penmap_protos.h>

#ifndef _VBCCINLINE_PENMAP_H
#include <inline/penmap_protos.h>
#endif /* _VBCCINLINE_PENMAP_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_PENMAP_H
#include <inline/penmap.h>
#endif /* _INLINE_PENMAP_H */
#else
#include <clib/penmap_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/penmap_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_PENMAP_H */
