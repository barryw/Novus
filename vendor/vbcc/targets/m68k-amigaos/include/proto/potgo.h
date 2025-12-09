#ifndef PROTO_POTGO_H
#define PROTO_POTGO_H

/*
**	$VER: potgo.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/potgo_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * PotgoBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_POTGO_PRAGMAS_H
#include <pragmas/potgo_pragmas.h>
#endif /* PRAGMAS_POTGO_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_POTGO_LIB_H
#include <pragma/potgo_lib.h>
#endif /* PRAGMA_POTGO_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/potgo_protos.h>

#ifndef _VBCCINLINE_POTGO_H
#include <inline/potgo_protos.h>
#endif /* _VBCCINLINE_POTGO_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_POTGO_H
#include <inline/potgo.h>
#endif /* _INLINE_POTGO_H */
#else
#include <clib/potgo_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/potgo_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_POTGO_H */
