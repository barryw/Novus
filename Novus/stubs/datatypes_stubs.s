; Generated from SFD file by Novus SFD Parser
; Library: datatypes.library
; Base: _DataTypesBase
; Each function is in its own section for dead code elimination

	xref	_DataTypesBase

	section	_ObtainDataTypeA_stub,code

; struct DataType * ObtainDataTypeA(ULONG type, APTR handle, struct TagItem * attrs)
	xdef	_ObtainDataTypeA
_ObtainDataTypeA:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_ObtainDataType_stub,code

; struct DataType * ObtainDataType(ULONG type, APTR handle, Tag attrs, ... )
	xdef	_ObtainDataType
_ObtainDataType:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	lea	16(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_ReleaseDataType_stub,code

; VOID ReleaseDataType(struct DataType * dt)
	xdef	_ReleaseDataType
_ReleaseDataType:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_NewDTObjectA_stub,code

; Object * NewDTObjectA(APTR name, struct TagItem * attrs)
	xdef	_NewDTObjectA
_NewDTObjectA:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	12(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_NewDTObject_stub,code

; Object * NewDTObject(APTR name, Tag attrs, ... )
	xdef	_NewDTObject
_NewDTObject:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	lea	12(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_DisposeDTObject_stub,code

; VOID DisposeDTObject(Object * o)
	xdef	_DisposeDTObject
_DisposeDTObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_SetDTAttrsA_stub,code

; ULONG SetDTAttrsA(Object * o, struct Window * win, struct Requester * req, struct TagItem * attrs)
	xdef	_SetDTAttrsA
_SetDTAttrsA:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_SetDTAttrs_stub,code

; ULONG SetDTAttrs(Object * o, struct Window * win, struct Requester * req, Tag attrs, ... )
	xdef	_SetDTAttrs
_SetDTAttrs:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_GetDTAttrsA_stub,code

; ULONG GetDTAttrsA(Object * o, struct TagItem * attrs)
	xdef	_GetDTAttrsA
_GetDTAttrsA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a2
	movea.l	_DataTypesBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_GetDTAttrs_stub,code

; ULONG GetDTAttrs(Object * o, Tag attrs, ... )
	xdef	_GetDTAttrs
_GetDTAttrs:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	lea	16(sp),a2
	movea.l	_DataTypesBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_AddDTObject_stub,code

; LONG AddDTObject(struct Window * win, struct Requester * req, Object * o, LONG pos)
	xdef	_AddDTObject
_AddDTObject:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	move.l	24(sp),d0
	movea.l	_DataTypesBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RefreshDTObjectA_stub,code

; VOID RefreshDTObjectA(Object * o, struct Window * win, struct Requester * req, struct TagItem * attrs)
	xdef	_RefreshDTObjectA
_RefreshDTObjectA:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_RefreshDTObjects_stub,code

; VOID RefreshDTObjects(Object * o, struct Window * win, struct Requester * req, Tag attrs, ... )
	xdef	_RefreshDTObjects
_RefreshDTObjects:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_RefreshDTObject_stub,code

; VOID RefreshDTObject(Object * o, struct Window * win, struct Requester * req, Tag attrs, ... )
	xdef	_RefreshDTObject
_RefreshDTObject:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_DoAsyncLayout_stub,code

; ULONG DoAsyncLayout(Object * o, struct gpLayout * gpl)
	xdef	_DoAsyncLayout
_DoAsyncLayout:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_DoDTMethodA_stub,code

; ULONG DoDTMethodA(Object * o, struct Window * win, struct Requester * req, Msg msg)
	xdef	_DoDTMethodA
_DoDTMethodA:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_DoDTMethod_stub,code

; ULONG DoDTMethod(Object * o, struct Window * win, struct Requester * req, ULONG msg, ... )
	xdef	_DoDTMethod
_DoDTMethod:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_RemoveDTObject_stub,code

; LONG RemoveDTObject(struct Window * win, Object * o)
	xdef	_RemoveDTObject
_RemoveDTObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetDTMethods_stub,code

; ULONG * GetDTMethods(Object * object)
	xdef	_GetDTMethods
_GetDTMethods:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetDTTriggerMethods_stub,code

; struct DTMethods * GetDTTriggerMethods(Object * object)
	xdef	_GetDTTriggerMethods
_GetDTTriggerMethods:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_PrintDTObjectA_stub,code

; ULONG PrintDTObjectA(Object * o, struct Window * w, struct Requester * r, struct dtPrint * msg)
	xdef	_PrintDTObjectA
_PrintDTObjectA:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_PrintDTObject_stub,code

; ULONG PrintDTObject(Object * o, struct Window * w, struct Requester * r, ULONG msg, ... )
	xdef	_PrintDTObject
_PrintDTObject:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	lea	28(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_ObtainDTDrawInfoA_stub,code

; APTR ObtainDTDrawInfoA(Object * o, struct TagItem * attrs)
	xdef	_ObtainDTDrawInfoA
_ObtainDTDrawInfoA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_ObtainDTDrawInfo_stub,code

; APTR ObtainDTDrawInfo(Object * o, Tag attrs, ... )
	xdef	_ObtainDTDrawInfo
_ObtainDTDrawInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_DrawDTObjectA_stub,code

; LONG DrawDTObjectA(struct RastPort * rp, Object * o, LONG x, LONG y, LONG w, LONG h, LONG th, LONG tv, struct TagItem * attrs)
	xdef	_DrawDTObjectA
_DrawDTObjectA:
	movem.l	d2/d3/d4/d5/a2/a6,-(sp)
	movea.l	28(sp),a0
	movea.l	32(sp),a1
	move.l	36(sp),d0
	move.l	40(sp),d1
	move.l	44(sp),d2
	move.l	48(sp),d3
	move.l	52(sp),d4
	move.l	56(sp),d5
	movea.l	60(sp),a2
	movea.l	_DataTypesBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a2/a6
	rts

	section	_DrawDTObject_stub,code

; LONG DrawDTObject(struct RastPort * rp, Object * o, LONG x, LONG y, LONG w, LONG h, LONG th, LONG tv, Tag attrs, ... )
	xdef	_DrawDTObject
_DrawDTObject:
	movem.l	d2/d3/d4/d5/a2/a6,-(sp)
	movea.l	28(sp),a0
	movea.l	32(sp),a1
	move.l	36(sp),d0
	move.l	40(sp),d1
	move.l	44(sp),d2
	move.l	48(sp),d3
	move.l	52(sp),d4
	move.l	56(sp),d5
	lea	60(sp),a2
	movea.l	_DataTypesBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,d2/d3/d4/d5/a2/a6
	rts

	section	_ReleaseDTDrawInfo_stub,code

; VOID ReleaseDTDrawInfo(Object * o, APTR handle)
	xdef	_ReleaseDTDrawInfo
_ReleaseDTDrawInfo:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetDTString_stub,code

; STRPTR GetDTString(ULONG id)
	xdef	_GetDTString
_GetDTString:
	movem.l	a6,-(sp)
	move.l	8(sp),d0
	movea.l	_DataTypesBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

