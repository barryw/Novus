; Generated from SFD file by Novus SFD Parser
; Library: speedbar.library
; Base: _SpeedBarBase
; Each function is in its own section for dead code elimination

	xref	_SpeedBarBase

	section	_SPEEDBAR_GetClass_stub,code

; Class * SPEEDBAR_GetClass()
	xdef	_SPEEDBAR_GetClass
_SPEEDBAR_GetClass:
	movem.l	a6,-(sp)
	movea.l	_SpeedBarBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocSpeedButtonNodeA_stub,code

; struct Node * AllocSpeedButtonNodeA(UWORD number, struct TagItem * tags)
	xdef	_AllocSpeedButtonNodeA
_AllocSpeedButtonNodeA:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_SpeedBarBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocSpeedButtonNode_stub,code

; struct Node * AllocSpeedButtonNode(UWORD number, Tag tags, ... )
	xdef	_AllocSpeedButtonNode
_AllocSpeedButtonNode:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	lea	12(sp),a0
	movea.l	_SpeedBarBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeSpeedButtonNode_stub,code

; VOID FreeSpeedButtonNode(struct Node * node)
	xdef	_FreeSpeedButtonNode
_FreeSpeedButtonNode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_SpeedBarBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetSpeedButtonNodeAttrsA_stub,code

; VOID SetSpeedButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetSpeedButtonNodeAttrsA
_SetSpeedButtonNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_SpeedBarBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetSpeedButtonNodeAttrs_stub,code

; VOID SetSpeedButtonNodeAttrs(struct Node * node, ... )
	xdef	_SetSpeedButtonNodeAttrs
_SetSpeedButtonNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_SpeedBarBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetSpeedButtonNodeAttrsA_stub,code

; VOID GetSpeedButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetSpeedButtonNodeAttrsA
_GetSpeedButtonNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_SpeedBarBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetSpeedButtonNodeAttrs_stub,code

; VOID GetSpeedButtonNodeAttrs(struct Node * node, ... )
	xdef	_GetSpeedButtonNodeAttrs
_GetSpeedButtonNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_SpeedBarBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

