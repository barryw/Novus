#ifndef _INLINE_WB_H
#define _INLINE_WB_H

#ifndef CLIB_WB_PROTOS_H
#define CLIB_WB_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  DOS_DOS_H
#include <dos/dos.h>
#endif
#ifndef  WORKBENCH_WORKBENCH_H
#include <workbench/workbench.h>
#endif
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif

#ifndef WB_BASE_NAME
#define WB_BASE_NAME WorkbenchBase
#endif

#define UpdateWorkbench(name, lock, action) ({ \
  CONST_STRPTR _UpdateWorkbench_name = (name); \
  BPTR _UpdateWorkbench_lock = (lock); \
  LONG _UpdateWorkbench_action = (action); \
  ({ \
  register char * _UpdateWorkbench__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), BPTR __asm("a1"), LONG __asm("d0"))) \
  (_UpdateWorkbench__bn - 30))(_UpdateWorkbench__bn, _UpdateWorkbench_name, _UpdateWorkbench_lock, _UpdateWorkbench_action); \
});})

#define AddAppWindowA(id, userdata, window, msgport, taglist) ({ \
  ULONG _AddAppWindowA_id = (id); \
  ULONG _AddAppWindowA_userdata = (userdata); \
  struct Window * _AddAppWindowA_window = (window); \
  struct MsgPort * _AddAppWindowA_msgport = (msgport); \
  CONST struct TagItem * _AddAppWindowA_taglist = (taglist); \
  ({ \
  register char * _AddAppWindowA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((struct AppWindow * (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), struct Window * __asm("a0"), struct MsgPort * __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_AddAppWindowA__bn - 48))(_AddAppWindowA__bn, _AddAppWindowA_id, _AddAppWindowA_userdata, _AddAppWindowA_window, _AddAppWindowA_msgport, _AddAppWindowA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct AppWindow * ___AddAppWindow(struct Library * WorkbenchBase, ULONG id, ULONG userdata, struct Window * window, struct MsgPort * msgport, ...)
{
  return AddAppWindowA(id, userdata, window, msgport, (CONST struct TagItem *) ((ULONG) &msgport + sizeof(struct MsgPort *)));
}

#define AddAppWindow(id, userdata, window, msgport...) ___AddAppWindow(WB_BASE_NAME, id, userdata, window, msgport)
#endif

#define RemoveAppWindow(appWindow) ({ \
  struct AppWindow * _RemoveAppWindow_appWindow = (appWindow); \
  ({ \
  register char * _RemoveAppWindow__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct AppWindow * __asm("a0"))) \
  (_RemoveAppWindow__bn - 54))(_RemoveAppWindow__bn, _RemoveAppWindow_appWindow); \
});})

#define AddAppIconA(id, userdata, text, msgport, lock, diskobj, taglist) ({ \
  ULONG _AddAppIconA_id = (id); \
  ULONG _AddAppIconA_userdata = (userdata); \
  CONST_STRPTR _AddAppIconA_text = (text); \
  struct MsgPort * _AddAppIconA_msgport = (msgport); \
  BPTR _AddAppIconA_lock = (lock); \
  struct DiskObject * _AddAppIconA_diskobj = (diskobj); \
  CONST struct TagItem * _AddAppIconA_taglist = (taglist); \
  ({ \
  register char * _AddAppIconA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((struct AppIcon * (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), CONST_STRPTR __asm("a0"), struct MsgPort * __asm("a1"), BPTR __asm("a2"), struct DiskObject * __asm("a3"), CONST struct TagItem * __asm("a4"))) \
  (_AddAppIconA__bn - 60))(_AddAppIconA__bn, _AddAppIconA_id, _AddAppIconA_userdata, _AddAppIconA_text, _AddAppIconA_msgport, _AddAppIconA_lock, _AddAppIconA_diskobj, _AddAppIconA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct AppIcon * ___AddAppIcon(struct Library * WorkbenchBase, ULONG id, ULONG userdata, CONST_STRPTR text, struct MsgPort * msgport, BPTR lock, struct DiskObject * diskobj, ...)
{
  return AddAppIconA(id, userdata, text, msgport, lock, diskobj, (CONST struct TagItem *) ((ULONG) &diskobj + sizeof(struct DiskObject *)));
}

#define AddAppIcon(id, userdata, text, msgport, lock, diskobj...) ___AddAppIcon(WB_BASE_NAME, id, userdata, text, msgport, lock, diskobj)
#endif

#define RemoveAppIcon(appIcon) ({ \
  struct AppIcon * _RemoveAppIcon_appIcon = (appIcon); \
  ({ \
  register char * _RemoveAppIcon__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct AppIcon * __asm("a0"))) \
  (_RemoveAppIcon__bn - 66))(_RemoveAppIcon__bn, _RemoveAppIcon_appIcon); \
});})

#define AddAppMenuItemA(id, userdata, text, msgport, taglist) ({ \
  ULONG _AddAppMenuItemA_id = (id); \
  ULONG _AddAppMenuItemA_userdata = (userdata); \
  CONST_STRPTR _AddAppMenuItemA_text = (text); \
  struct MsgPort * _AddAppMenuItemA_msgport = (msgport); \
  CONST struct TagItem * _AddAppMenuItemA_taglist = (taglist); \
  ({ \
  register char * _AddAppMenuItemA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((struct AppMenuItem * (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), CONST_STRPTR __asm("a0"), struct MsgPort * __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_AddAppMenuItemA__bn - 72))(_AddAppMenuItemA__bn, _AddAppMenuItemA_id, _AddAppMenuItemA_userdata, _AddAppMenuItemA_text, _AddAppMenuItemA_msgport, _AddAppMenuItemA_taglist); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct AppMenuItem * ___AddAppMenuItem(struct Library * WorkbenchBase, ULONG id, ULONG userdata, CONST_STRPTR text, struct MsgPort * msgport, ...)
{
  return AddAppMenuItemA(id, userdata, text, msgport, (CONST struct TagItem *) ((ULONG) &msgport + sizeof(struct MsgPort *)));
}

#define AddAppMenuItem(id, userdata, text, msgport...) ___AddAppMenuItem(WB_BASE_NAME, id, userdata, text, msgport)
#endif

#define RemoveAppMenuItem(appMenuItem) ({ \
  struct AppMenuItem * _RemoveAppMenuItem_appMenuItem = (appMenuItem); \
  ({ \
  register char * _RemoveAppMenuItem__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct AppMenuItem * __asm("a0"))) \
  (_RemoveAppMenuItem__bn - 78))(_RemoveAppMenuItem__bn, _RemoveAppMenuItem_appMenuItem); \
});})

#define WBInfo(lock, name, screen) ({ \
  BPTR _WBInfo_lock = (lock); \
  CONST_STRPTR _WBInfo_name = (name); \
  struct Screen * _WBInfo_screen = (screen); \
  ({ \
  register char * _WBInfo__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), BPTR __asm("a0"), CONST_STRPTR __asm("a1"), struct Screen * __asm("a2"))) \
  (_WBInfo__bn - 90))(_WBInfo__bn, _WBInfo_lock, _WBInfo_name, _WBInfo_screen); \
});})

#define OpenWorkbenchObjectA(name, tags) ({ \
  CONST_STRPTR _OpenWorkbenchObjectA_name = (name); \
  CONST struct TagItem * _OpenWorkbenchObjectA_tags = (tags); \
  ({ \
  register char * _OpenWorkbenchObjectA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_OpenWorkbenchObjectA__bn - 96))(_OpenWorkbenchObjectA__bn, _OpenWorkbenchObjectA_name, _OpenWorkbenchObjectA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___OpenWorkbenchObject(struct Library * WorkbenchBase, CONST_STRPTR name, ...)
{
  return OpenWorkbenchObjectA(name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define OpenWorkbenchObject(name...) ___OpenWorkbenchObject(WB_BASE_NAME, name)
#endif

#define CloseWorkbenchObjectA(name, tags) ({ \
  CONST_STRPTR _CloseWorkbenchObjectA_name = (name); \
  CONST struct TagItem * _CloseWorkbenchObjectA_tags = (tags); \
  ({ \
  register char * _CloseWorkbenchObjectA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_CloseWorkbenchObjectA__bn - 102))(_CloseWorkbenchObjectA__bn, _CloseWorkbenchObjectA_name, _CloseWorkbenchObjectA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___CloseWorkbenchObject(struct Library * WorkbenchBase, CONST_STRPTR name, ...)
{
  return CloseWorkbenchObjectA(name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define CloseWorkbenchObject(name...) ___CloseWorkbenchObject(WB_BASE_NAME, name)
#endif

#define WorkbenchControlA(name, tags) ({ \
  CONST_STRPTR _WorkbenchControlA_name = (name); \
  CONST struct TagItem * _WorkbenchControlA_tags = (tags); \
  ({ \
  register char * _WorkbenchControlA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_WorkbenchControlA__bn - 108))(_WorkbenchControlA__bn, _WorkbenchControlA_name, _WorkbenchControlA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___WorkbenchControl(struct Library * WorkbenchBase, CONST_STRPTR name, ...)
{
  return WorkbenchControlA(name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define WorkbenchControl(name...) ___WorkbenchControl(WB_BASE_NAME, name)
#endif

#define AddAppWindowDropZoneA(aw, id, userdata, tags) ({ \
  struct AppWindow * _AddAppWindowDropZoneA_aw = (aw); \
  ULONG _AddAppWindowDropZoneA_id = (id); \
  ULONG _AddAppWindowDropZoneA_userdata = (userdata); \
  CONST struct TagItem * _AddAppWindowDropZoneA_tags = (tags); \
  ({ \
  register char * _AddAppWindowDropZoneA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((struct AppWindowDropZone * (*)(char * __asm("a6"), struct AppWindow * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"), CONST struct TagItem * __asm("a1"))) \
  (_AddAppWindowDropZoneA__bn - 114))(_AddAppWindowDropZoneA__bn, _AddAppWindowDropZoneA_aw, _AddAppWindowDropZoneA_id, _AddAppWindowDropZoneA_userdata, _AddAppWindowDropZoneA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct AppWindowDropZone * ___AddAppWindowDropZone(struct Library * WorkbenchBase, struct AppWindow * aw, ULONG id, ULONG userdata, ...)
{
  return AddAppWindowDropZoneA(aw, id, userdata, (CONST struct TagItem *) ((ULONG) &userdata + sizeof(ULONG)));
}

#define AddAppWindowDropZone(aw, id, userdata...) ___AddAppWindowDropZone(WB_BASE_NAME, aw, id, userdata)
#endif

#define RemoveAppWindowDropZone(aw, dropZone) ({ \
  struct AppWindow * _RemoveAppWindowDropZone_aw = (aw); \
  struct AppWindowDropZone * _RemoveAppWindowDropZone_dropZone = (dropZone); \
  ({ \
  register char * _RemoveAppWindowDropZone__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct AppWindow * __asm("a0"), struct AppWindowDropZone * __asm("a1"))) \
  (_RemoveAppWindowDropZone__bn - 120))(_RemoveAppWindowDropZone__bn, _RemoveAppWindowDropZone_aw, _RemoveAppWindowDropZone_dropZone); \
});})

#define ChangeWorkbenchSelectionA(name, hook, tags) ({ \
  CONST_STRPTR _ChangeWorkbenchSelectionA_name = (name); \
  struct Hook * _ChangeWorkbenchSelectionA_hook = (hook); \
  CONST struct TagItem * _ChangeWorkbenchSelectionA_tags = (tags); \
  ({ \
  register char * _ChangeWorkbenchSelectionA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), struct Hook * __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_ChangeWorkbenchSelectionA__bn - 126))(_ChangeWorkbenchSelectionA__bn, _ChangeWorkbenchSelectionA_name, _ChangeWorkbenchSelectionA_hook, _ChangeWorkbenchSelectionA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___ChangeWorkbenchSelection(struct Library * WorkbenchBase, CONST_STRPTR name, struct Hook * hook, ...)
{
  return ChangeWorkbenchSelectionA(name, hook, (CONST struct TagItem *) ((ULONG) &hook + sizeof(struct Hook *)));
}

#define ChangeWorkbenchSelection(name, hook...) ___ChangeWorkbenchSelection(WB_BASE_NAME, name, hook)
#endif

#define MakeWorkbenchObjectVisibleA(name, tags) ({ \
  CONST_STRPTR _MakeWorkbenchObjectVisibleA_name = (name); \
  CONST struct TagItem * _MakeWorkbenchObjectVisibleA_tags = (tags); \
  ({ \
  register char * _MakeWorkbenchObjectVisibleA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_MakeWorkbenchObjectVisibleA__bn - 132))(_MakeWorkbenchObjectVisibleA__bn, _MakeWorkbenchObjectVisibleA_name, _MakeWorkbenchObjectVisibleA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___MakeWorkbenchObjectVisible(struct Library * WorkbenchBase, CONST_STRPTR name, ...)
{
  return MakeWorkbenchObjectVisibleA(name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define MakeWorkbenchObjectVisible(name...) ___MakeWorkbenchObjectVisible(WB_BASE_NAME, name)
#endif

#define WhichWorkbenchObjectA(window, x, y, tags) ({ \
  struct Window * _WhichWorkbenchObjectA_window = (window); \
  LONG _WhichWorkbenchObjectA_x = (x); \
  LONG _WhichWorkbenchObjectA_y = (y); \
  CONST struct TagItem * _WhichWorkbenchObjectA_tags = (tags); \
  ({ \
  register char * _WhichWorkbenchObjectA__bn __asm("a6") = (char *) (WB_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Window * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"), CONST struct TagItem * __asm("a1"))) \
  (_WhichWorkbenchObjectA__bn - 138))(_WhichWorkbenchObjectA__bn, _WhichWorkbenchObjectA_window, _WhichWorkbenchObjectA_x, _WhichWorkbenchObjectA_y, _WhichWorkbenchObjectA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___WhichWorkbenchObject(struct Library * WorkbenchBase, struct Window * window, LONG x, LONG y, ...)
{
  return WhichWorkbenchObjectA(window, x, y, (CONST struct TagItem *) ((ULONG) &y + sizeof(LONG)));
}

#define WhichWorkbenchObject(window, x, y...) ___WhichWorkbenchObject(WB_BASE_NAME, window, x, y)
#endif

#endif /*  _INLINE_WB_H  */
