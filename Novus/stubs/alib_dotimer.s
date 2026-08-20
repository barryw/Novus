; NDK 3.9 declares DoTimer(), but its amiga.lib archive does not define it.
; This is the classic Exec implementation, linked only when DoTimer is reachable.

	xref	_SysBase

	section	_DoTimer_compat,code
	xdef	_DoTimer
_DoTimer:
	movem.l	d2-d3/a2-a4/a6,-(sp)
	movea.l	28(sp),a2		; timeval *
	move.l	32(sp),d2		; timer unit
	move.l	36(sp),d3		; command
	tst.l	a2
	beq.s	.fail

	movea.l	_SysBase,a6
	jsr	-666(a6)		; CreateMsgPort()
	tst.l	d0
	beq.s	.fail
	movea.l	d0,a3

	movea.l	a3,a0
	moveq	#40,d0			; sizeof(struct timerequest)
	jsr	-654(a6)		; CreateIORequest()
	tst.l	d0
	bne.s	.have_request
	moveq	#-1,d2			; IOERR_OPENFAIL
	bra.s	.delete_port
.have_request:
	movea.l	d0,a4

	lea	.timer_name(pc),a0
	move.l	d2,d0
	movea.l	a4,a1
	moveq	#0,d1
	jsr	-444(a6)		; OpenDevice()
	ext.w	d0
	ext.l	d0
	move.l	d0,d2
	bne.s	.delete_request

	move.w	d3,28(a4)		; tr_node.io_Command
	move.l	(a2),32(a4)		; tr_time.tv_secs
	move.l	4(a2),36(a4)		; tr_time.tv_micro
	movea.l	a4,a1
	jsr	-456(a6)		; DoIO()
	ext.w	d0
	ext.l	d0
	move.l	d0,d2
	move.l	32(a4),(a2)
	move.l	36(a4),4(a2)

	movea.l	a4,a1
	jsr	-450(a6)		; CloseDevice()
.delete_request:
	movea.l	a4,a0
	jsr	-660(a6)		; DeleteIORequest()
.delete_port:
	movea.l	a3,a0
	jsr	-672(a6)		; DeleteMsgPort()
	move.l	d2,d0
	bra.s	.done
.fail:
	moveq	#-1,d0			; IOERR_OPENFAIL
.done:
	movem.l	(sp)+,d2-d3/a2-a4/a6
	rts

.timer_name:
	dc.b	'timer.device',0
	even
