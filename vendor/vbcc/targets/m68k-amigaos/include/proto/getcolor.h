#ifndef PROTO_GETCOLOR_H
#define PROTO_GETCOLOR_H

/*
**	$VER: getcolor.h 47.5 (6.12.2020)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2020 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/getcolor_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * GetColorBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

/****************************************************************************/

#ifndef PRAGMAS_GETCOLOR_PRAGMAS_H
#include <pragmas/getcolor_pragmas.h>
#endif /* PRAGMAS_GETCOLOR_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_GETCOLOR_LIB_H
#include <pragma/getcolor_lib.h>
#endif /* PRAGMA_GETCOLOR_LIB_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_GETCOLOR_H
#include <inline/getcolor.h>
#endif /* _INLINE_GETCOLOR_H */
#else
#include <clib/getcolor_protos.h>
#endif /* mc68000 */

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_GETCOLOR_H */
