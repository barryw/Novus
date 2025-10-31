; Generated from SFD file by Novus SFD Parser
; Library: asl.library
; Base: _AslBase
; Each function is in its own section for dead code elimination

	xref	_AslBase

	section	_AllocFileRequest_stub,code

; struct FileRequester * AllocFileRequest()
	xdef	_AllocFileRequest
_AllocFileRequest:
	movea.l	_AslBase,a6
	jsr	-30(a6)
	rts

	section	_FreeFileRequest_stub,code

; VOID FreeFileRequest(struct FileRequester * fileReq)
	xdef	_FreeFileRequest
_FreeFileRequest:
	movea.l	4(sp),a0
	movea.l	_AslBase,a6
	jsr	-36(a6)
	rts

	section	_RequestFile_stub,code

; BOOL RequestFile(struct FileRequester * fileReq)
	xdef	_RequestFile
_RequestFile:
	movea.l	4(sp),a0
	movea.l	_AslBase,a6
	jsr	-42(a6)
	rts

	section	_AllocAslRequest_stub,code

; APTR AllocAslRequest(ULONG reqType, struct TagItem * tagList)
	xdef	_AllocAslRequest
_AllocAslRequest:
	move.l	4(sp),d0
	movea.l	8(sp),a0
	movea.l	_AslBase,a6
	jsr	-48(a6)
	rts

	section	_FreeAslRequest_stub,code

; VOID FreeAslRequest(APTR requester)
	xdef	_FreeAslRequest
_FreeAslRequest:
	movea.l	4(sp),a0
	movea.l	_AslBase,a6
	jsr	-60(a6)
	rts

	section	_AslRequest_stub,code

; BOOL AslRequest(APTR requester, struct TagItem * tagList)
	xdef	_AslRequest
_AslRequest:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	_AslBase,a6
	jsr	-66(a6)
	rts

	section	_AbortAslRequest_stub,code

; VOID AbortAslRequest(APTR requester)
	xdef	_AbortAslRequest
_AbortAslRequest:
	movea.l	4(sp),a0
	movea.l	_AslBase,a6
	jsr	-90(a6)
	rts

	section	_ActivateAslRequest_stub,code

; VOID ActivateAslRequest(APTR requester)
	xdef	_ActivateAslRequest
_ActivateAslRequest:
	movea.l	4(sp),a0
	movea.l	_AslBase,a6
	jsr	-96(a6)
	rts

