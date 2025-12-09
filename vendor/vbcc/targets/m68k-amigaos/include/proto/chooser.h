#ifndef PROTO_CHOOSER_H
#define PROTO_CHOOSER_H

/*
**	$VER: chooser.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/chooser_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * ChooserBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_CHOOSER_PRAGMAS_H
#include <pragmas/chooser_pragmas.h>
#endif /* PRAGMAS_CHOOSER_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_CHOOSER_LIB_H
#include <pragma/chooser_lib.h>
#endif /* PRAGMA_CHOOSER_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/chooser_protos.h>

#ifndef _VBCCINLINE_CHOOSER_H
#include <inline/chooser_protos.h>
#endif /* _VBCCINLINE_CHOOSER_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_CHOOSER_H
#include <inline/chooser.h>
#endif /* _INLINE_CHOOSER_H */
#else
#include <clib/chooser_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/chooser_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_CHOOSER_H */
