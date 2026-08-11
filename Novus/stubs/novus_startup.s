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
	xref	_WBStartupMsg		; From library_bases.s (WBStartup message)
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
	jsr	-384(a6)		; WaitPort() - LVO -384

	lea	92(a4),a0		; a0 = &pr_MsgPort again
	jsr	-372(a6)		; GetMsg() - LVO -372

	move.l	d0,_WBStartupMsg	; Save WBStartup message pointer
	beq.w	.exit_no_msg		; If NULL, something went wrong

.cli_startup:
	; Open the exact libraries proven reachable in IR, plus DOS for startup.
	jsr	___novus_ffi_init
	tst.l	d0
	beq.s	.ffi_init_failed

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
	; Call main()
	jsr	_main

	; Save return code
	move.l	d0,-(sp)
	jsr	___novus_ffi_cleanup
	bra.s	.cleanup_core

.ffi_init_failed:
	moveq	#20,d0			; RETURN_FAIL
	move.l	d0,-(sp)

.cleanup_core:
	; Restore return code
	move.l	(sp)+,d0

	; Reply to WBStartup message if we got one
	tst.l	_WBStartupMsg
	beq.s	.no_wb_reply		; Skip if CLI

	movea.l	_SysBase,a6		; Get exec.library base
	movea.l	_WBStartupMsg,a1	; Get WBStartup message
	jsr	-378(a6)		; ReplyMsg() - LVO -378

.no_wb_reply:
.exit_no_msg:
	; Exit with return code from main (already in d0)
	rts				; Return to CLI/Workbench
