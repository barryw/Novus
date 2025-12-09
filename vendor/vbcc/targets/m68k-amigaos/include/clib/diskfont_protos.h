#ifndef CLIB_DISKFONT_PROTOS_H
#define CLIB_DISKFONT_PROTOS_H

/*
**	$VER: diskfont_protos.h 47.1 (30.11.2021)
**
**	'C' prototypes. For use with 32 bit integers only.
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  DOS_DOS_H
#include <dos/dos.h>
#endif
#ifndef  DISKFONT_DISKFONT_H
#include <diskfont/diskfont.h>
#endif
struct TextFont *OpenDiskFont( struct TextAttr *textAttr );
LONG AvailFonts( struct AvailFontsHeader *buffer, LONG bufBytes, ULONG flags );
/*--- functions in V34 or higher (Release 1.3) ---*/
struct FontContentsHeader *NewFontContents( BPTR fontsLock, CONST_STRPTR fontName );
VOID DisposeFontContents( struct FontContentsHeader *fontContentsHeader );
/*--- functions in V36 or higher (Release 2.0) ---*/
struct DiskFont *NewScaledDiskFont( struct TextFont *sourceFont, struct TextAttr *destTextAttr );
/*--- functions in V45 or higher (Release 3.9) ---*/
LONG GetDiskFontCtrl( LONG tagid );
VOID SetDiskFontCtrlA( CONST struct TagItem *taglist );
VOID SetDiskFontCtrl( Tag tag1, ... );
/*--- functions in V47 or higher (Release 3.2) ---*/
LONG EOpenEngine( struct EGlyphEngine *eEngine );
VOID ECloseEngine( struct EGlyphEngine *eEngine );
ULONG ESetInfoA( struct EGlyphEngine *eEngine, CONST struct TagItem *taglist );
ULONG ESetInfo( struct EGlyphEngine *eEngine, ... );
ULONG EObtainInfoA( struct EGlyphEngine *eEngine, CONST struct TagItem *taglist );
ULONG EObtainInfo( struct EGlyphEngine *eEngine, ... );
ULONG EReleaseInfoA( struct EGlyphEngine *eEngine, CONST struct TagItem *taglist );
ULONG EReleaseInfo( struct EGlyphEngine *eEngine, ... );
struct OutlineFont *OpenOutlineFont( CONST_STRPTR name, struct List *list, ULONG flags );
VOID CloseOutlineFont( struct OutlineFont *olf, struct List *list );
LONG WriteFontContents( BPTR fontsLock, CONST_STRPTR fontName, CONST struct FontContentsHeader *fontContentsHeader );
LONG WriteDiskFontHeaderA( CONST struct TextFont *font, CONST_STRPTR fileName, CONST struct TagItem *tagList );
LONG WriteDiskFontHeader( CONST struct TextFont *font, CONST_STRPTR fileName, ... );
ULONG ObtainCharsetInfo( ULONG knownTag, ULONG knownValue, ULONG wantedTag );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_DISKFONT_PROTOS_H */
