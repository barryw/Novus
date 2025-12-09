#ifndef PROTO_CONSOLE_H
#define PROTO_CONSOLE_H

/*
**	$VER: console.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/console_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_DEVICES_H
#include <exec/devices.h>
#endif /* EXEC_DEVICES_H */

extern struct Device * ConsoleBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_CONSOLE_PRAGMAS_H
#include <pragmas/console_pragmas.h>
#endif /* PRAGMAS_CONSOLE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_CONSOLE_LIB_H
#include <pragma/console_lib.h>
#endif /* PRAGMA_CONSOLE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/console_protos.h>

#ifndef _VBCCINLINE_CONSOLE_H
#include <inline/console_protos.h>
#endif /* _VBCCINLINE_CONSOLE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_CONSOLE_H
#include <inline/console.h>
#endif /* _INLINE_CONSOLE_H */
#else
#include <clib/console_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/console_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_CONSOLE_H */
