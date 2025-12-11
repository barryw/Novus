; ============================================================================
; GadTools Library Initialization for Novus
; ============================================================================
; Provides automatic gadtools.library opening/closing for Novus programs
;
; This module exports initialization functions that are called by the
; program to set up gadtools.library access.
;
; Required by: gadtools_stubs.s (all GadTools function stubs need _GadToolsBase)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_GadToolsBase		; Provided by library_bases.s
	xref	_SysBase		; Provided by library_bases.s

; ============================================================================
; Exports
; ============================================================================
	xdef	___gadtools_init	; Initialize GadTools library
	xdef	___gadtools_cleanup	; Cleanup GadTools library
	xdef	___gadtools_ensure	; Ensure GadTools library is open (for stubs)

; ============================================================================
; Data Section
; ============================================================================
	section	data,data

gadtools_name:
	dc.b	'gadtools.library',0	; Library name string
	even				; Word align

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___gadtools_init - Initialize GadTools library
; ----------------------------------------------------------------------------
; Opens gadtools.library and stores the base pointer in _GadToolsBase
;
; Input:  None
; Output: D0 = GadTools library base (0 if failed)
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___gadtools_init:
	movem.l	d1/a0-a1/a6,-(sp)	; Save registers

	; Initialize SysBase if not already done
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6			; Get exec.library base from absolute address 4
	move.l	a6,_SysBase		; Store it for future use
	bra.s	.check_gadtools

.sysbase_ok:
	move.l	d0,a6			; Use existing SysBase

.check_gadtools:
	; Check if GadToolsBase already initialized
	move.l	_GadToolsBase,d0
	bne.s	.already_open

	; OpenLibrary("gadtools.library", 0) - LVO offset -552
	move.l	#gadtools_name,a1	; Library name (absolute addressing)
	moveq	#0,d0			; Any version
	jsr	-552(a6)		; Call exec.library OpenLibrary()

	; Store result in _GadToolsBase
	move.l	d0,_GadToolsBase

.already_open:
	; Return GadToolsBase in d0
	move.l	_GadToolsBase,d0

	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers
	rts

; ----------------------------------------------------------------------------
; ___gadtools_cleanup - Cleanup GadTools library
; ----------------------------------------------------------------------------
; Closes gadtools.library if it was opened
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___gadtools_cleanup:
	movem.l	d0/a1/a6,-(sp)		; Save registers

	; Check if GadToolsBase is initialized
	move.l	_GadToolsBase,d0
	beq.s	.not_open		; Skip if not open

	; Get exec.library base
	move.l	4.w,a6

	; CloseLibrary(GadToolsBase) - LVO offset -414
	move.l	_GadToolsBase,a1	; Library base to close
	jsr	-414(a6)		; Call exec.library CloseLibrary()

	; Clear GadToolsBase
	clr.l	_GadToolsBase

.not_open:
	movem.l	(sp)+,d0/a1/a6		; Restore registers
	rts

; ----------------------------------------------------------------------------
; ___gadtools_ensure - Ensure GadTools library is open (lazy init)
; ----------------------------------------------------------------------------
; Fast check if library is open, initializes if not. Called by stubs.
; Returns _GadToolsBase in A6 for immediate use.
;
; Input:  None
; Output: A6 = GadTools library base (for immediate use by stub)
; Modifies: A6 only - D0-D1/A0-A1 are PRESERVED (stubs need these for params!)
; ----------------------------------------------------------------------------
___gadtools_ensure:
	move.l	_GadToolsBase,a6	; Load GadToolsBase into A6
	tst.l	a6			; Test if NULL (doesn't modify d0!)
	bne.s	.done			; Fast path - already open

	; Need to initialize - save regs and call init
	movem.l	d0-d1/a0-a1,-(sp)	; Save scratch regs (stubs have params in these!)
	bsr	___gadtools_init	; Initialize (result in d0)
	move.l	d0,a6			; Move to a6
	movem.l	(sp)+,d0-d1/a0-a1	; Restore scratch regs
.done:
	rts
