#ifndef PROTO_KEYMAP_H
#define PROTO_KEYMAP_H

/*
**	$VER: keymap.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/keymap_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * KeymapBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_KEYMAP_PRAGMAS_H
#include <pragmas/keymap_pragmas.h>
#endif /* PRAGMAS_KEYMAP_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_KEYMAP_LIB_H
#include <pragma/keymap_lib.h>
#endif /* PRAGMA_KEYMAP_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/keymap_protos.h>

#ifndef _VBCCINLINE_KEYMAP_H
#include <inline/keymap_protos.h>
#endif /* _VBCCINLINE_KEYMAP_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_KEYMAP_H
#include <inline/keymap.h>
#endif /* _INLINE_KEYMAP_H */
#else
#include <clib/keymap_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/keymap_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_KEYMAP_H */
