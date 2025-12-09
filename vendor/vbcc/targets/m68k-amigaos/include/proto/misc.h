#ifndef PROTO_MISC_H
#define PROTO_MISC_H

/*
**	$VER: misc.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/misc_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * MiscBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_MISC_PRAGMAS_H
#include <pragmas/misc_pragmas.h>
#endif /* PRAGMAS_MISC_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_MISC_LIB_H
#include <pragma/misc_lib.h>
#endif /* PRAGMA_MISC_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/misc_protos.h>

#ifndef _VBCCINLINE_MISC_H
#include <inline/misc_protos.h>
#endif /* _VBCCINLINE_MISC_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_MISC_H
#include <inline/misc.h>
#endif /* _INLINE_MISC_H */
#else
#include <clib/misc_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/misc_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_MISC_H */
