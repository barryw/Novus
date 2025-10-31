; Generated from SFD file by Novus SFD Parser
; Library: keymap.library
; Base: _KeymapBase
; Each function is in its own section for dead code elimination

	xref	_KeymapBase

	section	_SetKeyMapDefault_stub,code

; VOID SetKeyMapDefault(const struct KeyMap * keyMap)
	xdef	_SetKeyMapDefault
_SetKeyMapDefault:
	movea.l	4(sp),a0
	movea.l	_KeymapBase,a6
	jsr	-30(a6)
	rts

	section	_AskKeyMapDefault_stub,code

; struct KeyMap * AskKeyMapDefault()
	xdef	_AskKeyMapDefault
_AskKeyMapDefault:
	movea.l	_KeymapBase,a6
	jsr	-36(a6)
	rts

	section	_MapRawKey_stub,code

; WORD MapRawKey(const struct InputEvent * event, STRPTR buffer, WORD length, const struct KeyMap * keyMap)
	xdef	_MapRawKey
_MapRawKey:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d1
	movea.l	16(sp),a2
	movea.l	_KeymapBase,a6
	jsr	-42(a6)
	rts

	section	_MapANSI_stub,code

; LONG MapANSI(CONST_STRPTR string, LONG count, STRPTR buffer, LONG length, const struct KeyMap * keyMap)
	xdef	_MapANSI
_MapANSI:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	move.l	16(sp),d1
	movea.l	20(sp),a2
	movea.l	_KeymapBase,a6
	jsr	-48(a6)
	rts

