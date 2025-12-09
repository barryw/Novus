#ifndef PROTO_MATHIEEEDOUBBAS_H
#define PROTO_MATHIEEEDOUBBAS_H

/*
**	$VER: mathieeedoubbas.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/mathieeedoubbas_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef LIBRARIES_MATHLIBRARY_H
#include <libraries/mathlibrary.h>
#endif /* LIBRARIES_MATHLIBRARY_H */

extern struct MathIEEEBase * MathIeeeDoubBasBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_MATHIEEEDOUBBAS_PRAGMAS_H
#include <pragmas/mathieeedoubbas_pragmas.h>
#endif /* PRAGMAS_MATHIEEEDOUBBAS_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_MATHIEEEDOUBBAS_LIB_H
#include <pragma/mathieeedoubbas_lib.h>
#endif /* PRAGMA_MATHIEEEDOUBBAS_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/mathieeedoubbas_protos.h>

#ifndef _VBCCINLINE_MATHIEEEDOUBBAS_H
#include <inline/mathieeedoubbas_protos.h>
#endif /* _VBCCINLINE_MATHIEEEDOUBBAS_H */

/****************************************************************************/

#elif defined(__GNUC__)

#include <clib/mathieeedoubbas_protos.h>

/* Any other compiler */
#else

#include <clib/mathieeedoubbas_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_MATHIEEEDOUBBAS_H */
