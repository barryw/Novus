; dos library stubs for Novus
; Auto-generated from dos_lib.fd

	xref	_DOSBase	; Provided by startup.o + -lamiga

	section	text,code

; Open(name, accessMode)
	xdef	_Open
_Open:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; accessMode
	move.l	_DOSBase,a6
	jsr	-30(a6)	; Open()
	movem.l	(sp)+,d1-d2/a6
	rts

; Close(file)
	xdef	_Close
_Close:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; file
	move.l	_DOSBase,a6
	jsr	-36(a6)	; Close()
	movem.l	(sp)+,d1/a6
	rts

; Read(file, buffer, length)
	xdef	_Read
_Read:
	movem.l	d1-d3/a6,-(sp)
	move.l	20(sp),d1	; file (was 12)
	move.l	24(sp),d2	; buffer (was 16)
	move.l	28(sp),d3	; length (was 20)
	move.l	_DOSBase,a6
	jsr	-42(a6)	; Read()
	movem.l	(sp)+,d1-d3/a6
	rts

; Write(file, buffer, length)
	xdef	_Write
_Write:
	movem.l	d1-d3/a6,-(sp)
	move.l	20(sp),d1	; file (was 12)
	move.l	24(sp),d2	; buffer (was 16)
	move.l	28(sp),d3	; length (was 20)
	move.l	_DOSBase,a6
	jsr	-48(a6)	; Write()
	movem.l	(sp)+,d1-d3/a6
	rts

; Input()
	xdef	_Input
_Input:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-54(a6)	; Input()
	movem.l	(sp)+,a6
	rts

; Output()
	xdef	_Output
_Output:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-60(a6)	; Output()
	movem.l	(sp)+,a6
	rts

; Error()
	xdef	_Error
_Error:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-276(a6)	; Error()
	movem.l	(sp)+,a6
	rts

; Seek(file, position, offset)
	xdef	_Seek
_Seek:
	movem.l	d1-d3/a6,-(sp)
	move.l	20(sp),d1	; file (was 12)
	move.l	24(sp),d2	; position (was 16)
	move.l	28(sp),d3	; offset (was 20)
	move.l	_DOSBase,a6
	jsr	-66(a6)	; Seek()
	movem.l	(sp)+,d1-d3/a6
	rts

; DeleteFile(name)
	xdef	_DeleteFile
_DeleteFile:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-72(a6)	; DeleteFile()
	movem.l	(sp)+,d1/a6
	rts

; Rename(oldName, newName)
	xdef	_Rename
_Rename:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; oldName
	move.l	20(sp),d2	; newName
	move.l	_DOSBase,a6
	jsr	-78(a6)	; Rename()
	movem.l	(sp)+,d1-d2/a6
	rts

; Lock(name, type)
	xdef	_Lock
_Lock:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; type
	move.l	_DOSBase,a6
	jsr	-84(a6)	; Lock()
	movem.l	(sp)+,d1-d2/a6
	rts

; UnLock(lock)
	xdef	_UnLock
_UnLock:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; lock
	move.l	_DOSBase,a6
	jsr	-90(a6)	; UnLock()
	movem.l	(sp)+,d1/a6
	rts

; DupLock(lock)
	xdef	_DupLock
_DupLock:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; lock
	move.l	_DOSBase,a6
	jsr	-96(a6)	; DupLock()
	movem.l	(sp)+,d1/a6
	rts

; Examine(lock, fileInfoBlock)
	xdef	_Examine
_Examine:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; lock
	move.l	20(sp),d2	; fileInfoBlock
	move.l	_DOSBase,a6
	jsr	-102(a6)	; Examine()
	movem.l	(sp)+,d1-d2/a6
	rts

; ExNext(lock, fileInfoBlock)
	xdef	_ExNext
_ExNext:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; lock
	move.l	20(sp),d2	; fileInfoBlock
	move.l	_DOSBase,a6
	jsr	-108(a6)	; ExNext()
	movem.l	(sp)+,d1-d2/a6
	rts

; Info(lock, parameterBlock)
	xdef	_Info
_Info:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; lock
	move.l	20(sp),d2	; parameterBlock
	move.l	_DOSBase,a6
	jsr	-114(a6)	; Info()
	movem.l	(sp)+,d1-d2/a6
	rts

; CreateDir(name)
	xdef	_CreateDir
_CreateDir:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-120(a6)	; CreateDir()
	movem.l	(sp)+,d1/a6
	rts

; CurrentDir(lock)
	xdef	_CurrentDir
_CurrentDir:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; lock
	move.l	_DOSBase,a6
	jsr	-126(a6)	; CurrentDir()
	movem.l	(sp)+,d1/a6
	rts

; IoErr()
	xdef	_IoErr
_IoErr:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-132(a6)	; IoErr()
	movem.l	(sp)+,a6
	rts

; CreateProc(name, pri, segList, stackSize)
	xdef	_CreateProc
_CreateProc:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; name
	move.l	28(sp),d2	; pri
	move.l	32(sp),d3	; segList
	move.l	36(sp),d4	; stackSize
	move.l	_DOSBase,a6
	jsr	-138(a6)	; CreateProc()
	movem.l	(sp)+,d1-d4/a6
	rts

; Exit(returnCode)
	xdef	_Exit
_Exit:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; returnCode
	move.l	_DOSBase,a6
	jsr	-144(a6)	; Exit()
	movem.l	(sp)+,d1/a6
	rts

; LoadSeg(name)
	xdef	_LoadSeg
_LoadSeg:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-150(a6)	; LoadSeg()
	movem.l	(sp)+,d1/a6
	rts

; UnLoadSeg(seglist)
	xdef	_UnLoadSeg
_UnLoadSeg:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; seglist
	move.l	_DOSBase,a6
	jsr	-156(a6)	; UnLoadSeg()
	movem.l	(sp)+,d1/a6
	rts

; DeviceProc(name)
	xdef	_DeviceProc
_DeviceProc:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-174(a6)	; DeviceProc()
	movem.l	(sp)+,d1/a6
	rts

; SetComment(name, comment)
	xdef	_SetComment
_SetComment:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; comment
	move.l	_DOSBase,a6
	jsr	-180(a6)	; SetComment()
	movem.l	(sp)+,d1-d2/a6
	rts

; SetProtection(name, protect)
	xdef	_SetProtection
_SetProtection:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; protect
	move.l	_DOSBase,a6
	jsr	-186(a6)	; SetProtection()
	movem.l	(sp)+,d1-d2/a6
	rts

; DateStamp(date)
	xdef	_DateStamp
_DateStamp:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; date
	move.l	_DOSBase,a6
	jsr	-192(a6)	; DateStamp()
	movem.l	(sp)+,d1/a6
	rts

; Delay(timeout)
	xdef	_Delay
_Delay:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; timeout
	move.l	_DOSBase,a6
	jsr	-198(a6)	; Delay()
	movem.l	(sp)+,d1/a6
	rts

; WaitForChar(file, timeout)
	xdef	_WaitForChar
_WaitForChar:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; file
	move.l	20(sp),d2	; timeout
	move.l	_DOSBase,a6
	jsr	-204(a6)	; WaitForChar()
	movem.l	(sp)+,d1-d2/a6
	rts

; ParentDir(lock)
	xdef	_ParentDir
_ParentDir:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; lock
	move.l	_DOSBase,a6
	jsr	-210(a6)	; ParentDir()
	movem.l	(sp)+,d1/a6
	rts

; IsInteractive(file)
	xdef	_IsInteractive
_IsInteractive:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; file
	move.l	_DOSBase,a6
	jsr	-216(a6)	; IsInteractive()
	movem.l	(sp)+,d1/a6
	rts

; Execute(string, file, file2)
	xdef	_Execute
_Execute:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; string
	move.l	16(sp),d2	; file
	move.l	20(sp),d3	; file2
	move.l	_DOSBase,a6
	jsr	-222(a6)	; Execute()
	movem.l	(sp)+,d1-d3/a6
	rts

; AllocDosObject(type, tags)
	xdef	_AllocDosObject
_AllocDosObject:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; type
	move.l	20(sp),d2	; tags
	move.l	_DOSBase,a6
	jsr	-228(a6)	; AllocDosObject()
	movem.l	(sp)+,d1-d2/a6
	rts

; FreeDosObject(type, ptr)
	xdef	_FreeDosObject
_FreeDosObject:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; type
	move.l	20(sp),d2	; ptr
	move.l	_DOSBase,a6
	jsr	-234(a6)	; FreeDosObject()
	movem.l	(sp)+,d1-d2/a6
	rts

; DoPkt(port, action, arg1, arg2, arg3, arg4, arg5)
	xdef	_DoPkt
_DoPkt:
	movem.l	d1-d7/a6,-(sp)
	move.l	36(sp),d1	; port
	move.l	40(sp),d2	; action
	move.l	44(sp),d3	; arg1
	move.l	48(sp),d4	; arg2
	move.l	52(sp),d5	; arg3
	move.l	56(sp),d6	; arg4
	move.l	60(sp),d7	; arg5
	move.l	_DOSBase,a6
	jsr	-240(a6)	; DoPkt()
	movem.l	(sp)+,d1-d7/a6
	rts

; SendPkt(dp, port, replyport)
	xdef	_SendPkt
_SendPkt:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; dp
	move.l	16(sp),d2	; port
	move.l	20(sp),d3	; replyport
	move.l	_DOSBase,a6
	jsr	-246(a6)	; SendPkt()
	movem.l	(sp)+,d1-d3/a6
	rts

; WaitPkt()
	xdef	_WaitPkt
_WaitPkt:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-252(a6)	; WaitPkt()
	movem.l	(sp)+,a6
	rts

; ReplyPkt(dp, res1, res2)
	xdef	_ReplyPkt
_ReplyPkt:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; dp
	move.l	16(sp),d2	; res1
	move.l	20(sp),d3	; res2
	move.l	_DOSBase,a6
	jsr	-258(a6)	; ReplyPkt()
	movem.l	(sp)+,d1-d3/a6
	rts

; AbortPkt(port, pkt)
	xdef	_AbortPkt
_AbortPkt:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; port
	move.l	20(sp),d2	; pkt
	move.l	_DOSBase,a6
	jsr	-264(a6)	; AbortPkt()
	movem.l	(sp)+,d1-d2/a6
	rts

; LockRecord(fh, offset, length, mode, timeout)
	xdef	_LockRecord
_LockRecord:
	movem.l	d1-d5/a6,-(sp)
	move.l	28(sp),d1	; fh
	move.l	32(sp),d2	; offset
	move.l	36(sp),d3	; length
	move.l	40(sp),d4	; mode
	move.l	44(sp),d5	; timeout
	move.l	_DOSBase,a6
	jsr	-270(a6)	; LockRecord()
	movem.l	(sp)+,d1-d5/a6
	rts

; LockRecords(recArray, timeout)
	xdef	_LockRecords
_LockRecords:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; recArray
	move.l	20(sp),d2	; timeout
	move.l	_DOSBase,a6
	jsr	-276(a6)	; LockRecords()
	movem.l	(sp)+,d1-d2/a6
	rts

; UnLockRecord(fh, offset, length)
	xdef	_UnLockRecord
_UnLockRecord:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	16(sp),d2	; offset
	move.l	20(sp),d3	; length
	move.l	_DOSBase,a6
	jsr	-282(a6)	; UnLockRecord()
	movem.l	(sp)+,d1-d3/a6
	rts

; UnLockRecords(recArray)
	xdef	_UnLockRecords
_UnLockRecords:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; recArray
	move.l	_DOSBase,a6
	jsr	-288(a6)	; UnLockRecords()
	movem.l	(sp)+,d1/a6
	rts

; SelectInput(fh)
	xdef	_SelectInput
_SelectInput:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	_DOSBase,a6
	jsr	-294(a6)	; SelectInput()
	movem.l	(sp)+,d1/a6
	rts

; SelectOutput(fh)
	xdef	_SelectOutput
_SelectOutput:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	_DOSBase,a6
	jsr	-300(a6)	; SelectOutput()
	movem.l	(sp)+,d1/a6
	rts

; FGetC(fh)
	xdef	_FGetC
_FGetC:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	_DOSBase,a6
	jsr	-306(a6)	; FGetC()
	movem.l	(sp)+,d1/a6
	rts

; FPutC(fh, ch)
	xdef	_FPutC
_FPutC:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; fh
	move.l	20(sp),d2	; ch
	move.l	_DOSBase,a6
	jsr	-312(a6)	; FPutC()
	movem.l	(sp)+,d1-d2/a6
	rts

; UnGetC(fh, character)
	xdef	_UnGetC
_UnGetC:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; fh
	move.l	20(sp),d2	; character
	move.l	_DOSBase,a6
	jsr	-318(a6)	; UnGetC()
	movem.l	(sp)+,d1-d2/a6
	rts

; FRead(fh, block, blocklen, number)
	xdef	_FRead
_FRead:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; fh
	move.l	28(sp),d2	; block
	move.l	32(sp),d3	; blocklen
	move.l	36(sp),d4	; number
	move.l	_DOSBase,a6
	jsr	-324(a6)	; FRead()
	movem.l	(sp)+,d1-d4/a6
	rts

; FWrite(fh, block, blocklen, number)
	xdef	_FWrite
_FWrite:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; fh
	move.l	28(sp),d2	; block
	move.l	32(sp),d3	; blocklen
	move.l	36(sp),d4	; number
	move.l	_DOSBase,a6
	jsr	-330(a6)	; FWrite()
	movem.l	(sp)+,d1-d4/a6
	rts

; FGets(fh, buf, buflen)
	xdef	_FGets
_FGets:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	16(sp),d2	; buf
	move.l	20(sp),d3	; buflen
	move.l	_DOSBase,a6
	jsr	-336(a6)	; FGets()
	movem.l	(sp)+,d1-d3/a6
	rts

; FPuts(fh, str)
	xdef	_FPuts
_FPuts:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; fh
	move.l	20(sp),d2	; str
	move.l	_DOSBase,a6
	jsr	-342(a6)	; FPuts()
	movem.l	(sp)+,d1-d2/a6
	rts

; VFWritef(fh, format, argarray)
	xdef	_VFWritef
_VFWritef:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	16(sp),d2	; format
	move.l	20(sp),d3	; argarray
	move.l	_DOSBase,a6
	jsr	-348(a6)	; VFWritef()
	movem.l	(sp)+,d1-d3/a6
	rts

; VFPrintf(fh, format, argarray)
	xdef	_VFPrintf
_VFPrintf:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	16(sp),d2	; format
	move.l	20(sp),d3	; argarray
	move.l	_DOSBase,a6
	jsr	-354(a6)	; VFPrintf()
	movem.l	(sp)+,d1-d3/a6
	rts

; Flush(fh)
	xdef	_Flush
_Flush:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	_DOSBase,a6
	jsr	-360(a6)	; Flush()
	movem.l	(sp)+,d1/a6
	rts

; SetVBuf(fh, buff, type, size)
	xdef	_SetVBuf
_SetVBuf:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; fh
	move.l	28(sp),d2	; buff
	move.l	32(sp),d3	; type
	move.l	36(sp),d4	; size
	move.l	_DOSBase,a6
	jsr	-366(a6)	; SetVBuf()
	movem.l	(sp)+,d1-d4/a6
	rts

; DupLockFromFH(fh)
	xdef	_DupLockFromFH
_DupLockFromFH:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	_DOSBase,a6
	jsr	-372(a6)	; DupLockFromFH()
	movem.l	(sp)+,d1/a6
	rts

; OpenFromLock(lock)
	xdef	_OpenFromLock
_OpenFromLock:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; lock
	move.l	_DOSBase,a6
	jsr	-378(a6)	; OpenFromLock()
	movem.l	(sp)+,d1/a6
	rts

; ParentOfFH(fh)
	xdef	_ParentOfFH
_ParentOfFH:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	_DOSBase,a6
	jsr	-384(a6)	; ParentOfFH()
	movem.l	(sp)+,d1/a6
	rts

; ExamineFH(fh, fib)
	xdef	_ExamineFH
_ExamineFH:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; fh
	move.l	20(sp),d2	; fib
	move.l	_DOSBase,a6
	jsr	-390(a6)	; ExamineFH()
	movem.l	(sp)+,d1-d2/a6
	rts

; SetFileDate(name, date)
	xdef	_SetFileDate
_SetFileDate:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; date
	move.l	_DOSBase,a6
	jsr	-396(a6)	; SetFileDate()
	movem.l	(sp)+,d1-d2/a6
	rts

; NameFromLock(lock, buffer, len)
	xdef	_NameFromLock
_NameFromLock:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; lock
	move.l	16(sp),d2	; buffer
	move.l	20(sp),d3	; len
	move.l	_DOSBase,a6
	jsr	-402(a6)	; NameFromLock()
	movem.l	(sp)+,d1-d3/a6
	rts

; NameFromFH(fh, buffer, len)
	xdef	_NameFromFH
_NameFromFH:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	16(sp),d2	; buffer
	move.l	20(sp),d3	; len
	move.l	_DOSBase,a6
	jsr	-408(a6)	; NameFromFH()
	movem.l	(sp)+,d1-d3/a6
	rts

; SplitName(name, separator, buf, oldpos, size)
	xdef	_SplitName
_SplitName:
	movem.l	d1-d5/a6,-(sp)
	move.l	28(sp),d1	; name
	move.l	32(sp),d2	; separator
	move.l	36(sp),d3	; buf
	move.l	40(sp),d4	; oldpos
	move.l	44(sp),d5	; size
	move.l	_DOSBase,a6
	jsr	-414(a6)	; SplitName()
	movem.l	(sp)+,d1-d5/a6
	rts

; SameLock(lock1, lock2)
	xdef	_SameLock
_SameLock:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; lock1
	move.l	20(sp),d2	; lock2
	move.l	_DOSBase,a6
	jsr	-420(a6)	; SameLock()
	movem.l	(sp)+,d1-d2/a6
	rts

; SetMode(fh, mode)
	xdef	_SetMode
_SetMode:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; fh
	move.l	20(sp),d2	; mode
	move.l	_DOSBase,a6
	jsr	-426(a6)	; SetMode()
	movem.l	(sp)+,d1-d2/a6
	rts

; ExAll(lock, buffer, size, data, control)
	xdef	_ExAll
_ExAll:
	movem.l	d1-d5/a6,-(sp)
	move.l	28(sp),d1	; lock
	move.l	32(sp),d2	; buffer
	move.l	36(sp),d3	; size
	move.l	40(sp),d4	; data
	move.l	44(sp),d5	; control
	move.l	_DOSBase,a6
	jsr	-432(a6)	; ExAll()
	movem.l	(sp)+,d1-d5/a6
	rts

; ReadLink(port, lock, path, buffer, size)
	xdef	_ReadLink
_ReadLink:
	movem.l	d1-d5/a6,-(sp)
	move.l	28(sp),d1	; port
	move.l	32(sp),d2	; lock
	move.l	36(sp),d3	; path
	move.l	40(sp),d4	; buffer
	move.l	44(sp),d5	; size
	move.l	_DOSBase,a6
	jsr	-438(a6)	; ReadLink()
	movem.l	(sp)+,d1-d5/a6
	rts

; MakeLink(name, dest, soft)
	xdef	_MakeLink
_MakeLink:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	16(sp),d2	; dest
	move.l	20(sp),d3	; soft
	move.l	_DOSBase,a6
	jsr	-444(a6)	; MakeLink()
	movem.l	(sp)+,d1-d3/a6
	rts

; ChangeMode(type, fh, newmode)
	xdef	_ChangeMode
_ChangeMode:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; type
	move.l	16(sp),d2	; fh
	move.l	20(sp),d3	; newmode
	move.l	_DOSBase,a6
	jsr	-450(a6)	; ChangeMode()
	movem.l	(sp)+,d1-d3/a6
	rts

; SetFileSize(fh, pos, mode)
	xdef	_SetFileSize
_SetFileSize:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; fh
	move.l	16(sp),d2	; pos
	move.l	20(sp),d3	; mode
	move.l	_DOSBase,a6
	jsr	-456(a6)	; SetFileSize()
	movem.l	(sp)+,d1-d3/a6
	rts

; SetIoErr(result)
	xdef	_SetIoErr
_SetIoErr:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; result
	move.l	_DOSBase,a6
	jsr	-462(a6)	; SetIoErr()
	movem.l	(sp)+,d1/a6
	rts

; Fault(code, header, buffer, len)
	xdef	_Fault
_Fault:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; code
	move.l	28(sp),d2	; header
	move.l	32(sp),d3	; buffer
	move.l	36(sp),d4	; len
	move.l	_DOSBase,a6
	jsr	-468(a6)	; Fault()
	movem.l	(sp)+,d1-d4/a6
	rts

; PrintFault(code, header)
	xdef	_PrintFault
_PrintFault:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; code
	move.l	20(sp),d2	; header
	move.l	_DOSBase,a6
	jsr	-474(a6)	; PrintFault()
	movem.l	(sp)+,d1-d2/a6
	rts

; ErrorReport(code, type, arg1, device)
	xdef	_ErrorReport
_ErrorReport:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; code
	move.l	28(sp),d2	; type
	move.l	32(sp),d3	; arg1
	move.l	36(sp),d4	; device
	move.l	_DOSBase,a6
	jsr	-480(a6)	; ErrorReport()
	movem.l	(sp)+,d1-d4/a6
	rts

; Cli()
	xdef	_Cli
_Cli:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-492(a6)	; Cli()
	movem.l	(sp)+,a6
	rts

; CreateNewProc(tags)
	xdef	_CreateNewProc
_CreateNewProc:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; tags
	move.l	_DOSBase,a6
	jsr	-498(a6)	; CreateNewProc()
	movem.l	(sp)+,d1/a6
	rts

; RunCommand(seg, stack, paramptr, paramlen)
	xdef	_RunCommand
_RunCommand:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; seg
	move.l	28(sp),d2	; stack
	move.l	32(sp),d3	; paramptr
	move.l	36(sp),d4	; paramlen
	move.l	_DOSBase,a6
	jsr	-504(a6)	; RunCommand()
	movem.l	(sp)+,d1-d4/a6
	rts

; GetConsoleTask()
	xdef	_GetConsoleTask
_GetConsoleTask:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-510(a6)	; GetConsoleTask()
	movem.l	(sp)+,a6
	rts

; SetConsoleTask(task)
	xdef	_SetConsoleTask
_SetConsoleTask:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; task
	move.l	_DOSBase,a6
	jsr	-516(a6)	; SetConsoleTask()
	movem.l	(sp)+,d1/a6
	rts

; GetFileSysTask()
	xdef	_GetFileSysTask
_GetFileSysTask:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-522(a6)	; GetFileSysTask()
	movem.l	(sp)+,a6
	rts

; SetFileSysTask(task)
	xdef	_SetFileSysTask
_SetFileSysTask:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; task
	move.l	_DOSBase,a6
	jsr	-528(a6)	; SetFileSysTask()
	movem.l	(sp)+,d1/a6
	rts

; GetArgStr()
	xdef	_GetArgStr
_GetArgStr:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-534(a6)	; GetArgStr()
	movem.l	(sp)+,a6
	rts

; SetArgStr(string)
	xdef	_SetArgStr
_SetArgStr:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; string
	move.l	_DOSBase,a6
	jsr	-540(a6)	; SetArgStr()
	movem.l	(sp)+,d1/a6
	rts

; FindCliProc(num)
	xdef	_FindCliProc
_FindCliProc:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; num
	move.l	_DOSBase,a6
	jsr	-546(a6)	; FindCliProc()
	movem.l	(sp)+,d1/a6
	rts

; MaxCli()
	xdef	_MaxCli
_MaxCli:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-552(a6)	; MaxCli()
	movem.l	(sp)+,a6
	rts

; SetCurrentDirName(name)
	xdef	_SetCurrentDirName
_SetCurrentDirName:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-558(a6)	; SetCurrentDirName()
	movem.l	(sp)+,d1/a6
	rts

; GetCurrentDirName(buf, len)
	xdef	_GetCurrentDirName
_GetCurrentDirName:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; buf
	move.l	20(sp),d2	; len
	move.l	_DOSBase,a6
	jsr	-564(a6)	; GetCurrentDirName()
	movem.l	(sp)+,d1-d2/a6
	rts

; SetProgramName(name)
	xdef	_SetProgramName
_SetProgramName:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-570(a6)	; SetProgramName()
	movem.l	(sp)+,d1/a6
	rts

; GetProgramName(buf, len)
	xdef	_GetProgramName
_GetProgramName:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; buf
	move.l	20(sp),d2	; len
	move.l	_DOSBase,a6
	jsr	-576(a6)	; GetProgramName()
	movem.l	(sp)+,d1-d2/a6
	rts

; SetPrompt(name)
	xdef	_SetPrompt
_SetPrompt:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-582(a6)	; SetPrompt()
	movem.l	(sp)+,d1/a6
	rts

; GetPrompt(buf, len)
	xdef	_GetPrompt
_GetPrompt:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; buf
	move.l	20(sp),d2	; len
	move.l	_DOSBase,a6
	jsr	-588(a6)	; GetPrompt()
	movem.l	(sp)+,d1-d2/a6
	rts

; SetProgramDir(lock)
	xdef	_SetProgramDir
_SetProgramDir:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; lock
	move.l	_DOSBase,a6
	jsr	-594(a6)	; SetProgramDir()
	movem.l	(sp)+,d1/a6
	rts

; GetProgramDir()
	xdef	_GetProgramDir
_GetProgramDir:
	movem.l	a6,-(sp)
	move.l	_DOSBase,a6
	jsr	-600(a6)	; GetProgramDir()
	movem.l	(sp)+,a6
	rts

; SystemTagList(command, tags)
	xdef	_SystemTagList
_SystemTagList:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; command
	move.l	20(sp),d2	; tags
	move.l	_DOSBase,a6
	jsr	-606(a6)	; SystemTagList()
	movem.l	(sp)+,d1-d2/a6
	rts

; AssignLock(name, lock)
	xdef	_AssignLock
_AssignLock:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; lock
	move.l	_DOSBase,a6
	jsr	-612(a6)	; AssignLock()
	movem.l	(sp)+,d1-d2/a6
	rts

; AssignLate(name, path)
	xdef	_AssignLate
_AssignLate:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; path
	move.l	_DOSBase,a6
	jsr	-618(a6)	; AssignLate()
	movem.l	(sp)+,d1-d2/a6
	rts

; AssignPath(name, path)
	xdef	_AssignPath
_AssignPath:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; path
	move.l	_DOSBase,a6
	jsr	-624(a6)	; AssignPath()
	movem.l	(sp)+,d1-d2/a6
	rts

; AssignAdd(name, lock)
	xdef	_AssignAdd
_AssignAdd:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; lock
	move.l	_DOSBase,a6
	jsr	-630(a6)	; AssignAdd()
	movem.l	(sp)+,d1-d2/a6
	rts

; RemAssignList(name, lock)
	xdef	_RemAssignList
_RemAssignList:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; lock
	move.l	_DOSBase,a6
	jsr	-636(a6)	; RemAssignList()
	movem.l	(sp)+,d1-d2/a6
	rts

; GetDeviceProc(name, dp)
	xdef	_GetDeviceProc
_GetDeviceProc:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; dp
	move.l	_DOSBase,a6
	jsr	-642(a6)	; GetDeviceProc()
	movem.l	(sp)+,d1-d2/a6
	rts

; FreeDeviceProc(dp)
	xdef	_FreeDeviceProc
_FreeDeviceProc:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; dp
	move.l	_DOSBase,a6
	jsr	-648(a6)	; FreeDeviceProc()
	movem.l	(sp)+,d1/a6
	rts

; LockDosList(flags)
	xdef	_LockDosList
_LockDosList:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; flags
	move.l	_DOSBase,a6
	jsr	-654(a6)	; LockDosList()
	movem.l	(sp)+,d1/a6
	rts

; UnLockDosList(flags)
	xdef	_UnLockDosList
_UnLockDosList:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; flags
	move.l	_DOSBase,a6
	jsr	-660(a6)	; UnLockDosList()
	movem.l	(sp)+,d1/a6
	rts

; AttemptLockDosList(flags)
	xdef	_AttemptLockDosList
_AttemptLockDosList:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; flags
	move.l	_DOSBase,a6
	jsr	-666(a6)	; AttemptLockDosList()
	movem.l	(sp)+,d1/a6
	rts

; RemDosEntry(dlist)
	xdef	_RemDosEntry
_RemDosEntry:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; dlist
	move.l	_DOSBase,a6
	jsr	-672(a6)	; RemDosEntry()
	movem.l	(sp)+,d1/a6
	rts

; AddDosEntry(dlist)
	xdef	_AddDosEntry
_AddDosEntry:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; dlist
	move.l	_DOSBase,a6
	jsr	-678(a6)	; AddDosEntry()
	movem.l	(sp)+,d1/a6
	rts

; FindDosEntry(dlist, name, flags)
	xdef	_FindDosEntry
_FindDosEntry:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; dlist
	move.l	16(sp),d2	; name
	move.l	20(sp),d3	; flags
	move.l	_DOSBase,a6
	jsr	-684(a6)	; FindDosEntry()
	movem.l	(sp)+,d1-d3/a6
	rts

; NextDosEntry(dlist, flags)
	xdef	_NextDosEntry
_NextDosEntry:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; dlist
	move.l	20(sp),d2	; flags
	move.l	_DOSBase,a6
	jsr	-690(a6)	; NextDosEntry()
	movem.l	(sp)+,d1-d2/a6
	rts

; MakeDosEntry(name, type)
	xdef	_MakeDosEntry
_MakeDosEntry:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; type
	move.l	_DOSBase,a6
	jsr	-696(a6)	; MakeDosEntry()
	movem.l	(sp)+,d1-d2/a6
	rts

; FreeDosEntry(dlist)
	xdef	_FreeDosEntry
_FreeDosEntry:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; dlist
	move.l	_DOSBase,a6
	jsr	-702(a6)	; FreeDosEntry()
	movem.l	(sp)+,d1/a6
	rts

; IsFileSystem(name)
	xdef	_IsFileSystem
_IsFileSystem:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	_DOSBase,a6
	jsr	-708(a6)	; IsFileSystem()
	movem.l	(sp)+,d1/a6
	rts

; Format(filesystem, volumename, dostype)
	xdef	_Format
_Format:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; filesystem
	move.l	16(sp),d2	; volumename
	move.l	20(sp),d3	; dostype
	move.l	_DOSBase,a6
	jsr	-714(a6)	; Format()
	movem.l	(sp)+,d1-d3/a6
	rts

; Relabel(drive, newname)
	xdef	_Relabel
_Relabel:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; drive
	move.l	20(sp),d2	; newname
	move.l	_DOSBase,a6
	jsr	-720(a6)	; Relabel()
	movem.l	(sp)+,d1-d2/a6
	rts

; Inhibit(name, onoff)
	xdef	_Inhibit
_Inhibit:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; onoff
	move.l	_DOSBase,a6
	jsr	-726(a6)	; Inhibit()
	movem.l	(sp)+,d1-d2/a6
	rts

; AddBuffers(name, number)
	xdef	_AddBuffers
_AddBuffers:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; number
	move.l	_DOSBase,a6
	jsr	-732(a6)	; AddBuffers()
	movem.l	(sp)+,d1-d2/a6
	rts

; CompareDates(date1, date2)
	xdef	_CompareDates
_CompareDates:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; date1
	move.l	20(sp),d2	; date2
	move.l	_DOSBase,a6
	jsr	-738(a6)	; CompareDates()
	movem.l	(sp)+,d1-d2/a6
	rts

; DateToStr(datetime)
	xdef	_DateToStr
_DateToStr:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; datetime
	move.l	_DOSBase,a6
	jsr	-744(a6)	; DateToStr()
	movem.l	(sp)+,d1/a6
	rts

; StrToDate(datetime)
	xdef	_StrToDate
_StrToDate:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; datetime
	move.l	_DOSBase,a6
	jsr	-750(a6)	; StrToDate()
	movem.l	(sp)+,d1/a6
	rts

; InternalLoadSeg(fh, table, funcarray, stack)
	xdef	_InternalLoadSeg
_InternalLoadSeg:
	movem.l	d0/a0-a2/a6,-(sp)
	move.l	16(sp),d0	; fh
	move.l	20(sp),a0	; table
	move.l	24(sp),a1	; funcarray
	move.l	28(sp),a2	; stack
	move.l	_DOSBase,a6
	jsr	-756(a6)	; InternalLoadSeg()
	movem.l	(sp)+,d0/a0-a2/a6
	rts

; InternalUnLoadSeg(seglist, freefunc)
	xdef	_InternalUnLoadSeg
_InternalUnLoadSeg:
	movem.l	d1/a1/a6,-(sp)
	move.l	16(sp),d1	; seglist
	move.l	20(sp),a1	; freefunc
	move.l	_DOSBase,a6
	jsr	-762(a6)	; InternalUnLoadSeg()
	movem.l	(sp)+,d1/a1/a6
	rts

; NewLoadSeg(file, tags)
	xdef	_NewLoadSeg
_NewLoadSeg:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; file
	move.l	20(sp),d2	; tags
	move.l	_DOSBase,a6
	jsr	-768(a6)	; NewLoadSeg()
	movem.l	(sp)+,d1-d2/a6
	rts

; AddSegment(name, seg, system)
	xdef	_AddSegment
_AddSegment:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	16(sp),d2	; seg
	move.l	20(sp),d3	; system
	move.l	_DOSBase,a6
	jsr	-774(a6)	; AddSegment()
	movem.l	(sp)+,d1-d3/a6
	rts

; FindSegment(name, seg, system)
	xdef	_FindSegment
_FindSegment:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	16(sp),d2	; seg
	move.l	20(sp),d3	; system
	move.l	_DOSBase,a6
	jsr	-780(a6)	; FindSegment()
	movem.l	(sp)+,d1-d3/a6
	rts

; RemSegment(seg)
	xdef	_RemSegment
_RemSegment:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; seg
	move.l	_DOSBase,a6
	jsr	-786(a6)	; RemSegment()
	movem.l	(sp)+,d1/a6
	rts

; CheckSignal(mask)
	xdef	_CheckSignal
_CheckSignal:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; mask
	move.l	_DOSBase,a6
	jsr	-792(a6)	; CheckSignal()
	movem.l	(sp)+,d1/a6
	rts

; ReadArgs(arg_template, array, args)
	xdef	_ReadArgs
_ReadArgs:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; arg_template
	move.l	16(sp),d2	; array
	move.l	20(sp),d3	; args
	move.l	_DOSBase,a6
	jsr	-798(a6)	; ReadArgs()
	movem.l	(sp)+,d1-d3/a6
	rts

; FindArg(keyword, arg_template)
	xdef	_FindArg
_FindArg:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; keyword
	move.l	20(sp),d2	; arg_template
	move.l	_DOSBase,a6
	jsr	-804(a6)	; FindArg()
	movem.l	(sp)+,d1-d2/a6
	rts

; ReadItem(name, maxchars, cSource)
	xdef	_ReadItem
_ReadItem:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; name
	move.l	16(sp),d2	; maxchars
	move.l	20(sp),d3	; cSource
	move.l	_DOSBase,a6
	jsr	-810(a6)	; ReadItem()
	movem.l	(sp)+,d1-d3/a6
	rts

; StrToLong(string, value)
	xdef	_StrToLong
_StrToLong:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; string
	move.l	20(sp),d2	; value
	move.l	_DOSBase,a6
	jsr	-816(a6)	; StrToLong()
	movem.l	(sp)+,d1-d2/a6
	rts

; MatchFirst(pat, anchor)
	xdef	_MatchFirst
_MatchFirst:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; pat
	move.l	20(sp),d2	; anchor
	move.l	_DOSBase,a6
	jsr	-822(a6)	; MatchFirst()
	movem.l	(sp)+,d1-d2/a6
	rts

; MatchNext(anchor)
	xdef	_MatchNext
_MatchNext:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; anchor
	move.l	_DOSBase,a6
	jsr	-828(a6)	; MatchNext()
	movem.l	(sp)+,d1/a6
	rts

; MatchEnd(anchor)
	xdef	_MatchEnd
_MatchEnd:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; anchor
	move.l	_DOSBase,a6
	jsr	-834(a6)	; MatchEnd()
	movem.l	(sp)+,d1/a6
	rts

; ParsePattern(pat, buf, buflen)
	xdef	_ParsePattern
_ParsePattern:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; pat
	move.l	16(sp),d2	; buf
	move.l	20(sp),d3	; buflen
	move.l	_DOSBase,a6
	jsr	-840(a6)	; ParsePattern()
	movem.l	(sp)+,d1-d3/a6
	rts

; MatchPattern(pat, str)
	xdef	_MatchPattern
_MatchPattern:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; pat
	move.l	20(sp),d2	; str
	move.l	_DOSBase,a6
	jsr	-846(a6)	; MatchPattern()
	movem.l	(sp)+,d1-d2/a6
	rts

; FreeArgs(args)
	xdef	_FreeArgs
_FreeArgs:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; args
	move.l	_DOSBase,a6
	jsr	-858(a6)	; FreeArgs()
	movem.l	(sp)+,d1/a6
	rts

; FilePart(path)
	xdef	_FilePart
_FilePart:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; path
	move.l	_DOSBase,a6
	jsr	-870(a6)	; FilePart()
	movem.l	(sp)+,d1/a6
	rts

; PathPart(path)
	xdef	_PathPart
_PathPart:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; path
	move.l	_DOSBase,a6
	jsr	-876(a6)	; PathPart()
	movem.l	(sp)+,d1/a6
	rts

; AddPart(dirname, filename, size)
	xdef	_AddPart
_AddPart:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; dirname
	move.l	16(sp),d2	; filename
	move.l	20(sp),d3	; size
	move.l	_DOSBase,a6
	jsr	-882(a6)	; AddPart()
	movem.l	(sp)+,d1-d3/a6
	rts

; StartNotify(notify)
	xdef	_StartNotify
_StartNotify:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; notify
	move.l	_DOSBase,a6
	jsr	-888(a6)	; StartNotify()
	movem.l	(sp)+,d1/a6
	rts

; EndNotify(notify)
	xdef	_EndNotify
_EndNotify:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; notify
	move.l	_DOSBase,a6
	jsr	-894(a6)	; EndNotify()
	movem.l	(sp)+,d1/a6
	rts

; SetVar(name, buffer, size, flags)
	xdef	_SetVar
_SetVar:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; name
	move.l	28(sp),d2	; buffer
	move.l	32(sp),d3	; size
	move.l	36(sp),d4	; flags
	move.l	_DOSBase,a6
	jsr	-900(a6)	; SetVar()
	movem.l	(sp)+,d1-d4/a6
	rts

; GetVar(name, buffer, size, flags)
	xdef	_GetVar
_GetVar:
	movem.l	d1-d4/a6,-(sp)
	move.l	24(sp),d1	; name
	move.l	28(sp),d2	; buffer
	move.l	32(sp),d3	; size
	move.l	36(sp),d4	; flags
	move.l	_DOSBase,a6
	jsr	-906(a6)	; GetVar()
	movem.l	(sp)+,d1-d4/a6
	rts

; DeleteVar(name, flags)
	xdef	_DeleteVar
_DeleteVar:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; flags
	move.l	_DOSBase,a6
	jsr	-912(a6)	; DeleteVar()
	movem.l	(sp)+,d1-d2/a6
	rts

; FindVar(name, type)
	xdef	_FindVar
_FindVar:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; type
	move.l	_DOSBase,a6
	jsr	-918(a6)	; FindVar()
	movem.l	(sp)+,d1-d2/a6
	rts

; CliInitNewcli(dp)
	xdef	_CliInitNewcli
_CliInitNewcli:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; dp
	move.l	_DOSBase,a6
	jsr	-930(a6)	; CliInitNewcli()
	movem.l	(sp)+,a0/a6
	rts

; CliInitRun(dp)
	xdef	_CliInitRun
_CliInitRun:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; dp
	move.l	_DOSBase,a6
	jsr	-936(a6)	; CliInitRun()
	movem.l	(sp)+,a0/a6
	rts

; WriteChars(buf, buflen)
	xdef	_WriteChars
_WriteChars:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; buf
	move.l	20(sp),d2	; buflen
	move.l	_DOSBase,a6
	jsr	-942(a6)	; WriteChars()
	movem.l	(sp)+,d1-d2/a6
	rts

; PutStr(str)
	xdef	_PutStr
_PutStr:
	movem.l	d1/a6,-(sp)
	move.l	12(sp),d1	; str
	move.l	_DOSBase,a6
	jsr	-948(a6)	; PutStr()
	movem.l	(sp)+,d1/a6
	rts

; VPrintf(format, argarray)
	xdef	_VPrintf
_VPrintf:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; format
	move.l	20(sp),d2	; argarray
	move.l	_DOSBase,a6
	jsr	-954(a6)	; VPrintf()
	movem.l	(sp)+,d1-d2/a6
	rts

; ParsePatternNoCase(pat, buf, buflen)
	xdef	_ParsePatternNoCase
_ParsePatternNoCase:
	movem.l	d1-d3/a6,-(sp)
	move.l	12(sp),d1	; pat
	move.l	16(sp),d2	; buf
	move.l	20(sp),d3	; buflen
	move.l	_DOSBase,a6
	jsr	-966(a6)	; ParsePatternNoCase()
	movem.l	(sp)+,d1-d3/a6
	rts

; MatchPatternNoCase(pat, str)
	xdef	_MatchPatternNoCase
_MatchPatternNoCase:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; pat
	move.l	20(sp),d2	; str
	move.l	_DOSBase,a6
	jsr	-972(a6)	; MatchPatternNoCase()
	movem.l	(sp)+,d1-d2/a6
	rts

; SameDevice(lock1, lock2)
	xdef	_SameDevice
_SameDevice:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; lock1
	move.l	20(sp),d2	; lock2
	move.l	_DOSBase,a6
	jsr	-984(a6)	; SameDevice()
	movem.l	(sp)+,d1-d2/a6
	rts

; ExAllEnd(lock, buffer, size, data, control)
	xdef	_ExAllEnd
_ExAllEnd:
	movem.l	d1-d5/a6,-(sp)
	move.l	28(sp),d1	; lock
	move.l	32(sp),d2	; buffer
	move.l	36(sp),d3	; size
	move.l	40(sp),d4	; data
	move.l	44(sp),d5	; control
	move.l	_DOSBase,a6
	jsr	-990(a6)	; ExAllEnd()
	movem.l	(sp)+,d1-d5/a6
	rts

; SetOwner(name, owner_info)
	xdef	_SetOwner
_SetOwner:
	movem.l	d1-d2/a6,-(sp)
	move.l	16(sp),d1	; name
	move.l	20(sp),d2	; owner_info
	move.l	_DOSBase,a6
	jsr	-996(a6)	; SetOwner()
	movem.l	(sp)+,d1-d2/a6
	rts

