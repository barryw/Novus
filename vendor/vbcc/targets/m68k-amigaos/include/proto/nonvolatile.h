#ifndef PROTO_NONVOLATILE_H
#define PROTO_NONVOLATILE_H

/*
**	$VER: nonvolatile.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/nonvolatile_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * NVBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_NONVOLATILE_PRAGMAS_H
#include <pragmas/nonvolatile_pragmas.h>
#endif /* PRAGMAS_NONVOLATILE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_NONVOLATILE_LIB_H
#include <pragma/nonvolatile_lib.h>
#endif /* PRAGMA_NONVOLATILE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/nonvolatile_protos.h>

#ifndef _VBCCINLINE_NONVOLATILE_H
#include <inline/nonvolatile_protos.h>
#endif /* _VBCCINLINE_NONVOLATILE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_NONVOLATILE_H
#include <inline/nonvolatile.h>
#endif /* _INLINE_NONVOLATILE_H */
#else
#include <clib/nonvolatile_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/nonvolatile_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_NONVOLATILE_H */
