#ifndef _INLINE_INTUITION_H
#define _INLINE_INTUITION_H

#ifndef CLIB_INTUITION_PROTOS_H
#define CLIB_INTUITION_PROTOS_H
#endif

#ifndef  INTUITION_INTUITIONBASE_H
#include <intuition/intuitionbase.h>
#endif
#ifndef  INTUITION_CLASSES_H
#include <intuition/classes.h>
#endif
#ifndef  INTUITION_CGHOOKS_H
#include <intuition/cghooks.h>
#endif
#ifndef  INTUITION_CLASSUSR_H
#include <intuition/classusr.h>
#endif

#ifndef INTUITION_BASE_NAME
#define INTUITION_BASE_NAME IntuitionBase
#endif

#define OpenIntuition() ({ \
  register char * _OpenIntuition__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_OpenIntuition__bn - 30))(_OpenIntuition__bn); \
})

#define Intuition(iEvent) ({ \
  struct InputEvent * _Intuition_iEvent = (iEvent); \
  ({ \
  register char * _Intuition__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct InputEvent * __asm("a0"))) \
  (_Intuition__bn - 36))(_Intuition__bn, _Intuition_iEvent); \
});})

#define AddGadget(window, gadget, position) ({ \
  struct Window * _AddGadget_window = (window); \
  struct Gadget * _AddGadget_gadget = (gadget); \
  ULONG _AddGadget_position = (position); \
  ({ \
  register char * _AddGadget__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Gadget * __asm("a1"), ULONG __asm("d0"))) \
  (_AddGadget__bn - 42))(_AddGadget__bn, _AddGadget_window, _AddGadget_gadget, _AddGadget_position); \
});})

#define ClearDMRequest(window) ({ \
  struct Window * _ClearDMRequest_window = (window); \
  ({ \
  register char * _ClearDMRequest__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_ClearDMRequest__bn - 48))(_ClearDMRequest__bn, _ClearDMRequest_window); \
});})

#define ClearMenuStrip(window) ({ \
  struct Window * _ClearMenuStrip_window = (window); \
  ({ \
  register char * _ClearMenuStrip__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_ClearMenuStrip__bn - 54))(_ClearMenuStrip__bn, _ClearMenuStrip_window); \
});})

#define ClearPointer(window) ({ \
  struct Window * _ClearPointer_window = (window); \
  ({ \
  register char * _ClearPointer__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_ClearPointer__bn - 60))(_ClearPointer__bn, _ClearPointer_window); \
});})

#define CloseScreen(screen) ({ \
  struct Screen * _CloseScreen_screen = (screen); \
  ({ \
  register char * _CloseScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Screen * __asm("a0"))) \
  (_CloseScreen__bn - 66))(_CloseScreen__bn, _CloseScreen_screen); \
});})

#define CloseWindow(window) ({ \
  struct Window * _CloseWindow_window = (window); \
  ({ \
  register char * _CloseWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_CloseWindow__bn - 72))(_CloseWindow__bn, _CloseWindow_window); \
});})

#define CloseWorkBench() ({ \
  register char * _CloseWorkBench__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"))) \
  (_CloseWorkBench__bn - 78))(_CloseWorkBench__bn); \
})

#define CurrentTime(seconds, micros) ({ \
  ULONG * _CurrentTime_seconds = (seconds); \
  ULONG * _CurrentTime_micros = (micros); \
  ({ \
  register char * _CurrentTime__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG * __asm("a0"), ULONG * __asm("a1"))) \
  (_CurrentTime__bn - 84))(_CurrentTime__bn, _CurrentTime_seconds, _CurrentTime_micros); \
});})

#define DisplayAlert(alertNumber, string, height) ({ \
  ULONG _DisplayAlert_alertNumber = (alertNumber); \
  CONST_STRPTR _DisplayAlert_string = (string); \
  ULONG _DisplayAlert_height = (height); \
  ({ \
  register char * _DisplayAlert__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), ULONG __asm("d0"), CONST_STRPTR __asm("a0"), ULONG __asm("d1"))) \
  (_DisplayAlert__bn - 90))(_DisplayAlert__bn, _DisplayAlert_alertNumber, _DisplayAlert_string, _DisplayAlert_height); \
});})

#define DisplayBeep(screen) ({ \
  struct Screen * _DisplayBeep_screen = (screen); \
  ({ \
  register char * _DisplayBeep__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"))) \
  (_DisplayBeep__bn - 96))(_DisplayBeep__bn, _DisplayBeep_screen); \
});})

#define DoubleClick(sSeconds, sMicros, cSeconds, cMicros) ({ \
  ULONG _DoubleClick_sSeconds = (sSeconds); \
  ULONG _DoubleClick_sMicros = (sMicros); \
  ULONG _DoubleClick_cSeconds = (cSeconds); \
  ULONG _DoubleClick_cMicros = (cMicros); \
  ({ \
  register char * _DoubleClick__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_DoubleClick__bn - 102))(_DoubleClick__bn, _DoubleClick_sSeconds, _DoubleClick_sMicros, _DoubleClick_cSeconds, _DoubleClick_cMicros); \
});})

#define DrawBorder(rp, border, leftOffset, topOffset) ({ \
  struct RastPort * _DrawBorder_rp = (rp); \
  CONST struct Border * _DrawBorder_border = (border); \
  LONG _DrawBorder_leftOffset = (leftOffset); \
  LONG _DrawBorder_topOffset = (topOffset); \
  ({ \
  register char * _DrawBorder__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct Border * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_DrawBorder__bn - 108))(_DrawBorder__bn, _DrawBorder_rp, _DrawBorder_border, _DrawBorder_leftOffset, _DrawBorder_topOffset); \
});})

#define DrawImage(rp, image, leftOffset, topOffset) ({ \
  struct RastPort * _DrawImage_rp = (rp); \
  CONST struct Image * _DrawImage_image = (image); \
  LONG _DrawImage_leftOffset = (leftOffset); \
  LONG _DrawImage_topOffset = (topOffset); \
  ({ \
  register char * _DrawImage__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct Image * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_DrawImage__bn - 114))(_DrawImage__bn, _DrawImage_rp, _DrawImage_image, _DrawImage_leftOffset, _DrawImage_topOffset); \
});})

#define EndRequest(requester, window) ({ \
  struct Requester * _EndRequest_requester = (requester); \
  struct Window * _EndRequest_window = (window); \
  ({ \
  register char * _EndRequest__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Requester * __asm("a0"), struct Window * __asm("a1"))) \
  (_EndRequest__bn - 120))(_EndRequest__bn, _EndRequest_requester, _EndRequest_window); \
});})

#define GetDefPrefs(preferences, size) ({ \
  struct Preferences * _GetDefPrefs_preferences = (preferences); \
  LONG _GetDefPrefs_size = (size); \
  ({ \
  register char * _GetDefPrefs__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Preferences * (*)(char * __asm("a6"), struct Preferences * __asm("a0"), LONG __asm("d0"))) \
  (_GetDefPrefs__bn - 126))(_GetDefPrefs__bn, _GetDefPrefs_preferences, _GetDefPrefs_size); \
});})

#define GetPrefs(preferences, size) ({ \
  struct Preferences * _GetPrefs_preferences = (preferences); \
  LONG _GetPrefs_size = (size); \
  ({ \
  register char * _GetPrefs__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Preferences * (*)(char * __asm("a6"), struct Preferences * __asm("a0"), LONG __asm("d0"))) \
  (_GetPrefs__bn - 132))(_GetPrefs__bn, _GetPrefs_preferences, _GetPrefs_size); \
});})

#define InitRequester(requester) ({ \
  struct Requester * _InitRequester_requester = (requester); \
  ({ \
  register char * _InitRequester__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Requester * __asm("a0"))) \
  (_InitRequester__bn - 138))(_InitRequester__bn, _InitRequester_requester); \
});})

#define ItemAddress(menuStrip, menuNumber) ({ \
  CONST struct Menu * _ItemAddress_menuStrip = (menuStrip); \
  ULONG _ItemAddress_menuNumber = (menuNumber); \
  ({ \
  register char * _ItemAddress__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct MenuItem * (*)(char * __asm("a6"), CONST struct Menu * __asm("a0"), ULONG __asm("d0"))) \
  (_ItemAddress__bn - 144))(_ItemAddress__bn, _ItemAddress_menuStrip, _ItemAddress_menuNumber); \
});})

#define ModifyIDCMP(window, flags) ({ \
  struct Window * _ModifyIDCMP_window = (window); \
  ULONG _ModifyIDCMP_flags = (flags); \
  ({ \
  register char * _ModifyIDCMP__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"), ULONG __asm("d0"))) \
  (_ModifyIDCMP__bn - 150))(_ModifyIDCMP__bn, _ModifyIDCMP_window, _ModifyIDCMP_flags); \
});})

#define ModifyProp(gadget, window, requester, flags, horizPot, vertPot, horizBody, vertBody) ({ \
  struct Gadget * _ModifyProp_gadget = (gadget); \
  struct Window * _ModifyProp_window = (window); \
  struct Requester * _ModifyProp_requester = (requester); \
  ULONG _ModifyProp_flags = (flags); \
  ULONG _ModifyProp_horizPot = (horizPot); \
  ULONG _ModifyProp_vertPot = (vertPot); \
  ULONG _ModifyProp_horizBody = (horizBody); \
  ULONG _ModifyProp_vertBody = (vertBody); \
  ({ \
  register char * _ModifyProp__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), ULONG __asm("d4"))) \
  (_ModifyProp__bn - 156))(_ModifyProp__bn, _ModifyProp_gadget, _ModifyProp_window, _ModifyProp_requester, _ModifyProp_flags, _ModifyProp_horizPot, _ModifyProp_vertPot, _ModifyProp_horizBody, _ModifyProp_vertBody); \
});})

#define MoveScreen(screen, dx, dy) ({ \
  struct Screen * _MoveScreen_screen = (screen); \
  LONG _MoveScreen_dx = (dx); \
  LONG _MoveScreen_dy = (dy); \
  ({ \
  register char * _MoveScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_MoveScreen__bn - 162))(_MoveScreen__bn, _MoveScreen_screen, _MoveScreen_dx, _MoveScreen_dy); \
});})

#define MoveWindow(window, dx, dy) ({ \
  struct Window * _MoveWindow_window = (window); \
  LONG _MoveWindow_dx = (dx); \
  LONG _MoveWindow_dy = (dy); \
  ({ \
  register char * _MoveWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_MoveWindow__bn - 168))(_MoveWindow__bn, _MoveWindow_window, _MoveWindow_dx, _MoveWindow_dy); \
});})

#define OffGadget(gadget, window, requester) ({ \
  struct Gadget * _OffGadget_gadget = (gadget); \
  struct Window * _OffGadget_window = (window); \
  struct Requester * _OffGadget_requester = (requester); \
  ({ \
  register char * _OffGadget__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"))) \
  (_OffGadget__bn - 174))(_OffGadget__bn, _OffGadget_gadget, _OffGadget_window, _OffGadget_requester); \
});})

#define OffMenu(window, menuNumber) ({ \
  struct Window * _OffMenu_window = (window); \
  ULONG _OffMenu_menuNumber = (menuNumber); \
  ({ \
  register char * _OffMenu__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), ULONG __asm("d0"))) \
  (_OffMenu__bn - 180))(_OffMenu__bn, _OffMenu_window, _OffMenu_menuNumber); \
});})

#define OnGadget(gadget, window, requester) ({ \
  struct Gadget * _OnGadget_gadget = (gadget); \
  struct Window * _OnGadget_window = (window); \
  struct Requester * _OnGadget_requester = (requester); \
  ({ \
  register char * _OnGadget__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"))) \
  (_OnGadget__bn - 186))(_OnGadget__bn, _OnGadget_gadget, _OnGadget_window, _OnGadget_requester); \
});})

#define OnMenu(window, menuNumber) ({ \
  struct Window * _OnMenu_window = (window); \
  ULONG _OnMenu_menuNumber = (menuNumber); \
  ({ \
  register char * _OnMenu__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), ULONG __asm("d0"))) \
  (_OnMenu__bn - 192))(_OnMenu__bn, _OnMenu_window, _OnMenu_menuNumber); \
});})

#define OpenScreen(newScreen) ({ \
  CONST struct NewScreen * _OpenScreen_newScreen = (newScreen); \
  ({ \
  register char * _OpenScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Screen * (*)(char * __asm("a6"), CONST struct NewScreen * __asm("a0"))) \
  (_OpenScreen__bn - 198))(_OpenScreen__bn, _OpenScreen_newScreen); \
});})

#define OpenWindow(newWindow) ({ \
  CONST struct NewWindow * _OpenWindow_newWindow = (newWindow); \
  ({ \
  register char * _OpenWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Window * (*)(char * __asm("a6"), CONST struct NewWindow * __asm("a0"))) \
  (_OpenWindow__bn - 204))(_OpenWindow__bn, _OpenWindow_newWindow); \
});})

#define OpenWorkBench() ({ \
  register char * _OpenWorkBench__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_OpenWorkBench__bn - 210))(_OpenWorkBench__bn); \
})

#define PrintIText(rp, iText, left, top) ({ \
  struct RastPort * _PrintIText_rp = (rp); \
  CONST struct IntuiText * _PrintIText_iText = (iText); \
  LONG _PrintIText_left = (left); \
  LONG _PrintIText_top = (top); \
  ({ \
  register char * _PrintIText__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct IntuiText * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_PrintIText__bn - 216))(_PrintIText__bn, _PrintIText_rp, _PrintIText_iText, _PrintIText_left, _PrintIText_top); \
});})

#define RefreshGadgets(gadgets, window, requester) ({ \
  struct Gadget * _RefreshGadgets_gadgets = (gadgets); \
  struct Window * _RefreshGadgets_window = (window); \
  struct Requester * _RefreshGadgets_requester = (requester); \
  ({ \
  register char * _RefreshGadgets__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"))) \
  (_RefreshGadgets__bn - 222))(_RefreshGadgets__bn, _RefreshGadgets_gadgets, _RefreshGadgets_window, _RefreshGadgets_requester); \
});})

#define RemoveGadget(window, gadget) ({ \
  struct Window * _RemoveGadget_window = (window); \
  struct Gadget * _RemoveGadget_gadget = (gadget); \
  ({ \
  register char * _RemoveGadget__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Gadget * __asm("a1"))) \
  (_RemoveGadget__bn - 228))(_RemoveGadget__bn, _RemoveGadget_window, _RemoveGadget_gadget); \
});})

#define ReportMouse(flag, window) ({ \
  LONG _ReportMouse_flag = (flag); \
  struct Window * _ReportMouse_window = (window); \
  ({ \
  register char * _ReportMouse__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"), struct Window * __asm("a0"))) \
  (_ReportMouse__bn - 234))(_ReportMouse__bn, _ReportMouse_flag, _ReportMouse_window); \
});})

#define ReportMouse1(flag, window) ReportMouse((flag), (window))

#define Request(requester, window) ({ \
  struct Requester * _Request_requester = (requester); \
  struct Window * _Request_window = (window); \
  ({ \
  register char * _Request__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Requester * __asm("a0"), struct Window * __asm("a1"))) \
  (_Request__bn - 240))(_Request__bn, _Request_requester, _Request_window); \
});})

#define ScreenToBack(screen) ({ \
  struct Screen * _ScreenToBack_screen = (screen); \
  ({ \
  register char * _ScreenToBack__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"))) \
  (_ScreenToBack__bn - 246))(_ScreenToBack__bn, _ScreenToBack_screen); \
});})

#define ScreenToFront(screen) ({ \
  struct Screen * _ScreenToFront_screen = (screen); \
  ({ \
  register char * _ScreenToFront__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"))) \
  (_ScreenToFront__bn - 252))(_ScreenToFront__bn, _ScreenToFront_screen); \
});})

#define SetDMRequest(window, requester) ({ \
  struct Window * _SetDMRequest_window = (window); \
  struct Requester * _SetDMRequest_requester = (requester); \
  ({ \
  register char * _SetDMRequest__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Requester * __asm("a1"))) \
  (_SetDMRequest__bn - 258))(_SetDMRequest__bn, _SetDMRequest_window, _SetDMRequest_requester); \
});})

#define SetMenuStrip(window, menu) ({ \
  struct Window * _SetMenuStrip_window = (window); \
  struct Menu * _SetMenuStrip_menu = (menu); \
  ({ \
  register char * _SetMenuStrip__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Menu * __asm("a1"))) \
  (_SetMenuStrip__bn - 264))(_SetMenuStrip__bn, _SetMenuStrip_window, _SetMenuStrip_menu); \
});})

#define SetPointer(window, pointer, height, width, xOffset, yOffset) ({ \
  struct Window * _SetPointer_window = (window); \
  UWORD * _SetPointer_pointer = (pointer); \
  LONG _SetPointer_height = (height); \
  LONG _SetPointer_width = (width); \
  LONG _SetPointer_xOffset = (xOffset); \
  LONG _SetPointer_yOffset = (yOffset); \
  ({ \
  register char * _SetPointer__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), UWORD * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_SetPointer__bn - 270))(_SetPointer__bn, _SetPointer_window, _SetPointer_pointer, _SetPointer_height, _SetPointer_width, _SetPointer_xOffset, _SetPointer_yOffset); \
});})

#define SetWindowTitles(window, windowTitle, screenTitle) ({ \
  struct Window * _SetWindowTitles_window = (window); \
  CONST_STRPTR _SetWindowTitles_windowTitle = (windowTitle); \
  CONST_STRPTR _SetWindowTitles_screenTitle = (screenTitle); \
  ({ \
  register char * _SetWindowTitles__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), CONST_STRPTR __asm("a1"), CONST_STRPTR __asm("a2"))) \
  (_SetWindowTitles__bn - 276))(_SetWindowTitles__bn, _SetWindowTitles_window, _SetWindowTitles_windowTitle, _SetWindowTitles_screenTitle); \
});})

#define ShowTitle(screen, showIt) ({ \
  struct Screen * _ShowTitle_screen = (screen); \
  LONG _ShowTitle_showIt = (showIt); \
  ({ \
  register char * _ShowTitle__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"), LONG __asm("d0"))) \
  (_ShowTitle__bn - 282))(_ShowTitle__bn, _ShowTitle_screen, _ShowTitle_showIt); \
});})

#define SizeWindow(window, dx, dy) ({ \
  struct Window * _SizeWindow_window = (window); \
  LONG _SizeWindow_dx = (dx); \
  LONG _SizeWindow_dy = (dy); \
  ({ \
  register char * _SizeWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_SizeWindow__bn - 288))(_SizeWindow__bn, _SizeWindow_window, _SizeWindow_dx, _SizeWindow_dy); \
});})

#define ViewAddress() ({ \
  register char * _ViewAddress__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct View * (*)(char * __asm("a6"))) \
  (_ViewAddress__bn - 294))(_ViewAddress__bn); \
})

#define ViewPortAddress(window) ({ \
  CONST struct Window * _ViewPortAddress_window = (window); \
  ({ \
  register char * _ViewPortAddress__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct ViewPort * (*)(char * __asm("a6"), CONST struct Window * __asm("a0"))) \
  (_ViewPortAddress__bn - 300))(_ViewPortAddress__bn, _ViewPortAddress_window); \
});})

#define WindowToBack(window) ({ \
  struct Window * _WindowToBack_window = (window); \
  ({ \
  register char * _WindowToBack__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_WindowToBack__bn - 306))(_WindowToBack__bn, _WindowToBack_window); \
});})

#define WindowToFront(window) ({ \
  struct Window * _WindowToFront_window = (window); \
  ({ \
  register char * _WindowToFront__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_WindowToFront__bn - 312))(_WindowToFront__bn, _WindowToFront_window); \
});})

#define WindowLimits(window, widthMin, heightMin, widthMax, heightMax) ({ \
  struct Window * _WindowLimits_window = (window); \
  LONG _WindowLimits_widthMin = (widthMin); \
  LONG _WindowLimits_heightMin = (heightMin); \
  ULONG _WindowLimits_widthMax = (widthMax); \
  ULONG _WindowLimits_heightMax = (heightMax); \
  ({ \
  register char * _WindowLimits__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_WindowLimits__bn - 318))(_WindowLimits__bn, _WindowLimits_window, _WindowLimits_widthMin, _WindowLimits_heightMin, _WindowLimits_widthMax, _WindowLimits_heightMax); \
});})

#define SetPrefs(preferences, size, inform) ({ \
  CONST struct Preferences * _SetPrefs_preferences = (preferences); \
  LONG _SetPrefs_size = (size); \
  LONG _SetPrefs_inform = (inform); \
  ({ \
  register char * _SetPrefs__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Preferences  * (*)(char * __asm("a6"), CONST struct Preferences * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_SetPrefs__bn - 324))(_SetPrefs__bn, _SetPrefs_preferences, _SetPrefs_size, _SetPrefs_inform); \
});})

#define IntuiTextLength(iText) ({ \
  CONST struct IntuiText * _IntuiTextLength_iText = (iText); \
  ({ \
  register char * _IntuiTextLength__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST struct IntuiText * __asm("a0"))) \
  (_IntuiTextLength__bn - 330))(_IntuiTextLength__bn, _IntuiTextLength_iText); \
});})

#define WBenchToBack() ({ \
  register char * _WBenchToBack__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"))) \
  (_WBenchToBack__bn - 336))(_WBenchToBack__bn); \
})

#define WBenchToFront() ({ \
  register char * _WBenchToFront__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"))) \
  (_WBenchToFront__bn - 342))(_WBenchToFront__bn); \
})

#define AutoRequest(window, body, posText, negText, pFlag, nFlag, width, height) ({ \
  struct Window * _AutoRequest_window = (window); \
  CONST struct IntuiText * _AutoRequest_body = (body); \
  CONST struct IntuiText * _AutoRequest_posText = (posText); \
  CONST struct IntuiText * _AutoRequest_negText = (negText); \
  ULONG _AutoRequest_pFlag = (pFlag); \
  ULONG _AutoRequest_nFlag = (nFlag); \
  ULONG _AutoRequest_width = (width); \
  ULONG _AutoRequest_height = (height); \
  ({ \
  register char * _AutoRequest__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"), CONST struct IntuiText * __asm("a1"), CONST struct IntuiText * __asm("a2"), CONST struct IntuiText * __asm("a3"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_AutoRequest__bn - 348))(_AutoRequest__bn, _AutoRequest_window, _AutoRequest_body, _AutoRequest_posText, _AutoRequest_negText, _AutoRequest_pFlag, _AutoRequest_nFlag, _AutoRequest_width, _AutoRequest_height); \
});})

#define BeginRefresh(window) ({ \
  struct Window * _BeginRefresh_window = (window); \
  ({ \
  register char * _BeginRefresh__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_BeginRefresh__bn - 354))(_BeginRefresh__bn, _BeginRefresh_window); \
});})

#define BuildSysRequest(window, body, posText, negText, flags, width, height) ({ \
  struct Window * _BuildSysRequest_window = (window); \
  CONST struct IntuiText * _BuildSysRequest_body = (body); \
  CONST struct IntuiText * _BuildSysRequest_posText = (posText); \
  CONST struct IntuiText * _BuildSysRequest_negText = (negText); \
  ULONG _BuildSysRequest_flags = (flags); \
  ULONG _BuildSysRequest_width = (width); \
  ULONG _BuildSysRequest_height = (height); \
  ({ \
  register char * _BuildSysRequest__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Window * (*)(char * __asm("a6"), struct Window * __asm("a0"), CONST struct IntuiText * __asm("a1"), CONST struct IntuiText * __asm("a2"), CONST struct IntuiText * __asm("a3"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"))) \
  (_BuildSysRequest__bn - 360))(_BuildSysRequest__bn, _BuildSysRequest_window, _BuildSysRequest_body, _BuildSysRequest_posText, _BuildSysRequest_negText, _BuildSysRequest_flags, _BuildSysRequest_width, _BuildSysRequest_height); \
});})

#define EndRefresh(window, complete) ({ \
  struct Window * _EndRefresh_window = (window); \
  LONG _EndRefresh_complete = (complete); \
  ({ \
  register char * _EndRefresh__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), LONG __asm("d0"))) \
  (_EndRefresh__bn - 366))(_EndRefresh__bn, _EndRefresh_window, _EndRefresh_complete); \
});})

#define FreeSysRequest(window) ({ \
  struct Window * _FreeSysRequest_window = (window); \
  ({ \
  register char * _FreeSysRequest__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_FreeSysRequest__bn - 372))(_FreeSysRequest__bn, _FreeSysRequest_window); \
});})

#define MakeScreen(screen) ({ \
  struct Screen * _MakeScreen_screen = (screen); \
  ({ \
  register char * _MakeScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Screen * __asm("a0"))) \
  (_MakeScreen__bn - 378))(_MakeScreen__bn, _MakeScreen_screen); \
});})

#define RemakeDisplay() ({ \
  register char * _RemakeDisplay__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"))) \
  (_RemakeDisplay__bn - 384))(_RemakeDisplay__bn); \
})

#define RethinkDisplay() ({ \
  register char * _RethinkDisplay__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"))) \
  (_RethinkDisplay__bn - 390))(_RethinkDisplay__bn); \
})

#define AllocRemember(rememberKey, size, flags) ({ \
  struct Remember ** _AllocRemember_rememberKey = (rememberKey); \
  ULONG _AllocRemember_size = (size); \
  ULONG _AllocRemember_flags = (flags); \
  ({ \
  register char * _AllocRemember__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct Remember ** __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_AllocRemember__bn - 396))(_AllocRemember__bn, _AllocRemember_rememberKey, _AllocRemember_size, _AllocRemember_flags); \
});})

#define AlohaWorkbench(wbport) ({ \
  LONG _AlohaWorkbench_wbport = (wbport); \
  ({ \
  register char * _AlohaWorkbench__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("a0"))) \
  (_AlohaWorkbench__bn - 402))(_AlohaWorkbench__bn, _AlohaWorkbench_wbport); \
});})

#define FreeRemember(rememberKey, reallyForget) ({ \
  struct Remember ** _FreeRemember_rememberKey = (rememberKey); \
  LONG _FreeRemember_reallyForget = (reallyForget); \
  ({ \
  register char * _FreeRemember__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Remember ** __asm("a0"), LONG __asm("d0"))) \
  (_FreeRemember__bn - 408))(_FreeRemember__bn, _FreeRemember_rememberKey, _FreeRemember_reallyForget); \
});})

#define LockIBase(dontknow) ({ \
  ULONG _LockIBase_dontknow = (dontknow); \
  ({ \
  register char * _LockIBase__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_LockIBase__bn - 414))(_LockIBase__bn, _LockIBase_dontknow); \
});})

#define UnlockIBase(ibLock) ({ \
  ULONG _UnlockIBase_ibLock = (ibLock); \
  ({ \
  register char * _UnlockIBase__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("a0"))) \
  (_UnlockIBase__bn - 420))(_UnlockIBase__bn, _UnlockIBase_ibLock); \
});})

#define GetScreenData(buffer, size, type, screen) ({ \
  APTR _GetScreenData_buffer = (buffer); \
  ULONG _GetScreenData_size = (size); \
  ULONG _GetScreenData_type = (type); \
  CONST struct Screen * _GetScreenData_screen = (screen); \
  ({ \
  register char * _GetScreenData__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), CONST struct Screen * __asm("a1"))) \
  (_GetScreenData__bn - 426))(_GetScreenData__bn, _GetScreenData_buffer, _GetScreenData_size, _GetScreenData_type, _GetScreenData_screen); \
});})

#define RefreshGList(gadgets, window, requester, numGad) ({ \
  struct Gadget * _RefreshGList_gadgets = (gadgets); \
  struct Window * _RefreshGList_window = (window); \
  struct Requester * _RefreshGList_requester = (requester); \
  LONG _RefreshGList_numGad = (numGad); \
  ({ \
  register char * _RefreshGList__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), LONG __asm("d0"))) \
  (_RefreshGList__bn - 432))(_RefreshGList__bn, _RefreshGList_gadgets, _RefreshGList_window, _RefreshGList_requester, _RefreshGList_numGad); \
});})

#define AddGList(window, gadget, position, numGad, requester) ({ \
  struct Window * _AddGList_window = (window); \
  struct Gadget * _AddGList_gadget = (gadget); \
  ULONG _AddGList_position = (position); \
  LONG _AddGList_numGad = (numGad); \
  struct Requester * _AddGList_requester = (requester); \
  ({ \
  register char * _AddGList__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Gadget * __asm("a1"), ULONG __asm("d0"), LONG __asm("d1"), struct Requester * __asm("a2"))) \
  (_AddGList__bn - 438))(_AddGList__bn, _AddGList_window, _AddGList_gadget, _AddGList_position, _AddGList_numGad, _AddGList_requester); \
});})

#define RemoveGList(remPtr, gadget, numGad) ({ \
  struct Window * _RemoveGList_remPtr = (remPtr); \
  struct Gadget * _RemoveGList_gadget = (gadget); \
  LONG _RemoveGList_numGad = (numGad); \
  ({ \
  register char * _RemoveGList__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Gadget * __asm("a1"), LONG __asm("d0"))) \
  (_RemoveGList__bn - 444))(_RemoveGList__bn, _RemoveGList_remPtr, _RemoveGList_gadget, _RemoveGList_numGad); \
});})

#define ActivateWindow(window) ({ \
  struct Window * _ActivateWindow_window = (window); \
  ({ \
  register char * _ActivateWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_ActivateWindow__bn - 450))(_ActivateWindow__bn, _ActivateWindow_window); \
});})

#define RefreshWindowFrame(window) ({ \
  struct Window * _RefreshWindowFrame_window = (window); \
  ({ \
  register char * _RefreshWindowFrame__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_RefreshWindowFrame__bn - 456))(_RefreshWindowFrame__bn, _RefreshWindowFrame_window); \
});})

#define ActivateGadget(gadgets, window, requester) ({ \
  struct Gadget * _ActivateGadget_gadgets = (gadgets); \
  struct Window * _ActivateGadget_window = (window); \
  struct Requester * _ActivateGadget_requester = (requester); \
  ({ \
  register char * _ActivateGadget__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"))) \
  (_ActivateGadget__bn - 462))(_ActivateGadget__bn, _ActivateGadget_gadgets, _ActivateGadget_window, _ActivateGadget_requester); \
});})

#define NewModifyProp(gadget, window, requester, flags, horizPot, vertPot, horizBody, vertBody, numGad) ({ \
  struct Gadget * _NewModifyProp_gadget = (gadget); \
  struct Window * _NewModifyProp_window = (window); \
  struct Requester * _NewModifyProp_requester = (requester); \
  ULONG _NewModifyProp_flags = (flags); \
  ULONG _NewModifyProp_horizPot = (horizPot); \
  ULONG _NewModifyProp_vertPot = (vertPot); \
  ULONG _NewModifyProp_horizBody = (horizBody); \
  ULONG _NewModifyProp_vertBody = (vertBody); \
  LONG _NewModifyProp_numGad = (numGad); \
  ({ \
  register char * _NewModifyProp__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), ULONG __asm("d4"), LONG __asm("d5"))) \
  (_NewModifyProp__bn - 468))(_NewModifyProp__bn, _NewModifyProp_gadget, _NewModifyProp_window, _NewModifyProp_requester, _NewModifyProp_flags, _NewModifyProp_horizPot, _NewModifyProp_vertPot, _NewModifyProp_horizBody, _NewModifyProp_vertBody, _NewModifyProp_numGad); \
});})

#define QueryOverscan(displayID, rect, oScanType) ({ \
  ULONG _QueryOverscan_displayID = (displayID); \
  struct Rectangle * _QueryOverscan_rect = (rect); \
  LONG _QueryOverscan_oScanType = (oScanType); \
  ({ \
  register char * _QueryOverscan__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), ULONG __asm("a0"), struct Rectangle * __asm("a1"), LONG __asm("d0"))) \
  (_QueryOverscan__bn - 474))(_QueryOverscan__bn, _QueryOverscan_displayID, _QueryOverscan_rect, _QueryOverscan_oScanType); \
});})

#define MoveWindowInFrontOf(window, behindWindow) ({ \
  struct Window * _MoveWindowInFrontOf_window = (window); \
  struct Window * _MoveWindowInFrontOf_behindWindow = (behindWindow); \
  ({ \
  register char * _MoveWindowInFrontOf__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Window * __asm("a1"))) \
  (_MoveWindowInFrontOf__bn - 480))(_MoveWindowInFrontOf__bn, _MoveWindowInFrontOf_window, _MoveWindowInFrontOf_behindWindow); \
});})

#define ChangeWindowBox(window, left, top, width, height) ({ \
  struct Window * _ChangeWindowBox_window = (window); \
  LONG _ChangeWindowBox_left = (left); \
  LONG _ChangeWindowBox_top = (top); \
  LONG _ChangeWindowBox_width = (width); \
  LONG _ChangeWindowBox_height = (height); \
  ({ \
  register char * _ChangeWindowBox__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_ChangeWindowBox__bn - 486))(_ChangeWindowBox__bn, _ChangeWindowBox_window, _ChangeWindowBox_left, _ChangeWindowBox_top, _ChangeWindowBox_width, _ChangeWindowBox_height); \
});})

#define SetEditHook(hook) ({ \
  struct Hook * _SetEditHook_hook = (hook); \
  ({ \
  register char * _SetEditHook__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Hook * (*)(char * __asm("a6"), struct Hook * __asm("a0"))) \
  (_SetEditHook__bn - 492))(_SetEditHook__bn, _SetEditHook_hook); \
});})

#define SetMouseQueue(window, queueLength) ({ \
  struct Window * _SetMouseQueue_window = (window); \
  ULONG _SetMouseQueue_queueLength = (queueLength); \
  ({ \
  register char * _SetMouseQueue__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Window * __asm("a0"), ULONG __asm("d0"))) \
  (_SetMouseQueue__bn - 498))(_SetMouseQueue__bn, _SetMouseQueue_window, _SetMouseQueue_queueLength); \
});})

#define ZipWindow(window) ({ \
  struct Window * _ZipWindow_window = (window); \
  ({ \
  register char * _ZipWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_ZipWindow__bn - 504))(_ZipWindow__bn, _ZipWindow_window); \
});})

#define LockPubScreen(name) ({ \
  CONST_STRPTR _LockPubScreen_name = (name); \
  ({ \
  register char * _LockPubScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Screen * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_LockPubScreen__bn - 510))(_LockPubScreen__bn, _LockPubScreen_name); \
});})

#define UnlockPubScreen(name, screen) ({ \
  CONST_STRPTR _UnlockPubScreen_name = (name); \
  struct Screen * _UnlockPubScreen_screen = (screen); \
  ({ \
  register char * _UnlockPubScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), struct Screen * __asm("a1"))) \
  (_UnlockPubScreen__bn - 516))(_UnlockPubScreen__bn, _UnlockPubScreen_name, _UnlockPubScreen_screen); \
});})

#define LockPubScreenList() ({ \
  register char * _LockPubScreenList__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct List * (*)(char * __asm("a6"))) \
  (_LockPubScreenList__bn - 522))(_LockPubScreenList__bn); \
})

#define UnlockPubScreenList() ({ \
  register char * _UnlockPubScreenList__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_UnlockPubScreenList__bn - 528))(_UnlockPubScreenList__bn); \
})

#define NextPubScreen(screen, namebuf) ({ \
  CONST struct Screen * _NextPubScreen_screen = (screen); \
  STRPTR _NextPubScreen_namebuf = (namebuf); \
  ({ \
  register char * _NextPubScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), CONST struct Screen * __asm("a0"), STRPTR __asm("a1"))) \
  (_NextPubScreen__bn - 534))(_NextPubScreen__bn, _NextPubScreen_screen, _NextPubScreen_namebuf); \
});})

#define SetDefaultPubScreen(name) ({ \
  CONST_STRPTR _SetDefaultPubScreen_name = (name); \
  ({ \
  register char * _SetDefaultPubScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_SetDefaultPubScreen__bn - 540))(_SetDefaultPubScreen__bn, _SetDefaultPubScreen_name); \
});})

#define SetPubScreenModes(modes) ({ \
  ULONG _SetPubScreenModes_modes = (modes); \
  ({ \
  register char * _SetPubScreenModes__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_SetPubScreenModes__bn - 546))(_SetPubScreenModes__bn, _SetPubScreenModes_modes); \
});})

#define PubScreenStatus(screen, statusFlags) ({ \
  struct Screen * _PubScreenStatus_screen = (screen); \
  ULONG _PubScreenStatus_statusFlags = (statusFlags); \
  ({ \
  register char * _PubScreenStatus__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), struct Screen * __asm("a0"), ULONG __asm("d0"))) \
  (_PubScreenStatus__bn - 552))(_PubScreenStatus__bn, _PubScreenStatus_screen, _PubScreenStatus_statusFlags); \
});})

#define ObtainGIRPort(gInfo) ({ \
  struct GadgetInfo * _ObtainGIRPort_gInfo = (gInfo); \
  ({ \
  register char * _ObtainGIRPort__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct RastPort	* (*)(char * __asm("a6"), struct GadgetInfo * __asm("a0"))) \
  (_ObtainGIRPort__bn - 558))(_ObtainGIRPort__bn, _ObtainGIRPort_gInfo); \
});})

#define ReleaseGIRPort(rp) ({ \
  struct RastPort * _ReleaseGIRPort_rp = (rp); \
  ({ \
  register char * _ReleaseGIRPort__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"))) \
  (_ReleaseGIRPort__bn - 564))(_ReleaseGIRPort__bn, _ReleaseGIRPort_rp); \
});})

#define GadgetMouse(gadget, gInfo, mousePoint) ({ \
  struct Gadget * _GadgetMouse_gadget = (gadget); \
  struct GadgetInfo * _GadgetMouse_gInfo = (gInfo); \
  WORD * _GadgetMouse_mousePoint = (mousePoint); \
  ({ \
  register char * _GadgetMouse__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct GadgetInfo * __asm("a1"), WORD * __asm("a2"))) \
  (_GadgetMouse__bn - 570))(_GadgetMouse__bn, _GadgetMouse_gadget, _GadgetMouse_gInfo, _GadgetMouse_mousePoint); \
});})

#define GetDefaultPubScreen(nameBuffer) ({ \
  STRPTR _GetDefaultPubScreen_nameBuffer = (nameBuffer); \
  ({ \
  register char * _GetDefaultPubScreen__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), STRPTR __asm("a0"))) \
  (_GetDefaultPubScreen__bn - 582))(_GetDefaultPubScreen__bn, _GetDefaultPubScreen_nameBuffer); \
});})

#define EasyRequestArgs(window, easyStruct, idcmpPtr, args) ({ \
  struct Window * _EasyRequestArgs_window = (window); \
  CONST struct EasyStruct * _EasyRequestArgs_easyStruct = (easyStruct); \
  ULONG * _EasyRequestArgs_idcmpPtr = (idcmpPtr); \
  CONST_APTR _EasyRequestArgs_args = (args); \
  ({ \
  register char * _EasyRequestArgs__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Window * __asm("a0"), CONST struct EasyStruct * __asm("a1"), ULONG * __asm("a2"), CONST_APTR __asm("a3"))) \
  (_EasyRequestArgs__bn - 588))(_EasyRequestArgs__bn, _EasyRequestArgs_window, _EasyRequestArgs_easyStruct, _EasyRequestArgs_idcmpPtr, _EasyRequestArgs_args); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___EasyRequest(struct IntuitionBase * IntuitionBase, struct Window * window, CONST struct EasyStruct * easyStruct, ULONG * idcmpPtr, ...)
{
  return EasyRequestArgs(window, easyStruct, idcmpPtr, (CONST_APTR) ((ULONG) &idcmpPtr + sizeof(ULONG *)));
}

#define EasyRequest(window, easyStruct, idcmpPtr...) ___EasyRequest(INTUITION_BASE_NAME, window, easyStruct, idcmpPtr)
#endif

#define BuildEasyRequestArgs(window, easyStruct, idcmp, args) ({ \
  struct Window * _BuildEasyRequestArgs_window = (window); \
  CONST struct EasyStruct * _BuildEasyRequestArgs_easyStruct = (easyStruct); \
  ULONG _BuildEasyRequestArgs_idcmp = (idcmp); \
  CONST_APTR _BuildEasyRequestArgs_args = (args); \
  ({ \
  register char * _BuildEasyRequestArgs__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Window * (*)(char * __asm("a6"), struct Window * __asm("a0"), CONST struct EasyStruct * __asm("a1"), ULONG __asm("d0"), CONST_APTR __asm("a3"))) \
  (_BuildEasyRequestArgs__bn - 594))(_BuildEasyRequestArgs__bn, _BuildEasyRequestArgs_window, _BuildEasyRequestArgs_easyStruct, _BuildEasyRequestArgs_idcmp, _BuildEasyRequestArgs_args); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Window * ___BuildEasyRequest(struct IntuitionBase * IntuitionBase, struct Window * window, CONST struct EasyStruct * easyStruct, ULONG idcmp, ...)
{
  return BuildEasyRequestArgs(window, easyStruct, idcmp, (CONST_APTR) ((ULONG) &idcmp + sizeof(ULONG)));
}

#define BuildEasyRequest(window, easyStruct, idcmp...) ___BuildEasyRequest(INTUITION_BASE_NAME, window, easyStruct, idcmp)
#endif

#define SysReqHandler(window, idcmpPtr, waitInput) ({ \
  struct Window * _SysReqHandler_window = (window); \
  ULONG * _SysReqHandler_idcmpPtr = (idcmpPtr); \
  LONG _SysReqHandler_waitInput = (waitInput); \
  ({ \
  register char * _SysReqHandler__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Window * __asm("a0"), ULONG * __asm("a1"), LONG __asm("d0"))) \
  (_SysReqHandler__bn - 600))(_SysReqHandler__bn, _SysReqHandler_window, _SysReqHandler_idcmpPtr, _SysReqHandler_waitInput); \
});})

#define OpenWindowTagList(newWindow, tagList) ({ \
  CONST struct NewWindow * _OpenWindowTagList_newWindow = (newWindow); \
  CONST struct TagItem * _OpenWindowTagList_tagList = (tagList); \
  ({ \
  register char * _OpenWindowTagList__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Window * (*)(char * __asm("a6"), CONST struct NewWindow * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_OpenWindowTagList__bn - 606))(_OpenWindowTagList__bn, _OpenWindowTagList_newWindow, _OpenWindowTagList_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Window * ___OpenWindowTags(struct IntuitionBase * IntuitionBase, CONST struct NewWindow * newWindow, ULONG tagList, ...)
{
  return OpenWindowTagList(newWindow, (CONST struct TagItem *) &tagList);
}

#define OpenWindowTags(newWindow...) ___OpenWindowTags(INTUITION_BASE_NAME, newWindow)
#endif

#define OpenScreenTagList(newScreen, tagList) ({ \
  CONST struct NewScreen * _OpenScreenTagList_newScreen = (newScreen); \
  CONST struct TagItem * _OpenScreenTagList_tagList = (tagList); \
  ({ \
  register char * _OpenScreenTagList__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct Screen * (*)(char * __asm("a6"), CONST struct NewScreen * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_OpenScreenTagList__bn - 612))(_OpenScreenTagList__bn, _OpenScreenTagList_newScreen, _OpenScreenTagList_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Screen * ___OpenScreenTags(struct IntuitionBase * IntuitionBase, CONST struct NewScreen * newScreen, ULONG tagList, ...)
{
  return OpenScreenTagList(newScreen, (CONST struct TagItem *) &tagList);
}

#define OpenScreenTags(newScreen...) ___OpenScreenTags(INTUITION_BASE_NAME, newScreen)
#endif

#define DrawImageState(rp, image, leftOffset, topOffset, state, drawInfo) ({ \
  struct RastPort * _DrawImageState_rp = (rp); \
  CONST struct Image * _DrawImageState_image = (image); \
  LONG _DrawImageState_leftOffset = (leftOffset); \
  LONG _DrawImageState_topOffset = (topOffset); \
  ULONG _DrawImageState_state = (state); \
  struct DrawInfo * _DrawImageState_drawInfo = (drawInfo); \
  ({ \
  register char * _DrawImageState__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct Image * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), ULONG __asm("d2"), struct DrawInfo * __asm("a2"))) \
  (_DrawImageState__bn - 618))(_DrawImageState__bn, _DrawImageState_rp, _DrawImageState_image, _DrawImageState_leftOffset, _DrawImageState_topOffset, _DrawImageState_state, _DrawImageState_drawInfo); \
});})

#define PointInImage(point, image) ({ \
  ULONG _PointInImage_point = (point); \
  CONST struct Image * _PointInImage_image = (image); \
  ({ \
  register char * _PointInImage__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), ULONG __asm("d0"), CONST struct Image * __asm("a0"))) \
  (_PointInImage__bn - 624))(_PointInImage__bn, _PointInImage_point, _PointInImage_image); \
});})

#define EraseImage(rp, image, leftOffset, topOffset) ({ \
  struct RastPort * _EraseImage_rp = (rp); \
  CONST struct Image * _EraseImage_image = (image); \
  LONG _EraseImage_leftOffset = (leftOffset); \
  LONG _EraseImage_topOffset = (topOffset); \
  ({ \
  register char * _EraseImage__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct Image * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_EraseImage__bn - 630))(_EraseImage__bn, _EraseImage_rp, _EraseImage_image, _EraseImage_leftOffset, _EraseImage_topOffset); \
});})

#define NewObjectA(classPtr, classID, tagList) ({ \
  struct IClass * _NewObjectA_classPtr = (classPtr); \
  CONST_STRPTR _NewObjectA_classID = (classID); \
  CONST struct TagItem * _NewObjectA_tagList = (tagList); \
  ({ \
  register char * _NewObjectA__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct IClass * __asm("a0"), CONST_STRPTR __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_NewObjectA__bn - 636))(_NewObjectA__bn, _NewObjectA_classPtr, _NewObjectA_classID, _NewObjectA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___NewObject(struct IntuitionBase * IntuitionBase, struct IClass * classPtr, CONST_STRPTR classID, ULONG tagList, ...)
{
  return NewObjectA(classPtr, classID, (CONST struct TagItem *) &tagList);
}

#define NewObject(classPtr, classID...) ___NewObject(INTUITION_BASE_NAME, classPtr, classID)
#endif

#define DisposeObject(object) ({ \
  APTR _DisposeObject_object = (object); \
  ({ \
  register char * _DisposeObject__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_DisposeObject__bn - 642))(_DisposeObject__bn, _DisposeObject_object); \
});})

#define SetAttrsA(object, tagList) ({ \
  APTR _SetAttrsA_object = (object); \
  CONST struct TagItem * _SetAttrsA_tagList = (tagList); \
  ({ \
  register char * _SetAttrsA__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), APTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_SetAttrsA__bn - 648))(_SetAttrsA__bn, _SetAttrsA_object, _SetAttrsA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___SetAttrs(struct IntuitionBase * IntuitionBase, APTR object, ULONG tagList, ...)
{
  return SetAttrsA(object, (CONST struct TagItem *) &tagList);
}

#define SetAttrs(object...) ___SetAttrs(INTUITION_BASE_NAME, object)
#endif

#define GetAttr(attrID, object, storagePtr) ({ \
  ULONG _GetAttr_attrID = (attrID); \
  APTR _GetAttr_object = (object); \
  ULONG * _GetAttr_storagePtr = (storagePtr); \
  ({ \
  register char * _GetAttr__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"), APTR __asm("a0"), ULONG * __asm("a1"))) \
  (_GetAttr__bn - 654))(_GetAttr__bn, _GetAttr_attrID, _GetAttr_object, _GetAttr_storagePtr); \
});})

#define SetGadgetAttrsA(gadget, window, requester, tagList) ({ \
  struct Gadget * _SetGadgetAttrsA_gadget = (gadget); \
  struct Window * _SetGadgetAttrsA_window = (window); \
  struct Requester * _SetGadgetAttrsA_requester = (requester); \
  CONST struct TagItem * _SetGadgetAttrsA_tagList = (tagList); \
  ({ \
  register char * _SetGadgetAttrsA__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), CONST struct TagItem * __asm("a3"))) \
  (_SetGadgetAttrsA__bn - 660))(_SetGadgetAttrsA__bn, _SetGadgetAttrsA_gadget, _SetGadgetAttrsA_window, _SetGadgetAttrsA_requester, _SetGadgetAttrsA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___SetGadgetAttrs(struct IntuitionBase * IntuitionBase, struct Gadget * gadget, struct Window * window, struct Requester * requester, ULONG tagList, ...)
{
  return SetGadgetAttrsA(gadget, window, requester, (CONST struct TagItem *) &tagList);
}

#define SetGadgetAttrs(gadget, window, requester...) ___SetGadgetAttrs(INTUITION_BASE_NAME, gadget, window, requester)
#endif

#define NextObject(objectPtrPtr) ({ \
  CONST_APTR _NextObject_objectPtrPtr = (objectPtrPtr); \
  ({ \
  register char * _NextObject__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), CONST_APTR __asm("a0"))) \
  (_NextObject__bn - 666))(_NextObject__bn, _NextObject_objectPtrPtr); \
});})

#define MakeClass(classID, superClassID, superClassPtr, instanceSize, flags) ({ \
  CONST_STRPTR _MakeClass_classID = (classID); \
  CONST_STRPTR _MakeClass_superClassID = (superClassID); \
  CONST struct IClass * _MakeClass_superClassPtr = (superClassPtr); \
  ULONG _MakeClass_instanceSize = (instanceSize); \
  ULONG _MakeClass_flags = (flags); \
  ({ \
  register char * _MakeClass__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct IClass * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST_STRPTR __asm("a1"), CONST struct IClass * __asm("a2"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_MakeClass__bn - 678))(_MakeClass__bn, _MakeClass_classID, _MakeClass_superClassID, _MakeClass_superClassPtr, _MakeClass_instanceSize, _MakeClass_flags); \
});})

#define AddClass(classPtr) ({ \
  struct IClass * _AddClass_classPtr = (classPtr); \
  ({ \
  register char * _AddClass__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IClass * __asm("a0"))) \
  (_AddClass__bn - 684))(_AddClass__bn, _AddClass_classPtr); \
});})

#define GetScreenDrawInfo(screen) ({ \
  struct Screen * _GetScreenDrawInfo_screen = (screen); \
  ({ \
  register char * _GetScreenDrawInfo__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct DrawInfo * (*)(char * __asm("a6"), struct Screen * __asm("a0"))) \
  (_GetScreenDrawInfo__bn - 690))(_GetScreenDrawInfo__bn, _GetScreenDrawInfo_screen); \
});})

#define FreeScreenDrawInfo(screen, drawInfo) ({ \
  struct Screen * _FreeScreenDrawInfo_screen = (screen); \
  struct DrawInfo * _FreeScreenDrawInfo_drawInfo = (drawInfo); \
  ({ \
  register char * _FreeScreenDrawInfo__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"), struct DrawInfo * __asm("a1"))) \
  (_FreeScreenDrawInfo__bn - 696))(_FreeScreenDrawInfo__bn, _FreeScreenDrawInfo_screen, _FreeScreenDrawInfo_drawInfo); \
});})

#define ResetMenuStrip(window, menu) ({ \
  struct Window * _ResetMenuStrip_window = (window); \
  struct Menu * _ResetMenuStrip_menu = (menu); \
  ({ \
  register char * _ResetMenuStrip__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Menu * __asm("a1"))) \
  (_ResetMenuStrip__bn - 702))(_ResetMenuStrip__bn, _ResetMenuStrip_window, _ResetMenuStrip_menu); \
});})

#define RemoveClass(classPtr) ({ \
  struct IClass * _RemoveClass_classPtr = (classPtr); \
  ({ \
  register char * _RemoveClass__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IClass * __asm("a0"))) \
  (_RemoveClass__bn - 708))(_RemoveClass__bn, _RemoveClass_classPtr); \
});})

#define FreeClass(classPtr) ({ \
  struct IClass * _FreeClass_classPtr = (classPtr); \
  ({ \
  register char * _FreeClass__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct IClass * __asm("a0"))) \
  (_FreeClass__bn - 714))(_FreeClass__bn, _FreeClass_classPtr); \
});})

#define AllocScreenBuffer(sc, bm, flags) ({ \
  struct Screen * _AllocScreenBuffer_sc = (sc); \
  struct BitMap * _AllocScreenBuffer_bm = (bm); \
  ULONG _AllocScreenBuffer_flags = (flags); \
  ({ \
  register char * _AllocScreenBuffer__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((struct ScreenBuffer * (*)(char * __asm("a6"), struct Screen * __asm("a0"), struct BitMap * __asm("a1"), ULONG __asm("d0"))) \
  (_AllocScreenBuffer__bn - 768))(_AllocScreenBuffer__bn, _AllocScreenBuffer_sc, _AllocScreenBuffer_bm, _AllocScreenBuffer_flags); \
});})

#define FreeScreenBuffer(sc, sb) ({ \
  struct Screen * _FreeScreenBuffer_sc = (sc); \
  struct ScreenBuffer * _FreeScreenBuffer_sb = (sb); \
  ({ \
  register char * _FreeScreenBuffer__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"), struct ScreenBuffer * __asm("a1"))) \
  (_FreeScreenBuffer__bn - 774))(_FreeScreenBuffer__bn, _FreeScreenBuffer_sc, _FreeScreenBuffer_sb); \
});})

#define ChangeScreenBuffer(sc, sb) ({ \
  struct Screen * _ChangeScreenBuffer_sc = (sc); \
  struct ScreenBuffer * _ChangeScreenBuffer_sb = (sb); \
  ({ \
  register char * _ChangeScreenBuffer__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Screen * __asm("a0"), struct ScreenBuffer * __asm("a1"))) \
  (_ChangeScreenBuffer__bn - 780))(_ChangeScreenBuffer__bn, _ChangeScreenBuffer_sc, _ChangeScreenBuffer_sb); \
});})

#define ScreenDepth(screen, flags, reserved) ({ \
  struct Screen * _ScreenDepth_screen = (screen); \
  ULONG _ScreenDepth_flags = (flags); \
  APTR _ScreenDepth_reserved = (reserved); \
  ({ \
  register char * _ScreenDepth__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"), ULONG __asm("d0"), APTR __asm("a1"))) \
  (_ScreenDepth__bn - 786))(_ScreenDepth__bn, _ScreenDepth_screen, _ScreenDepth_flags, _ScreenDepth_reserved); \
});})

#define ScreenPosition(screen, flags, x1, y1, x2, y2) ({ \
  struct Screen * _ScreenPosition_screen = (screen); \
  ULONG _ScreenPosition_flags = (flags); \
  LONG _ScreenPosition_x1 = (x1); \
  LONG _ScreenPosition_y1 = (y1); \
  LONG _ScreenPosition_x2 = (x2); \
  LONG _ScreenPosition_y2 = (y2); \
  ({ \
  register char * _ScreenPosition__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Screen * __asm("a0"), ULONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"))) \
  (_ScreenPosition__bn - 792))(_ScreenPosition__bn, _ScreenPosition_screen, _ScreenPosition_flags, _ScreenPosition_x1, _ScreenPosition_y1, _ScreenPosition_x2, _ScreenPosition_y2); \
});})

#define ScrollWindowRaster(win, dx, dy, xMin, yMin, xMax, yMax) ({ \
  struct Window * _ScrollWindowRaster_win = (win); \
  LONG _ScrollWindowRaster_dx = (dx); \
  LONG _ScrollWindowRaster_dy = (dy); \
  LONG _ScrollWindowRaster_xMin = (xMin); \
  LONG _ScrollWindowRaster_yMin = (yMin); \
  LONG _ScrollWindowRaster_xMax = (xMax); \
  LONG _ScrollWindowRaster_yMax = (yMax); \
  ({ \
  register char * _ScrollWindowRaster__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"))) \
  (_ScrollWindowRaster__bn - 798))(_ScrollWindowRaster__bn, _ScrollWindowRaster_win, _ScrollWindowRaster_dx, _ScrollWindowRaster_dy, _ScrollWindowRaster_xMin, _ScrollWindowRaster_yMin, _ScrollWindowRaster_xMax, _ScrollWindowRaster_yMax); \
});})

#define LendMenus(fromwindow, towindow) ({ \
  struct Window * _LendMenus_fromwindow = (fromwindow); \
  struct Window * _LendMenus_towindow = (towindow); \
  ({ \
  register char * _LendMenus__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Window * __asm("a1"))) \
  (_LendMenus__bn - 804))(_LendMenus__bn, _LendMenus_fromwindow, _LendMenus_towindow); \
});})

#define DoGadgetMethodA(gad, win, req, message) ({ \
  struct Gadget * _DoGadgetMethodA_gad = (gad); \
  struct Window * _DoGadgetMethodA_win = (win); \
  struct Requester * _DoGadgetMethodA_req = (req); \
  Msg _DoGadgetMethodA_message = (message); \
  ({ \
  register char * _DoGadgetMethodA__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Gadget * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), Msg __asm("a3"))) \
  (_DoGadgetMethodA__bn - 810))(_DoGadgetMethodA__bn, _DoGadgetMethodA_gad, _DoGadgetMethodA_win, _DoGadgetMethodA_req, _DoGadgetMethodA_message); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___DoGadgetMethod(struct IntuitionBase * IntuitionBase, struct Gadget * gad, struct Window * win, struct Requester * req, ULONG message, ...)
{
  return DoGadgetMethodA(gad, win, req, (Msg) &message);
}

#define DoGadgetMethod(gad, win, req...) ___DoGadgetMethod(INTUITION_BASE_NAME, gad, win, req)
#endif

#define SetWindowPointerA(win, taglist) ({ \
  struct Window * _SetWindowPointerA_win = (win); \
  CONST struct TagItem * _SetWindowPointerA_taglist = (taglist); \
  ({ \
  register char * _SetWindowPointerA__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_SetWindowPointerA__bn - 816))(_SetWindowPointerA__bn, _SetWindowPointerA_win, _SetWindowPointerA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetWindowPointer(struct IntuitionBase * IntuitionBase, struct Window * win, ULONG taglist, ...)
{
  SetWindowPointerA(win, (CONST struct TagItem *) &taglist);
}

#define SetWindowPointer(win...) ___SetWindowPointer(INTUITION_BASE_NAME, win)
#endif

#define TimedDisplayAlert(alertNumber, string, height, time) ({ \
  ULONG _TimedDisplayAlert_alertNumber = (alertNumber); \
  CONST_STRPTR _TimedDisplayAlert_string = (string); \
  ULONG _TimedDisplayAlert_height = (height); \
  ULONG _TimedDisplayAlert_time = (time); \
  ({ \
  register char * _TimedDisplayAlert__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), ULONG __asm("d0"), CONST_STRPTR __asm("a0"), ULONG __asm("d1"), ULONG __asm("a1"))) \
  (_TimedDisplayAlert__bn - 822))(_TimedDisplayAlert__bn, _TimedDisplayAlert_alertNumber, _TimedDisplayAlert_string, _TimedDisplayAlert_height, _TimedDisplayAlert_time); \
});})

#define HelpControl(win, flags) ({ \
  struct Window * _HelpControl_win = (win); \
  ULONG _HelpControl_flags = (flags); \
  ({ \
  register char * _HelpControl__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Window * __asm("a0"), ULONG __asm("d0"))) \
  (_HelpControl__bn - 828))(_HelpControl__bn, _HelpControl_win, _HelpControl_flags); \
});})

#define ShowWindow(window, other) ({ \
  struct Window * _ShowWindow_window = (window); \
  struct Window * _ShowWindow_other = (other); \
  ({ \
  register char * _ShowWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Window * __asm("a1"))) \
  (_ShowWindow__bn - 834))(_ShowWindow__bn, _ShowWindow_window, _ShowWindow_other); \
});})

#define HideWindow(window) ({ \
  struct Window * _HideWindow_window = (window); \
  ({ \
  register char * _HideWindow__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Window * __asm("a0"))) \
  (_HideWindow__bn - 840))(_HideWindow__bn, _HideWindow_window); \
});})

#define IntuitionControlA(object, taglist) ({ \
  APTR _IntuitionControlA_object = (object); \
  CONST struct TagItem * _IntuitionControlA_taglist = (taglist); \
  ({ \
  register char * _IntuitionControlA__bn __asm("a6") = (char *) (INTUITION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), APTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_IntuitionControlA__bn - 1212))(_IntuitionControlA__bn, _IntuitionControlA_object, _IntuitionControlA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___IntuitionControl(struct IntuitionBase * IntuitionBase, APTR object, ...)
{
  return IntuitionControlA(object, (CONST struct TagItem *) ((ULONG) &object + sizeof(APTR)));
}

#define IntuitionControl(object...) ___IntuitionControl(INTUITION_BASE_NAME, object)
#endif

#endif /*  _INLINE_INTUITION_H  */
