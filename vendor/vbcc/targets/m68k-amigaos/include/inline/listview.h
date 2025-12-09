#ifndef _INLINE_LISTVIEW_H
#define _INLINE_LISTVIEW_H

#ifndef CLIB_LISTVIEW_PROTOS_H
#define CLIB_LISTVIEW_PROTOS_H
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
#ifndef  GADGETS_LISTVIEW_H
#include <gadgets/listview.h>
#endif

#ifndef LISTVIEW_BASE_NAME
#define LISTVIEW_BASE_NAME ListViewBase
#endif

#define LISTVIEW_GetClass() ({ \
  register char * _LISTVIEW_GetClass__bn __asm("a6") = (char *) (LISTVIEW_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_LISTVIEW_GetClass__bn - 30))(_LISTVIEW_GetClass__bn); \
})

#endif /*  _INLINE_LISTVIEW_H  */
