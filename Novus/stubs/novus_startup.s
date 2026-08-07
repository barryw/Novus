; ============================================================================
; Novus Minimal AmigaOS Startup Code
; ============================================================================
; Provides a clean entry point for Novus programs that doesn't require
; VBCC's startup.o or -lamiga dependencies.
;
; MINIMAL DESIGN: This startup only initializes SysBase and DOSBase.
; Other libraries (Intuition, Graphics, Diskfont, GadTools) are initialized
; LAZILY when first needed via their respective _init functions.
; This keeps binary size small for programs that don't use these libraries.
; ============================================================================

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_main			; User's main() function
	xref	_SysBase		; From library_bases.s
	xref	_DOSBase		; From library_bases.s
	xref	_IntuitionBase		; From library_bases.s
	xref	_GadToolsBase		; From library_bases.s
	xref	_GfxBase		; From library_bases.s
	xref	_DiskfontBase		; From library_bases.s
	xref	_WBStartupMsg		; From library_bases.s (WBStartup message)
	xref	___dos_init		; From dos_init.s
	xref	___dos_cleanup		; From dos_init.s
	xref	___novus_ffi_init	; Generated exact FFI dependencies
	xref	___novus_ffi_cleanup

; ============================================================================
; Entry Point
; ============================================================================
	xdef	_start			; Entry point for executable

_start:
	; AmigaOS entry point - can be called from CLI or Workbench
	; Detection strategy:
	;   CLI:       d0 = 0 (no WBStartup message on port yet)
	;   Workbench: d0 = 0, but pr_CLI field in Process is NULL
	;
	; We need to check our Process structure to determine launch type

	; Initialize SysBase from absolute location 4
	move.l	4.w,a6
	move.l	a6,_SysBase

	; Clear WBStartup message pointer (assume CLI)
	clr.l	_WBStartupMsg

	; Get our Process structure via FindTask(NULL)
	suba.l	a1,a1			; NULL parameter
	jsr	-294(a6)		; FindTask() - LVO -294
	move.l	d0,a4			; Save Process pointer in a4

	; Check pr_CLI field in Process structure (offset 172)
	move.l	172(a4),d0		; Get pr_CLI field
	bne.s	.cli_startup		; If non-zero, we're CLI

	; Workbench startup - get WBStartup message
	; The message is at our Process's message port (pr_MsgPort at offset 92)
	lea	92(a4),a0		; a0 = &pr_MsgPort
	movea.l	_SysBase,a6		; Get exec.library base
	jsr	-384(a6)		; WaitPort() - LVO -384

	lea	92(a4),a0		; a0 = &pr_MsgPort again
	movea.l	_SysBase,a6		; Get exec.library base
	jsr	-372(a6)		; GetMsg() - LVO -372

	move.l	d0,_WBStartupMsg	; Save WBStartup message pointer
	beq.w	.exit_no_msg		; If NULL, something went wrong

	; Set current directory to first WBArg's lock (the program icon's directory)
	move.l	d0,a2			; a2 = WBStartup message
	move.l	36(a2),a3		; a3 = ArgList pointer
	move.l	(a3),d1			; d1 = first WBArg's wa_Lock
	beq.s	.no_lock		; If lock is NULL, skip CurrentDir

	; Actually, we need DOS first. Do it below.

.no_lock:
.cli_startup:
	; Initialize DOS library (needed by runtime I/O functions)
	jsr	___dos_init
	tst.l	d0
	beq.w	.exit_no_dos		; Exit if DOS library couldn't open

	; If Workbench startup and we have a lock, set current directory
	tst.l	_WBStartupMsg
	beq.s	.skip_currentdir	; Skip if CLI

	move.l	_WBStartupMsg,a2	; Get WBStartup message
	move.l	36(a2),a3		; Get ArgList pointer
	move.l	(a3),d1			; Get first WBArg's wa_Lock
	beq.s	.skip_currentdir	; Skip if lock is NULL

	; Call CurrentDir(lock)
	movea.l	_DOSBase,a6		; Get dos.library base
	jsr	-126(a6)		; CurrentDir() - LVO -126

.skip_currentdir:
	; Open only the libraries/resources/devices proven reachable in IR.
	jsr	___novus_ffi_init
	tst.l	d0
	beq.s	.ffi_init_failed

	; Call main()
	jsr	_main

	; Save return code
	move.l	d0,-(sp)
	jsr	___novus_ffi_cleanup
	bra.s	.cleanup_core

.ffi_init_failed:
	moveq	#20,d0			; RETURN_FAIL
	move.l	d0,-(sp)

	; Clean up libraries that were lazily initialized
	; Note: We check if the base is non-NULL before closing

.cleanup_core:
	; Close GadTools if it was opened
	tst.l	_GadToolsBase
	beq.s	.no_gadtools
	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_GadToolsBase,a1	; Library to close
	jsr	-414(a6)		; CloseLibrary()

.no_gadtools:
	; Close Intuition if it was opened
	tst.l	_IntuitionBase
	beq.s	.no_intuition
	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_IntuitionBase,a1	; Library to close
	jsr	-414(a6)		; CloseLibrary()

.no_intuition:
	; Close Diskfont if it was opened
	tst.l	_DiskfontBase
	beq.s	.no_diskfont
	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_DiskfontBase,a1	; Library to close
	jsr	-414(a6)		; CloseLibrary()

.no_diskfont:
	; Close Graphics if it was opened
	tst.l	_GfxBase
	beq.s	.no_graphics
	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_GfxBase,a1		; Library to close
	jsr	-414(a6)		; CloseLibrary()

.no_graphics:
	; Clean up DOS library
	jsr	___dos_cleanup

	; Restore return code
	move.l	(sp)+,d0

	; Reply to WBStartup message if we got one
	tst.l	_WBStartupMsg
	beq.s	.no_wb_reply		; Skip if CLI

	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_WBStartupMsg,a1	; Get WBStartup message
	jsr	-378(a6)		; ReplyMsg() - LVO -378

.no_wb_reply:
.exit_no_dos:
.exit_no_msg:
	; Exit with return code from main (already in d0)
	rts				; Return to CLI/Workbench
