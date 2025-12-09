#ifndef PROTO_AREXX_H
#define PROTO_AREXX_H

/*
**	$VER: arexx.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/arexx_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * ARexxBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_AREXX_PRAGMAS_H
#include <pragmas/arexx_pragmas.h>
#endif /* PRAGMAS_AREXX_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_AREXX_LIB_H
#include <pragma/arexx_lib.h>
#endif /* PRAGMA_AREXX_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/arexx_protos.h>

#ifndef _VBCCINLINE_AREXX_H
#include <inline/arexx_protos.h>
#endif /* _VBCCINLINE_AREXX_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_AREXX_H
#include <inline/arexx.h>
#endif /* _INLINE_AREXX_H */
#else
#include <clib/arexx_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/arexx_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_AREXX_H */
