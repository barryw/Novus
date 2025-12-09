#ifndef _INLINE_VIRTUAL_H
#define _INLINE_VIRTUAL_H

#ifndef CLIB_VIRTUAL_PROTOS_H
#define CLIB_VIRTUAL_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  INTUITION_CLASSES_H
#include <intuition/classes.h>
#endif
#ifndef  GADGETS_LAYOUT_H
#include <gadgets/layout.h>
#endif
#ifndef  GADGETS_VIRTUAL_H
#include <gadgets/virtual.h>
#endif

#ifndef VIRTUAL_BASE_NAME
#define VIRTUAL_BASE_NAME VirtualBase
#endif

#define VIRTUAL_GetClass() ({ \
  register char * _VIRTUAL_GetClass__bn __asm("a6") = (char *) (VIRTUAL_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_VIRTUAL_GetClass__bn - 30))(_VIRTUAL_GetClass__bn); \
})

#define RefreshVirtualGadget(gadget, obj, window, requester) ({ \
  struct Gadget * _RefreshVirtualGadget_gadget = (gadget); \
  Object * _RefreshVirtualGadget_obj = (obj); \
  struct Window * _RefreshVirtualGadget_window = (window); \
  struct Requester * _RefreshVirtualGadget_requester = (requester); \
  ({ \
  register char * _RefreshVirtualGadget__bn __asm("a6") = (char *) (VIRTUAL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), Object * __asm("a1"), struct Window * __asm("a2"), struct Requester * __asm("a3"))) \
  (_RefreshVirtualGadget__bn - 36))(_RefreshVirtualGadget__bn, _RefreshVirtualGadget_gadget, _RefreshVirtualGadget_obj, _RefreshVirtualGadget_window, _RefreshVirtualGadget_requester); \
});})

#define RethinkVirtualSize(virt_obj, rootlayout, font, screen, layoutlimits) ({ \
  Object * _RethinkVirtualSize_virt_obj = (virt_obj); \
  Object * _RethinkVirtualSize_rootlayout = (rootlayout); \
  struct TextFont * _RethinkVirtualSize_font = (font); \
  struct Screen * _RethinkVirtualSize_screen = (screen); \
  struct LayoutLimits * _RethinkVirtualSize_layoutlimits = (layoutlimits); \
  ({ \
  register char * _RethinkVirtualSize__bn __asm("a6") = (char *) (VIRTUAL_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), Object * __asm("a0"), Object * __asm("a1"), struct TextFont * __asm("a2"), struct Screen * __asm("a3"), struct LayoutLimits * __asm("d0"))) \
  (_RethinkVirtualSize__bn - 42))(_RethinkVirtualSize__bn, _RethinkVirtualSize_virt_obj, _RethinkVirtualSize_rootlayout, _RethinkVirtualSize_font, _RethinkVirtualSize_screen, _RethinkVirtualSize_layoutlimits); \
});})

#endif /*  _INLINE_VIRTUAL_H  */
