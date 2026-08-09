; Generated from SFD file by Novus SFD Parser
; Library: card.resource
; Base: _CardResource
; Each function is in its own section for dead code elimination

	xref	_CardResource

	section	_OwnCard_stub,code

; struct CardHandle * OwnCard(struct CardHandle * handle)
	xdef	_OwnCard
_OwnCard:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_CardResource,a6
	jsr	-6(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReleaseCard_stub,code

; VOID ReleaseCard(struct CardHandle * handle, ULONG flags)
	xdef	_ReleaseCard
_ReleaseCard:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_CardResource,a6
	jsr	-12(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetCardMap_stub,code

; struct CardMemoryMap * GetCardMap()
	xdef	_GetCardMap
_GetCardMap:
	movem.l	a6,-(sp)
	movea.l	_CardResource,a6
	jsr	-18(a6)
	movem.l	(sp)+,a6
	rts

	section	_BeginCardAccess_stub,code

; BOOL BeginCardAccess(struct CardHandle * handle)
	xdef	_BeginCardAccess
_BeginCardAccess:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_CardResource,a6
	jsr	-24(a6)
	movem.l	(sp)+,a6
	rts

	section	_EndCardAccess_stub,code

; BOOL EndCardAccess(struct CardHandle * handle)
	xdef	_EndCardAccess
_EndCardAccess:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_CardResource,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadCardStatus_stub,code

; UBYTE ReadCardStatus()
	xdef	_ReadCardStatus
_ReadCardStatus:
	movem.l	a6,-(sp)
	movea.l	_CardResource,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_CardResetRemove_stub,code

; BOOL CardResetRemove(struct CardHandle * handle, ULONG flag)
	xdef	_CardResetRemove
_CardResetRemove:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_CardResource,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_CardMiscControl_stub,code

; UBYTE CardMiscControl(struct CardHandle * handle, UBYTE control_bits)
	xdef	_CardMiscControl
_CardMiscControl:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d1
	movea.l	_CardResource,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_CardAccessSpeed_stub,code

; ULONG CardAccessSpeed(struct CardHandle * handle, ULONG nanoseconds)
	xdef	_CardAccessSpeed
_CardAccessSpeed:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_CardResource,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_CardProgramVoltage_stub,code

; LONG CardProgramVoltage(struct CardHandle * handle, ULONG voltage)
	xdef	_CardProgramVoltage
_CardProgramVoltage:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_CardResource,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_CardResetCard_stub,code

; BOOL CardResetCard(struct CardHandle * handle)
	xdef	_CardResetCard
_CardResetCard:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	_CardResource,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_CopyTuple_stub,code

; BOOL CopyTuple(const struct CardHandle * handle, UBYTE * buffer, ULONG tuplecode, ULONG size)
	xdef	_CopyTuple
_CopyTuple:
	movem.l	a6,-(sp)
	movea.l	8(sp),a1
	movea.l	12(sp),a0
	move.l	16(sp),d1
	move.l	20(sp),d0
	movea.l	_CardResource,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_DeviceTuple_stub,code

; ULONG DeviceTuple(const UBYTE * tuple_data, struct DeviceTData * storage)
	xdef	_DeviceTuple
_DeviceTuple:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CardResource,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_IfAmigaXIP_stub,code

; struct Resident * IfAmigaXIP(const struct CardHandle * handle)
	xdef	_IfAmigaXIP
_IfAmigaXIP:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a2
	movea.l	_CardResource,a6
	jsr	-84(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_CardForceChange_stub,code

; BOOL CardForceChange()
	xdef	_CardForceChange
_CardForceChange:
	movem.l	a6,-(sp)
	movea.l	_CardResource,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_CardChangeCount_stub,code

; ULONG CardChangeCount()
	xdef	_CardChangeCount
_CardChangeCount:
	movem.l	a6,-(sp)
	movea.l	_CardResource,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_CardInterface_stub,code

; ULONG CardInterface()
	xdef	_CardInterface
_CardInterface:
	movem.l	a6,-(sp)
	movea.l	_CardResource,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

