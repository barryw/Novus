; Generated from SFD file by Novus SFD Parser
; Library: cia.resource
; Base: caller-supplied
; Each function is in its own section for dead code elimination

	section	_AddICRVector_stub,code

; struct Interrupt * AddICRVector(struct Library * resource, WORD iCRBit, struct Interrupt * interrupt)
	xdef	_AddICRVector
_AddICRVector:
	movem.l	a6,-(sp)
	movea.l	8(sp),a6
	move.l	12(sp),d0
	movea.l	16(sp),a1
	jsr	-6(a6)
	movem.l	(sp)+,a6
	rts
	section	_RemICRVector_stub,code

; VOID RemICRVector(struct Library * resource, WORD iCRBit, struct Interrupt * interrupt)
	xdef	_RemICRVector
_RemICRVector:
	movem.l	a6,-(sp)
	movea.l	8(sp),a6
	move.l	12(sp),d0
	movea.l	16(sp),a1
	jsr	-12(a6)
	movem.l	(sp)+,a6
	rts

	section	_AbleICR_stub,code

; WORD AbleICR(struct Library * resource, WORD mask)
	xdef	_AbleICR
_AbleICR:
	movem.l	a6,-(sp)
	movea.l	8(sp),a6
	move.l	12(sp),d0
	jsr	-18(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetICR_stub,code

; WORD SetICR(struct Library * resource, WORD mask)
	xdef	_SetICR
_SetICR:
	movem.l	a6,-(sp)
	movea.l	8(sp),a6
	move.l	12(sp),d0
	jsr	-24(a6)
	movem.l	(sp)+,a6
	rts
