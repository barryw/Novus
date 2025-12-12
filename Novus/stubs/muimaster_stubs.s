; Generated from FD file by Novus
; Library: muimaster.library
; Base: _MUIMasterBase
; Each function is in its own section for dead code elimination
; NOTE: Uses lazy initialization via ___mui_ensure

	xref	_MUIMasterBase
	xref	___mui_ensure	; Lazy init - opens library if needed, returns base in A6

	section	_MUI_NewObjectA_stub,code

; APTR MUI_NewObjectA(CONST_STRPTR classname, struct TagItem *tags)
; Registers: a0 = classname, a1 = tags
; LVO: -30 (0x1e)
	xdef	_MUI_NewObjectA
_MUI_NewObjectA:
	movea.l	4(sp),a0	; classname
	movea.l	8(sp),a1	; tags
	jsr	___mui_ensure
	jsr	-30(a6)
	rts

	section	_MUI_DisposeObject_stub,code

; VOID MUI_DisposeObject(APTR obj)
; Registers: a0 = obj
; LVO: -36 (0x24)
	xdef	_MUI_DisposeObject
_MUI_DisposeObject:
	movea.l	4(sp),a0	; obj
	jsr	___mui_ensure
	jsr	-36(a6)
	rts

	section	_MUI_RequestA_stub,code

; LONG MUI_RequestA(APTR app, APTR win, LONGBITS flags, CONST_STRPTR title, CONST_STRPTR gadgets, CONST_STRPTR format, APTR params)
; Registers: d0 = app, d1 = win, d2 = flags, a0 = title, a1 = gadgets, a2 = format, a3 = params
; LVO: -42 (0x2a)
	xdef	_MUI_RequestA
_MUI_RequestA:
	move.l	4(sp),d0	; app
	move.l	8(sp),d1	; win
	move.l	12(sp),d2	; flags
	movea.l	16(sp),a0	; title
	movea.l	20(sp),a1	; gadgets
	movea.l	24(sp),a2	; format
	movea.l	28(sp),a3	; params
	jsr	___mui_ensure
	jsr	-42(a6)
	rts

	section	_MUI_AllocAslRequest_stub,code

; APTR MUI_AllocAslRequest(ULONG type, struct TagItem *tags)
; Registers: d0 = type, a0 = tags
; LVO: -48 (0x30)
	xdef	_MUI_AllocAslRequest
_MUI_AllocAslRequest:
	move.l	4(sp),d0	; type
	movea.l	8(sp),a0	; tags
	jsr	___mui_ensure
	jsr	-48(a6)
	rts

	section	_MUI_AslRequest_stub,code

; BOOL MUI_AslRequest(APTR req, struct TagItem *tags)
; Registers: a0 = req, a1 = tags
; LVO: -54 (0x36)
	xdef	_MUI_AslRequest
_MUI_AslRequest:
	movea.l	4(sp),a0	; req
	movea.l	8(sp),a1	; tags
	jsr	___mui_ensure
	jsr	-54(a6)
	rts

	section	_MUI_FreeAslRequest_stub,code

; VOID MUI_FreeAslRequest(APTR req)
; Registers: a0 = req
; LVO: -60 (0x3c)
	xdef	_MUI_FreeAslRequest
_MUI_FreeAslRequest:
	movea.l	4(sp),a0	; req
	jsr	___mui_ensure
	jsr	-60(a6)
	rts

	section	_MUI_Error_stub,code

; LONG MUI_Error()
; Registers: none
; LVO: -66 (0x42)
	xdef	_MUI_Error
_MUI_Error:
	jsr	___mui_ensure
	jsr	-66(a6)
	rts

	section	_MUI_SetError_stub,code

; LONG MUI_SetError(LONG errnum)
; Registers: d0 = errnum
; LVO: -72 (0x48)
	xdef	_MUI_SetError
_MUI_SetError:
	move.l	4(sp),d0	; errnum
	jsr	___mui_ensure
	jsr	-72(a6)
	rts

	section	_MUI_GetClass_stub,code

; struct IClass * MUI_GetClass(CONST_STRPTR classname)
; Registers: a0 = classname
; LVO: -78 (0x4e)
	xdef	_MUI_GetClass
_MUI_GetClass:
	movea.l	4(sp),a0	; classname
	jsr	___mui_ensure
	jsr	-78(a6)
	rts

	section	_MUI_FreeClass_stub,code

; VOID MUI_FreeClass(struct IClass *cl)
; Registers: a0 = cl
; LVO: -84 (0x54)
	xdef	_MUI_FreeClass
_MUI_FreeClass:
	movea.l	4(sp),a0	; cl
	jsr	___mui_ensure
	jsr	-84(a6)
	rts

	section	_MUI_RequestIDCMP_stub,code

; VOID MUI_RequestIDCMP(Object *obj, ULONG flags)
; Registers: a0 = obj, d0 = flags
; LVO: -90 (0x5a)
	xdef	_MUI_RequestIDCMP
_MUI_RequestIDCMP:
	movea.l	4(sp),a0	; obj
	move.l	8(sp),d0	; flags
	jsr	___mui_ensure
	jsr	-90(a6)
	rts

	section	_MUI_RejectIDCMP_stub,code

; VOID MUI_RejectIDCMP(Object *obj, ULONG flags)
; Registers: a0 = obj, d0 = flags
; LVO: -96 (0x60)
	xdef	_MUI_RejectIDCMP
_MUI_RejectIDCMP:
	movea.l	4(sp),a0	; obj
	move.l	8(sp),d0	; flags
	jsr	___mui_ensure
	jsr	-96(a6)
	rts

	section	_MUI_Redraw_stub,code

; VOID MUI_Redraw(Object *obj, ULONG flags)
; Registers: a0 = obj, d0 = flags
; LVO: -102 (0x66)
	xdef	_MUI_Redraw
_MUI_Redraw:
	movea.l	4(sp),a0	; obj
	move.l	8(sp),d0	; flags
	jsr	___mui_ensure
	jsr	-102(a6)
	rts

	section	_MUI_CreateCustomClass_stub,code

; struct MUI_CustomClass * MUI_CreateCustomClass(struct Library *base, CONST_STRPTR supername, struct MUI_CustomClass *supermcc, LONG datasize, APTR dispatcher)
; Registers: a0 = base, a1 = supername, a2 = supermcc, d0 = datasize, a3 = dispatcher
; LVO: -108 (0x6c)
	xdef	_MUI_CreateCustomClass
_MUI_CreateCustomClass:
	movea.l	4(sp),a0	; base
	movea.l	8(sp),a1	; supername
	movea.l	12(sp),a2	; supermcc
	move.l	16(sp),d0	; datasize
	movea.l	20(sp),a3	; dispatcher
	jsr	___mui_ensure
	jsr	-108(a6)
	rts

	section	_MUI_DeleteCustomClass_stub,code

; BOOL MUI_DeleteCustomClass(struct MUI_CustomClass *mcc)
; Registers: a0 = mcc
; LVO: -114 (0x72)
	xdef	_MUI_DeleteCustomClass
_MUI_DeleteCustomClass:
	movea.l	4(sp),a0	; mcc
	jsr	___mui_ensure
	jsr	-114(a6)
	rts

	section	_MUI_MakeObjectA_stub,code

; Object * MUI_MakeObjectA(LONG type, ULONG *params)
; Registers: d0 = type, a0 = params
; LVO: -120 (0x78)
	xdef	_MUI_MakeObjectA
_MUI_MakeObjectA:
	move.l	4(sp),d0	; type
	movea.l	8(sp),a0	; params
	jsr	___mui_ensure
	jsr	-120(a6)
	rts

	section	_MUI_Layout_stub,code

; BOOL MUI_Layout(Object *obj, LONG l, LONG t, LONG w, LONG h, ULONG flags)
; Registers: a0 = obj, d0 = l, d1 = t, d2 = w, d3 = h, d4 = flags
; LVO: -126 (0x7e)
	xdef	_MUI_Layout
_MUI_Layout:
	movea.l	4(sp),a0	; obj
	move.l	8(sp),d0	; l
	move.l	12(sp),d1	; t
	move.l	16(sp),d2	; w
	move.l	20(sp),d3	; h
	move.l	24(sp),d4	; flags
	jsr	___mui_ensure
	jsr	-126(a6)
	rts

	section	_MUI_ObtainPen_stub,code

; LONG MUI_ObtainPen(struct MUI_RenderInfo *mri, struct MUI_PenSpec *spec, ULONG flags)
; Registers: a0 = mri, a1 = spec, d0 = flags
; LVO: -156 (0x9c)
	xdef	_MUI_ObtainPen
_MUI_ObtainPen:
	movea.l	4(sp),a0	; mri
	movea.l	8(sp),a1	; spec
	move.l	12(sp),d0	; flags
	jsr	___mui_ensure
	jsr	-156(a6)
	rts

	section	_MUI_ReleasePen_stub,code

; VOID MUI_ReleasePen(struct MUI_RenderInfo *mri, LONG pen)
; Registers: a0 = mri, d0 = pen
; LVO: -162 (0xa2)
	xdef	_MUI_ReleasePen
_MUI_ReleasePen:
	movea.l	4(sp),a0	; mri
	move.l	8(sp),d0	; pen
	jsr	___mui_ensure
	jsr	-162(a6)
	rts

	section	_MUI_AddClipping_stub,code

; APTR MUI_AddClipping(struct MUI_RenderInfo *mri, WORD l, WORD t, WORD w, WORD h)
; Registers: a0 = mri, d0 = l, d1 = t, d2 = w, d3 = h
; LVO: -168 (0xa8)
	xdef	_MUI_AddClipping
_MUI_AddClipping:
	movea.l	4(sp),a0	; mri
	move.l	8(sp),d0	; l
	move.l	12(sp),d1	; t
	move.l	16(sp),d2	; w
	move.l	20(sp),d3	; h
	jsr	___mui_ensure
	jsr	-168(a6)
	rts

	section	_MUI_RemoveClipping_stub,code

; VOID MUI_RemoveClipping(struct MUI_RenderInfo *mri, APTR h)
; Registers: a0 = mri, a1 = h
; LVO: -174 (0xae)
	xdef	_MUI_RemoveClipping
_MUI_RemoveClipping:
	movea.l	4(sp),a0	; mri
	movea.l	8(sp),a1	; h
	jsr	___mui_ensure
	jsr	-174(a6)
	rts

	section	_MUI_AddClipRegion_stub,code

; APTR MUI_AddClipRegion(struct MUI_RenderInfo *mri, struct Region *region)
; Registers: a0 = mri, a1 = region
; LVO: -180 (0xb4)
	xdef	_MUI_AddClipRegion
_MUI_AddClipRegion:
	movea.l	4(sp),a0	; mri
	movea.l	8(sp),a1	; region
	jsr	___mui_ensure
	jsr	-180(a6)
	rts

	section	_MUI_RemoveClipRegion_stub,code

; VOID MUI_RemoveClipRegion(struct MUI_RenderInfo *mri, APTR region)
; Registers: a0 = mri, a1 = region
; LVO: -186 (0xba)
	xdef	_MUI_RemoveClipRegion
_MUI_RemoveClipRegion:
	movea.l	4(sp),a0	; mri
	movea.l	8(sp),a1	; region
	jsr	___mui_ensure
	jsr	-186(a6)
	rts

	section	_MUI_BeginRefresh_stub,code

; BOOL MUI_BeginRefresh(struct MUI_RenderInfo *mri, ULONG flags)
; Registers: a0 = mri, d0 = flags
; LVO: -192 (0xc0)
	xdef	_MUI_BeginRefresh
_MUI_BeginRefresh:
	movea.l	4(sp),a0	; mri
	move.l	8(sp),d0	; flags
	jsr	___mui_ensure
	jsr	-192(a6)
	rts

	section	_MUI_EndRefresh_stub,code

; VOID MUI_EndRefresh(struct MUI_RenderInfo *mri, ULONG flags)
; Registers: a0 = mri, d0 = flags
; LVO: -198 (0xc6)
	xdef	_MUI_EndRefresh
_MUI_EndRefresh:
	movea.l	4(sp),a0	; mri
	move.l	8(sp),d0	; flags
	jsr	___mui_ensure
	jsr	-198(a6)
	rts

	section	_MUI_Show_stub,code

; BOOL MUI_Show(Object *obj)
; Registers: a0 = obj
; LVO: -216 (0xd8)
	xdef	_MUI_Show
_MUI_Show:
	movea.l	4(sp),a0	; obj
	jsr	___mui_ensure
	jsr	-216(a6)
	rts

	section	_MUI_Hide_stub,code

; VOID MUI_Hide(Object *obj)
; Registers: a0 = obj
; LVO: -222 (0xde)
	xdef	_MUI_Hide
_MUI_Hide:
	movea.l	4(sp),a0	; obj
	jsr	___mui_ensure
	jsr	-222(a6)
	rts

	section	_MUI_LayoutObj_stub,code

; BOOL MUI_LayoutObj(Object *obj, LONG l, LONG t, LONG w, LONG h, ULONG flags)
; Registers: a0 = obj, d0 = l, d1 = t, d2 = w, d3 = h, d4 = flags
; LVO: -228 (0xe4)
	xdef	_MUI_LayoutObj
_MUI_LayoutObj:
	movea.l	4(sp),a0	; obj
	move.l	8(sp),d0	; l
	move.l	12(sp),d1	; t
	move.l	16(sp),d2	; w
	move.l	20(sp),d3	; h
	move.l	24(sp),d4	; flags
	jsr	___mui_ensure
	jsr	-228(a6)
	rts

	section	_MUI_Offset_stub,code

; VOID MUI_Offset(Object *obj, LONG x, LONG y)
; Registers: a0 = obj, d0 = x, d1 = y
; LVO: -234 (0xea)
	xdef	_MUI_Offset
_MUI_Offset:
	movea.l	4(sp),a0	; obj
	move.l	8(sp),d0	; x
	move.l	12(sp),d1	; y
	jsr	___mui_ensure
	jsr	-234(a6)
	rts

