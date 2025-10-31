; Generated from SFD file by Novus SFD Parser
; Library: battmem.resource
; Base: _BattMemBase
; Each function is in its own section for dead code elimination

	xref	_BattMemBase

	section	_ObtainBattSemaphore_stub,code

; VOID ObtainBattSemaphore()
	xdef	_ObtainBattSemaphore
_ObtainBattSemaphore:
	movea.l	_BattMemBase,a6
	jsr	-6(a6)
	rts

	section	_ReleaseBattSemaphore_stub,code

; VOID ReleaseBattSemaphore()
	xdef	_ReleaseBattSemaphore
_ReleaseBattSemaphore:
	movea.l	_BattMemBase,a6
	jsr	-12(a6)
	rts

	section	_ReadBattMem_stub,code

; ULONG ReadBattMem(APTR buffer, ULONG offset, ULONG length)
	xdef	_ReadBattMem
_ReadBattMem:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_BattMemBase,a6
	jsr	-18(a6)
	rts

	section	_WriteBattMem_stub,code

; ULONG WriteBattMem(const APTR buffer, ULONG offset, ULONG length)
	xdef	_WriteBattMem
_WriteBattMem:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_BattMemBase,a6
	jsr	-24(a6)
	rts

