; Generated from SFD file by Novus SFD Parser
; Library: commodities.library
; Base: _CxBase
; Each function is in its own section for dead code elimination

	xref	_CxBase

	section	_CreateCxObj_stub,code

; CxObj * CreateCxObj(ULONG type, LONG arg1, LONG arg2)
	xdef	_CreateCxObj
_CreateCxObj:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_CxBase,a6
	jsr	-30(a6)
	movem.l	(sp)+,a6
	rts

	section	_CxBroker_stub,code

; CxObj * CxBroker(const struct NewBroker * nb, LONG * error)
	xdef	_CxBroker
_CxBroker:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_CxBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_ActivateCxObj_stub,code

; LONG ActivateCxObj(CxObj * co, LONG doIt)
	xdef	_ActivateCxObj
_ActivateCxObj:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_CxBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_DeleteCxObj_stub,code

; VOID DeleteCxObj(CxObj * co)
	xdef	_DeleteCxObj
_DeleteCxObj:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_DeleteCxObjAll_stub,code

; VOID DeleteCxObjAll(CxObj * co)
	xdef	_DeleteCxObjAll
_DeleteCxObjAll:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_CxObjType_stub,code

; ULONG CxObjType(const CxObj * co)
	xdef	_CxObjType
_CxObjType:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a6
	rts

	section	_CxObjError_stub,code

; LONG CxObjError(const CxObj * co)
	xdef	_CxObjError
_CxObjError:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_ClearCxObjError_stub,code

; VOID ClearCxObjError(CxObj * co)
	xdef	_ClearCxObjError
_ClearCxObjError:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetCxObjPri_stub,code

; LONG SetCxObjPri(CxObj * co, LONG pri)
	xdef	_SetCxObjPri
_SetCxObjPri:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_CxBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_AttachCxObj_stub,code

; VOID AttachCxObj(CxObj * headObj, CxObj * co)
	xdef	_AttachCxObj
_AttachCxObj:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_EnqueueCxObj_stub,code

; VOID EnqueueCxObj(CxObj * headObj, CxObj * co)
	xdef	_EnqueueCxObj
_EnqueueCxObj:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_InsertCxObj_stub,code

; VOID InsertCxObj(CxObj * headObj, CxObj * co, CxObj * pred)
	xdef	_InsertCxObj
_InsertCxObj:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_CxBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RemoveCxObj_stub,code

; VOID RemoveCxObj(CxObj * co)
	xdef	_RemoveCxObj
_RemoveCxObj:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetTranslate_stub,code

; VOID SetTranslate(CxObj * translator, struct InputEvent * events)
	xdef	_SetTranslate
_SetTranslate:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetFilter_stub,code

; VOID SetFilter(CxObj * filter, CONST_STRPTR text)
	xdef	_SetFilter
_SetFilter:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetFilterIX_stub,code

; VOID SetFilterIX(CxObj * filter, const IX * ix)
	xdef	_SetFilterIX
_SetFilterIX:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_ParseIX_stub,code

; LONG ParseIX(CONST_STRPTR description, IX * ix)
	xdef	_ParseIX
_ParseIX:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_CxMsgType_stub,code

; ULONG CxMsgType(const CxMsg * cxm)
	xdef	_CxMsgType
_CxMsgType:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_CxMsgData_stub,code

; APTR CxMsgData(const CxMsg * cxm)
	xdef	_CxMsgData
_CxMsgData:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_CxMsgID_stub,code

; LONG CxMsgID(const CxMsg * cxm)
	xdef	_CxMsgID
_CxMsgID:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a6
	rts

	section	_DivertCxMsg_stub,code

; VOID DivertCxMsg(CxMsg * cxm, CxObj * headObj, CxObj * returnObj)
	xdef	_DivertCxMsg
_DivertCxMsg:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_CxBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RouteCxMsg_stub,code

; VOID RouteCxMsg(CxMsg * cxm, CxObj * co)
	xdef	_RouteCxMsg
_RouteCxMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeCxMsg_stub,code

; VOID DisposeCxMsg(CxMsg * cxm)
	xdef	_DisposeCxMsg
_DisposeCxMsg:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-168(a6)
	movem.l	(sp)+,a6
	rts

	section	_InvertKeyMap_stub,code

; BOOL InvertKeyMap(ULONG ansiCode, struct InputEvent * event, const struct KeyMap * km)
	xdef	_InvertKeyMap
_InvertKeyMap:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_CxBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddIEvents_stub,code

; VOID AddIEvents(struct InputEvent * events)
	xdef	_AddIEvents
_AddIEvents:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_CxBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,a6
	rts

	section	_MatchIX_stub,code

; BOOL MatchIX(const struct InputEvent * event, const IX * ix)
	xdef	_MatchIX
_MatchIX:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_CxBase,a6
	jsr	-204(a6)
	movem.l	(sp)+,a6
	rts

