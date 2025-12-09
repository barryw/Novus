#ifndef PROTO_EXPANSION_H
#define PROTO_EXPANSION_H

/*
**	$VER: expansion.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/expansion_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef LIBRARIES_EXPANSIONBASE_H
#include <libraries/expansionbase.h>
#endif /* LIBRARIES_EXPANSIONBASE_H */

extern struct ExpansionBase * ExpansionBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_EXPANSION_PRAGMAS_H
#include <pragmas/expansion_pragmas.h>
#endif /* PRAGMAS_EXPANSION_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_EXPANSION_LIB_H
#include <pragma/expansion_lib.h>
#endif /* PRAGMA_EXPANSION_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/expansion_protos.h>

#ifndef _VBCCINLINE_EXPANSION_H
#include <inline/expansion_protos.h>
#endif /* _VBCCINLINE_EXPANSION_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_EXPANSION_H
#include <inline/expansion.h>
#endif /* _INLINE_EXPANSION_H */
#else
#include <clib/expansion_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/expansion_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_EXPANSION_H */
