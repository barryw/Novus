; ============================================================================
; AmiSSL Library Base Storage for Novus
; ============================================================================
; Provides storage for AmiSSL library base pointers.
; AmiSSL is an optional library for SSL/TLS encryption and must be
; opened explicitly at runtime via TlsConfig::client() or TlsConfig::server().
;
; The storage is initialized to 0 by the loader. The TlsConfig initialization
; opens amisslmaster.library and amissl.library, storing bases here.
; ============================================================================

	section	__MERGED,bss

; ============================================================================
; AmiSSL Library Bases
; ============================================================================
; Used by amissl.library stubs for TLS operations.
; Must be initialized by creating a TlsConfig before use.
; ============================================================================
	xdef	_AmiSSLMasterBase	; amisslmaster.library base
	xdef	_AmiSSLBase		; amissl.library base (opened via master)

_AmiSSLMasterBase:
	ds.l	1			; Reserve 1 longword for AmiSSLMasterBase

_AmiSSLBase:
	ds.l	1			; Reserve 1 longword for AmiSSLBase

