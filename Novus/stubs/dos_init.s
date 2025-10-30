; ============================================================================
; DOS Library Initialization for Novus
; ============================================================================
; Provides automatic dos.library opening/closing for Novus programs
;
; This module exports _DOSBase and initialization functions that are
; called by the program to set up dos.library access.
;
; Required by: dos_stubs.s (all DOS function stubs need _DOSBase)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_DOSBase		; Provided by library_bases.s
	xref	_SysBase		; Provided by library_bases.s

; ============================================================================
; Exports
; ============================================================================
	xdef	___dos_init		; Initialize DOS library
	xdef	___dos_cleanup		; Cleanup DOS library

; ============================================================================
; Data Section
; ============================================================================
	section	data,data

dos_name:
	dc.b	'dos.library',0		; Library name string
	even				; Word align

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___dos_init - Initialize DOS library
; ----------------------------------------------------------------------------
; Opens dos.library and stores the base pointer in _DOSBase
;
; Input:  None
; Output: D0 = DOS library base (0 if failed)
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___dos_init:
	movem.l	d1/a0-a1/a6,-(sp)	; Save registers

	; Initialize SysBase if not already done
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6			; Get exec.library base from absolute address 4
	move.l	a6,_SysBase		; Store it for future use
	bra.s	.check_dos

.sysbase_ok:
	move.l	d0,a6			; Use existing SysBase

.check_dos:
	; Check if DOSBase already initialized
	move.l	_DOSBase,d0
	bne.s	.already_open

	; OpenLibrary("dos.library", 0) - LVO offset -552
	lea	dos_name,a1		; Library name (absolute addressing)
	moveq	#0,d0			; Any version
	jsr	-552(a6)		; Call exec.library OpenLibrary()

	; Store result in _DOSBase
	move.l	d0,_DOSBase

.already_open:
	; Return DOSBase in d0
	move.l	_DOSBase,d0

	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers
	rts

; ----------------------------------------------------------------------------
; ___dos_cleanup - Cleanup DOS library
; ----------------------------------------------------------------------------
; Closes dos.library if it was opened
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___dos_cleanup:
	movem.l	d0/a1/a6,-(sp)		; Save registers

	; Check if DOSBase is initialized
	move.l	_DOSBase,d0
	beq.s	.not_open		; Skip if not open

	; Get exec.library base
	move.l	4.w,a6

	; CloseLibrary(DOSBase) - LVO offset -414
	move.l	_DOSBase,a1		; Library base to close
	jsr	-414(a6)		; Call exec.library CloseLibrary()

	; Clear DOSBase
	clr.l	_DOSBase

.not_open:
	movem.l	(sp)+,d0/a1/a6		; Restore registers
	rts
