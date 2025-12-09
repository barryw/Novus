#ifndef CLIB_REACTION_LIB_PROTOS_H
#define CLIB_REACTION_LIB_PROTOS_H

/*
**	$VER: reaction_lib_protos.h 47.1 (29.11.2021)
**
**	'C' prototypes. For use with 32 bit integers only.
*/

#ifdef __cplusplus
extern "C" {
#endif /* __cplusplus */

#ifndef  EXEC_NODES_H
#include <exec/nodes.h>
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
struct Node *LBAddNodeA( struct Gadget *gad, struct Window *win, struct Requester *req, struct Node *node, CONST struct TagItem *tags );
struct Node *LBAddNode( struct Gadget *gad, struct Window *win, struct Requester *req, struct Node *node, ... );
ULONG LBEditNodeA( struct Gadget *gad, struct Window *win, struct Requester *req, struct Node *node, CONST struct TagItem *tags );
ULONG LBEditNode( struct Gadget *gad, struct Window *win, struct Requester *req, struct Node *node, ... );
ULONG LBRemNode( struct Gadget *gad, struct Window *win, struct Requester *req, struct Node *node );
struct List *ClickTabsA( STRPTR *text_array );
struct List *ClickTabs( STRPTR first_text, ... );
VOID FreeClickTabs( struct List *list );
struct List *BrowserNodesA( STRPTR *text_array );
struct List *BrowserNodes( STRPTR first_text, ... );
VOID FreeBrowserNodes( struct List *list );
struct List *ChooserLabelsA( STRPTR *text_array );
struct List *ChooserLabels( STRPTR first_text, ... );
VOID FreeChooserLabels( struct List *list );
struct List *RadioButtonsA( STRPTR *text_array );
struct List *RadioButtons( STRPTR first_text, ... );
VOID FreeRadioButtons( struct List *list );
ULONG GetAttrsA( Object *o, CONST struct TagItem *tags );
ULONG GetAttrs( Object *o, ... );
UWORD GetCode( CONST struct IntuiMessage *imsg );
ULONG LibDoGadgetMethodA( struct Gadget *gadget, struct Window *window, struct Requester *requester, Msg message );
ULONG LibDoGadgetMethod( struct Gadget *gadget, struct Window *window, struct Requester *requester, ... );
struct Window *OpenLayoutWindowTagList( struct Gadget *layout, struct Screen *screen, CONST struct TagItem *tags );
struct Window *OpenLayoutWindowTags( struct Gadget *layout, struct Screen *screen, ... );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_REACTION_LIB_PROTOS_H */
