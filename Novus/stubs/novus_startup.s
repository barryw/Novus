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

	; Call main()
	jsr	_main

	; Exit with return code from main (already in d0)
	rts				; Return to CLI

