; Generated from SFD file by Novus SFD Parser
; Library: console.device
; Base: _ConsoleDevice
; Each function is in its own section for dead code elimination

	xref	_ConsoleDevice

	section	_CDInputHandler_stub,code

; struct InputEvent * CDInputHandler(const struct InputEvent * events, struct Library * consoleDevice)
	xdef	_CDInputHandler
_CDInputHandler:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ConsoleDevice,a6
	jsr	-42(a6)
	rts

	section	_RawKeyConvert_stub,code

; LONG RawKeyConvert(const struct InputEvent * events, STRPTR buffer, LONG length, const struct KeyMap * keyMap)
	xdef	_RawKeyConvert
_RawKeyConvert:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d1
	movea.l	16(sp),a2
	movea.l	_ConsoleDevice,a6
	jsr	-48(a6)
	rts

