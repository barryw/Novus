#ifndef CLIB_CHOOSER_PROTOS_H
#define CLIB_CHOOSER_PROTOS_H

/*
**	$VER: chooser_protos.h 47.1 (30.11.2021)
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
#ifndef  INTUITION_CLASSES_H
#include <intuition/classes.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  GADGETS_CHOOSER_H
#include <gadgets/chooser.h>
#endif
/*--- functions in V40 or higher (Release 3.1) ---*/
Class *CHOOSER_GetClass( VOID );
struct Node *AllocChooserNodeA( struct TagItem *tags );
struct Node *AllocChooserNode( Tag firstTag, ... );
VOID FreeChooserNode( struct Node *node );
VOID SetChooserNodeAttrsA( struct Node *node, struct TagItem *tags );
VOID SetChooserNodeAttrs( struct Node *node, ... );
VOID GetChooserNodeAttrsA( struct Node *node, struct TagItem *tags );
VOID GetChooserNodeAttrs( struct Node *node, ... );
ULONG ShowChooser( Object *o, struct Window *w, ULONG xpos, ULONG ypos );
VOID HideChooser( Object *o, struct Window *w );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_CHOOSER_PROTOS_H */
