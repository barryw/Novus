#ifndef PROTO_DISK_H
#define PROTO_DISK_H

/*
**	$VER: disk.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/disk_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * DiskBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_DISK_PRAGMAS_H
#include <pragmas/disk_pragmas.h>
#endif /* PRAGMAS_DISK_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_DISK_LIB_H
#include <pragma/disk_lib.h>
#endif /* PRAGMA_DISK_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/disk_protos.h>

#ifndef _VBCCINLINE_DISK_H
#include <inline/disk_protos.h>
#endif /* _VBCCINLINE_DISK_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_DISK_H
#include <inline/disk.h>
#endif /* _INLINE_DISK_H */
#else
#include <clib/disk_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/disk_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_DISK_H */
