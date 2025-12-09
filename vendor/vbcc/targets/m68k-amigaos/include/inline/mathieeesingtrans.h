#ifndef _INLINE_MATHIEEESINGTRANS_H
#define _INLINE_MATHIEEESINGTRANS_H

#ifndef CLIB_MATHIEEESINGTRANS_PROTOS_H
#define CLIB_MATHIEEESINGTRANS_PROTOS_H
#endif

#ifndef  LIBRARIES_MATHLIBRARY_H
#include <libraries/mathlibrary.h>
#endif

#ifndef MATHIEEESINGTRANS_BASE_NAME
#define MATHIEEESINGTRANS_BASE_NAME MathIeeeSingTransBase
#endif

#define IEEESPAtan(parm) ({ \
  FLOAT _IEEESPAtan_parm = (parm); \
  ({ \
  register char * _IEEESPAtan__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPAtan__bn - 30))(_IEEESPAtan__bn, _IEEESPAtan_parm); \
});})

#define IEEESPSin(parm) ({ \
  FLOAT _IEEESPSin_parm = (parm); \
  ({ \
  register char * _IEEESPSin__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPSin__bn - 36))(_IEEESPSin__bn, _IEEESPSin_parm); \
});})

#define IEEESPCos(parm) ({ \
  FLOAT _IEEESPCos_parm = (parm); \
  ({ \
  register char * _IEEESPCos__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPCos__bn - 42))(_IEEESPCos__bn, _IEEESPCos_parm); \
});})

#define IEEESPTan(parm) ({ \
  FLOAT _IEEESPTan_parm = (parm); \
  ({ \
  register char * _IEEESPTan__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPTan__bn - 48))(_IEEESPTan__bn, _IEEESPTan_parm); \
});})

#define IEEESPSincos(cosptr, parm) ({ \
  FLOAT * _IEEESPSincos_cosptr = (cosptr); \
  FLOAT _IEEESPSincos_parm = (parm); \
  ({ \
  register char * _IEEESPSincos__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT * __asm("a0"), FLOAT __asm("d0"))) \
  (_IEEESPSincos__bn - 54))(_IEEESPSincos__bn, _IEEESPSincos_cosptr, _IEEESPSincos_parm); \
});})

#define IEEESPSinh(parm) ({ \
  FLOAT _IEEESPSinh_parm = (parm); \
  ({ \
  register char * _IEEESPSinh__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPSinh__bn - 60))(_IEEESPSinh__bn, _IEEESPSinh_parm); \
});})

#define IEEESPCosh(parm) ({ \
  FLOAT _IEEESPCosh_parm = (parm); \
  ({ \
  register char * _IEEESPCosh__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPCosh__bn - 66))(_IEEESPCosh__bn, _IEEESPCosh_parm); \
});})

#define IEEESPTanh(parm) ({ \
  FLOAT _IEEESPTanh_parm = (parm); \
  ({ \
  register char * _IEEESPTanh__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPTanh__bn - 72))(_IEEESPTanh__bn, _IEEESPTanh_parm); \
});})

#define IEEESPExp(parm) ({ \
  FLOAT _IEEESPExp_parm = (parm); \
  ({ \
  register char * _IEEESPExp__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPExp__bn - 78))(_IEEESPExp__bn, _IEEESPExp_parm); \
});})

#define IEEESPLog(parm) ({ \
  FLOAT _IEEESPLog_parm = (parm); \
  ({ \
  register char * _IEEESPLog__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPLog__bn - 84))(_IEEESPLog__bn, _IEEESPLog_parm); \
});})

#define IEEESPPow(exp, arg) ({ \
  FLOAT _IEEESPPow_exp = (exp); \
  FLOAT _IEEESPPow_arg = (arg); \
  ({ \
  register char * _IEEESPPow__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d1"), FLOAT __asm("d0"))) \
  (_IEEESPPow__bn - 90))(_IEEESPPow__bn, _IEEESPPow_exp, _IEEESPPow_arg); \
});})

#define IEEESPSqrt(parm) ({ \
  FLOAT _IEEESPSqrt_parm = (parm); \
  ({ \
  register char * _IEEESPSqrt__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPSqrt__bn - 96))(_IEEESPSqrt__bn, _IEEESPSqrt_parm); \
});})

#define IEEESPTieee(parm) ({ \
  FLOAT _IEEESPTieee_parm = (parm); \
  ({ \
  register char * _IEEESPTieee__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPTieee__bn - 102))(_IEEESPTieee__bn, _IEEESPTieee_parm); \
});})

#define IEEESPFieee(parm) ({ \
  FLOAT _IEEESPFieee_parm = (parm); \
  ({ \
  register char * _IEEESPFieee__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPFieee__bn - 108))(_IEEESPFieee__bn, _IEEESPFieee_parm); \
});})

#define IEEESPAsin(parm) ({ \
  FLOAT _IEEESPAsin_parm = (parm); \
  ({ \
  register char * _IEEESPAsin__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPAsin__bn - 114))(_IEEESPAsin__bn, _IEEESPAsin_parm); \
});})

#define IEEESPAcos(parm) ({ \
  FLOAT _IEEESPAcos_parm = (parm); \
  ({ \
  register char * _IEEESPAcos__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPAcos__bn - 120))(_IEEESPAcos__bn, _IEEESPAcos_parm); \
});})

#define IEEESPLog10(parm) ({ \
  FLOAT _IEEESPLog10_parm = (parm); \
  ({ \
  register char * _IEEESPLog10__bn __asm("a6") = (char *) (MATHIEEESINGTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_IEEESPLog10__bn - 126))(_IEEESPLog10__bn, _IEEESPLog10_parm); \
});})

#endif /*  _INLINE_MATHIEEESINGTRANS_H  */
