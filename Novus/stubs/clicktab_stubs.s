; Generated from SFD file by Novus SFD Parser
; Library: clicktab.library
; Base: _ClickTabBase
; Each function is in its own section for dead code elimination

	xref	_ClickTabBase

	section	_CLICKTAB_GetClass_stub,code

; Class * CLICKTAB_GetClass()
	xdef	_CLICKTAB_GetClass
_CLICKTAB_GetClass:
	movea.l	_ClickTabBase,a6
	jsr	-30(a6)
	rts

	section	_AllocClickTabNodeA_stub,code

; struct Node * AllocClickTabNodeA(struct TagItem * tags)
	xdef	_AllocClickTabNodeA
_AllocClickTabNodeA:
	movea.l	4(sp),a0
	movea.l	_ClickTabBase,a6
	jsr	-36(a6)
	rts

	section	_FreeClickTabNode_stub,code

; VOID FreeClickTabNode(struct Node * node)
	xdef	_FreeClickTabNode
_FreeClickTabNode:
	movea.l	4(sp),a0
	movea.l	_ClickTabBase,a6
	jsr	-42(a6)
	rts

	section	_SetClickTabNodeAttrsA_stub,code

; VOID SetClickTabNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetClickTabNodeAttrsA
_SetClickTabNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ClickTabBase,a6
	jsr	-48(a6)
	rts

	section	_GetClickTabNodeAttrsA_stub,code

; VOID GetClickTabNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetClickTabNodeAttrsA
_GetClickTabNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ClickTabBase,a6
	jsr	-54(a6)
	rts

