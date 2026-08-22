; ptplayer_stubs.asm - Assembly stubs to convert C calling convention to ptplayer's register convention
;
; ptplayer uses register-based calling:
;   a6 = CUSTOM base ($dff000)
;   a0/a1 = pointer parameters
;   d0/d1/d2 = integer parameters
;
; VBCC uses stack-based calling convention.
; These stubs convert between the two.
;
; Symbol naming:
;   - C functions called with double underscore (__novus_mt_xxx) become
;     ___novus_mt_xxx in object files (VBCC adds one underscore)
;   - ptplayer symbols are exported as __mt_xxx (double underscore in asm)

    section "CODE",code

; Import ptplayer functions (ptplayer exports with double underscore)
    xref    __mt_install_cia
    xref    __mt_remove_cia
    xref    __mt_init
    xref    __mt_end
    xref    __mt_music_c
    xref    __mt_playfx
    xref    __mt_loopfx
    xref    __mt_stopfx
    xref    __mt_musicmask
    xref    __mt_mastervol
    xref    __mt_samplevol

; Export our C-callable wrappers (VBCC will add underscore, so use triple)
    xdef    ___novus_mt_install_cia
    xdef    ___novus_mt_remove_cia
    xdef    ___novus_mt_init
    xdef    ___novus_mt_end
    xdef    ___novus_mt_music
    xdef    ___novus_mt_playfx
    xdef    ___novus_mt_loopfx
    xdef    ___novus_mt_stopfx
    xdef    ___novus_mt_musicmask
    xdef    ___novus_mt_mastervol
    xdef    ___novus_mt_samplevol
    xdef    ___novus_get_vbr
    xdef    ___novus_debug_get_attn_flags

; void __novus_mt_install_cia(void* custom, void* vector_base, uint8_t pal_flag)
; Stack: 4(sp)=custom, 8(sp)=vector_base, 12(sp)=pal_flag (byte)
; Registers: a6=custom, a0=vector_base, d0=pal_flag
___novus_mt_install_cia:
    movem.l d2/a2/a6,-(sp)      ; Save callee-saved registers
    move.l  16(sp),a6           ; custom -> a6
    move.l  20(sp),a0           ; vector_base -> a0
    moveq   #0,d0
    move.b  27(sp),d0           ; pal_flag -> d0 (byte at offset 24+3)
    jsr     __mt_install_cia
    movem.l (sp)+,d2/a2/a6      ; Restore registers
    rts

; void __novus_mt_remove_cia(void* custom)
; Stack: 4(sp)=custom
; Registers: a6=custom
___novus_mt_remove_cia:
    movem.l a6,-(sp)
    move.l  8(sp),a6            ; custom -> a6
    jsr     __mt_remove_cia
    movem.l (sp)+,a6
    rts

; void __novus_mt_init(void* custom, void* module, void* samples, uint8_t song_pos)
; Stack: 4(sp)=custom, 8(sp)=module, 12(sp)=samples, 16(sp)=song_pos (byte)
; Registers: a6=custom, a0=module, a1=samples, d0=song_pos
___novus_mt_init:
    movem.l d2/a2/a6,-(sp)
    move.l  16(sp),a6           ; custom -> a6
    move.l  20(sp),a0           ; module -> a0
    move.l  24(sp),a1           ; samples -> a1
    moveq   #0,d0
    move.b  31(sp),d0           ; song_pos -> d0 (byte at offset 28+3)
    jsr     __mt_init
    movem.l (sp)+,d2/a2/a6
    rts

; void __novus_mt_end(void* custom)
; Stack: 4(sp)=custom
; Registers: a6=custom
___novus_mt_end:
    movem.l a6,-(sp)
    move.l  8(sp),a6            ; custom -> a6
    jsr     __mt_end
    movem.l (sp)+,a6
    rts

; void __novus_mt_music(void* custom)
; Stack: 4(sp)=custom
; Registers: a6=custom
___novus_mt_music:
    ; The interrupt entry saves every working register. A normal C caller does
    ; not, so preserve every callee-saved register used by the replayer.
    movem.l d2-d7/a2-a6,-(sp)
    move.l  48(sp),a6           ; custom -> a6
    jsr     __mt_music_c
    movem.l (sp)+,d2-d7/a2-a6
    rts

; void* __novus_mt_playfx(void* custom, void* sfx)
; Stack: 4(sp)=custom, 8(sp)=sfx
; Registers: a6=custom, a0=sfx
; Returns: d0=channel status pointer
___novus_mt_playfx:
    movem.l a2/a6,-(sp)
    move.l  12(sp),a6           ; custom -> a6
    move.l  16(sp),a0           ; sfx -> a0
    jsr     __mt_playfx
    ; Result is already in d0
    movem.l (sp)+,a2/a6
    rts

; void __novus_mt_loopfx(void* custom, void* sfx)
; Stack: 4(sp)=custom, 8(sp)=sfx
; Registers: a6=custom, a0=sfx
___novus_mt_loopfx:
    movem.l a2/a6,-(sp)
    move.l  12(sp),a6           ; custom -> a6
    move.l  16(sp),a0           ; sfx -> a0
    jsr     __mt_loopfx
    movem.l (sp)+,a2/a6
    rts

; void __novus_mt_stopfx(void* custom, uint8_t channel)
; Stack: 4(sp)=custom, 8(sp)=channel (byte)
; Registers: a6=custom, d0=channel
___novus_mt_stopfx:
    movem.l a6,-(sp)
    move.l  8(sp),a6            ; custom -> a6
    moveq   #0,d0
    move.b  15(sp),d0           ; channel -> d0 (byte at offset 12+3)
    jsr     __mt_stopfx
    movem.l (sp)+,a6
    rts

; void __novus_mt_musicmask(void* custom, uint8_t mask)
; Stack: 4(sp)=custom, 8(sp)=mask (byte)
; Registers: a6=custom, d0=mask
___novus_mt_musicmask:
    movem.l a6,-(sp)
    move.l  8(sp),a6            ; custom -> a6
    moveq   #0,d0
    move.b  15(sp),d0           ; mask -> d0 (byte at offset 12+3)
    jsr     __mt_musicmask
    movem.l (sp)+,a6
    rts

; void __novus_mt_mastervol(void* custom, uint16_t volume)
; Stack: 4(sp)=custom, 8(sp)=volume (word)
; Registers: a6=custom, d0=volume
___novus_mt_mastervol:
    movem.l a6,-(sp)
    move.l  8(sp),a6            ; custom -> a6
    moveq   #0,d0
    move.w  14(sp),d0           ; volume -> d0 (word at offset 12+2)
    jsr     __mt_mastervol
    movem.l (sp)+,a6
    rts

; void __novus_mt_samplevol(uint16_t sample_num, uint8_t volume)
; Stack: 4(sp)=sample_num (word), 8(sp)=volume (byte)
; Registers: d0=sample_num, d1=volume
___novus_mt_samplevol:
    moveq   #0,d0
    move.w  6(sp),d0            ; sample_num -> d0 (word at offset 4+2)
    moveq   #0,d1
    move.b  11(sp),d1           ; volume -> d1 (byte at offset 8+3)
    jsr     __mt_samplevol
    rts

; void* __novus_get_vbr(void)
; Returns the CPU's Vector Base Register (VBR)
; On 68000, returns 0 (vectors at address 0)
; On 68010+, returns the VBR value (requires Supervisor mode to read)
;
; We use Exec's Supervisor() function to execute a small routine
; that reads the VBR using the movec instruction.
___novus_get_vbr:
    movem.l a5/a6,-(sp)
    ; Get SysBase from absolute address 4
    move.l  4.w,a6
    ; Check if we have a 68010+ by looking at AttnFlags
    ; AttnFlags is at offset 0x128 in ExecBase
    ; AFB_68010 is bit 0
    move.w  $128(a6),d0
    btst    #0,d0
    beq.s   .no_vbr             ; 68000 has no VBR
    ; We have 68010+, use Supervisor to read VBR
    lea     .read_vbr(pc),a5
    jsr     -30(a6)             ; Supervisor(a5)
    movem.l (sp)+,a5/a6
    rts
.no_vbr:
    moveq   #0,d0               ; Return 0 for 68000
    movem.l (sp)+,a5/a6
    rts

; This code runs in Supervisor mode
; It reads the VBR register and returns it in d0
.read_vbr:
    ; movec vbr,d0 is encoded as: 4e7a0801 (32-bit instruction)
    ; VBCC's vasm supports this with the -m68010 or higher flag
    dc.l    $4e7a0801           ; movec vbr,d0 (32-bit opcode)
    dc.w    $4e73               ; rte

; uint16_t __novus_debug_get_attn_flags(void)
; Returns AttnFlags from ExecBase for debugging
___novus_debug_get_attn_flags:
    move.l  4.w,a0              ; Get SysBase
    moveq   #0,d0
    move.w  $128(a0),d0         ; AttnFlags at offset 0x128
    rts

    end
