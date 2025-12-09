#ifndef PROTO_MATHIEEEDOUBTRANS_H
#define PROTO_MATHIEEEDOUBTRANS_H

/*
**	$VER: mathieeedoubtrans.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/mathieeedoubtrans_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef LIBRARIES_MATHLIBRARY_H
#include <libraries/mathlibrary.h>
#endif /* LIBRARIES_MATHLIBRARY_H */

extern struct MathIEEEBase * MathIeeeDoubTransBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_MATHIEEEDOUBTRANS_PRAGMAS_H
#include <pragmas/mathieeedoubtrans_pragmas.h>
#endif /* PRAGMAS_MATHIEEEDOUBTRANS_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_MATHIEEEDOUBTRANS_LIB_H
#include <pragma/mathieeedoubtrans_lib.h>
#endif /* PRAGMA_MATHIEEEDOUBTRANS_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/mathieeedoubtrans_protos.h>

#ifndef _VBCCINLINE_MATHIEEEDOUBTRANS_H
#include <inline/mathieeedoubtrans_protos.h>
#endif /* _VBCCINLINE_MATHIEEEDOUBTRANS_H */

/****************************************************************************/

#elif defined(__GNUC__)

#include <clib/mathieeedoubtrans_protos.h>

/* Any other compiler */
#else

#include <clib/mathieeedoubtrans_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_MATHIEEEDOUBTRANS_H */
