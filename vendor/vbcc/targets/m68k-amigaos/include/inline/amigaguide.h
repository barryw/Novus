#ifndef _INLINE_AMIGAGUIDE_H
#define _INLINE_AMIGAGUIDE_H

#ifndef CLIB_AMIGAGUIDE_PROTOS_H
#define CLIB_AMIGAGUIDE_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  DOS_DOS_H
#include <dos/dos.h>
#endif
#ifndef  LIBRARIES_AMIGAGUIDE_H
#include <libraries/amigaguide.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  UTILITY_HOOKS_H
#include <utility/hooks.h>
#endif
#ifndef  REXX_STORAGE_H
#include <rexx/storage.h>
#endif

#ifndef AMIGAGUIDE_BASE_NAME
#define AMIGAGUIDE_BASE_NAME AmigaGuideBase
#endif

#define LockAmigaGuideBase(handle) ({ \
  APTR _LockAmigaGuideBase_handle = (handle); \
  ({ \
  register char * _LockAmigaGuideBase__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_LockAmigaGuideBase__bn - 36))(_LockAmigaGuideBase__bn, _LockAmigaGuideBase_handle); \
});})

#define UnlockAmigaGuideBase(key) ({ \
  LONG _UnlockAmigaGuideBase_key = (key); \
  ({ \
  register char * _UnlockAmigaGuideBase__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_UnlockAmigaGuideBase__bn - 42))(_UnlockAmigaGuideBase__bn, _UnlockAmigaGuideBase_key); \
});})

#define OpenAmigaGuideA(nag, attrs) ({ \
  struct NewAmigaGuide * _OpenAmigaGuideA_nag = (nag); \
  CONST struct TagItem * _OpenAmigaGuideA_attrs = (attrs); \
  ({ \
  register char * _OpenAmigaGuideA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct NewAmigaGuide * __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_OpenAmigaGuideA__bn - 54))(_OpenAmigaGuideA__bn, _OpenAmigaGuideA_nag, _OpenAmigaGuideA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___OpenAmigaGuide(struct Library * AmigaGuideBase, struct NewAmigaGuide * nag, ...)
{
  return OpenAmigaGuideA(nag, (CONST struct TagItem *) ((ULONG) &nag + sizeof(struct NewAmigaGuide *)));
}

#define OpenAmigaGuide(nag...) ___OpenAmigaGuide(AMIGAGUIDE_BASE_NAME, nag)
#endif

#define OpenAmigaGuideAsyncA(nag, attrs) ({ \
  struct NewAmigaGuide * _OpenAmigaGuideAsyncA_nag = (nag); \
  CONST struct TagItem * _OpenAmigaGuideAsyncA_attrs = (attrs); \
  ({ \
  register char * _OpenAmigaGuideAsyncA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct NewAmigaGuide * __asm("a0"), CONST struct TagItem * __asm("d0"))) \
  (_OpenAmigaGuideAsyncA__bn - 60))(_OpenAmigaGuideAsyncA__bn, _OpenAmigaGuideAsyncA_nag, _OpenAmigaGuideAsyncA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___OpenAmigaGuideAsync(struct Library * AmigaGuideBase, struct NewAmigaGuide * nag, ...)
{
  return OpenAmigaGuideAsyncA(nag, (CONST struct TagItem *) ((ULONG) &nag + sizeof(struct NewAmigaGuide *)));
}

#define OpenAmigaGuideAsync(nag...) ___OpenAmigaGuideAsync(AMIGAGUIDE_BASE_NAME, nag)
#endif

#define CloseAmigaGuide(cl) ({ \
  APTR _CloseAmigaGuide_cl = (cl); \
  ({ \
  register char * _CloseAmigaGuide__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_CloseAmigaGuide__bn - 66))(_CloseAmigaGuide__bn, _CloseAmigaGuide_cl); \
});})

#define AmigaGuideSignal(cl) ({ \
  APTR _AmigaGuideSignal_cl = (cl); \
  ({ \
  register char * _AmigaGuideSignal__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_AmigaGuideSignal__bn - 72))(_AmigaGuideSignal__bn, _AmigaGuideSignal_cl); \
});})

#define GetAmigaGuideMsg(cl) ({ \
  APTR _GetAmigaGuideMsg_cl = (cl); \
  ({ \
  register char * _GetAmigaGuideMsg__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((struct AmigaGuideMsg * (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_GetAmigaGuideMsg__bn - 78))(_GetAmigaGuideMsg__bn, _GetAmigaGuideMsg_cl); \
});})

#define ReplyAmigaGuideMsg(amsg) ({ \
  struct AmigaGuideMsg * _ReplyAmigaGuideMsg_amsg = (amsg); \
  ({ \
  register char * _ReplyAmigaGuideMsg__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct AmigaGuideMsg * __asm("a0"))) \
  (_ReplyAmigaGuideMsg__bn - 84))(_ReplyAmigaGuideMsg__bn, _ReplyAmigaGuideMsg_amsg); \
});})

#define SetAmigaGuideContextA(cl, id, attrs) ({ \
  APTR _SetAmigaGuideContextA_cl = (cl); \
  ULONG _SetAmigaGuideContextA_id = (id); \
  CONST struct TagItem * _SetAmigaGuideContextA_attrs = (attrs); \
  ({ \
  register char * _SetAmigaGuideContextA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"), ULONG __asm("d0"), CONST struct TagItem * __asm("d1"))) \
  (_SetAmigaGuideContextA__bn - 90))(_SetAmigaGuideContextA__bn, _SetAmigaGuideContextA_cl, _SetAmigaGuideContextA_id, _SetAmigaGuideContextA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___SetAmigaGuideContext(struct Library * AmigaGuideBase, APTR cl, ULONG id, ...)
{
  return SetAmigaGuideContextA(cl, id, (CONST struct TagItem *) ((ULONG) &id + sizeof(ULONG)));
}

#define SetAmigaGuideContext(cl, id...) ___SetAmigaGuideContext(AMIGAGUIDE_BASE_NAME, cl, id)
#endif

#define SendAmigaGuideContextA(cl, attrs) ({ \
  APTR _SendAmigaGuideContextA_cl = (cl); \
  CONST struct TagItem * _SendAmigaGuideContextA_attrs = (attrs); \
  ({ \
  register char * _SendAmigaGuideContextA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"), CONST struct TagItem * __asm("d0"))) \
  (_SendAmigaGuideContextA__bn - 96))(_SendAmigaGuideContextA__bn, _SendAmigaGuideContextA_cl, _SendAmigaGuideContextA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___SendAmigaGuideContext(struct Library * AmigaGuideBase, APTR cl, ...)
{
  return SendAmigaGuideContextA(cl, (CONST struct TagItem *) ((ULONG) &cl + sizeof(APTR)));
}

#define SendAmigaGuideContext(cl...) ___SendAmigaGuideContext(AMIGAGUIDE_BASE_NAME, cl)
#endif

#define SendAmigaGuideCmdA(cl, cmd, attrs) ({ \
  APTR _SendAmigaGuideCmdA_cl = (cl); \
  STRPTR _SendAmigaGuideCmdA_cmd = (cmd); \
  CONST struct TagItem * _SendAmigaGuideCmdA_attrs = (attrs); \
  ({ \
  register char * _SendAmigaGuideCmdA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"), STRPTR __asm("d0"), CONST struct TagItem * __asm("d1"))) \
  (_SendAmigaGuideCmdA__bn - 102))(_SendAmigaGuideCmdA__bn, _SendAmigaGuideCmdA_cl, _SendAmigaGuideCmdA_cmd, _SendAmigaGuideCmdA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___SendAmigaGuideCmd(struct Library * AmigaGuideBase, APTR cl, STRPTR cmd, ...)
{
  return SendAmigaGuideCmdA(cl, cmd, (CONST struct TagItem *) ((ULONG) &cmd + sizeof(STRPTR)));
}

#define SendAmigaGuideCmd(cl, cmd...) ___SendAmigaGuideCmd(AMIGAGUIDE_BASE_NAME, cl, cmd)
#endif

#define SetAmigaGuideAttrsA(cl, attrs) ({ \
  APTR _SetAmigaGuideAttrsA_cl = (cl); \
  CONST struct TagItem * _SetAmigaGuideAttrsA_attrs = (attrs); \
  ({ \
  register char * _SetAmigaGuideAttrsA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_SetAmigaGuideAttrsA__bn - 108))(_SetAmigaGuideAttrsA__bn, _SetAmigaGuideAttrsA_cl, _SetAmigaGuideAttrsA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___SetAmigaGuideAttrs(struct Library * AmigaGuideBase, APTR cl, ...)
{
  return SetAmigaGuideAttrsA(cl, (CONST struct TagItem *) ((ULONG) &cl + sizeof(APTR)));
}

#define SetAmigaGuideAttrs(cl...) ___SetAmigaGuideAttrs(AMIGAGUIDE_BASE_NAME, cl)
#endif

#define GetAmigaGuideAttr(tag1, cl, storage) ({ \
  Tag _GetAmigaGuideAttr_tag1 = (tag1); \
  APTR _GetAmigaGuideAttr_cl = (cl); \
  ULONG * _GetAmigaGuideAttr_storage = (storage); \
  ({ \
  register char * _GetAmigaGuideAttr__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), Tag __asm("d0"), APTR __asm("a0"), ULONG * __asm("a1"))) \
  (_GetAmigaGuideAttr__bn - 114))(_GetAmigaGuideAttr__bn, _GetAmigaGuideAttr_tag1, _GetAmigaGuideAttr_cl, _GetAmigaGuideAttr_storage); \
});})

#define LoadXRef(lock, name) ({ \
  BPTR _LoadXRef_lock = (lock); \
  STRPTR _LoadXRef_name = (name); \
  ({ \
  register char * _LoadXRef__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("a0"), STRPTR __asm("a1"))) \
  (_LoadXRef__bn - 126))(_LoadXRef__bn, _LoadXRef_lock, _LoadXRef_name); \
});})

#define ExpungeXRef() ({ \
  register char * _ExpungeXRef__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_ExpungeXRef__bn - 132))(_ExpungeXRef__bn); \
})

#define AddAmigaGuideHostA(h, name, attrs) ({ \
  struct Hook * _AddAmigaGuideHostA_h = (h); \
  CONST_STRPTR _AddAmigaGuideHostA_name = (name); \
  CONST struct TagItem * _AddAmigaGuideHostA_attrs = (attrs); \
  ({ \
  register char * _AddAmigaGuideHostA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct Hook * __asm("a0"), CONST_STRPTR __asm("d0"), CONST struct TagItem * __asm("a1"))) \
  (_AddAmigaGuideHostA__bn - 138))(_AddAmigaGuideHostA__bn, _AddAmigaGuideHostA_h, _AddAmigaGuideHostA_name, _AddAmigaGuideHostA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___AddAmigaGuideHost(struct Library * AmigaGuideBase, struct Hook * h, CONST_STRPTR name, ...)
{
  return AddAmigaGuideHostA(h, name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define AddAmigaGuideHost(h, name...) ___AddAmigaGuideHost(AMIGAGUIDE_BASE_NAME, h, name)
#endif

#define RemoveAmigaGuideHostA(hh, attrs) ({ \
  APTR _RemoveAmigaGuideHostA_hh = (hh); \
  CONST struct TagItem * _RemoveAmigaGuideHostA_attrs = (attrs); \
  ({ \
  register char * _RemoveAmigaGuideHostA__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), APTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_RemoveAmigaGuideHostA__bn - 144))(_RemoveAmigaGuideHostA__bn, _RemoveAmigaGuideHostA_hh, _RemoveAmigaGuideHostA_attrs); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___RemoveAmigaGuideHost(struct Library * AmigaGuideBase, APTR hh, ...)
{
  return RemoveAmigaGuideHostA(hh, (CONST struct TagItem *) ((ULONG) &hh + sizeof(APTR)));
}

#define RemoveAmigaGuideHost(hh...) ___RemoveAmigaGuideHost(AMIGAGUIDE_BASE_NAME, hh)
#endif

#define GetAmigaGuideString(id) ({ \
  LONG _GetAmigaGuideString_id = (id); \
  ({ \
  register char * _GetAmigaGuideString__bn __asm("a6") = (char *) (AMIGAGUIDE_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_GetAmigaGuideString__bn - 210))(_GetAmigaGuideString__bn, _GetAmigaGuideString_id); \
});})

#endif /*  _INLINE_AMIGAGUIDE_H  */
