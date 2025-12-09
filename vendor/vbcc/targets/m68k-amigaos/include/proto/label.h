#ifndef PROTO_LABEL_H
#define PROTO_LABEL_H

/*
**	$VER: label.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/label_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * LabelBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_LABEL_PRAGMAS_H
#include <pragmas/label_pragmas.h>
#endif /* PRAGMAS_LABEL_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_LABEL_LIB_H
#include <pragma/label_lib.h>
#endif /* PRAGMA_LABEL_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/label_protos.h>

#ifndef _VBCCINLINE_LABEL_H
#include <inline/label_protos.h>
#endif /* _VBCCINLINE_LABEL_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_LABEL_H
#include <inline/label.h>
#endif /* _INLINE_LABEL_H */
#else
#include <clib/label_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/label_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_LABEL_H */
