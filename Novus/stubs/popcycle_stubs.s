; Generated from SFD file by Novus SFD Parser
; Library: popcycle.library
; Base: _PopCycleBase
; Each function is in its own section for dead code elimination

	xref	_PopCycleBase

	section	_POPCYCLE_GetClass_stub,code

; Class * POPCYCLE_GetClass()
	xdef	_POPCYCLE_GetClass
_POPCYCLE_GetClass:
	movem.l	a6,-(sp)
	movea.l	_PopCycleBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocPopCycleNodeA_stub,code

; struct Node * AllocPopCycleNodeA(struct TagItem * tags)
	xdef	_AllocPopCycleNodeA
_AllocPopCycleNodeA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_PopCycleBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocPopCycleNode_stub,code

; struct Node * AllocPopCycleNode(Tag tags, ... )
	xdef	_AllocPopCycleNode
_AllocPopCycleNode:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_PopCycleBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreePopCycleNode_stub,code

; VOID FreePopCycleNode(struct Node * node)
	xdef	_FreePopCycleNode
_FreePopCycleNode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_PopCycleBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetPopCycleNodeAttrsA_stub,code

; VOID SetPopCycleNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetPopCycleNodeAttrsA
_SetPopCycleNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_PopCycleBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetPopCycleNodeAttrs_stub,code

; VOID SetPopCycleNodeAttrs(struct Node * node, ... )
	xdef	_SetPopCycleNodeAttrs
_SetPopCycleNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_PopCycleBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetPopCycleNodeAttrsA_stub,code

; VOID GetPopCycleNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetPopCycleNodeAttrsA
_GetPopCycleNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_PopCycleBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetPopCycleNodeAttrs_stub,code

; VOID GetPopCycleNodeAttrs(struct Node * node, ... )
	xdef	_GetPopCycleNodeAttrs
_GetPopCycleNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_PopCycleBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

