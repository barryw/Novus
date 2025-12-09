#ifndef PROTO_BUTTON_H
#define PROTO_BUTTON_H

/*
**	$VER: button.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/button_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * ButtonBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_BUTTON_PRAGMAS_H
#include <pragmas/button_pragmas.h>
#endif /* PRAGMAS_BUTTON_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_BUTTON_LIB_H
#include <pragma/button_lib.h>
#endif /* PRAGMA_BUTTON_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/button_protos.h>

#ifndef _VBCCINLINE_BUTTON_H
#include <inline/button_protos.h>
#endif /* _VBCCINLINE_BUTTON_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_BUTTON_H
#include <inline/button.h>
#endif /* _INLINE_BUTTON_H */
#else
#include <clib/button_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/button_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_BUTTON_H */
