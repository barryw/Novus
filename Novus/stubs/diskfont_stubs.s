; Generated from SFD file by Novus SFD Parser
; Library: diskfont.library
; Base: _DiskfontBase
; Each function is in its own section for dead code elimination
; NOTE: Uses lazy initialization via ___diskfont_ensure

	xref	_DiskfontBase
	xref	___diskfont_ensure	; Lazy init - opens library if needed, returns base in A6

	section	_OpenDiskFont_stub,code

; struct TextFont * OpenDiskFont(struct TextAttr * textAttr)
	xdef	_OpenDiskFont
_OpenDiskFont:
	movea.l	4(sp),a0
	jsr	___diskfont_ensure
	jsr	-30(a6)
	rts

	section	_AvailFonts_stub,code

; LONG AvailFonts(STRPTR buffer, LONG bufBytes, LONG flags)
	xdef	_AvailFonts
_AvailFonts:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___diskfont_ensure
	jsr	-36(a6)
	rts

	section	_NewFontContents_stub,code

; struct FontContentsHeader * NewFontContents(BPTR fontsLock, STRPTR fontName)
	xdef	_NewFontContents
_NewFontContents:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___diskfont_ensure
	jsr	-42(a6)
	rts

	section	_DisposeFontContents_stub,code

; VOID DisposeFontContents(struct FontContentsHeader * fontContentsHeader)
	xdef	_DisposeFontContents
_DisposeFontContents:
	movea.l	4(sp),a1
	jsr	___diskfont_ensure
	jsr	-48(a6)
	rts

	section	_NewScaledDiskFont_stub,code

; struct DiskFont * NewScaledDiskFont(struct TextFont * sourceFont, struct TextAttr * destTextAttr)
	xdef	_NewScaledDiskFont
_NewScaledDiskFont:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	jsr	___diskfont_ensure
	jsr	-54(a6)
	rts

	section	_GetDiskFontCtrl_stub,code

; LONG GetDiskFontCtrl(LONG tagid)
	xdef	_GetDiskFontCtrl
_GetDiskFontCtrl:
	move.l	4(sp),d0
	jsr	___diskfont_ensure
	jsr	-60(a6)
	rts

	section	_SetDiskFontCtrlA_stub,code

; VOID SetDiskFontCtrlA(struct TagItem * taglist)
	xdef	_SetDiskFontCtrlA
_SetDiskFontCtrlA:
	movea.l	4(sp),a0
	jsr	___diskfont_ensure
	jsr	-66(a6)
	rts

