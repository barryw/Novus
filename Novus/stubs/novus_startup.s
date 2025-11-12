; ============================================================================
; Novus Minimal AmigaOS Startup Code
; ============================================================================
; Provides a clean entry point for Novus programs that doesn't require
; VBCC's startup.o or -lamiga dependencies
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_main			; User's main() function
	xref	_SysBase		; From library_bases.s
	xref	_DOSBase		; From library_bases.s
	xref	_IntuitionBase		; From library_bases.s
	xref	___dos_init		; From dos_init.s
	xref	___dos_cleanup		; From dos_init.s

; ============================================================================
; Entry Point
; ============================================================================
	xdef	_start			; Entry point for executable

_start:
	; AmigaOS calls us with:
	;   a0 = command line length (arglen)
	;   a1 = pointer to command line (argptr)
	;   d0 = length of command line
	; We're not using these yet, but we should preserve them

	; Initialize SysBase from absolute location 4
	move.l	4.w,a6
	move.l	a6,_SysBase

	; Initialize DOS library (needed by runtime I/O functions)
	jsr	___dos_init
	tst.l	d0
	beq.s	.exit_no_dos		; Exit if DOS library couldn't open

	; Open intuition.library v33
	movea.l	_SysBase,a6		; Get exec.library base
	lea	.intuition_name(pc),a1	; Library name
	moveq	#33,d0			; Minimum version (v33 = AmigaOS 1.2+)
	jsr	-552(a6)		; OpenLibrary()
	move.l	d0,_IntuitionBase	; Save the base
	beq.s	.no_intuition		; If NULL, skip cleanup

	; Call main()
	jsr	_main

	; Close intuition.library
	move.l	d0,-(sp)		; Save return code
	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_IntuitionBase,a1	; Library to close
	jsr	-414(a6)		; CloseLibrary()
	move.l	(sp)+,d0		; Restore return code

.no_intuition:
	; Clean up DOS library
	move.l	d0,-(sp)		; Save return code
	jsr	___dos_cleanup
	move.l	(sp)+,d0		; Restore return code

.exit_no_dos:
	; Exit with return code from main (already in d0)
	rts				; Return to CLI

.intuition_name:
	dc.b	'intuition.library',0
	even

