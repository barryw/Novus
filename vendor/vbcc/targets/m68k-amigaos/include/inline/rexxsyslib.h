#ifndef _INLINE_REXXSYSLIB_H
#define _INLINE_REXXSYSLIB_H

#ifndef CLIB_REXXSYSLIB_PROTOS_H
#define CLIB_REXXSYSLIB_PROTOS_H
#endif

#ifndef  REXX_RXSLIB_H
#include <rexx/rxslib.h>
#endif
#ifndef  REXX_REXXIO_H
#include <rexx/rexxio.h>
#endif
#ifndef  DOS_DOS_H
#include <dos/dos.h>
#endif

#ifndef REXXSYSLIB_BASE_NAME
#define REXXSYSLIB_BASE_NAME RexxSysBase
#endif

#define CreateArgstring(string, length) ({ \
  CONST_STRPTR _CreateArgstring_string = (string); \
  ULONG _CreateArgstring_length = (length); \
  ({ \
  register char * _CreateArgstring__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((UBYTE * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), ULONG __asm("d0"))) \
  (_CreateArgstring__bn - 126))(_CreateArgstring__bn, _CreateArgstring_string, _CreateArgstring_length); \
});})

#define DeleteArgstring(argstring) ({ \
  UBYTE * _DeleteArgstring_argstring = (argstring); \
  ({ \
  register char * _DeleteArgstring__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), UBYTE * __asm("a0"))) \
  (_DeleteArgstring__bn - 132))(_DeleteArgstring__bn, _DeleteArgstring_argstring); \
});})

#define LengthArgstring(argstring) ({ \
  CONST UBYTE * _LengthArgstring_argstring = (argstring); \
  ({ \
  register char * _LengthArgstring__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST UBYTE * __asm("a0"))) \
  (_LengthArgstring__bn - 138))(_LengthArgstring__bn, _LengthArgstring_argstring); \
});})

#define CreateRexxMsg(port, extension, host) ({ \
  struct MsgPort * _CreateRexxMsg_port = (port); \
  CONST_STRPTR _CreateRexxMsg_extension = (extension); \
  CONST_STRPTR _CreateRexxMsg_host = (host); \
  ({ \
  register char * _CreateRexxMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((struct RexxMsg * (*)(char * __asm("a6"), struct MsgPort * __asm("a0"), CONST_STRPTR __asm("a1"), CONST_STRPTR __asm("d0"))) \
  (_CreateRexxMsg__bn - 144))(_CreateRexxMsg__bn, _CreateRexxMsg_port, _CreateRexxMsg_extension, _CreateRexxMsg_host); \
});})

#define DeleteRexxMsg(packet) ({ \
  struct RexxMsg * _DeleteRexxMsg_packet = (packet); \
  ({ \
  register char * _DeleteRexxMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RexxMsg * __asm("a0"))) \
  (_DeleteRexxMsg__bn - 150))(_DeleteRexxMsg__bn, _DeleteRexxMsg_packet); \
});})

#define ClearRexxMsg(msgptr, count) ({ \
  struct RexxMsg * _ClearRexxMsg_msgptr = (msgptr); \
  ULONG _ClearRexxMsg_count = (count); \
  ({ \
  register char * _ClearRexxMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RexxMsg * __asm("a0"), ULONG __asm("d0"))) \
  (_ClearRexxMsg__bn - 156))(_ClearRexxMsg__bn, _ClearRexxMsg_msgptr, _ClearRexxMsg_count); \
});})

#define FillRexxMsg(msgptr, count, mask) ({ \
  struct RexxMsg * _FillRexxMsg_msgptr = (msgptr); \
  ULONG _FillRexxMsg_count = (count); \
  ULONG _FillRexxMsg_mask = (mask); \
  ({ \
  register char * _FillRexxMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct RexxMsg * __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_FillRexxMsg__bn - 162))(_FillRexxMsg__bn, _FillRexxMsg_msgptr, _FillRexxMsg_count, _FillRexxMsg_mask); \
});})

#define IsRexxMsg(msgptr) ({ \
  CONST struct RexxMsg * _IsRexxMsg_msgptr = (msgptr); \
  ({ \
  register char * _IsRexxMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct RexxMsg * __asm("a0"))) \
  (_IsRexxMsg__bn - 168))(_IsRexxMsg__bn, _IsRexxMsg_msgptr); \
});})

#define LockRexxBase(resource) ({ \
  ULONG _LockRexxBase_resource = (resource); \
  ({ \
  register char * _LockRexxBase__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_LockRexxBase__bn - 450))(_LockRexxBase__bn, _LockRexxBase_resource); \
});})

#define UnlockRexxBase(resource) ({ \
  ULONG _UnlockRexxBase_resource = (resource); \
  ({ \
  register char * _UnlockRexxBase__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_UnlockRexxBase__bn - 456))(_UnlockRexxBase__bn, _UnlockRexxBase_resource); \
});})

#define CreateRexxHostPort(basename) ({ \
  CONST_STRPTR _CreateRexxHostPort_basename = (basename); \
  ({ \
  register char * _CreateRexxHostPort__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_CreateRexxHostPort__bn - 480))(_CreateRexxHostPort__bn, _CreateRexxHostPort_basename); \
});})

#define DeleteRexxHostPort(port) ({ \
  struct MsgPort * _DeleteRexxHostPort_port = (port); \
  ({ \
  register char * _DeleteRexxHostPort__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MsgPort * __asm("a0"))) \
  (_DeleteRexxHostPort__bn - 486))(_DeleteRexxHostPort__bn, _DeleteRexxHostPort_port); \
});})

#define GetRexxVarFromMsg(var, msgptr, value) ({ \
  CONST_STRPTR _GetRexxVarFromMsg_var = (var); \
  CONST struct RexxMsg * _GetRexxVarFromMsg_msgptr = (msgptr); \
  STRPTR _GetRexxVarFromMsg_value = (value); \
  ({ \
  register char * _GetRexxVarFromMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct RexxMsg * __asm("a2"), STRPTR __asm("a1"))) \
  (_GetRexxVarFromMsg__bn - 492))(_GetRexxVarFromMsg__bn, _GetRexxVarFromMsg_var, _GetRexxVarFromMsg_msgptr, _GetRexxVarFromMsg_value); \
});})

#define SetRexxVarFromMsg(var, msgptr, value) ({ \
  CONST_STRPTR _SetRexxVarFromMsg_var = (var); \
  struct RexxMsg * _SetRexxVarFromMsg_msgptr = (msgptr); \
  CONST_STRPTR _SetRexxVarFromMsg_value = (value); \
  ({ \
  register char * _SetRexxVarFromMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), struct RexxMsg * __asm("a2"), CONST_STRPTR __asm("a1"))) \
  (_SetRexxVarFromMsg__bn - 498))(_SetRexxVarFromMsg__bn, _SetRexxVarFromMsg_var, _SetRexxVarFromMsg_msgptr, _SetRexxVarFromMsg_value); \
});})

#define LaunchRexxScript(script, replyport, extension, input, output) ({ \
  CONST_STRPTR _LaunchRexxScript_script = (script); \
  struct MsgPort * _LaunchRexxScript_replyport = (replyport); \
  CONST_STRPTR _LaunchRexxScript_extension = (extension); \
  BPTR _LaunchRexxScript_input = (input); \
  BPTR _LaunchRexxScript_output = (output); \
  ({ \
  register char * _LaunchRexxScript__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((struct RexxMsg * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), struct MsgPort * __asm("a1"), CONST_STRPTR __asm("a2"), BPTR __asm("d1"), BPTR __asm("d2"))) \
  (_LaunchRexxScript__bn - 504))(_LaunchRexxScript__bn, _LaunchRexxScript_script, _LaunchRexxScript_replyport, _LaunchRexxScript_extension, _LaunchRexxScript_input, _LaunchRexxScript_output); \
});})

#define FreeRexxMsg(msgptr) ({ \
  struct RexxMsg * _FreeRexxMsg_msgptr = (msgptr); \
  ({ \
  register char * _FreeRexxMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RexxMsg * __asm("a0"))) \
  (_FreeRexxMsg__bn - 510))(_FreeRexxMsg__bn, _FreeRexxMsg_msgptr); \
});})

#define GetRexxBufferFromMsg(var, msgptr, buffer, buffer_size) ({ \
  CONST_STRPTR _GetRexxBufferFromMsg_var = (var); \
  CONST struct RexxMsg * _GetRexxBufferFromMsg_msgptr = (msgptr); \
  STRPTR _GetRexxBufferFromMsg_buffer = (buffer); \
  ULONG _GetRexxBufferFromMsg_buffer_size = (buffer_size); \
  ({ \
  register char * _GetRexxBufferFromMsg__bn __asm("a6") = (char *) (REXXSYSLIB_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), CONST struct RexxMsg * __asm("a2"), STRPTR __asm("a1"), ULONG __asm("d0"))) \
  (_GetRexxBufferFromMsg__bn - 516))(_GetRexxBufferFromMsg__bn, _GetRexxBufferFromMsg_var, _GetRexxBufferFromMsg_msgptr, _GetRexxBufferFromMsg_buffer, _GetRexxBufferFromMsg_buffer_size); \
});})

#endif /*  _INLINE_REXXSYSLIB_H  */
