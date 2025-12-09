#ifndef _INLINE_DATATYPES_H
#define _INLINE_DATATYPES_H

#ifndef CLIB_DATATYPES_PROTOS_H
#define CLIB_DATATYPES_PROTOS_H
#endif

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

#ifndef DATATYPES_BASE_NAME
#define DATATYPES_BASE_NAME DataTypesBase
#endif

#define ObtainDataTypeA(type, handle, attrs) ({ \
  ULONG _ObtainDataTypeA_type = (type); \
  APTR _ObtainDataTypeA_handle = (handle); \
  CONST struct TagItem * _ObtainDataTypeA_attrs = (attrs); \
  ({ \
  register char * _ObtainDataTypeA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((struct DataType * (*)(char * __asm("a6"), ULONG __asm("d0"), APTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_ObtainDataTypeA__bn - 36))(_ObtainDataTypeA__bn, _ObtainDataTypeA_type, _ObtainDataTypeA_handle, _ObtainDataTypeA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct DataType * ___ObtainDataType(struct Library * DataTypesBase, ULONG type, APTR handle, ...)
{
  return ObtainDataTypeA(type, handle, (CONST struct TagItem *) ((ULONG) &handle + sizeof(APTR)));
}

#define ObtainDataType(type, handle...) ___ObtainDataType(DATATYPES_BASE_NAME, type, handle)
#endif

#define ReleaseDataType(dt) ({ \
  struct DataType * _ReleaseDataType_dt = (dt); \
  ({ \
  register char * _ReleaseDataType__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct DataType * __asm("a0"))) \
  (_ReleaseDataType__bn - 42))(_ReleaseDataType__bn, _ReleaseDataType_dt); \
});})

#define NewDTObjectA(name, attrs) ({ \
  CONST_STRPTR _NewDTObjectA_name = (name); \
  CONST struct TagItem * _NewDTObjectA_attrs = (attrs); \
  ({ \
  register char * _NewDTObjectA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((Object * (*)(char * __asm("a6"), CONST_STRPTR __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_NewDTObjectA__bn - 48))(_NewDTObjectA__bn, _NewDTObjectA_name, _NewDTObjectA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ Object * ___NewDTObject(struct Library * DataTypesBase, CONST_STRPTR name, ...)
{
  return NewDTObjectA(name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define NewDTObject(name...) ___NewDTObject(DATATYPES_BASE_NAME, name)
#endif

#define DisposeDTObject(o) ({ \
  Object * _DisposeDTObject_o = (o); \
  ({ \
  register char * _DisposeDTObject__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), Object * __asm("a0"))) \
  (_DisposeDTObject__bn - 54))(_DisposeDTObject__bn, _DisposeDTObject_o); \
});})

#define SetDTAttrsA(o, win, req, attrs) ({ \
  Object * _SetDTAttrsA_o = (o); \
  struct Window * _SetDTAttrsA_win = (win); \
  struct Requester * _SetDTAttrsA_req = (req); \
  CONST struct TagItem * _SetDTAttrsA_attrs = (attrs); \
  ({ \
  register char * _SetDTAttrsA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), CONST struct TagItem * __asm("a3"))) \
  (_SetDTAttrsA__bn - 60))(_SetDTAttrsA__bn, _SetDTAttrsA_o, _SetDTAttrsA_win, _SetDTAttrsA_req, _SetDTAttrsA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___SetDTAttrs(struct Library * DataTypesBase, Object * o, struct Window * win, struct Requester * req, ...)
{
  return SetDTAttrsA(o, win, req, (CONST struct TagItem *) ((ULONG) &req + sizeof(struct Requester *)));
}

#define SetDTAttrs(o, win, req...) ___SetDTAttrs(DATATYPES_BASE_NAME, o, win, req)
#endif

#define GetDTAttrsA(o, attrs) ({ \
  Object * _GetDTAttrsA_o = (o); \
  CONST struct TagItem * _GetDTAttrsA_attrs = (attrs); \
  ({ \
  register char * _GetDTAttrsA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"), CONST struct TagItem * __asm("a2"))) \
  (_GetDTAttrsA__bn - 66))(_GetDTAttrsA__bn, _GetDTAttrsA_o, _GetDTAttrsA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___GetDTAttrs(struct Library * DataTypesBase, Object * o, ...)
{
  return GetDTAttrsA(o, (CONST struct TagItem *) ((ULONG) &o + sizeof(Object *)));
}

#define GetDTAttrs(o...) ___GetDTAttrs(DATATYPES_BASE_NAME, o)
#endif

#define AddDTObject(win, req, o, pos) ({ \
  struct Window * _AddDTObject_win = (win); \
  struct Requester * _AddDTObject_req = (req); \
  Object * _AddDTObject_o = (o); \
  LONG _AddDTObject_pos = (pos); \
  ({ \
  register char * _AddDTObject__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Window * __asm("a0"), struct Requester * __asm("a1"), Object * __asm("a2"), LONG __asm("d0"))) \
  (_AddDTObject__bn - 72))(_AddDTObject__bn, _AddDTObject_win, _AddDTObject_req, _AddDTObject_o, _AddDTObject_pos); \
});})

#define RefreshDTObjectA(o, win, req, attrs) ({ \
  Object * _RefreshDTObjectA_o = (o); \
  struct Window * _RefreshDTObjectA_win = (win); \
  struct Requester * _RefreshDTObjectA_req = (req); \
  CONST struct TagItem * _RefreshDTObjectA_attrs = (attrs); \
  ({ \
  register char * _RefreshDTObjectA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), Object * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), CONST struct TagItem * __asm("a3"))) \
  (_RefreshDTObjectA__bn - 78))(_RefreshDTObjectA__bn, _RefreshDTObjectA_o, _RefreshDTObjectA_win, _RefreshDTObjectA_req, _RefreshDTObjectA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___RefreshDTObjects(struct Library * DataTypesBase, Object * o, struct Window * win, struct Requester * req, ...)
{
  RefreshDTObjectA(o, win, req, (CONST struct TagItem *) ((ULONG) &req + sizeof(struct Requester *)));
}

#define RefreshDTObjects(o, win, req...) ___RefreshDTObjects(DATATYPES_BASE_NAME, o, win, req)
#endif

#ifndef NO_INLINE_STDARG
#define RefreshDTObject RefreshDTObjects
#endif

#define DoAsyncLayout(o, gpl) ({ \
  Object * _DoAsyncLayout_o = (o); \
  struct gpLayout * _DoAsyncLayout_gpl = (gpl); \
  ({ \
  register char * _DoAsyncLayout__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"), struct gpLayout * __asm("a1"))) \
  (_DoAsyncLayout__bn - 84))(_DoAsyncLayout__bn, _DoAsyncLayout_o, _DoAsyncLayout_gpl); \
});})

#define DoDTMethodA(o, win, req, msg) ({ \
  Object * _DoDTMethodA_o = (o); \
  struct Window * _DoDTMethodA_win = (win); \
  struct Requester * _DoDTMethodA_req = (req); \
  Msg _DoDTMethodA_msg = (msg); \
  ({ \
  register char * _DoDTMethodA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), Msg __asm("a3"))) \
  (_DoDTMethodA__bn - 90))(_DoDTMethodA__bn, _DoDTMethodA_o, _DoDTMethodA_win, _DoDTMethodA_req, _DoDTMethodA_msg); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___DoDTMethod(struct Library * DataTypesBase, Object * o, struct Window * win, struct Requester * req, ULONG msg, ...)
{
  return DoDTMethodA(o, win, req, (Msg) &msg);
}

#define DoDTMethod(o, win, req...) ___DoDTMethod(DATATYPES_BASE_NAME, o, win, req)
#endif

#define RemoveDTObject(win, o) ({ \
  struct Window * _RemoveDTObject_win = (win); \
  Object * _RemoveDTObject_o = (o); \
  ({ \
  register char * _RemoveDTObject__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Window * __asm("a0"), Object * __asm("a1"))) \
  (_RemoveDTObject__bn - 96))(_RemoveDTObject__bn, _RemoveDTObject_win, _RemoveDTObject_o); \
});})

#define GetDTMethods(object) ({ \
  CONST Object * _GetDTMethods_object = (object); \
  ({ \
  register char * _GetDTMethods__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG * (*)(char * __asm("a6"), CONST Object * __asm("a0"))) \
  (_GetDTMethods__bn - 102))(_GetDTMethods__bn, _GetDTMethods_object); \
});})

#define GetDTTriggerMethods(object) ({ \
  Object * _GetDTTriggerMethods_object = (object); \
  ({ \
  register char * _GetDTTriggerMethods__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((struct DTMethods * (*)(char * __asm("a6"), Object * __asm("a0"))) \
  (_GetDTTriggerMethods__bn - 108))(_GetDTTriggerMethods__bn, _GetDTTriggerMethods_object); \
});})

#define PrintDTObjectA(o, w, r, msg) ({ \
  Object * _PrintDTObjectA_o = (o); \
  struct Window * _PrintDTObjectA_w = (w); \
  struct Requester * _PrintDTObjectA_r = (r); \
  struct dtPrint * _PrintDTObjectA_msg = (msg); \
  ({ \
  register char * _PrintDTObjectA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), struct dtPrint * __asm("a3"))) \
  (_PrintDTObjectA__bn - 114))(_PrintDTObjectA__bn, _PrintDTObjectA_o, _PrintDTObjectA_w, _PrintDTObjectA_r, _PrintDTObjectA_msg); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___PrintDTObject(struct Library * DataTypesBase, Object * o, struct Window * w, struct Requester * r, ULONG msg, ...)
{
  return PrintDTObjectA(o, w, r, (struct dtPrint *) &msg);
}

#define PrintDTObject(o, w, r...) ___PrintDTObject(DATATYPES_BASE_NAME, o, w, r)
#endif

#define GetDTString(id) ({ \
  ULONG _GetDTString_id = (id); \
  ({ \
  register char * _GetDTString__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_GetDTString__bn - 138))(_GetDTString__bn, _GetDTString_id); \
});})

#define FindMethod(methods, searchmethodid) ({ \
  CONST ULONG * _FindMethod_methods = (methods); \
  ULONG _FindMethod_searchmethodid = (searchmethodid); \
  ({ \
  register char * _FindMethod__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG * (*)(char * __asm("a6"), CONST ULONG * __asm("a0"), ULONG __asm("a1"))) \
  (_FindMethod__bn - 258))(_FindMethod__bn, _FindMethod_methods, _FindMethod_searchmethodid); \
});})

#define FindTriggerMethod(dtm, command, method) ({ \
  CONST struct DTMethod * _FindTriggerMethod_dtm = (dtm); \
  CONST_STRPTR _FindTriggerMethod_command = (command); \
  ULONG _FindTriggerMethod_method = (method); \
  ({ \
  register char * _FindTriggerMethod__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((struct DTMethod * (*)(char * __asm("a6"), CONST struct DTMethod * __asm("a0"), CONST_STRPTR __asm("a1"), ULONG __asm("d0"))) \
  (_FindTriggerMethod__bn - 264))(_FindTriggerMethod__bn, _FindTriggerMethod_dtm, _FindTriggerMethod_command, _FindTriggerMethod_method); \
});})

#define CopyDTMethods(methods, include, exclude) ({ \
  CONST ULONG * _CopyDTMethods_methods = (methods); \
  CONST ULONG * _CopyDTMethods_include = (include); \
  CONST ULONG * _CopyDTMethods_exclude = (exclude); \
  ({ \
  register char * _CopyDTMethods__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG * (*)(char * __asm("a6"), CONST ULONG * __asm("a0"), CONST ULONG * __asm("a1"), CONST ULONG * __asm("a2"))) \
  (_CopyDTMethods__bn - 270))(_CopyDTMethods__bn, _CopyDTMethods_methods, _CopyDTMethods_include, _CopyDTMethods_exclude); \
});})

#define CopyDTTriggerMethods(methods, include, exclude) ({ \
  CONST struct DTMethod * _CopyDTTriggerMethods_methods = (methods); \
  CONST struct DTMethod * _CopyDTTriggerMethods_include = (include); \
  CONST struct DTMethod * _CopyDTTriggerMethods_exclude = (exclude); \
  ({ \
  register char * _CopyDTTriggerMethods__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((struct DTMethod * (*)(char * __asm("a6"), CONST struct DTMethod * __asm("a0"), CONST struct DTMethod * __asm("a1"), CONST struct DTMethod * __asm("a2"))) \
  (_CopyDTTriggerMethods__bn - 276))(_CopyDTTriggerMethods__bn, _CopyDTTriggerMethods_methods, _CopyDTTriggerMethods_include, _CopyDTTriggerMethods_exclude); \
});})

#define FreeDTMethods(methods) ({ \
  APTR _FreeDTMethods_methods = (methods); \
  ({ \
  register char * _FreeDTMethods__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((VOID * (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_FreeDTMethods__bn - 282))(_FreeDTMethods__bn, _FreeDTMethods_methods); \
});})

#define GetDTTriggerMethodDataFlags(triggermethod) ({ \
  ULONG _GetDTTriggerMethodDataFlags_triggermethod = (triggermethod); \
  ({ \
  register char * _GetDTTriggerMethodDataFlags__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_GetDTTriggerMethodDataFlags__bn - 288))(_GetDTTriggerMethodDataFlags__bn, _GetDTTriggerMethodDataFlags_triggermethod); \
});})

#define SaveDTObjectA(o, win, req, file, mode, saveicon, attrs) ({ \
  Object * _SaveDTObjectA_o = (o); \
  struct Window * _SaveDTObjectA_win = (win); \
  struct Requester * _SaveDTObjectA_req = (req); \
  CONST_STRPTR _SaveDTObjectA_file = (file); \
  ULONG _SaveDTObjectA_mode = (mode); \
  LONG _SaveDTObjectA_saveicon = (saveicon); \
  struct TagItem * _SaveDTObjectA_attrs = (attrs); \
  ({ \
  register char * _SaveDTObjectA__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"), struct Window * __asm("a1"), struct Requester * __asm("a2"), CONST_STRPTR __asm("a3"), ULONG __asm("d0"), LONG __asm("d1"), struct TagItem * __asm("a4"))) \
  (_SaveDTObjectA__bn - 294))(_SaveDTObjectA__bn, _SaveDTObjectA_o, _SaveDTObjectA_win, _SaveDTObjectA_req, _SaveDTObjectA_file, _SaveDTObjectA_mode, _SaveDTObjectA_saveicon, _SaveDTObjectA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___SaveDTObject(struct Library * DataTypesBase, Object * o, struct Window * win, struct Requester * req, CONST_STRPTR file, ULONG mode, LONG saveicon, ...)
{
  return SaveDTObjectA(o, win, req, file, mode, saveicon, (struct TagItem *) ((ULONG) &saveicon + sizeof(LONG)));
}

#define SaveDTObject(o, win, req, file, mode, saveicon...) ___SaveDTObject(DATATYPES_BASE_NAME, o, win, req, file, mode, saveicon)
#endif

#define StartDragSelect(o) ({ \
  Object * _StartDragSelect_o = (o); \
  ({ \
  register char * _StartDragSelect__bn __asm("a6") = (char *) (DATATYPES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"))) \
  (_StartDragSelect__bn - 300))(_StartDragSelect__bn, _StartDragSelect_o); \
});})

#endif /*  _INLINE_DATATYPES_H  */
