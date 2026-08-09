; Generated from SFD file by Novus SFD Parser
; Library: expansion.library
; Base: _ExpansionBase
; Each function is in its own section for dead code elimination

	xref	_ExpansionBase

	section	_AddConfigDev_stub,code

; VOID AddConfigDev(struct ConfigDev * configDev)
	xdef	_AddConfigDev
_AddConfigDev:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ExpansionBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddBootNode_stub,code

; BOOL AddBootNode(LONG bootPri, ULONG flags, struct DeviceNode * deviceNode, struct ConfigDev * configDev)
	xdef	_AddBootNode
_AddBootNode:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	_ExpansionBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocBoardMem_stub,code

; VOID AllocBoardMem(ULONG slotSpec)
	xdef	_AllocBoardMem
_AllocBoardMem:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_ExpansionBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocConfigDev_stub,code

; struct ConfigDev * AllocConfigDev()
	xdef	_AllocConfigDev
_AllocConfigDev:
	movem.l	a6,-(sp)
	movea.l	_ExpansionBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_AllocExpansionMem_stub,code

; APTR AllocExpansionMem(ULONG numSlots, ULONG slotAlign)
	xdef	_AllocExpansionMem
_AllocExpansionMem:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_ExpansionBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_ConfigBoard_stub,code

; VOID ConfigBoard(APTR board, struct ConfigDev * configDev)
	xdef	_ConfigBoard
_ConfigBoard:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_ExpansionBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_ConfigChain_stub,code

; VOID ConfigChain(APTR baseAddr)
	xdef	_ConfigChain
_ConfigChain:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ExpansionBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_FindConfigDev_stub,code

; struct ConfigDev * FindConfigDev(const struct ConfigDev * oldConfigDev, LONG manufacturer, LONG product)
	xdef	_FindConfigDev
_FindConfigDev:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_ExpansionBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeBoardMem_stub,code

; VOID FreeBoardMem(ULONG startSlot, ULONG slotSpec)
	xdef	_FreeBoardMem
_FreeBoardMem:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_ExpansionBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeConfigDev_stub,code

; VOID FreeConfigDev(struct ConfigDev * configDev)
	xdef	_FreeConfigDev
_FreeConfigDev:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ExpansionBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_FreeExpansionMem_stub,code

; VOID FreeExpansionMem(ULONG startSlot, ULONG numSlots)
	xdef	_FreeExpansionMem
_FreeExpansionMem:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	_ExpansionBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadExpansionByte_stub,code

; UBYTE ReadExpansionByte(const APTR board, ULONG offset)
	xdef	_ReadExpansionByte
_ReadExpansionByte:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_ExpansionBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReadExpansionRom_stub,code

; VOID ReadExpansionRom(const APTR board, struct ConfigDev * configDev)
	xdef	_ReadExpansionRom
_ReadExpansionRom:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_ExpansionBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemConfigDev_stub,code

; VOID RemConfigDev(struct ConfigDev * configDev)
	xdef	_RemConfigDev
_RemConfigDev:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ExpansionBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_WriteExpansionByte_stub,code

; VOID WriteExpansionByte(APTR board, ULONG offset, UBYTE byte)
	xdef	_WriteExpansionByte
_WriteExpansionByte:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	_ExpansionBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_ObtainConfigBinding_stub,code

; VOID ObtainConfigBinding()
	xdef	_ObtainConfigBinding
_ObtainConfigBinding:
	movem.l	a6,-(sp)
	movea.l	_ExpansionBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReleaseConfigBinding_stub,code

; VOID ReleaseConfigBinding()
	xdef	_ReleaseConfigBinding
_ReleaseConfigBinding:
	movem.l	a6,-(sp)
	movea.l	_ExpansionBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetCurrentBinding_stub,code

; VOID SetCurrentBinding(struct CurrentBinding * currentBinding, ULONG bindingSize)
	xdef	_SetCurrentBinding
_SetCurrentBinding:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_ExpansionBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetCurrentBinding_stub,code

; ULONG GetCurrentBinding(const struct CurrentBinding * currentBinding, ULONG bindingSize)
	xdef	_GetCurrentBinding
_GetCurrentBinding:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_ExpansionBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_MakeDosNode_stub,code

; struct DeviceNode * MakeDosNode(const APTR parmPacket)
	xdef	_MakeDosNode
_MakeDosNode:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_ExpansionBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddDosNode_stub,code

; BOOL AddDosNode(LONG bootPri, ULONG flags, struct DeviceNode * deviceNode)
	xdef	_AddDosNode
_AddDosNode:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	move.l	12(sp),d1
	movea.l	16(sp),a0
	movea.l	_ExpansionBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

