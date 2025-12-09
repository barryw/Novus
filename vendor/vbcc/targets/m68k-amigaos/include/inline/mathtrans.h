#ifndef _INLINE_MATHTRANS_H
#define _INLINE_MATHTRANS_H

#ifndef CLIB_MATHTRANS_PROTOS_H
#define CLIB_MATHTRANS_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif

#ifndef MATHTRANS_BASE_NAME
#define MATHTRANS_BASE_NAME MathTransBase
#endif

#define SPAtan(parm) ({ \
  FLOAT _SPAtan_parm = (parm); \
  ({ \
  register char * _SPAtan__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPAtan__bn - 30))(_SPAtan__bn, _SPAtan_parm); \
});})

#define SPSin(parm) ({ \
  FLOAT _SPSin_parm = (parm); \
  ({ \
  register char * _SPSin__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPSin__bn - 36))(_SPSin__bn, _SPSin_parm); \
});})

#define SPCos(parm) ({ \
  FLOAT _SPCos_parm = (parm); \
  ({ \
  register char * _SPCos__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPCos__bn - 42))(_SPCos__bn, _SPCos_parm); \
});})

#define SPTan(parm) ({ \
  FLOAT _SPTan_parm = (parm); \
  ({ \
  register char * _SPTan__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPTan__bn - 48))(_SPTan__bn, _SPTan_parm); \
});})

#define SPSincos(cosResult, parm) ({ \
  FLOAT * _SPSincos_cosResult = (cosResult); \
  FLOAT _SPSincos_parm = (parm); \
  ({ \
  register char * _SPSincos__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT * __asm("d1"), FLOAT __asm("d0"))) \
  (_SPSincos__bn - 54))(_SPSincos__bn, _SPSincos_cosResult, _SPSincos_parm); \
});})

#define SPSinh(parm) ({ \
  FLOAT _SPSinh_parm = (parm); \
  ({ \
  register char * _SPSinh__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPSinh__bn - 60))(_SPSinh__bn, _SPSinh_parm); \
});})

#define SPCosh(parm) ({ \
  FLOAT _SPCosh_parm = (parm); \
  ({ \
  register char * _SPCosh__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPCosh__bn - 66))(_SPCosh__bn, _SPCosh_parm); \
});})

#define SPTanh(parm) ({ \
  FLOAT _SPTanh_parm = (parm); \
  ({ \
  register char * _SPTanh__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPTanh__bn - 72))(_SPTanh__bn, _SPTanh_parm); \
});})

#define SPExp(parm) ({ \
  FLOAT _SPExp_parm = (parm); \
  ({ \
  register char * _SPExp__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPExp__bn - 78))(_SPExp__bn, _SPExp_parm); \
});})

#define SPLog(parm) ({ \
  FLOAT _SPLog_parm = (parm); \
  ({ \
  register char * _SPLog__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPLog__bn - 84))(_SPLog__bn, _SPLog_parm); \
});})

#define SPPow(power, arg) ({ \
  FLOAT _SPPow_power = (power); \
  FLOAT _SPPow_arg = (arg); \
  ({ \
  register char * _SPPow__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d1"), FLOAT __asm("d0"))) \
  (_SPPow__bn - 90))(_SPPow__bn, _SPPow_power, _SPPow_arg); \
});})

#define SPSqrt(parm) ({ \
  FLOAT _SPSqrt_parm = (parm); \
  ({ \
  register char * _SPSqrt__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPSqrt__bn - 96))(_SPSqrt__bn, _SPSqrt_parm); \
});})

#define SPTieee(parm) ({ \
  FLOAT _SPTieee_parm = (parm); \
  ({ \
  register char * _SPTieee__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPTieee__bn - 102))(_SPTieee__bn, _SPTieee_parm); \
});})

#define SPFieee(parm) ({ \
  FLOAT _SPFieee_parm = (parm); \
  ({ \
  register char * _SPFieee__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPFieee__bn - 108))(_SPFieee__bn, _SPFieee_parm); \
});})

#define SPAsin(parm) ({ \
  FLOAT _SPAsin_parm = (parm); \
  ({ \
  register char * _SPAsin__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPAsin__bn - 114))(_SPAsin__bn, _SPAsin_parm); \
});})

#define SPAcos(parm) ({ \
  FLOAT _SPAcos_parm = (parm); \
  ({ \
  register char * _SPAcos__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPAcos__bn - 120))(_SPAcos__bn, _SPAcos_parm); \
});})

#define SPLog10(parm) ({ \
  FLOAT _SPLog10_parm = (parm); \
  ({ \
  register char * _SPLog10__bn __asm("a6") = (char *) (MATHTRANS_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPLog10__bn - 126))(_SPLog10__bn, _SPLog10_parm); \
});})

#endif /*  _INLINE_MATHTRANS_H  */
