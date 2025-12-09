#ifndef CLIB_MATHIEEEDOUBTRANS_PROTOS_H
#define CLIB_MATHIEEEDOUBTRANS_PROTOS_H

/*
**	$VER: mathieeedoubtrans_protos.h 47.1 (30.11.2021)
**
**	'C' prototypes. For use with 32 bit integers only.
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

#ifndef  LIBRARIES_MATHLIBRARY_H
#include <libraries/mathlibrary.h>
#endif
DOUBLE IEEEDPAtan( DOUBLE parm );
DOUBLE IEEEDPSin( DOUBLE parm );
DOUBLE IEEEDPCos( DOUBLE parm );
DOUBLE IEEEDPTan( DOUBLE parm );
DOUBLE IEEEDPSincos( DOUBLE *pf2, DOUBLE parm );
DOUBLE IEEEDPSinh( DOUBLE parm );
DOUBLE IEEEDPCosh( DOUBLE parm );
DOUBLE IEEEDPTanh( DOUBLE parm );
DOUBLE IEEEDPExp( DOUBLE parm );
DOUBLE IEEEDPLog( DOUBLE parm );
DOUBLE IEEEDPPow( DOUBLE exp, DOUBLE arg );
DOUBLE IEEEDPSqrt( DOUBLE parm );
FLOAT IEEEDPTieee( DOUBLE parm );
DOUBLE IEEEDPFieee( FLOAT single );
DOUBLE IEEEDPAsin( DOUBLE parm );
DOUBLE IEEEDPAcos( DOUBLE parm );
DOUBLE IEEEDPLog10( DOUBLE parm );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_MATHIEEEDOUBTRANS_PROTOS_H */
