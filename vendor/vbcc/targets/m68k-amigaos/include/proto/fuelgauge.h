#ifndef PROTO_FUELGAUGE_H
#define PROTO_FUELGAUGE_H

/*
**	$VER: fuelgauge.h 47.6 (6.12.2021)
**
**	Lattice 'C' style prototype/pragma header file combo
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

/****************************************************************************/

#ifdef _NO_INLINE

#include <clib/fuelgauge_protos.h>

#else

/***************************************************************************/

#ifndef __NOLIBBASE__

#ifndef EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif /* EXEC_LIBRARIES_H */

extern struct Library * FuelGaugeBase;
#endif /* __NOLIBBASE__ */

/****************************************************************************/

#if defined(LATTICE) || defined(__SASC) || defined(_DCC)

#ifndef PRAGMAS_FUELGAUGE_PRAGMAS_H
#include <pragmas/fuelgauge_pragmas.h>
#endif /* PRAGMAS_FUELGAUGE_PRAGMAS_H */

/****************************************************************************/

#elif defined(AZTEC_C) || defined(__MAXON__) || defined(__STORM__)

#ifndef PRAGMA_FUELGAUGE_LIB_H
#include <pragma/fuelgauge_lib.h>
#endif /* PRAGMA_FUELGAUGE_LIB_H */

/****************************************************************************/

#elif defined(__VBCC__)

#include <clib/fuelgauge_protos.h>

#ifndef _VBCCINLINE_FUELGAUGE_H
#include <inline/fuelgauge_protos.h>
#endif /* _VBCCINLINE_FUELGAUGE_H */

/****************************************************************************/

#elif defined(__GNUC__)

#if defined(mc68000)
#ifndef _INLINE_FUELGAUGE_H
#include <inline/fuelgauge.h>
#endif /* _INLINE_FUELGAUGE_H */
#else
#include <clib/fuelgauge_protos.h>
#endif /* mc68000 */

/****************************************************************************/

/* Any other compiler */
#else

#include <clib/fuelgauge_protos.h>

/****************************************************************************/

#endif /* __GNUC__ */

/****************************************************************************/

#endif /* _NO_INLINE */

/****************************************************************************/

#endif /* PROTO_FUELGAUGE_H */
