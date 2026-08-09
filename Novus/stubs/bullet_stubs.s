; Generated from SFD file by Novus SFD Parser
; Library: bullet.library
; Base: _BulletBase
; Each function is in its own section for dead code elimination

	xref	_BulletBase

	section	_OpenEngine_stub,code

; struct GlyphEngine * OpenEngine()
	xdef	_OpenEngine
_OpenEngine:
	movem.l	a6,-(sp)
	movea.l	_BulletBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseEngine_stub,code

; VOID CloseEngine(struct GlyphEngine * glyphEngine)
	xdef	_CloseEngine
_CloseEngine:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_BulletBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetInfoA_stub,code

; ULONG SetInfoA(struct GlyphEngine * glyphEngine, struct TagItem * tagList)
	xdef	_SetInfoA
_SetInfoA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_BulletBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetInfo_stub,code

; ULONG SetInfo(struct GlyphEngine * glyphEngine, Tag tagList, ... )
	xdef	_SetInfo
_SetInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_BulletBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_ObtainInfoA_stub,code

; ULONG ObtainInfoA(struct GlyphEngine * glyphEngine, struct TagItem * tagList)
	xdef	_ObtainInfoA
_ObtainInfoA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_BulletBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_ObtainInfo_stub,code

; ULONG ObtainInfo(struct GlyphEngine * glyphEngine, Tag tagList, ... )
	xdef	_ObtainInfo
_ObtainInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_BulletBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReleaseInfoA_stub,code

; ULONG ReleaseInfoA(struct GlyphEngine * glyphEngine, struct TagItem * tagList)
	xdef	_ReleaseInfoA
_ReleaseInfoA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_BulletBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReleaseInfo_stub,code

; ULONG ReleaseInfo(struct GlyphEngine * glyphEngine, Tag tagList, ... )
	xdef	_ReleaseInfo
_ReleaseInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_BulletBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

