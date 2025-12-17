; Generated from SFD file by Novus SFD Parser
; Library: rexxsyslib.library
; Base: _RexxSysBase
; Each function is in its own section for dead code elimination
; NOTE: Uses lazy initialization via ___rexxsyslib_ensure

	xref	_RexxSysBase
	xref	___rexxsyslib_ensure	; Lazy init - opens library if needed, returns base in A6

	section	_CreateArgstring_stub,code

; UBYTE * CreateArgstring(const STRPTR string, ULONG length)
	xdef	_CreateArgstring
_CreateArgstring:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___rexxsyslib_ensure
	jsr	-126(a6)
	rts

	section	_DeleteArgstring_stub,code

; VOID DeleteArgstring(UBYTE * argstring)
	xdef	_DeleteArgstring
_DeleteArgstring:
	movea.l	4(sp),a0
	jsr	___rexxsyslib_ensure
	jsr	-132(a6)
	rts

	section	_LengthArgstring_stub,code

; ULONG LengthArgstring(const UBYTE * argstring)
	xdef	_LengthArgstring
_LengthArgstring:
	movea.l	4(sp),a0
	jsr	___rexxsyslib_ensure
	jsr	-138(a6)
	rts

	section	_CreateRexxMsg_stub,code

; struct RexxMsg * CreateRexxMsg(const struct MsgPort * port, CONST_STRPTR extension, CONST_STRPTR host)
	xdef	_CreateRexxMsg
_CreateRexxMsg:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	jsr	___rexxsyslib_ensure
	jsr	-144(a6)
	rts

	section	_DeleteRexxMsg_stub,code

; VOID DeleteRexxMsg(struct RexxMsg * packet)
	xdef	_DeleteRexxMsg
_DeleteRexxMsg:
	movea.l	4(sp),a0
	jsr	___rexxsyslib_ensure
	jsr	-150(a6)
	rts

	section	_ClearRexxMsg_stub,code

; VOID ClearRexxMsg(struct RexxMsg * msgptr, ULONG count)
	xdef	_ClearRexxMsg
_ClearRexxMsg:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	jsr	___rexxsyslib_ensure
	jsr	-156(a6)
	rts

	section	_FillRexxMsg_stub,code

; BOOL FillRexxMsg(struct RexxMsg * msgptr, ULONG count, ULONG mask)
	xdef	_FillRexxMsg
_FillRexxMsg:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	move.l	12(sp),d1
	jsr	___rexxsyslib_ensure
	jsr	-162(a6)
	rts

	section	_IsRexxMsg_stub,code

; BOOL IsRexxMsg(const struct RexxMsg * msgptr)
	xdef	_IsRexxMsg
_IsRexxMsg:
	movea.l	4(sp),a0
	jsr	___rexxsyslib_ensure
	jsr	-168(a6)
	rts

	section	_LockRexxBase_stub,code

; VOID LockRexxBase(ULONG resource)
	xdef	_LockRexxBase
_LockRexxBase:
	move.l	4(sp),d0
	jsr	___rexxsyslib_ensure
	jsr	-450(a6)
	rts

	section	_UnlockRexxBase_stub,code

; VOID UnlockRexxBase(ULONG resource)
	xdef	_UnlockRexxBase
_UnlockRexxBase:
	move.l	4(sp),d0
	jsr	___rexxsyslib_ensure
	jsr	-456(a6)
	rts
