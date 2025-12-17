; ============================================================================
; Intuition Library Initialization for Novus
; ============================================================================
; Provides automatic intuition.library opening/closing for Novus programs
;
; This module exports initialization functions that are called by the
; program to set up intuition.library access.
;
; Required by: intuition_stubs.s (all Intuition function stubs need _IntuitionBase)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_IntuitionBase		; Provided by library_bases.s
	xref	_SysBase		; Provided by library_bases.s
	xref	___novus_library_not_found	; Error handler in runtime_errors.c

; ============================================================================
; Exports
; ============================================================================
	xdef	___intuition_init	; Initialize Intuition library
	xdef	___intuition_cleanup	; Cleanup Intuition library
	xdef	___intuition_ensure	; Ensure Intuition library is open (for stubs)

; ============================================================================
; Data Section
; ============================================================================
	section	data,data

intuition_name:
	dc.b	'intuition.library',0	; Library name string
	even				; Word align

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___intuition_init - Initialize Intuition library
; ----------------------------------------------------------------------------
; Opens intuition.library and stores the base pointer in _IntuitionBase
;
; Input:  None
; Output: D0 = Intuition library base (0 if failed)
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___intuition_init:
	movem.l	d1/a0-a1/a6,-(sp)	; Save registers

	; Initialize SysBase if not already done
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6			; Get exec.library base from absolute address 4
	move.l	a6,_SysBase		; Store it for future use
	bra.s	.check_intuition

.sysbase_ok:
	move.l	d0,a6			; Use existing SysBase

.check_intuition:
	; Check if IntuitionBase already initialized
	move.l	_IntuitionBase,d0
	bne.s	.already_open

	; OpenLibrary("intuition.library", 0) - LVO offset -552
	move.l	#intuition_name,a1	; Library name (absolute addressing)
	moveq	#0,d0			; Any version
	jsr	-552(a6)		; Call exec.library OpenLibrary()

	; Check if OpenLibrary failed
	tst.l	d0
	beq.s	.library_failed

	; Store result in _IntuitionBase
	move.l	d0,_IntuitionBase

.already_open:
	; Return IntuitionBase in d0
	move.l	_IntuitionBase,d0

	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers
	rts

.library_failed:
	; Call error handler: __novus_library_not_found(name, version)
	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers first

	clr.l	-(sp)			; Push version = 0 (any version)
	pea	intuition_name		; Push library name pointer
	jsr	___novus_library_not_found
	addq.l	#8,sp			; Clean up stack

	; Return 0 (but we likely won't get here as error handler may exit)
	moveq	#0,d0
	rts

; ----------------------------------------------------------------------------
; ___intuition_cleanup - Cleanup Intuition library
; ----------------------------------------------------------------------------
; Closes intuition.library if it was opened
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___intuition_cleanup:
	movem.l	d0/a1/a6,-(sp)		; Save registers

	; Check if IntuitionBase is initialized
	move.l	_IntuitionBase,d0
	beq.s	.not_open		; Skip if not open

	; Get exec.library base
	move.l	4.w,a6

	; CloseLibrary(IntuitionBase) - LVO offset -414
	move.l	_IntuitionBase,a1	; Library base to close
	jsr	-414(a6)		; Call exec.library CloseLibrary()

	; Clear IntuitionBase
	clr.l	_IntuitionBase

.not_open:
	movem.l	(sp)+,d0/a1/a6		; Restore registers
	rts

; ----------------------------------------------------------------------------
; ___intuition_ensure - Ensure Intuition library is open (lazy init)
; ----------------------------------------------------------------------------
; Fast check if library is open, initializes if not. Called by stubs.
; Returns _IntuitionBase in A6 for immediate use.
;
; Input:  None
; Output: A6 = Intuition library base (for immediate use by stub)
; Modifies: A6 only - D0-D1/A0-A1 are PRESERVED (stubs need these for params!)
; ----------------------------------------------------------------------------
___intuition_ensure:
	move.l	_IntuitionBase,a6	; Load IntuitionBase into A6
	tst.l	a6			; Test if NULL (doesn't modify d0!)
	bne.s	.done			; Fast path - already open

	; Need to initialize - save regs and call init
	movem.l	d0-d1/a0-a1,-(sp)	; Save scratch regs (stubs have params in these!)
	bsr	___intuition_init	; Initialize (result in d0)
	move.l	d0,a6			; Move to a6
	movem.l	(sp)+,d0-d1/a0-a1	; Restore scratch regs
.done:
	rts
