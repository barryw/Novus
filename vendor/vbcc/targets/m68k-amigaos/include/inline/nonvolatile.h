#ifndef _INLINE_NONVOLATILE_H
#define _INLINE_NONVOLATILE_H

#ifndef CLIB_NONVOLATILE_PROTOS_H
#define CLIB_NONVOLATILE_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  EXEC_LISTS_H
#include <exec/lists.h>
#endif
#ifndef  LIBRARIES_NONVOLATILE_H
#include <libraries/nonvolatile.h>
#endif

#ifndef NONVOLATILE_BASE_NAME
#define NONVOLATILE_BASE_NAME NVBase
#endif

#define GetCopyNV(appName, itemName, killRequesters) ({ \
  CONST_STRPTR _GetCopyNV_appName = (appName); \
  CONST_STRPTR _GetCopyNV_itemName = (itemName); \
  LONG _GetCopyNV_killRequesters = (killRequesters); \
  ({ \
  register char * _GetCopyNV__bn __asm("a6") = (char *) (NONVOLATILE_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST_STRPTR __asm("a1"), LONG __asm("d1"))) \
  (_GetCopyNV__bn - 30))(_GetCopyNV__bn, _GetCopyNV_appName, _GetCopyNV_itemName, _GetCopyNV_killRequesters); \
});})

#define FreeNVData(data) ({ \
  APTR _FreeNVData_data = (data); \
  ({ \
  register char * _FreeNVData__bn __asm("a6") = (char *) (NONVOLATILE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_FreeNVData__bn - 36))(_FreeNVData__bn, _FreeNVData_data); \
});})

#define StoreNV(appName, itemName, data, length, killRequesters) ({ \
  CONST_STRPTR _StoreNV_appName = (appName); \
  CONST_STRPTR _StoreNV_itemName = (itemName); \
  CONST_APTR _StoreNV_data = (data); \
  ULONG _StoreNV_length = (length); \
  LONG _StoreNV_killRequesters = (killRequesters); \
  ({ \
  register char * _StoreNV__bn __asm("a6") = (char *) (NONVOLATILE_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST_STRPTR __asm("a1"), CONST_APTR __asm("a2"), ULONG __asm("d0"), LONG __asm("d1"))) \
  (_StoreNV__bn - 42))(_StoreNV__bn, _StoreNV_appName, _StoreNV_itemName, _StoreNV_data, _StoreNV_length, _StoreNV_killRequesters); \
});})

#define DeleteNV(appName, itemName, killRequesters) ({ \
  CONST_STRPTR _DeleteNV_appName = (appName); \
  CONST_STRPTR _DeleteNV_itemName = (itemName); \
  LONG _DeleteNV_killRequesters = (killRequesters); \
  ({ \
  register char * _DeleteNV__bn __asm("a6") = (char *) (NONVOLATILE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST_STRPTR __asm("a1"), LONG __asm("d1"))) \
  (_DeleteNV__bn - 48))(_DeleteNV__bn, _DeleteNV_appName, _DeleteNV_itemName, _DeleteNV_killRequesters); \
});})

#define GetNVInfo(killRequesters) ({ \
  LONG _GetNVInfo_killRequesters = (killRequesters); \
  ({ \
  register char * _GetNVInfo__bn __asm("a6") = (char *) (NONVOLATILE_BASE_NAME);\
  ((struct NVInfo * (*)(char * __asm("a6"), LONG __asm("d1"))) \
  (_GetNVInfo__bn - 54))(_GetNVInfo__bn, _GetNVInfo_killRequesters); \
});})

#define GetNVList(appName, killRequesters) ({ \
  CONST_STRPTR _GetNVList_appName = (appName); \
  LONG _GetNVList_killRequesters = (killRequesters); \
  ({ \
  register char * _GetNVList__bn __asm("a6") = (char *) (NONVOLATILE_BASE_NAME);\
  ((struct MinList * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), LONG __asm("d1"))) \
  (_GetNVList__bn - 60))(_GetNVList__bn, _GetNVList_appName, _GetNVList_killRequesters); \
});})

#define SetNVProtection(appName, itemName, mask, killRequesters) ({ \
  CONST_STRPTR _SetNVProtection_appName = (appName); \
  CONST_STRPTR _SetNVProtection_itemName = (itemName); \
  LONG _SetNVProtection_mask = (mask); \
  LONG _SetNVProtection_killRequesters = (killRequesters); \
  ({ \
  register char * _SetNVProtection__bn __asm("a6") = (char *) (NONVOLATILE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST_STRPTR __asm("a1"), LONG __asm("d2"), LONG __asm("d1"))) \
  (_SetNVProtection__bn - 66))(_SetNVProtection__bn, _SetNVProtection_appName, _SetNVProtection_itemName, _SetNVProtection_mask, _SetNVProtection_killRequesters); \
});})

#endif /*  _INLINE_NONVOLATILE_H  */
