#ifndef PROTO_GETFILE_H
#define PROTO_GETFILE_H

/*
**	$VER: getfile.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/getfile_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * GetFileBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_GETFILE_PRAGMAS_H
#include <pragmas/getfile_pragmas.h>
#endif /* PRAGMAS_GETFILE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_GETFILE_LIB_H
#include <pragma/getfile_lib.h>
#endif /* PRAGMA_GETFILE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/getfile_protos.h>

#ifndef _VBCCINLINE_GETFILE_H
#include <inline/getfile_protos.h>
#endif /* _VBCCINLINE_GETFILE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_GETFILE_H
#include <inline/getfile.h>
#endif /* _INLINE_GETFILE_H */
#else
#include <clib/getfile_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/getfile_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_GETFILE_H */
