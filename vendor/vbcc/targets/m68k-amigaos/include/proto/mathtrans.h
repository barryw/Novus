#ifndef PROTO_MATHTRANS_H
#define PROTO_MATHTRANS_H

/*
**	$VER: mathtrans.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/mathtrans_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * MathTransBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_MATHTRANS_PRAGMAS_H
#include <pragmas/mathtrans_pragmas.h>
#endif /* PRAGMAS_MATHTRANS_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_MATHTRANS_LIB_H
#include <pragma/mathtrans_lib.h>
#endif /* PRAGMA_MATHTRANS_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/mathtrans_protos.h>

#ifndef _VBCCINLINE_MATHTRANS_H
#include <inline/mathtrans_protos.h>
#endif /* _VBCCINLINE_MATHTRANS_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_MATHTRANS_H
#include <inline/mathtrans.h>
#endif /* _INLINE_MATHTRANS_H */
#else
#include <clib/mathtrans_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/mathtrans_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_MATHTRANS_H */
