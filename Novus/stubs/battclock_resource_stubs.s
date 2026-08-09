; Generated from SFD file by Novus SFD Parser
; Library: battclock.resource
; Base: _BattClockBase
; Each function is in its own section for dead code elimination

	xref	_BattClockBase

	section	_ResetBattClock_stub,code

; VOID ResetBattClock()
	xdef	_ResetBattClock
_ResetBattClock:
	movem.l	a6,-(sp)
	movea.l	_BattClockBase,a6
	jsr	-6(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadBattClock_stub,code

; ULONG ReadBattClock()
	xdef	_ReadBattClock
_ReadBattClock:
	movem.l	a6,-(sp)
	movea.l	_BattClockBase,a6
	jsr	-12(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteBattClock_stub,code

; VOID WriteBattClock(ULONG time)
	xdef	_WriteBattClock
_WriteBattClock:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_BattClockBase,a6
	jsr	-18(a6)
	movem.l	(sp)+,a6
	rts

