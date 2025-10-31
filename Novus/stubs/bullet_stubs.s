; Generated from SFD file by Novus SFD Parser
; Library: bullet.library
; Base: _BulletBase
; Each function is in its own section for dead code elimination

	xref	_BulletBase

	section	_OpenEngine_stub,code

; struct GlyphEngine * OpenEngine()
	xdef	_OpenEngine
_OpenEngine:
	movea.l	_BulletBase,a6
	jsr	-30(a6)
	rts

	section	_CloseEngine_stub,code

; VOID CloseEngine(struct GlyphEngine * glyphEngine)
	xdef	_CloseEngine
_CloseEngine:
	movea.l	4(sp),a0
	movea.l	_BulletBase,a6
	jsr	-36(a6)
	rts

	section	_SetInfoA_stub,code

; ULONG SetInfoA(struct GlyphEngine * glyphEngine, struct TagItem * tagList)
	xdef	_SetInfoA
_SetInfoA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_BulletBase,a6
	jsr	-42(a6)
	rts

	section	_ObtainInfoA_stub,code

; ULONG ObtainInfoA(struct GlyphEngine * glyphEngine, struct TagItem * tagList)
	xdef	_ObtainInfoA
_ObtainInfoA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_BulletBase,a6
	jsr	-54(a6)
	rts

	section	_ReleaseInfoA_stub,code

; ULONG ReleaseInfoA(struct GlyphEngine * glyphEngine, struct TagItem * tagList)
	xdef	_ReleaseInfoA
_ReleaseInfoA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_BulletBase,a6
	jsr	-66(a6)
	rts

