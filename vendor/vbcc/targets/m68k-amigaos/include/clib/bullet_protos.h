#ifndef CLIB_BULLET_PROTOS_H
#define CLIB_BULLET_PROTOS_H

/*
**	$VER: bullet_protos.h 47.1 (30.11.2021)
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
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  DISKFONT_GLYPH_H
#include <diskfont/glyph.h>
#endif
struct GlyphEngine *OpenEngine( VOID );
VOID CloseEngine( struct GlyphEngine *glyphEngine );
ULONG SetInfoA( struct GlyphEngine *glyphEngine, CONST struct TagItem *tagList );
ULONG SetInfo( struct GlyphEngine *glyphEngine, ... );
ULONG ObtainInfoA( struct GlyphEngine *glyphEngine, CONST struct TagItem *tagList );
ULONG ObtainInfo( struct GlyphEngine *glyphEngine, ... );
ULONG ReleaseInfoA( struct GlyphEngine *glyphEngine, CONST struct TagItem *tagList );
ULONG ReleaseInfo( struct GlyphEngine *glyphEngine, ... );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_BULLET_PROTOS_H */
