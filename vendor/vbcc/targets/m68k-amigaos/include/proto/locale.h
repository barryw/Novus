#ifndef PROTO_LOCALE_H
#define PROTO_LOCALE_H

/*
**	$VER: locale.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/locale_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef LIBRARIES_LOCALE_H
#include <libraries/locale.h>
#endif /* LIBRARIES_LOCALE_H */

extern struct LocaleBase * LocaleBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_LOCALE_PRAGMAS_H
#include <pragmas/locale_pragmas.h>
#endif /* PRAGMAS_LOCALE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_LOCALE_LIB_H
#include <pragma/locale_lib.h>
#endif /* PRAGMA_LOCALE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/locale_protos.h>

#ifndef _VBCCINLINE_LOCALE_H
#include <inline/locale_protos.h>
#endif /* _VBCCINLINE_LOCALE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_LOCALE_H
#include <inline/locale.h>
#endif /* _INLINE_LOCALE_H */
#else
#include <clib/locale_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/locale_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_LOCALE_H */
