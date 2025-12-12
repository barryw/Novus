; ============================================================================
; ReAction Class Library Initialization for Novus
; ============================================================================
; Provides automatic ReAction class library opening/closing for Novus programs.
;
; These are BOOPSI class libraries (*.class/gadget/image) that provide
; ReAction GUI toolkit functionality. They require AmigaOS 3.5+.
;
; Required by: ReAction builder API (std/ui/reaction.novus)
; ============================================================================

	section	"CODE",code

; ============================================================================
; External References
; ============================================================================
	xref	_WindowBase		; Provided by library_bases.s
	xref	_LayoutBase		; Provided by library_bases.s
	xref	_ButtonBase		; Provided by library_bases.s
	xref	_CheckBoxBase		; Provided by library_bases.s
	xref	_IntegerBase		; Provided by library_bases.s
	xref	_RadioButtonBase	; Provided by library_bases.s
	xref	_LabelBase		; Provided by library_bases.s
	xref	_SysBase		; Provided by library_bases.s

; ============================================================================
; Exports
; ============================================================================
	xdef	___reaction_init	; Initialize all ReAction class libraries
	xdef	___reaction_cleanup	; Cleanup all ReAction class libraries
	xdef	___reaction_ensure	; Ensure ReAction libraries are open

; ============================================================================
; Data Section - Library/Class Names
; ============================================================================
	section	data,data

window_class_name:
	dc.b	'window.class',0
	even
layout_gadget_name:
	dc.b	'gadgets/layout.gadget',0
	even
button_gadget_name:
	dc.b	'gadgets/button.gadget',0
	even
checkbox_gadget_name:
	dc.b	'gadgets/checkbox.gadget',0
	even
integer_gadget_name:
	dc.b	'gadgets/integer.gadget',0
	even
radiobutton_gadget_name:
	dc.b	'gadgets/radiobutton.gadget',0
	even
label_image_name:
	dc.b	'images/label.image',0
	even

REACTION_VERSION	equ	44	; ReAction requires OS 3.5+ (V44)

; ============================================================================
; Code Section
; ============================================================================
	section	"CODE",code

; ----------------------------------------------------------------------------
; ___reaction_init - Initialize all ReAction class libraries
; ----------------------------------------------------------------------------
; Opens all ReAction class libraries and stores their bases.
;
; Input:  None
; Output: D0 = 0 if any failed, non-zero if all succeeded
; Modifies: D0 (all other registers preserved)
; ----------------------------------------------------------------------------
___reaction_init:
	movem.l	d1-d2/a0-a1/a6,-(sp)

	; Get SysBase
	move.l	_SysBase,d0
	bne.s	.sysbase_ok
	move.l	4.w,a6
	move.l	a6,_SysBase
	bra.s	.open_window
.sysbase_ok:
	move.l	d0,a6

	; Open window.class
.open_window:
	move.l	_WindowBase,d0
	bne.s	.open_layout
	lea	window_class_name(pc),a1
	moveq	#REACTION_VERSION,d0
	jsr	-552(a6)		; OpenLibrary
	move.l	d0,_WindowBase
	beq	.fail

	; Open layout.gadget
.open_layout:
	move.l	_LayoutBase,d0
	bne.s	.open_button
	lea	layout_gadget_name(pc),a1
	moveq	#REACTION_VERSION,d0
	jsr	-552(a6)		; OpenLibrary
	move.l	d0,_LayoutBase
	beq	.fail

	; Open button.gadget
.open_button:
	move.l	_ButtonBase,d0
	bne.s	.open_checkbox
	lea	button_gadget_name(pc),a1
	moveq	#REACTION_VERSION,d0
	jsr	-552(a6)		; OpenLibrary
	move.l	d0,_ButtonBase
	beq	.fail

	; Open checkbox.gadget
.open_checkbox:
	move.l	_CheckBoxBase,d0
	bne.s	.open_integer
	lea	checkbox_gadget_name(pc),a1
	moveq	#REACTION_VERSION,d0
	jsr	-552(a6)		; OpenLibrary
	move.l	d0,_CheckBoxBase
	beq	.fail

	; Open integer.gadget
.open_integer:
	move.l	_IntegerBase,d0
	bne.s	.open_radiobutton
	lea	integer_gadget_name(pc),a1
	moveq	#REACTION_VERSION,d0
	jsr	-552(a6)		; OpenLibrary
	move.l	d0,_IntegerBase
	beq	.fail

	; Open radiobutton.gadget
.open_radiobutton:
	move.l	_RadioButtonBase,d0
	bne.s	.open_label
	lea	radiobutton_gadget_name(pc),a1
	moveq	#REACTION_VERSION,d0
	jsr	-552(a6)		; OpenLibrary
	move.l	d0,_RadioButtonBase
	beq	.fail

	; Open label.image
.open_label:
	move.l	_LabelBase,d0
	bne.s	.success
	lea	label_image_name(pc),a1
	moveq	#REACTION_VERSION,d0
	jsr	-552(a6)		; OpenLibrary
	move.l	d0,_LabelBase
	beq.s	.fail

.success:
	moveq	#1,d0			; Return success
	bra.s	.done

.fail:
	moveq	#0,d0			; Return failure

.done:
	movem.l	(sp)+,d1-d2/a0-a1/a6
	rts

; ----------------------------------------------------------------------------
; ___reaction_cleanup - Close all ReAction class libraries
; ----------------------------------------------------------------------------
; Closes all ReAction class libraries that were opened.
;
; Input:  None
; Output: None
; Modifies: None (all registers preserved)
; ----------------------------------------------------------------------------
___reaction_cleanup:
	movem.l	d0-d1/a0-a1/a6,-(sp)

	; Get SysBase
	move.l	4.w,a6

	; Close label.image
	move.l	_LabelBase,d0
	beq.s	.close_radiobutton
	move.l	d0,a1
	jsr	-414(a6)		; CloseLibrary
	clr.l	_LabelBase

	; Close radiobutton.gadget
.close_radiobutton:
	move.l	_RadioButtonBase,d0
	beq.s	.close_integer
	move.l	d0,a1
	jsr	-414(a6)		; CloseLibrary
	clr.l	_RadioButtonBase

	; Close integer.gadget
.close_integer:
	move.l	_IntegerBase,d0
	beq.s	.close_checkbox
	move.l	d0,a1
	jsr	-414(a6)		; CloseLibrary
	clr.l	_IntegerBase

	; Close checkbox.gadget
.close_checkbox:
	move.l	_CheckBoxBase,d0
	beq.s	.close_button
	move.l	d0,a1
	jsr	-414(a6)		; CloseLibrary
	clr.l	_CheckBoxBase

	; Close button.gadget
.close_button:
	move.l	_ButtonBase,d0
	beq.s	.close_layout
	move.l	d0,a1
	jsr	-414(a6)		; CloseLibrary
	clr.l	_ButtonBase

	; Close layout.gadget
.close_layout:
	move.l	_LayoutBase,d0
	beq.s	.close_window
	move.l	d0,a1
	jsr	-414(a6)		; CloseLibrary
	clr.l	_LayoutBase

	; Close window.class
.close_window:
	move.l	_WindowBase,d0
	beq.s	.done
	move.l	d0,a1
	jsr	-414(a6)		; CloseLibrary
	clr.l	_WindowBase

.done:
	movem.l	(sp)+,d0-d1/a0-a1/a6
	rts

; ----------------------------------------------------------------------------
; ___reaction_ensure - Ensure all ReAction libraries are open (lazy init)
; ----------------------------------------------------------------------------
; Fast check if libraries are open, initializes if not. Returns success/fail.
;
; Input:  None
; Output: D0 = 0 if failed, non-zero if all libraries open
; Modifies: D0 only (all other registers preserved)
; ----------------------------------------------------------------------------
___reaction_ensure:
	; Quick check - if WindowBase is set, assume all are open
	move.l	_WindowBase,d0
	bne.s	.done

	; Need to initialize
	movem.l	d1-d2/a0-a1/a6,-(sp)
	bsr	___reaction_init
	movem.l	(sp)+,d1-d2/a0-a1/a6
.done:
	rts

	end
