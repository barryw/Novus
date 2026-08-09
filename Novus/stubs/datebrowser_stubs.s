; Generated from SFD file by Novus SFD Parser
; Library: datebrowser.library
; Base: _DateBrowserBase
; Each function is in its own section for dead code elimination

	xref	_DateBrowserBase

	section	_DATEBROWSER_GetClass_stub,code

; Class * DATEBROWSER_GetClass()
	xdef	_DATEBROWSER_GetClass
_DATEBROWSER_GetClass:
	movem.l	a6,-(sp)
	movea.l	_DateBrowserBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_JulianWeekDay_stub,code

; UWORD JulianWeekDay(UWORD day, UWORD month, LONG year)
	xdef	_JulianWeekDay
_JulianWeekDay:
	movem.l	d2/a6,-(sp)
	move.l	12(sp),d0
	move.l	16(sp),d1
	move.l	20(sp),d2
	movea.l	_DateBrowserBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,d2/a6
	rts

	section	_JulianMonthDays_stub,code

; UWORD JulianMonthDays(UWORD month, LONG year)
	xdef	_JulianMonthDays
_JulianMonthDays:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_DateBrowserBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_JulianLeapYear_stub,code

; BOOL JulianLeapYear(LONG year)
	xdef	_JulianLeapYear
_JulianLeapYear:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_DateBrowserBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

