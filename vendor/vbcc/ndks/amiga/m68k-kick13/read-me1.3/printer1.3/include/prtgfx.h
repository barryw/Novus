/********************************************************************/
/*               Commodore-Amiga, Inc.                              */
/*               prtgfx.h                                           */
/********************************************************************/

#ifndef  DEVICES_PRTGFX_H
#define  DEVICES_PRTGFX_H

#define	PCMYELLOW	0		/* byte index for yellow */
#define	PCMMAGENTA	1		/* byte index for magenta */
#define	PCMCYAN		2		/* byte index for cyan */
#define	PCMBLACK	3		/* byte index for black */
#define PCMBLUE		PCMYELLOW	/* byte index for blue */
#define PCMGREEN	PCMMAGENTA	/* byte index for green */
#define PCMRED		PCMCYAN		/* byte index for red */
#define PCMWHITE	PCMBLACK	/* byte index for white */

union colorEntry {
	ULONG colorLong;	/* quick access to all of YMCB */
	UBYTE colorByte[4];	/* 1 entry for each of YMCB */
	BYTE colorSByte[4];	/* ditto (except signed) */
};

struct PrtInfo { /* printer info */
	int (*pi_render)();		/* ptr to render function */
	struct RastPort *pi_rp;		/* source rastport */
	struct RastPort *pi_temprp;	/* temp 1-line high rastport */
	UWORD *pi_RowBuf;		/* array of color codes */
	UWORD *pi_HamBuf;		/* array of HAM color codes */
	union colorEntry *pi_ColorMap;	/* rastport colormap in YMCB values */
	union colorEntry *pi_ColorInt;	/* color intensities for entire row */
	union colorEntry *pi_HamInt;	/* HAM color ints for entire row */
	union colorEntry *pi_Dest1Int;	/* color intensities for dest1 row */
	union colorEntry *pi_Dest2Int;	/* color intensities for dest2 row */
	UWORD *pi_ScaleX;		/* array of scale values for x */
	UWORD *pi_ScaleXAlt;		/* alt array of scale values for x */
	UBYTE *pi_dmatrix;		/* ptr to dither matrix */
	UWORD *pi_TopBuf;		/* color codes for line above us */
	UWORD *pi_BotBuf;		/* color codes for line below us */

	UWORD pi_RowBufSize;		/* size of RowBuf array */
	UWORD pi_HamBufSize;		/* size of HamBuf array */
	UWORD pi_ColorMapSize;		/* size of ColorMap array */
	UWORD pi_ColorIntSize;		/* size of Color Intensities array */
	UWORD pi_HamIntSize;		/* size of Ham Intensities array */
	UWORD pi_Dest1IntSize;		/* size of Dest1Int array */
	UWORD pi_Dest2IntSize;		/* size of Dest2Int array */
	UWORD pi_ScaleXSize;		/* size of ScaleX array */
	UWORD pi_ScaleXAltSize;		/* size of ScaleXAlt array */

	UWORD pi_PrefsFlags;		/* copy of Preferences 'PrintFlags' */
	ULONG pi_special;		/* copy of io_Special bits */
	UWORD pi_xstart;		/* source x origin to print from */
	UWORD pi_ystart;		/* source y origin to print from */
	UWORD pi_width;			/* source width */
	UWORD pi_height;		/* source height */
	ULONG pi_pc;			/* dest width (Print Cols) */
	ULONG pi_pr;			/* dest height (Print Rows) */
	UWORD pi_ymult;			/* y multiple (used for y scaling) */
	UWORD pi_ymod;			/* y modulas (used for y scaling) */
	WORD pi_ety;			/* y error term (for y scaling) */
	UWORD pi_xpos;			/* left offset print position */
	UWORD pi_threshold;		/* threshold value (from prefs) */
	UWORD pi_tempwidth;		/* temp var for width */
	UWORD pi_flags;			/* internal flags */
};
#endif  DEVICES_PRTGFX_H
