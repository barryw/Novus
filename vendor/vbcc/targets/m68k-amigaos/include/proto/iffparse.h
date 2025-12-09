#ifndef PROTO_IFFPARSE_H
#define PROTO_IFFPARSE_H

/*
**	$VER: iffparse.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/iffparse_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * IFFParseBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_IFFPARSE_PRAGMAS_H
#include <pragmas/iffparse_pragmas.h>
#endif /* PRAGMAS_IFFPARSE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_IFFPARSE_LIB_H
#include <pragma/iffparse_lib.h>
#endif /* PRAGMA_IFFPARSE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/iffparse_protos.h>

#ifndef _VBCCINLINE_IFFPARSE_H
#include <inline/iffparse_protos.h>
#endif /* _VBCCINLINE_IFFPARSE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_IFFPARSE_H
#include <inline/iffparse.h>
#endif /* _INLINE_IFFPARSE_H */
#else
#include <clib/iffparse_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/iffparse_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_IFFPARSE_H */
