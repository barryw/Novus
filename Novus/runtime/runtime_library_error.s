; Reliable required-library failure reporting without a C runtime dependency.
; C ABI: void __novus_library_not_found(const char *name, int32_t version)

	section	__novus_library_not_found,code
	xdef	___novus_library_not_found
	xref	_DOSBase

___novus_library_not_found:
	movem.l	d6-d7/a2-a4/a6,-(sp)
	movea.l	28(sp),a4		; library name
	move.l	32(sp),d6		; minimum version
	movea.l	4.w,a0
	move.b	294(a0),d0		; ExecBase.IDNestCnt
	and.b	295(a0),d0		; both counters must be negative
	bpl.s	.alert
	tst.l	276(a0)		; ExecBase.ThisTask
	beq.s	.alert

.try_output:
	move.l	_DOSBase,d0
	beq.s	.try_requester
	movea.l	d0,a6
	jsr	-60(a6)		; Output()
	tst.l	d0
	beq.s	.try_requester
	move.l	d6,-(sp)
	move.l	a4,-(sp)
	move.l	sp,d3
	lea	.cli_text(pc),a0
	move.l	a0,d2
	move.l	d0,d1
	jsr	-354(a6)		; VFPrintf()
	addq.l	#8,sp
	bra.s	.done

.try_requester:
	movea.l	4.w,a6
	lea	.intuition_name(pc),a1
	moveq	#33,d0
	jsr	-552(a6)		; OpenLibrary()
	move.l	d0,d7
	beq.s	.alert

.show_requester:
	move.l	d6,-(sp)
	move.l	a4,-(sp)
	movea.l	d7,a6
	suba.l	a0,a0
	lea	.request(pc),a1
	suba.l	a2,a2
	lea	(sp),a3
	jsr	-588(a6)		; EasyRequest()
	addq.l	#8,sp
	movea.l	d7,a1
	movea.l	4.w,a6
	jsr	-414(a6)		; CloseLibrary()
	bra.s	.done

.alert:
	movea.l	4.w,a6
	move.l	#$7f00000d,d7		; AN_NovusLib | AG_NovusError | AO_LibraryNotFound
	jsr	-108(a6)		; Alert()

.done:
	movem.l	(sp)+,d6-d7/a2-a4/a6
	rts

	cnop	0,4
.request:
	dc.l	20,0,.title,.text,.ok
.intuition_name:
	dc.b	'intuition.library',0
.title:
	dc.b	'Novus',0
.text:
	dc.b	'Need %s v%ld+.',0
.cli_text:
	dc.b	'Novus: need %s v%ld+',10,0
.ok:
	dc.b	'OK',0

	end
