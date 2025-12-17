; ============================================================================
; RexxSysLib Library Initialization for Novus
; ============================================================================
; Provides automatic rexxsyslib.library opening/closing for Novus programs
;
; This module exports initialization functions that are called by the
; program to set up rexxsyslib.library access.
;
; Required by: rexxsyslib_stubs.s (all ARexx function stubs need _RexxSysBase)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_SysBase		; Provided by library_bases.s
	xref	___novus_library_not_found	; Error handler in runtime_errors.c

; ============================================================================
; Exports
; ============================================================================
	xdef	_RexxSysBase		; Library base pointer
	xdef	___rexxsyslib_init	; Initialize RexxSysLib library
	xdef	___rexxsyslib_cleanup	; Cleanup RexxSysLib library
	xdef	___rexxsyslib_ensure	; Ensure RexxSysLib library is open (for stubs)

; ============================================================================
; Data Section
; ============================================================================
	section	data,data

_RexxSysBase:
	dc.l	0			; Library base pointer (initialized to NULL)

rexxsyslib_name:
	dc.b	'rexxsyslib.library',0	; Library name string
	even				; Word align

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___rexxsyslib_init - Initialize RexxSysLib library
; ----------------------------------------------------------------------------
; Opens rexxsyslib.library and stores the base pointer in _RexxSysBase
;
; Input:  None
; Output: D0 = RexxSysLib library base (0 if failed)
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___rexxsyslib_init:
	movem.l	d1/a0-a1/a6,-(sp)	; Save registers

	; Initialize SysBase if not already done
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6			; Get exec.library base from absolute address 4
	move.l	a6,_SysBase		; Store it for future use
	bra.s	.check_rexxsyslib

.sysbase_ok:
	move.l	d0,a6			; Use existing SysBase

.check_rexxsyslib:
	; Check if RexxSysBase already initialized
	move.l	_RexxSysBase,d0
	bne.s	.already_open

	; OpenLibrary("rexxsyslib.library", 0) - LVO offset -552
	move.l	#rexxsyslib_name,a1	; Library name (absolute addressing)
	moveq	#0,d0			; Any version
	jsr	-552(a6)		; Call exec.library OpenLibrary()

	; Check if OpenLibrary failed
	tst.l	d0
	beq.s	.library_failed

	; Store result in _RexxSysBase
	move.l	d0,_RexxSysBase

.already_open:
	; Return RexxSysBase in d0
	move.l	_RexxSysBase,d0

	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers
	rts

.library_failed:
	; Call error handler: __novus_library_not_found(name, version)
	; Arguments: library name (const char*), version (int32_t)
	; Stack: push version first (rightmost arg), then name
	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers first

	clr.l	-(sp)			; Push version = 0 (any version)
	pea	rexxsyslib_name		; Push library name pointer
	jsr	___novus_library_not_found
	addq.l	#8,sp			; Clean up stack

	; Return 0 (but we likely won't get here as error handler may exit)
	moveq	#0,d0
	rts

; ----------------------------------------------------------------------------
; ___rexxsyslib_cleanup - Cleanup RexxSysLib library
; ----------------------------------------------------------------------------
; Closes rexxsyslib.library if it was opened
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___rexxsyslib_cleanup:
	movem.l	d0/a1/a6,-(sp)		; Save registers

	; Check if RexxSysBase is initialized
	move.l	_RexxSysBase,d0
	beq.s	.not_open		; Skip if not open

	; Get exec.library base
	move.l	4.w,a6

	; CloseLibrary(RexxSysBase) - LVO offset -414
	move.l	_RexxSysBase,a1		; Library base to close
	jsr	-414(a6)		; Call exec.library CloseLibrary()

	; Clear RexxSysBase
	clr.l	_RexxSysBase

.not_open:
	movem.l	(sp)+,d0/a1/a6		; Restore registers
	rts

; ----------------------------------------------------------------------------
; ___rexxsyslib_ensure - Ensure RexxSysLib library is open (lazy init)
; ----------------------------------------------------------------------------
; Fast check if library is open, initializes if not. Called by stubs.
; Returns _RexxSysBase in A6 for immediate use.
;
; Input:  None
; Output: A6 = RexxSysLib library base (for immediate use by stub)
; Modifies: A6 only - D0-D1/A0-A1 are PRESERVED (stubs need these for params!)
; ----------------------------------------------------------------------------
___rexxsyslib_ensure:
	move.l	_RexxSysBase,a6		; Load RexxSysBase into A6
	tst.l	a6			; Test if NULL (doesn't modify d0!)
	bne.s	.done			; Fast path - already open

	; Need to initialize - save regs and call init
	movem.l	d0-d1/a0-a1,-(sp)	; Save scratch regs (stubs have params in these!)
	bsr	___rexxsyslib_init	; Initialize (result in d0)
	move.l	d0,a6			; Move to a6
	movem.l	(sp)+,d0-d1/a0-a1	; Restore scratch regs
.done:
	rts
