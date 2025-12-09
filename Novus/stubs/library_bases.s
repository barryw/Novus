; ============================================================================
; Library Base Storage for Novus
; ============================================================================
; Provides storage for library base pointers used by Novus programs.
; These are initialized by -lauto constructors called from ___main.
;
; NOTE: -lauto references these symbols but doesn't define them. We must
; provide the BSS storage here. The -lauto constructors then populate them.
; ============================================================================

	section	__MERGED,bss

; ============================================================================
; Exported Library Bases
; ============================================================================
; These symbols are required by -lauto for automatic library opening.
; They're also used directly by our runtime and generated code.
; ============================================================================
	xdef	_SysBase		; exec.library base
	xdef	_DOSBase		; dos.library base
	xdef	_IntuitionBase		; intuition.library base
	xdef	_GadToolsBase		; gadtools.library base
	xdef	_GfxBase		; graphics.library base
	xdef	_DiskfontBase		; diskfont.library base

; ============================================================================
; Storage (initialized to 0 by loader)
; ============================================================================
_SysBase:
	ds.l	1			; Reserve 1 longword for SysBase

_DOSBase:
	ds.l	1			; Reserve 1 longword for DOSBase

_IntuitionBase:
	ds.l	1			; Reserve 1 longword for IntuitionBase

_GadToolsBase:
	ds.l	1			; Reserve 1 longword for GadToolsBase

_GfxBase:
	ds.l	1			; Reserve 1 longword for GfxBase

_DiskfontBase:
	ds.l	1			; Reserve 1 longword for DiskfontBase

; ============================================================================
; Workbench Startup Support
; ============================================================================
	xdef	_WBStartupMsg		; WBStartup message pointer (NULL if CLI)

_WBStartupMsg:
	ds.l	1			; Reserve 1 longword for WBStartup message

