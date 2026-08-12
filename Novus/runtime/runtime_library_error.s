; Reliable required-library failure reporting without a C runtime dependency.
; C ABI: void __novus_library_not_found(const char *name, int32_t version)

	section	__novus_library_not_found,code
	xdef	___novus_library_not_found
	xref	_SysBase

___novus_library_not_found:
	movem.l	d6-d7/a2-a4/a6,-(sp)
	movea.l	28(sp),a4		; library name
	move.l	32(sp),d6		; minimum version
	movea.l	4.w,a0
	tst.b	294(a0)		; ExecBase.IDNestCnt
	bge	.alert
	tst.b	295(a0)		; ExecBase.TDNestCnt
	bge	.alert
	tst.l	276(a0)		; ExecBase.ThisTask
	beq	.alert

.try_requester:
	movea.l	_SysBase,a6
	lea	.intuition_name,a1
	moveq	#33,d0
	jsr	-552(a6)		; OpenLibrary()
	move.l	d0,d7
	beq.s	.alert

.show_requester:
	move.l	d6,-(sp)
	move.l	a4,-(sp)
	movea.l	d7,a6
	suba.l	a0,a0
	lea	.request,a1
	suba.l	a2,a2
	lea	(sp),a3
	jsr	-588(a6)		; EasyRequest()
	addq.l	#8,sp
	movea.l	d7,a1
	movea.l	_SysBase,a6
	jsr	-414(a6)		; CloseLibrary()
	bra.s	.done

.alert:
	movea.l	_SysBase,a6
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
	dc.b	'Novus failure',0
.text:
	dc.b	'Cannot open %s v%ld+.',10,'Add it to LIBS: and retry.',0
.ok:
	dc.b	'OK',0

	end
