; Generated from SFD file by Novus SFD Parser
; Library: chooser.library
; Base: _ChooserBase
; Each function is in its own section for dead code elimination

	xref	_ChooserBase

	section	_CHOOSER_GetClass_stub,code

; Class * CHOOSER_GetClass()
	xdef	_CHOOSER_GetClass
_CHOOSER_GetClass:
	movea.l	_ChooserBase,a6
	jsr	-30(a6)
	rts

	section	_AllocChooserNodeA_stub,code

; struct Node * AllocChooserNodeA(struct TagItem * tags)
	xdef	_AllocChooserNodeA
_AllocChooserNodeA:
	movea.l	4(sp),a0
	movea.l	_ChooserBase,a6
	jsr	-36(a6)
	rts

	section	_FreeChooserNode_stub,code

; VOID FreeChooserNode(struct Node * node)
	xdef	_FreeChooserNode
_FreeChooserNode:
	movea.l	4(sp),a0
	movea.l	_ChooserBase,a6
	jsr	-42(a6)
	rts

	section	_SetChooserNodeAttrsA_stub,code

; VOID SetChooserNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetChooserNodeAttrsA
_SetChooserNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ChooserBase,a6
	jsr	-48(a6)
	rts

	section	_GetChooserNodeAttrsA_stub,code

; VOID GetChooserNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetChooserNodeAttrsA
_GetChooserNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ChooserBase,a6
	jsr	-54(a6)
	rts

