#ifndef PROTO_STRING_H
#define PROTO_STRING_H

/*
**	$VER: string.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/string_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * StringBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_STRING_PRAGMAS_H
#include <pragmas/string_pragmas.h>
#endif /* PRAGMAS_STRING_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_STRING_LIB_H
#include <pragma/string_lib.h>
#endif /* PRAGMA_STRING_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/string_protos.h>

#ifndef _VBCCINLINE_STRING_H
#include <inline/string_protos.h>
#endif /* _VBCCINLINE_STRING_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_STRING_H
#include <inline/string.h>
#endif /* _INLINE_STRING_H */
#else
#include <clib/string_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/string_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_STRING_H */
