; Generated from SFD file by Novus SFD Parser
; Library: radiobutton.library
; Base: _RadioButtonBase
; Each function is in its own section for dead code elimination

	xref	_RadioButtonBase

	section	_RADIOBUTTON_GetClass_stub,code

; Class * RADIOBUTTON_GetClass()
	xdef	_RADIOBUTTON_GetClass
_RADIOBUTTON_GetClass:
	movea.l	_RadioButtonBase,a6
	jsr	-30(a6)
	rts

	section	_AllocRadioButtonNodeA_stub,code

; struct Node * AllocRadioButtonNodeA(UWORD columns, struct TagItem * tags)
	xdef	_AllocRadioButtonNodeA
_AllocRadioButtonNodeA:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_RadioButtonBase,a6
	jsr	-36(a6)
	rts

	section	_FreeRadioButtonNode_stub,code

; VOID FreeRadioButtonNode(struct Node * node)
	xdef	_FreeRadioButtonNode
_FreeRadioButtonNode:
	movea.l	4(sp),a0
	movea.l	_RadioButtonBase,a6
	jsr	-42(a6)
	rts

	section	_SetRadioButtonNodeAttrsA_stub,code

; VOID SetRadioButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetRadioButtonNodeAttrsA
_SetRadioButtonNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_RadioButtonBase,a6
	jsr	-48(a6)
	rts

	section	_GetRadioButtonNodeAttrsA_stub,code

; VOID GetRadioButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetRadioButtonNodeAttrsA
_GetRadioButtonNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_RadioButtonBase,a6
	jsr	-54(a6)
	rts

