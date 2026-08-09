; Generated from SFD file by Novus SFD Parser
; Library: rexxsyslib.library
; Base: _RexxSysBase
; Each function is in its own section for dead code elimination

	xref	_RexxSysBase

	section	_CreateArgstring_stub,code

; UBYTE * CreateArgstring(const STRPTR string, ULONG length)
	xdef	_CreateArgstring
_CreateArgstring:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_RexxSysBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts
	section	_DeleteArgstring_stub,code

; VOID DeleteArgstring(UBYTE * argstring)
	xdef	_DeleteArgstring
_DeleteArgstring:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RexxSysBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_LengthArgstring_stub,code

; ULONG LengthArgstring(const UBYTE * argstring)
	xdef	_LengthArgstring
_LengthArgstring:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RexxSysBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreateRexxMsg_stub,code

; struct RexxMsg * CreateRexxMsg(const struct MsgPort * port, CONST_STRPTR extension, CONST_STRPTR host)
	xdef	_CreateRexxMsg
_CreateRexxMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	move.l	16(sp),d0
	movea.l	_RexxSysBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_DeleteRexxMsg_stub,code

; VOID DeleteRexxMsg(struct RexxMsg * packet)
	xdef	_DeleteRexxMsg
_DeleteRexxMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RexxSysBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_ClearRexxMsg_stub,code

; VOID ClearRexxMsg(struct RexxMsg * msgptr, ULONG count)
	xdef	_ClearRexxMsg
_ClearRexxMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_RexxSysBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a6
	rts

	section	_FillRexxMsg_stub,code

; BOOL FillRexxMsg(struct RexxMsg * msgptr, ULONG count, ULONG mask)
	xdef	_FillRexxMsg
_FillRexxMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_RexxSysBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsRexxMsg_stub,code

; BOOL IsRexxMsg(const struct RexxMsg * msgptr)
	xdef	_IsRexxMsg
_IsRexxMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RexxSysBase,a6
	jsr	-168(a6)
	movem.l	(sp)+,a6
	rts

	section	_LockRexxBase_stub,code

; VOID LockRexxBase(ULONG resource)
	xdef	_LockRexxBase
_LockRexxBase:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_RexxSysBase,a6
	jsr	-450(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnlockRexxBase_stub,code

; VOID UnlockRexxBase(ULONG resource)
	xdef	_UnlockRexxBase
_UnlockRexxBase:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_RexxSysBase,a6
	jsr	-456(a6)
	movem.l	(sp)+,a6
	rts
