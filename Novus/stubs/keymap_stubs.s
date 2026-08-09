; Generated from SFD file by Novus SFD Parser
; Library: keymap.library
; Base: _KeymapBase
; Each function is in its own section for dead code elimination

	xref	_KeymapBase

	section	_SetKeyMapDefault_stub,code

; VOID SetKeyMapDefault(const struct KeyMap * keyMap)
	xdef	_SetKeyMapDefault
_SetKeyMapDefault:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_KeymapBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_AskKeyMapDefault_stub,code

; struct KeyMap * AskKeyMapDefault()
	xdef	_AskKeyMapDefault
_AskKeyMapDefault:
	movem.l	a6,-(sp)
	movea.l	_KeymapBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_MapRawKey_stub,code

; WORD MapRawKey(const struct InputEvent * event, STRPTR buffer, WORD length, const struct KeyMap * keyMap)
	xdef	_MapRawKey
_MapRawKey:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	move.l	20(sp),d1
	movea.l	24(sp),a2
	movea.l	_KeymapBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_MapANSI_stub,code

; LONG MapANSI(CONST_STRPTR string, LONG count, STRPTR buffer, LONG length, const struct KeyMap * keyMap)
	xdef	_MapANSI
_MapANSI:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	move.l	16(sp),d0
	movea.l	20(sp),a1
	move.l	24(sp),d1
	movea.l	28(sp),a2
	movea.l	_KeymapBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a2/a6
	rts

