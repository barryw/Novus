; Generated from SFD file by Novus SFD Parser
; Library: hdwrench.library
; Base: _HDWBase
; Each function is in its own section for dead code elimination

	xref	_HDWBase

	section	_HDWOpenDevice_stub,code

; BOOL HDWOpenDevice(char * DevName, ULONG unit)
	xdef	_HDWOpenDevice
_HDWOpenDevice:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_HDWBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_HDWCloseDevice_stub,code

; VOID HDWCloseDevice()
	xdef	_HDWCloseDevice
_HDWCloseDevice:
	movem.l	a6,-(sp)
	movea.l	_HDWBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_RawRead_stub,code

; USHORT RawRead(struct BootBlock * bbk, USHORT size)
	xdef	_RawRead
_RawRead:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_HDWBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_RawWrite_stub,code

; USHORT RawWrite(struct BootBlock * bb)
	xdef	_RawWrite
_RawWrite:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_HDWBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteBlock_stub,code

; USHORT WriteBlock(struct BootBlock * bb)
	xdef	_WriteBlock
_WriteBlock:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_HDWBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadRDBs_stub,code

; USHORT ReadRDBs()
	xdef	_ReadRDBs
_ReadRDBs:
	movem.l	a6,-(sp)
	movea.l	_HDWBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteRDBs_stub,code

; USHORT WriteRDBs()
	xdef	_WriteRDBs
_WriteRDBs:
	movem.l	a6,-(sp)
	movea.l	_HDWBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_QueryReady_stub,code

; BOOL QueryReady(int * errorcode)
	xdef	_QueryReady
_QueryReady:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_HDWBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_QueryInquiry_stub,code

; BOOL QueryInquiry(BYTE * inqbuf, int * errorcode)
	xdef	_QueryInquiry
_QueryInquiry:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_HDWBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_QueryModeSense_stub,code

; BOOL QueryModeSense(BYTE page, int msbsize, BYTE * msbuf, int * errorcode)
	xdef	_QueryModeSense
_QueryModeSense:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	_HDWBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_QueryFindValid_stub,code

; VOID QueryFindValid(struct ValidIDstruct * ValidIDs, char * devicename, int board, ULONG types, BOOL wide_scsi, LONG (*Callback)(struct HDWCallbackMsg *msg) Callback)
	xdef	_QueryFindValid
_QueryFindValid:
	movem.l	d2/a2/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	move.l	24(sp),d0
	move.l	28(sp),d1
	move.l	32(sp),d2
	movea.l	36(sp),a2
	movea.l	_HDWBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,d2/a2/a6
	rts

	section	_QueryCapacity_stub,code

; BOOL QueryCapacity(ULONG * totalblocks, ULONG * blocksize)
	xdef	_QueryCapacity
_QueryCapacity:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_HDWBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadMountfile_stub,code

; ULONG ReadMountfile(ULONG unit, char * filename, char * controller)
	xdef	_ReadMountfile
_ReadMountfile:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_HDWBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadRDBStructs_stub,code

; ULONG ReadRDBStructs(char * filename, ULONG unit)
	xdef	_ReadRDBStructs
_ReadRDBStructs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_HDWBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteMountfile_stub,code

; ULONG WriteMountfile(char * filename, char * ldir, ULONG unit)
	xdef	_WriteMountfile
_WriteMountfile:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_HDWBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteRDBStructs_stub,code

; ULONG WriteRDBStructs(char * filename)
	xdef	_WriteRDBStructs
_WriteRDBStructs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_HDWBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_InMemMountfile_stub,code

; ULONG InMemMountfile(ULONG unit, char * mfdata, char * controller)
	xdef	_InMemMountfile
_InMemMountfile:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_HDWBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_InMemRDBStructs_stub,code

; ULONG InMemRDBStructs(char * rdbp, ULONG sizerdb, ULONG unit)
	xdef	_InMemRDBStructs
_InMemRDBStructs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_HDWBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_OutMemMountfile_stub,code

; ULONG OutMemMountfile(char * mfp, ULONG * sizew, ULONG sizeb, ULONG unit)
	xdef	_OutMemMountfile
_OutMemMountfile:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_HDWBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_OutMemRDBStructs_stub,code

; ULONG OutMemRDBStructs(char * rdbp, ULONG * sizew, ULONG sizeb)
	xdef	_OutMemRDBStructs
_OutMemRDBStructs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_HDWBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindDiskName_stub,code

; BOOL FindDiskName(char * diskname)
	xdef	_FindDiskName
_FindDiskName:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_HDWBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindControllerID_stub,code

; BOOL FindControllerID(char * devname, ULONG * selfid)
	xdef	_FindControllerID
_FindControllerID:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_HDWBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindLastSector_stub,code

; ULONG FindLastSector()
	xdef	_FindLastSector
_FindLastSector:
	movem.l	a6,-(sp)
	movea.l	_HDWBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindDefaults_stub,code

; ULONG FindDefaults(USHORT Optimize, struct DefaultsArray * Return)
	xdef	_FindDefaults
_FindDefaults:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_HDWBase,a6
	jsr	-168(a6)
	movem.l	(sp)+,a6
	rts

	section	_LowlevelFormat_stub,code

; ULONG LowlevelFormat(LONG (*Callback)(struct HDWCallbackMsg *msg) Callback)
	xdef	_LowlevelFormat
_LowlevelFormat:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_HDWBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a6
	rts

	section	_VerifyDrive_stub,code

; ULONG VerifyDrive(LONG (*Callback)(struct HDWCallbackMsg *msg) Callback)
	xdef	_VerifyDrive
_VerifyDrive:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_HDWBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,a6
	rts

