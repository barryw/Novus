; ============================================================================
; MUI (Magic User Interface) Initialization for Novus
; ============================================================================
; Provides automatic MUI library opening/closing for Novus programs.
;
; MUI is a BOOPSI-based GUI toolkit requiring muimaster.library.
; Requires AmigaOS 2.0+ with MUI installed.
;
; Required by: MUI builder API (std/ui/mui.novus)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_MUIMasterBase		; Provided by library_bases.s
	xref	_SysBase		; Provided by library_bases.s
	xref	___novus_library_not_found	; Error handler in runtime_errors.c

; ============================================================================
; Exports
; ============================================================================
	xdef	___mui_init		; Initialize MUI library
	xdef	___mui_cleanup		; Cleanup MUI library
	xdef	___mui_ensure		; Ensure MUI library is open

; ============================================================================
; Data Section - Library Name
; ============================================================================
	section	data,data

muimaster_name:
	dc.b	'muimaster.library',0
	even

MUI_VERSION	equ	20		; MUI requires version 20+

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___mui_init - Initialize MUI library
; ----------------------------------------------------------------------------
; Opens muimaster.library and stores the base pointer.
;
; Input:  None
; Output: D0 = MUIMasterBase (0 if failed)
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___mui_init:
	movem.l	d1/a0-a1/a6,-(sp)

	; Get SysBase
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6
	move.l	a6,_SysBase
	bra.s	.check_mui
.sysbase_ok:
	move.l	d0,a6

	; Check if already open
.check_mui:
	move.l	_MUIMasterBase,d0
	bne.s	.already_open

	; OpenLibrary("muimaster.library", MUI_VERSION)
	move.l	#muimaster_name,a1	; Library name (absolute addressing)
	moveq	#MUI_VERSION,d0
	jsr	-552(a6)		; OpenLibrary

	; Check if OpenLibrary failed
	tst.l	d0
	beq.s	.library_failed

	move.l	d0,_MUIMasterBase

.already_open:
	; Return MUIMasterBase in d0
	move.l	_MUIMasterBase,d0

	movem.l	(sp)+,d1/a0-a1/a6
	rts

.library_failed:
	; Call error handler: __novus_library_not_found(name, version)
	movem.l	(sp)+,d1/a0-a1/a6	; Restore registers first

	move.l	#MUI_VERSION,-(sp)	; Push version = 20
	pea	muimaster_name		; Push library name pointer
	jsr	___novus_library_not_found
	addq.l	#8,sp			; Clean up stack

	; Return 0 (but we likely won't get here as error handler may exit)
	moveq	#0,d0
	rts

; ----------------------------------------------------------------------------
; ___mui_cleanup - Close MUI library
; ----------------------------------------------------------------------------
; Closes muimaster.library if it was opened.
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___mui_cleanup:
	movem.l	d0-d1/a0-a1/a6,-(sp)

	; Get SysBase
	move.l	4.w,a6

	; Close muimaster.library
	move.l	_MUIMasterBase,d0
	beq.s	.done
	move.l	d0,a1
	jsr	-414(a6)			; CloseLibrary
	clr.l	_MUIMasterBase

.done:
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; ----------------------------------------------------------------------------
; ___mui_ensure - Ensure MUI library is open (lazy init)
; ----------------------------------------------------------------------------
; Fast check if library is open, initializes if not. Called by stubs.
; Returns _MUIMasterBase in A6 for immediate use.
;
; Input:  None
; Output: A6 = MUIMasterBase (for immediate use by stub)
; Modifies: A6 only - D0-D1/A0-A1 are PRESERVED (stubs need these for params!)
; ----------------------------------------------------------------------------
___mui_ensure:
	move.l	_MUIMasterBase,a6	; Load MUIMasterBase into A6
	tst.l	a6			; Test if NULL (doesn't modify d0!)
	bne.s	.done			; Fast path - already open

	; Need to initialize - save regs and call init
	movem.l	d0-d1/a0-a1,-(sp)	; Save scratch regs (stubs have params in these!)
	bsr	___mui_init		; Initialize (result in d0)
	move.l	d0,a6			; Move to a6
	movem.l	(sp)+,d0-d1/a0-a1	; Restore scratch regs
.done:
	rts

	end
