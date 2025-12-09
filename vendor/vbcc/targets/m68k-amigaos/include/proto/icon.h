#ifndef PROTO_ICON_H
#define PROTO_ICON_H

/*
**	$VER: icon.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/icon_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * IconBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_ICON_PRAGMAS_H
#include <pragmas/icon_pragmas.h>
#endif /* PRAGMAS_ICON_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_ICON_LIB_H
#include <pragma/icon_lib.h>
#endif /* PRAGMA_ICON_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/icon_protos.h>

#ifndef _VBCCINLINE_ICON_H
#include <inline/icon_protos.h>
#endif /* _VBCCINLINE_ICON_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_ICON_H
#include <inline/icon.h>
#endif /* _INLINE_ICON_H */
#else
#include <clib/icon_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/icon_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_ICON_H */
