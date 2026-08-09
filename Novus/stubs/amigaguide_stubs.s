; Generated from SFD file by Novus SFD Parser
; Library: amigaguide.library
; Base: _AmigaGuideBase
; Each function is in its own section for dead code elimination

	xref	_AmigaGuideBase

	section	_LockAmigaGuideBase_stub,code

; LONG LockAmigaGuideBase(APTR handle)
	xdef	_LockAmigaGuideBase
_LockAmigaGuideBase:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnlockAmigaGuideBase_stub,code

; VOID UnlockAmigaGuideBase(LONG key)
	xdef	_UnlockAmigaGuideBase
_UnlockAmigaGuideBase:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAmigaGuideA_stub,code

; APTR OpenAmigaGuideA(struct NewAmigaGuide * nag, struct TagItem * tags)
	xdef	_OpenAmigaGuideA
_OpenAmigaGuideA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAmigaGuide_stub,code

; APTR OpenAmigaGuide(struct NewAmigaGuide * nag, Tag tags, ... )
	xdef	_OpenAmigaGuide
_OpenAmigaGuide:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAmigaGuideAsyncA_stub,code

; APTR OpenAmigaGuideAsyncA(struct NewAmigaGuide * nag, struct TagItem * attrs)
	xdef	_OpenAmigaGuideAsyncA
_OpenAmigaGuideAsyncA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenAmigaGuideAsync_stub,code

; APTR OpenAmigaGuideAsync(struct NewAmigaGuide * nag, Tag attrs, ... )
	xdef	_OpenAmigaGuideAsync
_OpenAmigaGuideAsync:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a6
	move.l	a6,d0
	movea.l	_AmigaGuideBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseAmigaGuide_stub,code

; VOID CloseAmigaGuide(APTR cl)
	xdef	_CloseAmigaGuide
_CloseAmigaGuide:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_AmigaGuideSignal_stub,code

; ULONG AmigaGuideSignal(APTR cl)
	xdef	_AmigaGuideSignal
_AmigaGuideSignal:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetAmigaGuideMsg_stub,code

; struct AmigaGuideMsg * GetAmigaGuideMsg(APTR cl)
	xdef	_GetAmigaGuideMsg
_GetAmigaGuideMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReplyAmigaGuideMsg_stub,code

; VOID ReplyAmigaGuideMsg(struct AmigaGuideMsg * amsg)
	xdef	_ReplyAmigaGuideMsg
_ReplyAmigaGuideMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_AmigaGuideBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetAmigaGuideContextA_stub,code

; LONG SetAmigaGuideContextA(APTR cl, ULONG id, struct TagItem * attrs)
	xdef	_SetAmigaGuideContextA
_SetAmigaGuideContextA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_AmigaGuideBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetAmigaGuideContext_stub,code

; LONG SetAmigaGuideContext(APTR cl, ULONG id, Tag attrs, ... )
	xdef	_SetAmigaGuideContext
_SetAmigaGuideContext:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	lea	16(sp),a6
	move.l	a6,d1
	movea.l	_AmigaGuideBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_SendAmigaGuideContextA_stub,code

; LONG SendAmigaGuideContextA(APTR cl, struct TagItem * attrs)
	xdef	_SendAmigaGuideContextA
_SendAmigaGuideContextA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_SendAmigaGuideContext_stub,code

; LONG SendAmigaGuideContext(APTR cl, Tag attrs, ... )
	xdef	_SendAmigaGuideContext
_SendAmigaGuideContext:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a6
	move.l	a6,d0
	movea.l	_AmigaGuideBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_SendAmigaGuideCmdA_stub,code

; LONG SendAmigaGuideCmdA(APTR cl, STRPTR cmd, struct TagItem * attrs)
	xdef	_SendAmigaGuideCmdA
_SendAmigaGuideCmdA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_AmigaGuideBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_SendAmigaGuideCmd_stub,code

; LONG SendAmigaGuideCmd(APTR cl, STRPTR cmd, Tag attrs, ... )
	xdef	_SendAmigaGuideCmd
_SendAmigaGuideCmd:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	lea	16(sp),a6
	move.l	a6,d1
	movea.l	_AmigaGuideBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetAmigaGuideAttrsA_stub,code

; LONG SetAmigaGuideAttrsA(APTR cl, struct TagItem * attrs)
	xdef	_SetAmigaGuideAttrsA
_SetAmigaGuideAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetAmigaGuideAttrs_stub,code

; LONG SetAmigaGuideAttrs(APTR cl, Tag attrs, ... )
	xdef	_SetAmigaGuideAttrs
_SetAmigaGuideAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetAmigaGuideAttr_stub,code

; LONG GetAmigaGuideAttr(Tag tag, APTR cl, ULONG * storage)
	xdef	_GetAmigaGuideAttr
_GetAmigaGuideAttr:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_LoadXRef_stub,code

; LONG LoadXRef(BPTR lock, STRPTR name)
	xdef	_LoadXRef
_LoadXRef:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_ExpungeXRef_stub,code

; VOID ExpungeXRef()
	xdef	_ExpungeXRef
_ExpungeXRef:
	movem.l	a6,-(sp)
	movea.l	_AmigaGuideBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddAmigaGuideHostA_stub,code

; APTR AddAmigaGuideHostA(struct Hook * h, STRPTR name, struct TagItem * attrs)
	xdef	_AddAmigaGuideHostA
_AddAmigaGuideHostA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	16(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddAmigaGuideHost_stub,code

; APTR AddAmigaGuideHost(struct Hook * h, STRPTR name, Tag attrs, ... )
	xdef	_AddAmigaGuideHost
_AddAmigaGuideHost:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	lea	16(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemoveAmigaGuideHostA_stub,code

; LONG RemoveAmigaGuideHostA(APTR hh, struct TagItem * attrs)
	xdef	_RemoveAmigaGuideHostA
_RemoveAmigaGuideHostA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemoveAmigaGuideHost_stub,code

; LONG RemoveAmigaGuideHost(APTR hh, Tag attrs, ... )
	xdef	_RemoveAmigaGuideHost
_RemoveAmigaGuideHost:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_AmigaGuideBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetAmigaGuideString_stub,code

; STRPTR GetAmigaGuideString(LONG id)
	xdef	_GetAmigaGuideString
_GetAmigaGuideString:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_AmigaGuideBase,a6
	jsr	-210(a6)
	movem.l	(sp)+,a6
	rts

