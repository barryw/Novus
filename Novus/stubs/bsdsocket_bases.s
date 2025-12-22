; ============================================================================
; BSD Socket Library Base Storage for Novus
; ============================================================================
; Provides storage for bsdsocket.library base pointer.
; Unlike system libraries (exec, dos, etc.), bsdsocket.library is optional
; and must be opened explicitly at runtime via SocketLibrary::init().
;
; The storage is initialized to 0 by the loader. The SocketLibrary::init()
; function opens bsdsocket.library and stores the base here.
; ============================================================================

	section	__MERGED,bss

; ============================================================================
; BSD Socket Library Base
; ============================================================================
; Used by bsdsocket.library stubs for socket operations.
; Must be initialized by calling SocketLibrary::init() before use.
; ============================================================================
	xdef	_SocketBase		; bsdsocket.library base

_SocketBase:
	ds.l	1			; Reserve 1 longword for SocketBase

