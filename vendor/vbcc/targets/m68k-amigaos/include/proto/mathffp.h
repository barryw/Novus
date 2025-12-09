#ifndef PROTO_MATHFFP_H
#define PROTO_MATHFFP_H

/*
**	$VER: mathffp.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/mathffp_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * MathBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_MATHFFP_PRAGMAS_H
#include <pragmas/mathffp_pragmas.h>
#endif /* PRAGMAS_MATHFFP_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_MATHFFP_LIB_H
#include <pragma/mathffp_lib.h>
#endif /* PRAGMA_MATHFFP_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/mathffp_protos.h>

#ifndef _VBCCINLINE_MATHFFP_H
#include <inline/mathffp_protos.h>
#endif /* _VBCCINLINE_MATHFFP_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_MATHFFP_H
#include <inline/mathffp.h>
#endif /* _INLINE_MATHFFP_H */
#else
#include <clib/mathffp_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/mathffp_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_MATHFFP_H */
