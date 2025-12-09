#ifndef PROTO_MATHIEEESINGBAS_H
#define PROTO_MATHIEEESINGBAS_H

/*
**	$VER: mathieeesingbas.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/mathieeesingbas_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * MathIeeeSingBasBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_MATHIEEESINGBAS_PRAGMAS_H
#include <pragmas/mathieeesingbas_pragmas.h>
#endif /* PRAGMAS_MATHIEEESINGBAS_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_MATHIEEESINGBAS_LIB_H
#include <pragma/mathieeesingbas_lib.h>
#endif /* PRAGMA_MATHIEEESINGBAS_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/mathieeesingbas_protos.h>

#ifndef _VBCCINLINE_MATHIEEESINGBAS_H
#include <inline/mathieeesingbas_protos.h>
#endif /* _VBCCINLINE_MATHIEEESINGBAS_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_MATHIEEESINGBAS_H
#include <inline/mathieeesingbas.h>
#endif /* _INLINE_MATHIEEESINGBAS_H */
#else
#include <clib/mathieeesingbas_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/mathieeesingbas_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_MATHIEEESINGBAS_H */
