#ifndef _INLINE_GADTOOLS_H
#define _INLINE_GADTOOLS_H

#ifndef CLIB_GADTOOLS_PROTOS_H
#define CLIB_GADTOOLS_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  LIBRARIES_GADTOOLS_H
#include <libraries/gadtools.h>
#endif

#ifndef GADTOOLS_BASE_NAME
#define GADTOOLS_BASE_NAME GadToolsBase
#endif

#define CreateGadgetA(kind, gad, ng, taglist) ({ \
  ULONG _CreateGadgetA_kind = (kind); \
  struct Gadget * _CreateGadgetA_gad = (gad); \
  struct NewGadget * _CreateGadgetA_ng = (ng); \
  CONST struct TagItem * _CreateGadgetA_taglist = (taglist); \
  ({ \
  register char * _CreateGadgetA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((struct Gadget * (*)(char * __asm("a6"), ULONG __asm("d0"), struct Gadget * __asm("a0"), struct NewGadget * __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_CreateGadgetA__bn - 30))(_CreateGadgetA__bn, _CreateGadgetA_kind, _CreateGadgetA_gad, _CreateGadgetA_ng, _CreateGadgetA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Gadget * ___CreateGadget(struct Library * GadToolsBase, ULONG kind, struct Gadget * gad, struct NewGadget * ng, ...)
{
  return CreateGadgetA(kind, gad, ng, (CONST struct TagItem *) ((ULONG) &ng + sizeof(struct NewGadget *)));
}

#define CreateGadget(kind, gad, ng...) ___CreateGadget(GADTOOLS_BASE_NAME, kind, gad, ng)
#endif

#define FreeGadgets(gad) ({ \
  struct Gadget * _FreeGadgets_gad = (gad); \
  ({ \
  register char * _FreeGadgets__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"))) \
  (_FreeGadgets__bn - 36))(_FreeGadgets__bn, _FreeGadgets_gad); \
});})

#define GT_SetGadgetAttrsA(gad, win, req, taglist) ({ \
  struct Gadget * _GT_SetGadgetAttrsA_gad = (gad); \
  struct Window * _GT_SetGadgetAttrsA_win = (win); \
  struct Requester * _GT_SetGadgetAttrsA_req = (req); \
  CONST struct TagItem * _GT_SetGadgetAttrsA_taglist = (taglist); \
  ({ \
  register char * _GT_SetGadgetAttrsA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), CONST struct TagItem * __asm("a3"))) \
  (_GT_SetGadgetAttrsA__bn - 42))(_GT_SetGadgetAttrsA__bn, _GT_SetGadgetAttrsA_gad, _GT_SetGadgetAttrsA_win, _GT_SetGadgetAttrsA_req, _GT_SetGadgetAttrsA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___GT_SetGadgetAttrs(struct Library * GadToolsBase, struct Gadget * gad, struct Window * win, struct Requester * req, ...)
{
  GT_SetGadgetAttrsA(gad, win, req, (CONST struct TagItem *) ((ULONG) &req + sizeof(struct Requester *)));
}

#define GT_SetGadgetAttrs(gad, win, req...) ___GT_SetGadgetAttrs(GADTOOLS_BASE_NAME, gad, win, req)
#endif

#define CreateMenusA(newmenu, taglist) ({ \
  CONST struct NewMenu * _CreateMenusA_newmenu = (newmenu); \
  struct TagItem * _CreateMenusA_taglist = (taglist); \
  ({ \
  register char * _CreateMenusA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((struct Menu * (*)(char * __asm("a6"), CONST struct NewMenu * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_CreateMenusA__bn - 48))(_CreateMenusA__bn, _CreateMenusA_newmenu, _CreateMenusA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Menu * ___CreateMenus(struct Library * GadToolsBase, CONST struct NewMenu * newmenu, ...)
{
  return CreateMenusA(newmenu, (struct TagItem *) ((ULONG) &newmenu + sizeof(CONST struct NewMenu *)));
}

#define CreateMenus(newmenu...) ___CreateMenus(GADTOOLS_BASE_NAME, newmenu)
#endif

#define FreeMenus(menu) ({ \
  struct Menu * _FreeMenus_menu = (menu); \
  ({ \
  register char * _FreeMenus__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Menu * __asm("a0"))) \
  (_FreeMenus__bn - 54))(_FreeMenus__bn, _FreeMenus_menu); \
});})

#define LayoutMenuItemsA(firstitem, vi, taglist) ({ \
  struct MenuItem * _LayoutMenuItemsA_firstitem = (firstitem); \
  APTR _LayoutMenuItemsA_vi = (vi); \
  CONST struct TagItem * _LayoutMenuItemsA_taglist = (taglist); \
  ({ \
  register char * _LayoutMenuItemsA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct MenuItem * __asm("a0"), APTR __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_LayoutMenuItemsA__bn - 60))(_LayoutMenuItemsA__bn, _LayoutMenuItemsA_firstitem, _LayoutMenuItemsA_vi, _LayoutMenuItemsA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___LayoutMenuItems(struct Library * GadToolsBase, struct MenuItem * firstitem, APTR vi, ...)
{
  return LayoutMenuItemsA(firstitem, vi, (CONST struct TagItem *) ((ULONG) &vi + sizeof(APTR)));
}

#define LayoutMenuItems(firstitem, vi...) ___LayoutMenuItems(GADTOOLS_BASE_NAME, firstitem, vi)
#endif

#define LayoutMenusA(firstmenu, vi, taglist) ({ \
  struct Menu * _LayoutMenusA_firstmenu = (firstmenu); \
  APTR _LayoutMenusA_vi = (vi); \
  CONST struct TagItem * _LayoutMenusA_taglist = (taglist); \
  ({ \
  register char * _LayoutMenusA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Menu * __asm("a0"), APTR __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_LayoutMenusA__bn - 66))(_LayoutMenusA__bn, _LayoutMenusA_firstmenu, _LayoutMenusA_vi, _LayoutMenusA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___LayoutMenus(struct Library * GadToolsBase, struct Menu * firstmenu, APTR vi, ...)
{
  return LayoutMenusA(firstmenu, vi, (CONST struct TagItem *) ((ULONG) &vi + sizeof(APTR)));
}

#define LayoutMenus(firstmenu, vi...) ___LayoutMenus(GADTOOLS_BASE_NAME, firstmenu, vi)
#endif

#define GT_GetIMsg(iport) ({ \
  struct MsgPort * _GT_GetIMsg_iport = (iport); \
  ({ \
  register char * _GT_GetIMsg__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((struct IntuiMessage * (*)(char * __asm("a6"), struct MsgPort * __asm("a0"))) \
  (_GT_GetIMsg__bn - 72))(_GT_GetIMsg__bn, _GT_GetIMsg_iport); \
});})

#define GT_ReplyIMsg(imsg) ({ \
  struct IntuiMessage * _GT_ReplyIMsg_imsg = (imsg); \
  ({ \
  register char * _GT_ReplyIMsg__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IntuiMessage * __asm("a1"))) \
  (_GT_ReplyIMsg__bn - 78))(_GT_ReplyIMsg__bn, _GT_ReplyIMsg_imsg); \
});})

#define GT_RefreshWindow(win, req) ({ \
  struct Window * _GT_RefreshWindow_win = (win); \
  struct Requester * _GT_RefreshWindow_req = (req); \
  ({ \
  register char * _GT_RefreshWindow__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Requester * __asm("a1"))) \
  (_GT_RefreshWindow__bn - 84))(_GT_RefreshWindow__bn, _GT_RefreshWindow_win, _GT_RefreshWindow_req); \
});})

#define GT_BeginRefresh(win) ({ \
  struct Window * _GT_BeginRefresh_win = (win); \
  ({ \
  register char * _GT_BeginRefresh__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_GT_BeginRefresh__bn - 90))(_GT_BeginRefresh__bn, _GT_BeginRefresh_win); \
});})

#define GT_EndRefresh(win, complete) ({ \
  struct Window * _GT_EndRefresh_win = (win); \
  LONG _GT_EndRefresh_complete = (complete); \
  ({ \
  register char * _GT_EndRefresh__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), LONG __asm("d0"))) \
  (_GT_EndRefresh__bn - 96))(_GT_EndRefresh__bn, _GT_EndRefresh_win, _GT_EndRefresh_complete); \
});})

#define GT_FilterIMsg(imsg) ({ \
  CONST struct IntuiMessage * _GT_FilterIMsg_imsg = (imsg); \
  ({ \
  register char * _GT_FilterIMsg__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((struct IntuiMessage * (*)(char * __asm("a6"), CONST struct IntuiMessage * __asm("a1"))) \
  (_GT_FilterIMsg__bn - 102))(_GT_FilterIMsg__bn, _GT_FilterIMsg_imsg); \
});})

#define GT_PostFilterIMsg(imsg) ({ \
  struct IntuiMessage * _GT_PostFilterIMsg_imsg = (imsg); \
  ({ \
  register char * _GT_PostFilterIMsg__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((struct IntuiMessage * (*)(char * __asm("a6"), struct IntuiMessage * __asm("a1"))) \
  (_GT_PostFilterIMsg__bn - 108))(_GT_PostFilterIMsg__bn, _GT_PostFilterIMsg_imsg); \
});})

#define CreateContext(glistptr) ({ \
  struct Gadget ** _CreateContext_glistptr = (glistptr); \
  ({ \
  register char * _CreateContext__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((struct Gadget * (*)(char * __asm("a6"), struct Gadget ** __asm("a0"))) \
  (_CreateContext__bn - 114))(_CreateContext__bn, _CreateContext_glistptr); \
});})

#define DrawBevelBoxA(rport, left, top, width, height, taglist) ({ \
  struct RastPort * _DrawBevelBoxA_rport = (rport); \
  LONG _DrawBevelBoxA_left = (left); \
  LONG _DrawBevelBoxA_top = (top); \
  LONG _DrawBevelBoxA_width = (width); \
  LONG _DrawBevelBoxA_height = (height); \
  CONST struct TagItem * _DrawBevelBoxA_taglist = (taglist); \
  ({ \
  register char * _DrawBevelBoxA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), CONST struct TagItem * __asm("a1"))) \
  (_DrawBevelBoxA__bn - 120))(_DrawBevelBoxA__bn, _DrawBevelBoxA_rport, _DrawBevelBoxA_left, _DrawBevelBoxA_top, _DrawBevelBoxA_width, _DrawBevelBoxA_height, _DrawBevelBoxA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___DrawBevelBox(struct Library * GadToolsBase, struct RastPort * rport, LONG left, LONG top, LONG width, LONG height, ...)
{
  DrawBevelBoxA(rport, left, top, width, height, (CONST struct TagItem *) ((ULONG) &height + sizeof(LONG)));
}

#define DrawBevelBox(rport, left, top, width, height...) ___DrawBevelBox(GADTOOLS_BASE_NAME, rport, left, top, width, height)
#endif

#define GetVisualInfoA(screen, taglist) ({ \
  struct Screen * _GetVisualInfoA_screen = (screen); \
  CONST struct TagItem * _GetVisualInfoA_taglist = (taglist); \
  ({ \
  register char * _GetVisualInfoA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct Screen * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_GetVisualInfoA__bn - 126))(_GetVisualInfoA__bn, _GetVisualInfoA_screen, _GetVisualInfoA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___GetVisualInfo(struct Library * GadToolsBase, struct Screen * screen, ...)
{
  return GetVisualInfoA(screen, (CONST struct TagItem *) ((ULONG) &screen + sizeof(struct Screen *)));
}

#define GetVisualInfo(screen...) ___GetVisualInfo(GADTOOLS_BASE_NAME, screen)
#endif

#define FreeVisualInfo(vi) ({ \
  APTR _FreeVisualInfo_vi = (vi); \
  ({ \
  register char * _FreeVisualInfo__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_FreeVisualInfo__bn - 132))(_FreeVisualInfo__bn, _FreeVisualInfo_vi); \
});})

#define SetDesignFontA(vi, tattr, tags) ({ \
  APTR _SetDesignFontA_vi = (vi); \
  struct TextAttr * _SetDesignFontA_tattr = (tattr); \
  CONST struct TagItem * _SetDesignFontA_tags = (tags); \
  ({ \
  register char * _SetDesignFontA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"), struct TextAttr * __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_SetDesignFontA__bn - 138))(_SetDesignFontA__bn, _SetDesignFontA_vi, _SetDesignFontA_tattr, _SetDesignFontA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___SetDesignFont(struct Library * GadToolsBase, APTR vi, struct TextAttr * tattr, ...)
{
  return SetDesignFontA(vi, tattr, (CONST struct TagItem *) ((ULONG) &tattr + sizeof(struct TextAttr *)));
}

#define SetDesignFont(vi, tattr...) ___SetDesignFont(GADTOOLS_BASE_NAME, vi, tattr)
#endif

#define ScaleGadgetRectA(ng, tags) ({ \
  struct NewGadget * _ScaleGadgetRectA_ng = (ng); \
  CONST struct TagItem * _ScaleGadgetRectA_tags = (tags); \
  ({ \
  register char * _ScaleGadgetRectA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct NewGadget * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_ScaleGadgetRectA__bn - 144))(_ScaleGadgetRectA__bn, _ScaleGadgetRectA_ng, _ScaleGadgetRectA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___ScaleGadgetRect(struct Library * GadToolsBase, struct NewGadget * ng, ...)
{
  return ScaleGadgetRectA(ng, (CONST struct TagItem *) ((ULONG) &ng + sizeof(struct NewGadget *)));
}

#define ScaleGadgetRect(ng...) ___ScaleGadgetRect(GADTOOLS_BASE_NAME, ng)
#endif

#define GT_GetGadgetAttrsA(gad, win, req, taglist) ({ \
  struct Gadget * _GT_GetGadgetAttrsA_gad = (gad); \
  struct Window * _GT_GetGadgetAttrsA_win = (win); \
  struct Requester * _GT_GetGadgetAttrsA_req = (req); \
  CONST struct TagItem * _GT_GetGadgetAttrsA_taglist = (taglist); \
  ({ \
  register char * _GT_GetGadgetAttrsA__bn __asm("a6") = (char *) (GADTOOLS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), CONST struct TagItem * __asm("a3"))) \
  (_GT_GetGadgetAttrsA__bn - 174))(_GT_GetGadgetAttrsA__bn, _GT_GetGadgetAttrsA_gad, _GT_GetGadgetAttrsA_win, _GT_GetGadgetAttrsA_req, _GT_GetGadgetAttrsA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___GT_GetGadgetAttrs(struct Library * GadToolsBase, struct Gadget * gad, struct Window * win, struct Requester * req, ...)
{
  return GT_GetGadgetAttrsA(gad, win, req, (CONST struct TagItem *) ((ULONG) &req + sizeof(struct Requester *)));
}

#define GT_GetGadgetAttrs(gad, win, req...) ___GT_GetGadgetAttrs(GADTOOLS_BASE_NAME, gad, win, req)
#endif

#endif /*  _INLINE_GADTOOLS_H  */
