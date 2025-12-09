#ifndef PROTO_UTILITY_H
#define PROTO_UTILITY_H

/*
**	$VER: utility.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/utility_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * UtilityBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_UTILITY_PRAGMAS_H
#include <pragmas/utility_pragmas.h>
#endif /* PRAGMAS_UTILITY_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_UTILITY_LIB_H
#include <pragma/utility_lib.h>
#endif /* PRAGMA_UTILITY_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/utility_protos.h>

#ifndef _VBCCINLINE_UTILITY_H
#include <inline/utility_protos.h>
#endif /* _VBCCINLINE_UTILITY_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_UTILITY_H
#include <inline/utility.h>
#endif /* _INLINE_UTILITY_H */
#else
#include <clib/utility_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/utility_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_UTILITY_H */
