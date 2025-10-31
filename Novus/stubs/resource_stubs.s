; Generated from SFD file by Novus SFD Parser
; Library: resource.library
; Base: _ResourceBase
; Each function is in its own section for dead code elimination

	xref	_ResourceBase

	section	_RL_OpenResource_stub,code

; RESOURCEFILE RL_OpenResource(APTR resource, struct Screen * screen, struct Catalog * catalog)
	xdef	_RL_OpenResource
_RL_OpenResource:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_ResourceBase,a6
	jsr	-30(a6)
	rts

	section	_RL_CloseResource_stub,code

; VOID RL_CloseResource(RESOURCEFILE resfile)
	xdef	_RL_CloseResource
_RL_CloseResource:
	movea.l	4(sp),a0
	movea.l	_ResourceBase,a6
	jsr	-36(a6)
	rts

	section	_RL_NewObjectA_stub,code

; Object * RL_NewObjectA(RESOURCEFILE resfile, RESOURCEID resid, struct TagItem * tags)
	xdef	_RL_NewObjectA
_RL_NewObjectA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_ResourceBase,a6
	jsr	-42(a6)
	rts

	section	_RL_DisposeObject_stub,code

; VOID RL_DisposeObject(RESOURCEFILE resfile, Object * obj)
	xdef	_RL_DisposeObject
_RL_DisposeObject:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ResourceBase,a6
	jsr	-54(a6)
	rts

	section	_RL_NewGroupA_stub,code

; Object ** RL_NewGroupA(RESOURCEFILE resfile, RESOURCEID id, struct TagItem * taglist)
	xdef	_RL_NewGroupA
_RL_NewGroupA:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_ResourceBase,a6
	jsr	-60(a6)
	rts

	section	_RL_DisposeGroup_stub,code

; VOID RL_DisposeGroup(RESOURCEFILE resfile, Object ** obj)
	xdef	_RL_DisposeGroup
_RL_DisposeGroup:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ResourceBase,a6
	jsr	-72(a6)
	rts

	section	_RL_GetObjectArray_stub,code

; Object ** RL_GetObjectArray(RESOURCEFILE resfile, Object * obj, RESOURCEID id)
	xdef	_RL_GetObjectArray
_RL_GetObjectArray:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	move.l	12(sp),d0
	movea.l	_ResourceBase,a6
	jsr	-78(a6)
	rts

	section	_RL_SetResourceScreen_stub,code

; BOOL RL_SetResourceScreen(RESOURCEFILE resfile, struct Screen * screen)
	xdef	_RL_SetResourceScreen
_RL_SetResourceScreen:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_ResourceBase,a6
	jsr	-84(a6)
	rts

