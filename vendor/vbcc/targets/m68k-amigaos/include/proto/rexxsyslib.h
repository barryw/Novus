#ifndef PROTO_REXXSYSLIB_H
#define PROTO_REXXSYSLIB_H

/*
**	$VER: rexxsyslib.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/rexxsyslib_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef REXX_RXSLIB_H
#include <rexx/rxslib.h>
#endif /* REXX_RXSLIB_H */

extern struct RxsLib * RexxSysBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_REXXSYSLIB_PRAGMAS_H
#include <pragmas/rexxsyslib_pragmas.h>
#endif /* PRAGMAS_REXXSYSLIB_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_REXXSYSLIB_LIB_H
#include <pragma/rexxsyslib_lib.h>
#endif /* PRAGMA_REXXSYSLIB_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/rexxsyslib_protos.h>

#ifndef _VBCCINLINE_REXXSYSLIB_H
#include <inline/rexxsyslib_protos.h>
#endif /* _VBCCINLINE_REXXSYSLIB_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_REXXSYSLIB_H
#include <inline/rexxsyslib.h>
#endif /* _INLINE_REXXSYSLIB_H */
#else
#include <clib/rexxsyslib_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/rexxsyslib_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_REXXSYSLIB_H */
