#ifndef PROTO_COMMODITIES_H
#define PROTO_COMMODITIES_H

/*
**	$VER: commodities.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/commodities_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * CxBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_COMMODITIES_PRAGMAS_H
#include <pragmas/commodities_pragmas.h>
#endif /* PRAGMAS_COMMODITIES_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_COMMODITIES_LIB_H
#include <pragma/commodities_lib.h>
#endif /* PRAGMA_COMMODITIES_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/commodities_protos.h>

#ifndef _VBCCINLINE_COMMODITIES_H
#include <inline/commodities_protos.h>
#endif /* _VBCCINLINE_COMMODITIES_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_COMMODITIES_H
#include <inline/commodities.h>
#endif /* _INLINE_COMMODITIES_H */
#else
#include <clib/commodities_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/commodities_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_COMMODITIES_H */
