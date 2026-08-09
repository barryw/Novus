; Generated from SFD file by Novus SFD Parser
; Library: radiobutton.library
; Base: _RadioButtonBase
; Each function is in its own section for dead code elimination

	xref	_RadioButtonBase

	section	_RADIOBUTTON_GetClass_stub,code

; Class * RADIOBUTTON_GetClass()
	xdef	_RADIOBUTTON_GetClass
_RADIOBUTTON_GetClass:
	movem.l	a6,-(sp)
	movea.l	_RadioButtonBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocRadioButtonNodeA_stub,code

; struct Node * AllocRadioButtonNodeA(UWORD columns, struct TagItem * tags)
	xdef	_AllocRadioButtonNodeA
_AllocRadioButtonNodeA:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_RadioButtonBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocRadioButtonNode_stub,code

; struct Node * AllocRadioButtonNode(UWORD columns, Tag tags, ... )
	xdef	_AllocRadioButtonNode
_AllocRadioButtonNode:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	lea	12(sp),a0
	movea.l	_RadioButtonBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeRadioButtonNode_stub,code

; VOID FreeRadioButtonNode(struct Node * node)
	xdef	_FreeRadioButtonNode
_FreeRadioButtonNode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RadioButtonBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetRadioButtonNodeAttrsA_stub,code

; VOID SetRadioButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetRadioButtonNodeAttrsA
_SetRadioButtonNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_RadioButtonBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetRadioButtonNodeAttrs_stub,code

; VOID SetRadioButtonNodeAttrs(struct Node * node, ... )
	xdef	_SetRadioButtonNodeAttrs
_SetRadioButtonNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_RadioButtonBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetRadioButtonNodeAttrsA_stub,code

; VOID GetRadioButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetRadioButtonNodeAttrsA
_GetRadioButtonNodeAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_RadioButtonBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetRadioButtonNodeAttrs_stub,code

; VOID GetRadioButtonNodeAttrs(struct Node * node, ... )
	xdef	_GetRadioButtonNodeAttrs
_GetRadioButtonNodeAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_RadioButtonBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

