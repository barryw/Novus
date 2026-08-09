; Generated from SFD file by Novus SFD Parser
; Library: clicktab.library
; Base: _ClickTabBase
; Each function is in its own section for dead code elimination

	xref	_ClickTabBase

	section	_CLICKTAB_GetClass_stub,code

; Class * CLICKTAB_GetClass()
	xdef	_CLICKTAB_GetClass
_CLICKTAB_GetClass:
	movem.l	a6,-(sp)
	movea.l	_ClickTabBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocClickTabNodeA_stub,code

; struct Node * AllocClickTabNodeA(struct TagItem * tags)
	xdef	_AllocClickTabNodeA
_AllocClickTabNodeA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ClickTabBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocClickTabNode_stub,code

; struct Node * AllocClickTabNode(Tag tags, ... )
	xdef	_AllocClickTabNode
_AllocClickTabNode:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_ClickTabBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeClickTabNode_stub,code

; VOID FreeClickTabNode(struct Node * node)
	xdef	_FreeClickTabNode
_FreeClickTabNode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ClickTabBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetClickTabNodeAttrsA_stub,code

; VOID SetClickTabNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetClickTabNodeAttrsA
_SetClickTabNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_ClickTabBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetClickTabNodeAttrs_stub,code

; VOID SetClickTabNodeAttrs(struct Node * node, ... )
	xdef	_SetClickTabNodeAttrs
_SetClickTabNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_ClickTabBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetClickTabNodeAttrsA_stub,code

; VOID GetClickTabNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetClickTabNodeAttrsA
_GetClickTabNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_ClickTabBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetClickTabNodeAttrs_stub,code

; VOID GetClickTabNodeAttrs(struct Node * node, ... )
	xdef	_GetClickTabNodeAttrs
_GetClickTabNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_ClickTabBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

