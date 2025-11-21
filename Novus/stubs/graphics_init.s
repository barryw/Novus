; ============================================================================
; Graphics Library Initialization for Novus
; ============================================================================
; Provides automatic graphics.library opening/closing for Novus programs
;
; This module exports initialization functions that are called by the
; program to set up graphics.library access.
;
; Required by: graphics_stubs.s (all Graphics function stubs need _GfxBase)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_GfxBase		; Provided by library_bases.s
	xref	_SysBase		; Provided by library_bases.s

; ============================================================================
; Exports
; ============================================================================
	xdef	___graphics_init	; Initialize Graphics library
	xdef	___graphics_cleanup	; Cleanup Graphics library

; ============================================================================
; Data Section
; ============================================================================
	section	data,data

graphics_name:
	dc.b	'graphics.library',0	; Library name string
	even				; Word align

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___graphics_init - Initialize Graphics library
; ----------------------------------------------------------------------------
; Opens graphics.library and stores the base pointer in _GfxBase
;
; Input:  None
; Output: D0 = Graphics library base (0 if failed)
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___graphics_init:
	movem.l	d1/a0-a1/a6,-(sp)	; Save registers

	; Initialize SysBase if not already done
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6			; Get exec.library base from absolute address 4
	move.l	a6,_SysBase		; Store it for future use
	bra.s	.check_graphics

.sysbase_ok:
	move.l	d0,a6			; Use existing SysBase

.check_graphics:
	; Check if GfxBase already initialized
	move.l	_GfxBase,d0
	bne.s	.already_open

	; OpenLibrary("graphics.library", 0) - LVO offset -552
	lea	graphics_name,a1	; Library name (absolute addressing)
	moveq	#0,d0			; Any version
	jsr	-552(a6)		; Call exec.library OpenLibrary()

	; Store result in _GfxBase
	move.l	d0,_GfxBase

.already_open:
	; Return GfxBase in d0
	move.l	_GfxBase,d0

	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers
	rts

; ----------------------------------------------------------------------------
; ___graphics_cleanup - Cleanup Graphics library
; ----------------------------------------------------------------------------
; Closes graphics.library if it was opened
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___graphics_cleanup:
	movem.l	d0/a1/a6,-(sp)		; Save registers

	; Check if GfxBase is initialized
	move.l	_GfxBase,d0
	beq.s	.not_open		; Skip if not open

	; Get exec.library base
	move.l	4.w,a6

	; CloseLibrary(GfxBase) - LVO offset -414
	move.l	_GfxBase,a1		; Library base to close
	jsr	-414(a6)		; Call exec.library CloseLibrary()

	; Clear GfxBase
	clr.l	_GfxBase

.not_open:
	movem.l	(sp)+,d0/a1/a6		; Restore registers
	rts
