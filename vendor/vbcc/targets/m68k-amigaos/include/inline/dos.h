#ifndef _INLINE_DOS_H
#define _INLINE_DOS_H

#ifndef CLIB_DOS_PROTOS_H
#define CLIB_DOS_PROTOS_H
#endif

#ifndef  DOS_DOSEXTENS_H
#include <dos/dosextens.h>
#endif
#ifndef  DOS_RECORD_H
#include <dos/record.h>
#endif
#ifndef  DOS_RDARGS_H
#include <dos/rdargs.h>
#endif
#ifndef  DOS_DOSASL_H
#include <dos/dosasl.h>
#endif
#ifndef  DOS_VAR_H
#include <dos/var.h>
#endif
#ifndef  DOS_NOTIFY_H
#include <dos/notify.h>
#endif
#ifndef  DOS_DATETIME_H
#include <dos/datetime.h>
#endif
#ifndef  DOS_EXALL_H
#include <dos/exall.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif

#ifndef DOS_BASE_NAME
#define DOS_BASE_NAME DOSBase
#endif

#define Open(name, accessMode) ({ \
  CONST_STRPTR _Open_name = (name); \
  LONG _Open_accessMode = (accessMode); \
  ({ \
  register char * _Open__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_Open__bn - 30))(_Open__bn, _Open_name, _Open_accessMode); \
});})

#define Close(file) ({ \
  BPTR _Close_file = (file); \
  ({ \
  register char * _Close__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_Close__bn - 36))(_Close__bn, _Close_file); \
});})

#define Read(file, buffer, length) ({ \
  BPTR _Read_file = (file); \
  APTR _Read_buffer = (buffer); \
  LONG _Read_length = (length); \
  ({ \
  register char * _Read__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), APTR __asm("d2"), LONG __asm("d3"))) \
  (_Read__bn - 42))(_Read__bn, _Read_file, _Read_buffer, _Read_length); \
});})

#define Write(file, buffer, length) ({ \
  BPTR _Write_file = (file); \
  CONST_APTR _Write_buffer = (buffer); \
  LONG _Write_length = (length); \
  ({ \
  register char * _Write__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), CONST_APTR __asm("d2"), LONG __asm("d3"))) \
  (_Write__bn - 48))(_Write__bn, _Write_file, _Write_buffer, _Write_length); \
});})

#define Input() ({ \
  register char * _Input__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"))) \
  (_Input__bn - 54))(_Input__bn); \
})

#define Output() ({ \
  register char * _Output__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"))) \
  (_Output__bn - 60))(_Output__bn); \
})

#define Seek(file, position, offset) ({ \
  BPTR _Seek_file = (file); \
  LONG _Seek_position = (position); \
  LONG _Seek_offset = (offset); \
  ({ \
  register char * _Seek__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_Seek__bn - 66))(_Seek__bn, _Seek_file, _Seek_position, _Seek_offset); \
});})

#define DeleteFile(name) ({ \
  CONST_STRPTR _DeleteFile_name = (name); \
  ({ \
  register char * _DeleteFile__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_DeleteFile__bn - 72))(_DeleteFile__bn, _DeleteFile_name); \
});})

#define Rename(oldName, newName) ({ \
  CONST_STRPTR _Rename_oldName = (oldName); \
  CONST_STRPTR _Rename_newName = (newName); \
  ({ \
  register char * _Rename__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_Rename__bn - 78))(_Rename__bn, _Rename_oldName, _Rename_newName); \
});})

#define Lock(name, type) ({ \
  CONST_STRPTR _Lock_name = (name); \
  LONG _Lock_type = (type); \
  ({ \
  register char * _Lock__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_Lock__bn - 84))(_Lock__bn, _Lock_name, _Lock_type); \
});})

#define UnLock(lock) ({ \
  BPTR _UnLock_lock = (lock); \
  ({ \
  register char * _UnLock__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_UnLock__bn - 90))(_UnLock__bn, _UnLock_lock); \
});})

#define DupLock(lock) ({ \
  BPTR _DupLock_lock = (lock); \
  ({ \
  register char * _DupLock__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_DupLock__bn - 96))(_DupLock__bn, _DupLock_lock); \
});})

#define Examine(lock, fileInfoBlock) ({ \
  BPTR _Examine_lock = (lock); \
  struct FileInfoBlock * _Examine_fileInfoBlock = (fileInfoBlock); \
  ({ \
  register char * _Examine__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), struct FileInfoBlock * __asm("d2"))) \
  (_Examine__bn - 102))(_Examine__bn, _Examine_lock, _Examine_fileInfoBlock); \
});})

#define ExNext(lock, fileInfoBlock) ({ \
  BPTR _ExNext_lock = (lock); \
  struct FileInfoBlock * _ExNext_fileInfoBlock = (fileInfoBlock); \
  ({ \
  register char * _ExNext__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), struct FileInfoBlock * __asm("d2"))) \
  (_ExNext__bn - 108))(_ExNext__bn, _ExNext_lock, _ExNext_fileInfoBlock); \
});})

#define Info(lock, parameterBlock) ({ \
  BPTR _Info_lock = (lock); \
  struct InfoData * _Info_parameterBlock = (parameterBlock); \
  ({ \
  register char * _Info__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), struct InfoData * __asm("d2"))) \
  (_Info__bn - 114))(_Info__bn, _Info_lock, _Info_parameterBlock); \
});})

#define CreateDir(name) ({ \
  CONST_STRPTR _CreateDir_name = (name); \
  ({ \
  register char * _CreateDir__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_CreateDir__bn - 120))(_CreateDir__bn, _CreateDir_name); \
});})

#define CurrentDir(lock) ({ \
  BPTR _CurrentDir_lock = (lock); \
  ({ \
  register char * _CurrentDir__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_CurrentDir__bn - 126))(_CurrentDir__bn, _CurrentDir_lock); \
});})

#define IoErr() ({ \
  register char * _IoErr__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"))) \
  (_IoErr__bn - 132))(_IoErr__bn); \
})

#define CreateProc(name, pri, segList, stackSize) ({ \
  CONST_STRPTR _CreateProc_name = (name); \
  LONG _CreateProc_pri = (pri); \
  BPTR _CreateProc_segList = (segList); \
  LONG _CreateProc_stackSize = (stackSize); \
  ({ \
  register char * _CreateProc__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"), BPTR __asm("d3"), LONG __asm("d4"))) \
  (_CreateProc__bn - 138))(_CreateProc__bn, _CreateProc_name, _CreateProc_pri, _CreateProc_segList, _CreateProc_stackSize); \
});})

#define Exit(returnCode) ({ \
  LONG _Exit_returnCode = (returnCode); \
  ({ \
  register char * _Exit__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d1"))) \
  (_Exit__bn - 144))(_Exit__bn, _Exit_returnCode); \
});})

#define LoadSeg(name) ({ \
  CONST_STRPTR _LoadSeg_name = (name); \
  ({ \
  register char * _LoadSeg__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_LoadSeg__bn - 150))(_LoadSeg__bn, _LoadSeg_name); \
});})

#define UnLoadSeg(seglist) ({ \
  BPTR _UnLoadSeg_seglist = (seglist); \
  ({ \
  register char * _UnLoadSeg__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_UnLoadSeg__bn - 156))(_UnLoadSeg__bn, _UnLoadSeg_seglist); \
});})

#define DeviceProc(name) ({ \
  CONST_STRPTR _DeviceProc_name = (name); \
  ({ \
  register char * _DeviceProc__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_DeviceProc__bn - 174))(_DeviceProc__bn, _DeviceProc_name); \
});})

#define SetComment(name, comment) ({ \
  CONST_STRPTR _SetComment_name = (name); \
  CONST_STRPTR _SetComment_comment = (comment); \
  ({ \
  register char * _SetComment__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_SetComment__bn - 180))(_SetComment__bn, _SetComment_name, _SetComment_comment); \
});})

#define SetProtection(name, protect) ({ \
  CONST_STRPTR _SetProtection_name = (name); \
  LONG _SetProtection_protect = (protect); \
  ({ \
  register char * _SetProtection__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_SetProtection__bn - 186))(_SetProtection__bn, _SetProtection_name, _SetProtection_protect); \
});})

#define DateStamp(date) ({ \
  struct DateStamp * _DateStamp_date = (date); \
  ({ \
  register char * _DateStamp__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DateStamp * (*)(char * __asm("a6"), struct DateStamp * __asm("d1"))) \
  (_DateStamp__bn - 192))(_DateStamp__bn, _DateStamp_date); \
});})

#define Delay(timeout) ({ \
  LONG _Delay_timeout = (timeout); \
  ({ \
  register char * _Delay__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d1"))) \
  (_Delay__bn - 198))(_Delay__bn, _Delay_timeout); \
});})

#define WaitForChar(file, timeout) ({ \
  BPTR _WaitForChar_file = (file); \
  LONG _WaitForChar_timeout = (timeout); \
  ({ \
  register char * _WaitForChar__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"))) \
  (_WaitForChar__bn - 204))(_WaitForChar__bn, _WaitForChar_file, _WaitForChar_timeout); \
});})

#define ParentDir(lock) ({ \
  BPTR _ParentDir_lock = (lock); \
  ({ \
  register char * _ParentDir__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_ParentDir__bn - 210))(_ParentDir__bn, _ParentDir_lock); \
});})

#define IsInteractive(file) ({ \
  BPTR _IsInteractive_file = (file); \
  ({ \
  register char * _IsInteractive__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_IsInteractive__bn - 216))(_IsInteractive__bn, _IsInteractive_file); \
});})

#define Execute(string, file, file2) ({ \
  CONST_STRPTR _Execute_string = (string); \
  BPTR _Execute_file = (file); \
  BPTR _Execute_file2 = (file2); \
  ({ \
  register char * _Execute__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), BPTR __asm("d2"), BPTR __asm("d3"))) \
  (_Execute__bn - 222))(_Execute__bn, _Execute_string, _Execute_file, _Execute_file2); \
});})

#define AllocDosObject(type, tags) ({ \
  ULONG _AllocDosObject_type = (type); \
  CONST struct TagItem * _AllocDosObject_tags = (tags); \
  ({ \
  register char * _AllocDosObject__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d1"), CONST struct TagItem * __asm("d2"))) \
  (_AllocDosObject__bn - 228))(_AllocDosObject__bn, _AllocDosObject_type, _AllocDosObject_tags); \
});})

#define AllocDosObjectTagList(type, tags) AllocDosObject((type), (tags))

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___AllocDosObjectTags(struct DosLibrary * DOSBase, ULONG type, ULONG tags, ...)
{
  return AllocDosObject(type, (CONST struct TagItem *) &tags);
}

#define AllocDosObjectTags(type...) ___AllocDosObjectTags(DOS_BASE_NAME, type)
#endif

#define FreeDosObject(type, ptr) ({ \
  ULONG _FreeDosObject_type = (type); \
  APTR _FreeDosObject_ptr = (ptr); \
  ({ \
  register char * _FreeDosObject__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d1"), APTR __asm("d2"))) \
  (_FreeDosObject__bn - 234))(_FreeDosObject__bn, _FreeDosObject_type, _FreeDosObject_ptr); \
});})

#define DoPkt(port, action, arg1, arg2, arg3, arg4, arg5) ({ \
  struct MsgPort * _DoPkt_port = (port); \
  LONG _DoPkt_action = (action); \
  LONG _DoPkt_arg1 = (arg1); \
  LONG _DoPkt_arg2 = (arg2); \
  LONG _DoPkt_arg3 = (arg3); \
  LONG _DoPkt_arg4 = (arg4); \
  LONG _DoPkt_arg5 = (arg5); \
  ({ \
  register char * _DoPkt__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"), LONG __asm("d6"), LONG __asm("d7"))) \
  (_DoPkt__bn - 240))(_DoPkt__bn, _DoPkt_port, _DoPkt_action, _DoPkt_arg1, _DoPkt_arg2, _DoPkt_arg3, _DoPkt_arg4, _DoPkt_arg5); \
});})

#define DoPkt0(port, action) ({ \
  struct MsgPort * _DoPkt0_port = (port); \
  LONG _DoPkt0_action = (action); \
  ({ \
  register char * _DoPkt0__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), LONG __asm("d2"))) \
  (_DoPkt0__bn - 240))(_DoPkt0__bn, _DoPkt0_port, _DoPkt0_action); \
});})

#define DoPkt1(port, action, arg1) ({ \
  struct MsgPort * _DoPkt1_port = (port); \
  LONG _DoPkt1_action = (action); \
  LONG _DoPkt1_arg1 = (arg1); \
  ({ \
  register char * _DoPkt1__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_DoPkt1__bn - 240))(_DoPkt1__bn, _DoPkt1_port, _DoPkt1_action, _DoPkt1_arg1); \
});})

#define DoPkt2(port, action, arg1, arg2) ({ \
  struct MsgPort * _DoPkt2_port = (port); \
  LONG _DoPkt2_action = (action); \
  LONG _DoPkt2_arg1 = (arg1); \
  LONG _DoPkt2_arg2 = (arg2); \
  ({ \
  register char * _DoPkt2__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"))) \
  (_DoPkt2__bn - 240))(_DoPkt2__bn, _DoPkt2_port, _DoPkt2_action, _DoPkt2_arg1, _DoPkt2_arg2); \
});})

#define DoPkt3(port, action, arg1, arg2, arg3) ({ \
  struct MsgPort * _DoPkt3_port = (port); \
  LONG _DoPkt3_action = (action); \
  LONG _DoPkt3_arg1 = (arg1); \
  LONG _DoPkt3_arg2 = (arg2); \
  LONG _DoPkt3_arg3 = (arg3); \
  ({ \
  register char * _DoPkt3__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"))) \
  (_DoPkt3__bn - 240))(_DoPkt3__bn, _DoPkt3_port, _DoPkt3_action, _DoPkt3_arg1, _DoPkt3_arg2, _DoPkt3_arg3); \
});})

#define DoPkt4(port, action, arg1, arg2, arg3, arg4) ({ \
  struct MsgPort * _DoPkt4_port = (port); \
  LONG _DoPkt4_action = (action); \
  LONG _DoPkt4_arg1 = (arg1); \
  LONG _DoPkt4_arg2 = (arg2); \
  LONG _DoPkt4_arg3 = (arg3); \
  LONG _DoPkt4_arg4 = (arg4); \
  ({ \
  register char * _DoPkt4__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), LONG __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), LONG __asm("d5"), LONG __asm("d6"))) \
  (_DoPkt4__bn - 240))(_DoPkt4__bn, _DoPkt4_port, _DoPkt4_action, _DoPkt4_arg1, _DoPkt4_arg2, _DoPkt4_arg3, _DoPkt4_arg4); \
});})

#define SendPkt(dp, port, replyport) ({ \
  struct DosPacket * _SendPkt_dp = (dp); \
  struct MsgPort * _SendPkt_port = (port); \
  struct MsgPort * _SendPkt_replyport = (replyport); \
  ({ \
  register char * _SendPkt__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct DosPacket * __asm("d1"), struct MsgPort * __asm("d2"), struct MsgPort * __asm("d3"))) \
  (_SendPkt__bn - 246))(_SendPkt__bn, _SendPkt_dp, _SendPkt_port, _SendPkt_replyport); \
});})

#define WaitPkt() ({ \
  register char * _WaitPkt__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DosPacket * (*)(char * __asm("a6"))) \
  (_WaitPkt__bn - 252))(_WaitPkt__bn); \
})

#define ReplyPkt(dp, res1, res2) ({ \
  struct DosPacket * _ReplyPkt_dp = (dp); \
  LONG _ReplyPkt_res1 = (res1); \
  LONG _ReplyPkt_res2 = (res2); \
  ({ \
  register char * _ReplyPkt__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct DosPacket * __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_ReplyPkt__bn - 258))(_ReplyPkt__bn, _ReplyPkt_dp, _ReplyPkt_res1, _ReplyPkt_res2); \
});})

#define AbortPkt(port, pkt) ({ \
  struct MsgPort * _AbortPkt_port = (port); \
  struct DosPacket * _AbortPkt_pkt = (pkt); \
  ({ \
  register char * _AbortPkt__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), struct DosPacket * __asm("d2"))) \
  (_AbortPkt__bn - 264))(_AbortPkt__bn, _AbortPkt_port, _AbortPkt_pkt); \
});})

#define LockRecord(fh, offset, length, mode, timeout) ({ \
  BPTR _LockRecord_fh = (fh); \
  ULONG _LockRecord_offset = (offset); \
  ULONG _LockRecord_length = (length); \
  ULONG _LockRecord_mode = (mode); \
  ULONG _LockRecord_timeout = (timeout); \
  ({ \
  register char * _LockRecord__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), BPTR __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"), ULONG __asm("d4"), ULONG __asm("d5"))) \
  (_LockRecord__bn - 270))(_LockRecord__bn, _LockRecord_fh, _LockRecord_offset, _LockRecord_length, _LockRecord_mode, _LockRecord_timeout); \
});})

#define LockRecords(recArray, timeout) ({ \
  CONST struct RecordLock * _LockRecords_recArray = (recArray); \
  ULONG _LockRecords_timeout = (timeout); \
  ({ \
  register char * _LockRecords__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct RecordLock * __asm("d1"), ULONG __asm("d2"))) \
  (_LockRecords__bn - 276))(_LockRecords__bn, _LockRecords_recArray, _LockRecords_timeout); \
});})

#define UnLockRecord(fh, offset, length) ({ \
  BPTR _UnLockRecord_fh = (fh); \
  ULONG _UnLockRecord_offset = (offset); \
  ULONG _UnLockRecord_length = (length); \
  ({ \
  register char * _UnLockRecord__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), BPTR __asm("d1"), ULONG __asm("d2"), ULONG __asm("d3"))) \
  (_UnLockRecord__bn - 282))(_UnLockRecord__bn, _UnLockRecord_fh, _UnLockRecord_offset, _UnLockRecord_length); \
});})

#define UnLockRecords(recArray) ({ \
  CONST struct RecordLock * _UnLockRecords_recArray = (recArray); \
  ({ \
  register char * _UnLockRecords__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct RecordLock * __asm("d1"))) \
  (_UnLockRecords__bn - 288))(_UnLockRecords__bn, _UnLockRecords_recArray); \
});})

#define SelectInput(fh) ({ \
  BPTR _SelectInput_fh = (fh); \
  ({ \
  register char * _SelectInput__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_SelectInput__bn - 294))(_SelectInput__bn, _SelectInput_fh); \
});})

#define SelectOutput(fh) ({ \
  BPTR _SelectOutput_fh = (fh); \
  ({ \
  register char * _SelectOutput__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_SelectOutput__bn - 300))(_SelectOutput__bn, _SelectOutput_fh); \
});})

#define FGetC(fh) ({ \
  BPTR _FGetC_fh = (fh); \
  ({ \
  register char * _FGetC__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_FGetC__bn - 306))(_FGetC__bn, _FGetC_fh); \
});})

#define FPutC(fh, ch) ({ \
  BPTR _FPutC_fh = (fh); \
  LONG _FPutC_ch = (ch); \
  ({ \
  register char * _FPutC__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"))) \
  (_FPutC__bn - 312))(_FPutC__bn, _FPutC_fh, _FPutC_ch); \
});})

#define UnGetC(fh, character) ({ \
  BPTR _UnGetC_fh = (fh); \
  LONG _UnGetC_character = (character); \
  ({ \
  register char * _UnGetC__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"))) \
  (_UnGetC__bn - 318))(_UnGetC__bn, _UnGetC_fh, _UnGetC_character); \
});})

#define FRead(fh, block, blocklen, number) ({ \
  BPTR _FRead_fh = (fh); \
  APTR _FRead_block = (block); \
  ULONG _FRead_blocklen = (blocklen); \
  ULONG _FRead_number = (number); \
  ({ \
  register char * _FRead__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), APTR __asm("d2"), ULONG __asm("d3"), ULONG __asm("d4"))) \
  (_FRead__bn - 324))(_FRead__bn, _FRead_fh, _FRead_block, _FRead_blocklen, _FRead_number); \
});})

#define FWrite(fh, block, blocklen, number) ({ \
  BPTR _FWrite_fh = (fh); \
  CONST_APTR _FWrite_block = (block); \
  ULONG _FWrite_blocklen = (blocklen); \
  ULONG _FWrite_number = (number); \
  ({ \
  register char * _FWrite__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), CONST_APTR __asm("d2"), ULONG __asm("d3"), ULONG __asm("d4"))) \
  (_FWrite__bn - 330))(_FWrite__bn, _FWrite_fh, _FWrite_block, _FWrite_blocklen, _FWrite_number); \
});})

#define FGets(fh, buf, buflen) ({ \
  BPTR _FGets_fh = (fh); \
  STRPTR _FGets_buf = (buf); \
  ULONG _FGets_buflen = (buflen); \
  ({ \
  register char * _FGets__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), BPTR __asm("d1"), STRPTR __asm("d2"), ULONG __asm("d3"))) \
  (_FGets__bn - 336))(_FGets__bn, _FGets_fh, _FGets_buf, _FGets_buflen); \
});})

#define FPuts(fh, str) ({ \
  BPTR _FPuts_fh = (fh); \
  CONST_STRPTR _FPuts_str = (str); \
  ({ \
  register char * _FPuts__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_FPuts__bn - 342))(_FPuts__bn, _FPuts_fh, _FPuts_str); \
});})

#define VFWritef(fh, format, argarray) ({ \
  BPTR _VFWritef_fh = (fh); \
  CONST_STRPTR _VFWritef_format = (format); \
  CONST LONG * _VFWritef_argarray = (argarray); \
  ({ \
  register char * _VFWritef__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), BPTR __asm("d1"), CONST_STRPTR __asm("d2"), CONST LONG * __asm("d3"))) \
  (_VFWritef__bn - 348))(_VFWritef__bn, _VFWritef_fh, _VFWritef_format, _VFWritef_argarray); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ VOID ___FWritef(struct DosLibrary * DOSBase, BPTR fh, CONST_STRPTR format, ...)
{
  VFWritef(fh, format, (CONST LONG *) ((ULONG) &format + sizeof(CONST_STRPTR)));
}

#define FWritef(fh, format...) ___FWritef(DOS_BASE_NAME, fh, format)
#endif

#define VFPrintf(fh, format, argarray) ({ \
  BPTR _VFPrintf_fh = (fh); \
  CONST_STRPTR _VFPrintf_format = (format); \
  CONST_APTR _VFPrintf_argarray = (argarray); \
  ({ \
  register char * _VFPrintf__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), CONST_STRPTR __asm("d2"), CONST_APTR __asm("d3"))) \
  (_VFPrintf__bn - 354))(_VFPrintf__bn, _VFPrintf_fh, _VFPrintf_format, _VFPrintf_argarray); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___FPrintf(struct DosLibrary * DOSBase, BPTR fh, CONST_STRPTR format, ...)
{
  return VFPrintf(fh, format, (CONST_APTR) ((ULONG) &format + sizeof(CONST_STRPTR)));
}

#define FPrintf(fh, format...) ___FPrintf(DOS_BASE_NAME, fh, format)
#endif

#define Flush(fh) ({ \
  BPTR _Flush_fh = (fh); \
  ({ \
  register char * _Flush__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_Flush__bn - 360))(_Flush__bn, _Flush_fh); \
});})

#define SetVBuf(fh, buff, type, size) ({ \
  BPTR _SetVBuf_fh = (fh); \
  STRPTR _SetVBuf_buff = (buff); \
  LONG _SetVBuf_type = (type); \
  LONG _SetVBuf_size = (size); \
  ({ \
  register char * _SetVBuf__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), STRPTR __asm("d2"), LONG __asm("d3"), LONG __asm("d4"))) \
  (_SetVBuf__bn - 366))(_SetVBuf__bn, _SetVBuf_fh, _SetVBuf_buff, _SetVBuf_type, _SetVBuf_size); \
});})

#define DupLockFromFH(fh) ({ \
  BPTR _DupLockFromFH_fh = (fh); \
  ({ \
  register char * _DupLockFromFH__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_DupLockFromFH__bn - 372))(_DupLockFromFH__bn, _DupLockFromFH_fh); \
});})

#define OpenFromLock(lock) ({ \
  BPTR _OpenFromLock_lock = (lock); \
  ({ \
  register char * _OpenFromLock__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_OpenFromLock__bn - 378))(_OpenFromLock__bn, _OpenFromLock_lock); \
});})

#define ParentOfFH(fh) ({ \
  BPTR _ParentOfFH_fh = (fh); \
  ({ \
  register char * _ParentOfFH__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_ParentOfFH__bn - 384))(_ParentOfFH__bn, _ParentOfFH_fh); \
});})

#define ExamineFH(fh, fib) ({ \
  BPTR _ExamineFH_fh = (fh); \
  struct FileInfoBlock * _ExamineFH_fib = (fib); \
  ({ \
  register char * _ExamineFH__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), BPTR __asm("d1"), struct FileInfoBlock * __asm("d2"))) \
  (_ExamineFH__bn - 390))(_ExamineFH__bn, _ExamineFH_fh, _ExamineFH_fib); \
});})

#define SetFileDate(name, date) ({ \
  CONST_STRPTR _SetFileDate_name = (name); \
  CONST struct DateStamp * _SetFileDate_date = (date); \
  ({ \
  register char * _SetFileDate__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST struct DateStamp * __asm("d2"))) \
  (_SetFileDate__bn - 396))(_SetFileDate__bn, _SetFileDate_name, _SetFileDate_date); \
});})

#define NameFromLock(lock, buffer, len) ({ \
  BPTR _NameFromLock_lock = (lock); \
  STRPTR _NameFromLock_buffer = (buffer); \
  LONG _NameFromLock_len = (len); \
  ({ \
  register char * _NameFromLock__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), STRPTR __asm("d2"), LONG __asm("d3"))) \
  (_NameFromLock__bn - 402))(_NameFromLock__bn, _NameFromLock_lock, _NameFromLock_buffer, _NameFromLock_len); \
});})

#define NameFromFH(fh, buffer, len) ({ \
  BPTR _NameFromFH_fh = (fh); \
  STRPTR _NameFromFH_buffer = (buffer); \
  LONG _NameFromFH_len = (len); \
  ({ \
  register char * _NameFromFH__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), STRPTR __asm("d2"), LONG __asm("d3"))) \
  (_NameFromFH__bn - 408))(_NameFromFH__bn, _NameFromFH_fh, _NameFromFH_buffer, _NameFromFH_len); \
});})

#define SplitName(name, separator, buf, oldpos, size) ({ \
  CONST_STRPTR _SplitName_name = (name); \
  ULONG _SplitName_separator = (separator); \
  STRPTR _SplitName_buf = (buf); \
  LONG _SplitName_oldpos = (oldpos); \
  LONG _SplitName_size = (size); \
  ({ \
  register char * _SplitName__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((WORD (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), ULONG __asm("d2"), STRPTR __asm("d3"), LONG __asm("d4"), LONG __asm("d5"))) \
  (_SplitName__bn - 414))(_SplitName__bn, _SplitName_name, _SplitName_separator, _SplitName_buf, _SplitName_oldpos, _SplitName_size); \
});})

#define SameLock(lock1, lock2) ({ \
  BPTR _SameLock_lock1 = (lock1); \
  BPTR _SameLock_lock2 = (lock2); \
  ({ \
  register char * _SameLock__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), BPTR __asm("d2"))) \
  (_SameLock__bn - 420))(_SameLock__bn, _SameLock_lock1, _SameLock_lock2); \
});})

#define SetMode(fh, mode) ({ \
  BPTR _SetMode_fh = (fh); \
  LONG _SetMode_mode = (mode); \
  ({ \
  register char * _SetMode__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"))) \
  (_SetMode__bn - 426))(_SetMode__bn, _SetMode_fh, _SetMode_mode); \
});})

#define ExAll(lock, buffer, size, data, control) ({ \
  BPTR _ExAll_lock = (lock); \
  struct ExAllData * _ExAll_buffer = (buffer); \
  LONG _ExAll_size = (size); \
  LONG _ExAll_data = (data); \
  struct ExAllControl * _ExAll_control = (control); \
  ({ \
  register char * _ExAll__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), struct ExAllData * __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), struct ExAllControl * __asm("d5"))) \
  (_ExAll__bn - 432))(_ExAll__bn, _ExAll_lock, _ExAll_buffer, _ExAll_size, _ExAll_data, _ExAll_control); \
});})

#define ReadLink(port, lock, path, buffer, size) ({ \
  struct MsgPort * _ReadLink_port = (port); \
  BPTR _ReadLink_lock = (lock); \
  CONST_STRPTR _ReadLink_path = (path); \
  STRPTR _ReadLink_buffer = (buffer); \
  ULONG _ReadLink_size = (size); \
  ({ \
  register char * _ReadLink__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct MsgPort * __asm("d1"), BPTR __asm("d2"), CONST_STRPTR __asm("d3"), STRPTR __asm("d4"), ULONG __asm("d5"))) \
  (_ReadLink__bn - 438))(_ReadLink__bn, _ReadLink_port, _ReadLink_lock, _ReadLink_path, _ReadLink_buffer, _ReadLink_size); \
});})

#define MakeLink(name, dest, soft) ({ \
  CONST_STRPTR _MakeLink_name = (name); \
  LONG _MakeLink_dest = (dest); \
  LONG _MakeLink_soft = (soft); \
  ({ \
  register char * _MakeLink__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_MakeLink__bn - 444))(_MakeLink__bn, _MakeLink_name, _MakeLink_dest, _MakeLink_soft); \
});})

#define ChangeMode(type, fh, newmode) ({ \
  LONG _ChangeMode_type = (type); \
  BPTR _ChangeMode_fh = (fh); \
  LONG _ChangeMode_newmode = (newmode); \
  ({ \
  register char * _ChangeMode__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d1"), BPTR __asm("d2"), LONG __asm("d3"))) \
  (_ChangeMode__bn - 450))(_ChangeMode__bn, _ChangeMode_type, _ChangeMode_fh, _ChangeMode_newmode); \
});})

#define SetFileSize(fh, pos, mode) ({ \
  BPTR _SetFileSize_fh = (fh); \
  LONG _SetFileSize_pos = (pos); \
  LONG _SetFileSize_mode = (mode); \
  ({ \
  register char * _SetFileSize__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"), LONG __asm("d3"))) \
  (_SetFileSize__bn - 456))(_SetFileSize__bn, _SetFileSize_fh, _SetFileSize_pos, _SetFileSize_mode); \
});})

#define SetIoErr(result) ({ \
  LONG _SetIoErr_result = (result); \
  ({ \
  register char * _SetIoErr__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d1"))) \
  (_SetIoErr__bn - 462))(_SetIoErr__bn, _SetIoErr_result); \
});})

#define Fault(code, header, buffer, len) ({ \
  LONG _Fault_code = (code); \
  CONST_STRPTR _Fault_header = (header); \
  STRPTR _Fault_buffer = (buffer); \
  LONG _Fault_len = (len); \
  ({ \
  register char * _Fault__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), LONG __asm("d1"), CONST_STRPTR __asm("d2"), STRPTR __asm("d3"), LONG __asm("d4"))) \
  (_Fault__bn - 468))(_Fault__bn, _Fault_code, _Fault_header, _Fault_buffer, _Fault_len); \
});})

#define PrintFault(code, header) ({ \
  LONG _PrintFault_code = (code); \
  CONST_STRPTR _PrintFault_header = (header); \
  ({ \
  register char * _PrintFault__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), LONG __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_PrintFault__bn - 474))(_PrintFault__bn, _PrintFault_code, _PrintFault_header); \
});})

#define ErrorReport(code, type, arg1, device) ({ \
  LONG _ErrorReport_code = (code); \
  LONG _ErrorReport_type = (type); \
  ULONG _ErrorReport_arg1 = (arg1); \
  struct MsgPort * _ErrorReport_device = (device); \
  ({ \
  register char * _ErrorReport__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d1"), LONG __asm("d2"), ULONG __asm("d3"), struct MsgPort * __asm("d4"))) \
  (_ErrorReport__bn - 480))(_ErrorReport__bn, _ErrorReport_code, _ErrorReport_type, _ErrorReport_arg1, _ErrorReport_device); \
});})

#define Cli() ({ \
  register char * _Cli__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct CommandLineInterface * (*)(char * __asm("a6"))) \
  (_Cli__bn - 492))(_Cli__bn); \
})

#define CreateNewProc(tags) ({ \
  CONST struct TagItem * _CreateNewProc_tags = (tags); \
  ({ \
  register char * _CreateNewProc__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct Process * (*)(char * __asm("a6"), CONST struct TagItem * __asm("d1"))) \
  (_CreateNewProc__bn - 498))(_CreateNewProc__bn, _CreateNewProc_tags); \
});})

#define CreateNewProcTagList(tags) CreateNewProc((tags))

#ifndef NO_INLINE_STDARG
static __inline__ struct Process * ___CreateNewProcTags(struct DosLibrary * DOSBase, ULONG tags, ...)
{
  return CreateNewProc((CONST struct TagItem *) &tags);
}

#define CreateNewProcTags(tags...) ___CreateNewProcTags(DOS_BASE_NAME, tags)
#endif

#define RunCommand(seg, stack, paramptr, paramlen) ({ \
  BPTR _RunCommand_seg = (seg); \
  LONG _RunCommand_stack = (stack); \
  CONST_STRPTR _RunCommand_paramptr = (paramptr); \
  LONG _RunCommand_paramlen = (paramlen); \
  ({ \
  register char * _RunCommand__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"), CONST_STRPTR __asm("d3"), LONG __asm("d4"))) \
  (_RunCommand__bn - 504))(_RunCommand__bn, _RunCommand_seg, _RunCommand_stack, _RunCommand_paramptr, _RunCommand_paramlen); \
});})

#define GetConsoleTask() ({ \
  register char * _GetConsoleTask__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"))) \
  (_GetConsoleTask__bn - 510))(_GetConsoleTask__bn); \
})

#define SetConsoleTask(task) ({ \
  struct MsgPort * _SetConsoleTask_task = (task); \
  ({ \
  register char * _SetConsoleTask__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"), struct MsgPort * __asm("d1"))) \
  (_SetConsoleTask__bn - 516))(_SetConsoleTask__bn, _SetConsoleTask_task); \
});})

#define GetFileSysTask() ({ \
  register char * _GetFileSysTask__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"))) \
  (_GetFileSysTask__bn - 522))(_GetFileSysTask__bn); \
})

#define SetFileSysTask(task) ({ \
  struct MsgPort * _SetFileSysTask_task = (task); \
  ({ \
  register char * _SetFileSysTask__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"), struct MsgPort * __asm("d1"))) \
  (_SetFileSysTask__bn - 528))(_SetFileSysTask__bn, _SetFileSysTask_task); \
});})

#define GetArgStr() ({ \
  register char * _GetArgStr__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"))) \
  (_GetArgStr__bn - 534))(_GetArgStr__bn); \
})

#define SetArgStr(string) ({ \
  STRPTR _SetArgStr_string = (string); \
  ({ \
  register char * _SetArgStr__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), STRPTR __asm("d1"))) \
  (_SetArgStr__bn - 540))(_SetArgStr__bn, _SetArgStr_string); \
});})

#define FindCliProc(num) ({ \
  ULONG _FindCliProc_num = (num); \
  ({ \
  register char * _FindCliProc__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct Process * (*)(char * __asm("a6"), ULONG __asm("d1"))) \
  (_FindCliProc__bn - 546))(_FindCliProc__bn, _FindCliProc_num); \
});})

#define MaxCli() ({ \
  register char * _MaxCli__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_MaxCli__bn - 552))(_MaxCli__bn); \
})

#define SetCurrentDirName(name) ({ \
  CONST_STRPTR _SetCurrentDirName_name = (name); \
  ({ \
  register char * _SetCurrentDirName__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_SetCurrentDirName__bn - 558))(_SetCurrentDirName__bn, _SetCurrentDirName_name); \
});})

#define GetCurrentDirName(buf, len) ({ \
  STRPTR _GetCurrentDirName_buf = (buf); \
  LONG _GetCurrentDirName_len = (len); \
  ({ \
  register char * _GetCurrentDirName__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_GetCurrentDirName__bn - 564))(_GetCurrentDirName__bn, _GetCurrentDirName_buf, _GetCurrentDirName_len); \
});})

#define SetProgramName(name) ({ \
  CONST_STRPTR _SetProgramName_name = (name); \
  ({ \
  register char * _SetProgramName__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_SetProgramName__bn - 570))(_SetProgramName__bn, _SetProgramName_name); \
});})

#define GetProgramName(buf, len) ({ \
  STRPTR _GetProgramName_buf = (buf); \
  LONG _GetProgramName_len = (len); \
  ({ \
  register char * _GetProgramName__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_GetProgramName__bn - 576))(_GetProgramName__bn, _GetProgramName_buf, _GetProgramName_len); \
});})

#define SetPrompt(name) ({ \
  CONST_STRPTR _SetPrompt_name = (name); \
  ({ \
  register char * _SetPrompt__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_SetPrompt__bn - 582))(_SetPrompt__bn, _SetPrompt_name); \
});})

#define GetPrompt(buf, len) ({ \
  STRPTR _GetPrompt_buf = (buf); \
  LONG _GetPrompt_len = (len); \
  ({ \
  register char * _GetPrompt__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_GetPrompt__bn - 588))(_GetPrompt__bn, _GetPrompt_buf, _GetPrompt_len); \
});})

#define SetProgramDir(lock) ({ \
  BPTR _SetProgramDir_lock = (lock); \
  ({ \
  register char * _SetProgramDir__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_SetProgramDir__bn - 594))(_SetProgramDir__bn, _SetProgramDir_lock); \
});})

#define GetProgramDir() ({ \
  register char * _GetProgramDir__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"))) \
  (_GetProgramDir__bn - 600))(_GetProgramDir__bn); \
})

#define SystemTagList(command, tags) ({ \
  CONST_STRPTR _SystemTagList_command = (command); \
  CONST struct TagItem * _SystemTagList_tags = (tags); \
  ({ \
  register char * _SystemTagList__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST struct TagItem * __asm("d2"))) \
  (_SystemTagList__bn - 606))(_SystemTagList__bn, _SystemTagList_command, _SystemTagList_tags); \
});})

#define System(command, tags) SystemTagList((command), (tags))

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___SystemTags(struct DosLibrary * DOSBase, CONST_STRPTR command, ULONG tags, ...)
{
  return SystemTagList(command, (CONST struct TagItem *) &tags);
}

#define SystemTags(command...) ___SystemTags(DOS_BASE_NAME, command)
#endif

#define AssignLock(name, lock) ({ \
  CONST_STRPTR _AssignLock_name = (name); \
  BPTR _AssignLock_lock = (lock); \
  ({ \
  register char * _AssignLock__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), BPTR __asm("d2"))) \
  (_AssignLock__bn - 612))(_AssignLock__bn, _AssignLock_name, _AssignLock_lock); \
});})

#define AssignLate(name, path) ({ \
  CONST_STRPTR _AssignLate_name = (name); \
  CONST_STRPTR _AssignLate_path = (path); \
  ({ \
  register char * _AssignLate__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_AssignLate__bn - 618))(_AssignLate__bn, _AssignLate_name, _AssignLate_path); \
});})

#define AssignPath(name, path) ({ \
  CONST_STRPTR _AssignPath_name = (name); \
  CONST_STRPTR _AssignPath_path = (path); \
  ({ \
  register char * _AssignPath__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_AssignPath__bn - 624))(_AssignPath__bn, _AssignPath_name, _AssignPath_path); \
});})

#define AssignAdd(name, lock) ({ \
  CONST_STRPTR _AssignAdd_name = (name); \
  BPTR _AssignAdd_lock = (lock); \
  ({ \
  register char * _AssignAdd__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), BPTR __asm("d2"))) \
  (_AssignAdd__bn - 630))(_AssignAdd__bn, _AssignAdd_name, _AssignAdd_lock); \
});})

#define RemAssignList(name, lock) ({ \
  CONST_STRPTR _RemAssignList_name = (name); \
  BPTR _RemAssignList_lock = (lock); \
  ({ \
  register char * _RemAssignList__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), BPTR __asm("d2"))) \
  (_RemAssignList__bn - 636))(_RemAssignList__bn, _RemAssignList_name, _RemAssignList_lock); \
});})

#define GetDeviceProc(name, dp) ({ \
  CONST_STRPTR _GetDeviceProc_name = (name); \
  struct DevProc * _GetDeviceProc_dp = (dp); \
  ({ \
  register char * _GetDeviceProc__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DevProc * (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), struct DevProc * __asm("d2"))) \
  (_GetDeviceProc__bn - 642))(_GetDeviceProc__bn, _GetDeviceProc_name, _GetDeviceProc_dp); \
});})

#define FreeDeviceProc(dp) ({ \
  struct DevProc * _FreeDeviceProc_dp = (dp); \
  ({ \
  register char * _FreeDeviceProc__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct DevProc * __asm("d1"))) \
  (_FreeDeviceProc__bn - 648))(_FreeDeviceProc__bn, _FreeDeviceProc_dp); \
});})

#define LockDosList(flags) ({ \
  ULONG _LockDosList_flags = (flags); \
  ({ \
  register char * _LockDosList__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DosList * (*)(char * __asm("a6"), ULONG __asm("d1"))) \
  (_LockDosList__bn - 654))(_LockDosList__bn, _LockDosList_flags); \
});})

#define UnLockDosList(flags) ({ \
  ULONG _UnLockDosList_flags = (flags); \
  ({ \
  register char * _UnLockDosList__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d1"))) \
  (_UnLockDosList__bn - 660))(_UnLockDosList__bn, _UnLockDosList_flags); \
});})

#define AttemptLockDosList(flags) ({ \
  ULONG _AttemptLockDosList_flags = (flags); \
  ({ \
  register char * _AttemptLockDosList__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DosList * (*)(char * __asm("a6"), ULONG __asm("d1"))) \
  (_AttemptLockDosList__bn - 666))(_AttemptLockDosList__bn, _AttemptLockDosList_flags); \
});})

#define RemDosEntry(dlist) ({ \
  struct DosList * _RemDosEntry_dlist = (dlist); \
  ({ \
  register char * _RemDosEntry__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct DosList * __asm("d1"))) \
  (_RemDosEntry__bn - 672))(_RemDosEntry__bn, _RemDosEntry_dlist); \
});})

#define AddDosEntry(dlist) ({ \
  struct DosList * _AddDosEntry_dlist = (dlist); \
  ({ \
  register char * _AddDosEntry__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct DosList * __asm("d1"))) \
  (_AddDosEntry__bn - 678))(_AddDosEntry__bn, _AddDosEntry_dlist); \
});})

#define FindDosEntry(dlist, name, flags) ({ \
  CONST struct DosList * _FindDosEntry_dlist = (dlist); \
  CONST_STRPTR _FindDosEntry_name = (name); \
  ULONG _FindDosEntry_flags = (flags); \
  ({ \
  register char * _FindDosEntry__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DosList * (*)(char * __asm("a6"), CONST struct DosList * __asm("d1"), CONST_STRPTR __asm("d2"), ULONG __asm("d3"))) \
  (_FindDosEntry__bn - 684))(_FindDosEntry__bn, _FindDosEntry_dlist, _FindDosEntry_name, _FindDosEntry_flags); \
});})

#define NextDosEntry(dlist, flags) ({ \
  CONST struct DosList * _NextDosEntry_dlist = (dlist); \
  ULONG _NextDosEntry_flags = (flags); \
  ({ \
  register char * _NextDosEntry__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DosList * (*)(char * __asm("a6"), CONST struct DosList * __asm("d1"), ULONG __asm("d2"))) \
  (_NextDosEntry__bn - 690))(_NextDosEntry__bn, _NextDosEntry_dlist, _NextDosEntry_flags); \
});})

#define MakeDosEntry(name, type) ({ \
  CONST_STRPTR _MakeDosEntry_name = (name); \
  LONG _MakeDosEntry_type = (type); \
  ({ \
  register char * _MakeDosEntry__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct DosList * (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_MakeDosEntry__bn - 696))(_MakeDosEntry__bn, _MakeDosEntry_name, _MakeDosEntry_type); \
});})

#define FreeDosEntry(dlist) ({ \
  struct DosList * _FreeDosEntry_dlist = (dlist); \
  ({ \
  register char * _FreeDosEntry__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct DosList * __asm("d1"))) \
  (_FreeDosEntry__bn - 702))(_FreeDosEntry__bn, _FreeDosEntry_dlist); \
});})

#define IsFileSystem(name) ({ \
  CONST_STRPTR _IsFileSystem_name = (name); \
  ({ \
  register char * _IsFileSystem__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_IsFileSystem__bn - 708))(_IsFileSystem__bn, _IsFileSystem_name); \
});})

#define Format(filesystem, volumename, dostype) ({ \
  CONST_STRPTR _Format_filesystem = (filesystem); \
  CONST_STRPTR _Format_volumename = (volumename); \
  ULONG _Format_dostype = (dostype); \
  ({ \
  register char * _Format__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"), ULONG __asm("d3"))) \
  (_Format__bn - 714))(_Format__bn, _Format_filesystem, _Format_volumename, _Format_dostype); \
});})

#define Relabel(drive, newname) ({ \
  CONST_STRPTR _Relabel_drive = (drive); \
  CONST_STRPTR _Relabel_newname = (newname); \
  ({ \
  register char * _Relabel__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_Relabel__bn - 720))(_Relabel__bn, _Relabel_drive, _Relabel_newname); \
});})

#define Inhibit(name, onoff) ({ \
  CONST_STRPTR _Inhibit_name = (name); \
  LONG _Inhibit_onoff = (onoff); \
  ({ \
  register char * _Inhibit__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_Inhibit__bn - 726))(_Inhibit__bn, _Inhibit_name, _Inhibit_onoff); \
});})

#define AddBuffers(name, number) ({ \
  CONST_STRPTR _AddBuffers_name = (name); \
  LONG _AddBuffers_number = (number); \
  ({ \
  register char * _AddBuffers__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_AddBuffers__bn - 732))(_AddBuffers__bn, _AddBuffers_name, _AddBuffers_number); \
});})

#define CompareDates(date1, date2) ({ \
  CONST struct DateStamp * _CompareDates_date1 = (date1); \
  CONST struct DateStamp * _CompareDates_date2 = (date2); \
  ({ \
  register char * _CompareDates__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST struct DateStamp * __asm("d1"), CONST struct DateStamp * __asm("d2"))) \
  (_CompareDates__bn - 738))(_CompareDates__bn, _CompareDates_date1, _CompareDates_date2); \
});})

#define DateToStr(datetime) ({ \
  struct DateTime * _DateToStr_datetime = (datetime); \
  ({ \
  register char * _DateToStr__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct DateTime * __asm("d1"))) \
  (_DateToStr__bn - 744))(_DateToStr__bn, _DateToStr_datetime); \
});})

#define StrToDate(datetime) ({ \
  struct DateTime * _StrToDate_datetime = (datetime); \
  ({ \
  register char * _StrToDate__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct DateTime * __asm("d1"))) \
  (_StrToDate__bn - 750))(_StrToDate__bn, _StrToDate_datetime); \
});})

#define InternalLoadSeg(fh, table, funcarray, stack) ({ \
  BPTR _InternalLoadSeg_fh = (fh); \
  BPTR _InternalLoadSeg_table = (table); \
  CONST LONG * _InternalLoadSeg_funcarray = (funcarray); \
  LONG * _InternalLoadSeg_stack = (stack); \
  ({ \
  register char * _InternalLoadSeg__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), BPTR __asm("d0"), BPTR __asm("a0"), CONST LONG * __asm("a1"), LONG * __asm("a2"))) \
  (_InternalLoadSeg__bn - 756))(_InternalLoadSeg__bn, _InternalLoadSeg_fh, _InternalLoadSeg_table, _InternalLoadSeg_funcarray, _InternalLoadSeg_stack); \
});})

#define InternalUnLoadSeg(seglist, freefunc) ({ \
  BPTR _InternalUnLoadSeg_seglist = (seglist); \
  VOID (*_InternalUnLoadSeg_freefunc)() = (freefunc); \
  ({ \
  register char * _InternalUnLoadSeg__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), BPTR __asm("d1"), VOID (*)() __asm("a1"))) \
  (_InternalUnLoadSeg__bn - 762))(_InternalUnLoadSeg__bn, _InternalUnLoadSeg_seglist, _InternalUnLoadSeg_freefunc); \
});})

#define NewLoadSeg(file, tags) ({ \
  CONST_STRPTR _NewLoadSeg_file = (file); \
  CONST struct TagItem * _NewLoadSeg_tags = (tags); \
  ({ \
  register char * _NewLoadSeg__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST struct TagItem * __asm("d2"))) \
  (_NewLoadSeg__bn - 768))(_NewLoadSeg__bn, _NewLoadSeg_file, _NewLoadSeg_tags); \
});})

#define NewLoadSegTagList(file, tags) NewLoadSeg((file), (tags))

#ifndef NO_INLINE_STDARG
static __inline__ BPTR ___NewLoadSegTags(struct DosLibrary * DOSBase, CONST_STRPTR file, ULONG tags, ...)
{
  return NewLoadSeg(file, (CONST struct TagItem *) &tags);
}

#define NewLoadSegTags(file...) ___NewLoadSegTags(DOS_BASE_NAME, file)
#endif

#define AddSegment(name, seg, system) ({ \
  CONST_STRPTR _AddSegment_name = (name); \
  BPTR _AddSegment_seg = (seg); \
  LONG _AddSegment_system = (system); \
  ({ \
  register char * _AddSegment__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), BPTR __asm("d2"), LONG __asm("d3"))) \
  (_AddSegment__bn - 774))(_AddSegment__bn, _AddSegment_name, _AddSegment_seg, _AddSegment_system); \
});})

#define FindSegment(name, seg, system) ({ \
  CONST_STRPTR _FindSegment_name = (name); \
  CONST struct Segment * _FindSegment_seg = (seg); \
  LONG _FindSegment_system = (system); \
  ({ \
  register char * _FindSegment__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct Segment * (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST struct Segment * __asm("d2"), LONG __asm("d3"))) \
  (_FindSegment__bn - 780))(_FindSegment__bn, _FindSegment_name, _FindSegment_seg, _FindSegment_system); \
});})

#define RemSegment(seg) ({ \
  struct Segment * _RemSegment_seg = (seg); \
  ({ \
  register char * _RemSegment__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Segment * __asm("d1"))) \
  (_RemSegment__bn - 786))(_RemSegment__bn, _RemSegment_seg); \
});})

#define CheckSignal(mask) ({ \
  LONG _CheckSignal_mask = (mask); \
  ({ \
  register char * _CheckSignal__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d1"))) \
  (_CheckSignal__bn - 792))(_CheckSignal__bn, _CheckSignal_mask); \
});})

#define ReadArgs(arg_template, array, args) ({ \
  CONST_STRPTR _ReadArgs_arg_template = (arg_template); \
  LONG * _ReadArgs_array = (array); \
  struct RDArgs * _ReadArgs_args = (args); \
  ({ \
  register char * _ReadArgs__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct RDArgs * (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG * __asm("d2"), struct RDArgs * __asm("d3"))) \
  (_ReadArgs__bn - 798))(_ReadArgs__bn, _ReadArgs_arg_template, _ReadArgs_array, _ReadArgs_args); \
});})

#define FindArg(keyword, arg_template) ({ \
  CONST_STRPTR _FindArg_keyword = (keyword); \
  CONST_STRPTR _FindArg_arg_template = (arg_template); \
  ({ \
  register char * _FindArg__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_FindArg__bn - 804))(_FindArg__bn, _FindArg_keyword, _FindArg_arg_template); \
});})

#define ReadItem(name, maxchars, cSource) ({ \
  CONST_STRPTR _ReadItem_name = (name); \
  LONG _ReadItem_maxchars = (maxchars); \
  struct CSource * _ReadItem_cSource = (cSource); \
  ({ \
  register char * _ReadItem__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"), struct CSource * __asm("d3"))) \
  (_ReadItem__bn - 810))(_ReadItem__bn, _ReadItem_name, _ReadItem_maxchars, _ReadItem_cSource); \
});})

#define StrToLong(string, value) ({ \
  CONST_STRPTR _StrToLong_string = (string); \
  LONG * _StrToLong_value = (value); \
  ({ \
  register char * _StrToLong__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG * __asm("d2"))) \
  (_StrToLong__bn - 816))(_StrToLong__bn, _StrToLong_string, _StrToLong_value); \
});})

#define MatchFirst(pat, anchor) ({ \
  CONST_STRPTR _MatchFirst_pat = (pat); \
  struct AnchorPath * _MatchFirst_anchor = (anchor); \
  ({ \
  register char * _MatchFirst__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), struct AnchorPath * __asm("d2"))) \
  (_MatchFirst__bn - 822))(_MatchFirst__bn, _MatchFirst_pat, _MatchFirst_anchor); \
});})

#define MatchNext(anchor) ({ \
  struct AnchorPath * _MatchNext_anchor = (anchor); \
  ({ \
  register char * _MatchNext__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct AnchorPath * __asm("d1"))) \
  (_MatchNext__bn - 828))(_MatchNext__bn, _MatchNext_anchor); \
});})

#define MatchEnd(anchor) ({ \
  struct AnchorPath * _MatchEnd_anchor = (anchor); \
  ({ \
  register char * _MatchEnd__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct AnchorPath * __asm("d1"))) \
  (_MatchEnd__bn - 834))(_MatchEnd__bn, _MatchEnd_anchor); \
});})

#define ParsePattern(pat, patbuf, patbuflen) ({ \
  CONST_STRPTR _ParsePattern_pat = (pat); \
  UBYTE * _ParsePattern_patbuf = (patbuf); \
  LONG _ParsePattern_patbuflen = (patbuflen); \
  ({ \
  register char * _ParsePattern__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), UBYTE * __asm("d2"), LONG __asm("d3"))) \
  (_ParsePattern__bn - 840))(_ParsePattern__bn, _ParsePattern_pat, _ParsePattern_patbuf, _ParsePattern_patbuflen); \
});})

#define MatchPattern(patbuf, str) ({ \
  CONST UBYTE * _MatchPattern_patbuf = (patbuf); \
  CONST_STRPTR _MatchPattern_str = (str); \
  ({ \
  register char * _MatchPattern__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST UBYTE * __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_MatchPattern__bn - 846))(_MatchPattern__bn, _MatchPattern_patbuf, _MatchPattern_str); \
});})

#define FreeArgs(args) ({ \
  struct RDArgs * _FreeArgs_args = (args); \
  ({ \
  register char * _FreeArgs__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct RDArgs * __asm("d1"))) \
  (_FreeArgs__bn - 858))(_FreeArgs__bn, _FreeArgs_args); \
});})

#define FilePart(path) ({ \
  CONST_STRPTR _FilePart_path = (path); \
  ({ \
  register char * _FilePart__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_FilePart__bn - 870))(_FilePart__bn, _FilePart_path); \
});})

#define PathPart(path) ({ \
  CONST_STRPTR _PathPart_path = (path); \
  ({ \
  register char * _PathPart__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_PathPart__bn - 876))(_PathPart__bn, _PathPart_path); \
});})

#define AddPart(dirname, filename, size) ({ \
  STRPTR _AddPart_dirname = (dirname); \
  CONST_STRPTR _AddPart_filename = (filename); \
  ULONG _AddPart_size = (size); \
  ({ \
  register char * _AddPart__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), STRPTR __asm("d1"), CONST_STRPTR __asm("d2"), ULONG __asm("d3"))) \
  (_AddPart__bn - 882))(_AddPart__bn, _AddPart_dirname, _AddPart_filename, _AddPart_size); \
});})

#define StartNotify(notify) ({ \
  struct NotifyRequest * _StartNotify_notify = (notify); \
  ({ \
  register char * _StartNotify__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct NotifyRequest * __asm("d1"))) \
  (_StartNotify__bn - 888))(_StartNotify__bn, _StartNotify_notify); \
});})

#define EndNotify(notify) ({ \
  struct NotifyRequest * _EndNotify_notify = (notify); \
  ({ \
  register char * _EndNotify__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct NotifyRequest * __asm("d1"))) \
  (_EndNotify__bn - 894))(_EndNotify__bn, _EndNotify_notify); \
});})

#define SetVar(name, buffer, size, flags) ({ \
  CONST_STRPTR _SetVar_name = (name); \
  CONST_STRPTR _SetVar_buffer = (buffer); \
  LONG _SetVar_size = (size); \
  LONG _SetVar_flags = (flags); \
  ({ \
  register char * _SetVar__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_STRPTR __asm("d2"), LONG __asm("d3"), LONG __asm("d4"))) \
  (_SetVar__bn - 900))(_SetVar__bn, _SetVar_name, _SetVar_buffer, _SetVar_size, _SetVar_flags); \
});})

#define GetVar(name, buffer, size, flags) ({ \
  CONST_STRPTR _GetVar_name = (name); \
  STRPTR _GetVar_buffer = (buffer); \
  LONG _GetVar_size = (size); \
  LONG _GetVar_flags = (flags); \
  ({ \
  register char * _GetVar__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), STRPTR __asm("d2"), LONG __asm("d3"), LONG __asm("d4"))) \
  (_GetVar__bn - 906))(_GetVar__bn, _GetVar_name, _GetVar_buffer, _GetVar_size, _GetVar_flags); \
});})

#define DeleteVar(name, flags) ({ \
  CONST_STRPTR _DeleteVar_name = (name); \
  ULONG _DeleteVar_flags = (flags); \
  ({ \
  register char * _DeleteVar__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), ULONG __asm("d2"))) \
  (_DeleteVar__bn - 912))(_DeleteVar__bn, _DeleteVar_name, _DeleteVar_flags); \
});})

#define FindVar(name, type) ({ \
  CONST_STRPTR _FindVar_name = (name); \
  ULONG _FindVar_type = (type); \
  ({ \
  register char * _FindVar__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((struct LocalVar * (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), ULONG __asm("d2"))) \
  (_FindVar__bn - 918))(_FindVar__bn, _FindVar_name, _FindVar_type); \
});})

#define CliInitNewcli(dp) ({ \
  struct DosPacket * _CliInitNewcli_dp = (dp); \
  ({ \
  register char * _CliInitNewcli__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct DosPacket * __asm("a0"))) \
  (_CliInitNewcli__bn - 930))(_CliInitNewcli__bn, _CliInitNewcli_dp); \
});})

#define CliInitRun(dp) ({ \
  struct DosPacket * _CliInitRun_dp = (dp); \
  ({ \
  register char * _CliInitRun__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct DosPacket * __asm("a0"))) \
  (_CliInitRun__bn - 936))(_CliInitRun__bn, _CliInitRun_dp); \
});})

#define WriteChars(buf, buflen) ({ \
  CONST_STRPTR _WriteChars_buf = (buf); \
  ULONG _WriteChars_buflen = (buflen); \
  ({ \
  register char * _WriteChars__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), ULONG __asm("d2"))) \
  (_WriteChars__bn - 942))(_WriteChars__bn, _WriteChars_buf, _WriteChars_buflen); \
});})

#define PutStr(str) ({ \
  CONST_STRPTR _PutStr_str = (str); \
  ({ \
  register char * _PutStr__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_PutStr__bn - 948))(_PutStr__bn, _PutStr_str); \
});})

#define VPrintf(format, argarray) ({ \
  CONST_STRPTR _VPrintf_format = (format); \
  CONST_APTR _VPrintf_argarray = (argarray); \
  ({ \
  register char * _VPrintf__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), CONST_APTR __asm("d2"))) \
  (_VPrintf__bn - 954))(_VPrintf__bn, _VPrintf_format, _VPrintf_argarray); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ LONG ___Printf(struct DosLibrary * DOSBase, CONST_STRPTR format, ...)
{
  return VPrintf(format, (CONST_APTR) ((ULONG) &format + sizeof(CONST_STRPTR)));
}

#define Printf(format...) ___Printf(DOS_BASE_NAME, format)
#endif

#define ParsePatternNoCase(pat, patbuf, patbuflen) ({ \
  CONST_STRPTR _ParsePatternNoCase_pat = (pat); \
  UBYTE * _ParsePatternNoCase_patbuf = (patbuf); \
  LONG _ParsePatternNoCase_patbuflen = (patbuflen); \
  ({ \
  register char * _ParsePatternNoCase__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), UBYTE * __asm("d2"), LONG __asm("d3"))) \
  (_ParsePatternNoCase__bn - 966))(_ParsePatternNoCase__bn, _ParsePatternNoCase_pat, _ParsePatternNoCase_patbuf, _ParsePatternNoCase_patbuflen); \
});})

#define MatchPatternNoCase(patbuf, str) ({ \
  CONST UBYTE * _MatchPatternNoCase_patbuf = (patbuf); \
  CONST_STRPTR _MatchPatternNoCase_str = (str); \
  ({ \
  register char * _MatchPatternNoCase__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST UBYTE * __asm("d1"), CONST_STRPTR __asm("d2"))) \
  (_MatchPatternNoCase__bn - 972))(_MatchPatternNoCase__bn, _MatchPatternNoCase_patbuf, _MatchPatternNoCase_str); \
});})

#define SameDevice(lock1, lock2) ({ \
  BPTR _SameDevice_lock1 = (lock1); \
  BPTR _SameDevice_lock2 = (lock2); \
  ({ \
  register char * _SameDevice__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), BPTR __asm("d1"), BPTR __asm("d2"))) \
  (_SameDevice__bn - 984))(_SameDevice__bn, _SameDevice_lock1, _SameDevice_lock2); \
});})

#define ExAllEnd(lock, buffer, size, data, control) ({ \
  BPTR _ExAllEnd_lock = (lock); \
  struct ExAllData * _ExAllEnd_buffer = (buffer); \
  LONG _ExAllEnd_size = (size); \
  LONG _ExAllEnd_data = (data); \
  struct ExAllControl * _ExAllEnd_control = (control); \
  ({ \
  register char * _ExAllEnd__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), BPTR __asm("d1"), struct ExAllData * __asm("d2"), LONG __asm("d3"), LONG __asm("d4"), struct ExAllControl * __asm("d5"))) \
  (_ExAllEnd__bn - 990))(_ExAllEnd__bn, _ExAllEnd_lock, _ExAllEnd_buffer, _ExAllEnd_size, _ExAllEnd_data, _ExAllEnd_control); \
});})

#define SetOwner(name, owner_info) ({ \
  CONST_STRPTR _SetOwner_name = (name); \
  LONG _SetOwner_owner_info = (owner_info); \
  ({ \
  register char * _SetOwner__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"), LONG __asm("d2"))) \
  (_SetOwner__bn - 996))(_SetOwner__bn, _SetOwner_name, _SetOwner_owner_info); \
});})

#define VolumeRequestHook(vol) ({ \
  CONST_STRPTR _VolumeRequestHook_vol = (vol); \
  ({ \
  register char * _VolumeRequestHook__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_VolumeRequestHook__bn - 1014))(_VolumeRequestHook__bn, _VolumeRequestHook_vol); \
});})

#define GetCurrentDir() ({ \
  register char * _GetCurrentDir__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((BPTR (*)(char * __asm("a6"))) \
  (_GetCurrentDir__bn - 1026))(_GetCurrentDir__bn); \
})

#define PutErrStr(str) ({ \
  CONST_STRPTR _PutErrStr_str = (str); \
  ({ \
  register char * _PutErrStr__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), CONST_STRPTR __asm("d1"))) \
  (_PutErrStr__bn - 1128))(_PutErrStr__bn, _PutErrStr_str); \
});})

#define ErrorOutput() ({ \
  register char * _ErrorOutput__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"))) \
  (_ErrorOutput__bn - 1134))(_ErrorOutput__bn); \
})

#define SelectError(fh) ({ \
  BPTR _SelectError_fh = (fh); \
  ({ \
  register char * _SelectError__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"))) \
  (_SelectError__bn - 1140))(_SelectError__bn, _SelectError_fh); \
});})

#define DoShellMethodTagList(method, tags) ({ \
  ULONG _DoShellMethodTagList_method = (method); \
  CONST struct TagItem * _DoShellMethodTagList_tags = (tags); \
  ({ \
  register char * _DoShellMethodTagList__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"), CONST struct TagItem * __asm("a0"))) \
  (_DoShellMethodTagList__bn - 1152))(_DoShellMethodTagList__bn, _DoShellMethodTagList_method, _DoShellMethodTagList_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ APTR ___DoShellMethod(struct DosLibrary * DOSBase, ULONG method, ULONG tags, ...)
{
  return DoShellMethodTagList(method, (CONST struct TagItem *) &tags);
}

#define DoShellMethod(method...) ___DoShellMethod(DOS_BASE_NAME, method)
#endif

#define ScanStackToken(seg, defaultstack) ({ \
  BPTR _ScanStackToken_seg = (seg); \
  LONG _ScanStackToken_defaultstack = (defaultstack); \
  ({ \
  register char * _ScanStackToken__bn __asm("a6") = (char *) (DOS_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), BPTR __asm("d1"), LONG __asm("d2"))) \
  (_ScanStackToken__bn - 1158))(_ScanStackToken__bn, _ScanStackToken_seg, _ScanStackToken_defaultstack); \
});})

#endif /*  _INLINE_DOS_H  */
