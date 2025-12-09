#ifndef PROTO_SPEEDBAR_H
#define PROTO_SPEEDBAR_H

/*
**	$VER: speedbar.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/speedbar_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * SpeedBarBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_SPEEDBAR_PRAGMAS_H
#include <pragmas/speedbar_pragmas.h>
#endif /* PRAGMAS_SPEEDBAR_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_SPEEDBAR_LIB_H
#include <pragma/speedbar_lib.h>
#endif /* PRAGMA_SPEEDBAR_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/speedbar_protos.h>

#ifndef _VBCCINLINE_SPEEDBAR_H
#include <inline/speedbar_protos.h>
#endif /* _VBCCINLINE_SPEEDBAR_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_SPEEDBAR_H
#include <inline/speedbar.h>
#endif /* _INLINE_SPEEDBAR_H */
#else
#include <clib/speedbar_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/speedbar_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_SPEEDBAR_H */
