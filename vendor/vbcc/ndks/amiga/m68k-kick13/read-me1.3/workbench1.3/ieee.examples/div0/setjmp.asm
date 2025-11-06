

	XDEF	_setjmp
	XDEF	_longjmp

*
*   setjmp( jmpvec ) -- marks a place in the calling stack than may be
*	later longjmp'ed to.  Will return 0 if it is the initial setjmp,
*	and the longjmp code if it is longjmp'ed to.
*
*   longjmp( jmpvec, code ) -- jumps to the saved jmpvec, passing the
*	code along.
*
*   jmpvec should be an array of 13 longs.  The first element is the
*	saved return pc.  The others are the saved registers.
*
_setjmp:
	move.l	4(a7),a0
	movem.l	d2-d7/a2-a7,4(a0)
	move.l	(a7),(a0)
	moveq	#0,d0
	rts

_longjmp:
	move.l	4(a7),a0
	move.l	8(a7),d0
	movem.l	4(a0),d2-d7/a2-a7
	move.l	(a0),(a7)
	rts

	END
