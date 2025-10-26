; diskfont library stubs for Novus
; Auto-generated from diskfont_lib.fd

	xref	_DiskfontBase	; Provided by startup.o + -lamiga

	section	text,code

; OpenDiskFont(textAttr)
	xdef	_OpenDiskFont
_OpenDiskFont:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; textAttr
	move.l	_DiskfontBase,a6
	jsr	-30(a6)	; OpenDiskFont()
	movem.l	(sp)+,a0/a6
	rts

; AvailFonts(buffer, bufBytes, flags)
	xdef	_AvailFonts
_AvailFonts:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; buffer
	move.l	20(sp),d0	; bufBytes
	move.l	24(sp),d1	; flags
	move.l	_DiskfontBase,a6
	jsr	-36(a6)	; AvailFonts()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; NewFontContents(fontsLock, fontName)
	xdef	_NewFontContents
_NewFontContents:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; fontsLock
	move.l	16(sp),a1	; fontName
	move.l	_DiskfontBase,a6
	jsr	-42(a6)	; NewFontContents()
	movem.l	(sp)+,a0-a1/a6
	rts

; DisposeFontContents(fontContentsHeader)
	xdef	_DisposeFontContents
_DisposeFontContents:
	movem.l	a1/a6,-(sp)
	move.l	12(sp),a1	; fontContentsHeader
	move.l	_DiskfontBase,a6
	jsr	-48(a6)	; DisposeFontContents()
	movem.l	(sp)+,a1/a6
	rts

; NewScaledDiskFont(sourceFont, destTextAttr)
	xdef	_NewScaledDiskFont
_NewScaledDiskFont:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; sourceFont
	move.l	16(sp),a1	; destTextAttr
	move.l	_DiskfontBase,a6
	jsr	-54(a6)	; NewScaledDiskFont()
	movem.l	(sp)+,a0-a1/a6
	rts

; GetDiskFontCtrl(tagid)
	xdef	_GetDiskFontCtrl
_GetDiskFontCtrl:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; tagid
	move.l	_DiskfontBase,a6
	jsr	-60(a6)	; GetDiskFontCtrl()
	movem.l	(sp)+,d0/a6
	rts

; SetDiskFontCtrlA(taglist)
	xdef	_SetDiskFontCtrlA
_SetDiskFontCtrlA:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; taglist
	move.l	_DiskfontBase,a6
	jsr	-66(a6)	; SetDiskFontCtrlA()
	movem.l	(sp)+,a0/a6
	rts

