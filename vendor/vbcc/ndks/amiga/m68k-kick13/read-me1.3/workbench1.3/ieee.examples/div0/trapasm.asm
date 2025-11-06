
	include	'exec/types.i'
	include	'exec/tasks.i'

	xref	_localf
	xref	_oldtraphandler

*	localf is a table of user trap handlers

	xdef	_trapintercept
_trapintercept:
*	called from exec with the following on the stack
*	(sp).w	= trap#		8
*	(sp).w  = 0
*	(sp).w  = sr		12
*	(sp).l  = pc		14
*	other processor dependant gunk
*	We are in supervisor mode
*	all we want to do is check to see if this trap is one
*	we want to handle, if so, rte to correct handler
*	else let other trap handler deal with it.
	movem.l	d0/a0,-(sp)	; need a data register
*	4(sp) -> ap
	move.l	8(sp),d0	; get trap number
	asl.l	#2,d0		; convert in array index
	lea	_localf,a0
	move.l	0(a0,d0.l),d0	; get alternate usermode vector
	if <>
		move.l	d0,14(sp)	; modify rte address
		movem.l	(sp)+,d0/a0	; restore d0/a0, why?
		addq.l	#4,sp		; remove trap #
		rte			; close eyes, plug ears
	endif
*	take original trap
	movem.l	(sp)+,d0/a0	;restore d0 contents
	move.l	_oldtraphandler,-(sp)
	rts			; jump to it

	end
