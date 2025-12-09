#ifndef CLIB_GADTOOLS_PROTOS_H
#define CLIB_GADTOOLS_PROTOS_H

/*
**	$VER: gadtools_protos.h 47.1 (30.11.2021)
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
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  LIBRARIES_GADTOOLS_H
#include <libraries/gadtools.h>
#endif

/*--- functions in V36 or higher (Release 2.0) ---*/

/* Gadget Functions */

struct Gadget *CreateGadgetA( ULONG kind, struct Gadget *gad, struct NewGadget *ng, CONST struct TagItem *taglist );
struct Gadget *CreateGadget( ULONG kind, struct Gadget *gad, struct NewGadget *ng, ... );
VOID FreeGadgets( struct Gadget *gad );
VOID GT_SetGadgetAttrsA( struct Gadget *gad, struct Window *win, struct Requester *req, CONST struct TagItem *taglist );
VOID GT_SetGadgetAttrs( struct Gadget *gad, struct Window *win, struct Requester *req, ... );

/* Menu functions */

struct Menu *CreateMenusA( CONST struct NewMenu *newmenu, struct TagItem *taglist );
struct Menu *CreateMenus( CONST struct NewMenu *newmenu, ... );
VOID FreeMenus( struct Menu *menu );
BOOL LayoutMenuItemsA( struct MenuItem *firstitem, APTR vi, CONST struct TagItem *taglist );
BOOL LayoutMenuItems( struct MenuItem *firstitem, APTR vi, ... );
BOOL LayoutMenusA( struct Menu *firstmenu, APTR vi, CONST struct TagItem *taglist );
BOOL LayoutMenus( struct Menu *firstmenu, APTR vi, ... );

/* Misc Event-Handling Functions */

struct IntuiMessage *GT_GetIMsg( struct MsgPort *iport );
VOID GT_ReplyIMsg( struct IntuiMessage *imsg );
VOID GT_RefreshWindow( struct Window *win, struct Requester *req );
VOID GT_BeginRefresh( struct Window *win );
VOID GT_EndRefresh( struct Window *win, LONG complete );
struct IntuiMessage *GT_FilterIMsg( CONST struct IntuiMessage *imsg );
struct IntuiMessage *GT_PostFilterIMsg( struct IntuiMessage *imsg );
struct Gadget *CreateContext( struct Gadget **glistptr );

/* Rendering Functions */

VOID DrawBevelBoxA( struct RastPort *rport, LONG left, LONG top, LONG width, LONG height, CONST struct TagItem *taglist );
VOID DrawBevelBox( struct RastPort *rport, LONG left, LONG top, LONG width, LONG height, ... );

/* Visuals Functions */

APTR GetVisualInfoA( struct Screen *screen, CONST struct TagItem *taglist );
APTR GetVisualInfo( struct Screen *screen, ... );
VOID FreeVisualInfo( APTR vi );

/* New in V47 */

/*--- functions in V47 or higher (Release 3.2) ---*/
LONG SetDesignFontA( APTR vi, struct TextAttr *tattr, CONST struct TagItem *tags );
LONG SetDesignFont( APTR vi, struct TextAttr *tattr, ... );
LONG ScaleGadgetRectA( struct NewGadget *ng, CONST struct TagItem *tags );
LONG ScaleGadgetRect( struct NewGadget *ng, ... );


/*--- functions in V39 or higher (Release 3.0) ---*/

LONG GT_GetGadgetAttrsA( struct Gadget *gad, struct Window *win, struct Requester *req, CONST struct TagItem *taglist );
LONG GT_GetGadgetAttrs( struct Gadget *gad, struct Window *win, struct Requester *req, ... );


#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_GADTOOLS_PROTOS_H */
