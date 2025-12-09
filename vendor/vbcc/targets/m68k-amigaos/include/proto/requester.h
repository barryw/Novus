#ifndef PROTO_REQUESTER_H
#define PROTO_REQUESTER_H

/*
**	$VER: requester.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/requester_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * RequesterBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_REQUESTER_PRAGMAS_H
#include <pragmas/requester_pragmas.h>
#endif /* PRAGMAS_REQUESTER_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_REQUESTER_LIB_H
#include <pragma/requester_lib.h>
#endif /* PRAGMA_REQUESTER_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/requester_protos.h>

#ifndef _VBCCINLINE_REQUESTER_H
#include <inline/requester_protos.h>
#endif /* _VBCCINLINE_REQUESTER_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_REQUESTER_H
#include <inline/requester.h>
#endif /* _INLINE_REQUESTER_H */
#else
#include <clib/requester_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/requester_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_REQUESTER_H */
