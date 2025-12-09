#ifndef _INLINE_CHOOSER_H
#define _INLINE_CHOOSER_H

#ifndef CLIB_CHOOSER_PROTOS_H
#define CLIB_CHOOSER_PROTOS_H
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
#ifndef  GADGETS_CHOOSER_H
#include <gadgets/chooser.h>
#endif

#ifndef CHOOSER_BASE_NAME
#define CHOOSER_BASE_NAME ChooserBase
#endif

#define CHOOSER_GetClass() ({ \
  register char * _CHOOSER_GetClass__bn __asm("a6") = (char *) (CHOOSER_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_CHOOSER_GetClass__bn - 30))(_CHOOSER_GetClass__bn); \
})

#define AllocChooserNodeA(tags) ({ \
  struct TagItem * _AllocChooserNodeA_tags = (tags); \
  ({ \
  register char * _AllocChooserNodeA__bn __asm("a6") = (char *) (CHOOSER_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), struct TagItem * __asm("a0"))) \
  (_AllocChooserNodeA__bn - 36))(_AllocChooserNodeA__bn, _AllocChooserNodeA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Node * ___AllocChooserNode(struct Library * ChooserBase, Tag tags, ...)
{
  return AllocChooserNodeA((struct TagItem *) &tags);
}

#define AllocChooserNode(tags...) ___AllocChooserNode(CHOOSER_BASE_NAME, tags)
#endif

#define FreeChooserNode(node) ({ \
  struct Node * _FreeChooserNode_node = (node); \
  ({ \
  register char * _FreeChooserNode__bn __asm("a6") = (char *) (CHOOSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"))) \
  (_FreeChooserNode__bn - 42))(_FreeChooserNode__bn, _FreeChooserNode_node); \
});})

#define SetChooserNodeAttrsA(node, tags) ({ \
  struct Node * _SetChooserNodeAttrsA_node = (node); \
  struct TagItem * _SetChooserNodeAttrsA_tags = (tags); \
  ({ \
  register char * _SetChooserNodeAttrsA__bn __asm("a6") = (char *) (CHOOSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_SetChooserNodeAttrsA__bn - 48))(_SetChooserNodeAttrsA__bn, _SetChooserNodeAttrsA_node, _SetChooserNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___SetChooserNodeAttrs(struct Library * ChooserBase, struct Node * node, ...)
{
  SetChooserNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define SetChooserNodeAttrs(node...) ___SetChooserNodeAttrs(CHOOSER_BASE_NAME, node)
#endif

#define GetChooserNodeAttrsA(node, tags) ({ \
  struct Node * _GetChooserNodeAttrsA_node = (node); \
  struct TagItem * _GetChooserNodeAttrsA_tags = (tags); \
  ({ \
  register char * _GetChooserNodeAttrsA__bn __asm("a6") = (char *) (CHOOSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a0"), struct TagItem * __asm("a1"))) \
  (_GetChooserNodeAttrsA__bn - 54))(_GetChooserNodeAttrsA__bn, _GetChooserNodeAttrsA_node, _GetChooserNodeAttrsA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___GetChooserNodeAttrs(struct Library * ChooserBase, struct Node * node, ...)
{
  GetChooserNodeAttrsA(node, (struct TagItem *) ((ULONG) &node + sizeof(struct Node *)));
}

#define GetChooserNodeAttrs(node...) ___GetChooserNodeAttrs(CHOOSER_BASE_NAME, node)
#endif

#define ShowChooser(o, w, xpos, ypos) ({ \
  Object * _ShowChooser_o = (o); \
  struct Window * _ShowChooser_w = (w); \
  ULONG _ShowChooser_xpos = (xpos); \
  ULONG _ShowChooser_ypos = (ypos); \
  ({ \
  register char * _ShowChooser__bn __asm("a6") = (char *) (CHOOSER_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), Object * __asm("a0"), struct Window * __asm("a1"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_ShowChooser__bn - 60))(_ShowChooser__bn, _ShowChooser_o, _ShowChooser_w, _ShowChooser_xpos, _ShowChooser_ypos); \
});})

#define HideChooser(o, w) ({ \
  Object * _HideChooser_o = (o); \
  struct Window * _HideChooser_w = (w); \
  ({ \
  register char * _HideChooser__bn __asm("a6") = (char *) (CHOOSER_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), Object * __asm("a0"), struct Window * __asm("a1"))) \
  (_HideChooser__bn - 66))(_HideChooser__bn, _HideChooser_o, _HideChooser_w); \
});})

#endif /*  _INLINE_CHOOSER_H  */
