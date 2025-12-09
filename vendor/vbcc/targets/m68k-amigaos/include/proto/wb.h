#ifndef PROTO_WB_H
#define PROTO_WB_H

/*
**	$VER: wb.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/wb_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * WorkbenchBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_WB_PRAGMAS_H
#include <pragmas/wb_pragmas.h>
#endif /* PRAGMAS_WB_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_WB_LIB_H
#include <pragma/wb_lib.h>
#endif /* PRAGMA_WB_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/wb_protos.h>

#ifndef _VBCCINLINE_WB_H
#include <inline/wb_protos.h>
#endif /* _VBCCINLINE_WB_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_WB_H
#include <inline/wb.h>
#endif /* _INLINE_WB_H */
#else
#include <clib/wb_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/wb_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_WB_H */
