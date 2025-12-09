#ifndef PROTO_LISTBROWSER_H
#define PROTO_LISTBROWSER_H

/*
**	$VER: listbrowser.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/listbrowser_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * ListBrowserBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_LISTBROWSER_PRAGMAS_H
#include <pragmas/listbrowser_pragmas.h>
#endif /* PRAGMAS_LISTBROWSER_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_LISTBROWSER_LIB_H
#include <pragma/listbrowser_lib.h>
#endif /* PRAGMA_LISTBROWSER_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/listbrowser_protos.h>

#ifndef _VBCCINLINE_LISTBROWSER_H
#include <inline/listbrowser_protos.h>
#endif /* _VBCCINLINE_LISTBROWSER_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_LISTBROWSER_H
#include <inline/listbrowser.h>
#endif /* _INLINE_LISTBROWSER_H */
#else
#include <clib/listbrowser_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/listbrowser_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_LISTBROWSER_H */
