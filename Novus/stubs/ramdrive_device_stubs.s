; Generated from SFD file by Novus SFD Parser
; Library: ramdrive.device
; Base: _RamdriveDevice
; Each function is in its own section for dead code elimination

	xref	_RamdriveDevice

	section	_KillRAD0_stub,code

; STRPTR KillRAD0()
	xdef	_KillRAD0
_KillRAD0:
	movea.l	_RamdriveDevice,a6
	jsr	-42(a6)
	rts

	section	_KillRAD_stub,code

; STRPTR KillRAD(ULONG unit)
	xdef	_KillRAD
_KillRAD:
	move.l	4(sp),d0
	movea.l	_RamdriveDevice,a6
	jsr	-48(a6)
	rts

