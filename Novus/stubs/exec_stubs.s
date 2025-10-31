; Generated from SFD file by Novus SFD Parser
; Library: exec.library
; Base: _SysBase
; Each function is in its own section for dead code elimination

	xref	_SysBase

	section	_Supervisor_stub,code

; ULONG Supervisor(ULONG (*userFunction)() userFunction)
	xdef	_Supervisor
_Supervisor:
	movea.l	4(sp),a5
	movea.l	_SysBase,a6
	jsr	-30(a6)
	rts

	section	_InitCode_stub,code

; VOID InitCode(ULONG startClass, ULONG version)
	xdef	_InitCode
_InitCode:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-72(a6)
	rts

	section	_InitStruct_stub,code

; VOID InitStruct(const APTR initTable, APTR memory, ULONG size)
	xdef	_InitStruct
_InitStruct:
	movea.l	4(sp),a1
	movea.l	8(sp),a2
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-78(a6)
	rts

	section	_MakeLibrary_stub,code

; struct Library * MakeLibrary(const APTR funcInit, const APTR structInit, ULONG (*libInit)() libInit, ULONG dataSize, ULONG segList)
	xdef	_MakeLibrary
_MakeLibrary:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_SysBase,a6
	jsr	-84(a6)
	rts

	section	_MakeFunctions_stub,code

; VOID MakeFunctions(APTR target, const APTR functionArray, const APTR funcDispBase)
	xdef	_MakeFunctions
_MakeFunctions:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_SysBase,a6
	jsr	-90(a6)
	rts

	section	_FindResident_stub,code

; struct Resident * FindResident(CONST_STRPTR name)
	xdef	_FindResident
_FindResident:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-96(a6)
	rts

	section	_InitResident_stub,code

; APTR InitResident(const struct Resident * resident, ULONG segList)
	xdef	_InitResident
_InitResident:
	movea.l	4(sp),a1
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-102(a6)
	rts

	section	_Alert_stub,code

; VOID Alert(ULONG alertNum)
	xdef	_Alert
_Alert:
	move.l	4(sp),d7
	movea.l	_SysBase,a6
	jsr	-108(a6)
	rts

	section	_Debug_stub,code

; VOID Debug(ULONG flags)
	xdef	_Debug
_Debug:
	move.l	4(sp),d0
	movea.l	_SysBase,a6
	jsr	-114(a6)
	rts

	section	_Disable_stub,code

; VOID Disable()
	xdef	_Disable
_Disable:
	movea.l	_SysBase,a6
	jsr	-120(a6)
	rts

	section	_Enable_stub,code

; VOID Enable()
	xdef	_Enable
_Enable:
	movea.l	_SysBase,a6
	jsr	-126(a6)
	rts

	section	_Forbid_stub,code

; VOID Forbid()
	xdef	_Forbid
_Forbid:
	movea.l	_SysBase,a6
	jsr	-132(a6)
	rts

	section	_Permit_stub,code

; VOID Permit()
	xdef	_Permit
_Permit:
	movea.l	_SysBase,a6
	jsr	-138(a6)
	rts

	section	_SetSR_stub,code

; ULONG SetSR(ULONG newSR, ULONG mask)
	xdef	_SetSR
_SetSR:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-144(a6)
	rts

	section	_SuperState_stub,code

; APTR SuperState()
	xdef	_SuperState
_SuperState:
	movea.l	_SysBase,a6
	jsr	-150(a6)
	rts

	section	_UserState_stub,code

; VOID UserState(APTR sysStack)
	xdef	_UserState
_UserState:
	move.l	4(sp),d0
	movea.l	_SysBase,a6
	jsr	-156(a6)
	rts

	section	_SetIntVector_stub,code

; struct Interrupt * SetIntVector(LONG intNumber, const struct Interrupt * interrupt)
	xdef	_SetIntVector
_SetIntVector:
	move.l	4(sp),d0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-162(a6)
	rts

	section	_AddIntServer_stub,code

; VOID AddIntServer(LONG intNumber, struct Interrupt * interrupt)
	xdef	_AddIntServer
_AddIntServer:
	move.l	4(sp),d0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-168(a6)
	rts

	section	_RemIntServer_stub,code

; VOID RemIntServer(LONG intNumber, struct Interrupt * interrupt)
	xdef	_RemIntServer
_RemIntServer:
	move.l	4(sp),d0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-174(a6)
	rts

	section	_Cause_stub,code

; VOID Cause(struct Interrupt * interrupt)
	xdef	_Cause
_Cause:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-180(a6)
	rts

	section	_Allocate_stub,code

; APTR Allocate(struct MemHeader * freeList, ULONG byteSize)
	xdef	_Allocate
_Allocate:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_SysBase,a6
	jsr	-186(a6)
	rts

	section	_Deallocate_stub,code

; VOID Deallocate(struct MemHeader * freeList, APTR memoryBlock, ULONG byteSize)
	xdef	_Deallocate
_Deallocate:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-192(a6)
	rts

	section	_AllocMem_stub,code

; APTR AllocMem(ULONG byteSize, ULONG requirements)
	xdef	_AllocMem
_AllocMem:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-198(a6)
	rts

	section	_AllocAbs_stub,code

; APTR AllocAbs(ULONG byteSize, APTR location)
	xdef	_AllocAbs
_AllocAbs:
	move.l	4(sp),d0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-204(a6)
	rts

	section	_FreeMem_stub,code

; VOID FreeMem(APTR memoryBlock, ULONG byteSize)
	xdef	_FreeMem
_FreeMem:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	movea.l	_SysBase,a6
	jsr	-210(a6)
	rts

	section	_AvailMem_stub,code

; ULONG AvailMem(ULONG requirements)
	xdef	_AvailMem
_AvailMem:
	move.l	4(sp),d1
	movea.l	_SysBase,a6
	jsr	-216(a6)
	rts

	section	_AllocEntry_stub,code

; struct MemList * AllocEntry(struct MemList * entry)
	xdef	_AllocEntry
_AllocEntry:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-222(a6)
	rts

	section	_FreeEntry_stub,code

; VOID FreeEntry(struct MemList * entry)
	xdef	_FreeEntry
_FreeEntry:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-228(a6)
	rts

	section	_Insert_stub,code

; VOID Insert(struct List * list, struct Node * node, struct Node * pred)
	xdef	_Insert
_Insert:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_SysBase,a6
	jsr	-234(a6)
	rts

	section	_AddHead_stub,code

; VOID AddHead(struct List * list, struct Node * node)
	xdef	_AddHead
_AddHead:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-240(a6)
	rts

	section	_AddTail_stub,code

; VOID AddTail(struct List * list, struct Node * node)
	xdef	_AddTail
_AddTail:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-246(a6)
	rts

	section	_Remove_stub,code

; VOID Remove(struct Node * node)
	xdef	_Remove
_Remove:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-252(a6)
	rts

	section	_RemHead_stub,code

; struct Node * RemHead(struct List * list)
	xdef	_RemHead
_RemHead:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-258(a6)
	rts

	section	_RemTail_stub,code

; struct Node * RemTail(struct List * list)
	xdef	_RemTail
_RemTail:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-264(a6)
	rts

	section	_Enqueue_stub,code

; VOID Enqueue(struct List * list, struct Node * node)
	xdef	_Enqueue
_Enqueue:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-270(a6)
	rts

	section	_FindName_stub,code

; struct Node * FindName(struct List * list, CONST_STRPTR name)
	xdef	_FindName
_FindName:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-276(a6)
	rts

	section	_AddTask_stub,code

; APTR AddTask(struct Task * task, const APTR initPC, const APTR finalPC)
	xdef	_AddTask
_AddTask:
	movea.l	4(sp),a1
	movea.l	8(sp),a2
	movea.l	12(sp),a3
	movea.l	_SysBase,a6
	jsr	-282(a6)
	rts

	section	_RemTask_stub,code

; VOID RemTask(struct Task * task)
	xdef	_RemTask
_RemTask:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-288(a6)
	rts

	section	_FindTask_stub,code

; struct Task * FindTask(CONST_STRPTR name)
	xdef	_FindTask
_FindTask:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-294(a6)
	rts

	section	_SetTaskPri_stub,code

; BYTE SetTaskPri(struct Task * task, LONG priority)
	xdef	_SetTaskPri
_SetTaskPri:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	movea.l	_SysBase,a6
	jsr	-300(a6)
	rts

	section	_SetSignal_stub,code

; ULONG SetSignal(ULONG newSignals, ULONG signalSet)
	xdef	_SetSignal
_SetSignal:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-306(a6)
	rts

	section	_SetExcept_stub,code

; ULONG SetExcept(ULONG newSignals, ULONG signalSet)
	xdef	_SetExcept
_SetExcept:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-312(a6)
	rts

	section	_Wait_stub,code

; ULONG Wait(ULONG signalSet)
	xdef	_Wait
_Wait:
	move.l	4(sp),d0
	movea.l	_SysBase,a6
	jsr	-318(a6)
	rts

	section	_Signal_stub,code

; VOID Signal(struct Task * task, ULONG signalSet)
	xdef	_Signal
_Signal:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	movea.l	_SysBase,a6
	jsr	-324(a6)
	rts

	section	_AllocSignal_stub,code

; BYTE AllocSignal(BYTE signalNum)
	xdef	_AllocSignal
_AllocSignal:
	move.l	4(sp),d0
	movea.l	_SysBase,a6
	jsr	-330(a6)
	rts

	section	_FreeSignal_stub,code

; VOID FreeSignal(BYTE signalNum)
	xdef	_FreeSignal
_FreeSignal:
	move.l	4(sp),d0
	movea.l	_SysBase,a6
	jsr	-336(a6)
	rts

	section	_AllocTrap_stub,code

; LONG AllocTrap(LONG trapNum)
	xdef	_AllocTrap
_AllocTrap:
	move.l	4(sp),d0
	movea.l	_SysBase,a6
	jsr	-342(a6)
	rts

	section	_FreeTrap_stub,code

; VOID FreeTrap(LONG trapNum)
	xdef	_FreeTrap
_FreeTrap:
	move.l	4(sp),d0
	movea.l	_SysBase,a6
	jsr	-348(a6)
	rts

	section	_AddPort_stub,code

; VOID AddPort(struct MsgPort * port)
	xdef	_AddPort
_AddPort:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-354(a6)
	rts

	section	_RemPort_stub,code

; VOID RemPort(struct MsgPort * port)
	xdef	_RemPort
_RemPort:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-360(a6)
	rts

	section	_PutMsg_stub,code

; VOID PutMsg(struct MsgPort * port, struct Message * message)
	xdef	_PutMsg
_PutMsg:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-366(a6)
	rts

	section	_GetMsg_stub,code

; struct Message * GetMsg(struct MsgPort * port)
	xdef	_GetMsg
_GetMsg:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-372(a6)
	rts

	section	_ReplyMsg_stub,code

; VOID ReplyMsg(struct Message * message)
	xdef	_ReplyMsg
_ReplyMsg:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-378(a6)
	rts

	section	_WaitPort_stub,code

; struct Message * WaitPort(struct MsgPort * port)
	xdef	_WaitPort
_WaitPort:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-384(a6)
	rts

	section	_FindPort_stub,code

; struct MsgPort * FindPort(CONST_STRPTR name)
	xdef	_FindPort
_FindPort:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-390(a6)
	rts

	section	_AddLibrary_stub,code

; VOID AddLibrary(struct Library * library)
	xdef	_AddLibrary
_AddLibrary:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-396(a6)
	rts

	section	_RemLibrary_stub,code

; VOID RemLibrary(struct Library * library)
	xdef	_RemLibrary
_RemLibrary:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-402(a6)
	rts

	section	_OldOpenLibrary_stub,code

; struct Library * OldOpenLibrary(CONST_STRPTR libName)
	xdef	_OldOpenLibrary
_OldOpenLibrary:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-408(a6)
	rts

	section	_CloseLibrary_stub,code

; VOID CloseLibrary(struct Library * library)
	xdef	_CloseLibrary
_CloseLibrary:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-414(a6)
	rts

	section	_SetFunction_stub,code

; APTR SetFunction(struct Library * library, LONG funcOffset, ULONG (*newFunction)() newFunction)
	xdef	_SetFunction
_SetFunction:
	movea.l	4(sp),a1
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-420(a6)
	rts

	section	_SumLibrary_stub,code

; VOID SumLibrary(struct Library * library)
	xdef	_SumLibrary
_SumLibrary:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-426(a6)
	rts

	section	_AddDevice_stub,code

; VOID AddDevice(struct Device * device)
	xdef	_AddDevice
_AddDevice:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-432(a6)
	rts

	section	_RemDevice_stub,code

; VOID RemDevice(struct Device * device)
	xdef	_RemDevice
_RemDevice:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-438(a6)
	rts

	section	_OpenDevice_stub,code

; BYTE OpenDevice(CONST_STRPTR devName, ULONG unit, struct IORequest * ioRequest, ULONG flags)
	xdef	_OpenDevice
_OpenDevice:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	move.l	16(sp),d1
	movea.l	_SysBase,a6
	jsr	-444(a6)
	rts

	section	_CloseDevice_stub,code

; VOID CloseDevice(struct IORequest * ioRequest)
	xdef	_CloseDevice
_CloseDevice:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-450(a6)
	rts

	section	_DoIO_stub,code

; BYTE DoIO(struct IORequest * ioRequest)
	xdef	_DoIO
_DoIO:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-456(a6)
	rts

	section	_SendIO_stub,code

; VOID SendIO(struct IORequest * ioRequest)
	xdef	_SendIO
_SendIO:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-462(a6)
	rts

	section	_CheckIO_stub,code

; struct IORequest * CheckIO(struct IORequest * ioRequest)
	xdef	_CheckIO
_CheckIO:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-468(a6)
	rts

	section	_WaitIO_stub,code

; BYTE WaitIO(struct IORequest * ioRequest)
	xdef	_WaitIO
_WaitIO:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-474(a6)
	rts

	section	_AbortIO_stub,code

; VOID AbortIO(struct IORequest * ioRequest)
	xdef	_AbortIO
_AbortIO:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-480(a6)
	rts

	section	_AddResource_stub,code

; VOID AddResource(APTR resource)
	xdef	_AddResource
_AddResource:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-486(a6)
	rts

	section	_RemResource_stub,code

; VOID RemResource(APTR resource)
	xdef	_RemResource
_RemResource:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-492(a6)
	rts

	section	_OpenResource_stub,code

; APTR OpenResource(CONST_STRPTR resName)
	xdef	_OpenResource
_OpenResource:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-498(a6)
	rts

	section	_RawDoFmt_stub,code

; APTR RawDoFmt(CONST_STRPTR formatString, const APTR dataStream, VOID (*putChProc)() putChProc, APTR putChData)
	xdef	_RawDoFmt
_RawDoFmt:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_SysBase,a6
	jsr	-522(a6)
	rts

	section	_GetCC_stub,code

; ULONG GetCC()
	xdef	_GetCC
_GetCC:
	movea.l	_SysBase,a6
	jsr	-528(a6)
	rts

	section	_TypeOfMem_stub,code

; ULONG TypeOfMem(const APTR address)
	xdef	_TypeOfMem
_TypeOfMem:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-534(a6)
	rts

	section	_Procure_stub,code

; ULONG Procure(struct SignalSemaphore * sigSem, struct SemaphoreMessage * bidMsg)
	xdef	_Procure
_Procure:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-540(a6)
	rts

	section	_Vacate_stub,code

; VOID Vacate(struct SignalSemaphore * sigSem, struct SemaphoreMessage * bidMsg)
	xdef	_Vacate
_Vacate:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-546(a6)
	rts

	section	_OpenLibrary_stub,code

; struct Library * OpenLibrary(CONST_STRPTR libName, ULONG version)
	xdef	_OpenLibrary
_OpenLibrary:
	movea.l	4(sp),a1
	move.l	8(sp),d0
	movea.l	_SysBase,a6
	jsr	-552(a6)
	rts

	section	_InitSemaphore_stub,code

; VOID InitSemaphore(struct SignalSemaphore * sigSem)
	xdef	_InitSemaphore
_InitSemaphore:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-558(a6)
	rts

	section	_ObtainSemaphore_stub,code

; VOID ObtainSemaphore(struct SignalSemaphore * sigSem)
	xdef	_ObtainSemaphore
_ObtainSemaphore:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-564(a6)
	rts

	section	_ReleaseSemaphore_stub,code

; VOID ReleaseSemaphore(struct SignalSemaphore * sigSem)
	xdef	_ReleaseSemaphore
_ReleaseSemaphore:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-570(a6)
	rts

	section	_AttemptSemaphore_stub,code

; ULONG AttemptSemaphore(struct SignalSemaphore * sigSem)
	xdef	_AttemptSemaphore
_AttemptSemaphore:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-576(a6)
	rts

	section	_ObtainSemaphoreList_stub,code

; VOID ObtainSemaphoreList(struct List * sigSem)
	xdef	_ObtainSemaphoreList
_ObtainSemaphoreList:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-582(a6)
	rts

	section	_ReleaseSemaphoreList_stub,code

; VOID ReleaseSemaphoreList(struct List * sigSem)
	xdef	_ReleaseSemaphoreList
_ReleaseSemaphoreList:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-588(a6)
	rts

	section	_FindSemaphore_stub,code

; struct SignalSemaphore * FindSemaphore(STRPTR name)
	xdef	_FindSemaphore
_FindSemaphore:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-594(a6)
	rts

	section	_AddSemaphore_stub,code

; VOID AddSemaphore(struct SignalSemaphore * sigSem)
	xdef	_AddSemaphore
_AddSemaphore:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-600(a6)
	rts

	section	_RemSemaphore_stub,code

; VOID RemSemaphore(struct SignalSemaphore * sigSem)
	xdef	_RemSemaphore
_RemSemaphore:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-606(a6)
	rts

	section	_SumKickData_stub,code

; ULONG SumKickData()
	xdef	_SumKickData
_SumKickData:
	movea.l	_SysBase,a6
	jsr	-612(a6)
	rts

	section	_AddMemList_stub,code

; VOID AddMemList(ULONG size, ULONG attributes, LONG pri, APTR base, CONST_STRPTR name)
	xdef	_AddMemList
_AddMemList:
	move.l	4(sp),d0
	move.l	8(sp),d1
	move.l	12(sp),d2
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	_SysBase,a6
	jsr	-618(a6)
	rts

	section	_CopyMem_stub,code

; VOID CopyMem(const APTR source, APTR dest, ULONG size)
	xdef	_CopyMem
_CopyMem:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-624(a6)
	rts

	section	_CopyMemQuick_stub,code

; VOID CopyMemQuick(const APTR source, APTR dest, ULONG size)
	xdef	_CopyMemQuick
_CopyMemQuick:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-630(a6)
	rts

	section	_CacheClearU_stub,code

; VOID CacheClearU()
	xdef	_CacheClearU
_CacheClearU:
	movea.l	_SysBase,a6
	jsr	-636(a6)
	rts

	section	_CacheClearE_stub,code

; VOID CacheClearE(APTR address, ULONG length, ULONG caches)
	xdef	_CacheClearE
_CacheClearE:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_SysBase,a6
	jsr	-642(a6)
	rts

	section	_CacheControl_stub,code

; ULONG CacheControl(ULONG cacheBits, ULONG cacheMask)
	xdef	_CacheControl
_CacheControl:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-648(a6)
	rts

	section	_CreateIORequest_stub,code

; APTR CreateIORequest(const struct MsgPort * port, ULONG size)
	xdef	_CreateIORequest
_CreateIORequest:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_SysBase,a6
	jsr	-654(a6)
	rts

	section	_DeleteIORequest_stub,code

; VOID DeleteIORequest(APTR iorequest)
	xdef	_DeleteIORequest
_DeleteIORequest:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-660(a6)
	rts

	section	_CreateMsgPort_stub,code

; struct MsgPort * CreateMsgPort()
	xdef	_CreateMsgPort
_CreateMsgPort:
	movea.l	_SysBase,a6
	jsr	-666(a6)
	rts

	section	_DeleteMsgPort_stub,code

; VOID DeleteMsgPort(struct MsgPort * port)
	xdef	_DeleteMsgPort
_DeleteMsgPort:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-672(a6)
	rts

	section	_ObtainSemaphoreShared_stub,code

; VOID ObtainSemaphoreShared(struct SignalSemaphore * sigSem)
	xdef	_ObtainSemaphoreShared
_ObtainSemaphoreShared:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-678(a6)
	rts

	section	_AllocVec_stub,code

; APTR AllocVec(ULONG byteSize, ULONG requirements)
	xdef	_AllocVec
_AllocVec:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_SysBase,a6
	jsr	-684(a6)
	rts

	section	_FreeVec_stub,code

; VOID FreeVec(APTR memoryBlock)
	xdef	_FreeVec
_FreeVec:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-690(a6)
	rts

	section	_CreatePool_stub,code

; APTR CreatePool(ULONG requirements, ULONG puddleSize, ULONG threshSize)
	xdef	_CreatePool
_CreatePool:
	move.l	4(sp),d0
	move.l	8(sp),d1
	move.l	12(sp),d2
	movea.l	_SysBase,a6
	jsr	-696(a6)
	rts

	section	_DeletePool_stub,code

; VOID DeletePool(APTR poolHeader)
	xdef	_DeletePool
_DeletePool:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-702(a6)
	rts

	section	_AllocPooled_stub,code

; APTR AllocPooled(APTR poolHeader, ULONG memSize)
	xdef	_AllocPooled
_AllocPooled:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_SysBase,a6
	jsr	-708(a6)
	rts

	section	_FreePooled_stub,code

; VOID FreePooled(APTR poolHeader, APTR memory, ULONG memSize)
	xdef	_FreePooled
_FreePooled:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-714(a6)
	rts

	section	_AttemptSemaphoreShared_stub,code

; ULONG AttemptSemaphoreShared(struct SignalSemaphore * sigSem)
	xdef	_AttemptSemaphoreShared
_AttemptSemaphoreShared:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-720(a6)
	rts

	section	_ColdReboot_stub,code

; VOID ColdReboot()
	xdef	_ColdReboot
_ColdReboot:
	movea.l	_SysBase,a6
	jsr	-726(a6)
	rts

	section	_StackSwap_stub,code

; VOID StackSwap(struct StackSwapStruct * newStack)
	xdef	_StackSwap
_StackSwap:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-732(a6)
	rts

	section	_CachePreDMA_stub,code

; APTR CachePreDMA(const APTR address, ULONG * length, ULONG flags)
	xdef	_CachePreDMA
_CachePreDMA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-762(a6)
	rts

	section	_CachePostDMA_stub,code

; VOID CachePostDMA(const APTR address, ULONG * length, ULONG flags)
	xdef	_CachePostDMA
_CachePostDMA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_SysBase,a6
	jsr	-768(a6)
	rts

	section	_AddMemHandler_stub,code

; VOID AddMemHandler(struct Interrupt * memhand)
	xdef	_AddMemHandler
_AddMemHandler:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-774(a6)
	rts

	section	_RemMemHandler_stub,code

; VOID RemMemHandler(struct Interrupt * memhand)
	xdef	_RemMemHandler
_RemMemHandler:
	movea.l	4(sp),a1
	movea.l	_SysBase,a6
	jsr	-780(a6)
	rts

	section	_ObtainQuickVector_stub,code

; ULONG ObtainQuickVector(APTR interruptCode)
	xdef	_ObtainQuickVector
_ObtainQuickVector:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-786(a6)
	rts

	section	_NewMinList_stub,code

; VOID NewMinList(struct MinList * minlist)
	xdef	_NewMinList
_NewMinList:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-828(a6)
	rts

	section	_AVL_AddNode_stub,code

; struct AVLNode * AVL_AddNode(struct AVLNode ** root, struct AVLNode * node, APTR func)
	xdef	_AVL_AddNode
_AVL_AddNode:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_SysBase,a6
	jsr	-852(a6)
	rts

	section	_AVL_RemNodeByAddress_stub,code

; struct AVLNode * AVL_RemNodeByAddress(struct AVLNode ** root, struct AVLNode * node)
	xdef	_AVL_RemNodeByAddress
_AVL_RemNodeByAddress:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SysBase,a6
	jsr	-858(a6)
	rts

	section	_AVL_RemNodeByKey_stub,code

; struct AVLNode * AVL_RemNodeByKey(struct AVLNode ** root, APTR key, APTR func)
	xdef	_AVL_RemNodeByKey
_AVL_RemNodeByKey:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_SysBase,a6
	jsr	-864(a6)
	rts

	section	_AVL_FindNode_stub,code

; struct AVLNode * AVL_FindNode(CONST struct AVLNode * root, APTR key, APTR func)
	xdef	_AVL_FindNode
_AVL_FindNode:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_SysBase,a6
	jsr	-870(a6)
	rts

	section	_AVL_FindPrevNodeByAddress_stub,code

; struct AVLNode * AVL_FindPrevNodeByAddress(CONST struct AVLNode * node)
	xdef	_AVL_FindPrevNodeByAddress
_AVL_FindPrevNodeByAddress:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-876(a6)
	rts

	section	_AVL_FindPrevNodeByKey_stub,code

; struct AVLNode * AVL_FindPrevNodeByKey(CONST struct AVLNode * root, APTR key, APTR func)
	xdef	_AVL_FindPrevNodeByKey
_AVL_FindPrevNodeByKey:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_SysBase,a6
	jsr	-882(a6)
	rts

	section	_AVL_FindNextNodeByAddress_stub,code

; struct AVLNode * AVL_FindNextNodeByAddress(CONST struct AVLNode * node)
	xdef	_AVL_FindNextNodeByAddress
_AVL_FindNextNodeByAddress:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-888(a6)
	rts

	section	_AVL_FindNextNodeByKey_stub,code

; struct AVLNode * AVL_FindNextNodeByKey(CONST struct AVLNode * root, APTR key, APTR func)
	xdef	_AVL_FindNextNodeByKey
_AVL_FindNextNodeByKey:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_SysBase,a6
	jsr	-894(a6)
	rts

	section	_AVL_FindFirstNode_stub,code

; struct AVLNode * AVL_FindFirstNode(CONST struct AVLNode * root)
	xdef	_AVL_FindFirstNode
_AVL_FindFirstNode:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-900(a6)
	rts

	section	_AVL_FindLastNode_stub,code

; struct AVLNode * AVL_FindLastNode(CONST struct AVLNode * root)
	xdef	_AVL_FindLastNode
_AVL_FindLastNode:
	movea.l	4(sp),a0
	movea.l	_SysBase,a6
	jsr	-906(a6)
	rts

