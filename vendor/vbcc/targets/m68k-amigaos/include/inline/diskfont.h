#ifndef _INLINE_DISKFONT_H
#define _INLINE_DISKFONT_H

#ifndef CLIB_DISKFONT_PROTOS_H
#define CLIB_DISKFONT_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  DOS_DOS_H
#include <dos/dos.h>
#endif
#ifndef  DISKFONT_DISKFONT_H
#include <diskfont/diskfont.h>
#endif

#ifndef DISKFONT_BASE_NAME
#define DISKFONT_BASE_NAME DiskfontBase
#endif

#define OpenDiskFont(textAttr) ({ \
  struct TextAttr * _OpenDiskFont_textAttr = (textAttr); \
  ({ \
  register char * _OpenDiskFont__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((struct TextFont * (*)(char * __asm("a6"), struct TextAttr * __asm("a0"))) \
  (_OpenDiskFont__bn - 30))(_OpenDiskFont__bn, _OpenDiskFont_textAttr); \
});})

#define AvailFonts(buffer, bufBytes, flags) ({ \
  struct AvailFontsHeader * _AvailFonts_buffer = (buffer); \
  LONG _AvailFonts_bufBytes = (bufBytes); \
  ULONG _AvailFonts_flags = (flags); \
  ({ \
  register char * _AvailFonts__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct AvailFontsHeader * __asm("a0"), LONG __asm("d0"), ULONG __asm("d1"))) \
  (_AvailFonts__bn - 36))(_AvailFonts__bn, _AvailFonts_buffer, _AvailFonts_bufBytes, _AvailFonts_flags); \
});})

#define NewFontContents(fontsLock, fontName) ({ \
  BPTR _NewFontContents_fontsLock = (fontsLock); \
  CONST_STRPTR _NewFontContents_fontName = (fontName); \
  ({ \
  register char * _NewFontContents__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((struct FontContentsHeader * (*)(char * __asm("a6"), BPTR __asm("a0"), CONST_STRPTR __asm("a1"))) \
  (_NewFontContents__bn - 42))(_NewFontContents__bn, _NewFontContents_fontsLock, _NewFontContents_fontName); \
});})

#define DisposeFontContents(fontContentsHeader) ({ \
  struct FontContentsHeader * _DisposeFontContents_fontContentsHeader = (fontContentsHeader); \
  ({ \
  register char * _DisposeFontContents__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct FontContentsHeader * __asm("a1"))) \
  (_DisposeFontContents__bn - 48))(_DisposeFontContents__bn, _DisposeFontContents_fontContentsHeader); \
});})

#define NewScaledDiskFont(sourceFont, destTextAttr) ({ \
  struct TextFont * _NewScaledDiskFont_sourceFont = (sourceFont); \
  struct TextAttr * _NewScaledDiskFont_destTextAttr = (destTextAttr); \
  ({ \
  register char * _NewScaledDiskFont__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((struct DiskFont * (*)(char * __asm("a6"), struct TextFont * __asm("a0"), struct TextAttr * __asm("a1"))) \
  (_NewScaledDiskFont__bn - 54))(_NewScaledDiskFont__bn, _NewScaledDiskFont_sourceFont, _NewScaledDiskFont_destTextAttr); \
});})

#define GetDiskFontCtrl(tagid) ({ \
  LONG _GetDiskFontCtrl_tagid = (tagid); \
  ({ \
  register char * _GetDiskFontCtrl__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_GetDiskFontCtrl__bn - 60))(_GetDiskFontCtrl__bn, _GetDiskFontCtrl_tagid); \
});})

#define SetDiskFontCtrlA(taglist) ({ \
  CONST struct TagItem * _SetDiskFontCtrlA_taglist = (taglist); \
  ({ \
  register char * _SetDiskFontCtrlA__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct TagItem * __asm("a0"))) \
  (_SetDiskFontCtrlA__bn - 66))(_SetDiskFontCtrlA__bn, _SetDiskFontCtrlA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetDiskFontCtrl(struct Library * DiskfontBase, Tag taglist, ...)
{
  SetDiskFontCtrlA((CONST struct TagItem *) &taglist);
}

#define SetDiskFontCtrl(tags...) ___SetDiskFontCtrl(DISKFONT_BASE_NAME, tags)
#endif

#define EOpenEngine(EEngine) ({ \
  struct EGlyphEngine * _EOpenEngine_EEngine = (EEngine); \
  ({ \
  register char * _EOpenEngine__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct EGlyphEngine * __asm("a0"))) \
  (_EOpenEngine__bn - 72))(_EOpenEngine__bn, _EOpenEngine_EEngine); \
});})

#define ECloseEngine(EEngine) ({ \
  struct EGlyphEngine * _ECloseEngine_EEngine = (EEngine); \
  ({ \
  register char * _ECloseEngine__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct EGlyphEngine * __asm("a0"))) \
  (_ECloseEngine__bn - 78))(_ECloseEngine__bn, _ECloseEngine_EEngine); \
});})

#define ESetInfoA(EEngine, taglist) ({ \
  struct EGlyphEngine * _ESetInfoA_EEngine = (EEngine); \
  CONST struct TagItem * _ESetInfoA_taglist = (taglist); \
  ({ \
  register char * _ESetInfoA__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct EGlyphEngine * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_ESetInfoA__bn - 84))(_ESetInfoA__bn, _ESetInfoA_EEngine, _ESetInfoA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___ESetInfo(struct Library * DiskfontBase, struct EGlyphEngine * EEngine, ...)
{
  return ESetInfoA(EEngine, (CONST struct TagItem *) ((ULONG) &EEngine + sizeof(struct EGlyphEngine *)));
}

#define ESetInfo(EEngine...) ___ESetInfo(DISKFONT_BASE_NAME, EEngine)
#endif

#define EObtainInfoA(EEngine, taglist) ({ \
  struct EGlyphEngine * _EObtainInfoA_EEngine = (EEngine); \
  CONST struct TagItem * _EObtainInfoA_taglist = (taglist); \
  ({ \
  register char * _EObtainInfoA__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct EGlyphEngine * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_EObtainInfoA__bn - 90))(_EObtainInfoA__bn, _EObtainInfoA_EEngine, _EObtainInfoA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___EObtainInfo(struct Library * DiskfontBase, struct EGlyphEngine * EEngine, ...)
{
  return EObtainInfoA(EEngine, (CONST struct TagItem *) ((ULONG) &EEngine + sizeof(struct EGlyphEngine *)));
}

#define EObtainInfo(EEngine...) ___EObtainInfo(DISKFONT_BASE_NAME, EEngine)
#endif

#define EReleaseInfoA(EEngine, taglist) ({ \
  struct EGlyphEngine * _EReleaseInfoA_EEngine = (EEngine); \
  CONST struct TagItem * _EReleaseInfoA_taglist = (taglist); \
  ({ \
  register char * _EReleaseInfoA__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct EGlyphEngine * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_EReleaseInfoA__bn - 96))(_EReleaseInfoA__bn, _EReleaseInfoA_EEngine, _EReleaseInfoA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___EReleaseInfo(struct Library * DiskfontBase, struct EGlyphEngine * EEngine, ...)
{
  return EReleaseInfoA(EEngine, (CONST struct TagItem *) ((ULONG) &EEngine + sizeof(struct EGlyphEngine *)));
}

#define EReleaseInfo(EEngine...) ___EReleaseInfo(DISKFONT_BASE_NAME, EEngine)
#endif

#define OpenOutlineFont(name, list, flags) ({ \
  CONST_STRPTR _OpenOutlineFont_name = (name); \
  struct List * _OpenOutlineFont_list = (list); \
  ULONG _OpenOutlineFont_flags = (flags); \
  ({ \
  register char * _OpenOutlineFont__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((struct OutlineFont * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), struct List * __asm("a1"), ULONG __asm("d0"))) \
  (_OpenOutlineFont__bn - 102))(_OpenOutlineFont__bn, _OpenOutlineFont_name, _OpenOutlineFont_list, _OpenOutlineFont_flags); \
});})

#define CloseOutlineFont(olf, list) ({ \
  struct OutlineFont * _CloseOutlineFont_olf = (olf); \
  struct List * _CloseOutlineFont_list = (list); \
  ({ \
  register char * _CloseOutlineFont__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct OutlineFont * __asm("a0"), struct List * __asm("a1"))) \
  (_CloseOutlineFont__bn - 108))(_CloseOutlineFont__bn, _CloseOutlineFont_olf, _CloseOutlineFont_list); \
});})

#define WriteFontContents(fontsLock, fontName, fontContentsHeader) ({ \
  BPTR _WriteFontContents_fontsLock = (fontsLock); \
  CONST_STRPTR _WriteFontContents_fontName = (fontName); \
  CONST struct FontContentsHeader * _WriteFontContents_fontContentsHeader = (fontContentsHeader); \
  ({ \
  register char * _WriteFontContents__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("a0"), CONST_STRPTR __asm("a1"), CONST struct FontContentsHeader * __asm("a2"))) \
  (_WriteFontContents__bn - 114))(_WriteFontContents__bn, _WriteFontContents_fontsLock, _WriteFontContents_fontName, _WriteFontContents_fontContentsHeader); \
});})

#define WriteDiskFontHeaderA(font, fileName, tagList) ({ \
  CONST struct TextFont * _WriteDiskFontHeaderA_font = (font); \
  CONST_STRPTR _WriteDiskFontHeaderA_fileName = (fileName); \
  CONST struct TagItem * _WriteDiskFontHeaderA_tagList = (tagList); \
  ({ \
  register char * _WriteDiskFontHeaderA__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST struct TextFont * __asm("a0"), CONST_STRPTR __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_WriteDiskFontHeaderA__bn - 120))(_WriteDiskFontHeaderA__bn, _WriteDiskFontHeaderA_font, _WriteDiskFontHeaderA_fileName, _WriteDiskFontHeaderA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___WriteDiskFontHeader(struct Library * DiskfontBase, CONST struct TextFont * font, CONST_STRPTR fileName, ...)
{
  return WriteDiskFontHeaderA(font, fileName, (CONST struct TagItem *) ((ULONG) &fileName + sizeof(CONST_STRPTR)));
}

#define WriteDiskFontHeader(font, fileName...) ___WriteDiskFontHeader(DISKFONT_BASE_NAME, font, fileName)
#endif

#define ObtainCharsetInfo(knownTag, knownValue, wantedTag) ({ \
  ULONG _ObtainCharsetInfo_knownTag = (knownTag); \
  ULONG _ObtainCharsetInfo_knownValue = (knownValue); \
  ULONG _ObtainCharsetInfo_wantedTag = (wantedTag); \
  ({ \
  register char * _ObtainCharsetInfo__bn __asm("a6") = (char *) (DISKFONT_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"))) \
  (_ObtainCharsetInfo__bn - 126))(_ObtainCharsetInfo__bn, _ObtainCharsetInfo_knownTag, _ObtainCharsetInfo_knownValue, _ObtainCharsetInfo_wantedTag); \
});})

#endif /*  _INLINE_DISKFONT_H  */
