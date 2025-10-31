; Generated from SFD file by Novus SFD Parser
; Library: potgo.resource
; Base: _PotgoBase
; Each function is in its own section for dead code elimination

	xref	_PotgoBase

	section	_AllocPotBits_stub,code

; UWORD AllocPotBits(UWORD bits)
	xdef	_AllocPotBits
_AllocPotBits:
	move.l	4(sp),d0
	movea.l	_PotgoBase,a6
	jsr	-6(a6)
	rts

	section	_FreePotBits_stub,code

; VOID FreePotBits(UWORD bits)
	xdef	_FreePotBits
_FreePotBits:
	move.l	4(sp),d0
	movea.l	_PotgoBase,a6
	jsr	-12(a6)
	rts

	section	_WritePotgo_stub,code

; VOID WritePotgo(UWORD word, UWORD mask)
	xdef	_WritePotgo
_WritePotgo:
	move.l	4(sp),d0
	move.l	8(sp),d1
	movea.l	_PotgoBase,a6
	jsr	-18(a6)
	rts

