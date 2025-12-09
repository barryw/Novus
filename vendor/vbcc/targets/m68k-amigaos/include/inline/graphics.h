#ifndef _INLINE_GRAPHICS_H
#define _INLINE_GRAPHICS_H

#ifndef CLIB_GRAPHICS_PROTOS_H
#define CLIB_GRAPHICS_PROTOS_H
#endif

#ifndef  GRAPHICS_GFXBASE_H
#include <graphics/gfxbase.h>
#endif
#ifndef  GRAPHICS_GFX_H
#include <graphics/gfx.h>
#endif
#ifndef  GRAPHICS_DISPLAYINFO_H
#include <graphics/displayinfo.h>
#endif
#ifndef  GRAPHICS_GELS_H
#include <graphics/gels.h>
#endif
#ifndef  GRAPHICS_RASTPORT_H
#include <graphics/rastport.h>
#endif
#ifndef  GRAPHICS_VIEW_H
#include <graphics/view.h>
#endif
#ifndef  GRAPHICS_COPPER_H
#include <graphics/copper.h>
#endif
#ifndef  GRAPHICS_CLIP_H
#include <graphics/clip.h>
#endif
#ifndef  GRAPHICS_REGIONS_H
#include <graphics/regions.h>
#endif
#ifndef  GRAPHICS_SPRITE_H
#include <graphics/sprite.h>
#endif
#ifndef  GRAPHICS_TEXT_H
#include <graphics/text.h>
#endif
#ifndef  HARDWARE_BLIT_H
#include <hardware/blit.h>
#endif
#ifndef  GRAPHICS_SCALE_H
#include <graphics/scale.h>
#endif

#ifndef GRAPHICS_BASE_NAME
#define GRAPHICS_BASE_NAME GfxBase
#endif

#define BltBitMap(srcBitMap, xSrc, ySrc, destBitMap, xDest, yDest, xSize, ySize, minterm, mask, tempA) ({ \
  CONST struct BitMap * _BltBitMap_srcBitMap = (srcBitMap); \
  LONG _BltBitMap_xSrc = (xSrc); \
  LONG _BltBitMap_ySrc = (ySrc); \
  struct BitMap * _BltBitMap_destBitMap = (destBitMap); \
  LONG _BltBitMap_xDest = (xDest); \
  LONG _BltBitMap_yDest = (yDest); \
  LONG _BltBitMap_xSize = (xSize); \
  LONG _BltBitMap_ySize = (ySize); \
  ULONG _BltBitMap_minterm = (minterm); \
  ULONG _BltBitMap_mask = (mask); \
  PLANEPTR _BltBitMap_tempA = (tempA); \
  ({ \
  register char * _BltBitMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST struct BitMap * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), struct BitMap * __asm("a1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"), ULONG __asm("d6"), ULONG __asm("d7"), PLANEPTR __asm("a2"))) \
  (_BltBitMap__bn - 30))(_BltBitMap__bn, _BltBitMap_srcBitMap, _BltBitMap_xSrc, _BltBitMap_ySrc, _BltBitMap_destBitMap, _BltBitMap_xDest, _BltBitMap_yDest, _BltBitMap_xSize, _BltBitMap_ySize, _BltBitMap_minterm, _BltBitMap_mask, _BltBitMap_tempA); \
});})

#define BltTemplate(source, xSrc, srcMod, destRP, xDest, yDest, xSize, ySize) ({ \
  CONST PLANEPTR _BltTemplate_source = (source); \
  LONG _BltTemplate_xSrc = (xSrc); \
  LONG _BltTemplate_srcMod = (srcMod); \
  struct RastPort * _BltTemplate_destRP = (destRP); \
  LONG _BltTemplate_xDest = (xDest); \
  LONG _BltTemplate_yDest = (yDest); \
  LONG _BltTemplate_xSize = (xSize); \
  LONG _BltTemplate_ySize = (ySize); \
  ({ \
  register char * _BltTemplate__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST PLANEPTR __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), struct RastPort * __asm("a1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"))) \
  (_BltTemplate__bn - 36))(_BltTemplate__bn, _BltTemplate_source, _BltTemplate_xSrc, _BltTemplate_srcMod, _BltTemplate_destRP, _BltTemplate_xDest, _BltTemplate_yDest, _BltTemplate_xSize, _BltTemplate_ySize); \
});})

#define ClearEOL(rp) ({ \
  struct RastPort * _ClearEOL_rp = (rp); \
  ({ \
  register char * _ClearEOL__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"))) \
  (_ClearEOL__bn - 42))(_ClearEOL__bn, _ClearEOL_rp); \
});})

#define ClearScreen(rp) ({ \
  struct RastPort * _ClearScreen_rp = (rp); \
  ({ \
  register char * _ClearScreen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"))) \
  (_ClearScreen__bn - 48))(_ClearScreen__bn, _ClearScreen_rp); \
});})

#define TextLength(rp, string, count) ({ \
  struct RastPort * _TextLength_rp = (rp); \
  CONST_STRPTR _TextLength_string = (string); \
  ULONG _TextLength_count = (count); \
  ({ \
  register char * _TextLength__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((WORD (*)(char * __asm("a6"), struct RastPort * __asm("a1"), CONST_STRPTR __asm("a0"), ULONG __asm("d0"))) \
  (_TextLength__bn - 54))(_TextLength__bn, _TextLength_rp, _TextLength_string, _TextLength_count); \
});})

#define Text(rp, string, count) ({ \
  struct RastPort * _Text_rp = (rp); \
  CONST_STRPTR _Text_string = (string); \
  ULONG _Text_count = (count); \
  ({ \
  register char * _Text__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), CONST_STRPTR __asm("a0"), ULONG __asm("d0"))) \
  (_Text__bn - 60))(_Text__bn, _Text_rp, _Text_string, _Text_count); \
});})

#define SetFont(rp, textFont) ({ \
  struct RastPort * _SetFont_rp = (rp); \
  struct TextFont * _SetFont_textFont = (textFont); \
  ({ \
  register char * _SetFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), struct TextFont * __asm("a0"))) \
  (_SetFont__bn - 66))(_SetFont__bn, _SetFont_rp, _SetFont_textFont); \
});})

#define OpenFont(textAttr) ({ \
  CONST struct TextAttr * _OpenFont_textAttr = (textAttr); \
  ({ \
  register char * _OpenFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct TextFont * (*)(char * __asm("a6"), CONST struct TextAttr * __asm("a0"))) \
  (_OpenFont__bn - 72))(_OpenFont__bn, _OpenFont_textAttr); \
});})

#define CloseFont(textFont) ({ \
  struct TextFont * _CloseFont_textFont = (textFont); \
  ({ \
  register char * _CloseFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct TextFont * __asm("a1"))) \
  (_CloseFont__bn - 78))(_CloseFont__bn, _CloseFont_textFont); \
});})

#define AskSoftStyle(rp) ({ \
  struct RastPort * _AskSoftStyle_rp = (rp); \
  ({ \
  register char * _AskSoftStyle__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"))) \
  (_AskSoftStyle__bn - 84))(_AskSoftStyle__bn, _AskSoftStyle_rp); \
});})

#define SetSoftStyle(rp, style, enable) ({ \
  struct RastPort * _SetSoftStyle_rp = (rp); \
  ULONG _SetSoftStyle_style = (style); \
  ULONG _SetSoftStyle_enable = (enable); \
  ({ \
  register char * _SetSoftStyle__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_SetSoftStyle__bn - 90))(_SetSoftStyle__bn, _SetSoftStyle_rp, _SetSoftStyle_style, _SetSoftStyle_enable); \
});})

#define AddBob(bob, rp) ({ \
  struct Bob * _AddBob_bob = (bob); \
  struct RastPort * _AddBob_rp = (rp); \
  ({ \
  register char * _AddBob__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Bob * __asm("a0"), struct RastPort * __asm("a1"))) \
  (_AddBob__bn - 96))(_AddBob__bn, _AddBob_bob, _AddBob_rp); \
});})

#define AddVSprite(vSprite, rp) ({ \
  struct VSprite * _AddVSprite_vSprite = (vSprite); \
  struct RastPort * _AddVSprite_rp = (rp); \
  ({ \
  register char * _AddVSprite__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct VSprite * __asm("a0"), struct RastPort * __asm("a1"))) \
  (_AddVSprite__bn - 102))(_AddVSprite__bn, _AddVSprite_vSprite, _AddVSprite_rp); \
});})

#define DoCollision(rp) ({ \
  struct RastPort * _DoCollision_rp = (rp); \
  ({ \
  register char * _DoCollision__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"))) \
  (_DoCollision__bn - 108))(_DoCollision__bn, _DoCollision_rp); \
});})

#define DrawGList(rp, vp) ({ \
  struct RastPort * _DrawGList_rp = (rp); \
  struct ViewPort * _DrawGList_vp = (vp); \
  ({ \
  register char * _DrawGList__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), struct ViewPort * __asm("a0"))) \
  (_DrawGList__bn - 114))(_DrawGList__bn, _DrawGList_rp, _DrawGList_vp); \
});})

#define InitGels(head, tail, gelsInfo) ({ \
  struct VSprite * _InitGels_head = (head); \
  struct VSprite * _InitGels_tail = (tail); \
  struct GelsInfo * _InitGels_gelsInfo = (gelsInfo); \
  ({ \
  register char * _InitGels__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct VSprite * __asm("a0"), struct VSprite * __asm("a1"), struct GelsInfo * __asm("a2"))) \
  (_InitGels__bn - 120))(_InitGels__bn, _InitGels_head, _InitGels_tail, _InitGels_gelsInfo); \
});})

#define InitMasks(vSprite) ({ \
  struct VSprite * _InitMasks_vSprite = (vSprite); \
  ({ \
  register char * _InitMasks__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct VSprite * __asm("a0"))) \
  (_InitMasks__bn - 126))(_InitMasks__bn, _InitMasks_vSprite); \
});})

#define RemIBob(bob, rp, vp) ({ \
  struct Bob * _RemIBob_bob = (bob); \
  struct RastPort * _RemIBob_rp = (rp); \
  struct ViewPort * _RemIBob_vp = (vp); \
  ({ \
  register char * _RemIBob__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Bob * __asm("a0"), struct RastPort * __asm("a1"), struct ViewPort * __asm("a2"))) \
  (_RemIBob__bn - 132))(_RemIBob__bn, _RemIBob_bob, _RemIBob_rp, _RemIBob_vp); \
});})

#define RemVSprite(vSprite) ({ \
  struct VSprite * _RemVSprite_vSprite = (vSprite); \
  ({ \
  register char * _RemVSprite__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct VSprite * __asm("a0"))) \
  (_RemVSprite__bn - 138))(_RemVSprite__bn, _RemVSprite_vSprite); \
});})

#define SetCollision(num, routine, gelsInfo) ({ \
  ULONG _SetCollision_num = (num); \
  VOID (*_SetCollision_routine)(struct VSprite *gelA, struct VSprite *gelB) = (routine); \
  struct GelsInfo * _SetCollision_gelsInfo = (gelsInfo); \
  ({ \
  register char * _SetCollision__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"), VOID (*)(struct VSprite *gelA, struct VSprite *gelB) __asm("a0"), struct GelsInfo * __asm("a1"))) \
  (_SetCollision__bn - 144))(_SetCollision__bn, _SetCollision_num, _SetCollision_routine, _SetCollision_gelsInfo); \
});})

#define SortGList(rp) ({ \
  struct RastPort * _SortGList_rp = (rp); \
  ({ \
  register char * _SortGList__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"))) \
  (_SortGList__bn - 150))(_SortGList__bn, _SortGList_rp); \
});})

#define AddAnimOb(anOb, anKey, rp) ({ \
  struct AnimOb * _AddAnimOb_anOb = (anOb); \
  struct AnimOb ** _AddAnimOb_anKey = (anKey); \
  struct RastPort * _AddAnimOb_rp = (rp); \
  ({ \
  register char * _AddAnimOb__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct AnimOb * __asm("a0"), struct AnimOb ** __asm("a1"), struct RastPort * __asm("a2"))) \
  (_AddAnimOb__bn - 156))(_AddAnimOb__bn, _AddAnimOb_anOb, _AddAnimOb_anKey, _AddAnimOb_rp); \
});})

#define Animate(anKey, rp) ({ \
  struct AnimOb ** _Animate_anKey = (anKey); \
  struct RastPort * _Animate_rp = (rp); \
  ({ \
  register char * _Animate__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct AnimOb ** __asm("a0"), struct RastPort * __asm("a1"))) \
  (_Animate__bn - 162))(_Animate__bn, _Animate_anKey, _Animate_rp); \
});})

#define GetGBuffers(anOb, rp, flag) ({ \
  struct AnimOb * _GetGBuffers_anOb = (anOb); \
  struct RastPort * _GetGBuffers_rp = (rp); \
  LONG _GetGBuffers_flag = (flag); \
  ({ \
  register char * _GetGBuffers__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct AnimOb * __asm("a0"), struct RastPort * __asm("a1"), LONG __asm("d0"))) \
  (_GetGBuffers__bn - 168))(_GetGBuffers__bn, _GetGBuffers_anOb, _GetGBuffers_rp, _GetGBuffers_flag); \
});})

#define InitGMasks(anOb) ({ \
  struct AnimOb * _InitGMasks_anOb = (anOb); \
  ({ \
  register char * _InitGMasks__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct AnimOb * __asm("a0"))) \
  (_InitGMasks__bn - 174))(_InitGMasks__bn, _InitGMasks_anOb); \
});})

#define DrawEllipse(rp, xCenter, yCenter, a, b) ({ \
  struct RastPort * _DrawEllipse_rp = (rp); \
  LONG _DrawEllipse_xCenter = (xCenter); \
  LONG _DrawEllipse_yCenter = (yCenter); \
  LONG _DrawEllipse_a = (a); \
  LONG _DrawEllipse_b = (b); \
  ({ \
  register char * _DrawEllipse__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_DrawEllipse__bn - 180))(_DrawEllipse__bn, _DrawEllipse_rp, _DrawEllipse_xCenter, _DrawEllipse_yCenter, _DrawEllipse_a, _DrawEllipse_b); \
});})

#define AreaEllipse(rp, xCenter, yCenter, a, b) ({ \
  struct RastPort * _AreaEllipse_rp = (rp); \
  LONG _AreaEllipse_xCenter = (xCenter); \
  LONG _AreaEllipse_yCenter = (yCenter); \
  LONG _AreaEllipse_a = (a); \
  LONG _AreaEllipse_b = (b); \
  ({ \
  register char * _AreaEllipse__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_AreaEllipse__bn - 186))(_AreaEllipse__bn, _AreaEllipse_rp, _AreaEllipse_xCenter, _AreaEllipse_yCenter, _AreaEllipse_a, _AreaEllipse_b); \
});})

#define LoadRGB4(vp, colors, count) ({ \
  struct ViewPort * _LoadRGB4_vp = (vp); \
  CONST UWORD * _LoadRGB4_colors = (colors); \
  LONG _LoadRGB4_count = (count); \
  ({ \
  register char * _LoadRGB4__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), CONST UWORD * __asm("a1"), LONG __asm("d0"))) \
  (_LoadRGB4__bn - 192))(_LoadRGB4__bn, _LoadRGB4_vp, _LoadRGB4_colors, _LoadRGB4_count); \
});})

#define InitRastPort(rp) ({ \
  struct RastPort * _InitRastPort_rp = (rp); \
  ({ \
  register char * _InitRastPort__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"))) \
  (_InitRastPort__bn - 198))(_InitRastPort__bn, _InitRastPort_rp); \
});})

#define InitVPort(vp) ({ \
  struct ViewPort * _InitVPort_vp = (vp); \
  ({ \
  register char * _InitVPort__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"))) \
  (_InitVPort__bn - 204))(_InitVPort__bn, _InitVPort_vp); \
});})

#define MrgCop(view) ({ \
  struct View * _MrgCop_view = (view); \
  ({ \
  register char * _MrgCop__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct View * __asm("a1"))) \
  (_MrgCop__bn - 210))(_MrgCop__bn, _MrgCop_view); \
});})

#define MakeVPort(view, vp) ({ \
  struct View * _MakeVPort_view = (view); \
  struct ViewPort * _MakeVPort_vp = (vp); \
  ({ \
  register char * _MakeVPort__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct View * __asm("a0"), struct ViewPort * __asm("a1"))) \
  (_MakeVPort__bn - 216))(_MakeVPort__bn, _MakeVPort_view, _MakeVPort_vp); \
});})

#define LoadView(view) ({ \
  struct View * _LoadView_view = (view); \
  ({ \
  register char * _LoadView__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct View * __asm("a1"))) \
  (_LoadView__bn - 222))(_LoadView__bn, _LoadView_view); \
});})

#define WaitBlit() ({ \
  register char * _WaitBlit__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_WaitBlit__bn - 228))(_WaitBlit__bn); \
})

#define SetRast(rp, pen) ({ \
  struct RastPort * _SetRast_rp = (rp); \
  ULONG _SetRast_pen = (pen); \
  ({ \
  register char * _SetRast__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), ULONG __asm("d0"))) \
  (_SetRast__bn - 234))(_SetRast__bn, _SetRast_rp, _SetRast_pen); \
});})

#define Move(rp, x, y) ({ \
  struct RastPort * _Move_rp = (rp); \
  LONG _Move_x = (x); \
  LONG _Move_y = (y); \
  ({ \
  register char * _Move__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_Move__bn - 240))(_Move__bn, _Move_rp, _Move_x, _Move_y); \
});})

#define Draw(rp, x, y) ({ \
  struct RastPort * _Draw_rp = (rp); \
  LONG _Draw_x = (x); \
  LONG _Draw_y = (y); \
  ({ \
  register char * _Draw__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_Draw__bn - 246))(_Draw__bn, _Draw_rp, _Draw_x, _Draw_y); \
});})

#define AreaMove(rp, x, y) ({ \
  struct RastPort * _AreaMove_rp = (rp); \
  LONG _AreaMove_x = (x); \
  LONG _AreaMove_y = (y); \
  ({ \
  register char * _AreaMove__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_AreaMove__bn - 252))(_AreaMove__bn, _AreaMove_rp, _AreaMove_x, _AreaMove_y); \
});})

#define AreaDraw(rp, x, y) ({ \
  struct RastPort * _AreaDraw_rp = (rp); \
  LONG _AreaDraw_x = (x); \
  LONG _AreaDraw_y = (y); \
  ({ \
  register char * _AreaDraw__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_AreaDraw__bn - 258))(_AreaDraw__bn, _AreaDraw_rp, _AreaDraw_x, _AreaDraw_y); \
});})

#define AreaEnd(rp) ({ \
  struct RastPort * _AreaEnd_rp = (rp); \
  ({ \
  register char * _AreaEnd__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"))) \
  (_AreaEnd__bn - 264))(_AreaEnd__bn, _AreaEnd_rp); \
});})

#define WaitTOF() ({ \
  register char * _WaitTOF__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_WaitTOF__bn - 270))(_WaitTOF__bn); \
})

#define QBlit(blit) ({ \
  struct bltnode * _QBlit_blit = (blit); \
  ({ \
  register char * _QBlit__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct bltnode * __asm("a1"))) \
  (_QBlit__bn - 276))(_QBlit__bn, _QBlit_blit); \
});})

#define InitArea(areaInfo, vectorBuffer, maxVectors) ({ \
  struct AreaInfo * _InitArea_areaInfo = (areaInfo); \
  APTR _InitArea_vectorBuffer = (vectorBuffer); \
  LONG _InitArea_maxVectors = (maxVectors); \
  ({ \
  register char * _InitArea__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct AreaInfo * __asm("a0"), APTR __asm("a1"), LONG __asm("d0"))) \
  (_InitArea__bn - 282))(_InitArea__bn, _InitArea_areaInfo, _InitArea_vectorBuffer, _InitArea_maxVectors); \
});})

#define SetRGB4(vp, index, red, green, blue) ({ \
  struct ViewPort * _SetRGB4_vp = (vp); \
  LONG _SetRGB4_index = (index); \
  ULONG _SetRGB4_red = (red); \
  ULONG _SetRGB4_green = (green); \
  ULONG _SetRGB4_blue = (blue); \
  ({ \
  register char * _SetRGB4__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), LONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_SetRGB4__bn - 288))(_SetRGB4__bn, _SetRGB4_vp, _SetRGB4_index, _SetRGB4_red, _SetRGB4_green, _SetRGB4_blue); \
});})

#define QBSBlit(blit) ({ \
  struct bltnode * _QBSBlit_blit = (blit); \
  ({ \
  register char * _QBSBlit__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct bltnode * __asm("a1"))) \
  (_QBSBlit__bn - 294))(_QBSBlit__bn, _QBSBlit_blit); \
});})

#define BltClear(memBlock, byteCount, flags) ({ \
  PLANEPTR _BltClear_memBlock = (memBlock); \
  ULONG _BltClear_byteCount = (byteCount); \
  ULONG _BltClear_flags = (flags); \
  ({ \
  register char * _BltClear__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), PLANEPTR __asm("a1"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_BltClear__bn - 300))(_BltClear__bn, _BltClear_memBlock, _BltClear_byteCount, _BltClear_flags); \
});})

#define RectFill(rp, xMin, yMin, xMax, yMax) ({ \
  struct RastPort * _RectFill_rp = (rp); \
  LONG _RectFill_xMin = (xMin); \
  LONG _RectFill_yMin = (yMin); \
  LONG _RectFill_xMax = (xMax); \
  LONG _RectFill_yMax = (yMax); \
  ({ \
  register char * _RectFill__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_RectFill__bn - 306))(_RectFill__bn, _RectFill_rp, _RectFill_xMin, _RectFill_yMin, _RectFill_xMax, _RectFill_yMax); \
});})

#define BltPattern(rp, mask, xMin, yMin, xMax, yMax, maskBPR) ({ \
  struct RastPort * _BltPattern_rp = (rp); \
  CONST PLANEPTR _BltPattern_mask = (mask); \
  LONG _BltPattern_xMin = (xMin); \
  LONG _BltPattern_yMin = (yMin); \
  LONG _BltPattern_xMax = (xMax); \
  LONG _BltPattern_yMax = (yMax); \
  ULONG _BltPattern_maskBPR = (maskBPR); \
  ({ \
  register char * _BltPattern__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), CONST PLANEPTR __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), ULONG __asm("d4"))) \
  (_BltPattern__bn - 312))(_BltPattern__bn, _BltPattern_rp, _BltPattern_mask, _BltPattern_xMin, _BltPattern_yMin, _BltPattern_xMax, _BltPattern_yMax, _BltPattern_maskBPR); \
});})

#define ReadPixel(rp, x, y) ({ \
  struct RastPort * _ReadPixel_rp = (rp); \
  LONG _ReadPixel_x = (x); \
  LONG _ReadPixel_y = (y); \
  ({ \
  register char * _ReadPixel__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_ReadPixel__bn - 318))(_ReadPixel__bn, _ReadPixel_rp, _ReadPixel_x, _ReadPixel_y); \
});})

#define WritePixel(rp, x, y) ({ \
  struct RastPort * _WritePixel_rp = (rp); \
  LONG _WritePixel_x = (x); \
  LONG _WritePixel_y = (y); \
  ({ \
  register char * _WritePixel__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_WritePixel__bn - 324))(_WritePixel__bn, _WritePixel_rp, _WritePixel_x, _WritePixel_y); \
});})

#define Flood(rp, mode, x, y) ({ \
  struct RastPort * _Flood_rp = (rp); \
  ULONG _Flood_mode = (mode); \
  LONG _Flood_x = (x); \
  LONG _Flood_y = (y); \
  ({ \
  register char * _Flood__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct RastPort * __asm("a1"), ULONG __asm("d2"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_Flood__bn - 330))(_Flood__bn, _Flood_rp, _Flood_mode, _Flood_x, _Flood_y); \
});})

#define PolyDraw(rp, count, polyTable) ({ \
  struct RastPort * _PolyDraw_rp = (rp); \
  LONG _PolyDraw_count = (count); \
  CONST WORD * _PolyDraw_polyTable = (polyTable); \
  ({ \
  register char * _PolyDraw__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), CONST WORD * __asm("a0"))) \
  (_PolyDraw__bn - 336))(_PolyDraw__bn, _PolyDraw_rp, _PolyDraw_count, _PolyDraw_polyTable); \
});})

#define SetAPen(rp, pen) ({ \
  struct RastPort * _SetAPen_rp = (rp); \
  ULONG _SetAPen_pen = (pen); \
  ({ \
  register char * _SetAPen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), ULONG __asm("d0"))) \
  (_SetAPen__bn - 342))(_SetAPen__bn, _SetAPen_rp, _SetAPen_pen); \
});})

#define SetBPen(rp, pen) ({ \
  struct RastPort * _SetBPen_rp = (rp); \
  ULONG _SetBPen_pen = (pen); \
  ({ \
  register char * _SetBPen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), ULONG __asm("d0"))) \
  (_SetBPen__bn - 348))(_SetBPen__bn, _SetBPen_rp, _SetBPen_pen); \
});})

#define SetDrMd(rp, drawMode) ({ \
  struct RastPort * _SetDrMd_rp = (rp); \
  ULONG _SetDrMd_drawMode = (drawMode); \
  ({ \
  register char * _SetDrMd__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), ULONG __asm("d0"))) \
  (_SetDrMd__bn - 354))(_SetDrMd__bn, _SetDrMd_rp, _SetDrMd_drawMode); \
});})

#define InitView(view) ({ \
  struct View * _InitView_view = (view); \
  ({ \
  register char * _InitView__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct View * __asm("a1"))) \
  (_InitView__bn - 360))(_InitView__bn, _InitView_view); \
});})

#define CBump(copList) ({ \
  struct UCopList * _CBump_copList = (copList); \
  ({ \
  register char * _CBump__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct UCopList * __asm("a1"))) \
  (_CBump__bn - 366))(_CBump__bn, _CBump_copList); \
});})

#define CMove(copList, destination, data) ({ \
  struct UCopList * _CMove_copList = (copList); \
  APTR _CMove_destination = (destination); \
  LONG _CMove_data = (data); \
  ({ \
  register char * _CMove__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct UCopList * __asm("a1"), APTR __asm("d0"), LONG __asm("d1"))) \
  (_CMove__bn - 372))(_CMove__bn, _CMove_copList, _CMove_destination, _CMove_data); \
});})

#define CWait(copList, v, h) ({ \
  struct UCopList * _CWait_copList = (copList); \
  LONG _CWait_v = (v); \
  LONG _CWait_h = (h); \
  ({ \
  register char * _CWait__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct UCopList * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_CWait__bn - 378))(_CWait__bn, _CWait_copList, _CWait_v, _CWait_h); \
});})

#define VBeamPos() ({ \
  register char * _VBeamPos__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"))) \
  (_VBeamPos__bn - 384))(_VBeamPos__bn); \
})

#define InitBitMap(bitMap, depth, width, height) ({ \
  struct BitMap * _InitBitMap_bitMap = (bitMap); \
  LONG _InitBitMap_depth = (depth); \
  LONG _InitBitMap_width = (width); \
  LONG _InitBitMap_height = (height); \
  ({ \
  register char * _InitBitMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct BitMap * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"))) \
  (_InitBitMap__bn - 390))(_InitBitMap__bn, _InitBitMap_bitMap, _InitBitMap_depth, _InitBitMap_width, _InitBitMap_height); \
});})

#define ScrollRaster(rp, dx, dy, xMin, yMin, xMax, yMax) ({ \
  struct RastPort * _ScrollRaster_rp = (rp); \
  LONG _ScrollRaster_dx = (dx); \
  LONG _ScrollRaster_dy = (dy); \
  LONG _ScrollRaster_xMin = (xMin); \
  LONG _ScrollRaster_yMin = (yMin); \
  LONG _ScrollRaster_xMax = (xMax); \
  LONG _ScrollRaster_yMax = (yMax); \
  ({ \
  register char * _ScrollRaster__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"))) \
  (_ScrollRaster__bn - 396))(_ScrollRaster__bn, _ScrollRaster_rp, _ScrollRaster_dx, _ScrollRaster_dy, _ScrollRaster_xMin, _ScrollRaster_yMin, _ScrollRaster_xMax, _ScrollRaster_yMax); \
});})

#define WaitBOVP(vp) ({ \
  struct ViewPort * _WaitBOVP_vp = (vp); \
  ({ \
  register char * _WaitBOVP__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"))) \
  (_WaitBOVP__bn - 402))(_WaitBOVP__bn, _WaitBOVP_vp); \
});})

#define GetSprite(sprite, num) ({ \
  struct SimpleSprite * _GetSprite_sprite = (sprite); \
  LONG _GetSprite_num = (num); \
  ({ \
  register char * _GetSprite__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((WORD (*)(char * __asm("a6"), struct SimpleSprite * __asm("a0"), LONG __asm("d0"))) \
  (_GetSprite__bn - 408))(_GetSprite__bn, _GetSprite_sprite, _GetSprite_num); \
});})

#define FreeSprite(num) ({ \
  LONG _FreeSprite_num = (num); \
  ({ \
  register char * _FreeSprite__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_FreeSprite__bn - 414))(_FreeSprite__bn, _FreeSprite_num); \
});})

#define ChangeSprite(vp, sprite, newData) ({ \
  struct ViewPort * _ChangeSprite_vp = (vp); \
  struct SimpleSprite * _ChangeSprite_sprite = (sprite); \
  UWORD * _ChangeSprite_newData = (newData); \
  ({ \
  register char * _ChangeSprite__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), struct SimpleSprite * __asm("a1"), UWORD * __asm("a2"))) \
  (_ChangeSprite__bn - 420))(_ChangeSprite__bn, _ChangeSprite_vp, _ChangeSprite_sprite, _ChangeSprite_newData); \
});})

#define MoveSprite(vp, sprite, x, y) ({ \
  struct ViewPort * _MoveSprite_vp = (vp); \
  struct SimpleSprite * _MoveSprite_sprite = (sprite); \
  LONG _MoveSprite_x = (x); \
  LONG _MoveSprite_y = (y); \
  ({ \
  register char * _MoveSprite__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), struct SimpleSprite * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_MoveSprite__bn - 426))(_MoveSprite__bn, _MoveSprite_vp, _MoveSprite_sprite, _MoveSprite_x, _MoveSprite_y); \
});})

#define LockLayerRom(layer) ({ \
  struct Layer * _LockLayerRom_layer = (layer); \
  ({ \
  register char * _LockLayerRom__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Layer * __asm("a5"))) \
  (_LockLayerRom__bn - 432))(_LockLayerRom__bn, _LockLayerRom_layer); \
});})

#define UnlockLayerRom(layer) ({ \
  struct Layer * _UnlockLayerRom_layer = (layer); \
  ({ \
  register char * _UnlockLayerRom__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Layer * __asm("a5"))) \
  (_UnlockLayerRom__bn - 438))(_UnlockLayerRom__bn, _UnlockLayerRom_layer); \
});})

#define SyncSBitMap(layer) ({ \
  struct Layer * _SyncSBitMap_layer = (layer); \
  ({ \
  register char * _SyncSBitMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Layer * __asm("a0"))) \
  (_SyncSBitMap__bn - 444))(_SyncSBitMap__bn, _SyncSBitMap_layer); \
});})

#define CopySBitMap(layer) ({ \
  struct Layer * _CopySBitMap_layer = (layer); \
  ({ \
  register char * _CopySBitMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Layer * __asm("a0"))) \
  (_CopySBitMap__bn - 450))(_CopySBitMap__bn, _CopySBitMap_layer); \
});})

#define OwnBlitter() ({ \
  register char * _OwnBlitter__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_OwnBlitter__bn - 456))(_OwnBlitter__bn); \
})

#define DisownBlitter() ({ \
  register char * _DisownBlitter__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_DisownBlitter__bn - 462))(_DisownBlitter__bn); \
})

#define InitTmpRas(tmpRas, buffer, size) ({ \
  struct TmpRas * _InitTmpRas_tmpRas = (tmpRas); \
  PLANEPTR _InitTmpRas_buffer = (buffer); \
  LONG _InitTmpRas_size = (size); \
  ({ \
  register char * _InitTmpRas__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct TmpRas * (*)(char * __asm("a6"), struct TmpRas * __asm("a0"), PLANEPTR __asm("a1"), LONG __asm("d0"))) \
  (_InitTmpRas__bn - 468))(_InitTmpRas__bn, _InitTmpRas_tmpRas, _InitTmpRas_buffer, _InitTmpRas_size); \
});})

#define AskFont(rp, textAttr) ({ \
  struct RastPort * _AskFont_rp = (rp); \
  struct TextAttr * _AskFont_textAttr = (textAttr); \
  ({ \
  register char * _AskFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), struct TextAttr * __asm("a0"))) \
  (_AskFont__bn - 474))(_AskFont__bn, _AskFont_rp, _AskFont_textAttr); \
});})

#define AddFont(textFont) ({ \
  struct TextFont * _AddFont_textFont = (textFont); \
  ({ \
  register char * _AddFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct TextFont * __asm("a1"))) \
  (_AddFont__bn - 480))(_AddFont__bn, _AddFont_textFont); \
});})

#define RemFont(textFont) ({ \
  struct TextFont * _RemFont_textFont = (textFont); \
  ({ \
  register char * _RemFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct TextFont * __asm("a1"))) \
  (_RemFont__bn - 486))(_RemFont__bn, _RemFont_textFont); \
});})

#define AllocRaster(width, height) ({ \
  ULONG _AllocRaster_width = (width); \
  ULONG _AllocRaster_height = (height); \
  ({ \
  register char * _AllocRaster__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((PLANEPTR (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_AllocRaster__bn - 492))(_AllocRaster__bn, _AllocRaster_width, _AllocRaster_height); \
});})

#define FreeRaster(p, width, height) ({ \
  PLANEPTR _FreeRaster_p = (p); \
  ULONG _FreeRaster_width = (width); \
  ULONG _FreeRaster_height = (height); \
  ({ \
  register char * _FreeRaster__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), PLANEPTR __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_FreeRaster__bn - 498))(_FreeRaster__bn, _FreeRaster_p, _FreeRaster_width, _FreeRaster_height); \
});})

#define AndRectRegion(region, rectangle) ({ \
  struct Region * _AndRectRegion_region = (region); \
  CONST struct Rectangle * _AndRectRegion_rectangle = (rectangle); \
  ({ \
  register char * _AndRectRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Region * __asm("a0"), CONST struct Rectangle * __asm("a1"))) \
  (_AndRectRegion__bn - 504))(_AndRectRegion__bn, _AndRectRegion_region, _AndRectRegion_rectangle); \
});})

#define OrRectRegion(region, rectangle) ({ \
  struct Region * _OrRectRegion_region = (region); \
  CONST struct Rectangle * _OrRectRegion_rectangle = (rectangle); \
  ({ \
  register char * _OrRectRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Region * __asm("a0"), CONST struct Rectangle * __asm("a1"))) \
  (_OrRectRegion__bn - 510))(_OrRectRegion__bn, _OrRectRegion_region, _OrRectRegion_rectangle); \
});})

#define NewRegion() ({ \
  register char * _NewRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct Region * (*)(char * __asm("a6"))) \
  (_NewRegion__bn - 516))(_NewRegion__bn); \
})

#define ClearRectRegion(region, rectangle) ({ \
  struct Region * _ClearRectRegion_region = (region); \
  CONST struct Rectangle * _ClearRectRegion_rectangle = (rectangle); \
  ({ \
  register char * _ClearRectRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Region * __asm("a0"), CONST struct Rectangle * __asm("a1"))) \
  (_ClearRectRegion__bn - 522))(_ClearRectRegion__bn, _ClearRectRegion_region, _ClearRectRegion_rectangle); \
});})

#define ClearRegion(region) ({ \
  struct Region * _ClearRegion_region = (region); \
  ({ \
  register char * _ClearRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Region * __asm("a0"))) \
  (_ClearRegion__bn - 528))(_ClearRegion__bn, _ClearRegion_region); \
});})

#define DisposeRegion(region) ({ \
  struct Region * _DisposeRegion_region = (region); \
  ({ \
  register char * _DisposeRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Region * __asm("a0"))) \
  (_DisposeRegion__bn - 534))(_DisposeRegion__bn, _DisposeRegion_region); \
});})

#define FreeVPortCopLists(vp) ({ \
  struct ViewPort * _FreeVPortCopLists_vp = (vp); \
  ({ \
  register char * _FreeVPortCopLists__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"))) \
  (_FreeVPortCopLists__bn - 540))(_FreeVPortCopLists__bn, _FreeVPortCopLists_vp); \
});})

#define FreeCopList(copList) ({ \
  struct CopList * _FreeCopList_copList = (copList); \
  ({ \
  register char * _FreeCopList__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct CopList * __asm("a0"))) \
  (_FreeCopList__bn - 546))(_FreeCopList__bn, _FreeCopList_copList); \
});})

#define ClipBlit(srcRP, xSrc, ySrc, destRP, xDest, yDest, xSize, ySize, minterm) ({ \
  struct RastPort * _ClipBlit_srcRP = (srcRP); \
  LONG _ClipBlit_xSrc = (xSrc); \
  LONG _ClipBlit_ySrc = (ySrc); \
  struct RastPort * _ClipBlit_destRP = (destRP); \
  LONG _ClipBlit_xDest = (xDest); \
  LONG _ClipBlit_yDest = (yDest); \
  LONG _ClipBlit_xSize = (xSize); \
  LONG _ClipBlit_ySize = (ySize); \
  ULONG _ClipBlit_minterm = (minterm); \
  ({ \
  register char * _ClipBlit__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), struct RastPort * __asm("a1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"), ULONG __asm("d6"))) \
  (_ClipBlit__bn - 552))(_ClipBlit__bn, _ClipBlit_srcRP, _ClipBlit_xSrc, _ClipBlit_ySrc, _ClipBlit_destRP, _ClipBlit_xDest, _ClipBlit_yDest, _ClipBlit_xSize, _ClipBlit_ySize, _ClipBlit_minterm); \
});})

#define XorRectRegion(region, rectangle) ({ \
  struct Region * _XorRectRegion_region = (region); \
  CONST struct Rectangle * _XorRectRegion_rectangle = (rectangle); \
  ({ \
  register char * _XorRectRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Region * __asm("a0"), CONST struct Rectangle * __asm("a1"))) \
  (_XorRectRegion__bn - 558))(_XorRectRegion__bn, _XorRectRegion_region, _XorRectRegion_rectangle); \
});})

#define FreeCprList(cprList) ({ \
  struct cprlist * _FreeCprList_cprList = (cprList); \
  ({ \
  register char * _FreeCprList__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct cprlist * __asm("a0"))) \
  (_FreeCprList__bn - 564))(_FreeCprList__bn, _FreeCprList_cprList); \
});})

#define GetColorMap(entries) ({ \
  LONG _GetColorMap_entries = (entries); \
  ({ \
  register char * _GetColorMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct ColorMap * (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_GetColorMap__bn - 570))(_GetColorMap__bn, _GetColorMap_entries); \
});})

#define FreeColorMap(colorMap) ({ \
  struct ColorMap * _FreeColorMap_colorMap = (colorMap); \
  ({ \
  register char * _FreeColorMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ColorMap * __asm("a0"))) \
  (_FreeColorMap__bn - 576))(_FreeColorMap__bn, _FreeColorMap_colorMap); \
});})

#define GetRGB4(colorMap, entry) ({ \
  struct ColorMap * _GetRGB4_colorMap = (colorMap); \
  LONG _GetRGB4_entry = (entry); \
  ({ \
  register char * _GetRGB4__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), LONG __asm("d0"))) \
  (_GetRGB4__bn - 582))(_GetRGB4__bn, _GetRGB4_colorMap, _GetRGB4_entry); \
});})

#define ScrollVPort(vp) ({ \
  struct ViewPort * _ScrollVPort_vp = (vp); \
  ({ \
  register char * _ScrollVPort__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"))) \
  (_ScrollVPort__bn - 588))(_ScrollVPort__bn, _ScrollVPort_vp); \
});})

#define UCopperListInit(uCopList, n) ({ \
  struct UCopList * _UCopperListInit_uCopList = (uCopList); \
  LONG _UCopperListInit_n = (n); \
  ({ \
  register char * _UCopperListInit__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct CopList * (*)(char * __asm("a6"), struct UCopList * __asm("a0"), LONG __asm("d0"))) \
  (_UCopperListInit__bn - 594))(_UCopperListInit__bn, _UCopperListInit_uCopList, _UCopperListInit_n); \
});})

#define FreeGBuffers(anOb, rp, flag) ({ \
  struct AnimOb * _FreeGBuffers_anOb = (anOb); \
  struct RastPort * _FreeGBuffers_rp = (rp); \
  LONG _FreeGBuffers_flag = (flag); \
  ({ \
  register char * _FreeGBuffers__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct AnimOb * __asm("a0"), struct RastPort * __asm("a1"), LONG __asm("d0"))) \
  (_FreeGBuffers__bn - 600))(_FreeGBuffers__bn, _FreeGBuffers_anOb, _FreeGBuffers_rp, _FreeGBuffers_flag); \
});})

#define BltBitMapRastPort(srcBitMap, xSrc, ySrc, destRP, xDest, yDest, xSize, ySize, minterm) ({ \
  CONST struct BitMap * _BltBitMapRastPort_srcBitMap = (srcBitMap); \
  LONG _BltBitMapRastPort_xSrc = (xSrc); \
  LONG _BltBitMapRastPort_ySrc = (ySrc); \
  struct RastPort * _BltBitMapRastPort_destRP = (destRP); \
  LONG _BltBitMapRastPort_xDest = (xDest); \
  LONG _BltBitMapRastPort_yDest = (yDest); \
  LONG _BltBitMapRastPort_xSize = (xSize); \
  LONG _BltBitMapRastPort_ySize = (ySize); \
  ULONG _BltBitMapRastPort_minterm = (minterm); \
  ({ \
  register char * _BltBitMapRastPort__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct BitMap * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), struct RastPort * __asm("a1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"), ULONG __asm("d6"))) \
  (_BltBitMapRastPort__bn - 606))(_BltBitMapRastPort__bn, _BltBitMapRastPort_srcBitMap, _BltBitMapRastPort_xSrc, _BltBitMapRastPort_ySrc, _BltBitMapRastPort_destRP, _BltBitMapRastPort_xDest, _BltBitMapRastPort_yDest, _BltBitMapRastPort_xSize, _BltBitMapRastPort_ySize, _BltBitMapRastPort_minterm); \
});})

#define OrRegionRegion(srcRegion, destRegion) ({ \
  CONST struct Region * _OrRegionRegion_srcRegion = (srcRegion); \
  struct Region * _OrRegionRegion_destRegion = (destRegion); \
  ({ \
  register char * _OrRegionRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct Region * __asm("a0"), struct Region * __asm("a1"))) \
  (_OrRegionRegion__bn - 612))(_OrRegionRegion__bn, _OrRegionRegion_srcRegion, _OrRegionRegion_destRegion); \
});})

#define XorRegionRegion(srcRegion, destRegion) ({ \
  CONST struct Region * _XorRegionRegion_srcRegion = (srcRegion); \
  struct Region * _XorRegionRegion_destRegion = (destRegion); \
  ({ \
  register char * _XorRegionRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct Region * __asm("a0"), struct Region * __asm("a1"))) \
  (_XorRegionRegion__bn - 618))(_XorRegionRegion__bn, _XorRegionRegion_srcRegion, _XorRegionRegion_destRegion); \
});})

#define AndRegionRegion(srcRegion, destRegion) ({ \
  CONST struct Region * _AndRegionRegion_srcRegion = (srcRegion); \
  struct Region * _AndRegionRegion_destRegion = (destRegion); \
  ({ \
  register char * _AndRegionRegion__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct Region * __asm("a0"), struct Region * __asm("a1"))) \
  (_AndRegionRegion__bn - 624))(_AndRegionRegion__bn, _AndRegionRegion_srcRegion, _AndRegionRegion_destRegion); \
});})

#define SetRGB4CM(colorMap, index, red, green, blue) ({ \
  struct ColorMap * _SetRGB4CM_colorMap = (colorMap); \
  LONG _SetRGB4CM_index = (index); \
  ULONG _SetRGB4CM_red = (red); \
  ULONG _SetRGB4CM_green = (green); \
  ULONG _SetRGB4CM_blue = (blue); \
  ({ \
  register char * _SetRGB4CM__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), LONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_SetRGB4CM__bn - 630))(_SetRGB4CM__bn, _SetRGB4CM_colorMap, _SetRGB4CM_index, _SetRGB4CM_red, _SetRGB4CM_green, _SetRGB4CM_blue); \
});})

#define BltMaskBitMapRastPort(srcBitMap, xSrc, ySrc, destRP, xDest, yDest, xSize, ySize, minterm, bltMask) ({ \
  CONST struct BitMap * _BltMaskBitMapRastPort_srcBitMap = (srcBitMap); \
  LONG _BltMaskBitMapRastPort_xSrc = (xSrc); \
  LONG _BltMaskBitMapRastPort_ySrc = (ySrc); \
  struct RastPort * _BltMaskBitMapRastPort_destRP = (destRP); \
  LONG _BltMaskBitMapRastPort_xDest = (xDest); \
  LONG _BltMaskBitMapRastPort_yDest = (yDest); \
  LONG _BltMaskBitMapRastPort_xSize = (xSize); \
  LONG _BltMaskBitMapRastPort_ySize = (ySize); \
  ULONG _BltMaskBitMapRastPort_minterm = (minterm); \
  CONST PLANEPTR _BltMaskBitMapRastPort_bltMask = (bltMask); \
  ({ \
  register char * _BltMaskBitMapRastPort__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct BitMap * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), struct RastPort * __asm("a1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"), ULONG __asm("d6"), CONST PLANEPTR __asm("a2"))) \
  (_BltMaskBitMapRastPort__bn - 636))(_BltMaskBitMapRastPort__bn, _BltMaskBitMapRastPort_srcBitMap, _BltMaskBitMapRastPort_xSrc, _BltMaskBitMapRastPort_ySrc, _BltMaskBitMapRastPort_destRP, _BltMaskBitMapRastPort_xDest, _BltMaskBitMapRastPort_yDest, _BltMaskBitMapRastPort_xSize, _BltMaskBitMapRastPort_ySize, _BltMaskBitMapRastPort_minterm, _BltMaskBitMapRastPort_bltMask); \
});})

#define AttemptLockLayerRom(layer) ({ \
  struct Layer * _AttemptLockLayerRom_layer = (layer); \
  ({ \
  register char * _AttemptLockLayerRom__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Layer * __asm("a5"))) \
  (_AttemptLockLayerRom__bn - 654))(_AttemptLockLayerRom__bn, _AttemptLockLayerRom_layer); \
});})

#define GfxNew(gfxNodeType) ({ \
  ULONG _GfxNew_gfxNodeType = (gfxNodeType); \
  ({ \
  register char * _GfxNew__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_GfxNew__bn - 660))(_GfxNew__bn, _GfxNew_gfxNodeType); \
});})

#define GfxFree(gfxNodePtr) ({ \
  APTR _GfxFree_gfxNodePtr = (gfxNodePtr); \
  ({ \
  register char * _GfxFree__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_GfxFree__bn - 666))(_GfxFree__bn, _GfxFree_gfxNodePtr); \
});})

#define GfxAssociate(associateNode, gfxNodePtr) ({ \
  APTR _GfxAssociate_associateNode = (associateNode); \
  APTR _GfxAssociate_gfxNodePtr = (gfxNodePtr); \
  ({ \
  register char * _GfxAssociate__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"), APTR __asm("a1"))) \
  (_GfxAssociate__bn - 672))(_GfxAssociate__bn, _GfxAssociate_associateNode, _GfxAssociate_gfxNodePtr); \
});})

#define BitMapScale(bitScaleArgs) ({ \
  struct BitScaleArgs * _BitMapScale_bitScaleArgs = (bitScaleArgs); \
  ({ \
  register char * _BitMapScale__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct BitScaleArgs * __asm("a0"))) \
  (_BitMapScale__bn - 678))(_BitMapScale__bn, _BitMapScale_bitScaleArgs); \
});})

#define ScalerDiv(factor, numerator, denominator) ({ \
  ULONG _ScalerDiv_factor = (factor); \
  ULONG _ScalerDiv_numerator = (numerator); \
  ULONG _ScalerDiv_denominator = (denominator); \
  ({ \
  register char * _ScalerDiv__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"))) \
  (_ScalerDiv__bn - 684))(_ScalerDiv__bn, _ScalerDiv_factor, _ScalerDiv_numerator, _ScalerDiv_denominator); \
});})

#define TextExtent(rp, string, count, textExtent) ({ \
  struct RastPort * _TextExtent_rp = (rp); \
  CONST_STRPTR _TextExtent_string = (string); \
  LONG _TextExtent_count = (count); \
  struct TextExtent * _TextExtent_textExtent = (textExtent); \
  ({ \
  register char * _TextExtent__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((WORD (*)(char * __asm("a6"), struct RastPort * __asm("a1"), CONST_STRPTR __asm("a0"), LONG __asm("d0"), struct TextExtent * __asm("a2"))) \
  (_TextExtent__bn - 690))(_TextExtent__bn, _TextExtent_rp, _TextExtent_string, _TextExtent_count, _TextExtent_textExtent); \
});})

#define TextFit(rp, string, strLen, textExtent, constrainingExtent, strDirection, constrainingBitWidth, constrainingBitHeight) ({ \
  struct RastPort * _TextFit_rp = (rp); \
  CONST_STRPTR _TextFit_string = (string); \
  ULONG _TextFit_strLen = (strLen); \
  CONST struct TextExtent * _TextFit_textExtent = (textExtent); \
  CONST struct TextExtent * _TextFit_constrainingExtent = (constrainingExtent); \
  LONG _TextFit_strDirection = (strDirection); \
  ULONG _TextFit_constrainingBitWidth = (constrainingBitWidth); \
  ULONG _TextFit_constrainingBitHeight = (constrainingBitHeight); \
  ({ \
  register char * _TextFit__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a1"), CONST_STRPTR __asm("a0"), ULONG __asm("d0"), CONST struct TextExtent * __asm("a2"), CONST struct TextExtent * __asm("a3"), LONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_TextFit__bn - 696))(_TextFit__bn, _TextFit_rp, _TextFit_string, _TextFit_strLen, _TextFit_textExtent, _TextFit_constrainingExtent, _TextFit_strDirection, _TextFit_constrainingBitWidth, _TextFit_constrainingBitHeight); \
});})

#define GfxLookUp(associateNode) ({ \
  CONST_APTR _GfxLookUp_associateNode = (associateNode); \
  ({ \
  register char * _GfxLookUp__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), CONST_APTR __asm("a0"))) \
  (_GfxLookUp__bn - 702))(_GfxLookUp__bn, _GfxLookUp_associateNode); \
});})

#define VideoControl(colorMap, tagarray) ({ \
  struct ColorMap * _VideoControl_colorMap = (colorMap); \
  struct TagItem * _VideoControl_tagarray = (tagarray); \
  ({ \
  register char * _VideoControl__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_VideoControl__bn - 708))(_VideoControl__bn, _VideoControl_colorMap, _VideoControl_tagarray); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___VideoControlTags(struct GfxBase * GfxBase, struct ColorMap * colorMap, ULONG tagarray, ...)
{
  return VideoControl(colorMap, (struct TagItem *) &tagarray);
}

#define VideoControlTags(colorMap...) ___VideoControlTags(GRAPHICS_BASE_NAME, colorMap)
#endif

#define OpenMonitor(monitorName, displayID) ({ \
  CONST_STRPTR _OpenMonitor_monitorName = (monitorName); \
  ULONG _OpenMonitor_displayID = (displayID); \
  ({ \
  register char * _OpenMonitor__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct MonitorSpec * (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"), ULONG __asm("d0"))) \
  (_OpenMonitor__bn - 714))(_OpenMonitor__bn, _OpenMonitor_monitorName, _OpenMonitor_displayID); \
});})

#define CloseMonitor(monitorSpec) ({ \
  struct MonitorSpec * _CloseMonitor_monitorSpec = (monitorSpec); \
  ({ \
  register char * _CloseMonitor__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct MonitorSpec * __asm("a0"))) \
  (_CloseMonitor__bn - 720))(_CloseMonitor__bn, _CloseMonitor_monitorSpec); \
});})

#define FindDisplayInfo(displayID) ({ \
  ULONG _FindDisplayInfo_displayID = (displayID); \
  ({ \
  register char * _FindDisplayInfo__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((DisplayInfoHandle (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_FindDisplayInfo__bn - 726))(_FindDisplayInfo__bn, _FindDisplayInfo_displayID); \
});})

#define NextDisplayInfo(displayID) ({ \
  ULONG _NextDisplayInfo_displayID = (displayID); \
  ({ \
  register char * _NextDisplayInfo__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_NextDisplayInfo__bn - 732))(_NextDisplayInfo__bn, _NextDisplayInfo_displayID); \
});})

#define GetDisplayInfoData(handle, buf, size, tagID, displayID) ({ \
  DisplayInfoHandle _GetDisplayInfoData_handle = (handle); \
  APTR _GetDisplayInfoData_buf = (buf); \
  ULONG _GetDisplayInfoData_size = (size); \
  ULONG _GetDisplayInfoData_tagID = (tagID); \
  ULONG _GetDisplayInfoData_displayID = (displayID); \
  ({ \
  register char * _GetDisplayInfoData__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), DisplayInfoHandle __asm("a0"), APTR __asm("a1"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"))) \
  (_GetDisplayInfoData__bn - 756))(_GetDisplayInfoData__bn, _GetDisplayInfoData_handle, _GetDisplayInfoData_buf, _GetDisplayInfoData_size, _GetDisplayInfoData_tagID, _GetDisplayInfoData_displayID); \
});})

#define FontExtent(font, fontExtent) ({ \
  CONST struct TextFont * _FontExtent_font = (font); \
  struct TextExtent * _FontExtent_fontExtent = (fontExtent); \
  ({ \
  register char * _FontExtent__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct TextFont * __asm("a0"), struct TextExtent * __asm("a1"))) \
  (_FontExtent__bn - 762))(_FontExtent__bn, _FontExtent_font, _FontExtent_fontExtent); \
});})

#define ReadPixelLine8(rp, xstart, ystart, width, array, tempRP) ({ \
  struct RastPort * _ReadPixelLine8_rp = (rp); \
  ULONG _ReadPixelLine8_xstart = (xstart); \
  ULONG _ReadPixelLine8_ystart = (ystart); \
  ULONG _ReadPixelLine8_width = (width); \
  UBYTE * _ReadPixelLine8_array = (array); \
  struct RastPort * _ReadPixelLine8_tempRP = (tempRP); \
  ({ \
  register char * _ReadPixelLine8__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), UBYTE * __asm("a2"), struct RastPort * __asm("a1"))) \
  (_ReadPixelLine8__bn - 768))(_ReadPixelLine8__bn, _ReadPixelLine8_rp, _ReadPixelLine8_xstart, _ReadPixelLine8_ystart, _ReadPixelLine8_width, _ReadPixelLine8_array, _ReadPixelLine8_tempRP); \
});})

#define WritePixelLine8(rp, xstart, ystart, width, array, tempRP) ({ \
  struct RastPort * _WritePixelLine8_rp = (rp); \
  ULONG _WritePixelLine8_xstart = (xstart); \
  ULONG _WritePixelLine8_ystart = (ystart); \
  ULONG _WritePixelLine8_width = (width); \
  UBYTE * _WritePixelLine8_array = (array); \
  struct RastPort * _WritePixelLine8_tempRP = (tempRP); \
  ({ \
  register char * _WritePixelLine8__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), UBYTE * __asm("a2"), struct RastPort * __asm("a1"))) \
  (_WritePixelLine8__bn - 774))(_WritePixelLine8__bn, _WritePixelLine8_rp, _WritePixelLine8_xstart, _WritePixelLine8_ystart, _WritePixelLine8_width, _WritePixelLine8_array, _WritePixelLine8_tempRP); \
});})

#define ReadPixelArray8(rp, xstart, ystart, xstop, ystop, array, temprp) ({ \
  struct RastPort * _ReadPixelArray8_rp = (rp); \
  ULONG _ReadPixelArray8_xstart = (xstart); \
  ULONG _ReadPixelArray8_ystart = (ystart); \
  ULONG _ReadPixelArray8_xstop = (xstop); \
  ULONG _ReadPixelArray8_ystop = (ystop); \
  UBYTE * _ReadPixelArray8_array = (array); \
  struct RastPort * _ReadPixelArray8_temprp = (temprp); \
  ({ \
  register char * _ReadPixelArray8__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), UBYTE * __asm("a2"), struct RastPort * __asm("a1"))) \
  (_ReadPixelArray8__bn - 780))(_ReadPixelArray8__bn, _ReadPixelArray8_rp, _ReadPixelArray8_xstart, _ReadPixelArray8_ystart, _ReadPixelArray8_xstop, _ReadPixelArray8_ystop, _ReadPixelArray8_array, _ReadPixelArray8_temprp); \
});})

#define WritePixelArray8(rp, xstart, ystart, xstop, ystop, array, temprp) ({ \
  struct RastPort * _WritePixelArray8_rp = (rp); \
  ULONG _WritePixelArray8_xstart = (xstart); \
  ULONG _WritePixelArray8_ystart = (ystart); \
  ULONG _WritePixelArray8_xstop = (xstop); \
  ULONG _WritePixelArray8_ystop = (ystop); \
  UBYTE * _WritePixelArray8_array = (array); \
  struct RastPort * _WritePixelArray8_temprp = (temprp); \
  ({ \
  register char * _WritePixelArray8__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), UBYTE * __asm("a2"), struct RastPort * __asm("a1"))) \
  (_WritePixelArray8__bn - 786))(_WritePixelArray8__bn, _WritePixelArray8_rp, _WritePixelArray8_xstart, _WritePixelArray8_ystart, _WritePixelArray8_xstop, _WritePixelArray8_ystop, _WritePixelArray8_array, _WritePixelArray8_temprp); \
});})

#define GetVPModeID(vp) ({ \
  CONST struct ViewPort * _GetVPModeID_vp = (vp); \
  ({ \
  register char * _GetVPModeID__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST struct ViewPort * __asm("a0"))) \
  (_GetVPModeID__bn - 792))(_GetVPModeID__bn, _GetVPModeID_vp); \
});})

#define ModeNotAvailable(modeID) ({ \
  ULONG _ModeNotAvailable_modeID = (modeID); \
  ({ \
  register char * _ModeNotAvailable__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_ModeNotAvailable__bn - 798))(_ModeNotAvailable__bn, _ModeNotAvailable_modeID); \
});})

#define WeighTAMatch(reqTextAttr, targetTextAttr, targetTags) ({ \
  CONST struct TextAttr * _WeighTAMatch_reqTextAttr = (reqTextAttr); \
  CONST struct TextAttr * _WeighTAMatch_targetTextAttr = (targetTextAttr); \
  CONST struct TagItem * _WeighTAMatch_targetTags = (targetTags); \
  ({ \
  register char * _WeighTAMatch__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((WORD (*)(char * __asm("a6"), CONST struct TextAttr * __asm("a0"), CONST struct TextAttr * __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_WeighTAMatch__bn - 804))(_WeighTAMatch__bn, _WeighTAMatch_reqTextAttr, _WeighTAMatch_targetTextAttr, _WeighTAMatch_targetTags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ WORD ___WeighTAMatchTags(struct GfxBase * GfxBase, CONST struct TextAttr * reqTextAttr, CONST struct TextAttr * targetTextAttr, ULONG targetTags, ...)
{
  return WeighTAMatch(reqTextAttr, targetTextAttr, (CONST struct TagItem *) &targetTags);
}

#define WeighTAMatchTags(reqTextAttr, targetTextAttr...) ___WeighTAMatchTags(GRAPHICS_BASE_NAME, reqTextAttr, targetTextAttr)
#endif

#define EraseRect(rp, xMin, yMin, xMax, yMax) ({ \
  struct RastPort * _EraseRect_rp = (rp); \
  LONG _EraseRect_xMin = (xMin); \
  LONG _EraseRect_yMin = (yMin); \
  LONG _EraseRect_xMax = (xMax); \
  LONG _EraseRect_yMax = (yMax); \
  ({ \
  register char * _EraseRect__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_EraseRect__bn - 810))(_EraseRect__bn, _EraseRect_rp, _EraseRect_xMin, _EraseRect_yMin, _EraseRect_xMax, _EraseRect_yMax); \
});})

#define ExtendFont(font, fontTags) ({ \
  struct TextFont * _ExtendFont_font = (font); \
  CONST struct TagItem * _ExtendFont_fontTags = (fontTags); \
  ({ \
  register char * _ExtendFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct TextFont * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_ExtendFont__bn - 816))(_ExtendFont__bn, _ExtendFont_font, _ExtendFont_fontTags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___ExtendFontTags(struct GfxBase * GfxBase, struct TextFont * font, ULONG fontTags, ...)
{
  return ExtendFont(font, (CONST struct TagItem *) &fontTags);
}

#define ExtendFontTags(font...) ___ExtendFontTags(GRAPHICS_BASE_NAME, font)
#endif

#define StripFont(font) ({ \
  struct TextFont * _StripFont_font = (font); \
  ({ \
  register char * _StripFont__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct TextFont * __asm("a0"))) \
  (_StripFont__bn - 822))(_StripFont__bn, _StripFont_font); \
});})

#define CalcIVG(v, vp) ({ \
  struct View * _CalcIVG_v = (v); \
  struct ViewPort * _CalcIVG_vp = (vp); \
  ({ \
  register char * _CalcIVG__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), struct View * __asm("a0"), struct ViewPort * __asm("a1"))) \
  (_CalcIVG__bn - 828))(_CalcIVG__bn, _CalcIVG_v, _CalcIVG_vp); \
});})

#define AttachPalExtra(cm, vp) ({ \
  struct ColorMap * _AttachPalExtra_cm = (cm); \
  struct ViewPort * _AttachPalExtra_vp = (vp); \
  ({ \
  register char * _AttachPalExtra__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), struct ViewPort * __asm("a1"))) \
  (_AttachPalExtra__bn - 834))(_AttachPalExtra__bn, _AttachPalExtra_cm, _AttachPalExtra_vp); \
});})

#define ObtainBestPenA(cm, r, g, b, tags) ({ \
  struct ColorMap * _ObtainBestPenA_cm = (cm); \
  ULONG _ObtainBestPenA_r = (r); \
  ULONG _ObtainBestPenA_g = (g); \
  ULONG _ObtainBestPenA_b = (b); \
  CONST struct TagItem * _ObtainBestPenA_tags = (tags); \
  ({ \
  register char * _ObtainBestPenA__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), CONST struct TagItem * __asm("a1"))) \
  (_ObtainBestPenA__bn - 840))(_ObtainBestPenA__bn, _ObtainBestPenA_cm, _ObtainBestPenA_r, _ObtainBestPenA_g, _ObtainBestPenA_b, _ObtainBestPenA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___ObtainBestPen(struct GfxBase * GfxBase, struct ColorMap * cm, ULONG r, ULONG g, ULONG b, ULONG tags, ...)
{
  return ObtainBestPenA(cm, r, g, b, (CONST struct TagItem *) &tags);
}

#define ObtainBestPen(cm, r, g, b...) ___ObtainBestPen(GRAPHICS_BASE_NAME, cm, r, g, b)
#endif

#define SetRGB32(vp, n, r, g, b) ({ \
  struct ViewPort * _SetRGB32_vp = (vp); \
  ULONG _SetRGB32_n = (n); \
  ULONG _SetRGB32_r = (r); \
  ULONG _SetRGB32_g = (g); \
  ULONG _SetRGB32_b = (b); \
  ({ \
  register char * _SetRGB32__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_SetRGB32__bn - 852))(_SetRGB32__bn, _SetRGB32_vp, _SetRGB32_n, _SetRGB32_r, _SetRGB32_g, _SetRGB32_b); \
});})

#define GetAPen(rp) ({ \
  struct RastPort * _GetAPen_rp = (rp); \
  ({ \
  register char * _GetAPen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"))) \
  (_GetAPen__bn - 858))(_GetAPen__bn, _GetAPen_rp); \
});})

#define GetBPen(rp) ({ \
  struct RastPort * _GetBPen_rp = (rp); \
  ({ \
  register char * _GetBPen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"))) \
  (_GetBPen__bn - 864))(_GetBPen__bn, _GetBPen_rp); \
});})

#define GetDrMd(rp) ({ \
  struct RastPort * _GetDrMd_rp = (rp); \
  ({ \
  register char * _GetDrMd__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"))) \
  (_GetDrMd__bn - 870))(_GetDrMd__bn, _GetDrMd_rp); \
});})

#define GetOutlinePen(rp) ({ \
  struct RastPort * _GetOutlinePen_rp = (rp); \
  ({ \
  register char * _GetOutlinePen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"))) \
  (_GetOutlinePen__bn - 876))(_GetOutlinePen__bn, _GetOutlinePen_rp); \
});})

#define LoadRGB32(vp, table) ({ \
  struct ViewPort * _LoadRGB32_vp = (vp); \
  CONST ULONG * _LoadRGB32_table = (table); \
  ({ \
  register char * _LoadRGB32__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), CONST ULONG * __asm("a1"))) \
  (_LoadRGB32__bn - 882))(_LoadRGB32__bn, _LoadRGB32_vp, _LoadRGB32_table); \
});})

#define SetChipRev(want) ({ \
  ULONG _SetChipRev_want = (want); \
  ({ \
  register char * _SetChipRev__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_SetChipRev__bn - 888))(_SetChipRev__bn, _SetChipRev_want); \
});})

#define SetABPenDrMd(rp, apen, bpen, drawmode) ({ \
  struct RastPort * _SetABPenDrMd_rp = (rp); \
  ULONG _SetABPenDrMd_apen = (apen); \
  ULONG _SetABPenDrMd_bpen = (bpen); \
  ULONG _SetABPenDrMd_drawmode = (drawmode); \
  ({ \
  register char * _SetABPenDrMd__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"))) \
  (_SetABPenDrMd__bn - 894))(_SetABPenDrMd__bn, _SetABPenDrMd_rp, _SetABPenDrMd_apen, _SetABPenDrMd_bpen, _SetABPenDrMd_drawmode); \
});})

#define GetRGB32(cm, firstcolor, ncolors, table) ({ \
  CONST struct ColorMap * _GetRGB32_cm = (cm); \
  ULONG _GetRGB32_firstcolor = (firstcolor); \
  ULONG _GetRGB32_ncolors = (ncolors); \
  ULONG * _GetRGB32_table = (table); \
  ({ \
  register char * _GetRGB32__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct ColorMap * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG * __asm("a1"))) \
  (_GetRGB32__bn - 900))(_GetRGB32__bn, _GetRGB32_cm, _GetRGB32_firstcolor, _GetRGB32_ncolors, _GetRGB32_table); \
});})

#define AllocBitMap(sizex, sizey, depth, flags, friend_bitmap) ({ \
  ULONG _AllocBitMap_sizex = (sizex); \
  ULONG _AllocBitMap_sizey = (sizey); \
  ULONG _AllocBitMap_depth = (depth); \
  ULONG _AllocBitMap_flags = (flags); \
  CONST struct BitMap * _AllocBitMap_friend_bitmap = (friend_bitmap); \
  ({ \
  register char * _AllocBitMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct BitMap * (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), CONST struct BitMap * __asm("a0"))) \
  (_AllocBitMap__bn - 918))(_AllocBitMap__bn, _AllocBitMap_sizex, _AllocBitMap_sizey, _AllocBitMap_depth, _AllocBitMap_flags, _AllocBitMap_friend_bitmap); \
});})

#define FreeBitMap(bm) ({ \
  struct BitMap * _FreeBitMap_bm = (bm); \
  ({ \
  register char * _FreeBitMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct BitMap * __asm("a0"))) \
  (_FreeBitMap__bn - 924))(_FreeBitMap__bn, _FreeBitMap_bm); \
});})

#define GetExtSpriteA(ss, tags) ({ \
  struct ExtSprite * _GetExtSpriteA_ss = (ss); \
  CONST struct TagItem * _GetExtSpriteA_tags = (tags); \
  ({ \
  register char * _GetExtSpriteA__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct ExtSprite * __asm("a2"), CONST struct TagItem * __asm("a1"))) \
  (_GetExtSpriteA__bn - 930))(_GetExtSpriteA__bn, _GetExtSpriteA_ss, _GetExtSpriteA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___GetExtSprite(struct GfxBase * GfxBase, struct ExtSprite * ss, ULONG tags, ...)
{
  return GetExtSpriteA(ss, (CONST struct TagItem *) &tags);
}

#define GetExtSprite(ss...) ___GetExtSprite(GRAPHICS_BASE_NAME, ss)
#endif

#define CoerceMode(vp, monitorid, flags) ({ \
  struct ViewPort * _CoerceMode_vp = (vp); \
  ULONG _CoerceMode_monitorid = (monitorid); \
  ULONG _CoerceMode_flags = (flags); \
  ({ \
  register char * _CoerceMode__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_CoerceMode__bn - 936))(_CoerceMode__bn, _CoerceMode_vp, _CoerceMode_monitorid, _CoerceMode_flags); \
});})

#define ChangeVPBitMap(vp, bm, db) ({ \
  struct ViewPort * _ChangeVPBitMap_vp = (vp); \
  struct BitMap * _ChangeVPBitMap_bm = (bm); \
  struct DBufInfo * _ChangeVPBitMap_db = (db); \
  ({ \
  register char * _ChangeVPBitMap__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), struct BitMap * __asm("a1"), struct DBufInfo * __asm("a2"))) \
  (_ChangeVPBitMap__bn - 942))(_ChangeVPBitMap__bn, _ChangeVPBitMap_vp, _ChangeVPBitMap_bm, _ChangeVPBitMap_db); \
});})

#define ReleasePen(cm, n) ({ \
  struct ColorMap * _ReleasePen_cm = (cm); \
  ULONG _ReleasePen_n = (n); \
  ({ \
  register char * _ReleasePen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), ULONG __asm("d0"))) \
  (_ReleasePen__bn - 948))(_ReleasePen__bn, _ReleasePen_cm, _ReleasePen_n); \
});})

#define ObtainPen(cm, n, r, g, b, f) ({ \
  struct ColorMap * _ObtainPen_cm = (cm); \
  ULONG _ObtainPen_n = (n); \
  ULONG _ObtainPen_r = (r); \
  ULONG _ObtainPen_g = (g); \
  ULONG _ObtainPen_b = (b); \
  LONG _ObtainPen_f = (f); \
  ({ \
  register char * _ObtainPen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), LONG __asm("d4"))) \
  (_ObtainPen__bn - 954))(_ObtainPen__bn, _ObtainPen_cm, _ObtainPen_n, _ObtainPen_r, _ObtainPen_g, _ObtainPen_b, _ObtainPen_f); \
});})

#define GetBitMapAttr(bm, attrnum) ({ \
  CONST struct BitMap * _GetBitMapAttr_bm = (bm); \
  ULONG _GetBitMapAttr_attrnum = (attrnum); \
  ({ \
  register char * _GetBitMapAttr__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST struct BitMap * __asm("a0"), ULONG __asm("d1"))) \
  (_GetBitMapAttr__bn - 960))(_GetBitMapAttr__bn, _GetBitMapAttr_bm, _GetBitMapAttr_attrnum); \
});})

#define AllocDBufInfo(vp) ({ \
  struct ViewPort * _AllocDBufInfo_vp = (vp); \
  ({ \
  register char * _AllocDBufInfo__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct DBufInfo * (*)(char * __asm("a6"), struct ViewPort * __asm("a0"))) \
  (_AllocDBufInfo__bn - 966))(_AllocDBufInfo__bn, _AllocDBufInfo_vp); \
});})

#define FreeDBufInfo(dbi) ({ \
  struct DBufInfo * _FreeDBufInfo_dbi = (dbi); \
  ({ \
  register char * _FreeDBufInfo__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct DBufInfo * __asm("a1"))) \
  (_FreeDBufInfo__bn - 972))(_FreeDBufInfo__bn, _FreeDBufInfo_dbi); \
});})

#define SetOutlinePen(rp, pen) ({ \
  struct RastPort * _SetOutlinePen_rp = (rp); \
  ULONG _SetOutlinePen_pen = (pen); \
  ({ \
  register char * _SetOutlinePen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"))) \
  (_SetOutlinePen__bn - 978))(_SetOutlinePen__bn, _SetOutlinePen_rp, _SetOutlinePen_pen); \
});})

#define SetWriteMask(rp, msk) ({ \
  struct RastPort * _SetWriteMask_rp = (rp); \
  ULONG _SetWriteMask_msk = (msk); \
  ({ \
  register char * _SetWriteMask__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"))) \
  (_SetWriteMask__bn - 984))(_SetWriteMask__bn, _SetWriteMask_rp, _SetWriteMask_msk); \
});})

#define SetMaxPen(rp, maxpen) ({ \
  struct RastPort * _SetMaxPen_rp = (rp); \
  ULONG _SetMaxPen_maxpen = (maxpen); \
  ({ \
  register char * _SetMaxPen__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"))) \
  (_SetMaxPen__bn - 990))(_SetMaxPen__bn, _SetMaxPen_rp, _SetMaxPen_maxpen); \
});})

#define SetRGB32CM(cm, n, r, g, b) ({ \
  struct ColorMap * _SetRGB32CM_cm = (cm); \
  ULONG _SetRGB32CM_n = (n); \
  ULONG _SetRGB32CM_r = (r); \
  ULONG _SetRGB32CM_g = (g); \
  ULONG _SetRGB32CM_b = (b); \
  ({ \
  register char * _SetRGB32CM__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ColorMap * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_SetRGB32CM__bn - 996))(_SetRGB32CM__bn, _SetRGB32CM_cm, _SetRGB32CM_n, _SetRGB32CM_r, _SetRGB32CM_g, _SetRGB32CM_b); \
});})

#define ScrollRasterBF(rp, dx, dy, xMin, yMin, xMax, yMax) ({ \
  struct RastPort * _ScrollRasterBF_rp = (rp); \
  LONG _ScrollRasterBF_dx = (dx); \
  LONG _ScrollRasterBF_dy = (dy); \
  LONG _ScrollRasterBF_xMin = (xMin); \
  LONG _ScrollRasterBF_yMin = (yMin); \
  LONG _ScrollRasterBF_xMax = (xMax); \
  LONG _ScrollRasterBF_yMax = (yMax); \
  ({ \
  register char * _ScrollRasterBF__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a1"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"))) \
  (_ScrollRasterBF__bn - 1002))(_ScrollRasterBF__bn, _ScrollRasterBF_rp, _ScrollRasterBF_dx, _ScrollRasterBF_dy, _ScrollRasterBF_xMin, _ScrollRasterBF_yMin, _ScrollRasterBF_xMax, _ScrollRasterBF_yMax); \
});})

#define FindColor(cm, r, g, b, maxcolor) ({ \
  struct ColorMap * _FindColor_cm = (cm); \
  ULONG _FindColor_r = (r); \
  ULONG _FindColor_g = (g); \
  ULONG _FindColor_b = (b); \
  LONG _FindColor_maxcolor = (maxcolor); \
  ({ \
  register char * _FindColor__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct ColorMap * __asm("a3"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), LONG __asm("d4"))) \
  (_FindColor__bn - 1008))(_FindColor__bn, _FindColor_cm, _FindColor_r, _FindColor_g, _FindColor_b, _FindColor_maxcolor); \
});})

#define AllocSpriteDataA(bm, tags) ({ \
  CONST struct BitMap * _AllocSpriteDataA_bm = (bm); \
  CONST struct TagItem * _AllocSpriteDataA_tags = (tags); \
  ({ \
  register char * _AllocSpriteDataA__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((struct ExtSprite * (*)(char * __asm("a6"), CONST struct BitMap * __asm("a2"), CONST struct TagItem * __asm("a1"))) \
  (_AllocSpriteDataA__bn - 1020))(_AllocSpriteDataA__bn, _AllocSpriteDataA_bm, _AllocSpriteDataA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct ExtSprite * ___AllocSpriteData(struct GfxBase * GfxBase, CONST struct BitMap * bm, ULONG tags, ...)
{
  return AllocSpriteDataA(bm, (CONST struct TagItem *) &tags);
}

#define AllocSpriteData(bm...) ___AllocSpriteData(GRAPHICS_BASE_NAME, bm)
#endif

#define ChangeExtSpriteA(vp, oldsprite, newsprite, tags) ({ \
  struct ViewPort * _ChangeExtSpriteA_vp = (vp); \
  struct ExtSprite * _ChangeExtSpriteA_oldsprite = (oldsprite); \
  struct ExtSprite * _ChangeExtSpriteA_newsprite = (newsprite); \
  CONST struct TagItem * _ChangeExtSpriteA_tags = (tags); \
  ({ \
  register char * _ChangeExtSpriteA__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct ViewPort * __asm("a0"), struct ExtSprite * __asm("a1"), struct ExtSprite * __asm("a2"), CONST struct TagItem * __asm("a3"))) \
  (_ChangeExtSpriteA__bn - 1026))(_ChangeExtSpriteA__bn, _ChangeExtSpriteA_vp, _ChangeExtSpriteA_oldsprite, _ChangeExtSpriteA_newsprite, _ChangeExtSpriteA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___ChangeExtSprite(struct GfxBase * GfxBase, struct ViewPort * vp, struct ExtSprite * oldsprite, struct ExtSprite * newsprite, ULONG tags, ...)
{
  return ChangeExtSpriteA(vp, oldsprite, newsprite, (CONST struct TagItem *) &tags);
}

#define ChangeExtSprite(vp, oldsprite, newsprite...) ___ChangeExtSprite(GRAPHICS_BASE_NAME, vp, oldsprite, newsprite)
#endif

#define FreeSpriteData(sp) ({ \
  struct ExtSprite * _FreeSpriteData_sp = (sp); \
  ({ \
  register char * _FreeSpriteData__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ExtSprite * __asm("a2"))) \
  (_FreeSpriteData__bn - 1032))(_FreeSpriteData__bn, _FreeSpriteData_sp); \
});})

#define SetRPAttrsA(rp, tags) ({ \
  struct RastPort * _SetRPAttrsA_rp = (rp); \
  CONST struct TagItem * _SetRPAttrsA_tags = (tags); \
  ({ \
  register char * _SetRPAttrsA__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_SetRPAttrsA__bn - 1038))(_SetRPAttrsA__bn, _SetRPAttrsA_rp, _SetRPAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetRPAttrs(struct GfxBase * GfxBase, struct RastPort * rp, ULONG tags, ...)
{
  SetRPAttrsA(rp, (CONST struct TagItem *) &tags);
}

#define SetRPAttrs(rp...) ___SetRPAttrs(GRAPHICS_BASE_NAME, rp)
#endif

#define GetRPAttrsA(rp, tags) ({ \
  struct RastPort * _GetRPAttrsA_rp = (rp); \
  CONST struct TagItem * _GetRPAttrsA_tags = (tags); \
  ({ \
  register char * _GetRPAttrsA__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_GetRPAttrsA__bn - 1044))(_GetRPAttrsA__bn, _GetRPAttrsA_rp, _GetRPAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___GetRPAttrs(struct GfxBase * GfxBase, struct RastPort * rp, ULONG tags, ...)
{
  GetRPAttrsA(rp, (CONST struct TagItem *) &tags);
}

#define GetRPAttrs(rp...) ___GetRPAttrs(GRAPHICS_BASE_NAME, rp)
#endif

#define BestModeIDA(tags) ({ \
  CONST struct TagItem * _BestModeIDA_tags = (tags); \
  ({ \
  register char * _BestModeIDA__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST struct TagItem * __asm("a0"))) \
  (_BestModeIDA__bn - 1050))(_BestModeIDA__bn, _BestModeIDA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___BestModeID(struct GfxBase * GfxBase, ULONG tags, ...)
{
  return BestModeIDA((CONST struct TagItem *) &tags);
}

#define BestModeID(tags...) ___BestModeID(GRAPHICS_BASE_NAME, tags)
#endif

#define WriteChunkyPixels(rp, xstart, ystart, xstop, ystop, array, bytesperrow) ({ \
  struct RastPort * _WriteChunkyPixels_rp = (rp); \
  ULONG _WriteChunkyPixels_xstart = (xstart); \
  ULONG _WriteChunkyPixels_ystart = (ystart); \
  ULONG _WriteChunkyPixels_xstop = (xstop); \
  ULONG _WriteChunkyPixels_ystop = (ystop); \
  UBYTE * _WriteChunkyPixels_array = (array); \
  LONG _WriteChunkyPixels_bytesperrow = (bytesperrow); \
  ({ \
  register char * _WriteChunkyPixels__bn __asm("a6") = (char *) (GRAPHICS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), UBYTE * __asm("a2"), LONG __asm("d4"))) \
  (_WriteChunkyPixels__bn - 1056))(_WriteChunkyPixels__bn, _WriteChunkyPixels_rp, _WriteChunkyPixels_xstart, _WriteChunkyPixels_ystart, _WriteChunkyPixels_xstop, _WriteChunkyPixels_ystop, _WriteChunkyPixels_array, _WriteChunkyPixels_bytesperrow); \
});})

#endif /*  _INLINE_GRAPHICS_H  */
