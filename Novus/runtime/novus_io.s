; Novus Runtime Library - I/O Support (Assembly)
; Variadic write() function that forwards to dos.library/VPrintf
;
; C signature: int32_t write(const char* format, ...)
; 68k ABI:     D0 = format ptr, args on stack at 4(sp)
;
; VPrintf signature: LONG VPrintf(STRPTR fmt, LONG *argv)
; 68k ABI:           D1 = format, D2 = argv ptr

    xdef    _write              ; Export write symbol
    xref    _DOSBase            ; DOS library base (initialized by startup)

    section code

_write:
    ; Parameters:
    ;   4(sp) = format string pointer
    ;   8(sp) = first variadic arg
    ;   12(sp), 16(sp), ... = additional args
    ;
    ; Strategy: Pass format in D1, and address of first arg (8(sp)) in D2

    move.l  4(sp),d1            ; D1 = format string
    lea     8(sp),a0            ; A0 = pointer to first vararg
    move.l  a0,d2               ; D2 = argv array pointer

    move.l  _DOSBase,a6         ; A6 = DOS library base
    jsr     -954(a6)            ; VPrintf is at offset -954

    ; VPrintf returns count in D0 (already there)
    rts
