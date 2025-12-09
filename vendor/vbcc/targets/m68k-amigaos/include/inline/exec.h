#ifndef _INLINE_EXEC_H
#define _INLINE_EXEC_H

#ifndef CLIB_EXEC_PROTOS_H
#define CLIB_EXEC_PROTOS_H
#endif

#ifndef  EXEC_EXECBASE_H
#include <exec/execbase.h>
#endif
#ifndef  EXEC_TASKS_H
#include <exec/tasks.h>
#endif
#ifndef  EXEC_MEMORY_H
#include <exec/memory.h>
#endif
#ifndef  EXEC_PORTS_H
#include <exec/ports.h>
#endif
#ifndef  EXEC_DEVICES_H
#include <exec/devices.h>
#endif
#ifndef  EXEC_IO_H
#include <exec/io.h>
#endif
#ifndef  EXEC_SEMAPHORES_H
#include <exec/semaphores.h>
#endif
#ifndef  EXEC_RESIDENT_H
#include <exec/resident.h>
#endif
#ifndef  EXEC_INTERRUPTS_H
#include <exec/interrupts.h>
#endif

#ifndef EXEC_BASE_NAME
#define EXEC_BASE_NAME SysBase
#endif

#define Supervisor(userFunction) ({ \
  ULONG (*_Supervisor_userFunction)() = (userFunction); \
  ({ \
  register char * _Supervisor__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG (*)() __asm("a5"))) \
  (_Supervisor__bn - 30))(_Supervisor__bn, _Supervisor_userFunction); \
});})

#define InitCode(startClass, version) ({ \
  ULONG _InitCode_startClass = (startClass); \
  ULONG _InitCode_version = (version); \
  ({ \
  register char * _InitCode__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_InitCode__bn - 72))(_InitCode__bn, _InitCode_startClass, _InitCode_version); \
});})

#define InitStruct(initTable, memory, size) ({ \
  CONST_APTR _InitStruct_initTable = (initTable); \
  APTR _InitStruct_memory = (memory); \
  ULONG _InitStruct_size = (size); \
  ({ \
  register char * _InitStruct__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_APTR __asm("a1"), APTR __asm("a2"), ULONG __asm("d0"))) \
  (_InitStruct__bn - 78))(_InitStruct__bn, _InitStruct_initTable, _InitStruct_memory, _InitStruct_size); \
});})

#define MakeLibrary(funcInit, structInit, libInit, dataSize, segList) ({ \
  CONST_APTR _MakeLibrary_funcInit = (funcInit); \
  CONST_APTR _MakeLibrary_structInit = (structInit); \
  ULONG (*_MakeLibrary_libInit)() = (libInit); \
  ULONG _MakeLibrary_dataSize = (dataSize); \
  ULONG _MakeLibrary_segList = (segList); \
  ({ \
  register char * _MakeLibrary__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Library * (*)(char * __asm("a6"), CONST_APTR __asm("a0"), CONST_APTR __asm("a1"), ULONG (*)() __asm("a2"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_MakeLibrary__bn - 84))(_MakeLibrary__bn, _MakeLibrary_funcInit, _MakeLibrary_structInit, _MakeLibrary_libInit, _MakeLibrary_dataSize, _MakeLibrary_segList); \
});})

#define MakeFunctions(target, functionArray, funcDispBase) ({ \
  APTR _MakeFunctions_target = (target); \
  CONST_APTR _MakeFunctions_functionArray = (functionArray); \
  ULONG _MakeFunctions_funcDispBase = (funcDispBase); \
  ({ \
  register char * _MakeFunctions__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"), CONST_APTR __asm("a1"), ULONG __asm("a2"))) \
  (_MakeFunctions__bn - 90))(_MakeFunctions__bn, _MakeFunctions_target, _MakeFunctions_functionArray, _MakeFunctions_funcDispBase); \
});})

#define FindResident(name) ({ \
  CONST_STRPTR _FindResident_name = (name); \
  ({ \
  register char * _FindResident__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Resident * (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"))) \
  (_FindResident__bn - 96))(_FindResident__bn, _FindResident_name); \
});})

#define InitResident(resident, segList) ({ \
  CONST struct Resident * _InitResident_resident = (resident); \
  ULONG _InitResident_segList = (segList); \
  ({ \
  register char * _InitResident__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), CONST struct Resident * __asm("a1"), ULONG __asm("d1"))) \
  (_InitResident__bn - 102))(_InitResident__bn, _InitResident_resident, _InitResident_segList); \
});})

#define Alert(alertNum) ({ \
  ULONG _Alert_alertNum = (alertNum); \
  ({ \
  register char * _Alert__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d7"))) \
  (_Alert__bn - 108))(_Alert__bn, _Alert_alertNum); \
});})

#define Debug(flags) ({ \
  ULONG _Debug_flags = (flags); \
  ({ \
  register char * _Debug__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_Debug__bn - 114))(_Debug__bn, _Debug_flags); \
});})

#define Disable() ({ \
  register char * _Disable__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_Disable__bn - 120))(_Disable__bn); \
})

#define Enable() ({ \
  register char * _Enable__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_Enable__bn - 126))(_Enable__bn); \
})

#define Forbid() ({ \
  register char * _Forbid__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_Forbid__bn - 132))(_Forbid__bn); \
})

#define Permit() ({ \
  register char * _Permit__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_Permit__bn - 138))(_Permit__bn); \
})

#define SetSR(newSR, mask) ({ \
  ULONG _SetSR_newSR = (newSR); \
  ULONG _SetSR_mask = (mask); \
  ({ \
  register char * _SetSR__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_SetSR__bn - 144))(_SetSR__bn, _SetSR_newSR, _SetSR_mask); \
});})

#define SuperState() ({ \
  register char * _SuperState__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"))) \
  (_SuperState__bn - 150))(_SuperState__bn); \
})

#define UserState(sysStack) ({ \
  APTR _UserState_sysStack = (sysStack); \
  ({ \
  register char * _UserState__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("d0"))) \
  (_UserState__bn - 156))(_UserState__bn, _UserState_sysStack); \
});})

#define SetIntVector(intNumber, interrupt) ({ \
  LONG _SetIntVector_intNumber = (intNumber); \
  struct Interrupt * _SetIntVector_interrupt = (interrupt); \
  ({ \
  register char * _SetIntVector__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Interrupt * (*)(char * __asm("a6"), LONG __asm("d0"), struct Interrupt * __asm("a1"))) \
  (_SetIntVector__bn - 162))(_SetIntVector__bn, _SetIntVector_intNumber, _SetIntVector_interrupt); \
});})

#define AddIntServer(intNumber, interrupt) ({ \
  LONG _AddIntServer_intNumber = (intNumber); \
  struct Interrupt * _AddIntServer_interrupt = (interrupt); \
  ({ \
  register char * _AddIntServer__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"), struct Interrupt * __asm("a1"))) \
  (_AddIntServer__bn - 168))(_AddIntServer__bn, _AddIntServer_intNumber, _AddIntServer_interrupt); \
});})

#define RemIntServer(intNumber, interrupt) ({ \
  LONG _RemIntServer_intNumber = (intNumber); \
  struct Interrupt * _RemIntServer_interrupt = (interrupt); \
  ({ \
  register char * _RemIntServer__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"), struct Interrupt * __asm("a1"))) \
  (_RemIntServer__bn - 174))(_RemIntServer__bn, _RemIntServer_intNumber, _RemIntServer_interrupt); \
});})

#define Cause(interrupt) ({ \
  CONST struct Interrupt * _Cause_interrupt = (interrupt); \
  ({ \
  register char * _Cause__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST struct Interrupt * __asm("a1"))) \
  (_Cause__bn - 180))(_Cause__bn, _Cause_interrupt); \
});})

#define Allocate(freeList, byteSize) ({ \
  struct MemHeader * _Allocate_freeList = (freeList); \
  ULONG _Allocate_byteSize = (byteSize); \
  ({ \
  register char * _Allocate__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct MemHeader * __asm("a0"), ULONG __asm("d0"))) \
  (_Allocate__bn - 186))(_Allocate__bn, _Allocate_freeList, _Allocate_byteSize); \
});})

#define Deallocate(freeList, memoryBlock, byteSize) ({ \
  struct MemHeader * _Deallocate_freeList = (freeList); \
  APTR _Deallocate_memoryBlock = (memoryBlock); \
  ULONG _Deallocate_byteSize = (byteSize); \
  ({ \
  register char * _Deallocate__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MemHeader * __asm("a0"), APTR __asm("a1"), ULONG __asm("d0"))) \
  (_Deallocate__bn - 192))(_Deallocate__bn, _Deallocate_freeList, _Deallocate_memoryBlock, _Deallocate_byteSize); \
});})

#define AllocMem(byteSize, requirements) ({ \
  ULONG _AllocMem_byteSize = (byteSize); \
  ULONG _AllocMem_requirements = (requirements); \
  ({ \
  register char * _AllocMem__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_AllocMem__bn - 198))(_AllocMem__bn, _AllocMem_byteSize, _AllocMem_requirements); \
});})

#define AllocAbs(byteSize, location) ({ \
  ULONG _AllocAbs_byteSize = (byteSize); \
  APTR _AllocAbs_location = (location); \
  ({ \
  register char * _AllocAbs__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"), APTR __asm("a1"))) \
  (_AllocAbs__bn - 204))(_AllocAbs__bn, _AllocAbs_byteSize, _AllocAbs_location); \
});})

#define FreeMem(memoryBlock, byteSize) ({ \
  APTR _FreeMem_memoryBlock = (memoryBlock); \
  ULONG _FreeMem_byteSize = (byteSize); \
  ({ \
  register char * _FreeMem__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"), ULONG __asm("d0"))) \
  (_FreeMem__bn - 210))(_FreeMem__bn, _FreeMem_memoryBlock, _FreeMem_byteSize); \
});})

#define AvailMem(requirements) ({ \
  ULONG _AvailMem_requirements = (requirements); \
  ({ \
  register char * _AvailMem__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d1"))) \
  (_AvailMem__bn - 216))(_AvailMem__bn, _AvailMem_requirements); \
});})

#define AllocEntry(entry) ({ \
  CONST struct MemList * _AllocEntry_entry = (entry); \
  ({ \
  register char * _AllocEntry__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct MemList * (*)(char * __asm("a6"), CONST struct MemList * __asm("a0"))) \
  (_AllocEntry__bn - 222))(_AllocEntry__bn, _AllocEntry_entry); \
});})

#define FreeEntry(entry) ({ \
  struct MemList * _FreeEntry_entry = (entry); \
  ({ \
  register char * _FreeEntry__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MemList * __asm("a0"))) \
  (_FreeEntry__bn - 228))(_FreeEntry__bn, _FreeEntry_entry); \
});})

#define Insert(list, node, pred) ({ \
  struct List * _Insert_list = (list); \
  struct Node * _Insert_node = (node); \
  struct Node * _Insert_pred = (pred); \
  ({ \
  register char * _Insert__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"), struct Node * __asm("a1"), struct Node * __asm("a2"))) \
  (_Insert__bn - 234))(_Insert__bn, _Insert_list, _Insert_node, _Insert_pred); \
});})

#define InsertMinNode(list, node, pred) Insert((list), (node), (pred))

#define AddHead(list, node) ({ \
  struct List * _AddHead_list = (list); \
  struct Node * _AddHead_node = (node); \
  ({ \
  register char * _AddHead__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"), struct Node * __asm("a1"))) \
  (_AddHead__bn - 240))(_AddHead__bn, _AddHead_list, _AddHead_node); \
});})

#define AddHeadMinList(list, node) AddHead((list), (node))

#define AddTail(list, node) ({ \
  struct List * _AddTail_list = (list); \
  struct Node * _AddTail_node = (node); \
  ({ \
  register char * _AddTail__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"), struct Node * __asm("a1"))) \
  (_AddTail__bn - 246))(_AddTail__bn, _AddTail_list, _AddTail_node); \
});})

#define AddTailMinList(list, node) AddTail((list), (node))

#define Remove(node) ({ \
  struct Node * _Remove_node = (node); \
  ({ \
  register char * _Remove__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Node * __asm("a1"))) \
  (_Remove__bn - 252))(_Remove__bn, _Remove_node); \
});})

#define RemoveMinNode(node) Remove((node))

#define RemHead(list) ({ \
  struct List * _RemHead_list = (list); \
  ({ \
  register char * _RemHead__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_RemHead__bn - 258))(_RemHead__bn, _RemHead_list); \
});})

#define RemHeadMinList(list) RemHead((list))

#define RemTail(list) ({ \
  struct List * _RemTail_list = (list); \
  ({ \
  register char * _RemTail__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_RemTail__bn - 264))(_RemTail__bn, _RemTail_list); \
});})

#define RemTailMinList(list) RemTail((list))

#define Enqueue(list, node) ({ \
  struct List * _Enqueue_list = (list); \
  struct Node * _Enqueue_node = (node); \
  ({ \
  register char * _Enqueue__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"), struct Node * __asm("a1"))) \
  (_Enqueue__bn - 270))(_Enqueue__bn, _Enqueue_list, _Enqueue_node); \
});})

#define FindName(list, name) ({ \
  struct List * _FindName_list = (list); \
  CONST_STRPTR _FindName_name = (name); \
  ({ \
  register char * _FindName__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Node * (*)(char * __asm("a6"), struct List * __asm("a0"), CONST_STRPTR __asm("a1"))) \
  (_FindName__bn - 276))(_FindName__bn, _FindName_list, _FindName_name); \
});})

#define AddTask(task, initPC, finalPC) ({ \
  struct Task * _AddTask_task = (task); \
  APTR _AddTask_initPC = (initPC); \
  APTR _AddTask_finalPC = (finalPC); \
  ({ \
  register char * _AddTask__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct Task * __asm("a1"), APTR __asm("a2"), APTR __asm("a3"))) \
  (_AddTask__bn - 282))(_AddTask__bn, _AddTask_task, _AddTask_initPC, _AddTask_finalPC); \
});})

#define RemTask(task) ({ \
  struct Task * _RemTask_task = (task); \
  ({ \
  register char * _RemTask__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Task * __asm("a1"))) \
  (_RemTask__bn - 288))(_RemTask__bn, _RemTask_task); \
});})

#define FindTask(name) ({ \
  CONST_STRPTR _FindTask_name = (name); \
  ({ \
  register char * _FindTask__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Task * (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"))) \
  (_FindTask__bn - 294))(_FindTask__bn, _FindTask_name); \
});})

#define SetTaskPri(task, priority) ({ \
  struct Task * _SetTaskPri_task = (task); \
  LONG _SetTaskPri_priority = (priority); \
  ({ \
  register char * _SetTaskPri__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((BYTE (*)(char * __asm("a6"), struct Task * __asm("a1"), LONG __asm("d0"))) \
  (_SetTaskPri__bn - 300))(_SetTaskPri__bn, _SetTaskPri_task, _SetTaskPri_priority); \
});})

#define SetSignal(newSignals, signalSet) ({ \
  ULONG _SetSignal_newSignals = (newSignals); \
  ULONG _SetSignal_signalSet = (signalSet); \
  ({ \
  register char * _SetSignal__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_SetSignal__bn - 306))(_SetSignal__bn, _SetSignal_newSignals, _SetSignal_signalSet); \
});})

#define SetExcept(newSignals, signalSet) ({ \
  ULONG _SetExcept_newSignals = (newSignals); \
  ULONG _SetExcept_signalSet = (signalSet); \
  ({ \
  register char * _SetExcept__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_SetExcept__bn - 312))(_SetExcept__bn, _SetExcept_newSignals, _SetExcept_signalSet); \
});})

#define Wait(signalSet) ({ \
  ULONG _Wait_signalSet = (signalSet); \
  ({ \
  register char * _Wait__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_Wait__bn - 318))(_Wait__bn, _Wait_signalSet); \
});})

#define Signal(task, signalSet) ({ \
  struct Task * _Signal_task = (task); \
  ULONG _Signal_signalSet = (signalSet); \
  ({ \
  register char * _Signal__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Task * __asm("a1"), ULONG __asm("d0"))) \
  (_Signal__bn - 324))(_Signal__bn, _Signal_task, _Signal_signalSet); \
});})

#define AllocSignal(signalNum) ({ \
  LONG _AllocSignal_signalNum = (signalNum); \
  ({ \
  register char * _AllocSignal__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((BYTE (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_AllocSignal__bn - 330))(_AllocSignal__bn, _AllocSignal_signalNum); \
});})

#define FreeSignal(signalNum) ({ \
  LONG _FreeSignal_signalNum = (signalNum); \
  ({ \
  register char * _FreeSignal__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_FreeSignal__bn - 336))(_FreeSignal__bn, _FreeSignal_signalNum); \
});})

#define AllocTrap(trapNum) ({ \
  LONG _AllocTrap_trapNum = (trapNum); \
  ({ \
  register char * _AllocTrap__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_AllocTrap__bn - 342))(_AllocTrap__bn, _AllocTrap_trapNum); \
});})

#define FreeTrap(trapNum) ({ \
  LONG _FreeTrap_trapNum = (trapNum); \
  ({ \
  register char * _FreeTrap__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_FreeTrap__bn - 348))(_FreeTrap__bn, _FreeTrap_trapNum); \
});})

#define AddPort(port) ({ \
  struct MsgPort * _AddPort_port = (port); \
  ({ \
  register char * _AddPort__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MsgPort * __asm("a1"))) \
  (_AddPort__bn - 354))(_AddPort__bn, _AddPort_port); \
});})

#define RemPort(port) ({ \
  struct MsgPort * _RemPort_port = (port); \
  ({ \
  register char * _RemPort__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MsgPort * __asm("a1"))) \
  (_RemPort__bn - 360))(_RemPort__bn, _RemPort_port); \
});})

#define PutMsg(port, message) ({ \
  struct MsgPort * _PutMsg_port = (port); \
  struct Message * _PutMsg_message = (message); \
  ({ \
  register char * _PutMsg__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MsgPort * __asm("a0"), struct Message * __asm("a1"))) \
  (_PutMsg__bn - 366))(_PutMsg__bn, _PutMsg_port, _PutMsg_message); \
});})

#define GetMsg(port) ({ \
  struct MsgPort * _GetMsg_port = (port); \
  ({ \
  register char * _GetMsg__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Message * (*)(char * __asm("a6"), struct MsgPort * __asm("a0"))) \
  (_GetMsg__bn - 372))(_GetMsg__bn, _GetMsg_port); \
});})

#define ReplyMsg(message) ({ \
  struct Message * _ReplyMsg_message = (message); \
  ({ \
  register char * _ReplyMsg__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Message * __asm("a1"))) \
  (_ReplyMsg__bn - 378))(_ReplyMsg__bn, _ReplyMsg_message); \
});})

#define WaitPort(port) ({ \
  struct MsgPort * _WaitPort_port = (port); \
  ({ \
  register char * _WaitPort__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Message * (*)(char * __asm("a6"), struct MsgPort * __asm("a0"))) \
  (_WaitPort__bn - 384))(_WaitPort__bn, _WaitPort_port); \
});})

#define FindPort(name) ({ \
  CONST_STRPTR _FindPort_name = (name); \
  ({ \
  register char * _FindPort__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"))) \
  (_FindPort__bn - 390))(_FindPort__bn, _FindPort_name); \
});})

#define AddLibrary(library) ({ \
  struct Library * _AddLibrary_library = (library); \
  ({ \
  register char * _AddLibrary__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Library * __asm("a1"))) \
  (_AddLibrary__bn - 396))(_AddLibrary__bn, _AddLibrary_library); \
});})

#define RemLibrary(library) ({ \
  struct Library * _RemLibrary_library = (library); \
  ({ \
  register char * _RemLibrary__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Library * __asm("a1"))) \
  (_RemLibrary__bn - 402))(_RemLibrary__bn, _RemLibrary_library); \
});})

#define OldOpenLibrary(libName) ({ \
  CONST_STRPTR _OldOpenLibrary_libName = (libName); \
  ({ \
  register char * _OldOpenLibrary__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Library * (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"))) \
  (_OldOpenLibrary__bn - 408))(_OldOpenLibrary__bn, _OldOpenLibrary_libName); \
});})

#define CloseLibrary(library) ({ \
  struct Library * _CloseLibrary_library = (library); \
  ({ \
  register char * _CloseLibrary__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Library * __asm("a1"))) \
  (_CloseLibrary__bn - 414))(_CloseLibrary__bn, _CloseLibrary_library); \
});})

#define SetFunction(library, funcOffset, newFunction) ({ \
  struct Library * _SetFunction_library = (library); \
  LONG _SetFunction_funcOffset = (funcOffset); \
  ULONG (*_SetFunction_newFunction)() = (newFunction); \
  ({ \
  register char * _SetFunction__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct Library * __asm("a1"), LONG __asm("a0"), ULONG (*)() __asm("d0"))) \
  (_SetFunction__bn - 420))(_SetFunction__bn, _SetFunction_library, _SetFunction_funcOffset, _SetFunction_newFunction); \
});})

#define SumLibrary(library) ({ \
  struct Library * _SumLibrary_library = (library); \
  ({ \
  register char * _SumLibrary__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Library * __asm("a1"))) \
  (_SumLibrary__bn - 426))(_SumLibrary__bn, _SumLibrary_library); \
});})

#define AddDevice(device) ({ \
  struct Device * _AddDevice_device = (device); \
  ({ \
  register char * _AddDevice__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Device * __asm("a1"))) \
  (_AddDevice__bn - 432))(_AddDevice__bn, _AddDevice_device); \
});})

#define RemDevice(device) ({ \
  struct Device * _RemDevice_device = (device); \
  ({ \
  register char * _RemDevice__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Device * __asm("a1"))) \
  (_RemDevice__bn - 438))(_RemDevice__bn, _RemDevice_device); \
});})

#define OpenDevice(devName, unit, ioRequest, flags) ({ \
  CONST_STRPTR _OpenDevice_devName = (devName); \
  ULONG _OpenDevice_unit = (unit); \
  struct IORequest * _OpenDevice_ioRequest = (ioRequest); \
  ULONG _OpenDevice_flags = (flags); \
  ({ \
  register char * _OpenDevice__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((BYTE (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), ULONG __asm("d0"), struct IORequest * __asm("a1"), ULONG __asm("d1"))) \
  (_OpenDevice__bn - 444))(_OpenDevice__bn, _OpenDevice_devName, _OpenDevice_unit, _OpenDevice_ioRequest, _OpenDevice_flags); \
});})

#define CloseDevice(ioRequest) ({ \
  struct IORequest * _CloseDevice_ioRequest = (ioRequest); \
  ({ \
  register char * _CloseDevice__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IORequest * __asm("a1"))) \
  (_CloseDevice__bn - 450))(_CloseDevice__bn, _CloseDevice_ioRequest); \
});})

#define DoIO(ioRequest) ({ \
  struct IORequest * _DoIO_ioRequest = (ioRequest); \
  ({ \
  register char * _DoIO__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((BYTE (*)(char * __asm("a6"), struct IORequest * __asm("a1"))) \
  (_DoIO__bn - 456))(_DoIO__bn, _DoIO_ioRequest); \
});})

#define SendIO(ioRequest) ({ \
  struct IORequest * _SendIO_ioRequest = (ioRequest); \
  ({ \
  register char * _SendIO__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IORequest * __asm("a1"))) \
  (_SendIO__bn - 462))(_SendIO__bn, _SendIO_ioRequest); \
});})

#define CheckIO(ioRequest) ({ \
  CONST struct IORequest * _CheckIO_ioRequest = (ioRequest); \
  ({ \
  register char * _CheckIO__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct IORequest * (*)(char * __asm("a6"), CONST struct IORequest * __asm("a1"))) \
  (_CheckIO__bn - 468))(_CheckIO__bn, _CheckIO_ioRequest); \
});})

#define WaitIO(ioRequest) ({ \
  struct IORequest * _WaitIO_ioRequest = (ioRequest); \
  ({ \
  register char * _WaitIO__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((BYTE (*)(char * __asm("a6"), struct IORequest * __asm("a1"))) \
  (_WaitIO__bn - 474))(_WaitIO__bn, _WaitIO_ioRequest); \
});})

#define AbortIO(ioRequest) ({ \
  struct IORequest * _AbortIO_ioRequest = (ioRequest); \
  ({ \
  register char * _AbortIO__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct IORequest * __asm("a1"))) \
  (_AbortIO__bn - 480))(_AbortIO__bn, _AbortIO_ioRequest); \
});})

#define AddResource(resource) ({ \
  APTR _AddResource_resource = (resource); \
  ({ \
  register char * _AddResource__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"))) \
  (_AddResource__bn - 486))(_AddResource__bn, _AddResource_resource); \
});})

#define RemResource(resource) ({ \
  APTR _RemResource_resource = (resource); \
  ({ \
  register char * _RemResource__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"))) \
  (_RemResource__bn - 492))(_RemResource__bn, _RemResource_resource); \
});})

#define OpenResource(resName) ({ \
  CONST_STRPTR _OpenResource_resName = (resName); \
  ({ \
  register char * _OpenResource__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"))) \
  (_OpenResource__bn - 498))(_OpenResource__bn, _OpenResource_resName); \
});})

#define RawDoFmt(formatString, dataStream, putChProc, putChData) ({ \
  CONST_STRPTR _RawDoFmt_formatString = (formatString); \
  APTR _RawDoFmt_dataStream = (dataStream); \
  VOID (*_RawDoFmt_putChProc)() = (putChProc); \
  APTR _RawDoFmt_putChData = (putChData); \
  ({ \
  register char * _RawDoFmt__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"), APTR __asm("a1"), VOID (*)() __asm("a2"), APTR __asm("a3"))) \
  (_RawDoFmt__bn - 522))(_RawDoFmt__bn, _RawDoFmt_formatString, _RawDoFmt_dataStream, _RawDoFmt_putChProc, _RawDoFmt_putChData); \
});})

#define GetCC() ({ \
  register char * _GetCC__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_GetCC__bn - 528))(_GetCC__bn); \
})

#define TypeOfMem(address) ({ \
  CONST_APTR _TypeOfMem_address = (address); \
  ({ \
  register char * _TypeOfMem__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST_APTR __asm("a1"))) \
  (_TypeOfMem__bn - 534))(_TypeOfMem__bn, _TypeOfMem_address); \
});})

#define Procure(sigSem, bidMsg) ({ \
  struct SignalSemaphore * _Procure_sigSem = (sigSem); \
  struct SemaphoreMessage * _Procure_bidMsg = (bidMsg); \
  ({ \
  register char * _Procure__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"), struct SemaphoreMessage * __asm("a1"))) \
  (_Procure__bn - 540))(_Procure__bn, _Procure_sigSem, _Procure_bidMsg); \
});})

#define Vacate(sigSem, bidMsg) ({ \
  struct SignalSemaphore * _Vacate_sigSem = (sigSem); \
  struct SemaphoreMessage * _Vacate_bidMsg = (bidMsg); \
  ({ \
  register char * _Vacate__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"), struct SemaphoreMessage * __asm("a1"))) \
  (_Vacate__bn - 546))(_Vacate__bn, _Vacate_sigSem, _Vacate_bidMsg); \
});})

#define OpenLibrary(libName, version) ({ \
  CONST_STRPTR _OpenLibrary_libName = (libName); \
  ULONG _OpenLibrary_version = (version); \
  ({ \
  register char * _OpenLibrary__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct Library * (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"), ULONG __asm("d0"))) \
  (_OpenLibrary__bn - 552))(_OpenLibrary__bn, _OpenLibrary_libName, _OpenLibrary_version); \
});})

#define InitSemaphore(sigSem) ({ \
  struct SignalSemaphore * _InitSemaphore_sigSem = (sigSem); \
  ({ \
  register char * _InitSemaphore__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"))) \
  (_InitSemaphore__bn - 558))(_InitSemaphore__bn, _InitSemaphore_sigSem); \
});})

#define ObtainSemaphore(sigSem) ({ \
  struct SignalSemaphore * _ObtainSemaphore_sigSem = (sigSem); \
  ({ \
  register char * _ObtainSemaphore__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"))) \
  (_ObtainSemaphore__bn - 564))(_ObtainSemaphore__bn, _ObtainSemaphore_sigSem); \
});})

#define ReleaseSemaphore(sigSem) ({ \
  struct SignalSemaphore * _ReleaseSemaphore_sigSem = (sigSem); \
  ({ \
  register char * _ReleaseSemaphore__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"))) \
  (_ReleaseSemaphore__bn - 570))(_ReleaseSemaphore__bn, _ReleaseSemaphore_sigSem); \
});})

#define AttemptSemaphore(sigSem) ({ \
  struct SignalSemaphore * _AttemptSemaphore_sigSem = (sigSem); \
  ({ \
  register char * _AttemptSemaphore__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"))) \
  (_AttemptSemaphore__bn - 576))(_AttemptSemaphore__bn, _AttemptSemaphore_sigSem); \
});})

#define ObtainSemaphoreList(sigSem) ({ \
  struct List * _ObtainSemaphoreList_sigSem = (sigSem); \
  ({ \
  register char * _ObtainSemaphoreList__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_ObtainSemaphoreList__bn - 582))(_ObtainSemaphoreList__bn, _ObtainSemaphoreList_sigSem); \
});})

#define ReleaseSemaphoreList(sigSem) ({ \
  struct List * _ReleaseSemaphoreList_sigSem = (sigSem); \
  ({ \
  register char * _ReleaseSemaphoreList__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct List * __asm("a0"))) \
  (_ReleaseSemaphoreList__bn - 588))(_ReleaseSemaphoreList__bn, _ReleaseSemaphoreList_sigSem); \
});})

#define FindSemaphore(name) ({ \
  CONST_STRPTR _FindSemaphore_name = (name); \
  ({ \
  register char * _FindSemaphore__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct SignalSemaphore * (*)(char * __asm("a6"), CONST_STRPTR __asm("a1"))) \
  (_FindSemaphore__bn - 594))(_FindSemaphore__bn, _FindSemaphore_name); \
});})

#define AddSemaphore(sigSem) ({ \
  struct SignalSemaphore * _AddSemaphore_sigSem = (sigSem); \
  ({ \
  register char * _AddSemaphore__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a1"))) \
  (_AddSemaphore__bn - 600))(_AddSemaphore__bn, _AddSemaphore_sigSem); \
});})

#define RemSemaphore(sigSem) ({ \
  struct SignalSemaphore * _RemSemaphore_sigSem = (sigSem); \
  ({ \
  register char * _RemSemaphore__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a1"))) \
  (_RemSemaphore__bn - 606))(_RemSemaphore__bn, _RemSemaphore_sigSem); \
});})

#define SumKickData() ({ \
  register char * _SumKickData__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_SumKickData__bn - 612))(_SumKickData__bn); \
})

#define AddMemList(size, attributes, pri, base, name) ({ \
  ULONG _AddMemList_size = (size); \
  ULONG _AddMemList_attributes = (attributes); \
  LONG _AddMemList_pri = (pri); \
  APTR _AddMemList_base = (base); \
  STRPTR _AddMemList_name = (name); \
  ({ \
  register char * _AddMemList__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), LONG __asm("d2"), APTR __asm("a0"), STRPTR __asm("a1"))) \
  (_AddMemList__bn - 618))(_AddMemList__bn, _AddMemList_size, _AddMemList_attributes, _AddMemList_pri, _AddMemList_base, _AddMemList_name); \
});})

#define CopyMem(source, dest, size) ({ \
  CONST_APTR _CopyMem_source = (source); \
  APTR _CopyMem_dest = (dest); \
  ULONG _CopyMem_size = (size); \
  ({ \
  register char * _CopyMem__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_APTR __asm("a0"), APTR __asm("a1"), ULONG __asm("d0"))) \
  (_CopyMem__bn - 624))(_CopyMem__bn, _CopyMem_source, _CopyMem_dest, _CopyMem_size); \
});})

#define CopyMemQuick(source, dest, size) ({ \
  CONST_APTR _CopyMemQuick_source = (source); \
  APTR _CopyMemQuick_dest = (dest); \
  ULONG _CopyMemQuick_size = (size); \
  ({ \
  register char * _CopyMemQuick__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_APTR __asm("a0"), APTR __asm("a1"), ULONG __asm("d0"))) \
  (_CopyMemQuick__bn - 630))(_CopyMemQuick__bn, _CopyMemQuick_source, _CopyMemQuick_dest, _CopyMemQuick_size); \
});})

#define CacheClearU() ({ \
  register char * _CacheClearU__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_CacheClearU__bn - 636))(_CacheClearU__bn); \
})

#define CacheClearE(address, length, caches) ({ \
  APTR _CacheClearE_address = (address); \
  ULONG _CacheClearE_length = (length); \
  ULONG _CacheClearE_caches = (caches); \
  ({ \
  register char * _CacheClearE__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_CacheClearE__bn - 642))(_CacheClearE__bn, _CacheClearE_address, _CacheClearE_length, _CacheClearE_caches); \
});})

#define CacheControl(cacheBits, cacheMask) ({ \
  ULONG _CacheControl_cacheBits = (cacheBits); \
  ULONG _CacheControl_cacheMask = (cacheMask); \
  ({ \
  register char * _CacheControl__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_CacheControl__bn - 648))(_CacheControl__bn, _CacheControl_cacheBits, _CacheControl_cacheMask); \
});})

#define CreateIORequest(port, size) ({ \
  struct MsgPort * _CreateIORequest_port = (port); \
  ULONG _CreateIORequest_size = (size); \
  ({ \
  register char * _CreateIORequest__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct MsgPort * __asm("a0"), ULONG __asm("d0"))) \
  (_CreateIORequest__bn - 654))(_CreateIORequest__bn, _CreateIORequest_port, _CreateIORequest_size); \
});})

#define DeleteIORequest(iorequest) ({ \
  APTR _DeleteIORequest_iorequest = (iorequest); \
  ({ \
  register char * _DeleteIORequest__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_DeleteIORequest__bn - 660))(_DeleteIORequest__bn, _DeleteIORequest_iorequest); \
});})

#define CreateMsgPort() ({ \
  register char * _CreateMsgPort__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((struct MsgPort * (*)(char * __asm("a6"))) \
  (_CreateMsgPort__bn - 666))(_CreateMsgPort__bn); \
})

#define DeleteMsgPort(port) ({ \
  struct MsgPort * _DeleteMsgPort_port = (port); \
  ({ \
  register char * _DeleteMsgPort__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MsgPort * __asm("a0"))) \
  (_DeleteMsgPort__bn - 672))(_DeleteMsgPort__bn, _DeleteMsgPort_port); \
});})

#define ObtainSemaphoreShared(sigSem) ({ \
  struct SignalSemaphore * _ObtainSemaphoreShared_sigSem = (sigSem); \
  ({ \
  register char * _ObtainSemaphoreShared__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"))) \
  (_ObtainSemaphoreShared__bn - 678))(_ObtainSemaphoreShared__bn, _ObtainSemaphoreShared_sigSem); \
});})

#define AllocVec(byteSize, requirements) ({ \
  ULONG _AllocVec_byteSize = (byteSize); \
  ULONG _AllocVec_requirements = (requirements); \
  ({ \
  register char * _AllocVec__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_AllocVec__bn - 684))(_AllocVec__bn, _AllocVec_byteSize, _AllocVec_requirements); \
});})

#define FreeVec(memoryBlock) ({ \
  APTR _FreeVec_memoryBlock = (memoryBlock); \
  ({ \
  register char * _FreeVec__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"))) \
  (_FreeVec__bn - 690))(_FreeVec__bn, _FreeVec_memoryBlock); \
});})

#define CreatePool(requirements, puddleSize, threshSize) ({ \
  ULONG _CreatePool_requirements = (requirements); \
  ULONG _CreatePool_puddleSize = (puddleSize); \
  ULONG _CreatePool_threshSize = (threshSize); \
  ({ \
  register char * _CreatePool__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), ULONG __asm("d2"))) \
  (_CreatePool__bn - 696))(_CreatePool__bn, _CreatePool_requirements, _CreatePool_puddleSize, _CreatePool_threshSize); \
});})

#define DeletePool(poolHeader) ({ \
  APTR _DeletePool_poolHeader = (poolHeader); \
  ({ \
  register char * _DeletePool__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_DeletePool__bn - 702))(_DeletePool__bn, _DeletePool_poolHeader); \
});})

#define AllocPooled(poolHeader, memSize) ({ \
  APTR _AllocPooled_poolHeader = (poolHeader); \
  ULONG _AllocPooled_memSize = (memSize); \
  ({ \
  register char * _AllocPooled__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), APTR __asm("a0"), ULONG __asm("d0"))) \
  (_AllocPooled__bn - 708))(_AllocPooled__bn, _AllocPooled_poolHeader, _AllocPooled_memSize); \
});})

#define FreePooled(poolHeader, memory, memSize) ({ \
  APTR _FreePooled_poolHeader = (poolHeader); \
  APTR _FreePooled_memory = (memory); \
  ULONG _FreePooled_memSize = (memSize); \
  ({ \
  register char * _FreePooled__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"), APTR __asm("a1"), ULONG __asm("d0"))) \
  (_FreePooled__bn - 714))(_FreePooled__bn, _FreePooled_poolHeader, _FreePooled_memory, _FreePooled_memSize); \
});})

#define AttemptSemaphoreShared(sigSem) ({ \
  struct SignalSemaphore * _AttemptSemaphoreShared_sigSem = (sigSem); \
  ({ \
  register char * _AttemptSemaphoreShared__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct SignalSemaphore * __asm("a0"))) \
  (_AttemptSemaphoreShared__bn - 720))(_AttemptSemaphoreShared__bn, _AttemptSemaphoreShared_sigSem); \
});})

#define ColdReboot() ({ \
  register char * _ColdReboot__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_ColdReboot__bn - 726))(_ColdReboot__bn); \
})

#define StackSwap(newStack) ({ \
  struct StackSwapStruct * _StackSwap_newStack = (newStack); \
  ({ \
  register char * _StackSwap__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct StackSwapStruct * __asm("a0"))) \
  (_StackSwap__bn - 732))(_StackSwap__bn, _StackSwap_newStack); \
});})

#define CachePreDMA(address, length, flags) ({ \
  CONST_APTR _CachePreDMA_address = (address); \
  ULONG * _CachePreDMA_length = (length); \
  ULONG _CachePreDMA_flags = (flags); \
  ({ \
  register char * _CachePreDMA__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), CONST_APTR __asm("a0"), ULONG * __asm("a1"), ULONG __asm("d0"))) \
  (_CachePreDMA__bn - 762))(_CachePreDMA__bn, _CachePreDMA_address, _CachePreDMA_length, _CachePreDMA_flags); \
});})

#define CachePostDMA(address, length, flags) ({ \
  CONST_APTR _CachePostDMA_address = (address); \
  ULONG * _CachePostDMA_length = (length); \
  ULONG _CachePostDMA_flags = (flags); \
  ({ \
  register char * _CachePostDMA__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_APTR __asm("a0"), ULONG * __asm("a1"), ULONG __asm("d0"))) \
  (_CachePostDMA__bn - 768))(_CachePostDMA__bn, _CachePostDMA_address, _CachePostDMA_length, _CachePostDMA_flags); \
});})

#define AddMemHandler(memhand) ({ \
  struct Interrupt * _AddMemHandler_memhand = (memhand); \
  ({ \
  register char * _AddMemHandler__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Interrupt * __asm("a1"))) \
  (_AddMemHandler__bn - 774))(_AddMemHandler__bn, _AddMemHandler_memhand); \
});})

#define RemMemHandler(memhand) ({ \
  struct Interrupt * _RemMemHandler_memhand = (memhand); \
  ({ \
  register char * _RemMemHandler__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Interrupt * __asm("a1"))) \
  (_RemMemHandler__bn - 780))(_RemMemHandler__bn, _RemMemHandler_memhand); \
});})

#define ObtainQuickVector(interruptCode) ({ \
  APTR _ObtainQuickVector_interruptCode = (interruptCode); \
  ({ \
  register char * _ObtainQuickVector__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_ObtainQuickVector__bn - 786))(_ObtainQuickVector__bn, _ObtainQuickVector_interruptCode); \
});})

#define NewMinList(minlist) ({ \
  struct MinList * _NewMinList_minlist = (minlist); \
  ({ \
  register char * _NewMinList__bn __asm("a6") = (char *) (EXEC_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct MinList * __asm("a0"))) \
  (_NewMinList__bn - 828))(_NewMinList__bn, _NewMinList_minlist); \
});})

#endif /*  _INLINE_EXEC_H  */
