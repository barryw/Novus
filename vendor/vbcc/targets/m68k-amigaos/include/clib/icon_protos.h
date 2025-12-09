#ifndef CLIB_ICON_PROTOS_H
#define CLIB_ICON_PROTOS_H

/*
**	$VER: icon_protos.h 47.1 (30.11.2021)
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
#ifndef  WORKBENCH_WORKBENCH_H
#include <workbench/workbench.h>
#endif
#ifndef  DATATYPES_PICTURECLASS_H
#include <datatypes/pictureclass.h>
#endif
VOID FreeFreeList( struct FreeList *freelist );
BOOL AddFreeList( struct FreeList *freelist, CONST_APTR mem, ULONG size );
struct DiskObject *GetDiskObject( CONST_STRPTR name );
BOOL PutDiskObject( CONST_STRPTR name, CONST struct DiskObject *diskobj );
VOID FreeDiskObject( struct DiskObject *diskobj );
UBYTE *FindToolType( CONST_STRPTR *toolTypeArray, CONST_STRPTR typeName );
BOOL MatchToolValue( CONST_STRPTR typeString, CONST_STRPTR value );
STRPTR BumpRevision( STRPTR newname, CONST_STRPTR oldname );
APTR FreeAlloc( struct FreeList *free, ULONG len, ULONG type );
/*--- functions in V36 or higher (Release 2.0) ---*/
struct DiskObject *GetDefDiskObject( LONG type );
BOOL PutDefDiskObject( CONST struct DiskObject *diskObject );
struct DiskObject *GetDiskObjectNew( CONST_STRPTR name );
/*--- functions in V37 or higher (Release 2.04) ---*/
BOOL DeleteDiskObject( CONST_STRPTR name );
/*--- functions in V44 or higher (Release 3.5) ---*/
VOID FreeFree( struct FreeList *fl, APTR address );
struct DiskObject *DupDiskObjectA( CONST struct DiskObject *diskObject, CONST struct TagItem *tags );
struct DiskObject *DupDiskObject( CONST struct DiskObject *diskObject, ... );
ULONG IconControlA( struct DiskObject *icon, CONST struct TagItem *tags );
ULONG IconControl( struct DiskObject *icon, ... );
VOID DrawIconStateA( struct RastPort *rp, CONST struct DiskObject *icon, CONST_STRPTR label, LONG leftOffset, LONG topOffset, ULONG state, CONST struct TagItem *tags );
VOID DrawIconState( struct RastPort *rp, CONST struct DiskObject *icon, CONST_STRPTR label, LONG leftOffset, LONG topOffset, ULONG state, ... );
BOOL GetIconRectangleA( struct RastPort *rp, CONST struct DiskObject *icon, CONST_STRPTR label, struct Rectangle *rect, CONST struct TagItem *tags );
BOOL GetIconRectangle( struct RastPort *rp, CONST struct DiskObject *icon, CONST_STRPTR label, struct Rectangle *rect, ... );
struct DiskObject *NewDiskObject( LONG type );
struct DiskObject *GetIconTagList( CONST_STRPTR name, CONST struct TagItem *tags );
struct DiskObject *GetIconTags( CONST_STRPTR name, ... );
BOOL PutIconTagList( CONST_STRPTR name, CONST struct DiskObject *icon, CONST struct TagItem *tags );
BOOL PutIconTags( CONST_STRPTR name, CONST struct DiskObject *icon, ... );
BOOL LayoutIconA( struct DiskObject *icon, struct Screen *screen, struct TagItem *tags );
BOOL LayoutIcon( struct DiskObject *icon, struct Screen *screen, ... );
VOID ChangeToSelectedIconColor( struct ColorRegister *cr );
STRPTR BumpRevisionLength( STRPTR newname, CONST_STRPTR oldname, ULONG maxLength );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_ICON_PROTOS_H */
