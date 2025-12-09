#ifndef PROTO_CIA_H
#define PROTO_CIA_H

/*
**	$VER: cia.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/cia_protos.h>

#else

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_CIA_PRAGMAS_H
#include <pragmas/cia_pragmas.h>
#endif /* PRAGMAS_CIA_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_CIA_LIB_H
#include <pragma/cia_lib.h>
#endif /* PRAGMA_CIA_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/cia_protos.h>

#ifndef _VBCCINLINE_CIA_H
#include <inline/cia_protos.h>
#endif /* _VBCCINLINE_CIA_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_CIA_H
#include <inline/cia.h>
#endif /* _INLINE_CIA_H */
#else
#include <clib/cia_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/cia_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_CIA_H */
