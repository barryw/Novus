; AmigaDOS filesystem handlers have pr_CLI == 0, but their first port message is
; a StandardPacket, not a WBStartup. Leave it queued for main() to receive.

	section "CODE",code

	xref _main
	xref _SysBase
	xref ___dos_init
	xref ___dos_cleanup
	xref ___novus_ffi_init
	xref ___novus_ffi_cleanup

	xdef _start
_start:
	move.l 4.w,a6
	move.l a6,_SysBase
	jsr ___dos_init
	tst.l d0
	beq.s .failed
	jsr ___novus_ffi_init
	tst.l d0
	beq.s .cleanup_dos
	jsr _main
	move.l d0,-(sp)
	jsr ___novus_ffi_cleanup
	jsr ___dos_cleanup
	move.l (sp)+,d0
	rts
.cleanup_dos:
	jsr ___dos_cleanup
.failed:
	moveq #20,d0
	rts
