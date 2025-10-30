; exec library stubs for Novus
; Auto-generated from exec_lib.fd

	xref	_SysBase	; Provided by library_bases.s

	section	"CODE",code

; Supervisor(userFunction)
	xdef	_Supervisor
_Supervisor:
	movem.l	a5/a6,-(sp)
	move.l	12(sp),a5	; userFunction
	move.l	_SysBase,a6
	jsr	-30(a6)	; Supervisor()
	movem.l	(sp)+,a5/a6
	rts

; InitCode(startClass, version)
	xdef	_InitCode
_InitCode:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; startClass
	move.l	16(sp),d1	; version
	move.l	_SysBase,a6
	jsr	-72(a6)	; InitCode()
	movem.l	(sp)+,d0-d1/a6
	rts

; InitStruct(initTable, memory, size)
	xdef	_InitStruct
_InitStruct:
	movem.l	d0/a1-a2/a6,-(sp)
	move.l	16(sp),a1	; initTable
	move.l	20(sp),a2	; memory
	move.l	24(sp),d0	; size
	move.l	_SysBase,a6
	jsr	-78(a6)	; InitStruct()
	movem.l	(sp)+,d0/a1-a2/a6
	rts

; MakeLibrary(funcInit, structInit, libInit, dataSize, segList)
	xdef	_MakeLibrary
_MakeLibrary:
	movem.l	d0-d1/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; funcInit
	move.l	20(sp),a1	; structInit
	move.l	24(sp),a2	; libInit
	move.l	28(sp),d0	; dataSize
	move.l	32(sp),d1	; segList
	move.l	_SysBase,a6
	jsr	-84(a6)	; MakeLibrary()
	movem.l	(sp)+,d0-d1/a0-a2/a6
	rts

; MakeFunctions(target, functionArray, funcDispBase)
	xdef	_MakeFunctions
_MakeFunctions:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; target
	move.l	16(sp),a1	; functionArray
	move.l	20(sp),a2	; funcDispBase
	move.l	_SysBase,a6
	jsr	-90(a6)	; MakeFunctions()
	movem.l	(sp)+,a0-a2/a6
	rts

; FindResident(name)
	xdef	_FindResident
_FindResident:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; name
	move.l	_SysBase,a6
	jsr	-96(a6)	; FindResident()
	movem.l	(sp)+,a1/a6
	rts

; InitResident(resident, segList)
	xdef	_InitResident
_InitResident:
	movem.l	d1/a1/a6,-(sp)
	move.l	16(sp),a1	; resident
	move.l	20(sp),d1	; segList
	move.l	_SysBase,a6
	jsr	-102(a6)	; InitResident()
	movem.l	(sp)+,d1/a1/a6
	rts

; Alert(alertNum)
	xdef	_Alert
_Alert:
	movem.l	d7/a6,-(sp)
	move.l	12(sp),d7	; alertNum
	move.l	_SysBase,a6
	jsr	-108(a6)	; Alert()
	movem.l	(sp)+,d7/a6
	rts

; Debug(flags)
	xdef	_Debug
_Debug:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; flags
	move.l	_SysBase,a6
	jsr	-114(a6)	; Debug()
	movem.l	(sp)+,d0/a6
	rts

; Disable()
	xdef	_Disable
_Disable:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-120(a6)	; Disable()
	movem.l	(sp)+,a6
	rts

; Enable()
	xdef	_Enable
_Enable:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-126(a6)	; Enable()
	movem.l	(sp)+,a6
	rts

; Forbid()
	xdef	_Forbid
_Forbid:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-132(a6)	; Forbid()
	movem.l	(sp)+,a6
	rts

; Permit()
	xdef	_Permit
_Permit:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-138(a6)	; Permit()
	movem.l	(sp)+,a6
	rts

; SetSR(newSR, mask)
	xdef	_SetSR
_SetSR:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; newSR
	move.l	16(sp),d1	; mask
	move.l	_SysBase,a6
	jsr	-144(a6)	; SetSR()
	movem.l	(sp)+,d0-d1/a6
	rts

; SuperState()
	xdef	_SuperState
_SuperState:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-150(a6)	; SuperState()
	movem.l	(sp)+,a6
	rts

; UserState(sysStack)
	xdef	_UserState
_UserState:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; sysStack
	move.l	_SysBase,a6
	jsr	-156(a6)	; UserState()
	movem.l	(sp)+,d0/a6
	rts

; SetIntVector(intNumber, interrupt)
	xdef	_SetIntVector
_SetIntVector:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),d0	; intNumber
	move.l	20(sp),a1	; interrupt
	move.l	_SysBase,a6
	jsr	-162(a6)	; SetIntVector()
	movem.l	(sp)+,d0/a1/a6
	rts

; AddIntServer(intNumber, interrupt)
	xdef	_AddIntServer
_AddIntServer:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),d0	; intNumber
	move.l	20(sp),a1	; interrupt
	move.l	_SysBase,a6
	jsr	-168(a6)	; AddIntServer()
	movem.l	(sp)+,d0/a1/a6
	rts

; RemIntServer(intNumber, interrupt)
	xdef	_RemIntServer
_RemIntServer:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),d0	; intNumber
	move.l	20(sp),a1	; interrupt
	move.l	_SysBase,a6
	jsr	-174(a6)	; RemIntServer()
	movem.l	(sp)+,d0/a1/a6
	rts

; Cause(interrupt)
	xdef	_Cause
_Cause:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; interrupt
	move.l	_SysBase,a6
	jsr	-180(a6)	; Cause()
	movem.l	(sp)+,a1/a6
	rts

; Allocate(freeList, byteSize)
	xdef	_Allocate
_Allocate:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; freeList
	move.l	20(sp),d0	; byteSize
	move.l	_SysBase,a6
	jsr	-186(a6)	; Allocate()
	movem.l	(sp)+,d0/a0/a6
	rts

; Deallocate(freeList, memoryBlock, byteSize)
	xdef	_Deallocate
_Deallocate:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; freeList
	move.l	20(sp),a1	; memoryBlock
	move.l	24(sp),d0	; byteSize
	move.l	_SysBase,a6
	jsr	-192(a6)	; Deallocate()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; AllocMem(byteSize, requirements)
	xdef	_AllocMem
_AllocMem:
	movem.l	d1/a6,-(sp)	; Don't save d0 - it's the return value!
	move.l	12(sp),d0	; byteSize (skip 2 saved regs + return addr = 12)
	move.l	16(sp),d1	; requirements (skip 2 saved regs + return addr + 1 param = 16)
	move.l	_SysBase,a6
	jsr	-198(a6)	; AllocMem() - returns pointer in D0
	movem.l	(sp)+,d1/a6	; Restore d1 and a6 but NOT d0!
	rts

; AllocAbs(byteSize, location)
	xdef	_AllocAbs
_AllocAbs:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),d0	; byteSize
	move.l	20(sp),a1	; location
	move.l	_SysBase,a6
	jsr	-204(a6)	; AllocAbs()
	movem.l	(sp)+,d0/a1/a6
	rts

; FreeMem(memoryBlock, byteSize)
	xdef	_FreeMem
_FreeMem:
	movem.l	a1/a6,-(sp)	; Don't save d0 - we'll use it for byteSize
	move.l	12(sp),a1	; memoryBlock (skip 2 saved regs + return addr = 12)
	move.l	16(sp),d0	; byteSize (skip 2 saved regs + return addr + 1 param = 16)
	move.l	_SysBase,a6
	jsr	-210(a6)	; FreeMem() - no return value
	movem.l	(sp)+,a1/a6	; Restore a1 and a6
	rts

; AvailMem(requirements)
	xdef	_AvailMem
_AvailMem:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; requirements
	move.l	_SysBase,a6
	jsr	-216(a6)	; AvailMem()
	movem.l	(sp)+,d1/a6
	rts

; AllocEntry(entry)
	xdef	_AllocEntry
_AllocEntry:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; entry
	move.l	_SysBase,a6
	jsr	-222(a6)	; AllocEntry()
	movem.l	(sp)+,a0/a6
	rts

; FreeEntry(entry)
	xdef	_FreeEntry
_FreeEntry:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; entry
	move.l	_SysBase,a6
	jsr	-228(a6)	; FreeEntry()
	movem.l	(sp)+,a0/a6
	rts

; Insert(list, node, pred)
	xdef	_Insert
_Insert:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	16(sp),a1	; node
	move.l	20(sp),a2	; pred
	move.l	_SysBase,a6
	jsr	-234(a6)	; Insert()
	movem.l	(sp)+,a0-a2/a6
	rts

; AddHead(list, node)
	xdef	_AddHead
_AddHead:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	16(sp),a1	; node
	move.l	_SysBase,a6
	jsr	-240(a6)	; AddHead()
	movem.l	(sp)+,a0-a1/a6
	rts

; AddTail(list, node)
	xdef	_AddTail
_AddTail:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	16(sp),a1	; node
	move.l	_SysBase,a6
	jsr	-246(a6)	; AddTail()
	movem.l	(sp)+,a0-a1/a6
	rts

; Remove(node)
	xdef	_Remove
_Remove:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; node
	move.l	_SysBase,a6
	jsr	-252(a6)	; Remove()
	movem.l	(sp)+,a1/a6
	rts

; RemHead(list)
	xdef	_RemHead
_RemHead:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	_SysBase,a6
	jsr	-258(a6)	; RemHead()
	movem.l	(sp)+,a0/a6
	rts

; RemTail(list)
	xdef	_RemTail
_RemTail:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	_SysBase,a6
	jsr	-264(a6)	; RemTail()
	movem.l	(sp)+,a0/a6
	rts

; Enqueue(list, node)
	xdef	_Enqueue
_Enqueue:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	16(sp),a1	; node
	move.l	_SysBase,a6
	jsr	-270(a6)	; Enqueue()
	movem.l	(sp)+,a0-a1/a6
	rts

; FindName(list, name)
	xdef	_FindName
_FindName:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; list
	move.l	16(sp),a1	; name
	move.l	_SysBase,a6
	jsr	-276(a6)	; FindName()
	movem.l	(sp)+,a0-a1/a6
	rts

; AddTask(task, initPC, finalPC)
	xdef	_AddTask
_AddTask:
	movem.l	a1-a3/a6,-(sp)
	move.l	12(sp),a1	; task
	move.l	16(sp),a2	; initPC
	move.l	20(sp),a3	; finalPC
	move.l	_SysBase,a6
	jsr	-282(a6)	; AddTask()
	movem.l	(sp)+,a1-a3/a6
	rts

; RemTask(task)
	xdef	_RemTask
_RemTask:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; task
	move.l	_SysBase,a6
	jsr	-288(a6)	; RemTask()
	movem.l	(sp)+,a1/a6
	rts

; FindTask(name)
	xdef	_FindTask
_FindTask:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; name
	move.l	_SysBase,a6
	jsr	-294(a6)	; FindTask()
	movem.l	(sp)+,a1/a6
	rts

; SetTaskPri(task, priority)
	xdef	_SetTaskPri
_SetTaskPri:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; task
	move.l	20(sp),d0	; priority
	move.l	_SysBase,a6
	jsr	-300(a6)	; SetTaskPri()
	movem.l	(sp)+,d0/a1/a6
	rts

; SetSignal(newSignals, signalSet)
	xdef	_SetSignal
_SetSignal:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; newSignals
	move.l	16(sp),d1	; signalSet
	move.l	_SysBase,a6
	jsr	-306(a6)	; SetSignal()
	movem.l	(sp)+,d0-d1/a6
	rts

; SetExcept(newSignals, signalSet)
	xdef	_SetExcept
_SetExcept:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; newSignals
	move.l	16(sp),d1	; signalSet
	move.l	_SysBase,a6
	jsr	-312(a6)	; SetExcept()
	movem.l	(sp)+,d0-d1/a6
	rts

; Wait(signalSet)
	xdef	_Wait
_Wait:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; signalSet
	move.l	_SysBase,a6
	jsr	-318(a6)	; Wait()
	movem.l	(sp)+,d0/a6
	rts

; Signal(task, signalSet)
	xdef	_Signal
_Signal:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; task
	move.l	20(sp),d0	; signalSet
	move.l	_SysBase,a6
	jsr	-324(a6)	; Signal()
	movem.l	(sp)+,d0/a1/a6
	rts

; AllocSignal(signalNum)
	xdef	_AllocSignal
_AllocSignal:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; signalNum
	move.l	_SysBase,a6
	jsr	-330(a6)	; AllocSignal()
	movem.l	(sp)+,d0/a6
	rts

; FreeSignal(signalNum)
	xdef	_FreeSignal
_FreeSignal:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; signalNum
	move.l	_SysBase,a6
	jsr	-336(a6)	; FreeSignal()
	movem.l	(sp)+,d0/a6
	rts

; AllocTrap(trapNum)
	xdef	_AllocTrap
_AllocTrap:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; trapNum
	move.l	_SysBase,a6
	jsr	-342(a6)	; AllocTrap()
	movem.l	(sp)+,d0/a6
	rts

; FreeTrap(trapNum)
	xdef	_FreeTrap
_FreeTrap:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; trapNum
	move.l	_SysBase,a6
	jsr	-348(a6)	; FreeTrap()
	movem.l	(sp)+,d0/a6
	rts

; AddPort(port)
	xdef	_AddPort
_AddPort:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; port
	move.l	_SysBase,a6
	jsr	-354(a6)	; AddPort()
	movem.l	(sp)+,a1/a6
	rts

; RemPort(port)
	xdef	_RemPort
_RemPort:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; port
	move.l	_SysBase,a6
	jsr	-360(a6)	; RemPort()
	movem.l	(sp)+,a1/a6
	rts

; PutMsg(port, message)
	xdef	_PutMsg
_PutMsg:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; port
	move.l	16(sp),a1	; message
	move.l	_SysBase,a6
	jsr	-366(a6)	; PutMsg()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetMsg(port)
	xdef	_GetMsg
_GetMsg:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; port
	move.l	_SysBase,a6
	jsr	-372(a6)	; GetMsg()
	movem.l	(sp)+,a0/a6
	rts

; ReplyMsg(message)
	xdef	_ReplyMsg
_ReplyMsg:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; message
	move.l	_SysBase,a6
	jsr	-378(a6)	; ReplyMsg()
	movem.l	(sp)+,a1/a6
	rts

; WaitPort(port)
	xdef	_WaitPort
_WaitPort:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; port
	move.l	_SysBase,a6
	jsr	-384(a6)	; WaitPort()
	movem.l	(sp)+,a0/a6
	rts

; FindPort(name)
	xdef	_FindPort
_FindPort:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; name
	move.l	_SysBase,a6
	jsr	-390(a6)	; FindPort()
	movem.l	(sp)+,a1/a6
	rts

; AddLibrary(library)
	xdef	_AddLibrary
_AddLibrary:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; library
	move.l	_SysBase,a6
	jsr	-396(a6)	; AddLibrary()
	movem.l	(sp)+,a1/a6
	rts

; RemLibrary(library)
	xdef	_RemLibrary
_RemLibrary:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; library
	move.l	_SysBase,a6
	jsr	-402(a6)	; RemLibrary()
	movem.l	(sp)+,a1/a6
	rts

; OldOpenLibrary(libName)
	xdef	_OldOpenLibrary
_OldOpenLibrary:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; libName
	move.l	_SysBase,a6
	jsr	-408(a6)	; OldOpenLibrary()
	movem.l	(sp)+,a1/a6
	rts

; CloseLibrary(library)
	xdef	_CloseLibrary
_CloseLibrary:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; library
	move.l	_SysBase,a6
	jsr	-414(a6)	; CloseLibrary()
	movem.l	(sp)+,a1/a6
	rts

; SetFunction(library, funcOffset, newFunction)
	xdef	_SetFunction
_SetFunction:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a1	; library
	move.l	20(sp),a0	; funcOffset
	move.l	24(sp),d0	; newFunction
	move.l	_SysBase,a6
	jsr	-420(a6)	; SetFunction()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; SumLibrary(library)
	xdef	_SumLibrary
_SumLibrary:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; library
	move.l	_SysBase,a6
	jsr	-426(a6)	; SumLibrary()
	movem.l	(sp)+,a1/a6
	rts

; AddDevice(device)
	xdef	_AddDevice
_AddDevice:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; device
	move.l	_SysBase,a6
	jsr	-432(a6)	; AddDevice()
	movem.l	(sp)+,a1/a6
	rts

; RemDevice(device)
	xdef	_RemDevice
_RemDevice:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; device
	move.l	_SysBase,a6
	jsr	-438(a6)	; RemDevice()
	movem.l	(sp)+,a1/a6
	rts

; OpenDevice(devName, unit, ioRequest, flags)
	xdef	_OpenDevice
_OpenDevice:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; devName
	move.l	20(sp),d0	; unit
	move.l	24(sp),a1	; ioRequest
	move.l	28(sp),d1	; flags
	move.l	_SysBase,a6
	jsr	-444(a6)	; OpenDevice()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; CloseDevice(ioRequest)
	xdef	_CloseDevice
_CloseDevice:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; ioRequest
	move.l	_SysBase,a6
	jsr	-450(a6)	; CloseDevice()
	movem.l	(sp)+,a1/a6
	rts

; DoIO(ioRequest)
	xdef	_DoIO
_DoIO:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; ioRequest
	move.l	_SysBase,a6
	jsr	-456(a6)	; DoIO()
	movem.l	(sp)+,a1/a6
	rts

; SendIO(ioRequest)
	xdef	_SendIO
_SendIO:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; ioRequest
	move.l	_SysBase,a6
	jsr	-462(a6)	; SendIO()
	movem.l	(sp)+,a1/a6
	rts

; CheckIO(ioRequest)
	xdef	_CheckIO
_CheckIO:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; ioRequest
	move.l	_SysBase,a6
	jsr	-468(a6)	; CheckIO()
	movem.l	(sp)+,a1/a6
	rts

; WaitIO(ioRequest)
	xdef	_WaitIO
_WaitIO:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; ioRequest
	move.l	_SysBase,a6
	jsr	-474(a6)	; WaitIO()
	movem.l	(sp)+,a1/a6
	rts

; AbortIO(ioRequest)
	xdef	_AbortIO
_AbortIO:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; ioRequest
	move.l	_SysBase,a6
	jsr	-480(a6)	; AbortIO()
	movem.l	(sp)+,a1/a6
	rts

; AddResource(resource)
	xdef	_AddResource
_AddResource:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; resource
	move.l	_SysBase,a6
	jsr	-486(a6)	; AddResource()
	movem.l	(sp)+,a1/a6
	rts

; RemResource(resource)
	xdef	_RemResource
_RemResource:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; resource
	move.l	_SysBase,a6
	jsr	-492(a6)	; RemResource()
	movem.l	(sp)+,a1/a6
	rts

; OpenResource(resName)
	xdef	_OpenResource
_OpenResource:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; resName
	move.l	_SysBase,a6
	jsr	-498(a6)	; OpenResource()
	movem.l	(sp)+,a1/a6
	rts

; RawDoFmt(formatString, dataStream, putChProc, putChData)
	xdef	_RawDoFmt
_RawDoFmt:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; formatString
	move.l	16(sp),a1	; dataStream
	move.l	20(sp),a2	; putChProc
	move.l	24(sp),a3	; putChData
	move.l	_SysBase,a6
	jsr	-522(a6)	; RawDoFmt()
	movem.l	(sp)+,a0-a3/a6
	rts

; GetCC()
	xdef	_GetCC
_GetCC:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-528(a6)	; GetCC()
	movem.l	(sp)+,a6
	rts

; TypeOfMem(address)
	xdef	_TypeOfMem
_TypeOfMem:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; address
	move.l	_SysBase,a6
	jsr	-534(a6)	; TypeOfMem()
	movem.l	(sp)+,a1/a6
	rts

; Procure(sigSem, bidMsg)
	xdef	_Procure
_Procure:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	16(sp),a1	; bidMsg
	move.l	_SysBase,a6
	jsr	-540(a6)	; Procure()
	movem.l	(sp)+,a0-a1/a6
	rts

; Vacate(sigSem, bidMsg)
	xdef	_Vacate
_Vacate:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	16(sp),a1	; bidMsg
	move.l	_SysBase,a6
	jsr	-546(a6)	; Vacate()
	movem.l	(sp)+,a0-a1/a6
	rts

; OpenLibrary(libName, version)
	xdef	_OpenLibrary
_OpenLibrary:
	movem.l	d0/a1/a6,-(sp)
	move.l	16(sp),a1	; libName
	move.l	20(sp),d0	; version
	move.l	_SysBase,a6
	jsr	-552(a6)	; OpenLibrary()
	movem.l	(sp)+,d0/a1/a6
	rts

; InitSemaphore(sigSem)
	xdef	_InitSemaphore
_InitSemaphore:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-558(a6)	; InitSemaphore()
	movem.l	(sp)+,a0/a6
	rts

; ObtainSemaphore(sigSem)
	xdef	_ObtainSemaphore
_ObtainSemaphore:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-564(a6)	; ObtainSemaphore()
	movem.l	(sp)+,a0/a6
	rts

; ReleaseSemaphore(sigSem)
	xdef	_ReleaseSemaphore
_ReleaseSemaphore:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-570(a6)	; ReleaseSemaphore()
	movem.l	(sp)+,a0/a6
	rts

; AttemptSemaphore(sigSem)
	xdef	_AttemptSemaphore
_AttemptSemaphore:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-576(a6)	; AttemptSemaphore()
	movem.l	(sp)+,a0/a6
	rts

; ObtainSemaphoreList(sigSem)
	xdef	_ObtainSemaphoreList
_ObtainSemaphoreList:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-582(a6)	; ObtainSemaphoreList()
	movem.l	(sp)+,a0/a6
	rts

; ReleaseSemaphoreList(sigSem)
	xdef	_ReleaseSemaphoreList
_ReleaseSemaphoreList:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-588(a6)	; ReleaseSemaphoreList()
	movem.l	(sp)+,a0/a6
	rts

; FindSemaphore(name)
	xdef	_FindSemaphore
_FindSemaphore:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; name
	move.l	_SysBase,a6
	jsr	-594(a6)	; FindSemaphore()
	movem.l	(sp)+,a1/a6
	rts

; AddSemaphore(sigSem)
	xdef	_AddSemaphore
_AddSemaphore:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; sigSem
	move.l	_SysBase,a6
	jsr	-600(a6)	; AddSemaphore()
	movem.l	(sp)+,a1/a6
	rts

; RemSemaphore(sigSem)
	xdef	_RemSemaphore
_RemSemaphore:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; sigSem
	move.l	_SysBase,a6
	jsr	-606(a6)	; RemSemaphore()
	movem.l	(sp)+,a1/a6
	rts

; SumKickData()
	xdef	_SumKickData
_SumKickData:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-612(a6)	; SumKickData()
	movem.l	(sp)+,a6
	rts

; AddMemList(size, attributes, pri, base, name)
	xdef	_AddMemList
_AddMemList:
	movem.l	d0-d2/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; size
	move.l	20(sp),d1	; attributes
	move.l	24(sp),d2	; pri
	move.l	28(sp),a0	; base
	move.l	32(sp),a1	; name
	move.l	_SysBase,a6
	jsr	-618(a6)	; AddMemList()
	movem.l	(sp)+,d0-d2/a0-a1/a6
	rts

; CopyMem(source, dest, size)
	xdef	_CopyMem
_CopyMem:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; source
	move.l	20(sp),a1	; dest
	move.l	24(sp),d0	; size
	move.l	_SysBase,a6
	jsr	-624(a6)	; CopyMem()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; CopyMemQuick(source, dest, size)
	xdef	_CopyMemQuick
_CopyMemQuick:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; source
	move.l	20(sp),a1	; dest
	move.l	24(sp),d0	; size
	move.l	_SysBase,a6
	jsr	-630(a6)	; CopyMemQuick()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; CacheClearU()
	xdef	_CacheClearU
_CacheClearU:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-636(a6)	; CacheClearU()
	movem.l	(sp)+,a6
	rts

; CacheClearE(address, length, caches)
	xdef	_CacheClearE
_CacheClearE:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; address
	move.l	20(sp),d0	; length
	move.l	24(sp),d1	; caches
	move.l	_SysBase,a6
	jsr	-642(a6)	; CacheClearE()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; CacheControl(cacheBits, cacheMask)
	xdef	_CacheControl
_CacheControl:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; cacheBits
	move.l	16(sp),d1	; cacheMask
	move.l	_SysBase,a6
	jsr	-648(a6)	; CacheControl()
	movem.l	(sp)+,d0-d1/a6
	rts

; CreateIORequest(port, size)
	xdef	_CreateIORequest
_CreateIORequest:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; port
	move.l	20(sp),d0	; size
	move.l	_SysBase,a6
	jsr	-654(a6)	; CreateIORequest()
	movem.l	(sp)+,d0/a0/a6
	rts

; DeleteIORequest(iorequest)
	xdef	_DeleteIORequest
_DeleteIORequest:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; iorequest
	move.l	_SysBase,a6
	jsr	-660(a6)	; DeleteIORequest()
	movem.l	(sp)+,a0/a6
	rts

; CreateMsgPort()
	xdef	_CreateMsgPort
_CreateMsgPort:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-666(a6)	; CreateMsgPort()
	movem.l	(sp)+,a6
	rts

; DeleteMsgPort(port)
	xdef	_DeleteMsgPort
_DeleteMsgPort:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; port
	move.l	_SysBase,a6
	jsr	-672(a6)	; DeleteMsgPort()
	movem.l	(sp)+,a0/a6
	rts

; ObtainSemaphoreShared(sigSem)
	xdef	_ObtainSemaphoreShared
_ObtainSemaphoreShared:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-678(a6)	; ObtainSemaphoreShared()
	movem.l	(sp)+,a0/a6
	rts

; AllocVec(byteSize, requirements)
	xdef	_AllocVec
_AllocVec:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; byteSize
	move.l	16(sp),d1	; requirements
	move.l	_SysBase,a6
	jsr	-684(a6)	; AllocVec()
	movem.l	(sp)+,d0-d1/a6
	rts

; FreeVec(memoryBlock)
	xdef	_FreeVec
_FreeVec:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; memoryBlock
	move.l	_SysBase,a6
	jsr	-690(a6)	; FreeVec()
	movem.l	(sp)+,a1/a6
	rts

; CreatePool(requirements, puddleSize, threshSize)
	xdef	_CreatePool
_CreatePool:
	movem.l	d0-d2/a6,-(sp)
	move.l	12(sp),d0	; requirements
	move.l	16(sp),d1	; puddleSize
	move.l	20(sp),d2	; threshSize
	move.l	_SysBase,a6
	jsr	-696(a6)	; CreatePool()
	movem.l	(sp)+,d0-d2/a6
	rts

; DeletePool(poolHeader)
	xdef	_DeletePool
_DeletePool:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; poolHeader
	move.l	_SysBase,a6
	jsr	-702(a6)	; DeletePool()
	movem.l	(sp)+,a0/a6
	rts

; AllocPooled(poolHeader, memSize)
	xdef	_AllocPooled
_AllocPooled:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; poolHeader
	move.l	20(sp),d0	; memSize
	move.l	_SysBase,a6
	jsr	-708(a6)	; AllocPooled()
	movem.l	(sp)+,d0/a0/a6
	rts

; FreePooled(poolHeader, memory, memSize)
	xdef	_FreePooled
_FreePooled:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; poolHeader
	move.l	20(sp),a1	; memory
	move.l	24(sp),d0	; memSize
	move.l	_SysBase,a6
	jsr	-714(a6)	; FreePooled()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; AttemptSemaphoreShared(sigSem)
	xdef	_AttemptSemaphoreShared
_AttemptSemaphoreShared:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; sigSem
	move.l	_SysBase,a6
	jsr	-720(a6)	; AttemptSemaphoreShared()
	movem.l	(sp)+,a0/a6
	rts

; ColdReboot()
	xdef	_ColdReboot
_ColdReboot:
	movem.l	a6,-(sp)
	move.l	_SysBase,a6
	jsr	-726(a6)	; ColdReboot()
	movem.l	(sp)+,a6
	rts

; StackSwap(newStack)
	xdef	_StackSwap
_StackSwap:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; newStack
	move.l	_SysBase,a6
	jsr	-732(a6)	; StackSwap()
	movem.l	(sp)+,a0/a6
	rts

; CachePreDMA(address, length, flags)
	xdef	_CachePreDMA
_CachePreDMA:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; address
	move.l	20(sp),a1	; length
	move.l	24(sp),d0	; flags
	move.l	_SysBase,a6
	jsr	-762(a6)	; CachePreDMA()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; CachePostDMA(address, length, flags)
	xdef	_CachePostDMA
_CachePostDMA:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; address
	move.l	20(sp),a1	; length
	move.l	24(sp),d0	; flags
	move.l	_SysBase,a6
	jsr	-768(a6)	; CachePostDMA()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; AddMemHandler(memhand)
	xdef	_AddMemHandler
_AddMemHandler:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; memhand
	move.l	_SysBase,a6
	jsr	-774(a6)	; AddMemHandler()
	movem.l	(sp)+,a1/a6
	rts

; RemMemHandler(memhand)
	xdef	_RemMemHandler
_RemMemHandler:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; memhand
	move.l	_SysBase,a6
	jsr	-780(a6)	; RemMemHandler()
	movem.l	(sp)+,a1/a6
	rts

; ObtainQuickVector(interruptCode)
	xdef	_ObtainQuickVector
_ObtainQuickVector:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; interruptCode
	move.l	_SysBase,a6
	jsr	-786(a6)	; ObtainQuickVector()
	movem.l	(sp)+,a0/a6
	rts

; NewMinList(minlist)
	xdef	_NewMinList
_NewMinList:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; minlist
	move.l	_SysBase,a6
	jsr	-828(a6)	; NewMinList()
	movem.l	(sp)+,a0/a6
	rts

; AVL_AddNode(root, node, func)
	xdef	_AVL_AddNode
_AVL_AddNode:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	16(sp),a1	; node
	move.l	20(sp),a2	; func
	move.l	_SysBase,a6
	jsr	-852(a6)	; AVL_AddNode()
	movem.l	(sp)+,a0-a2/a6
	rts

; AVL_RemNodeByAddress(root, node)
	xdef	_AVL_RemNodeByAddress
_AVL_RemNodeByAddress:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	16(sp),a1	; node
	move.l	_SysBase,a6
	jsr	-858(a6)	; AVL_RemNodeByAddress()
	movem.l	(sp)+,a0-a1/a6
	rts

; AVL_RemNodeByKey(root, key, func)
	xdef	_AVL_RemNodeByKey
_AVL_RemNodeByKey:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	16(sp),a1	; key
	move.l	20(sp),a2	; func
	move.l	_SysBase,a6
	jsr	-864(a6)	; AVL_RemNodeByKey()
	movem.l	(sp)+,a0-a2/a6
	rts

; AVL_FindNode(root, key, func)
	xdef	_AVL_FindNode
_AVL_FindNode:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	16(sp),a1	; key
	move.l	20(sp),a2	; func
	move.l	_SysBase,a6
	jsr	-870(a6)	; AVL_FindNode()
	movem.l	(sp)+,a0-a2/a6
	rts

; AVL_FindPrevNodeByAddress(node)
	xdef	_AVL_FindPrevNodeByAddress
_AVL_FindPrevNodeByAddress:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; node
	move.l	_SysBase,a6
	jsr	-876(a6)	; AVL_FindPrevNodeByAddress()
	movem.l	(sp)+,a0/a6
	rts

; AVL_FindPrevNodeByKey(root, key, func)
	xdef	_AVL_FindPrevNodeByKey
_AVL_FindPrevNodeByKey:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	16(sp),a1	; key
	move.l	20(sp),a2	; func
	move.l	_SysBase,a6
	jsr	-882(a6)	; AVL_FindPrevNodeByKey()
	movem.l	(sp)+,a0-a2/a6
	rts

; AVL_FindNextNodeByAddress(node)
	xdef	_AVL_FindNextNodeByAddress
_AVL_FindNextNodeByAddress:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; node
	move.l	_SysBase,a6
	jsr	-888(a6)	; AVL_FindNextNodeByAddress()
	movem.l	(sp)+,a0/a6
	rts

; AVL_FindNextNodeByKey(root, key, func)
	xdef	_AVL_FindNextNodeByKey
_AVL_FindNextNodeByKey:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	16(sp),a1	; key
	move.l	20(sp),a2	; func
	move.l	_SysBase,a6
	jsr	-894(a6)	; AVL_FindNextNodeByKey()
	movem.l	(sp)+,a0-a2/a6
	rts

; AVL_FindFirstNode(root)
	xdef	_AVL_FindFirstNode
_AVL_FindFirstNode:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	_SysBase,a6
	jsr	-900(a6)	; AVL_FindFirstNode()
	movem.l	(sp)+,a0/a6
	rts

; AVL_FindLastNode(root)
	xdef	_AVL_FindLastNode
_AVL_FindLastNode:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; root
	move.l	_SysBase,a6
	jsr	-906(a6)	; AVL_FindLastNode()
	movem.l	(sp)+,a0/a6
	rts

