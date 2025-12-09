#ifndef PROTO_GADTOOLS_H
#define PROTO_GADTOOLS_H

/*
**	$VER: gadtools.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/gadtools_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * GadToolsBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_GADTOOLS_PRAGMAS_H
#include <pragmas/gadtools_pragmas.h>
#endif /* PRAGMAS_GADTOOLS_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_GADTOOLS_LIB_H
#include <pragma/gadtools_lib.h>
#endif /* PRAGMA_GADTOOLS_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/gadtools_protos.h>

#ifndef _VBCCINLINE_GADTOOLS_H
#include <inline/gadtools_protos.h>
#endif /* _VBCCINLINE_GADTOOLS_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_GADTOOLS_H
#include <inline/gadtools.h>
#endif /* _INLINE_GADTOOLS_H */
#else
#include <clib/gadtools_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/gadtools_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_GADTOOLS_H */
