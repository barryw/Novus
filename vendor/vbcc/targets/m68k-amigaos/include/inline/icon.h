#ifndef _INLINE_ICON_H
#define _INLINE_ICON_H

#ifndef CLIB_ICON_PROTOS_H
#define CLIB_ICON_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  WORKBENCH_WORKBENCH_H
#include <workbench/workbench.h>
#endif
#ifndef  DATATYPES_PICTURECLASS_H
#include <datatypes/pictureclass.h>
#endif

#ifndef ICON_BASE_NAME
#define ICON_BASE_NAME IconBase
#endif

#define FreeFreeList(freelist) ({ \
  struct FreeList * _FreeFreeList_freelist = (freelist); \
  ({ \
  register char * _FreeFreeList__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct FreeList * __asm("a0"))) \
  (_FreeFreeList__bn - 54))(_FreeFreeList__bn, _FreeFreeList_freelist); \
});})

#define AddFreeList(freelist, mem, size) ({ \
  struct FreeList * _AddFreeList_freelist = (freelist); \
  CONST_APTR _AddFreeList_mem = (mem); \
  ULONG _AddFreeList_size = (size); \
  ({ \
  register char * _AddFreeList__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct FreeList * __asm("a0"), CONST_APTR __asm("a1"), ULONG __asm("a2"))) \
  (_AddFreeList__bn - 72))(_AddFreeList__bn, _AddFreeList_freelist, _AddFreeList_mem, _AddFreeList_size); \
});})

#define GetDiskObject(name) ({ \
  CONST_STRPTR _GetDiskObject_name = (name); \
  ({ \
  register char * _GetDiskObject__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((struct DiskObject * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_GetDiskObject__bn - 78))(_GetDiskObject__bn, _GetDiskObject_name); \
});})

#define PutDiskObject(name, diskobj) ({ \
  CONST_STRPTR _PutDiskObject_name = (name); \
  CONST struct DiskObject * _PutDiskObject_diskobj = (diskobj); \
  ({ \
  register char * _PutDiskObject__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct DiskObject * __asm("a1"))) \
  (_PutDiskObject__bn - 84))(_PutDiskObject__bn, _PutDiskObject_name, _PutDiskObject_diskobj); \
});})

#define FreeDiskObject(diskobj) ({ \
  struct DiskObject * _FreeDiskObject_diskobj = (diskobj); \
  ({ \
  register char * _FreeDiskObject__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct DiskObject * __asm("a0"))) \
  (_FreeDiskObject__bn - 90))(_FreeDiskObject__bn, _FreeDiskObject_diskobj); \
});})

#define FindToolType(toolTypeArray, typeName) ({ \
  CONST_STRPTR * _FindToolType_toolTypeArray = (toolTypeArray); \
  CONST_STRPTR _FindToolType_typeName = (typeName); \
  ({ \
  register char * _FindToolType__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((UBYTE * (*)(char * __asm("a6"), CONST_STRPTR * __asm("a0"), CONST_STRPTR __asm("a1"))) \
  (_FindToolType__bn - 96))(_FindToolType__bn, _FindToolType_toolTypeArray, _FindToolType_typeName); \
});})

#define MatchToolValue(typeString, value) ({ \
  CONST_STRPTR _MatchToolValue_typeString = (typeString); \
  CONST_STRPTR _MatchToolValue_value = (value); \
  ({ \
  register char * _MatchToolValue__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST_STRPTR __asm("a1"))) \
  (_MatchToolValue__bn - 102))(_MatchToolValue__bn, _MatchToolValue_typeString, _MatchToolValue_value); \
});})

#define BumpRevision(newname, oldname) ({ \
  STRPTR _BumpRevision_newname = (newname); \
  CONST_STRPTR _BumpRevision_oldname = (oldname); \
  ({ \
  register char * _BumpRevision__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), STRPTR __asm("a0"), CONST_STRPTR __asm("a1"))) \
  (_BumpRevision__bn - 108))(_BumpRevision__bn, _BumpRevision_newname, _BumpRevision_oldname); \
});})

#define FreeAlloc(free, len, type) ({ \
  struct FreeList * _FreeAlloc_free = (free); \
  ULONG _FreeAlloc_len = (len); \
  ULONG _FreeAlloc_type = (type); \
  ({ \
  register char * _FreeAlloc__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct FreeList * __asm("a0"), ULONG __asm("a1"), ULONG __asm("a2"))) \
  (_FreeAlloc__bn - 114))(_FreeAlloc__bn, _FreeAlloc_free, _FreeAlloc_len, _FreeAlloc_type); \
});})

#define GetDefDiskObject(type) ({ \
  LONG _GetDefDiskObject_type = (type); \
  ({ \
  register char * _GetDefDiskObject__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((struct DiskObject * (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_GetDefDiskObject__bn - 120))(_GetDefDiskObject__bn, _GetDefDiskObject_type); \
});})

#define PutDefDiskObject(diskObject) ({ \
  CONST struct DiskObject * _PutDefDiskObject_diskObject = (diskObject); \
  ({ \
  register char * _PutDefDiskObject__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct DiskObject * __asm("a0"))) \
  (_PutDefDiskObject__bn - 126))(_PutDefDiskObject__bn, _PutDefDiskObject_diskObject); \
});})

#define GetDiskObjectNew(name) ({ \
  CONST_STRPTR _GetDiskObjectNew_name = (name); \
  ({ \
  register char * _GetDiskObjectNew__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((struct DiskObject * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_GetDiskObjectNew__bn - 132))(_GetDiskObjectNew__bn, _GetDiskObjectNew_name); \
});})

#define DeleteDiskObject(name) ({ \
  CONST_STRPTR _DeleteDiskObject_name = (name); \
  ({ \
  register char * _DeleteDiskObject__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_DeleteDiskObject__bn - 138))(_DeleteDiskObject__bn, _DeleteDiskObject_name); \
});})

#define FreeFree(fl, address) ({ \
  struct FreeList * _FreeFree_fl = (fl); \
  APTR _FreeFree_address = (address); \
  ({ \
  register char * _FreeFree__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct FreeList * __asm("a0"), APTR __asm("a1"))) \
  (_FreeFree__bn - 144))(_FreeFree__bn, _FreeFree_fl, _FreeFree_address); \
});})

#define DupDiskObjectA(diskObject, tags) ({ \
  CONST struct DiskObject * _DupDiskObjectA_diskObject = (diskObject); \
  CONST struct TagItem * _DupDiskObjectA_tags = (tags); \
  ({ \
  register char * _DupDiskObjectA__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((struct DiskObject * (*)(char * __asm("a6"), CONST struct DiskObject * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_DupDiskObjectA__bn - 150))(_DupDiskObjectA__bn, _DupDiskObjectA_diskObject, _DupDiskObjectA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct DiskObject * ___DupDiskObject(struct Library * IconBase, CONST struct DiskObject * diskObject, ...)
{
  return DupDiskObjectA(diskObject, (CONST struct TagItem *) ((ULONG) &diskObject + sizeof(CONST struct DiskObject *)));
}

#define DupDiskObject(diskObject...) ___DupDiskObject(ICON_BASE_NAME, diskObject)
#endif

#define IconControlA(icon, tags) ({ \
  struct DiskObject * _IconControlA_icon = (icon); \
  CONST struct TagItem * _IconControlA_tags = (tags); \
  ({ \
  register char * _IconControlA__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct DiskObject * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_IconControlA__bn - 156))(_IconControlA__bn, _IconControlA_icon, _IconControlA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___IconControl(struct Library * IconBase, struct DiskObject * icon, ...)
{
  return IconControlA(icon, (CONST struct TagItem *) ((ULONG) &icon + sizeof(struct DiskObject *)));
}

#define IconControl(icon...) ___IconControl(ICON_BASE_NAME, icon)
#endif

#define DrawIconStateA(rp, icon, label, leftOffset, topOffset, state, tags) ({ \
  struct RastPort * _DrawIconStateA_rp = (rp); \
  CONST struct DiskObject * _DrawIconStateA_icon = (icon); \
  CONST_STRPTR _DrawIconStateA_label = (label); \
  LONG _DrawIconStateA_leftOffset = (leftOffset); \
  LONG _DrawIconStateA_topOffset = (topOffset); \
  ULONG _DrawIconStateA_state = (state); \
  CONST struct TagItem * _DrawIconStateA_tags = (tags); \
  ({ \
  register char * _DrawIconStateA__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct DiskObject * __asm("a1"), CONST_STRPTR __asm("a2"), LONG __asm("d0"), LONG __asm("d1"), ULONG __asm("d2"), CONST struct TagItem * __asm("a3"))) \
  (_DrawIconStateA__bn - 162))(_DrawIconStateA__bn, _DrawIconStateA_rp, _DrawIconStateA_icon, _DrawIconStateA_label, _DrawIconStateA_leftOffset, _DrawIconStateA_topOffset, _DrawIconStateA_state, _DrawIconStateA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___DrawIconState(struct Library * IconBase, struct RastPort * rp, CONST struct DiskObject * icon, CONST_STRPTR label, LONG leftOffset, LONG topOffset, ULONG state, ...)
{
  DrawIconStateA(rp, icon, label, leftOffset, topOffset, state, (CONST struct TagItem *) ((ULONG) &state + sizeof(ULONG)));
}

#define DrawIconState(rp, icon, label, leftOffset, topOffset, state...) ___DrawIconState(ICON_BASE_NAME, rp, icon, label, leftOffset, topOffset, state)
#endif

#define GetIconRectangleA(rp, icon, label, rect, tags) ({ \
  struct RastPort * _GetIconRectangleA_rp = (rp); \
  CONST struct DiskObject * _GetIconRectangleA_icon = (icon); \
  CONST_STRPTR _GetIconRectangleA_label = (label); \
  struct Rectangle * _GetIconRectangleA_rect = (rect); \
  CONST struct TagItem * _GetIconRectangleA_tags = (tags); \
  ({ \
  register char * _GetIconRectangleA__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct RastPort * __asm("a0"), CONST struct DiskObject * __asm("a1"), CONST_STRPTR __asm("a2"), struct Rectangle * __asm("a3"), CONST struct TagItem * __asm("a4"))) \
  (_GetIconRectangleA__bn - 168))(_GetIconRectangleA__bn, _GetIconRectangleA_rp, _GetIconRectangleA_icon, _GetIconRectangleA_label, _GetIconRectangleA_rect, _GetIconRectangleA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___GetIconRectangle(struct Library * IconBase, struct RastPort * rp, CONST struct DiskObject * icon, CONST_STRPTR label, struct Rectangle * rect, ...)
{
  return GetIconRectangleA(rp, icon, label, rect, (CONST struct TagItem *) ((ULONG) &rect + sizeof(struct Rectangle *)));
}

#define GetIconRectangle(rp, icon, label, rect...) ___GetIconRectangle(ICON_BASE_NAME, rp, icon, label, rect)
#endif

#define NewDiskObject(type) ({ \
  LONG _NewDiskObject_type = (type); \
  ({ \
  register char * _NewDiskObject__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((struct DiskObject * (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_NewDiskObject__bn - 174))(_NewDiskObject__bn, _NewDiskObject_type); \
});})

#define GetIconTagList(name, tags) ({ \
  CONST_STRPTR _GetIconTagList_name = (name); \
  CONST struct TagItem * _GetIconTagList_tags = (tags); \
  ({ \
  register char * _GetIconTagList__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((struct DiskObject * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_GetIconTagList__bn - 180))(_GetIconTagList__bn, _GetIconTagList_name, _GetIconTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct DiskObject * ___GetIconTags(struct Library * IconBase, CONST_STRPTR name, ...)
{
  return GetIconTagList(name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define GetIconTags(name...) ___GetIconTags(ICON_BASE_NAME, name)
#endif

#define PutIconTagList(name, icon, tags) ({ \
  CONST_STRPTR _PutIconTagList_name = (name); \
  CONST struct DiskObject * _PutIconTagList_icon = (icon); \
  CONST struct TagItem * _PutIconTagList_tags = (tags); \
  ({ \
  register char * _PutIconTagList__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct DiskObject * __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_PutIconTagList__bn - 186))(_PutIconTagList__bn, _PutIconTagList_name, _PutIconTagList_icon, _PutIconTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___PutIconTags(struct Library * IconBase, CONST_STRPTR name, CONST struct DiskObject * icon, ...)
{
  return PutIconTagList(name, icon, (CONST struct TagItem *) ((ULONG) &icon + sizeof(CONST struct DiskObject *)));
}

#define PutIconTags(name, icon...) ___PutIconTags(ICON_BASE_NAME, name, icon)
#endif

#define LayoutIconA(icon, screen, tags) ({ \
  struct DiskObject * _LayoutIconA_icon = (icon); \
  struct Screen * _LayoutIconA_screen = (screen); \
  struct TagItem * _LayoutIconA_tags = (tags); \
  ({ \
  register char * _LayoutIconA__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct DiskObject * __asm("a0"), struct Screen * __asm("a1"), struct TagItem * __asm("a2"))) \
  (_LayoutIconA__bn - 192))(_LayoutIconA__bn, _LayoutIconA_icon, _LayoutIconA_screen, _LayoutIconA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___LayoutIcon(struct Library * IconBase, struct DiskObject * icon, struct Screen * screen, ...)
{
  return LayoutIconA(icon, screen, (struct TagItem *) ((ULONG) &screen + sizeof(struct Screen *)));
}

#define LayoutIcon(icon, screen...) ___LayoutIcon(ICON_BASE_NAME, icon, screen)
#endif

#define ChangeToSelectedIconColor(cr) ({ \
  struct ColorRegister * _ChangeToSelectedIconColor_cr = (cr); \
  ({ \
  register char * _ChangeToSelectedIconColor__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ColorRegister * __asm("a0"))) \
  (_ChangeToSelectedIconColor__bn - 198))(_ChangeToSelectedIconColor__bn, _ChangeToSelectedIconColor_cr); \
});})

#define BumpRevisionLength(newname, oldname, maxLength) ({ \
  STRPTR _BumpRevisionLength_newname = (newname); \
  CONST_STRPTR _BumpRevisionLength_oldname = (oldname); \
  ULONG _BumpRevisionLength_maxLength = (maxLength); \
  ({ \
  register char * _BumpRevisionLength__bn __asm("a6") = (char *) (ICON_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), STRPTR __asm("a0"), CONST_STRPTR __asm("a1"), ULONG __asm("d0"))) \
  (_BumpRevisionLength__bn - 204))(_BumpRevisionLength__bn, _BumpRevisionLength_newname, _BumpRevisionLength_oldname, _BumpRevisionLength_maxLength); \
});})

#endif /*  _INLINE_ICON_H  */
