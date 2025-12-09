#ifndef PROTO_TEXTEDITOR_H
#define PROTO_TEXTEDITOR_H

/*
**	$VER: texteditor.h 47.7 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/texteditor_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * TextFieldBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_TEXTEDITOR_PRAGMAS_H
#include <pragmas/texteditor_pragmas.h>
#endif /* PRAGMAS_TEXTEDITOR_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_TEXTEDITOR_LIB_H
#include <pragma/texteditor_lib.h>
#endif /* PRAGMA_TEXTEDITOR_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/texteditor_protos.h>

#ifndef _VBCCINLINE_TEXTEDITOR_H
#include <inline/texteditor_protos.h>
#endif /* _VBCCINLINE_TEXTEDITOR_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_TEXTEDITOR_H
#include <inline/texteditor.h>
#endif /* _INLINE_TEXTEDITOR_H */
#else
#include <clib/texteditor_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/texteditor_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_TEXTEDITOR_H */
