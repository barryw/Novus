	IFND	DEVICES_PRTGFX_I
DEVICES_PRTGFX_I	SET	1
**
**	$Filename: devices/prtgfx.i $
**	$Release: 1.3 $
**
**	
**
**	(C) Copyright 1987,1988 Commodore-Amiga, Inc.
**	    All Rights Reserved
**

PCMYELLOW	EQU	0		; byte index for yellow
PCMMAGENTA	EQU	1		; byte index for magenta
PCMCYAN		EQU	2		; byte index for cyan
PCMBLACK	EQU	3		; byte index for black
PCMBLUE		EQU	PCMYELLOW	; byte index for blue
PCMGREEN	EQU	PCMMAGENTA	; byte index for green
PCMRED		EQU	PCMCYAN		; byte index for red
PCMWHITE	EQU	PCMBLACK	; byte index for white

	STRUCTURE	colorEntry,0
		LABEL	colorLong	; quick access to all of YMCB
		LABEL	colorSByte	; 1 entry for each of YMCB
		STRUCT	colorByte,4	; ditto (except signed)
		LABEL	ce_SIZEOF
		
	STRUCTURE	PrtInfo,0
		APTR	pi_render	; ptr to render function
		APTR	pi_rp		; source rastport
		APTR	pi_temprp	; temp 1 line high rastport
		APTR	pi_RowBuf	; array of color codes
		APTR	pi_HamBuf	; array of HAM color codes
		APTR	pi_ColorMap	; rastport colormap in YMCB values
		APTR	pi_ColorInt	; color intensities for entire row
		APTR	pi_HamInt	; HAM color intensities for entire row
		APTR	pi_Dest1Int	; color intensities for dest1 row
		APTR	pi_Dest2Int	; color intensities for dest2 row
		APTR	pi_ScaleX	; array of scale values for X
		APTR	pi_ScaleXAlt	; alt array of scale values for X
		APTR	pi_dmatrix	; ptr to dither matrix
		APTR	pi_TopBuf	; color codes for line above us
		APTR	pi_BotBuf	; color codes for line below us

		UWORD	pi_RowBufSize	; size of RowBuf array
		UWORD	pi_HamBufSize	; size of HamBuf array
		UWORD	pi_ColorMapSize ; size of ColorMap array
		UWORD	pi_ColorIntSize ; size of Color Intensities array
		UWORD	pi_HamIntSize	; size of Ham Intensities array
		UWORD	pi_Dest1IntSize ; size of Dest1Int array
		UWORD	pi_Dest2IntSize ; size of Dest2Int array
		UWORD	pi_ScaleXSize	; size of ScaleX array
		UWORD	pi_ScaleXAltSize ; size of ScaleXAlt array

		UWORD	pi_PrefsFlags	; copy of Preferences 'PrintFlags'
		ULONG	pi_special	; copy of io_Special bits
		UWORD	pi_xstart	; source x origin to print from
		UWORD	pi_ystart	; source y origin to print from
		UWORD	pi_width	; source width
		UWORD	pi_height	; source height
		ULONG	pi_pc		; destination width
		ULONG	pi_pr		; destination height
		UWORD	pi_ymult	; y multiple (for y scaling)
		UWORD	pi_ymod		; y modulas (for y scaling)
		UWORD	pi_ety		; y error term (for y scaling)
		UWORD	pi_xpos		; offset to start printing picture
		UWORD	pi_threshold	; copy of threshold value (from prefs)
		UWORD	pi_tempwidth	; temp var for x scaling
		UWORD	pi_flags	; internal flags
		LABEL	prtinfo_SIZEOF

	ENDC	; DEVICES_PRTGFX_I
