#ifndef PROTO_DATATYPES_H
#define PROTO_DATATYPES_H

/*
**	$VER: datatypes.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/datatypes_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * DataTypesBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_DATATYPES_PRAGMAS_H
#include <pragmas/datatypes_pragmas.h>
#endif /* PRAGMAS_DATATYPES_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_DATATYPES_LIB_H
#include <pragma/datatypes_lib.h>
#endif /* PRAGMA_DATATYPES_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/datatypes_protos.h>

#ifndef _VBCCINLINE_DATATYPES_H
#include <inline/datatypes_protos.h>
#endif /* _VBCCINLINE_DATATYPES_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_DATATYPES_H
#include <inline/datatypes.h>
#endif /* _INLINE_DATATYPES_H */
#else
#include <clib/datatypes_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/datatypes_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_DATATYPES_H */
