#ifndef PROTO_RAMDRIVE_H
#define PROTO_RAMDRIVE_H

/*
**	$VER: ramdrive.h 47.4 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/ramdrive_protos.h>

#else

/****************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_DEVICES_H
#include <exec/devices.h>
#endif /* EXEC_DEVICES_H */

extern struct Device * RamdriveBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_RAMDRIVE_PRAGMAS_H
#include <pragmas/ramdrive_pragmas.h>
#endif /* PRAGMAS_RAMDRIVE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_RAMDRIVE_LIB_H
#include <pragma/ramdrive_lib.h>
#endif /* PRAGMA_RAMDRIVE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/ramdrive_protos.h>

#ifndef _VBCCINLINE_RAMDRIVE_H
#include <inline/ramdrive_protos.h>
#endif /* _VBCCINLINE_RAMDRIVE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_RAMDRIVE_H
#include <inline/ramdrive.h>
#endif /* _INLINE_RAMDRIVE_H */
#else
#include <clib/ramdrive_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/ramdrive_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_RAMDRIVE_H */
