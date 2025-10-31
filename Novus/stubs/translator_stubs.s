; Generated from SFD file by Novus SFD Parser
; Library: translator.library
; Base: _TranslatorBase
; Each function is in its own section for dead code elimination

	xref	_TranslatorBase

	section	_Translate_stub,code

; LONG Translate(CONST_STRPTR inputString, LONG inputLength, STRPTR outputBuffer, LONG bufferSize)
	xdef	_Translate
_Translate:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	move.l	16(sp),d1
	movea.l	_TranslatorBase,a6
	jsr	-30(a6)
	rts

