; Generated from SFD file by Novus SFD Parser
; Library: timer.device
; Base: _TimerBase
; Each function is in its own section for dead code elimination

	xref	_TimerBase

	section	_AddTime_stub,code

; VOID AddTime(struct timeval * dest, const struct timeval * src)
	xdef	_AddTime
_AddTime:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_TimerBase,a6
	jsr	-42(a6)
	rts

	section	_SubTime_stub,code

; VOID SubTime(struct timeval * dest, const struct timeval * src)
	xdef	_SubTime
_SubTime:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_TimerBase,a6
	jsr	-48(a6)
	rts

	section	_CmpTime_stub,code

; LONG CmpTime(const struct timeval * dest, const struct timeval * src)
	xdef	_CmpTime
_CmpTime:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_TimerBase,a6
	jsr	-54(a6)
	rts

	section	_ReadEClock_stub,code

; ULONG ReadEClock(struct EClockVal * dest)
	xdef	_ReadEClock
_ReadEClock:
	movea.l	4(sp),a0
	movea.l	_TimerBase,a6
	jsr	-60(a6)
	rts

	section	_GetSysTime_stub,code

; VOID GetSysTime(struct timeval * dest)
	xdef	_GetSysTime
_GetSysTime:
	movea.l	4(sp),a0
	movea.l	_TimerBase,a6
	jsr	-66(a6)
	rts

