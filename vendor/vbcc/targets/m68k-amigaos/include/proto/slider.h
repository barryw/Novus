#ifndef PROTO_SLIDER_H
#define PROTO_SLIDER_H

/*
**	$VER: slider.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/slider_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * SliderBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_SLIDER_PRAGMAS_H
#include <pragmas/slider_pragmas.h>
#endif /* PRAGMAS_SLIDER_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_SLIDER_LIB_H
#include <pragma/slider_lib.h>
#endif /* PRAGMA_SLIDER_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/slider_protos.h>

#ifndef _VBCCINLINE_SLIDER_H
#include <inline/slider_protos.h>
#endif /* _VBCCINLINE_SLIDER_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_SLIDER_H
#include <inline/slider.h>
#endif /* _INLINE_SLIDER_H */
#else
#include <clib/slider_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/slider_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_SLIDER_H */
