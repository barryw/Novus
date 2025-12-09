#ifndef PROTO_CLICKTAB_H
#define PROTO_CLICKTAB_H

/*
**	$VER: clicktab.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/clicktab_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * ClickTabBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_CLICKTAB_PRAGMAS_H
#include <pragmas/clicktab_pragmas.h>
#endif /* PRAGMAS_CLICKTAB_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_CLICKTAB_LIB_H
#include <pragma/clicktab_lib.h>
#endif /* PRAGMA_CLICKTAB_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/clicktab_protos.h>

#ifndef _VBCCINLINE_CLICKTAB_H
#include <inline/clicktab_protos.h>
#endif /* _VBCCINLINE_CLICKTAB_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_CLICKTAB_H
#include <inline/clicktab.h>
#endif /* _INLINE_CLICKTAB_H */
#else
#include <clib/clicktab_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/clicktab_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_CLICKTAB_H */
