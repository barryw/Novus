#ifndef _INLINE_RADIOBUTTON_H
#define _INLINE_RADIOBUTTON_H

#ifndef CLIB_RADIOBUTTON_PROTOS_H
#define CLIB_RADIOBUTTON_PROTOS_H
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
#ifndef  GADGETS_RADIOBUTTON_H
#include <gadgets/radiobutton.h>
#endif

#ifndef RADIOBUTTON_BASE_NAME
#define RADIOBUTTON_BASE_NAME RadioButtonBase
#endif

#define RADIOBUTTON_GetClass() ({ \
  register char * _RADIOBUTTON_GetClass__bn __asm("a6") = (char *) (RADIOBUTTON_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_RADIOBUTTON_GetClass__bn - 30))(_RADIOBUTTON_GetClass__bn); \
})

#define AllocRadioButtonNodeA(columns, tags) ({ \
  ULONG _AllocRadioButtonNodeA_columns = (columns); \
  struct TagItem * _AllocRadioButtonNodeA_tags = (tags); \
  ({ \
  register char * _AllocRadioButtonNodeA__bn __asm("a6") = (char *) (RADIOBUTTON_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), ULONG __asm("d0"), struct TagItem * __asm("a0"))) \
  (_AllocRadioButtonNodeA__bn - 36))(_AllocRadioButtonNodeA__bn, _AllocRadioButtonNodeA_columns, _AllocRadioButtonNodeA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Node * ___AllocRadioButtonNode(struct Library * RadioButtonBase, ULONG columns, ...)
{
  return AllocRadioButtonNodeA(columns, (struct TagItem *) ((ULONG) &columns + sizeof(ULONG)));
}

#define AllocRadioButtonNode(columns...) ___AllocRadioButtonNode(RADIOBUTTON_BASE_NAME, columns)
#endif

#define FreeRadioButtonNode(node) ({ \
  struct Node * _FreeRadioButtonNode_node = (node); \
  ({ \
  register char * _FreeRadioButtonNode__bn __asm("a6") = (char *) (RADIOBUTTON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"))) \
  (_FreeRadioButtonNode__bn - 42))(_FreeRadioButtonNode__bn, _FreeRadioButtonNode_node); \
});})

#define SetRadioButtonNodeAttrsA(node, tags) ({ \
  struct Node * _SetRadioButtonNodeAttrsA_node = (node); \
  struct TagItem * _SetRadioButtonNodeAttrsA_tags = (tags); \
  ({ \
  register char * _SetRadioButtonNodeAttrsA__bn __asm("a6") = (char *) (RADIOBUTTON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_SetRadioButtonNodeAttrsA__bn - 48))(_SetRadioButtonNodeAttrsA__bn, _SetRadioButtonNodeAttrsA_node, _SetRadioButtonNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetRadioButtonNodeAttrs(struct Library * RadioButtonBase, struct Node * node, ...)
{
  SetRadioButtonNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define SetRadioButtonNodeAttrs(node...) ___SetRadioButtonNodeAttrs(RADIOBUTTON_BASE_NAME, node)
#endif

#define GetRadioButtonNodeAttrsA(node, tags) ({ \
  struct Node * _GetRadioButtonNodeAttrsA_node = (node); \
  struct TagItem * _GetRadioButtonNodeAttrsA_tags = (tags); \
  ({ \
  register char * _GetRadioButtonNodeAttrsA__bn __asm("a6") = (char *) (RADIOBUTTON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_GetRadioButtonNodeAttrsA__bn - 54))(_GetRadioButtonNodeAttrsA__bn, _GetRadioButtonNodeAttrsA_node, _GetRadioButtonNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___GetRadioButtonNodeAttrs(struct Library * RadioButtonBase, struct Node * node, ...)
{
  GetRadioButtonNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define GetRadioButtonNodeAttrs(node...) ___GetRadioButtonNodeAttrs(RADIOBUTTON_BASE_NAME, node)
#endif

#endif /*  _INLINE_RADIOBUTTON_H  */
