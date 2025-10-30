; string_utils.s - String utility functions for Novus runtime
; Provides strlen and other string operations

	section	"CODE",code

; strlen(str) - Calculate length of null-terminated string
; Args: str in d0 (or stack)
; Returns: length in d0
	xdef	_strlen
_strlen:
	move.l	4(sp),a0		; Get string pointer from stack
	moveq	#0,d0			; Length counter = 0
.loop:
	tst.b	(a0)+			; Test byte and increment
	beq.s	.done			; If zero, we're done
	addq.l	#1,d0			; Increment counter
	bra.s	.loop			; Keep looping
.done:
	rts
