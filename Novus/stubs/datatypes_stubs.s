; Generated from SFD file by Novus SFD Parser
; Library: datatypes.library
; Base: _DataTypesBase
; Each function is in its own section for dead code elimination

	xref	_DataTypesBase

	section	_ObtainDataTypeA_stub,code

; struct DataType * ObtainDataTypeA(ULONG type, APTR handle, struct TagItem * attrs)
	xdef	_ObtainDataTypeA
_ObtainDataTypeA:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-36(a6)
	rts

	section	_ReleaseDataType_stub,code

; VOID ReleaseDataType(struct DataType * dt)
	xdef	_ReleaseDataType
_ReleaseDataType:
	movea.l	4(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-42(a6)
	rts

	section	_NewDTObjectA_stub,code

; Object * NewDTObjectA(APTR name, struct TagItem * attrs)
	xdef	_NewDTObjectA
_NewDTObjectA:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-48(a6)
	rts

	section	_DisposeDTObject_stub,code

; VOID DisposeDTObject(Object * o)
	xdef	_DisposeDTObject
_DisposeDTObject:
	movea.l	4(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-54(a6)
	rts

	section	_SetDTAttrsA_stub,code

; ULONG SetDTAttrsA(Object * o, struct Window * win, struct Requester * req, struct TagItem * attrs)
	xdef	_SetDTAttrsA
_SetDTAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-60(a6)
	rts

	section	_GetDTAttrsA_stub,code

; ULONG GetDTAttrsA(Object * o, struct TagItem * attrs)
	xdef	_GetDTAttrsA
_GetDTAttrsA:
	movea.l	4(sp),a0
	movea.l	8(sp),a2
	movea.l	_DataTypesBase,a6
	jsr	-66(a6)
	rts

	section	_AddDTObject_stub,code

; LONG AddDTObject(struct Window * win, struct Requester * req, Object * o, LONG pos)
	xdef	_AddDTObject
_AddDTObject:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	movea.l	_DataTypesBase,a6
	jsr	-72(a6)
	rts

	section	_RefreshDTObjectA_stub,code

; VOID RefreshDTObjectA(Object * o, struct Window * win, struct Requester * req, struct TagItem * attrs)
	xdef	_RefreshDTObjectA
_RefreshDTObjectA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-78(a6)
	rts

	section	_DoAsyncLayout_stub,code

; ULONG DoAsyncLayout(Object * o, struct gpLayout * gpl)
	xdef	_DoAsyncLayout
_DoAsyncLayout:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-84(a6)
	rts

	section	_DoDTMethodA_stub,code

; ULONG DoDTMethodA(Object * o, struct Window * win, struct Requester * req, Msg msg)
	xdef	_DoDTMethodA
_DoDTMethodA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-90(a6)
	rts

	section	_RemoveDTObject_stub,code

; LONG RemoveDTObject(struct Window * win, Object * o)
	xdef	_RemoveDTObject
_RemoveDTObject:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-96(a6)
	rts

	section	_GetDTMethods_stub,code

; ULONG * GetDTMethods(Object * object)
	xdef	_GetDTMethods
_GetDTMethods:
	movea.l	4(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-102(a6)
	rts

	section	_GetDTTriggerMethods_stub,code

; struct DTMethods * GetDTTriggerMethods(Object * object)
	xdef	_GetDTTriggerMethods
_GetDTTriggerMethods:
	movea.l	4(sp),a0
	movea.l	_DataTypesBase,a6
	jsr	-108(a6)
	rts

	section	_PrintDTObjectA_stub,code

; ULONG PrintDTObjectA(Object * o, struct Window * w, struct Requester * r, struct dtPrint * msg)
	xdef	_PrintDTObjectA
_PrintDTObjectA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_DataTypesBase,a6
	jsr	-114(a6)
	rts

	section	_ObtainDTDrawInfoA_stub,code

; APTR ObtainDTDrawInfoA(Object * o, struct TagItem * attrs)
	xdef	_ObtainDTDrawInfoA
_ObtainDTDrawInfoA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-120(a6)
	rts

	section	_DrawDTObjectA_stub,code

; LONG DrawDTObjectA(struct RastPort * rp, Object * o, LONG x, LONG y, LONG w, LONG h, LONG th, LONG tv, struct TagItem * attrs)
	xdef	_DrawDTObjectA
_DrawDTObjectA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	move.l	16(sp),d1
	move.l	20(sp),d2
	move.l	24(sp),d3
	move.l	28(sp),d4
	move.l	32(sp),d5
	movea.l	36(sp),a2
	movea.l	_DataTypesBase,a6
	jsr	-126(a6)
	rts

	section	_ReleaseDTDrawInfo_stub,code

; VOID ReleaseDTDrawInfo(Object * o, APTR handle)
	xdef	_ReleaseDTDrawInfo
_ReleaseDTDrawInfo:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_DataTypesBase,a6
	jsr	-132(a6)
	rts

	section	_GetDTString_stub,code

; STRPTR GetDTString(ULONG id)
	xdef	_GetDTString
_GetDTString:
	move.l	4(sp),d0
	movea.l	_DataTypesBase,a6
	jsr	-138(a6)
	rts

