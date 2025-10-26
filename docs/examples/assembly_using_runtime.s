; Example: Assembly code using Novus runtime library
; This demonstrates how assembly programmers can use
; the optimized primitives without worrying about CPU detection
;
; To build:
;   1. Compile any Novus program to generate the runtime library:
;      novus dummy.novus -o runtime.s --emit-asm
;   2. Assemble this file:
;      vasmm68k_mot -Fhunk -o example.o assembly_using_runtime.s
;   3. Assemble the runtime:
;      vasmm68k_mot -Fhunk -o runtime.o runtime.s
;   4. Link together:
;      vlink -bamigahunk -o example example.o runtime.o -lvc

	section	text,code

; Import Novus runtime primitives
	xref	__mul_i32	; d0 * d1 -> d0
	xref	__mul_u32	; d0 * d1 -> d0 (unsigned)
	xref	__div_u32	; d0 / d1 -> d0 (unsigned)
	xref	__shl_i32	; d0 << d1 -> d0
	xref	__shr_i32	; d0 >> d1 -> d0 (signed)
	xref	__shr_u32	; d0 >> d1 -> d0 (unsigned)

; Example 1: Calculate area
; area = width * height
	xdef	_calculate_area
_calculate_area:
	link	a6,#0

	move.l	8(a6),d0	; Load width
	move.l	12(a6),d1	; Load height
	jsr	__mul_i32	; d0 = width * height (auto-optimized!)

	unlk	a6
	rts

; Example 2: Calculate average
; average = total / count
	xdef	_calculate_average
_calculate_average:
	link	a6,#0

	move.l	8(a6),d0	; Load total
	move.l	12(a6),d1	; Load count
	jsr	__div_u32	; d0 = total / count (auto-optimized!)

	unlk	a6
	rts

; Example 3: Complex calculation
; result = ((x * 10) + y) >> 2
	xdef	_complex_calculation
_complex_calculation:
	link	a6,#0

	; Multiply x by 10
	move.l	8(a6),d0	; x
	moveq	#10,d1
	jsr	__mul_i32	; d0 = x * 10

	; Add y
	add.l	12(a6),d0	; d0 = (x * 10) + y

	; Shift right by 2 (divide by 4)
	moveq	#2,d1
	jsr	__shr_i32	; d0 = result >> 2

	unlk	a6
	rts

; Example 4: Bit manipulation
; Multiply by 16 using shift (x << 4)
; Then multiply result by 3
	xdef	_bit_manipulation
_bit_manipulation:
	link	a6,#0

	move.l	8(a6),d0	; Load parameter
	moveq	#4,d1
	jsr	__shl_i32	; d0 = d0 << 4 (uses barrel shifter on 68020+!)

	moveq	#3,d1
	jsr	__mul_i32	; d0 = d0 * 3 (uses shift/add on 68060!)

	unlk	a6
	rts

; Example 5: Array indexing
; Calculate byte offset: index * element_size
; Then add to base address
	xdef	_array_access
_array_access:
	link	a6,#0

	; Calculate offset = index * size
	move.l	8(a6),d0	; index
	move.l	12(a6),d1	; element_size
	jsr	__mul_u32	; d0 = offset

	; Add base address
	move.l	16(a6),a0	; base_address
	lea	(a0,d0.l),a0	; a0 = base + offset

	; Return pointer in d0 (Amiga convention)
	move.l	a0,d0

	unlk	a6
	rts
