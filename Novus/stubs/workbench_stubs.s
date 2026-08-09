; Generated from SFD file by Novus SFD Parser
; Library: workbench.library
; Base: _WorkbenchBase
; Each function is in its own section for dead code elimination

	xref	_WorkbenchBase

	section	_AddAppWindowA_stub,code

; struct AppWindow * AddAppWindowA(ULONG id, ULONG userdata, struct Window * window, struct MsgPort * msgport, struct TagItem * taglist)
	xdef	_AddAppWindowA
_AddAppWindowA:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a0
	movea.l	24(sp),a1
	movea.l	28(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_AddAppWindow_stub,code

; struct AppWindow * AddAppWindow(ULONG id, ULONG userdata, struct Window * window, struct MsgPort * msgport, Tag taglist, ... )
	xdef	_AddAppWindow
_AddAppWindow:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a0
	movea.l	24(sp),a1
	lea	28(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RemoveAppWindow_stub,code

; BOOL RemoveAppWindow(struct AppWindow * appWindow)
	xdef	_RemoveAppWindow
_RemoveAppWindow:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_WorkbenchBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddAppIconA_stub,code

; struct AppIcon * AddAppIconA(ULONG id, ULONG userdata, UBYTE * text, struct MsgPort * msgport, BPTR lock, struct DiskObject * diskobj, struct TagItem * taglist)
	xdef	_AddAppIconA
_AddAppIconA:
	movem.l	a2/a3/a4/a6,-(sp)
	move.l	20(sp),d0
	move.l	24(sp),d1
	movea.l	28(sp),a0
	movea.l	32(sp),a1
	movea.l	36(sp),a2
	movea.l	40(sp),a3
	movea.l	44(sp),a4
	movea.l	_WorkbenchBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a2/a3/a4/a6
	rts

	section	_AddAppIcon_stub,code

; struct AppIcon * AddAppIcon(ULONG id, ULONG userdata, UBYTE * text, struct MsgPort * msgport, BPTR lock, struct DiskObject * diskobj, Tag taglist, ... )
	xdef	_AddAppIcon
_AddAppIcon:
	movem.l	a2/a3/a4/a6,-(sp)
	move.l	20(sp),d0
	move.l	24(sp),d1
	movea.l	28(sp),a0
	movea.l	32(sp),a1
	movea.l	36(sp),a2
	movea.l	40(sp),a3
	lea	44(sp),a4
	movea.l	_WorkbenchBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a2/a3/a4/a6
	rts

	section	_RemoveAppIcon_stub,code

; BOOL RemoveAppIcon(struct AppIcon * appIcon)
	xdef	_RemoveAppIcon
_RemoveAppIcon:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_WorkbenchBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddAppMenuItemA_stub,code

; struct AppMenuItem * AddAppMenuItemA(ULONG id, ULONG userdata, UBYTE * text, struct MsgPort * msgport, struct TagItem * taglist)
	xdef	_AddAppMenuItemA
_AddAppMenuItemA:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a0
	movea.l	24(sp),a1
	movea.l	28(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_AddAppMenuItem_stub,code

; struct AppMenuItem * AddAppMenuItem(ULONG id, ULONG userdata, UBYTE * text, struct MsgPort * msgport, Tag taglist, ... )
	xdef	_AddAppMenuItem
_AddAppMenuItem:
	movem.l	a2/a6,-(sp)
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a0
	movea.l	24(sp),a1
	lea	28(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_RemoveAppMenuItem_stub,code

; BOOL RemoveAppMenuItem(struct AppMenuItem * appMenuItem)
	xdef	_RemoveAppMenuItem
_RemoveAppMenuItem:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_WorkbenchBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_WBInfo_stub,code

; VOID WBInfo(BPTR lock, STRPTR name, struct Screen * screen)
	xdef	_WBInfo
_WBInfo:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_OpenWorkbenchObjectA_stub,code

; BOOL OpenWorkbenchObjectA(STRPTR name, struct TagItem * tags)
	xdef	_OpenWorkbenchObjectA
_OpenWorkbenchObjectA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenWorkbenchObject_stub,code

; BOOL OpenWorkbenchObject(STRPTR name, ... )
	xdef	_OpenWorkbenchObject
_OpenWorkbenchObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseWorkbenchObjectA_stub,code

; BOOL CloseWorkbenchObjectA(STRPTR name, struct TagItem * tags)
	xdef	_CloseWorkbenchObjectA
_CloseWorkbenchObjectA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseWorkbenchObject_stub,code

; BOOL CloseWorkbenchObject(STRPTR name, ... )
	xdef	_CloseWorkbenchObject
_CloseWorkbenchObject:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_WorkbenchControlA_stub,code

; BOOL WorkbenchControlA(STRPTR name, struct TagItem * tags)
	xdef	_WorkbenchControlA
_WorkbenchControlA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_WorkbenchControl_stub,code

; BOOL WorkbenchControl(STRPTR name, ... )
	xdef	_WorkbenchControl
_WorkbenchControl:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddAppWindowDropZoneA_stub,code

; struct AppWindowDropZone * AddAppWindowDropZoneA(struct AppWindow * aw, ULONG id, ULONG userdata, struct TagItem * tags)
	xdef	_AddAppWindowDropZoneA
_AddAppWindowDropZoneA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	movea.l	20(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_AddAppWindowDropZone_stub,code

; struct AppWindowDropZone * AddAppWindowDropZone(struct AppWindow * aw, ULONG id, ULONG userdata, ... )
	xdef	_AddAppWindowDropZone
_AddAppWindowDropZone:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	move.l	16(sp),d1
	lea	20(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_RemoveAppWindowDropZone_stub,code

; BOOL RemoveAppWindowDropZone(struct AppWindow * aw, struct AppWindowDropZone * dropZone)
	xdef	_RemoveAppWindowDropZone
_RemoveAppWindowDropZone:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_ChangeWorkbenchSelectionA_stub,code

; BOOL ChangeWorkbenchSelectionA(STRPTR name, struct Hook * hook, struct TagItem * tags)
	xdef	_ChangeWorkbenchSelectionA
_ChangeWorkbenchSelectionA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_ChangeWorkbenchSelection_stub,code

; BOOL ChangeWorkbenchSelection(STRPTR name, struct Hook * hook, ... )
	xdef	_ChangeWorkbenchSelection
_ChangeWorkbenchSelection:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	lea	20(sp),a2
	movea.l	_WorkbenchBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_MakeWorkbenchObjectVisibleA_stub,code

; BOOL MakeWorkbenchObjectVisibleA(STRPTR name, struct TagItem * tags)
	xdef	_MakeWorkbenchObjectVisibleA
_MakeWorkbenchObjectVisibleA:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_MakeWorkbenchObjectVisible_stub,code

; BOOL MakeWorkbenchObjectVisible(STRPTR name, ... )
	xdef	_MakeWorkbenchObjectVisible
_MakeWorkbenchObjectVisible:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	lea	12(sp),a1
	movea.l	_WorkbenchBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

