; Generated from SFD file by Novus SFD Parser
; Library: speedbar.library
; Base: _SpeedBarBase
; Each function is in its own section for dead code elimination

	xref	_SpeedBarBase

	section	_SPEEDBAR_GetClass_stub,code

; Class * SPEEDBAR_GetClass()
	xdef	_SPEEDBAR_GetClass
_SPEEDBAR_GetClass:
	movea.l	_SpeedBarBase,a6
	jsr	-30(a6)
	rts

	section	_AllocSpeedButtonNodeA_stub,code

; struct Node * AllocSpeedButtonNodeA(UWORD number, struct TagItem * tags)
	xdef	_AllocSpeedButtonNodeA
_AllocSpeedButtonNodeA:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_SpeedBarBase,a6
	jsr	-36(a6)
	rts

	section	_FreeSpeedButtonNode_stub,code

; VOID FreeSpeedButtonNode(struct Node * node)
	xdef	_FreeSpeedButtonNode
_FreeSpeedButtonNode:
	movea.l	4(sp),a0
	movea.l	_SpeedBarBase,a6
	jsr	-48(a6)
	rts

	section	_SetSpeedButtonNodeAttrsA_stub,code

; VOID SetSpeedButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_SetSpeedButtonNodeAttrsA
_SetSpeedButtonNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SpeedBarBase,a6
	jsr	-54(a6)
	rts

	section	_GetSpeedButtonNodeAttrsA_stub,code

; VOID GetSpeedButtonNodeAttrsA(struct Node * node, struct TagItem * tags)
	xdef	_GetSpeedButtonNodeAttrsA
_GetSpeedButtonNodeAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_SpeedBarBase,a6
	jsr	-66(a6)
	rts

