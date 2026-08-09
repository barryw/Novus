; Generated from SFD file by Novus SFD Parser
; Library: chooser.library
; Base: _ChooserBase
; Each function is in its own section for dead code elimination

	xref	_ChooserBase

	section	_CHOOSER_GetClass_stub,code

; Class * CHOOSER_GetClass()
	xdef	_CHOOSER_GetClass
_CHOOSER_GetClass:
	movem.l	a6,-(sp)
	movea.l	_ChooserBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocChooserNodeA_stub,code

; struct Node * AllocChooserNodeA(struct TagItem * tags)
	xdef	_AllocChooserNodeA
_AllocChooserNodeA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ChooserBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocChooserNode_stub,code

; struct Node * AllocChooserNode(Tag tags, ... )
	xdef	_AllocChooserNode
_AllocChooserNode:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_ChooserBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeChooserNode_stub,code

; VOID FreeChooserNode(struct Node * node)
	xdef	_FreeChooserNode
_FreeChooserNode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ChooserBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetChooserNodeAttrsA_stub,code

; VOID SetChooserNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetChooserNodeAttrsA
_SetChooserNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_ChooserBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetChooserNodeAttrs_stub,code

; VOID SetChooserNodeAttrs(struct Node * node, ... )
	xdef	_SetChooserNodeAttrs
_SetChooserNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_ChooserBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetChooserNodeAttrsA_stub,code

; VOID GetChooserNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetChooserNodeAttrsA
_GetChooserNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_ChooserBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetChooserNodeAttrs_stub,code

; VOID GetChooserNodeAttrs(struct Node * node, ... )
	xdef	_GetChooserNodeAttrs
_GetChooserNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_ChooserBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

