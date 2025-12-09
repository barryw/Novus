#ifndef PROTO_EXEC_H
#define PROTO_EXEC_H

/*
**	$VER: exec.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/exec_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_EXECBASE_H
#include <exec/execbase.h>
#endif /* EXEC_EXECBASE_H */

extern struct ExecBase * SysBase;

#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#if defined(_USEOLDEXEC_) || !defined(__USE_SYSBASE)

/****************************************************************************/

#ifndef PRAGMAS_EXEC_PRAGMAS_H
#include <pragmas/exec_pragmas.h>
#endif /* PRAGMAS_EXEC_PRAGMAS_H */

#else

/****************************************************************************/

#ifndef PRAGMAS_EXEC_SYSBASE_PRAGMAS_H
#include <pragmas/exec_sysbase_pragmas.h>
#endif /* PRAGMAS_EXEC_SYSBASE_PRAGMAS_H */

/****************************************************************************/

#endif /* defined(_USEOLDEXEC_) || !defined(__USE_SYSBASE) */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_EXEC_LIB_H
#include <pragma/exec_lib.h>
#endif /* PRAGMA_EXEC_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/exec_protos.h>

#ifndef _VBCCINLINE_EXEC_H
#include <inline/exec_protos.h>
#endif /* _VBCCINLINE_EXEC_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_EXEC_H
#include <inline/exec.h>
#endif /* _INLINE_EXEC_H */
#else
#include <clib/exec_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/exec_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_EXEC_H */
