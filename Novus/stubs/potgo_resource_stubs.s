; Generated from SFD file by Novus SFD Parser
; Library: potgo.resource
; Base: _PotgoBase
; Each function is in its own section for dead code elimination

	xref	_PotgoBase

	section	_AllocPotBits_stub,code

; UWORD AllocPotBits(UWORD bits)
	xdef	_AllocPotBits
_AllocPotBits:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_PotgoBase,a6
	jsr	-6(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreePotBits_stub,code

; VOID FreePotBits(UWORD bits)
	xdef	_FreePotBits
_FreePotBits:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_PotgoBase,a6
	jsr	-12(a6)
	movem.l	(sp)+,a6
	rts

	section	_WritePotgo_stub,code

; VOID WritePotgo(UWORD word, UWORD mask)
	xdef	_WritePotgo
_WritePotgo:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_PotgoBase,a6
	jsr	-18(a6)
	movem.l	(sp)+,a6
	rts

