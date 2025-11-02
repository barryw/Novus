; Auto-generated library stub for VBCC
; Opens greeting.library automatically

        SECTION __MERGED,DATA

; Library base variable (exported for use in programs)
        XDEF    _GreetingLibraryBase
_GreetingLibraryBase:
        dc.l    0

; Library name string
LibName:
        dc.b    'greeting.library',0
        EVEN

; Constructor (called at program startup)
        SECTION CODE
        XDEF    __INIT_greeting_library
__INIT_greeting_library:
        movem.l d0-d1/a0-a1/a6,-(sp)
        move.l  4.w,a6          ; Get ExecBase
        lea     LibName(pc),a1  ; Library name
        moveq   #1,d0         ; Minimum version
        jsr     -552(a6)        ; OpenLibrary
        lea     _GreetingLibraryBase(pc),a0
        move.l  d0,(a0)         ; Store library base
        movem.l (sp)+,d0-d1/a0-a1/a6
        rts

; Destructor (called at program exit)
        XDEF    __EXIT_greeting_library
__EXIT_greeting_library:
        movem.l a1/a6,-(sp)
        move.l  _GreetingLibraryBase(pc),d0
        beq.s   .skip
        move.l  d0,a1           ; Library base
        move.l  4.w,a6          ; Get ExecBase
        jsr     -414(a6)        ; CloseLibrary
.skip:
        movem.l (sp)+,a1/a6
        rts

; Register constructor/destructor with linker sets
        SECTION ___CTOR_LIST__,DATA
        dc.l    __INIT_greeting_library
        SECTION ___DTOR_LIST__,DATA
        dc.l    __EXIT_greeting_library

        END
