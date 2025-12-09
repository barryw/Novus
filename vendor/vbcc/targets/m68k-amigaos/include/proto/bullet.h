#ifndef PROTO_BULLET_H
#define PROTO_BULLET_H

/*
**	$VER: bullet.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/bullet_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * BulletBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_BULLET_PRAGMAS_H
#include <pragmas/bullet_pragmas.h>
#endif /* PRAGMAS_BULLET_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_BULLET_LIB_H
#include <pragma/bullet_lib.h>
#endif /* PRAGMA_BULLET_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/bullet_protos.h>

#ifndef _VBCCINLINE_BULLET_H
#include <inline/bullet_protos.h>
#endif /* _VBCCINLINE_BULLET_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_BULLET_H
#include <inline/bullet.h>
#endif /* _INLINE_BULLET_H */
#else
#include <clib/bullet_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/bullet_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_BULLET_H */
