; wrappers.s - A6-relative calling convention wrappers
;
; This file contains assembly wrappers that translate between AmigaOS
; library calling conventions and standard C calling conventions.
;
; AMIGAOS LIBRARY CONVENTION:
; - Library base in A6 register
; - Parameters in D0, D1, A0, A1, then stack (right-to-left)
; - Return value in D0
; - Must preserve D2-D7, A2-A6
; - Can modify D0-D1, A0-A1
;
; C/NOVUS CONVENTION:
; - All parameters on stack (right-to-left push)
; - Return value in D0
; - A6 used as frame pointer
; - Must preserve D2-D7, A2-A6
;
; TRANSLATION PROCESS:
; 1. Save A6 (library base) - we need it for the Novus function
; 2. Move library base from A6 to stack as first parameter
; 3. Move other parameters from registers to stack
; 4. Call the Novus function (standard C linkage)
; 5. Restore registers
; 6. Return (RTS)

    XDEF    _LibOpen        ; Export Open wrapper
    XDEF    _LibClose       ; Export Close wrapper
    XDEF    _LibExpunge     ; Export Expunge wrapper
    XDEF    _LibReserved    ; Export Reserved wrapper
    XDEF    _GetVersion     ; Export GetVersion wrapper
    XDEF    _Multiply       ; Export Multiply wrapper

    XREF    _novus_LibOpen      ; Import Novus function from lib.o
    XREF    _novus_LibClose     ; Import Novus function from lib.o
    XREF    _novus_LibExpunge   ; Import Novus function from lib.o
    XREF    _novus_LibReserved  ; Import Novus function from lib.o
    XREF    _novus_GetVersion   ; Import Novus function from lib.o
    XREF    _novus_Multiply     ; Import Novus function from lib.o

; ============================================================================
; LibOpen Wrapper
; ============================================================================
; AmigaOS convention: LibOpen(A6=base, D0=version)
; C convention: LibOpen(base, version)
;
; TRANSLATION:
; A6 (base) -> stack parameter 1
; D0 (version) -> stack parameter 2

_LibOpen:
    movem.l d2-d7/a2-a6,-(sp)   ; Save registers (10 regs * 4 bytes = 40 bytes)
    move.l  a6,-(sp)            ; Push library base (parameter 1)
    move.l  d0,-(sp)            ; Push version (parameter 2)
    jsr     _novus_LibOpen      ; Call Novus function
    addq.l  #8,sp               ; Clean up 2 parameters (2 * 4 = 8 bytes)
    movem.l (sp)+,d2-d7/a2-a6   ; Restore registers
    rts                         ; Return to caller (result in D0)

; ============================================================================
; LibClose Wrapper
; ============================================================================
; AmigaOS convention: LibClose(A6=base)
; C convention: LibClose(base)
;
; TRANSLATION:
; A6 (base) -> stack parameter 1

_LibClose:
    movem.l d2-d7/a2-a6,-(sp)   ; Save registers
    move.l  a6,-(sp)            ; Push library base (parameter 1)
    jsr     _novus_LibClose     ; Call Novus function
    addq.l  #4,sp               ; Clean up 1 parameter (1 * 4 = 4 bytes)
    movem.l (sp)+,d2-d7/a2-a6   ; Restore registers
    rts                         ; Return to caller (result in D0)

; ============================================================================
; LibExpunge Wrapper
; ============================================================================
; AmigaOS convention: LibExpunge(A6=base)
; C convention: LibExpunge(base)
;
; TRANSLATION:
; A6 (base) -> stack parameter 1

_LibExpunge:
    movem.l d2-d7/a2-a6,-(sp)   ; Save registers
    move.l  a6,-(sp)            ; Push library base (parameter 1)
    jsr     _novus_LibExpunge   ; Call Novus function
    addq.l  #4,sp               ; Clean up 1 parameter
    movem.l (sp)+,d2-d7/a2-a6   ; Restore registers
    rts                         ; Return to caller (result in D0)

; ============================================================================
; LibReserved Wrapper
; ============================================================================
; AmigaOS convention: LibReserved(A6=base)
; C convention: LibReserved(base)
;
; TRANSLATION:
; A6 (base) -> stack parameter 1

_LibReserved:
    movem.l d2-d7/a2-a6,-(sp)   ; Save registers
    move.l  a6,-(sp)            ; Push library base (parameter 1)
    jsr     _novus_LibReserved  ; Call Novus function
    addq.l  #4,sp               ; Clean up 1 parameter
    movem.l (sp)+,d2-d7/a2-a6   ; Restore registers
    rts                         ; Return to caller (result in D0)

; ============================================================================
; GetVersion Wrapper
; ============================================================================
; AmigaOS convention: GetVersion(A6=base)
; C convention: GetVersion(base)
;
; TRANSLATION:
; A6 (base) -> stack parameter 1
;
; USER CALL EXAMPLE:
; move.l  ExampleBase,a6  ; Load library base into A6
; jsr     -30(a6)         ; Call GetVersion (vector offset -30)
; ; Result in D0

_GetVersion:
    movem.l d2-d7/a2-a6,-(sp)   ; Save registers
    move.l  a6,-(sp)            ; Push library base (parameter 1)
    jsr     _novus_GetVersion   ; Call Novus function
    addq.l  #4,sp               ; Clean up 1 parameter
    movem.l (sp)+,d2-d7/a2-a6   ; Restore registers
    rts                         ; Return to caller (result in D0)

; ============================================================================
; Multiply Wrapper
; ============================================================================
; AmigaOS convention: Multiply(D0=a, D1=b, A6=base)
; C convention: Multiply(base, a, b)
;
; TRANSLATION:
; A6 (base) -> stack parameter 1
; D0 (a) -> stack parameter 2
; D1 (b) -> stack parameter 3
;
; NOTE: Parameter order is reversed! AmigaOS passes parameters in
; registers D0, D1, etc., but C expects them on the stack in
; reverse order (right-to-left push).
;
; USER CALL EXAMPLE:
; move.l  #7,d0           ; First parameter (a)
; move.l  #6,d1           ; Second parameter (b)
; move.l  ExampleBase,a6  ; Load library base into A6
; jsr     -36(a6)         ; Call Multiply (vector offset -36)
; ; Result in D0 (should be 42)

_Multiply:
    movem.l d2-d7/a2-a6,-(sp)   ; Save registers
    move.l  a6,-(sp)            ; Push library base (parameter 1)
    move.l  d0,-(sp)            ; Push a (parameter 2)
    move.l  d1,-(sp)            ; Push b (parameter 3)
    jsr     _novus_Multiply     ; Call Novus function
    lea     12(sp),sp           ; Clean up 3 parameters (3 * 4 = 12 bytes)
    movem.l (sp)+,d2-d7/a2-a6   ; Restore registers
    rts                         ; Return to caller (result in D0)

; ============================================================================
; Adding New Function Wrappers
; ============================================================================
; To add a new function wrapper:
;
; 1. Declare exports/imports at top of file:
;    XDEF    _YourFunction
;    XREF    _novus_YourFunction
;
; 2. Write the wrapper (copy one above as template)
;
; 3. Follow the parameter translation pattern:
;    - Count your parameters
;    - Push A6 (base) first
;    - Push other parameters in order (D0, D1, A0, A1, stack...)
;    - Call _novus_YourFunction
;    - Clean up: lea (param_count * 4)(sp),sp
;
; EXAMPLE: Add(D0=a, D1=b, A6=base) -> i32
;
; _Add:
;     movem.l d2-d7/a2-a6,-(sp)   ; Save registers
;     move.l  a6,-(sp)            ; Push base
;     move.l  d0,-(sp)            ; Push a
;     move.l  d1,-(sp)            ; Push b
;     jsr     _novus_Add          ; Call Novus function
;     lea     12(sp),sp           ; Clean up 3 params
;     movem.l (sp)+,d2-d7/a2-a6   ; Restore registers
;     rts                         ; Return

    END
