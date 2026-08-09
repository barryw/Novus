; Generated from SFD file by Novus SFD Parser
; Library: dos.library
; Base: _DOSBase
; Each function is in its own section for dead code elimination

	xref	_DOSBase

	section	_Open_stub,code

; BPTR Open(CONST_STRPTR name, LONG accessMode)
	xdef	_Open
_Open:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_Close_stub,code

; LONG Close(BPTR file)
	xdef	_Close
_Close:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_Read_stub,code

; LONG Read(BPTR file, APTR buffer, LONG length)
	xdef	_Read
_Read:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_Write_stub,code

; LONG Write(BPTR file, const APTR buffer, LONG length)
	xdef	_Write
_Write:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_Input_stub,code

; BPTR Input()
	xdef	_Input
_Input:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_Output_stub,code

; BPTR Output()
	xdef	_Output
_Output:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_Seek_stub,code

; LONG Seek(BPTR file, LONG position, LONG offset)
	xdef	_Seek
_Seek:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_DeleteFile_stub,code

; LONG DeleteFile(CONST_STRPTR name)
	xdef	_DeleteFile
_DeleteFile:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_Rename_stub,code

; LONG Rename(CONST_STRPTR oldName, CONST_STRPTR newName)
	xdef	_Rename
_Rename:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_Lock_stub,code

; BPTR Lock(CONST_STRPTR name, LONG type)
	xdef	_Lock
_Lock:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_UnLock_stub,code

; VOID UnLock(BPTR lock)
	xdef	_UnLock
_UnLock:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_DupLock_stub,code

; BPTR DupLock(BPTR lock)
	xdef	_DupLock
_DupLock:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_Examine_stub,code

; LONG Examine(BPTR lock, struct FileInfoBlock * fileInfoBlock)
	xdef	_Examine
_Examine:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ExNext_stub,code

; LONG ExNext(BPTR lock, struct FileInfoBlock * fileInfoBlock)
	xdef	_ExNext
_ExNext:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_Info_stub,code

; LONG Info(BPTR lock, struct InfoData * parameterBlock)
	xdef	_Info
_Info:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_CreateDir_stub,code

; BPTR CreateDir(CONST_STRPTR name)
	xdef	_CreateDir
_CreateDir:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_CurrentDir_stub,code

; BPTR CurrentDir(BPTR lock)
	xdef	_CurrentDir
_CurrentDir:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_IoErr_stub,code

; LONG IoErr()
	xdef	_IoErr
_IoErr:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateProc_stub,code

; struct MsgPort * CreateProc(CONST_STRPTR name, LONG pri, BPTR segList, LONG stackSize)
	xdef	_CreateProc
_CreateProc:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_Exit_stub,code

; VOID Exit(LONG returnCode)
	xdef	_Exit
_Exit:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_LoadSeg_stub,code

; BPTR LoadSeg(CONST_STRPTR name)
	xdef	_LoadSeg
_LoadSeg:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnLoadSeg_stub,code

; VOID UnLoadSeg(BPTR seglist)
	xdef	_UnLoadSeg
_UnLoadSeg:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a6
	rts

	section	_DeviceProc_stub,code

; struct MsgPort * DeviceProc(CONST_STRPTR name)
	xdef	_DeviceProc
_DeviceProc:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetComment_stub,code

; LONG SetComment(CONST_STRPTR name, CONST_STRPTR comment)
	xdef	_SetComment
_SetComment:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SetProtection_stub,code

; LONG SetProtection(CONST_STRPTR name, LONG protect)
	xdef	_SetProtection
_SetProtection:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-186(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_DateStamp_stub,code

; struct DateStamp * DateStamp(struct DateStamp * date)
	xdef	_DateStamp
_DateStamp:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-192(a6)
	movem.l	(sp)+,a6
	rts

	section	_Delay_stub,code

; VOID Delay(LONG timeout)
	xdef	_Delay
_Delay:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-198(a6)
	movem.l	(sp)+,a6
	rts

	section	_WaitForChar_stub,code

; LONG WaitForChar(BPTR file, LONG timeout)
	xdef	_WaitForChar
_WaitForChar:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-204(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ParentDir_stub,code

; BPTR ParentDir(BPTR lock)
	xdef	_ParentDir
_ParentDir:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-210(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsInteractive_stub,code

; LONG IsInteractive(BPTR file)
	xdef	_IsInteractive
_IsInteractive:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-216(a6)
	movem.l	(sp)+,a6
	rts

	section	_Execute_stub,code

; LONG Execute(CONST_STRPTR string, BPTR file, BPTR file2)
	xdef	_Execute
_Execute:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-222(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_AllocDosObject_stub,code

; APTR AllocDosObject(ULONG type, const struct TagItem * tags)
	xdef	_AllocDosObject
_AllocDosObject:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AllocDosObjectTagList_stub,code

; APTR AllocDosObjectTagList(ULONG type, const struct TagItem * tags)
	xdef	_AllocDosObjectTagList
_AllocDosObjectTagList:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AllocDosObjectTags_stub,code

; APTR AllocDosObjectTags(ULONG type, ULONG tags, ... )
	xdef	_AllocDosObjectTags
_AllocDosObjectTags:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	lea	16(sp),a6
	move.l	a6,d2
	movea.l	_DOSBase,a6
	jsr	-228(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_FreeDosObject_stub,code

; VOID FreeDosObject(ULONG type, APTR ptr)
	xdef	_FreeDosObject
_FreeDosObject:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-234(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_DoPkt_stub,code

; LONG DoPkt(struct MsgPort * port, LONG action, LONG arg1, LONG arg2, LONG arg3, LONG arg4, LONG arg5)
	xdef	_DoPkt
_DoPkt:
	movem.l	d2/d3/d4/d5/d6/d7/a6,-(sp)
	move.l	32(sp),d1
	move.l	36(sp),d2
	move.l	40(sp),d3
	move.l	44(sp),d4
	move.l	48(sp),d5
	move.l	52(sp),d6
	move.l	56(sp),d7
	movea.l	_DOSBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,d2/d3/d4/d5/d6/d7/a6
	rts

	section	_DoPkt0_stub,code

; LONG DoPkt0(struct MsgPort * port, LONG action)
	xdef	_DoPkt0
_DoPkt0:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_DoPkt1_stub,code

; LONG DoPkt1(struct MsgPort * port, LONG action, LONG arg1)
	xdef	_DoPkt1
_DoPkt1:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_DoPkt2_stub,code

; LONG DoPkt2(struct MsgPort * port, LONG action, LONG arg1, LONG arg2)
	xdef	_DoPkt2
_DoPkt2:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_DoPkt3_stub,code

; LONG DoPkt3(struct MsgPort * port, LONG action, LONG arg1, LONG arg2, LONG arg3)
	xdef	_DoPkt3
_DoPkt3:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	move.l	36(sp),d4
	move.l	40(sp),d5
	movea.l	_DOSBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_DoPkt4_stub,code

; LONG DoPkt4(struct MsgPort * port, LONG action, LONG arg1, LONG arg2, LONG arg3, LONG arg4)
	xdef	_DoPkt4
_DoPkt4:
	movem.l	d2/d3/d4/d5/d6/a6,-(sp)
	move.l	28(sp),d1
	move.l	32(sp),d2
	move.l	36(sp),d3
	move.l	40(sp),d4
	move.l	44(sp),d5
	move.l	48(sp),d6
	movea.l	_DOSBase,a6
	jsr	-240(a6)
	movem.l	(sp)+,d2/d3/d4/d5/d6/a6
	rts

	section	_SendPkt_stub,code

; VOID SendPkt(struct DosPacket * dp, struct MsgPort * port, struct MsgPort * replyport)
	xdef	_SendPkt
_SendPkt:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-246(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_WaitPkt_stub,code

; struct DosPacket * WaitPkt()
	xdef	_WaitPkt
_WaitPkt:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-252(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReplyPkt_stub,code

; VOID ReplyPkt(struct DosPacket * dp, LONG res1, LONG res2)
	xdef	_ReplyPkt
_ReplyPkt:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-258(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_AbortPkt_stub,code

; VOID AbortPkt(struct MsgPort * port, struct DosPacket * pkt)
	xdef	_AbortPkt
_AbortPkt:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-264(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_LockRecord_stub,code

; BOOL LockRecord(BPTR fh, ULONG offset, ULONG length, ULONG mode, ULONG timeout)
	xdef	_LockRecord
_LockRecord:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	move.l	36(sp),d4
	move.l	40(sp),d5
	movea.l	_DOSBase,a6
	jsr	-270(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_LockRecords_stub,code

; BOOL LockRecords(struct RecordLock * recArray, ULONG timeout)
	xdef	_LockRecords
_LockRecords:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-276(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_UnLockRecord_stub,code

; BOOL UnLockRecord(BPTR fh, ULONG offset, ULONG length)
	xdef	_UnLockRecord
_UnLockRecord:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-282(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_UnLockRecords_stub,code

; BOOL UnLockRecords(struct RecordLock * recArray)
	xdef	_UnLockRecords
_UnLockRecords:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-288(a6)
	movem.l	(sp)+,a6
	rts

	section	_SelectInput_stub,code

; BPTR SelectInput(BPTR fh)
	xdef	_SelectInput
_SelectInput:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-294(a6)
	movem.l	(sp)+,a6
	rts

	section	_SelectOutput_stub,code

; BPTR SelectOutput(BPTR fh)
	xdef	_SelectOutput
_SelectOutput:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-300(a6)
	movem.l	(sp)+,a6
	rts

	section	_FGetC_stub,code

; LONG FGetC(BPTR fh)
	xdef	_FGetC
_FGetC:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-306(a6)
	movem.l	(sp)+,a6
	rts

	section	_FPutC_stub,code

; LONG FPutC(BPTR fh, LONG ch)
	xdef	_FPutC
_FPutC:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-312(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_UnGetC_stub,code

; LONG UnGetC(BPTR fh, LONG character)
	xdef	_UnGetC
_UnGetC:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-318(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_FRead_stub,code

; LONG FRead(BPTR fh, APTR block, ULONG blocklen, ULONG number)
	xdef	_FRead
_FRead:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-324(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_FWrite_stub,code

; LONG FWrite(BPTR fh, const APTR block, ULONG blocklen, ULONG number)
	xdef	_FWrite
_FWrite:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-330(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_FGets_stub,code

; STRPTR FGets(BPTR fh, STRPTR buf, ULONG buflen)
	xdef	_FGets
_FGets:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-336(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_FPuts_stub,code

; LONG FPuts(BPTR fh, CONST_STRPTR str)
	xdef	_FPuts
_FPuts:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-342(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_VFWritef_stub,code

; VOID VFWritef(BPTR fh, CONST_STRPTR format, const LONG * argarray)
	xdef	_VFWritef
_VFWritef:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-348(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_FWritef_stub,code

; VOID FWritef(BPTR fh, CONST_STRPTR format, ... )
	xdef	_FWritef
_FWritef:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	lea	24(sp),a6
	move.l	a6,d3
	movea.l	_DOSBase,a6
	jsr	-348(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_VFPrintf_stub,code

; LONG VFPrintf(BPTR fh, CONST_STRPTR format, const APTR argarray)
	xdef	_VFPrintf
_VFPrintf:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-354(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_FPrintf_stub,code

; LONG FPrintf(BPTR fh, CONST_STRPTR format, ... )
	xdef	_FPrintf
_FPrintf:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	lea	24(sp),a6
	move.l	a6,d3
	movea.l	_DOSBase,a6
	jsr	-354(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_Flush_stub,code

; LONG Flush(BPTR fh)
	xdef	_Flush
_Flush:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-360(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetVBuf_stub,code

; LONG SetVBuf(BPTR fh, STRPTR buff, LONG type, LONG size)
	xdef	_SetVBuf
_SetVBuf:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-366(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_DupLockFromFH_stub,code

; BPTR DupLockFromFH(BPTR fh)
	xdef	_DupLockFromFH
_DupLockFromFH:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-372(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenFromLock_stub,code

; BPTR OpenFromLock(BPTR lock)
	xdef	_OpenFromLock
_OpenFromLock:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-378(a6)
	movem.l	(sp)+,a6
	rts

	section	_ParentOfFH_stub,code

; BPTR ParentOfFH(BPTR fh)
	xdef	_ParentOfFH
_ParentOfFH:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-384(a6)
	movem.l	(sp)+,a6
	rts

	section	_ExamineFH_stub,code

; BOOL ExamineFH(BPTR fh, struct FileInfoBlock * fib)
	xdef	_ExamineFH
_ExamineFH:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-390(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SetFileDate_stub,code

; LONG SetFileDate(CONST_STRPTR name, const struct DateStamp * date)
	xdef	_SetFileDate
_SetFileDate:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-396(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_NameFromLock_stub,code

; LONG NameFromLock(BPTR lock, STRPTR buffer, LONG len)
	xdef	_NameFromLock
_NameFromLock:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-402(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_NameFromFH_stub,code

; LONG NameFromFH(BPTR fh, STRPTR buffer, LONG len)
	xdef	_NameFromFH
_NameFromFH:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-408(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_SplitName_stub,code

; WORD SplitName(CONST_STRPTR name, UBYTE separator, STRPTR buf, WORD oldpos, LONG size)
	xdef	_SplitName
_SplitName:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	move.l	36(sp),d4
	move.l	40(sp),d5
	movea.l	_DOSBase,a6
	jsr	-414(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_SameLock_stub,code

; LONG SameLock(BPTR lock1, BPTR lock2)
	xdef	_SameLock
_SameLock:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-420(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SetMode_stub,code

; LONG SetMode(BPTR fh, LONG mode)
	xdef	_SetMode
_SetMode:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-426(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ExAll_stub,code

; LONG ExAll(BPTR lock, struct ExAllData * buffer, LONG size, LONG data, struct ExAllControl * control)
	xdef	_ExAll
_ExAll:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	move.l	36(sp),d4
	move.l	40(sp),d5
	movea.l	_DOSBase,a6
	jsr	-432(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_ReadLink_stub,code

; LONG ReadLink(struct MsgPort * port, BPTR lock, CONST_STRPTR path, STRPTR buffer, ULONG size)
	xdef	_ReadLink
_ReadLink:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	move.l	36(sp),d4
	move.l	40(sp),d5
	movea.l	_DOSBase,a6
	jsr	-438(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_MakeLink_stub,code

; LONG MakeLink(CONST_STRPTR name, LONG dest, LONG soft)
	xdef	_MakeLink
_MakeLink:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-444(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_ChangeMode_stub,code

; LONG ChangeMode(LONG type, BPTR fh, LONG newmode)
	xdef	_ChangeMode
_ChangeMode:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-450(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_SetFileSize_stub,code

; LONG SetFileSize(BPTR fh, LONG pos, LONG mode)
	xdef	_SetFileSize
_SetFileSize:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-456(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_SetIoErr_stub,code

; LONG SetIoErr(LONG result)
	xdef	_SetIoErr
_SetIoErr:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-462(a6)
	movem.l	(sp)+,a6
	rts

	section	_Fault_stub,code

; BOOL Fault(LONG code, STRPTR header, STRPTR buffer, LONG len)
	xdef	_Fault
_Fault:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-468(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_PrintFault_stub,code

; BOOL PrintFault(LONG code, CONST_STRPTR header)
	xdef	_PrintFault
_PrintFault:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-474(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ErrorReport_stub,code

; LONG ErrorReport(LONG code, LONG type, ULONG arg1, struct MsgPort * device)
	xdef	_ErrorReport
_ErrorReport:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-480(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_Cli_stub,code

; struct CommandLineInterface * Cli()
	xdef	_Cli
_Cli:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-492(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateNewProc_stub,code

; struct Process * CreateNewProc(const struct TagItem * tags)
	xdef	_CreateNewProc
_CreateNewProc:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-498(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateNewProcTagList_stub,code

; struct Process * CreateNewProcTagList(const struct TagItem * tags)
	xdef	_CreateNewProcTagList
_CreateNewProcTagList:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-498(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateNewProcTags_stub,code

; struct Process * CreateNewProcTags(ULONG tags, ... )
	xdef	_CreateNewProcTags
_CreateNewProcTags:
	movem.l	a6,-(sp)
	lea	8(sp),a6
	move.l	a6,d1
	movea.l	_DOSBase,a6
	jsr	-498(a6)
	movem.l	(sp)+,a6
	rts

	section	_RunCommand_stub,code

; LONG RunCommand(BPTR seg, LONG stack, CONST_STRPTR paramptr, LONG paramlen)
	xdef	_RunCommand
_RunCommand:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-504(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_GetConsoleTask_stub,code

; struct MsgPort * GetConsoleTask()
	xdef	_GetConsoleTask
_GetConsoleTask:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-510(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetConsoleTask_stub,code

; struct MsgPort * SetConsoleTask(const struct MsgPort * task)
	xdef	_SetConsoleTask
_SetConsoleTask:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-516(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetFileSysTask_stub,code

; struct MsgPort * GetFileSysTask()
	xdef	_GetFileSysTask
_GetFileSysTask:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-522(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetFileSysTask_stub,code

; struct MsgPort * SetFileSysTask(const struct MsgPort * task)
	xdef	_SetFileSysTask
_SetFileSysTask:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-528(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetArgStr_stub,code

; STRPTR GetArgStr()
	xdef	_GetArgStr
_GetArgStr:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-534(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetArgStr_stub,code

; BOOL SetArgStr(CONST_STRPTR string)
	xdef	_SetArgStr
_SetArgStr:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-540(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindCliProc_stub,code

; struct Process * FindCliProc(ULONG num)
	xdef	_FindCliProc
_FindCliProc:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-546(a6)
	movem.l	(sp)+,a6
	rts

	section	_MaxCli_stub,code

; ULONG MaxCli()
	xdef	_MaxCli
_MaxCli:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-552(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetCurrentDirName_stub,code

; BOOL SetCurrentDirName(CONST_STRPTR name)
	xdef	_SetCurrentDirName
_SetCurrentDirName:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-558(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetCurrentDirName_stub,code

; BOOL GetCurrentDirName(STRPTR buf, LONG len)
	xdef	_GetCurrentDirName
_GetCurrentDirName:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-564(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SetProgramName_stub,code

; BOOL SetProgramName(CONST_STRPTR name)
	xdef	_SetProgramName
_SetProgramName:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-570(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetProgramName_stub,code

; BOOL GetProgramName(STRPTR buf, LONG len)
	xdef	_GetProgramName
_GetProgramName:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-576(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SetPrompt_stub,code

; BOOL SetPrompt(CONST_STRPTR name)
	xdef	_SetPrompt
_SetPrompt:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-582(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetPrompt_stub,code

; BOOL GetPrompt(STRPTR buf, LONG len)
	xdef	_GetPrompt
_GetPrompt:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-588(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SetProgramDir_stub,code

; BPTR SetProgramDir(BPTR lock)
	xdef	_SetProgramDir
_SetProgramDir:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-594(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetProgramDir_stub,code

; BPTR GetProgramDir()
	xdef	_GetProgramDir
_GetProgramDir:
	movem.l	a6,-(sp)
	movea.l	_DOSBase,a6
	jsr	-600(a6)
	movem.l	(sp)+,a6
	rts

	section	_SystemTagList_stub,code

; LONG SystemTagList(CONST_STRPTR command, const struct TagItem * tags)
	xdef	_SystemTagList
_SystemTagList:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-606(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_System_stub,code

; LONG System(CONST_STRPTR command, const struct TagItem * tags)
	xdef	_System
_System:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-606(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SystemTags_stub,code

; LONG SystemTags(CONST_STRPTR command, ULONG tags, ... )
	xdef	_SystemTags
_SystemTags:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	lea	16(sp),a6
	move.l	a6,d2
	movea.l	_DOSBase,a6
	jsr	-606(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AssignLock_stub,code

; LONG AssignLock(CONST_STRPTR name, BPTR lock)
	xdef	_AssignLock
_AssignLock:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-612(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AssignLate_stub,code

; BOOL AssignLate(CONST_STRPTR name, CONST_STRPTR path)
	xdef	_AssignLate
_AssignLate:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-618(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AssignPath_stub,code

; BOOL AssignPath(CONST_STRPTR name, CONST_STRPTR path)
	xdef	_AssignPath
_AssignPath:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-624(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AssignAdd_stub,code

; BOOL AssignAdd(CONST_STRPTR name, BPTR lock)
	xdef	_AssignAdd
_AssignAdd:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-630(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_RemAssignList_stub,code

; LONG RemAssignList(CONST_STRPTR name, BPTR lock)
	xdef	_RemAssignList
_RemAssignList:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-636(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_GetDeviceProc_stub,code

; struct DevProc * GetDeviceProc(CONST_STRPTR name, struct DevProc * dp)
	xdef	_GetDeviceProc
_GetDeviceProc:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-642(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_FreeDeviceProc_stub,code

; VOID FreeDeviceProc(struct DevProc * dp)
	xdef	_FreeDeviceProc
_FreeDeviceProc:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-648(a6)
	movem.l	(sp)+,a6
	rts

	section	_LockDosList_stub,code

; struct DosList * LockDosList(ULONG flags)
	xdef	_LockDosList
_LockDosList:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-654(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnLockDosList_stub,code

; VOID UnLockDosList(ULONG flags)
	xdef	_UnLockDosList
_UnLockDosList:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-660(a6)
	movem.l	(sp)+,a6
	rts

	section	_AttemptLockDosList_stub,code

; struct DosList * AttemptLockDosList(ULONG flags)
	xdef	_AttemptLockDosList
_AttemptLockDosList:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-666(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemDosEntry_stub,code

; BOOL RemDosEntry(struct DosList * dlist)
	xdef	_RemDosEntry
_RemDosEntry:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-672(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddDosEntry_stub,code

; LONG AddDosEntry(struct DosList * dlist)
	xdef	_AddDosEntry
_AddDosEntry:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-678(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindDosEntry_stub,code

; struct DosList * FindDosEntry(const struct DosList * dlist, CONST_STRPTR name, ULONG flags)
	xdef	_FindDosEntry
_FindDosEntry:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-684(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_NextDosEntry_stub,code

; struct DosList * NextDosEntry(const struct DosList * dlist, ULONG flags)
	xdef	_NextDosEntry
_NextDosEntry:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-690(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_MakeDosEntry_stub,code

; struct DosList * MakeDosEntry(CONST_STRPTR name, LONG type)
	xdef	_MakeDosEntry
_MakeDosEntry:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-696(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_FreeDosEntry_stub,code

; VOID FreeDosEntry(struct DosList * dlist)
	xdef	_FreeDosEntry
_FreeDosEntry:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-702(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsFileSystem_stub,code

; BOOL IsFileSystem(CONST_STRPTR name)
	xdef	_IsFileSystem
_IsFileSystem:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-708(a6)
	movem.l	(sp)+,a6
	rts

	section	_Format_stub,code

; BOOL Format(CONST_STRPTR filesystem, CONST_STRPTR volumename, ULONG dostype)
	xdef	_Format
_Format:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-714(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_Relabel_stub,code

; LONG Relabel(CONST_STRPTR drive, CONST_STRPTR newname)
	xdef	_Relabel
_Relabel:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-720(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_Inhibit_stub,code

; LONG Inhibit(CONST_STRPTR name, LONG onoff)
	xdef	_Inhibit
_Inhibit:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-726(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AddBuffers_stub,code

; LONG AddBuffers(CONST_STRPTR name, LONG number)
	xdef	_AddBuffers
_AddBuffers:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-732(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_CompareDates_stub,code

; LONG CompareDates(const struct DateStamp * date1, const struct DateStamp * date2)
	xdef	_CompareDates
_CompareDates:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-738(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_DateToStr_stub,code

; LONG DateToStr(struct DateTime * datetime)
	xdef	_DateToStr
_DateToStr:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-744(a6)
	movem.l	(sp)+,a6
	rts

	section	_StrToDate_stub,code

; LONG StrToDate(struct DateTime * datetime)
	xdef	_StrToDate
_StrToDate:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-750(a6)
	movem.l	(sp)+,a6
	rts

	section	_InternalLoadSeg_stub,code

; BPTR InternalLoadSeg(BPTR fh, BPTR table, const LONG * funcarray, LONG * stack)
	xdef	_InternalLoadSeg
_InternalLoadSeg:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),d0
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	_DOSBase,a6
	jsr	-756(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_InternalUnLoadSeg_stub,code

; BOOL InternalUnLoadSeg(BPTR seglist, VOID (*freefunc)() freefunc)
	xdef	_InternalUnLoadSeg
_InternalUnLoadSeg:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	12(sp),a1
	movea.l	_DOSBase,a6
	jsr	-762(a6)
	movem.l	(sp)+,a6
	rts

	section	_NewLoadSeg_stub,code

; BPTR NewLoadSeg(CONST_STRPTR file, const struct TagItem * tags)
	xdef	_NewLoadSeg
_NewLoadSeg:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-768(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_NewLoadSegTagList_stub,code

; BPTR NewLoadSegTagList(CONST_STRPTR file, const struct TagItem * tags)
	xdef	_NewLoadSegTagList
_NewLoadSegTagList:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-768(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_NewLoadSegTags_stub,code

; BPTR NewLoadSegTags(CONST_STRPTR file, ULONG tags, ... )
	xdef	_NewLoadSegTags
_NewLoadSegTags:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	lea	16(sp),a6
	move.l	a6,d2
	movea.l	_DOSBase,a6
	jsr	-768(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_AddSegment_stub,code

; LONG AddSegment(CONST_STRPTR name, BPTR seg, LONG system)
	xdef	_AddSegment
_AddSegment:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-774(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_FindSegment_stub,code

; struct Segment * FindSegment(CONST_STRPTR name, const struct Segment * seg, LONG system)
	xdef	_FindSegment
_FindSegment:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-780(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_RemSegment_stub,code

; LONG RemSegment(struct Segment * seg)
	xdef	_RemSegment
_RemSegment:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-786(a6)
	movem.l	(sp)+,a6
	rts

	section	_CheckSignal_stub,code

; LONG CheckSignal(LONG mask)
	xdef	_CheckSignal
_CheckSignal:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-792(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadArgs_stub,code

; struct RDArgs * ReadArgs(CONST_STRPTR arg_template, LONG * array, struct RDArgs * args)
	xdef	_ReadArgs
_ReadArgs:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-798(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_FindArg_stub,code

; LONG FindArg(CONST_STRPTR keyword, CONST_STRPTR arg_template)
	xdef	_FindArg
_FindArg:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-804(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ReadItem_stub,code

; LONG ReadItem(CONST_STRPTR name, LONG maxchars, struct CSource * cSource)
	xdef	_ReadItem
_ReadItem:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-810(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_StrToLong_stub,code

; LONG StrToLong(CONST_STRPTR string, LONG * value)
	xdef	_StrToLong
_StrToLong:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-816(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_MatchFirst_stub,code

; LONG MatchFirst(CONST_STRPTR pat, struct AnchorPath * anchor)
	xdef	_MatchFirst
_MatchFirst:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-822(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_MatchNext_stub,code

; LONG MatchNext(struct AnchorPath * anchor)
	xdef	_MatchNext
_MatchNext:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-828(a6)
	movem.l	(sp)+,a6
	rts

	section	_MatchEnd_stub,code

; VOID MatchEnd(struct AnchorPath * anchor)
	xdef	_MatchEnd
_MatchEnd:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-834(a6)
	movem.l	(sp)+,a6
	rts

	section	_ParsePattern_stub,code

; LONG ParsePattern(CONST_STRPTR pat, STRPTR buf, LONG buflen)
	xdef	_ParsePattern
_ParsePattern:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-840(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_MatchPattern_stub,code

; BOOL MatchPattern(CONST_STRPTR pat, STRPTR str)
	xdef	_MatchPattern
_MatchPattern:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-846(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_FreeArgs_stub,code

; VOID FreeArgs(struct RDArgs * args)
	xdef	_FreeArgs
_FreeArgs:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-858(a6)
	movem.l	(sp)+,a6
	rts

	section	_FilePart_stub,code

; STRPTR FilePart(CONST_STRPTR path)
	xdef	_FilePart
_FilePart:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-870(a6)
	movem.l	(sp)+,a6
	rts

	section	_PathPart_stub,code

; STRPTR PathPart(CONST_STRPTR path)
	xdef	_PathPart
_PathPart:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-876(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddPart_stub,code

; BOOL AddPart(STRPTR dirname, CONST_STRPTR filename, ULONG size)
	xdef	_AddPart
_AddPart:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-882(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_StartNotify_stub,code

; BOOL StartNotify(struct NotifyRequest * notify)
	xdef	_StartNotify
_StartNotify:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-888(a6)
	movem.l	(sp)+,a6
	rts

	section	_EndNotify_stub,code

; VOID EndNotify(struct NotifyRequest * notify)
	xdef	_EndNotify
_EndNotify:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-894(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetVar_stub,code

; BOOL SetVar(CONST_STRPTR name, CONST_STRPTR buffer, LONG size, LONG flags)
	xdef	_SetVar
_SetVar:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-900(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_GetVar_stub,code

; LONG GetVar(CONST_STRPTR name, STRPTR buffer, LONG size, LONG flags)
	xdef	_GetVar
_GetVar:
	movem.l	d2/d3/d4/a6,-(sp)
	move.l	20(sp),d1
	move.l	24(sp),d2
	move.l	28(sp),d3
	move.l	32(sp),d4
	movea.l	_DOSBase,a6
	jsr	-906(a6)
	movem.l	(sp)+,d2/d3/d4/a6
	rts

	section	_DeleteVar_stub,code

; LONG DeleteVar(CONST_STRPTR name, ULONG flags)
	xdef	_DeleteVar
_DeleteVar:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-912(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_FindVar_stub,code

; struct LocalVar * FindVar(CONST_STRPTR name, ULONG type)
	xdef	_FindVar
_FindVar:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-918(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_CliInitNewcli_stub,code

; LONG CliInitNewcli(struct DosPacket * dp)
	xdef	_CliInitNewcli
_CliInitNewcli:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_DOSBase,a6
	jsr	-930(a6)
	movem.l	(sp)+,a6
	rts

	section	_CliInitRun_stub,code

; LONG CliInitRun(struct DosPacket * dp)
	xdef	_CliInitRun
_CliInitRun:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_DOSBase,a6
	jsr	-936(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteChars_stub,code

; LONG WriteChars(CONST_STRPTR buf, ULONG buflen)
	xdef	_WriteChars
_WriteChars:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-942(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_PutStr_stub,code

; LONG PutStr(CONST_STRPTR str)
	xdef	_PutStr
_PutStr:
	movem.l	a6,-(sp)
	move.l	8(sp),d1
	movea.l	_DOSBase,a6
	jsr	-948(a6)
	movem.l	(sp)+,a6
	rts

	section	_VPrintf_stub,code

; LONG VPrintf(CONST_STRPTR format, const APTR argarray)
	xdef	_VPrintf
_VPrintf:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-954(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_Printf_stub,code

; LONG Printf(CONST_STRPTR format, ... )
	xdef	_Printf
_Printf:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	lea	16(sp),a6
	move.l	a6,d2
	movea.l	_DOSBase,a6
	jsr	-954(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ParsePatternNoCase_stub,code

; LONG ParsePatternNoCase(CONST_STRPTR pat, UBYTE * buf, LONG buflen)
	xdef	_ParsePatternNoCase
_ParsePatternNoCase:
	movem.l	d2/d3/a6,-(sp)
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	movea.l	_DOSBase,a6
	jsr	-966(a6)
	movem.l	(sp)+,d2/d3/a6
	rts

	section	_MatchPatternNoCase_stub,code

; BOOL MatchPatternNoCase(CONST_STRPTR pat, STRPTR str)
	xdef	_MatchPatternNoCase
_MatchPatternNoCase:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-972(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_SameDevice_stub,code

; BOOL SameDevice(BPTR lock1, BPTR lock2)
	xdef	_SameDevice
_SameDevice:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-984(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_ExAllEnd_stub,code

; VOID ExAllEnd(BPTR lock, struct ExAllData * buffer, LONG size, LONG data, struct ExAllControl * control)
	xdef	_ExAllEnd
_ExAllEnd:
	movem.l	d2/d3/d4/d5/a6,-(sp)
	move.l	24(sp),d1
	move.l	28(sp),d2
	move.l	32(sp),d3
	move.l	36(sp),d4
	move.l	40(sp),d5
	movea.l	_DOSBase,a6
	jsr	-990(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a6
	rts

	section	_SetOwner_stub,code

; BOOL SetOwner(CONST_STRPTR name, LONG owner_info)
	xdef	_SetOwner
_SetOwner:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d1
	move.l	16(sp),d2
	movea.l	_DOSBase,a6
	jsr	-996(a6)
	movem.l	(sp)+,d2/a6
	rts

