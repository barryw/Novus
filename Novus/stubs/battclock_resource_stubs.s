; Generated from SFD file by Novus SFD Parser
; Library: battclock.resource
; Base: _BattClockBase
; Each function is in its own section for dead code elimination

	xref	_BattClockBase

	section	_ResetBattClock_stub,code

; VOID ResetBattClock()
	xdef	_ResetBattClock
_ResetBattClock:
	movea.l	_BattClockBase,a6
	jsr	-6(a6)
	rts

	section	_ReadBattClock_stub,code

; ULONG ReadBattClock()
	xdef	_ReadBattClock
_ReadBattClock:
	movea.l	_BattClockBase,a6
	jsr	-12(a6)
	rts

	section	_WriteBattClock_stub,code

; VOID WriteBattClock(ULONG time)
	xdef	_WriteBattClock
_WriteBattClock:
	move.l	4(sp),d0
	movea.l	_BattClockBase,a6
	jsr	-18(a6)
	rts

