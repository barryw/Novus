; commodities library stubs for Novus
; Auto-generated from commodities_lib.fd

	xref	_CxBase	; Provided by startup.o + -lamiga

	section	text,code

; CreateCxObj(type, arg1, arg2)
	xdef	_CreateCxObj
_CreateCxObj:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; type
	move.l	20(sp),a0	; arg1
	move.l	24(sp),a1	; arg2
	move.l	_CxBase,a6
	jsr	-30(a6)	; CreateCxObj()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; CxBroker(nb, error)
	xdef	_CxBroker
_CxBroker:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; nb
	move.l	20(sp),d0	; error
	move.l	_CxBase,a6
	jsr	-36(a6)	; CxBroker()
	movem.l	(sp)+,d0/a0/a6
	rts

; ActivateCxObj(co, doIt)
	xdef	_ActivateCxObj
_ActivateCxObj:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; co
	move.l	20(sp),d0	; doIt
	move.l	_CxBase,a6
	jsr	-42(a6)	; ActivateCxObj()
	movem.l	(sp)+,d0/a0/a6
	rts

; DeleteCxObj(co)
	xdef	_DeleteCxObj
_DeleteCxObj:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; co
	move.l	_CxBase,a6
	jsr	-48(a6)	; DeleteCxObj()
	movem.l	(sp)+,a0/a6
	rts

; DeleteCxObjAll(co)
	xdef	_DeleteCxObjAll
_DeleteCxObjAll:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; co
	move.l	_CxBase,a6
	jsr	-54(a6)	; DeleteCxObjAll()
	movem.l	(sp)+,a0/a6
	rts

; CxObjType(co)
	xdef	_CxObjType
_CxObjType:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; co
	move.l	_CxBase,a6
	jsr	-60(a6)	; CxObjType()
	movem.l	(sp)+,a0/a6
	rts

; CxObjError(co)
	xdef	_CxObjError
_CxObjError:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; co
	move.l	_CxBase,a6
	jsr	-66(a6)	; CxObjError()
	movem.l	(sp)+,a0/a6
	rts

; ClearCxObjError(co)
	xdef	_ClearCxObjError
_ClearCxObjError:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; co
	move.l	_CxBase,a6
	jsr	-72(a6)	; ClearCxObjError()
	movem.l	(sp)+,a0/a6
	rts

; SetCxObjPri(co, pri)
	xdef	_SetCxObjPri
_SetCxObjPri:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; co
	move.l	20(sp),d0	; pri
	move.l	_CxBase,a6
	jsr	-78(a6)	; SetCxObjPri()
	movem.l	(sp)+,d0/a0/a6
	rts

; AttachCxObj(headObj, co)
	xdef	_AttachCxObj
_AttachCxObj:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; headObj
	move.l	16(sp),a1	; co
	move.l	_CxBase,a6
	jsr	-84(a6)	; AttachCxObj()
	movem.l	(sp)+,a0-a1/a6
	rts

; EnqueueCxObj(headObj, co)
	xdef	_EnqueueCxObj
_EnqueueCxObj:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; headObj
	move.l	16(sp),a1	; co
	move.l	_CxBase,a6
	jsr	-90(a6)	; EnqueueCxObj()
	movem.l	(sp)+,a0-a1/a6
	rts

; InsertCxObj(headObj, co, pred)
	xdef	_InsertCxObj
_InsertCxObj:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; headObj
	move.l	16(sp),a1	; co
	move.l	20(sp),a2	; pred
	move.l	_CxBase,a6
	jsr	-96(a6)	; InsertCxObj()
	movem.l	(sp)+,a0-a2/a6
	rts

; RemoveCxObj(co)
	xdef	_RemoveCxObj
_RemoveCxObj:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; co
	move.l	_CxBase,a6
	jsr	-102(a6)	; RemoveCxObj()
	movem.l	(sp)+,a0/a6
	rts

; SetTranslate(translator, events)
	xdef	_SetTranslate
_SetTranslate:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; translator
	move.l	16(sp),a1	; events
	move.l	_CxBase,a6
	jsr	-114(a6)	; SetTranslate()
	movem.l	(sp)+,a0-a1/a6
	rts

; SetFilter(filter, text)
	xdef	_SetFilter
_SetFilter:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; filter
	move.l	16(sp),a1	; text
	move.l	_CxBase,a6
	jsr	-120(a6)	; SetFilter()
	movem.l	(sp)+,a0-a1/a6
	rts

; SetFilterIX(filter, ix)
	xdef	_SetFilterIX
_SetFilterIX:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; filter
	move.l	16(sp),a1	; ix
	move.l	_CxBase,a6
	jsr	-126(a6)	; SetFilterIX()
	movem.l	(sp)+,a0-a1/a6
	rts

; ParseIX(description, ix)
	xdef	_ParseIX
_ParseIX:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; description
	move.l	16(sp),a1	; ix
	move.l	_CxBase,a6
	jsr	-132(a6)	; ParseIX()
	movem.l	(sp)+,a0-a1/a6
	rts

; CxMsgType(cxm)
	xdef	_CxMsgType
_CxMsgType:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; cxm
	move.l	_CxBase,a6
	jsr	-138(a6)	; CxMsgType()
	movem.l	(sp)+,a0/a6
	rts

; CxMsgData(cxm)
	xdef	_CxMsgData
_CxMsgData:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; cxm
	move.l	_CxBase,a6
	jsr	-144(a6)	; CxMsgData()
	movem.l	(sp)+,a0/a6
	rts

; CxMsgID(cxm)
	xdef	_CxMsgID
_CxMsgID:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; cxm
	move.l	_CxBase,a6
	jsr	-150(a6)	; CxMsgID()
	movem.l	(sp)+,a0/a6
	rts

; DivertCxMsg(cxm, headObj, returnObj)
	xdef	_DivertCxMsg
_DivertCxMsg:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; cxm
	move.l	16(sp),a1	; headObj
	move.l	20(sp),a2	; returnObj
	move.l	_CxBase,a6
	jsr	-156(a6)	; DivertCxMsg()
	movem.l	(sp)+,a0-a2/a6
	rts

; RouteCxMsg(cxm, co)
	xdef	_RouteCxMsg
_RouteCxMsg:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; cxm
	move.l	16(sp),a1	; co
	move.l	_CxBase,a6
	jsr	-162(a6)	; RouteCxMsg()
	movem.l	(sp)+,a0-a1/a6
	rts

; DisposeCxMsg(cxm)
	xdef	_DisposeCxMsg
_DisposeCxMsg:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; cxm
	move.l	_CxBase,a6
	jsr	-168(a6)	; DisposeCxMsg()
	movem.l	(sp)+,a0/a6
	rts

; InvertKeyMap(ansiCode, event, km)
	xdef	_InvertKeyMap
_InvertKeyMap:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),d0	; ansiCode
	move.l	20(sp),a0	; event
	move.l	24(sp),a1	; km
	move.l	_CxBase,a6
	jsr	-174(a6)	; InvertKeyMap()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; AddIEvents(events)
	xdef	_AddIEvents
_AddIEvents:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; events
	move.l	_CxBase,a6
	jsr	-180(a6)	; AddIEvents()
	movem.l	(sp)+,a0/a6
	rts

; MatchIX(event, ix)
	xdef	_MatchIX
_MatchIX:
	movem.l	a0-a1/a6,-(sp)
	move.l	12(sp),a0	; event
	move.l	16(sp),a1	; ix
	move.l	_CxBase,a6
	jsr	-204(a6)	; MatchIX()
	movem.l	(sp)+,a0-a1/a6
	rts

