; Assembly wrappers for calling greeting.library from Novus
; These manually open the library and call functions
;
; FIXED: Use absolute addressing and separate data section to avoid
; PC-relative offset calculation errors during linking

        SECTION CODE

; Open library (called first time)
OpenLib:
        tst.l   GreetingLibraryBase.l
        bne.s   .already_open

        movem.l d0-d1/a0-a1/a6,-(sp)
        move.l  4.w,a6                  ; ExecBase
        lea     LibName,a1              ; Library name (PC-relative OK for lea)
        moveq   #1,d0                   ; Min version
        jsr     -552(a6)                ; OpenLibrary
        move.l  d0,GreetingLibraryBase.l ; Store with absolute addressing
        movem.l (sp)+,d0-d1/a0-a1/a6
.already_open:
        rts

; LibraryVersion call_GreetingLibrary_GetLibraryVersion(void)
; Returns LibraryVersion struct (6 bytes: major u16, minor u16, patch u16)
; VBCC calling convention: Caller pushes hidden result pointer on stack as first parameter
; This function receives that pointer in 4(sp) and passes it to the library in A0
        XDEF    _call_GreetingLibrary_GetLibraryVersion
_call_GreetingLibrary_GetLibraryVersion:
        jsr     OpenLib
        move.l  GreetingLibraryBase.l,a6  ; Absolute addressing
        cmp.l   #0,a6                      ; 68000-compatible test
        beq.s   .fail
        move.l  4(sp),a0                   ; A0 = result pointer passed by VBCC
        jsr     -42(a6)                    ; Call GetLibraryVersion (writes to *a0)
        rts                                ; Return (result already written via pointer)
.fail:
        ; On failure, zero out the result struct
        move.l  4(sp),a0                   ; Get result pointer
        clr.w   (a0)+                      ; major = 0
        clr.w   (a0)+                      ; minor = 0
        clr.w   (a0)+                      ; patch = 0
        rts

; int32_t call_GreetingLibrary_Add(int32_t a, int32_t b)
        XDEF    _call_GreetingLibrary_Add
_call_GreetingLibrary_Add:
        jsr     OpenLib
        move.l  GreetingLibraryBase.l,a6  ; Absolute addressing
        cmp.l   #0,a6                      ; 68000-compatible test
        beq.s   .fail
        move.l  4(sp),d0                   ; First param (a)
        move.l  8(sp),d1                   ; Second param (b)
        jsr     -30(a6)                    ; Call Add (offset -30)
        rts
.fail:
        moveq   #0,d0
        rts

; uint32_t call_GreetingLibrary_GetCallCount(void)
        XDEF    _call_GreetingLibrary_GetCallCount
_call_GreetingLibrary_GetCallCount:
        jsr     OpenLib
        move.l  GreetingLibraryBase.l,a6  ; Absolute addressing
        cmp.l   #0,a6                      ; 68000-compatible test
        beq.s   .fail
        jsr     -36(a6)                    ; Call GetCallCount (offset -36)
        rts
.fail:
        moveq   #0,d0
        rts

; Put data in separate section for proper linking
        SECTION DATA

        EVEN
GreetingLibraryBase:
        dc.l    0

LibName:
        dc.b    'greeting.library',0
        EVEN

        END
