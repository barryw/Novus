#ifndef CLIB_DATATYPES_PROTOS_H
#define CLIB_DATATYPES_PROTOS_H

/*
**	$VER: datatypes_protos.h 47.1 (30.11.2021)
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
#ifndef  EXEC_LISTS_H
#include <exec/lists.h>
#endif
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  INTUITION_CLASSES_H
#include <intuition/classes.h>
#endif
#ifndef  INTUITION_CLASSUSR_H
#include <intuition/classusr.h>
#endif
#ifndef  INTUITION_GADGETCLASS_H
#include <intuition/gadgetclass.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  DATATYPES_DATATYPES_H
#include <datatypes/datatypes.h>
#endif
#ifndef  DATATYPES_DATATYPESCLASS_H
#include <datatypes/datatypesclass.h>
#endif
#ifndef  REXX_STORAGE_H
#include <rexx/storage.h>
#endif
/*--- functions in V40 or higher (Release 3.1) ---*/

/* Public entries */

struct DataType *ObtainDataTypeA( ULONG type, APTR handle, CONST struct TagItem *attrs );
struct DataType *ObtainDataType( ULONG type, APTR handle, ... );
VOID ReleaseDataType( struct DataType *dt );
Object *NewDTObjectA( CONST_STRPTR name, CONST struct TagItem *attrs );
Object *NewDTObject( CONST_STRPTR name, ... );
VOID DisposeDTObject( Object *o );
ULONG SetDTAttrsA( Object *o, struct Window *win, struct Requester *req, CONST struct TagItem *attrs );
ULONG SetDTAttrs( Object *o, struct Window *win, struct Requester *req, ... );
ULONG GetDTAttrsA( Object *o, CONST struct TagItem *attrs );
ULONG GetDTAttrs( Object *o, ... );
LONG AddDTObject( struct Window *win, struct Requester *req, Object *o, LONG pos );
VOID RefreshDTObjectA( Object *o, struct Window *win, struct Requester *req, CONST struct TagItem *attrs );
VOID RefreshDTObjects( Object *o, struct Window *win, struct Requester *req, ... );
VOID RefreshDTObject( Object *o, struct Window *win, struct Requester *req, ... );
ULONG DoAsyncLayout( Object *o, struct gpLayout *gpl );
ULONG DoDTMethodA( Object *o, struct Window *win, struct Requester *req, Msg msg );
ULONG DoDTMethod( Object *o, struct Window *win, struct Requester *req, ULONG data, ... );
LONG RemoveDTObject( struct Window *win, Object *o );
ULONG *GetDTMethods( CONST Object *object );
struct DTMethods *GetDTTriggerMethods( Object *object );
ULONG PrintDTObjectA( Object *o, struct Window *w, struct Requester *r, struct dtPrint *msg );
ULONG PrintDTObject( Object *o, struct Window *w, struct Requester *r, ULONG data, ... );
STRPTR GetDTString( ULONG id );
/*--- functions in V45 or higher ---*/
ULONG *FindMethod( CONST ULONG *methods, ULONG searchmethodid );
struct DTMethod *FindTriggerMethod( CONST struct DTMethod *dtm, CONST_STRPTR command, ULONG method );
ULONG *CopyDTMethods( CONST ULONG *methods, CONST ULONG *include, CONST ULONG *exclude );
struct DTMethod *CopyDTTriggerMethods( CONST struct DTMethod *methods, CONST struct DTMethod *include, CONST struct DTMethod *exclude );
VOID *FreeDTMethods( APTR methods );
ULONG GetDTTriggerMethodDataFlags( ULONG triggermethod );
ULONG SaveDTObjectA( Object *o, struct Window *win, struct Requester *req, CONST_STRPTR file, ULONG mode, LONG saveicon, struct TagItem *attrs );
ULONG SaveDTObject( Object *o, struct Window *win, struct Requester *req, CONST_STRPTR file, ULONG mode, LONG saveicon, ... );
ULONG StartDragSelect( Object *o );

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif /* CLIB_DATATYPES_PROTOS_H */
