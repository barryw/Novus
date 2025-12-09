#ifndef _INLINE_CIA_H
#define _INLINE_CIA_H

#ifndef CLIB_CIA_PROTOS_H
#define CLIB_CIA_PROTOS_H
#endif

#ifndef  EXEC_INTERRUPTS_H
#include <exec/interrupts.h>
#endif
#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif

#define AddICRVector(resource, iCRBit, interrupt) ({ \
  struct Library * _AddICRVector_resource = (resource); \
  LONG _AddICRVector_iCRBit = (iCRBit); \
  struct Interrupt * _AddICRVector_interrupt = (interrupt); \
  ({ \
  register char * _AddICRVector__bn __asm("a6") = (char *) (resource);\
  ((struct Interrupt * (*)(struct Library * __asm("a6"), LONG __asm("d0"), struct Interrupt * __asm("a1"))) \
  (_AddICRVector__bn - 6))(_AddICRVector__bn, _AddICRVector_iCRBit, _AddICRVector_interrupt); \
});})

#define RemICRVector(resource, iCRBit, interrupt) ({ \
  struct Library * _RemICRVector_resource = (resource); \
  LONG _RemICRVector_iCRBit = (iCRBit); \
  struct Interrupt * _RemICRVector_interrupt = (interrupt); \
  ({ \
  register char * _RemICRVector__bn __asm("a6") = (char *) (resource);\
  ((VOID (*)(struct Library * __asm("a6"), LONG __asm("d0"), struct Interrupt * __asm("a1"))) \
  (_RemICRVector__bn - 12))(_RemICRVector__bn, _RemICRVector_iCRBit, _RemICRVector_interrupt); \
});})

#define AbleICR(resource, mask) ({ \
  struct Library * _AbleICR_resource = (resource); \
  LONG _AbleICR_mask = (mask); \
  ({ \
  register char * _AbleICR__bn __asm("a6") = (char *) (resource);\
  ((WORD (*)(struct Library * __asm("a6"), LONG __asm("d0"))) \
  (_AbleICR__bn - 18))(_AbleICR__bn, _AbleICR_mask); \
});})

#define SetICR(resource, mask) ({ \
  struct Library * _SetICR_resource = (resource); \
  LONG _SetICR_mask = (mask); \
  ({ \
  register char * _SetICR__bn __asm("a6") = (char *) (resource);\
  ((WORD (*)(struct Library * __asm("a6"), LONG __asm("d0"))) \
  (_SetICR__bn - 24))(_SetICR__bn, _SetICR_mask); \
});})

#endif /*  _INLINE_CIA_H  */
