; Small, alignment-safe memory primitives for generated Novus code.
; Uses only volatile VBCC registers (d0-d1/a0-a1) and byte accesses.

	section	__novus_memset,code
	xdef	___novus_memset
___novus_memset:
	movea.l	4(sp),a0
	move.l	8(sp),d1
	move.l	12(sp),d0
	beq.s	.memset_done
.memset_loop:
	move.b	d1,(a0)+
	subq.l	#1,d0
	bne.s	.memset_loop
.memset_done:
	rts

	section	__novus_memcpy,code
	xdef	___novus_memcpy
___novus_memcpy:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	beq.s	.memcpy_done
.memcpy_loop:
	move.b	(a1)+,d1
	move.b	d1,(a0)+
	subq.l	#1,d0
	bne.s	.memcpy_loop
.memcpy_done:
	rts

	end
