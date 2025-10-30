; timer library stubs for Novus
; Auto-generated from timer_lib.fd

	xref	_TimerBase	; Provided by startup.o + -lamiga

	section	"CODE",code

; AddTime(dest, src)
	xdef	_AddTime
_AddTime:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; dest
	move.l	16(sp),a1	; src
	move.l	_TimerBase,a6
	jsr	-42(a6)	; AddTime()
	movem.l	(sp)+,a0-a1/a6
	rts

; SubTime(dest, src)
	xdef	_SubTime
_SubTime:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; dest
	move.l	16(sp),a1	; src
	move.l	_TimerBase,a6
	jsr	-48(a6)	; SubTime()
	movem.l	(sp)+,a0-a1/a6
	rts

; CmpTime(dest, src)
	xdef	_CmpTime
_CmpTime:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; dest
	move.l	16(sp),a1	; src
	move.l	_TimerBase,a6
	jsr	-54(a6)	; CmpTime()
	movem.l	(sp)+,a0-a1/a6
	rts

; ReadEClock(dest)
	xdef	_ReadEClock
_ReadEClock:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; dest
	move.l	_TimerBase,a6
	jsr	-60(a6)	; ReadEClock()
	movem.l	(sp)+,a0/a6
	rts

; GetSysTime(dest)
	xdef	_GetSysTime
_GetSysTime:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; dest
	move.l	_TimerBase,a6
	jsr	-66(a6)	; GetSysTime()
	movem.l	(sp)+,a0/a6
	rts

