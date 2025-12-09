#ifndef _INLINE_ASL_H
#define _INLINE_ASL_H

#ifndef CLIB_ASL_PROTOS_H
#define CLIB_ASL_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  LIBRARIES_ASL_H
#include <libraries/asl.h>
#endif

#ifndef ASL_BASE_NAME
#define ASL_BASE_NAME AslBase
#endif

#define AllocFileRequest() ({ \
  register char * _AllocFileRequest__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((struct FileRequester * (*)(char * __asm("a6"))) \
  (_AllocFileRequest__bn - 30))(_AllocFileRequest__bn); \
})

#define FreeFileRequest(fileReq) ({ \
  struct FileRequester * _FreeFileRequest_fileReq = (fileReq); \
  ({ \
  register char * _FreeFileRequest__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct FileRequester * __asm("a0"))) \
  (_FreeFileRequest__bn - 36))(_FreeFileRequest__bn, _FreeFileRequest_fileReq); \
});})

#define RequestFile(fileReq) ({ \
  struct FileRequester * _RequestFile_fileReq = (fileReq); \
  ({ \
  register char * _RequestFile__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct FileRequester * __asm("a0"))) \
  (_RequestFile__bn - 42))(_RequestFile__bn, _RequestFile_fileReq); \
});})

#define AllocAslRequest(reqType, tagList) ({ \
  ULONG _AllocAslRequest_reqType = (reqType); \
  CONST struct TagItem * _AllocAslRequest_tagList = (tagList); \
  ({ \
  register char * _AllocAslRequest__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_AllocAslRequest__bn - 48))(_AllocAslRequest__bn, _AllocAslRequest_reqType, _AllocAslRequest_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___AllocAslRequestTags(struct Library * AslBase, ULONG reqType, ...)
{
  return AllocAslRequest(reqType, (CONST struct TagItem *) ((ULONG) &reqType + sizeof(ULONG)));
}

#define AllocAslRequestTags(reqType...) ___AllocAslRequestTags(ASL_BASE_NAME, reqType)
#endif

#define FreeAslRequest(requester) ({ \
  APTR _FreeAslRequest_requester = (requester); \
  ({ \
  register char * _FreeAslRequest__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_FreeAslRequest__bn - 54))(_FreeAslRequest__bn, _FreeAslRequest_requester); \
});})

#define AslRequest(requester, tagList) ({ \
  APTR _AslRequest_requester = (requester); \
  CONST struct TagItem * _AslRequest_tagList = (tagList); \
  ({ \
  register char * _AslRequest__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), APTR __asm("a0"), CONST struct TagItem * __asm("a1"))) \
  (_AslRequest__bn - 60))(_AslRequest__bn, _AslRequest_requester, _AslRequest_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___AslRequestTags(struct Library * AslBase, APTR requester, ...)
{
  return AslRequest(requester, (CONST struct TagItem *) ((ULONG) &requester + sizeof(APTR)));
}

#define AslRequestTags(requester...) ___AslRequestTags(ASL_BASE_NAME, requester)
#endif

#define AbortAslRequest(requester) ({ \
  APTR _AbortAslRequest_requester = (requester); \
  ({ \
  register char * _AbortAslRequest__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_AbortAslRequest__bn - 78))(_AbortAslRequest__bn, _AbortAslRequest_requester); \
});})

#define ActivateAslRequest(requester) ({ \
  APTR _ActivateAslRequest_requester = (requester); \
  ({ \
  register char * _ActivateAslRequest__bn __asm("a6") = (char *) (ASL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_ActivateAslRequest__bn - 84))(_ActivateAslRequest__bn, _ActivateAslRequest_requester); \
});})

#endif /*  _INLINE_ASL_H  */
