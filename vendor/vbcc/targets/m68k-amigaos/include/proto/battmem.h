#ifndef PROTO_BATTMEM_H
#define PROTO_BATTMEM_H

/*
**	$VER: battmem.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/battmem_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * BattMemBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_BATTMEM_PRAGMAS_H
#include <pragmas/battmem_pragmas.h>
#endif /* PRAGMAS_BATTMEM_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_BATTMEM_LIB_H
#include <pragma/battmem_lib.h>
#endif /* PRAGMA_BATTMEM_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/battmem_protos.h>

#ifndef _VBCCINLINE_BATTMEM_H
#include <inline/battmem_protos.h>
#endif /* _VBCCINLINE_BATTMEM_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_BATTMEM_H
#include <inline/battmem.h>
#endif /* _INLINE_BATTMEM_H */
#else
#include <clib/battmem_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/battmem_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_BATTMEM_H */
