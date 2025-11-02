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

; uint32_t call_GreetingLibrary_GetVersion(void)
        XDEF    _call_GreetingLibrary_GetVersion
_call_GreetingLibrary_GetVersion:
        jsr     OpenLib
        move.l  GreetingLibraryBase.l,a6  ; Absolute addressing
        cmp.l   #0,a6                      ; 68000-compatible test
        beq.s   .fail
        jsr     -30(a6)                    ; Call GetVersion
        rts
.fail:
        moveq   #0,d0
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
        jsr     -36(a6)                    ; Call Add
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
        jsr     -42(a6)                    ; Call GetCallCount
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
