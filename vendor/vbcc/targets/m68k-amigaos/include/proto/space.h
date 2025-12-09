#ifndef PROTO_SPACE_H
#define PROTO_SPACE_H

/*
**	$VER: space.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/space_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * SpaceBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_SPACE_PRAGMAS_H
#include <pragmas/space_pragmas.h>
#endif /* PRAGMAS_SPACE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_SPACE_LIB_H
#include <pragma/space_lib.h>
#endif /* PRAGMA_SPACE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/space_protos.h>

#ifndef _VBCCINLINE_SPACE_H
#include <inline/space_protos.h>
#endif /* _VBCCINLINE_SPACE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_SPACE_H
#include <inline/space.h>
#endif /* _INLINE_SPACE_H */
#else
#include <clib/space_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/space_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_SPACE_H */
