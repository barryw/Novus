#ifndef _INLINE_MATHFFP_H
#define _INLINE_MATHFFP_H

#ifndef CLIB_MATHFFP_PROTOS_H
#define CLIB_MATHFFP_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif

#ifndef MATHFFP_BASE_NAME
#define MATHFFP_BASE_NAME MathBase
#endif

#define SPFix(parm) ({ \
  FLOAT _SPFix_parm = (parm); \
  ({ \
  register char * _SPFix__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPFix__bn - 30))(_SPFix__bn, _SPFix_parm); \
});})

#define SPFlt(integer) ({ \
  LONG _SPFlt_integer = (integer); \
  ({ \
  register char * _SPFlt__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_SPFlt__bn - 36))(_SPFlt__bn, _SPFlt_integer); \
});})

#define SPCmp(leftParm, rightParm) ({ \
  FLOAT _SPCmp_leftParm = (leftParm); \
  FLOAT _SPCmp_rightParm = (rightParm); \
  ({ \
  register char * _SPCmp__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), FLOAT __asm("d1"), FLOAT __asm("d0"))) \
  (_SPCmp__bn - 42))(_SPCmp__bn, _SPCmp_leftParm, _SPCmp_rightParm); \
});})

#define SPTst(parm) ({ \
  FLOAT _SPTst_parm = (parm); \
  ({ \
  register char * _SPTst__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), FLOAT __asm("d1"))) \
  (_SPTst__bn - 48))(_SPTst__bn, _SPTst_parm); \
});})

#define SPAbs(parm) ({ \
  FLOAT _SPAbs_parm = (parm); \
  ({ \
  register char * _SPAbs__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPAbs__bn - 54))(_SPAbs__bn, _SPAbs_parm); \
});})

#define SPNeg(parm) ({ \
  FLOAT _SPNeg_parm = (parm); \
  ({ \
  register char * _SPNeg__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPNeg__bn - 60))(_SPNeg__bn, _SPNeg_parm); \
});})

#define SPAdd(leftParm, rightParm) ({ \
  FLOAT _SPAdd_leftParm = (leftParm); \
  FLOAT _SPAdd_rightParm = (rightParm); \
  ({ \
  register char * _SPAdd__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d1"), FLOAT __asm("d0"))) \
  (_SPAdd__bn - 66))(_SPAdd__bn, _SPAdd_leftParm, _SPAdd_rightParm); \
});})

#define SPSub(leftParm, rightParm) ({ \
  FLOAT _SPSub_leftParm = (leftParm); \
  FLOAT _SPSub_rightParm = (rightParm); \
  ({ \
  register char * _SPSub__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d1"), FLOAT __asm("d0"))) \
  (_SPSub__bn - 72))(_SPSub__bn, _SPSub_leftParm, _SPSub_rightParm); \
});})

#define SPMul(leftParm, rightParm) ({ \
  FLOAT _SPMul_leftParm = (leftParm); \
  FLOAT _SPMul_rightParm = (rightParm); \
  ({ \
  register char * _SPMul__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d1"), FLOAT __asm("d0"))) \
  (_SPMul__bn - 78))(_SPMul__bn, _SPMul_leftParm, _SPMul_rightParm); \
});})

#define SPDiv(leftParm, rightParm) ({ \
  FLOAT _SPDiv_leftParm = (leftParm); \
  FLOAT _SPDiv_rightParm = (rightParm); \
  ({ \
  register char * _SPDiv__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d1"), FLOAT __asm("d0"))) \
  (_SPDiv__bn - 84))(_SPDiv__bn, _SPDiv_leftParm, _SPDiv_rightParm); \
});})

#define SPFloor(parm) ({ \
  FLOAT _SPFloor_parm = (parm); \
  ({ \
  register char * _SPFloor__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPFloor__bn - 90))(_SPFloor__bn, _SPFloor_parm); \
});})

#define SPCeil(parm) ({ \
  FLOAT _SPCeil_parm = (parm); \
  ({ \
  register char * _SPCeil__bn __asm("a6") = (char *) (MATHFFP_BASE_NAME);\
  ((FLOAT (*)(char * __asm("a6"), FLOAT __asm("d0"))) \
  (_SPCeil__bn - 96))(_SPCeil__bn, _SPCeil_parm); \
});})

#endif /*  _INLINE_MATHFFP_H  */
