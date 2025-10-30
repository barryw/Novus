; expansion library stubs for Novus
; Auto-generated from expansion_lib.fd

	xref	_ExpansionBase	; Provided by startup.o + -lamiga

	section	"CODE",code

; AddConfigDev(configDev)
	xdef	_AddConfigDev
_AddConfigDev:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; configDev
	move.l	_ExpansionBase,a6
	jsr	-30(a6)	; AddConfigDev()
	movem.l	(sp)+,a0/a6
	rts

; AddBootNode(bootPri, flags, deviceNode, configDev)
	xdef	_AddBootNode
_AddBootNode:
	movem.l	d0-d1/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; bootPri
	move.l	20(sp),d1	; flags
	move.l	24(sp),a0	; deviceNode
	move.l	28(sp),a1	; configDev
	move.l	_ExpansionBase,a6
	jsr	-36(a6)	; AddBootNode()
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; AllocBoardMem(slotSpec)
	xdef	_AllocBoardMem
_AllocBoardMem:
	movem.l	d0/a6,-(sp)
	move.l	12(sp),d0	; slotSpec
	move.l	_ExpansionBase,a6
	jsr	-42(a6)	; AllocBoardMem()
	movem.l	(sp)+,d0/a6
	rts

; AllocConfigDev()
	xdef	_AllocConfigDev
_AllocConfigDev:
	movem.l	a6,-(sp)
	move.l	_ExpansionBase,a6
	jsr	-48(a6)	; AllocConfigDev()
	movem.l	(sp)+,a6
	rts

; AllocExpansionMem(numSlots, slotAlign)
	xdef	_AllocExpansionMem
_AllocExpansionMem:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; numSlots
	move.l	16(sp),d1	; slotAlign
	move.l	_ExpansionBase,a6
	jsr	-54(a6)	; AllocExpansionMem()
	movem.l	(sp)+,d0-d1/a6
	rts

; ConfigBoard(board, configDev)
	xdef	_ConfigBoard
_ConfigBoard:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; board
	move.l	16(sp),a1	; configDev
	move.l	_ExpansionBase,a6
	jsr	-60(a6)	; ConfigBoard()
	movem.l	(sp)+,a0-a1/a6
	rts

; ConfigChain(baseAddr)
	xdef	_ConfigChain
_ConfigChain:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; baseAddr
	move.l	_ExpansionBase,a6
	jsr	-66(a6)	; ConfigChain()
	movem.l	(sp)+,a0/a6
	rts

; FindConfigDev(oldConfigDev, manufacturer, product)
	xdef	_FindConfigDev
_FindConfigDev:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; oldConfigDev
	move.l	20(sp),d0	; manufacturer
	move.l	24(sp),d1	; product
	move.l	_ExpansionBase,a6
	jsr	-72(a6)	; FindConfigDev()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; FreeBoardMem(startSlot, slotSpec)
	xdef	_FreeBoardMem
_FreeBoardMem:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; startSlot
	move.l	16(sp),d1	; slotSpec
	move.l	_ExpansionBase,a6
	jsr	-78(a6)	; FreeBoardMem()
	movem.l	(sp)+,d0-d1/a6
	rts

; FreeConfigDev(configDev)
	xdef	_FreeConfigDev
_FreeConfigDev:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; configDev
	move.l	_ExpansionBase,a6
	jsr	-84(a6)	; FreeConfigDev()
	movem.l	(sp)+,a0/a6
	rts

; FreeExpansionMem(startSlot, numSlots)
	xdef	_FreeExpansionMem
_FreeExpansionMem:
	movem.l	d0-d1/a6,-(sp)
	move.l	12(sp),d0	; startSlot
	move.l	16(sp),d1	; numSlots
	move.l	_ExpansionBase,a6
	jsr	-90(a6)	; FreeExpansionMem()
	movem.l	(sp)+,d0-d1/a6
	rts

; ReadExpansionByte(board, offset)
	xdef	_ReadExpansionByte
_ReadExpansionByte:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; board
	move.l	20(sp),d0	; offset
	move.l	_ExpansionBase,a6
	jsr	-96(a6)	; ReadExpansionByte()
	movem.l	(sp)+,d0/a0/a6
	rts

; ReadExpansionRom(board, configDev)
	xdef	_ReadExpansionRom
_ReadExpansionRom:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; board
	move.l	16(sp),a1	; configDev
	move.l	_ExpansionBase,a6
	jsr	-102(a6)	; ReadExpansionRom()
	movem.l	(sp)+,a0-a1/a6
	rts

; RemConfigDev(configDev)
	xdef	_RemConfigDev
_RemConfigDev:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; configDev
	move.l	_ExpansionBase,a6
	jsr	-108(a6)	; RemConfigDev()
	movem.l	(sp)+,a0/a6
	rts

; WriteExpansionByte(board, offset, byte)
	xdef	_WriteExpansionByte
_WriteExpansionByte:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),a0	; board
	move.l	20(sp),d0	; offset
	move.l	24(sp),d1	; byte
	move.l	_ExpansionBase,a6
	jsr	-114(a6)	; WriteExpansionByte()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

; ObtainConfigBinding()
	xdef	_ObtainConfigBinding
_ObtainConfigBinding:
	movem.l	a6,-(sp)
	move.l	_ExpansionBase,a6
	jsr	-120(a6)	; ObtainConfigBinding()
	movem.l	(sp)+,a6
	rts

; ReleaseConfigBinding()
	xdef	_ReleaseConfigBinding
_ReleaseConfigBinding:
	movem.l	a6,-(sp)
	move.l	_ExpansionBase,a6
	jsr	-126(a6)	; ReleaseConfigBinding()
	movem.l	(sp)+,a6
	rts

; SetCurrentBinding(currentBinding, bindingSize)
	xdef	_SetCurrentBinding
_SetCurrentBinding:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; currentBinding
	move.l	20(sp),d0	; bindingSize
	move.l	_ExpansionBase,a6
	jsr	-132(a6)	; SetCurrentBinding()
	movem.l	(sp)+,d0/a0/a6
	rts

; GetCurrentBinding(currentBinding, bindingSize)
	xdef	_GetCurrentBinding
_GetCurrentBinding:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; currentBinding
	move.l	20(sp),d0	; bindingSize
	move.l	_ExpansionBase,a6
	jsr	-138(a6)	; GetCurrentBinding()
	movem.l	(sp)+,d0/a0/a6
	rts

; MakeDosNode(parmPacket)
	xdef	_MakeDosNode
_MakeDosNode:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; parmPacket
	move.l	_ExpansionBase,a6
	jsr	-144(a6)	; MakeDosNode()
	movem.l	(sp)+,a0/a6
	rts

; AddDosNode(bootPri, flags, deviceNode)
	xdef	_AddDosNode
_AddDosNode:
	movem.l	d0-d1/a0/a6,-(sp)
	move.l	16(sp),d0	; bootPri
	move.l	20(sp),d1	; flags
	move.l	24(sp),a0	; deviceNode
	move.l	_ExpansionBase,a6
	jsr	-150(a6)	; AddDosNode()
	movem.l	(sp)+,d0-d1/a0/a6
	rts

