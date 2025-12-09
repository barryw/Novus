#ifndef _INLINE_COLORWHEEL_H
#define _INLINE_COLORWHEEL_H

#ifndef CLIB_COLORWHEEL_PROTOS_H
#define CLIB_COLORWHEEL_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  GADGETS_COLORWHEEL_H
#include <gadgets/colorwheel.h>
#endif

#ifndef COLORWHEEL_BASE_NAME
#define COLORWHEEL_BASE_NAME ColorWheelBase
#endif

#define ConvertHSBToRGB(hsb, rgb) ({ \
  CONST struct ColorWheelHSB * _ConvertHSBToRGB_hsb = (hsb); \
  struct ColorWheelRGB * _ConvertHSBToRGB_rgb = (rgb); \
  ({ \
  register char * _ConvertHSBToRGB__bn __asm("a6") = (char *) (COLORWHEEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct ColorWheelHSB * __asm("a0"), struct ColorWheelRGB * __asm("a1"))) \
  (_ConvertHSBToRGB__bn - 30))(_ConvertHSBToRGB__bn, _ConvertHSBToRGB_hsb, _ConvertHSBToRGB_rgb); \
});})

#define ConvertRGBToHSB(rgb, hsb) ({ \
  CONST struct ColorWheelRGB * _ConvertRGBToHSB_rgb = (rgb); \
  struct ColorWheelHSB * _ConvertRGBToHSB_hsb = (hsb); \
  ({ \
  register char * _ConvertRGBToHSB__bn __asm("a6") = (char *) (COLORWHEEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct ColorWheelRGB * __asm("a0"), struct ColorWheelHSB * __asm("a1"))) \
  (_ConvertRGBToHSB__bn - 36))(_ConvertRGBToHSB__bn, _ConvertRGBToHSB_rgb, _ConvertRGBToHSB_hsb); \
});})

#endif /*  _INLINE_COLORWHEEL_H  */
