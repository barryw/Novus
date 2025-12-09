#ifndef _INLINE_IFFPARSE_H
#define _INLINE_IFFPARSE_H

#ifndef CLIB_IFFPARSE_PROTOS_H
#define CLIB_IFFPARSE_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  LIBRARIES_IFFPARSE_H
#include <libraries/iffparse.h>
#endif
#ifndef  UTILITY_HOOKS_H
#include <utility/hooks.h>
#endif

#ifndef IFFPARSE_BASE_NAME
#define IFFPARSE_BASE_NAME IFFParseBase
#endif

#define AllocIFF() ({ \
  register char * _AllocIFF__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct IFFHandle * (*)(char * __asm("a6"))) \
  (_AllocIFF__bn - 30))(_AllocIFF__bn); \
})

#define OpenIFF(iff, rwMode) ({ \
  struct IFFHandle * _OpenIFF_iff = (iff); \
  LONG _OpenIFF_rwMode = (rwMode); \
  ({ \
  register char * _OpenIFF__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"))) \
  (_OpenIFF__bn - 36))(_OpenIFF__bn, _OpenIFF_iff, _OpenIFF_rwMode); \
});})

#define ParseIFF(iff, control) ({ \
  struct IFFHandle * _ParseIFF_iff = (iff); \
  LONG _ParseIFF_control = (control); \
  ({ \
  register char * _ParseIFF__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"))) \
  (_ParseIFF__bn - 42))(_ParseIFF__bn, _ParseIFF_iff, _ParseIFF_control); \
});})

#define CloseIFF(iff) ({ \
  struct IFFHandle * _CloseIFF_iff = (iff); \
  ({ \
  register char * _CloseIFF__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"))) \
  (_CloseIFF__bn - 48))(_CloseIFF__bn, _CloseIFF_iff); \
});})

#define FreeIFF(iff) ({ \
  struct IFFHandle * _FreeIFF_iff = (iff); \
  ({ \
  register char * _FreeIFF__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"))) \
  (_FreeIFF__bn - 54))(_FreeIFF__bn, _FreeIFF_iff); \
});})

#define ReadChunkBytes(iff, buf, numBytes) ({ \
  struct IFFHandle * _ReadChunkBytes_iff = (iff); \
  APTR _ReadChunkBytes_buf = (buf); \
  LONG _ReadChunkBytes_numBytes = (numBytes); \
  ({ \
  register char * _ReadChunkBytes__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), APTR __asm("a1"), LONG __asm("d0"))) \
  (_ReadChunkBytes__bn - 60))(_ReadChunkBytes__bn, _ReadChunkBytes_iff, _ReadChunkBytes_buf, _ReadChunkBytes_numBytes); \
});})

#define WriteChunkBytes(iff, buf, numBytes) ({ \
  struct IFFHandle * _WriteChunkBytes_iff = (iff); \
  CONST_APTR _WriteChunkBytes_buf = (buf); \
  LONG _WriteChunkBytes_numBytes = (numBytes); \
  ({ \
  register char * _WriteChunkBytes__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), CONST_APTR __asm("a1"), LONG __asm("d0"))) \
  (_WriteChunkBytes__bn - 66))(_WriteChunkBytes__bn, _WriteChunkBytes_iff, _WriteChunkBytes_buf, _WriteChunkBytes_numBytes); \
});})

#define ReadChunkRecords(iff, buf, bytesPerRecord, numRecords) ({ \
  struct IFFHandle * _ReadChunkRecords_iff = (iff); \
  APTR _ReadChunkRecords_buf = (buf); \
  LONG _ReadChunkRecords_bytesPerRecord = (bytesPerRecord); \
  LONG _ReadChunkRecords_numRecords = (numRecords); \
  ({ \
  register char * _ReadChunkRecords__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), APTR __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_ReadChunkRecords__bn - 72))(_ReadChunkRecords__bn, _ReadChunkRecords_iff, _ReadChunkRecords_buf, _ReadChunkRecords_bytesPerRecord, _ReadChunkRecords_numRecords); \
});})

#define WriteChunkRecords(iff, buf, bytesPerRecord, numRecords) ({ \
  struct IFFHandle * _WriteChunkRecords_iff = (iff); \
  CONST_APTR _WriteChunkRecords_buf = (buf); \
  LONG _WriteChunkRecords_bytesPerRecord = (bytesPerRecord); \
  LONG _WriteChunkRecords_numRecords = (numRecords); \
  ({ \
  register char * _WriteChunkRecords__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), CONST_APTR __asm("a1"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_WriteChunkRecords__bn - 78))(_WriteChunkRecords__bn, _WriteChunkRecords_iff, _WriteChunkRecords_buf, _WriteChunkRecords_bytesPerRecord, _WriteChunkRecords_numRecords); \
});})

#define PushChunk(iff, type, id, size) ({ \
  struct IFFHandle * _PushChunk_iff = (iff); \
  LONG _PushChunk_type = (type); \
  LONG _PushChunk_id = (id); \
  LONG _PushChunk_size = (size); \
  ({ \
  register char * _PushChunk__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"))) \
  (_PushChunk__bn - 84))(_PushChunk__bn, _PushChunk_iff, _PushChunk_type, _PushChunk_id, _PushChunk_size); \
});})

#define PopChunk(iff) ({ \
  struct IFFHandle * _PopChunk_iff = (iff); \
  ({ \
  register char * _PopChunk__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"))) \
  (_PopChunk__bn - 90))(_PopChunk__bn, _PopChunk_iff); \
});})

#define EntryHandler(iff, type, id, position, handler, object) ({ \
  struct IFFHandle * _EntryHandler_iff = (iff); \
  LONG _EntryHandler_type = (type); \
  LONG _EntryHandler_id = (id); \
  LONG _EntryHandler_position = (position); \
  struct Hook * _EntryHandler_handler = (handler); \
  APTR _EntryHandler_object = (object); \
  ({ \
  register char * _EntryHandler__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), struct Hook * __asm("a1"), APTR __asm("a2"))) \
  (_EntryHandler__bn - 102))(_EntryHandler__bn, _EntryHandler_iff, _EntryHandler_type, _EntryHandler_id, _EntryHandler_position, _EntryHandler_handler, _EntryHandler_object); \
});})

#define ExitHandler(iff, type, id, position, handler, object) ({ \
  struct IFFHandle * _ExitHandler_iff = (iff); \
  LONG _ExitHandler_type = (type); \
  LONG _ExitHandler_id = (id); \
  LONG _ExitHandler_position = (position); \
  struct Hook * _ExitHandler_handler = (handler); \
  APTR _ExitHandler_object = (object); \
  ({ \
  register char * _ExitHandler__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), struct Hook * __asm("a1"), APTR __asm("a2"))) \
  (_ExitHandler__bn - 108))(_ExitHandler__bn, _ExitHandler_iff, _ExitHandler_type, _ExitHandler_id, _ExitHandler_position, _ExitHandler_handler, _ExitHandler_object); \
});})

#define PropChunk(iff, type, id) ({ \
  struct IFFHandle * _PropChunk_iff = (iff); \
  LONG _PropChunk_type = (type); \
  LONG _PropChunk_id = (id); \
  ({ \
  register char * _PropChunk__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_PropChunk__bn - 114))(_PropChunk__bn, _PropChunk_iff, _PropChunk_type, _PropChunk_id); \
});})

#define PropChunks(iff, propArray, numPairs) ({ \
  struct IFFHandle * _PropChunks_iff = (iff); \
  CONST LONG * _PropChunks_propArray = (propArray); \
  LONG _PropChunks_numPairs = (numPairs); \
  ({ \
  register char * _PropChunks__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), CONST LONG * __asm("a1"), LONG __asm("d0"))) \
  (_PropChunks__bn - 120))(_PropChunks__bn, _PropChunks_iff, _PropChunks_propArray, _PropChunks_numPairs); \
});})

#define StopChunk(iff, type, id) ({ \
  struct IFFHandle * _StopChunk_iff = (iff); \
  LONG _StopChunk_type = (type); \
  LONG _StopChunk_id = (id); \
  ({ \
  register char * _StopChunk__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_StopChunk__bn - 126))(_StopChunk__bn, _StopChunk_iff, _StopChunk_type, _StopChunk_id); \
});})

#define StopChunks(iff, propArray, numPairs) ({ \
  struct IFFHandle * _StopChunks_iff = (iff); \
  CONST LONG * _StopChunks_propArray = (propArray); \
  LONG _StopChunks_numPairs = (numPairs); \
  ({ \
  register char * _StopChunks__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), CONST LONG * __asm("a1"), LONG __asm("d0"))) \
  (_StopChunks__bn - 132))(_StopChunks__bn, _StopChunks_iff, _StopChunks_propArray, _StopChunks_numPairs); \
});})

#define CollectionChunk(iff, type, id) ({ \
  struct IFFHandle * _CollectionChunk_iff = (iff); \
  LONG _CollectionChunk_type = (type); \
  LONG _CollectionChunk_id = (id); \
  ({ \
  register char * _CollectionChunk__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_CollectionChunk__bn - 138))(_CollectionChunk__bn, _CollectionChunk_iff, _CollectionChunk_type, _CollectionChunk_id); \
});})

#define CollectionChunks(iff, propArray, numPairs) ({ \
  struct IFFHandle * _CollectionChunks_iff = (iff); \
  CONST LONG * _CollectionChunks_propArray = (propArray); \
  LONG _CollectionChunks_numPairs = (numPairs); \
  ({ \
  register char * _CollectionChunks__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), CONST LONG * __asm("a1"), LONG __asm("d0"))) \
  (_CollectionChunks__bn - 144))(_CollectionChunks__bn, _CollectionChunks_iff, _CollectionChunks_propArray, _CollectionChunks_numPairs); \
});})

#define StopOnExit(iff, type, id) ({ \
  struct IFFHandle * _StopOnExit_iff = (iff); \
  LONG _StopOnExit_type = (type); \
  LONG _StopOnExit_id = (id); \
  ({ \
  register char * _StopOnExit__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_StopOnExit__bn - 150))(_StopOnExit__bn, _StopOnExit_iff, _StopOnExit_type, _StopOnExit_id); \
});})

#define FindProp(iff, type, id) ({ \
  struct IFFHandle * _FindProp_iff = (iff); \
  LONG _FindProp_type = (type); \
  LONG _FindProp_id = (id); \
  ({ \
  register char * _FindProp__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct StoredProperty * (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_FindProp__bn - 156))(_FindProp__bn, _FindProp_iff, _FindProp_type, _FindProp_id); \
});})

#define FindCollection(iff, type, id) ({ \
  struct IFFHandle * _FindCollection_iff = (iff); \
  LONG _FindCollection_type = (type); \
  LONG _FindCollection_id = (id); \
  ({ \
  register char * _FindCollection__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct CollectionItem * (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_FindCollection__bn - 162))(_FindCollection__bn, _FindCollection_iff, _FindCollection_type, _FindCollection_id); \
});})

#define FindPropContext(iff) ({ \
  struct IFFHandle * _FindPropContext_iff = (iff); \
  ({ \
  register char * _FindPropContext__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct ContextNode * (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"))) \
  (_FindPropContext__bn - 168))(_FindPropContext__bn, _FindPropContext_iff); \
});})

#define CurrentChunk(iff) ({ \
  struct IFFHandle * _CurrentChunk_iff = (iff); \
  ({ \
  register char * _CurrentChunk__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct ContextNode * (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"))) \
  (_CurrentChunk__bn - 174))(_CurrentChunk__bn, _CurrentChunk_iff); \
});})

#define ParentChunk(contextNode) ({ \
  struct ContextNode * _ParentChunk_contextNode = (contextNode); \
  ({ \
  register char * _ParentChunk__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct ContextNode * (*)(char * __asm("a6"), struct ContextNode * __asm("a0"))) \
  (_ParentChunk__bn - 180))(_ParentChunk__bn, _ParentChunk_contextNode); \
});})

#define AllocLocalItem(type, id, ident, dataSize) ({ \
  LONG _AllocLocalItem_type = (type); \
  LONG _AllocLocalItem_id = (id); \
  LONG _AllocLocalItem_ident = (ident); \
  LONG _AllocLocalItem_dataSize = (dataSize); \
  ({ \
  register char * _AllocLocalItem__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct LocalContextItem * (*)(char * __asm("a6"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_AllocLocalItem__bn - 186))(_AllocLocalItem__bn, _AllocLocalItem_type, _AllocLocalItem_id, _AllocLocalItem_ident, _AllocLocalItem_dataSize); \
});})

#define LocalItemData(localItem) ({ \
  struct LocalContextItem * _LocalItemData_localItem = (localItem); \
  ({ \
  register char * _LocalItemData__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct LocalContextItem * __asm("a0"))) \
  (_LocalItemData__bn - 192))(_LocalItemData__bn, _LocalItemData_localItem); \
});})

#define SetLocalItemPurge(localItem, purgeHook) ({ \
  struct LocalContextItem * _SetLocalItemPurge_localItem = (localItem); \
  struct Hook * _SetLocalItemPurge_purgeHook = (purgeHook); \
  ({ \
  register char * _SetLocalItemPurge__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct LocalContextItem * __asm("a0"), struct Hook * __asm("a1"))) \
  (_SetLocalItemPurge__bn - 198))(_SetLocalItemPurge__bn, _SetLocalItemPurge_localItem, _SetLocalItemPurge_purgeHook); \
});})

#define FreeLocalItem(localItem) ({ \
  struct LocalContextItem * _FreeLocalItem_localItem = (localItem); \
  ({ \
  register char * _FreeLocalItem__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct LocalContextItem * __asm("a0"))) \
  (_FreeLocalItem__bn - 204))(_FreeLocalItem__bn, _FreeLocalItem_localItem); \
});})

#define FindLocalItem(iff, type, id, ident) ({ \
  struct IFFHandle * _FindLocalItem_iff = (iff); \
  LONG _FindLocalItem_type = (type); \
  LONG _FindLocalItem_id = (id); \
  LONG _FindLocalItem_ident = (ident); \
  ({ \
  register char * _FindLocalItem__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct LocalContextItem * (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), LONG __asm("d2"))) \
  (_FindLocalItem__bn - 210))(_FindLocalItem__bn, _FindLocalItem_iff, _FindLocalItem_type, _FindLocalItem_id, _FindLocalItem_ident); \
});})

#define StoreLocalItem(iff, localItem, position) ({ \
  struct IFFHandle * _StoreLocalItem_iff = (iff); \
  struct LocalContextItem * _StoreLocalItem_localItem = (localItem); \
  LONG _StoreLocalItem_position = (position); \
  ({ \
  register char * _StoreLocalItem__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), struct LocalContextItem * __asm("a1"), LONG __asm("d0"))) \
  (_StoreLocalItem__bn - 216))(_StoreLocalItem__bn, _StoreLocalItem_iff, _StoreLocalItem_localItem, _StoreLocalItem_position); \
});})

#define StoreItemInContext(iff, localItem, contextNode) ({ \
  struct IFFHandle * _StoreItemInContext_iff = (iff); \
  struct LocalContextItem * _StoreItemInContext_localItem = (localItem); \
  struct ContextNode * _StoreItemInContext_contextNode = (contextNode); \
  ({ \
  register char * _StoreItemInContext__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), struct LocalContextItem * __asm("a1"), struct ContextNode * __asm("a2"))) \
  (_StoreItemInContext__bn - 222))(_StoreItemInContext__bn, _StoreItemInContext_iff, _StoreItemInContext_localItem, _StoreItemInContext_contextNode); \
});})

#define InitIFF(iff, flags, streamHook) ({ \
  struct IFFHandle * _InitIFF_iff = (iff); \
  LONG _InitIFF_flags = (flags); \
  struct Hook * _InitIFF_streamHook = (streamHook); \
  ({ \
  register char * _InitIFF__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"), LONG __asm("d0"), struct Hook * __asm("a1"))) \
  (_InitIFF__bn - 228))(_InitIFF__bn, _InitIFF_iff, _InitIFF_flags, _InitIFF_streamHook); \
});})

#define InitIFFasDOS(iff) ({ \
  struct IFFHandle * _InitIFFasDOS_iff = (iff); \
  ({ \
  register char * _InitIFFasDOS__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"))) \
  (_InitIFFasDOS__bn - 234))(_InitIFFasDOS__bn, _InitIFFasDOS_iff); \
});})

#define InitIFFasClip(iff) ({ \
  struct IFFHandle * _InitIFFasClip_iff = (iff); \
  ({ \
  register char * _InitIFFasClip__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IFFHandle * __asm("a0"))) \
  (_InitIFFasClip__bn - 240))(_InitIFFasClip__bn, _InitIFFasClip_iff); \
});})

#define OpenClipboard(unitNumber) ({ \
  LONG _OpenClipboard_unitNumber = (unitNumber); \
  ({ \
  register char * _OpenClipboard__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((struct ClipboardHandle * (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_OpenClipboard__bn - 246))(_OpenClipboard__bn, _OpenClipboard_unitNumber); \
});})

#define CloseClipboard(clipHandle) ({ \
  struct ClipboardHandle * _CloseClipboard_clipHandle = (clipHandle); \
  ({ \
  register char * _CloseClipboard__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ClipboardHandle * __asm("a0"))) \
  (_CloseClipboard__bn - 252))(_CloseClipboard__bn, _CloseClipboard_clipHandle); \
});})

#define GoodID(id) ({ \
  LONG _GoodID_id = (id); \
  ({ \
  register char * _GoodID__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_GoodID__bn - 258))(_GoodID__bn, _GoodID_id); \
});})

#define GoodType(type) ({ \
  LONG _GoodType_type = (type); \
  ({ \
  register char * _GoodType__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_GoodType__bn - 264))(_GoodType__bn, _GoodType_type); \
});})

#define IDtoStr(id, buf) ({ \
  LONG _IDtoStr_id = (id); \
  STRPTR _IDtoStr_buf = (buf); \
  ({ \
  register char * _IDtoStr__bn __asm("a6") = (char *) (IFFPARSE_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), LONG __asm("d0"), STRPTR __asm("a0"))) \
  (_IDtoStr__bn - 270))(_IDtoStr__bn, _IDtoStr_id, _IDtoStr_buf); \
});})

#endif /*  _INLINE_IFFPARSE_H  */
