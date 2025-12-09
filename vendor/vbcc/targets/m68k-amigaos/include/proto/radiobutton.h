#ifndef PROTO_RADIOBUTTON_H
#define PROTO_RADIOBUTTON_H

/*
**	$VER: radiobutton.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/radiobutton_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * RadioButtonBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_RADIOBUTTON_PRAGMAS_H
#include <pragmas/radiobutton_pragmas.h>
#endif /* PRAGMAS_RADIOBUTTON_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_RADIOBUTTON_LIB_H
#include <pragma/radiobutton_lib.h>
#endif /* PRAGMA_RADIOBUTTON_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/radiobutton_protos.h>

#ifndef _VBCCINLINE_RADIOBUTTON_H
#include <inline/radiobutton_protos.h>
#endif /* _VBCCINLINE_RADIOBUTTON_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_RADIOBUTTON_H
#include <inline/radiobutton.h>
#endif /* _INLINE_RADIOBUTTON_H */
#else
#include <clib/radiobutton_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/radiobutton_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_RADIOBUTTON_H */
