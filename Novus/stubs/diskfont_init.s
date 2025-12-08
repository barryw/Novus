; ============================================================================
; Diskfont Library Initialization for Novus
; ============================================================================
; Provides automatic diskfont.library opening/closing for Novus programs
;
; This module exports initialization functions that are called by the
; program to set up diskfont.library access.
;
; Required by: diskfont_stubs.s (all Diskfont function stubs need _DiskfontBase)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_DiskfontBase		; Provided by library_bases.s
	xref	_SysBase		; Provided by library_bases.s

; ============================================================================
; Exports
; ============================================================================
	xdef	___diskfont_init	; Initialize Diskfont library
	xdef	___diskfont_cleanup	; Cleanup Diskfont library

; ============================================================================
; Data Section
; ============================================================================
	section	data,data

diskfont_name:
	dc.b	'diskfont.library',0	; Library name string
	even				; Word align

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___diskfont_init - Initialize Diskfont library
; ----------------------------------------------------------------------------
; Opens diskfont.library and stores the base pointer in _DiskfontBase
;
; Input:  None
; Output: D0 = Diskfont library base (0 if failed)
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___diskfont_init:
	movem.l	d1/a0-a1/a6,-(sp)	; Save registers

	; Initialize SysBase if not already done
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6			; Get exec.library base from absolute address 4
	move.l	a6,_SysBase		; Store it for future use
	bra.s	.check_diskfont

.sysbase_ok:
	move.l	d0,a6			; Use existing SysBase

.check_diskfont:
	; Check if DiskfontBase already initialized
	move.l	_DiskfontBase,d0
	bne.s	.already_open

	; OpenLibrary("diskfont.library", 0) - LVO offset -552
	lea	diskfont_name,a1	; Library name (absolute addressing)
	moveq	#0,d0			; Any version
	jsr	-552(a6)		; Call exec.library OpenLibrary()

	; Store result in _DiskfontBase
	move.l	d0,_DiskfontBase

.already_open:
	; Return DiskfontBase in d0
	move.l	_DiskfontBase,d0

	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers
	rts

; ----------------------------------------------------------------------------
; ___diskfont_cleanup - Cleanup Diskfont library
; ----------------------------------------------------------------------------
; Closes diskfont.library if it was opened
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___diskfont_cleanup:
	movem.l	d0/a1/a6,-(sp)		; Save registers

	; Check if DiskfontBase is initialized
	move.l	_DiskfontBase,d0
	beq.s	.not_open		; Skip if not open

	; Get exec.library base
	move.l	4.w,a6

	; CloseLibrary(DiskfontBase) - LVO offset -414
	move.l	_DiskfontBase,a1	; Library base to close
	jsr	-414(a6)		; Call exec.library CloseLibrary()

	; Clear DiskfontBase
	clr.l	_DiskfontBase

.not_open:
	movem.l	(sp)+,d0/a1/a6		; Restore registers
	rts
