#ifndef PROTO_INPUT_H
#define PROTO_INPUT_H

/*
**	$VER: input.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/input_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_DEVICES_H
#include <exec/devices.h>
#endif /* EXEC_DEVICES_H */

extern struct Device * InputBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_INPUT_PRAGMAS_H
#include <pragmas/input_pragmas.h>
#endif /* PRAGMAS_INPUT_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_INPUT_LIB_H
#include <pragma/input_lib.h>
#endif /* PRAGMA_INPUT_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/input_protos.h>

#ifndef _VBCCINLINE_INPUT_H
#include <inline/input_protos.h>
#endif /* _VBCCINLINE_INPUT_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_INPUT_H
#include <inline/input.h>
#endif /* _INLINE_INPUT_H */
#else
#include <clib/input_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/input_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_INPUT_H */
