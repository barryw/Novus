#ifndef _INLINE_SPEEDBAR_H
#define _INLINE_SPEEDBAR_H

#ifndef CLIB_SPEEDBAR_PROTOS_H
#define CLIB_SPEEDBAR_PROTOS_H
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
#ifndef  GADGETS_SPEEDBAR_H
#include <gadgets/speedbar.h>
#endif

#ifndef SPEEDBAR_BASE_NAME
#define SPEEDBAR_BASE_NAME SpeedBarBase
#endif

#define SPEEDBAR_GetClass() ({ \
  register char * _SPEEDBAR_GetClass__bn __asm("a6") = (char *) (SPEEDBAR_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_SPEEDBAR_GetClass__bn - 30))(_SPEEDBAR_GetClass__bn); \
})

#define AllocSpeedButtonNodeA(number, tags) ({ \
  ULONG _AllocSpeedButtonNodeA_number = (number); \
  struct TagItem * _AllocSpeedButtonNodeA_tags = (tags); \
  ({ \
  register char * _AllocSpeedButtonNodeA__bn __asm("a6") = (char *) (SPEEDBAR_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), ULONG __asm("d0"), struct TagItem * __asm("a0"))) \
  (_AllocSpeedButtonNodeA__bn - 36))(_AllocSpeedButtonNodeA__bn, _AllocSpeedButtonNodeA_number, _AllocSpeedButtonNodeA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Node * ___AllocSpeedButtonNode(struct Library * SpeedBarBase, ULONG number, ...)
{
  return AllocSpeedButtonNodeA(number, (struct TagItem *) ((ULONG) &number + sizeof(ULONG)));
}

#define AllocSpeedButtonNode(number...) ___AllocSpeedButtonNode(SPEEDBAR_BASE_NAME, number)
#endif

#define FreeSpeedButtonNode(node) ({ \
  struct Node * _FreeSpeedButtonNode_node = (node); \
  ({ \
  register char * _FreeSpeedButtonNode__bn __asm("a6") = (char *) (SPEEDBAR_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"))) \
  (_FreeSpeedButtonNode__bn - 42))(_FreeSpeedButtonNode__bn, _FreeSpeedButtonNode_node); \
});})

#define SetSpeedButtonNodeAttrsA(node, tags) ({ \
  struct Node * _SetSpeedButtonNodeAttrsA_node = (node); \
  struct TagItem * _SetSpeedButtonNodeAttrsA_tags = (tags); \
  ({ \
  register char * _SetSpeedButtonNodeAttrsA__bn __asm("a6") = (char *) (SPEEDBAR_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_SetSpeedButtonNodeAttrsA__bn - 48))(_SetSpeedButtonNodeAttrsA__bn, _SetSpeedButtonNodeAttrsA_node, _SetSpeedButtonNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetSpeedButtonNodeAttrs(struct Library * SpeedBarBase, struct Node * node, ...)
{
  SetSpeedButtonNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define SetSpeedButtonNodeAttrs(node...) ___SetSpeedButtonNodeAttrs(SPEEDBAR_BASE_NAME, node)
#endif

#define GetSpeedButtonNodeAttrsA(node, tags) ({ \
  struct Node * _GetSpeedButtonNodeAttrsA_node = (node); \
  struct TagItem * _GetSpeedButtonNodeAttrsA_tags = (tags); \
  ({ \
  register char * _GetSpeedButtonNodeAttrsA__bn __asm("a6") = (char *) (SPEEDBAR_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_GetSpeedButtonNodeAttrsA__bn - 54))(_GetSpeedButtonNodeAttrsA__bn, _GetSpeedButtonNodeAttrsA_node, _GetSpeedButtonNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___GetSpeedButtonNodeAttrs(struct Library * SpeedBarBase, struct Node * node, ...)
{
  GetSpeedButtonNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define GetSpeedButtonNodeAttrs(node...) ___GetSpeedButtonNodeAttrs(SPEEDBAR_BASE_NAME, node)
#endif

#endif /*  _INLINE_SPEEDBAR_H  */
