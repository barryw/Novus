; Generated from SFD file by Novus SFD Parser
; Library: realtime.library
; Base: _RealTimeBase
; Each function is in its own section for dead code elimination

	xref	_RealTimeBase

	section	_LockRealTime_stub,code

; APTR LockRealTime(ULONG lockType)
	xdef	_LockRealTime
_LockRealTime:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_RealTimeBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_UnlockRealTime_stub,code

; VOID UnlockRealTime(APTR lock)
	xdef	_UnlockRealTime
_UnlockRealTime:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RealTimeBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreatePlayerA_stub,code

; struct Player * CreatePlayerA(const struct TagItem * tagList)
	xdef	_CreatePlayerA
_CreatePlayerA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RealTimeBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_CreatePlayer_stub,code

; struct Player * CreatePlayer(Tag tagList, ... )
	xdef	_CreatePlayer
_CreatePlayer:
	movem.l	a6,-(sp)
	lea	8(sp),a0
	movea.l	_RealTimeBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_DeletePlayer_stub,code

; VOID DeletePlayer(struct Player * player)
	xdef	_DeletePlayer
_DeletePlayer:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RealTimeBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetPlayerAttrsA_stub,code

; BOOL SetPlayerAttrsA(struct Player * player, const struct TagItem * tagList)
	xdef	_SetPlayerAttrsA
_SetPlayerAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_RealTimeBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetPlayerAttrs_stub,code

; BOOL SetPlayerAttrs(struct Player * player, Tag tagList, ... )
	xdef	_SetPlayerAttrs
_SetPlayerAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_RealTimeBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetConductorState_stub,code

; LONG SetConductorState(struct Player * player, ULONG state, LONG time)
	xdef	_SetConductorState
_SetConductorState:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_RealTimeBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_ExternalSync_stub,code

; BOOL ExternalSync(struct Player * player, LONG minTime, LONG maxTime)
	xdef	_ExternalSync
_ExternalSync:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_RealTimeBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_NextConductor_stub,code

; struct Conductor * NextConductor(const struct Conductor * previousConductor)
	xdef	_NextConductor
_NextConductor:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RealTimeBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindConductor_stub,code

; struct Conductor * FindConductor(CONST_STRPTR name)
	xdef	_FindConductor
_FindConductor:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_RealTimeBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetPlayerAttrsA_stub,code

; ULONG GetPlayerAttrsA(const struct Player * player, const struct TagItem * tagList)
	xdef	_GetPlayerAttrsA
_GetPlayerAttrsA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_RealTimeBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetPlayerAttrs_stub,code

; ULONG GetPlayerAttrs(const struct Player * player, Tag tagList, ... )
	xdef	_GetPlayerAttrs
_GetPlayerAttrs:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_RealTimeBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

