; keymap library stubs for Novus
; Auto-generated from keymap_lib.fd

	xref	_KeymapBase	; Provided by startup.o + -lamiga

	section	text,code

; SetKeyMapDefault(keyMap)
	xdef	_SetKeyMapDefault
_SetKeyMapDefault:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; keyMap
	move.l	_KeymapBase,a6
	jsr	-30(a6)	; SetKeyMapDefault()
	movem.l	(sp)+,a0/a6
	rts

; AskKeyMapDefault()
	xdef	_AskKeyMapDefault
_AskKeyMapDefault:
	movem.l	a6,-(sp)
	move.l	_KeymapBase,a6
	jsr	-36(a6)	; AskKeyMapDefault()
	movem.l	(sp)+,a6
	rts

; MapRawKey(event, buffer, length, keyMap)
	xdef	_MapRawKey
_MapRawKey:
	movem.l	d1/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; event
	move.l	20(sp),a1	; buffer
	move.l	24(sp),d1	; length
	move.l	28(sp),a2	; keyMap
	move.l	_KeymapBase,a6
	jsr	-42(a6)	; MapRawKey()
	movem.l	(sp)+,d1/a0-a2/a6
	rts

; MapANSI(string, count, buffer, length, keyMap)
	xdef	_MapANSI
_MapANSI:
	movem.l	d0-d1/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; string
	move.l	20(sp),d0	; count
	move.l	24(sp),a1	; buffer
	move.l	28(sp),d1	; length
	move.l	32(sp),a2	; keyMap
	move.l	_KeymapBase,a6
	jsr	-48(a6)	; MapANSI()
	movem.l	(sp)+,d0-d1/a0-a2/a6
	rts

