; Generated from SFD file by Novus SFD Parser
; Library: amigaguide.library
; Base: _AmigaGuideBase
; Each function is in its own section for dead code elimination

	xref	_AmigaGuideBase

	section	_LockAmigaGuideBase_stub,code

; LONG LockAmigaGuideBase(APTR handle)
	xdef	_LockAmigaGuideBase
_LockAmigaGuideBase:
	movea.l	4(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-36(a6)
	rts

	section	_UnlockAmigaGuideBase_stub,code

; VOID UnlockAmigaGuideBase(LONG key)
	xdef	_UnlockAmigaGuideBase
_UnlockAmigaGuideBase:
	move.l	4(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-42(a6)
	rts

	section	_OpenAmigaGuideA_stub,code

; APTR OpenAmigaGuideA(struct NewAmigaGuide * nag, struct TagItem * tags)
	xdef	_OpenAmigaGuideA
_OpenAmigaGuideA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-54(a6)
	rts

	section	_OpenAmigaGuideAsyncA_stub,code

; APTR OpenAmigaGuideAsyncA(struct NewAmigaGuide * nag, struct TagItem * attrs)
	xdef	_OpenAmigaGuideAsyncA
_OpenAmigaGuideAsyncA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-66(a6)
	rts

	section	_CloseAmigaGuide_stub,code

; VOID CloseAmigaGuide(APTR cl)
	xdef	_CloseAmigaGuide
_CloseAmigaGuide:
	movea.l	4(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-78(a6)
	rts

	section	_AmigaGuideSignal_stub,code

; ULONG AmigaGuideSignal(APTR cl)
	xdef	_AmigaGuideSignal
_AmigaGuideSignal:
	movea.l	4(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-84(a6)
	rts

	section	_GetAmigaGuideMsg_stub,code

; struct AmigaGuideMsg * GetAmigaGuideMsg(APTR cl)
	xdef	_GetAmigaGuideMsg
_GetAmigaGuideMsg:
	movea.l	4(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-90(a6)
	rts

	section	_ReplyAmigaGuideMsg_stub,code

; VOID ReplyAmigaGuideMsg(struct AmigaGuideMsg * amsg)
	xdef	_ReplyAmigaGuideMsg
_ReplyAmigaGuideMsg:
	movea.l	4(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-96(a6)
	rts

	section	_SetAmigaGuideContextA_stub,code

; LONG SetAmigaGuideContextA(APTR cl, ULONG id, struct TagItem * attrs)
	xdef	_SetAmigaGuideContextA
_SetAmigaGuideContextA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_AmigaGuideBase,a6
	jsr	-102(a6)
	rts

	section	_SendAmigaGuideContextA_stub,code

; LONG SendAmigaGuideContextA(APTR cl, struct TagItem * attrs)
	xdef	_SendAmigaGuideContextA
_SendAmigaGuideContextA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-114(a6)
	rts

	section	_SendAmigaGuideCmdA_stub,code

; LONG SendAmigaGuideCmdA(APTR cl, STRPTR cmd, struct TagItem * attrs)
	xdef	_SendAmigaGuideCmdA
_SendAmigaGuideCmdA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_AmigaGuideBase,a6
	jsr	-126(a6)
	rts

	section	_SetAmigaGuideAttrsA_stub,code

; LONG SetAmigaGuideAttrsA(APTR cl, struct TagItem * attrs)
	xdef	_SetAmigaGuideAttrsA
_SetAmigaGuideAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-138(a6)
	rts

	section	_GetAmigaGuideAttr_stub,code

; LONG GetAmigaGuideAttr(Tag tag, APTR cl, ULONG * storage)
	xdef	_GetAmigaGuideAttr
_GetAmigaGuideAttr:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-150(a6)
	rts

	section	_LoadXRef_stub,code

; LONG LoadXRef(BPTR lock, STRPTR name)
	xdef	_LoadXRef
_LoadXRef:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-162(a6)
	rts

	section	_ExpungeXRef_stub,code

; VOID ExpungeXRef()
	xdef	_ExpungeXRef
_ExpungeXRef:
	movea.l	_AmigaGuideBase,a6
	jsr	-168(a6)
	rts

	section	_AddAmigaGuideHostA_stub,code

; APTR AddAmigaGuideHostA(struct Hook * h, STRPTR name, struct TagItem * attrs)
	xdef	_AddAmigaGuideHostA
_AddAmigaGuideHostA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-174(a6)
	rts

	section	_RemoveAmigaGuideHostA_stub,code

; LONG RemoveAmigaGuideHostA(APTR hh, struct TagItem * attrs)
	xdef	_RemoveAmigaGuideHostA
_RemoveAmigaGuideHostA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-186(a6)
	rts

	section	_GetAmigaGuideString_stub,code

; STRPTR GetAmigaGuideString(LONG id)
	xdef	_GetAmigaGuideString
_GetAmigaGuideString:
	move.l	4(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-258(a6)
	rts

