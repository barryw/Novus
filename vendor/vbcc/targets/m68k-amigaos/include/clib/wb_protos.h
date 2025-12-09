#ifndef CLIB_WB_PROTOS_H
#define CLIB_WB_PROTOS_H

/*
**	$VER: wb_protos.h 47.1 (30.11.2021)
**
**	'C' prototypes. For use with 32 bit integers only.
**
**	Copyright (C) 2019-2022 Hyperion Entertainment CVBA.
**	    Developed under license.
*/

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

/*--- functions in V36 or higher (Release 2.0) ---*/
#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  DOS_DOS_H
#include <dos/dos.h>
#endif
#ifndef  WORKBENCH_WORKBENCH_H
#include <workbench/workbench.h>
#endif
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif

VOID UpdateWorkbench( CONST_STRPTR name, BPTR lock, LONG action );

struct AppWindow *AddAppWindowA( ULONG id, ULONG userdata, struct Window *window, struct MsgPort *msgport, CONST struct TagItem *taglist );
struct AppWindow *AddAppWindow( ULONG id, ULONG userdata, struct Window *window, struct MsgPort *msgport, ... );

BOOL RemoveAppWindow( struct AppWindow *appWindow );

struct AppIcon *AddAppIconA( ULONG id, ULONG userdata, CONST_STRPTR text, struct MsgPort *msgport, BPTR lock, struct DiskObject *diskobj, CONST struct TagItem *taglist );
struct AppIcon *AddAppIcon( ULONG id, ULONG userdata, CONST_STRPTR text, struct MsgPort *msgport, BPTR lock, struct DiskObject *diskobj, ... );

BOOL RemoveAppIcon( struct AppIcon *appIcon );

struct AppMenuItem *AddAppMenuItemA( ULONG id, ULONG userdata, CONST_STRPTR text, struct MsgPort *msgport, CONST struct TagItem *taglist );
struct AppMenuItem *AddAppMenuItem( ULONG id, ULONG userdata, CONST_STRPTR text, struct MsgPort *msgport, ... );

BOOL RemoveAppMenuItem( struct AppMenuItem *appMenuItem );

/*--- functions in V39 or higher (Release 3.0) ---*/


ULONG WBInfo( BPTR lock, CONST_STRPTR name, struct Screen *screen );

/*--- functions in V44 or higher (Release 3.5) ---*/
BOOL OpenWorkbenchObjectA( CONST_STRPTR name, CONST struct TagItem *tags );
BOOL OpenWorkbenchObject( CONST_STRPTR name, ... );
BOOL CloseWorkbenchObjectA( CONST_STRPTR name, CONST struct TagItem *tags );
BOOL CloseWorkbenchObject( CONST_STRPTR name, ... );
BOOL WorkbenchControlA( CONST_STRPTR name, CONST struct TagItem *tags );
BOOL WorkbenchControl( CONST_STRPTR name, ... );
struct AppWindowDropZone *AddAppWindowDropZoneA( struct AppWindow *aw, ULONG id, ULONG userdata, CONST struct TagItem *tags );
struct AppWindowDropZone *AddAppWindowDropZone( struct AppWindow *aw, ULONG id, ULONG userdata, ... );
BOOL RemoveAppWindowDropZone( struct AppWindow *aw, struct AppWindowDropZone *dropZone );
BOOL ChangeWorkbenchSelectionA( CONST_STRPTR name, struct Hook *hook, CONST struct TagItem *tags );
BOOL ChangeWorkbenchSelection( CONST_STRPTR name, struct Hook *hook, ... );
BOOL MakeWorkbenchObjectVisibleA( CONST_STRPTR name, CONST struct TagItem *tags );
BOOL MakeWorkbenchObjectVisible( CONST_STRPTR name, ... );

/*--- functions in V47 or higher (Release 3.2) ---*/
ULONG WhichWorkbenchObjectA( struct Window *window, LONG x, LONG y, CONST struct TagItem *tags );
ULONG WhichWorkbenchObject( struct Window *window, LONG x, LONG y, ... );


#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_WB_PROTOS_H */
