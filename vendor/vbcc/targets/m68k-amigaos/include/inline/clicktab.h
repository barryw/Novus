#ifndef _INLINE_CLICKTAB_H
#define _INLINE_CLICKTAB_H

#ifndef CLIB_CLICKTAB_PROTOS_H
#define CLIB_CLICKTAB_PROTOS_H
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
#ifndef  GADGETS_CLICKTAB_H
#include <gadgets/clicktab.h>
#endif

#ifndef CLICKTAB_BASE_NAME
#define CLICKTAB_BASE_NAME ClickTabBase
#endif

#define CLICKTAB_GetClass() ({ \
  register char * _CLICKTAB_GetClass__bn __asm("a6") = (char *) (CLICKTAB_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_CLICKTAB_GetClass__bn - 30))(_CLICKTAB_GetClass__bn); \
})

#define AllocClickTabNodeA(tags) ({ \
  struct TagItem * _AllocClickTabNodeA_tags = (tags); \
  ({ \
  register char * _AllocClickTabNodeA__bn __asm("a6") = (char *) (CLICKTAB_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), struct TagItem * __asm("a0"))) \
  (_AllocClickTabNodeA__bn - 36))(_AllocClickTabNodeA__bn, _AllocClickTabNodeA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Node * ___AllocClickTabNode(struct Library * ClickTabBase, Tag tags, ...)
{
  return AllocClickTabNodeA((struct TagItem *) &tags);
}

#define AllocClickTabNode(tags...) ___AllocClickTabNode(CLICKTAB_BASE_NAME, tags)
#endif

#define FreeClickTabNode(node) ({ \
  struct Node * _FreeClickTabNode_node = (node); \
  ({ \
  register char * _FreeClickTabNode__bn __asm("a6") = (char *) (CLICKTAB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"))) \
  (_FreeClickTabNode__bn - 42))(_FreeClickTabNode__bn, _FreeClickTabNode_node); \
});})

#define SetClickTabNodeAttrsA(node, tags) ({ \
  struct Node * _SetClickTabNodeAttrsA_node = (node); \
  struct TagItem * _SetClickTabNodeAttrsA_tags = (tags); \
  ({ \
  register char * _SetClickTabNodeAttrsA__bn __asm("a6") = (char *) (CLICKTAB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_SetClickTabNodeAttrsA__bn - 48))(_SetClickTabNodeAttrsA__bn, _SetClickTabNodeAttrsA_node, _SetClickTabNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetClickTabNodeAttrs(struct Library * ClickTabBase, struct Node * node, ...)
{
  SetClickTabNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define SetClickTabNodeAttrs(node...) ___SetClickTabNodeAttrs(CLICKTAB_BASE_NAME, node)
#endif

#define GetClickTabNodeAttrsA(node, tags) ({ \
  struct Node * _GetClickTabNodeAttrsA_node = (node); \
  struct TagItem * _GetClickTabNodeAttrsA_tags = (tags); \
  ({ \
  register char * _GetClickTabNodeAttrsA__bn __asm("a6") = (char *) (CLICKTAB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_GetClickTabNodeAttrsA__bn - 54))(_GetClickTabNodeAttrsA__bn, _GetClickTabNodeAttrsA_node, _GetClickTabNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___GetClickTabNodeAttrs(struct Library * ClickTabBase, struct Node * node, ...)
{
  GetClickTabNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define GetClickTabNodeAttrs(node...) ___GetClickTabNodeAttrs(CLICKTAB_BASE_NAME, node)
#endif

#endif /*  _INLINE_CLICKTAB_H  */
