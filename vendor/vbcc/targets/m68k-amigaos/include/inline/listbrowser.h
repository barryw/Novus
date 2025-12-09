#ifndef _INLINE_LISTBROWSER_H
#define _INLINE_LISTBROWSER_H

#ifndef CLIB_LISTBROWSER_PROTOS_H
#define CLIB_LISTBROWSER_PROTOS_H
#endif

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
#ifndef  GADGETS_LISTBROWSER_H
#include <gadgets/listbrowser.h>
#endif

#ifndef LISTBROWSER_BASE_NAME
#define LISTBROWSER_BASE_NAME ListBrowserBase
#endif

#define LISTBROWSER_GetClass() ({ \
  register char * _LISTBROWSER_GetClass__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_LISTBROWSER_GetClass__bn - 30))(_LISTBROWSER_GetClass__bn); \
})

#define AllocListBrowserNodeA(columns, tags) ({ \
  ULONG _AllocListBrowserNodeA_columns = (columns); \
  struct TagItem * _AllocListBrowserNodeA_tags = (tags); \
  ({ \
  register char * _AllocListBrowserNodeA__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), ULONG __asm("d0"), struct TagItem * __asm("a0"))) \
  (_AllocListBrowserNodeA__bn - 36))(_AllocListBrowserNodeA__bn, _AllocListBrowserNodeA_columns, _AllocListBrowserNodeA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Node * ___AllocListBrowserNode(struct Library * ListBrowserBase, ULONG columns, ...)
{
  return AllocListBrowserNodeA(columns, (struct TagItem *) ((ULONG) &columns + sizeof(ULONG)));
}

#define AllocListBrowserNode(columns...) ___AllocListBrowserNode(LISTBROWSER_BASE_NAME, columns)
#endif

#define FreeListBrowserNode(node) ({ \
  struct Node * _FreeListBrowserNode_node = (node); \
  ({ \
  register char * _FreeListBrowserNode__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"))) \
  (_FreeListBrowserNode__bn - 42))(_FreeListBrowserNode__bn, _FreeListBrowserNode_node); \
});})

#define SetListBrowserNodeAttrsA(node, tags) ({ \
  struct Node * _SetListBrowserNodeAttrsA_node = (node); \
  struct TagItem * _SetListBrowserNodeAttrsA_tags = (tags); \
  ({ \
  register char * _SetListBrowserNodeAttrsA__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_SetListBrowserNodeAttrsA__bn - 48))(_SetListBrowserNodeAttrsA__bn, _SetListBrowserNodeAttrsA_node, _SetListBrowserNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetListBrowserNodeAttrs(struct Library * ListBrowserBase, struct Node * node, ...)
{
  SetListBrowserNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define SetListBrowserNodeAttrs(node...) ___SetListBrowserNodeAttrs(LISTBROWSER_BASE_NAME, node)
#endif

#define GetListBrowserNodeAttrsA(node, tags) ({ \
  struct Node * _GetListBrowserNodeAttrsA_node = (node); \
  struct TagItem * _GetListBrowserNodeAttrsA_tags = (tags); \
  ({ \
  register char * _GetListBrowserNodeAttrsA__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_GetListBrowserNodeAttrsA__bn - 54))(_GetListBrowserNodeAttrsA__bn, _GetListBrowserNodeAttrsA_node, _GetListBrowserNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___GetListBrowserNodeAttrs(struct Library * ListBrowserBase, struct Node * node, ...)
{
  GetListBrowserNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define GetListBrowserNodeAttrs(node...) ___GetListBrowserNodeAttrs(LISTBROWSER_BASE_NAME, node)
#endif

#define ListBrowserSelectAll(list) ({ \
  struct List * _ListBrowserSelectAll_list = (list); \
  ({ \
  register char * _ListBrowserSelectAll__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_ListBrowserSelectAll__bn - 60))(_ListBrowserSelectAll__bn, _ListBrowserSelectAll_list); \
});})

#define ShowListBrowserNodeChildren(node, depth) ({ \
  struct Node * _ShowListBrowserNodeChildren_node = (node); \
  LONG _ShowListBrowserNodeChildren_depth = (depth); \
  ({ \
  register char * _ShowListBrowserNodeChildren__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), LONG __asm("d0"))) \
  (_ShowListBrowserNodeChildren__bn - 66))(_ShowListBrowserNodeChildren__bn, _ShowListBrowserNodeChildren_node, _ShowListBrowserNodeChildren_depth); \
});})

#define HideListBrowserNodeChildren(node) ({ \
  struct Node * _HideListBrowserNodeChildren_node = (node); \
  ({ \
  register char * _HideListBrowserNodeChildren__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"))) \
  (_HideListBrowserNodeChildren__bn - 72))(_HideListBrowserNodeChildren__bn, _HideListBrowserNodeChildren_node); \
});})

#define ShowAllListBrowserChildren(list) ({ \
  struct List * _ShowAllListBrowserChildren_list = (list); \
  ({ \
  register char * _ShowAllListBrowserChildren__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_ShowAllListBrowserChildren__bn - 78))(_ShowAllListBrowserChildren__bn, _ShowAllListBrowserChildren_list); \
});})

#define HideAllListBrowserChildren(list) ({ \
  struct List * _HideAllListBrowserChildren_list = (list); \
  ({ \
  register char * _HideAllListBrowserChildren__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_HideAllListBrowserChildren__bn - 84))(_HideAllListBrowserChildren__bn, _HideAllListBrowserChildren_list); \
});})

#define FreeListBrowserList(list) ({ \
  struct List * _FreeListBrowserList_list = (list); \
  ({ \
  register char * _FreeListBrowserList__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_FreeListBrowserList__bn - 90))(_FreeListBrowserList__bn, _FreeListBrowserList_list); \
});})

#define AllocLBColumnInfoA(columns, tags) ({ \
  ULONG _AllocLBColumnInfoA_columns = (columns); \
  struct TagItem * _AllocLBColumnInfoA_tags = (tags); \
  ({ \
  register char * _AllocLBColumnInfoA__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((struct ColumnInfo * (*)(char * __asm("a6"), ULONG __asm("d0"), struct TagItem * __asm("a0"))) \
  (_AllocLBColumnInfoA__bn - 96))(_AllocLBColumnInfoA__bn, _AllocLBColumnInfoA_columns, _AllocLBColumnInfoA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct ColumnInfo * ___AllocLBColumnInfo(struct Library * ListBrowserBase, ULONG columns, ...)
{
  return AllocLBColumnInfoA(columns, (struct TagItem *) ((ULONG) &columns + sizeof(ULONG)));
}

#define AllocLBColumnInfo(columns...) ___AllocLBColumnInfo(LISTBROWSER_BASE_NAME, columns)
#endif

#define SetLBColumnInfoAttrsA(columninfo, tags) ({ \
  struct ColumnInfo * _SetLBColumnInfoAttrsA_columninfo = (columninfo); \
  struct TagItem * _SetLBColumnInfoAttrsA_tags = (tags); \
  ({ \
  register char * _SetLBColumnInfoAttrsA__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct ColumnInfo * __asm("a1"), struct TagItem * __asm("a0"))) \
  (_SetLBColumnInfoAttrsA__bn - 102))(_SetLBColumnInfoAttrsA__bn, _SetLBColumnInfoAttrsA_columninfo, _SetLBColumnInfoAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___SetLBColumnInfoAttrs(struct Library * ListBrowserBase, struct ColumnInfo * columninfo, ...)
{
  return SetLBColumnInfoAttrsA(columninfo, (struct TagItem *) ((ULONG) &columninfo + sizeof(struct ColumnInfo *)));
}

#define SetLBColumnInfoAttrs(columninfo...) ___SetLBColumnInfoAttrs(LISTBROWSER_BASE_NAME, columninfo)
#endif

#define GetLBColumnInfoAttrsA(columninfo, tags) ({ \
  struct ColumnInfo * _GetLBColumnInfoAttrsA_columninfo = (columninfo); \
  struct TagItem * _GetLBColumnInfoAttrsA_tags = (tags); \
  ({ \
  register char * _GetLBColumnInfoAttrsA__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct ColumnInfo * __asm("a1"), struct TagItem * __asm("a0"))) \
  (_GetLBColumnInfoAttrsA__bn - 108))(_GetLBColumnInfoAttrsA__bn, _GetLBColumnInfoAttrsA_columninfo, _GetLBColumnInfoAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___GetLBColumnInfoAttrs(struct Library * ListBrowserBase, struct ColumnInfo * columninfo, ...)
{
  return GetLBColumnInfoAttrsA(columninfo, (struct TagItem *) ((ULONG) &columninfo + sizeof(struct ColumnInfo *)));
}

#define GetLBColumnInfoAttrs(columninfo...) ___GetLBColumnInfoAttrs(LISTBROWSER_BASE_NAME, columninfo)
#endif

#define FreeLBColumnInfo(columninfo) ({ \
  struct ColumnInfo * _FreeLBColumnInfo_columninfo = (columninfo); \
  ({ \
  register char * _FreeLBColumnInfo__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ColumnInfo * __asm("a0"))) \
  (_FreeLBColumnInfo__bn - 114))(_FreeLBColumnInfo__bn, _FreeLBColumnInfo_columninfo); \
});})

#define ListBrowserClearAll(list) ({ \
  struct List * _ListBrowserClearAll_list = (list); \
  ({ \
  register char * _ListBrowserClearAll__bn __asm("a6") = (char *) (LISTBROWSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_ListBrowserClearAll__bn - 120))(_ListBrowserClearAll__bn, _ListBrowserClearAll_list); \
});})

#endif /*  _INLINE_LISTBROWSER_H  */
