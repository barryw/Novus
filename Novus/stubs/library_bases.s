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
; ReAction Class Library Bases
; ============================================================================
; These are BOOPSI class libraries (*.class) for ReAction GUI toolkit.
; They're opened via OpenLibrary("xxx.class", 44) and provide GetClass functions.
; ============================================================================
	xdef	_WindowBase		; window.class base
	xdef	_LayoutBase		; layout.gadget base
	xdef	_ButtonBase		; button.gadget base
	xdef	_CheckBoxBase		; checkbox.gadget base
	xdef	_IntegerBase		; integer.gadget base
	xdef	_RadioButtonBase	; radiobutton.gadget base
	xdef	_LabelBase		; label.image base

; ============================================================================
; MUI (Magic User Interface) Library Base
; ============================================================================
; MUI is a BOOPSI-based GUI toolkit providing advanced widget capabilities.
; Opened via OpenLibrary("muimaster.library", 20).
; ============================================================================
	xdef	_MUIMasterBase		; muimaster.library base

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
; ReAction Class Library Base Storage
; ============================================================================
_WindowBase:
	ds.l	1			; Reserve 1 longword for WindowBase
_LayoutBase:
	ds.l	1			; Reserve 1 longword for LayoutBase
_ButtonBase:
	ds.l	1			; Reserve 1 longword for ButtonBase
_CheckBoxBase:
	ds.l	1			; Reserve 1 longword for CheckBoxBase
_IntegerBase:
	ds.l	1			; Reserve 1 longword for IntegerBase
_RadioButtonBase:
	ds.l	1			; Reserve 1 longword for RadioButtonBase
_LabelBase:
	ds.l	1			; Reserve 1 longword for LabelBase

; ============================================================================
; MUI Library Base Storage
; ============================================================================
_MUIMasterBase:
	ds.l	1			; Reserve 1 longword for MUIMasterBase

; ============================================================================
; Workbench Startup Support
; ============================================================================
	xdef	_WBStartupMsg		; WBStartup message pointer (NULL if CLI)

_WBStartupMsg:
	ds.l	1			; Reserve 1 longword for WBStartup message

