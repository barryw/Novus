#ifndef PROTO_CARDRES_H
#define PROTO_CARDRES_H

/*
**	$VER: cardres.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/cardres_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * CardResource;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_CARDRES_PRAGMAS_H
#include <pragmas/cardres_pragmas.h>
#endif /* PRAGMAS_CARDRES_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_CARDRES_LIB_H
#include <pragma/cardres_lib.h>
#endif /* PRAGMA_CARDRES_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/cardres_protos.h>

#ifndef _VBCCINLINE_CARDRES_H
#include <inline/cardres_protos.h>
#endif /* _VBCCINLINE_CARDRES_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_CARDRES_H
#include <inline/cardres.h>
#endif /* _INLINE_CARDRES_H */
#else
#include <clib/cardres_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/cardres_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_CARDRES_H */
