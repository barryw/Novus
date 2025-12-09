#ifndef _INLINE_LAYOUT_H
#define _INLINE_LAYOUT_H

#ifndef CLIB_LAYOUT_PROTOS_H
#define CLIB_LAYOUT_PROTOS_H
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
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  GADGETS_LAYOUT_H
#include <gadgets/layout.h>
#endif

#ifndef LAYOUT_BASE_NAME
#define LAYOUT_BASE_NAME LayoutBase
#endif

#define LAYOUT_GetClass() ({ \
  register char * _LAYOUT_GetClass__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_LAYOUT_GetClass__bn - 30))(_LAYOUT_GetClass__bn); \
})

#define ActivateLayoutGadget(gadget, window, requester, object) ({ \
  struct Gadget * _ActivateLayoutGadget_gadget = (gadget); \
  struct Window * _ActivateLayoutGadget_window = (window); \
  struct Requester * _ActivateLayoutGadget_requester = (requester); \
  ULONG _ActivateLayoutGadget_object = (object); \
  ({ \
  register char * _ActivateLayoutGadget__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), ULONG __asm("d0"))) \
  (_ActivateLayoutGadget__bn - 36))(_ActivateLayoutGadget__bn, _ActivateLayoutGadget_gadget, _ActivateLayoutGadget_window, _ActivateLayoutGadget_requester, _ActivateLayoutGadget_object); \
});})

#define FlushLayoutDomainCache(gadget) ({ \
  struct Gadget * _FlushLayoutDomainCache_gadget = (gadget); \
  ({ \
  register char * _FlushLayoutDomainCache__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"))) \
  (_FlushLayoutDomainCache__bn - 42))(_FlushLayoutDomainCache__bn, _FlushLayoutDomainCache_gadget); \
});})

#define RethinkLayout(gadget, window, requester, refresh) ({ \
  struct Gadget * _RethinkLayout_gadget = (gadget); \
  struct Window * _RethinkLayout_window = (window); \
  struct Requester * _RethinkLayout_requester = (requester); \
  LONG _RethinkLayout_refresh = (refresh); \
  ({ \
  register char * _RethinkLayout__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), LONG __asm("d0"))) \
  (_RethinkLayout__bn - 48))(_RethinkLayout__bn, _RethinkLayout_gadget, _RethinkLayout_window, _RethinkLayout_requester, _RethinkLayout_refresh); \
});})

#define LayoutLimits(gadget, limits, font, screen) ({ \
  struct Gadget * _LayoutLimits_gadget = (gadget); \
  struct LayoutLimits * _LayoutLimits_limits = (limits); \
  struct TextFont * _LayoutLimits_font = (font); \
  struct Screen * _LayoutLimits_screen = (screen); \
  ({ \
  register char * _LayoutLimits__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct LayoutLimits * __asm("a1"), struct TextFont * __asm("a2"), struct Screen * __asm("a3"))) \
  (_LayoutLimits__bn - 54))(_LayoutLimits__bn, _LayoutLimits_gadget, _LayoutLimits_limits, _LayoutLimits_font, _LayoutLimits_screen); \
});})

#define PAGE_GetClass() ({ \
  register char * _PAGE_GetClass__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_PAGE_GetClass__bn - 60))(_PAGE_GetClass__bn); \
})

#define SetPageGadgetAttrsA(gadget, object, window, requester, tags) ({ \
  struct Gadget * _SetPageGadgetAttrsA_gadget = (gadget); \
  Object * _SetPageGadgetAttrsA_object = (object); \
  struct Window * _SetPageGadgetAttrsA_window = (window); \
  struct Requester * _SetPageGadgetAttrsA_requester = (requester); \
  struct TagItem * _SetPageGadgetAttrsA_tags = (tags); \
  ({ \
  register char * _SetPageGadgetAttrsA__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Gadget * __asm("a0"), Object * __asm("a1"), struct Window * __asm("a2"), struct Requester * __asm("a3"), struct TagItem * __asm("a4"))) \
  (_SetPageGadgetAttrsA__bn - 66))(_SetPageGadgetAttrsA__bn, _SetPageGadgetAttrsA_gadget, _SetPageGadgetAttrsA_object, _SetPageGadgetAttrsA_window, _SetPageGadgetAttrsA_requester, _SetPageGadgetAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___SetPageGadgetAttrs(struct Library * LayoutBase, struct Gadget * gadget, Object * object, struct Window * window, struct Requester * requester, ...)
{
  return SetPageGadgetAttrsA(gadget, object, window, requester, (struct TagItem *) ((ULONG) &requester + sizeof(struct Requester *)));
}

#define SetPageGadgetAttrs(gadget, object, window, requester...) ___SetPageGadgetAttrs(LAYOUT_BASE_NAME, gadget, object, window, requester)
#endif

#define RefreshPageGadget(gadget, object, window, requester) ({ \
  struct Gadget * _RefreshPageGadget_gadget = (gadget); \
  Object * _RefreshPageGadget_object = (object); \
  struct Window * _RefreshPageGadget_window = (window); \
  struct Requester * _RefreshPageGadget_requester = (requester); \
  ({ \
  register char * _RefreshPageGadget__bn __asm("a6") = (char *) (LAYOUT_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), Object * __asm("a1"), struct Window * __asm("a2"), struct Requester * __asm("a3"))) \
  (_RefreshPageGadget__bn - 72))(_RefreshPageGadget__bn, _RefreshPageGadget_gadget, _RefreshPageGadget_object, _RefreshPageGadget_window, _RefreshPageGadget_requester); \
});})

#endif /*  _INLINE_LAYOUT_H  */
