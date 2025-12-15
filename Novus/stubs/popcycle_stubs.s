; Generated from SFD file by Novus SFD Parser
; Library: popcycle.library
; Base: _PopCycleBase
; Each function is in its own section for dead code elimination

	xref	_PopCycleBase

	section	_POPCYCLE_GetClass_stub,code

; Class * POPCYCLE_GetClass()
	xdef	_POPCYCLE_GetClass
_POPCYCLE_GetClass:
	movea.l	_PopCycleBase,a6
	jsr	-30(a6)
	rts

	section	_AllocPopCycleNodeA_stub,code

; struct Node * AllocPopCycleNodeA(struct TagItem * tags)
	xdef	_AllocPopCycleNodeA
_AllocPopCycleNodeA:
	movea.l	4(sp),a0
	movea.l	_PopCycleBase,a6
	jsr	-36(a6)
	rts

	section	_FreePopCycleNode_stub,code

; VOID FreePopCycleNode(struct Node * node)
	xdef	_FreePopCycleNode
_FreePopCycleNode:
	movea.l	4(sp),a0
	movea.l	_PopCycleBase,a6
	jsr	-42(a6)
	rts

	section	_SetPopCycleNodeAttrsA_stub,code

; VOID SetPopCycleNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetPopCycleNodeAttrsA
_SetPopCycleNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_PopCycleBase,a6
	jsr	-48(a6)
	rts

	section	_GetPopCycleNodeAttrsA_stub,code

; VOID GetPopCycleNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetPopCycleNodeAttrsA
_GetPopCycleNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_PopCycleBase,a6
	jsr	-54(a6)
	rts

