; ============================================================================
; Library Base Storage for Novus
; ============================================================================
; Provides storage for library base pointers used by Novus programs
; This replaces the auto-library feature from VBCC's -lamiga
; ============================================================================

	section	__MERGED,bss

; ============================================================================
; Exported Library Bases
; ============================================================================
	xdef	_SysBase	; exec.library base
	xdef	_DOSBase	; dos.library base
	xdef	_IntuitionBase	; intuition.library base
	xdef	_GadToolsBase	; gadtools.library base

; ============================================================================
; Storage (initialized to 0 by loader)
; ============================================================================
_SysBase:
	ds.l	1		; Reserve 1 longword for SysBase

_DOSBase:
	ds.l	1		; Reserve 1 longword for DOSBase

_IntuitionBase:
	ds.l	1		; Reserve 1 longword for IntuitionBase

_GadToolsBase:
	ds.l	1		; Reserve 1 longword for GadToolsBase
