#ifndef PROTO_DOS_H
#define PROTO_DOS_H

/*
**	$VER: dos.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/dos_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef DOS_DOSEXTENS_H
#include <dos/dosextens.h>
#endif /* DOS_DOSEXTENS_H */

extern struct DosLibrary * DOSBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_DOS_PRAGMAS_H
#include <pragmas/dos_pragmas.h>
#endif /* PRAGMAS_DOS_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_DOS_LIB_H
#include <pragma/dos_lib.h>
#endif /* PRAGMA_DOS_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/dos_protos.h>

#ifndef _VBCCINLINE_DOS_H
#include <inline/dos_protos.h>
#endif /* _VBCCINLINE_DOS_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_DOS_H
#include <inline/dos.h>
#endif /* _INLINE_DOS_H */
#else
#include <clib/dos_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/dos_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_DOS_H */
