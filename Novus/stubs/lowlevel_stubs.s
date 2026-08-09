; Generated from SFD file by Novus SFD Parser
; Library: lowlevel.library
; Base: _LowLevelBase
; Each function is in its own section for dead code elimination

	xref	_LowLevelBase

	section	_ReadJoyPort_stub,code

; ULONG ReadJoyPort(ULONG port)
	xdef	_ReadJoyPort
_ReadJoyPort:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_LowLevelBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetLanguageSelection_stub,code

; UBYTE GetLanguageSelection()
	xdef	_GetLanguageSelection
_GetLanguageSelection:
	movem.l	a6,-(sp)
	movea.l	_LowLevelBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetKey_stub,code

; ULONG GetKey()
	xdef	_GetKey
_GetKey:
	movem.l	a6,-(sp)
	movea.l	_LowLevelBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_QueryKeys_stub,code

; VOID QueryKeys(struct KeyQuery * queryArray, UBYTE arraySize)
	xdef	_QueryKeys
_QueryKeys:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d1
	movea.l	_LowLevelBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddKBInt_stub,code

; APTR AddKBInt(const APTR intRoutine, const APTR intData)
	xdef	_AddKBInt
_AddKBInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemKBInt_stub,code

; VOID RemKBInt(APTR intHandle)
	xdef	_RemKBInt
_RemKBInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_SystemControlA_stub,code

; ULONG SystemControlA(const struct TagItem * tagList)
	xdef	_SystemControlA
_SystemControlA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_SystemControl_stub,code

; ULONG SystemControl(Tag tagList, ... )
	xdef	_SystemControl
_SystemControl:
	movem.l	a6,-(sp)
	lea	8(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddTimerInt_stub,code

; APTR AddTimerInt(const APTR intRoutine, const APTR intData)
	xdef	_AddTimerInt
_AddTimerInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemTimerInt_stub,code

; VOID RemTimerInt(APTR intHandle)
	xdef	_RemTimerInt
_RemTimerInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_StopTimerInt_stub,code

; VOID StopTimerInt(APTR intHandle)
	xdef	_StopTimerInt
_StopTimerInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_StartTimerInt_stub,code

; VOID StartTimerInt(APTR intHandle, ULONG timeInterval, BOOL continuous)
	xdef	_StartTimerInt
_StartTimerInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_LowLevelBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_ElapsedTime_stub,code

; ULONG ElapsedTime(struct EClockVal * context)
	xdef	_ElapsedTime
_ElapsedTime:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LowLevelBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddVBlankInt_stub,code

; APTR AddVBlankInt(const APTR intRoutine, const APTR intData)
	xdef	_AddVBlankInt
_AddVBlankInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemVBlankInt_stub,code

; VOID RemVBlankInt(APTR intHandle)
	xdef	_RemVBlankInt
_RemVBlankInt:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetJoyPortAttrsA_stub,code

; BOOL SetJoyPortAttrsA(ULONG portNumber, const struct TagItem * tagList)
	xdef	_SetJoyPortAttrsA
_SetJoyPortAttrsA:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetJoyPortAttrs_stub,code

; BOOL SetJoyPortAttrs(ULONG portNumber, Tag tagList, ... )
	xdef	_SetJoyPortAttrs
_SetJoyPortAttrs:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	lea	12(sp),a1
	movea.l	_LowLevelBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

