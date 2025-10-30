; locale library stubs for Novus
; Auto-generated from locale_lib.fd

	xref	_LocaleBase	; Provided by startup.o + -lamiga

	section	"CODE",code

; CloseCatalog(catalog)
	xdef	_CloseCatalog
_CloseCatalog:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; catalog
	move.l	_LocaleBase,a6
	jsr	-36(a6)	; CloseCatalog()
	movem.l	(sp)+,a0/a6
	rts

; CloseLocale(locale)
	xdef	_CloseLocale
_CloseLocale:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; locale
	move.l	_LocaleBase,a6
	jsr	-42(a6)	; CloseLocale()
	movem.l	(sp)+,a0/a6
	rts

; ConvToLower(locale, character)
	xdef	_ConvToLower
_ConvToLower:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-48(a6)	; ConvToLower()
	movem.l	(sp)+,d0/a0/a6
	rts

; ConvToUpper(locale, character)
	xdef	_ConvToUpper
_ConvToUpper:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-54(a6)	; ConvToUpper()
	movem.l	(sp)+,d0/a0/a6
	rts

; FormatDate(locale, fmtTemplate, date, putCharFunc)
	xdef	_FormatDate
_FormatDate:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; locale
	move.l	16(sp),a1	; fmtTemplate
	move.l	20(sp),a2	; date
	move.l	24(sp),a3	; putCharFunc
	move.l	_LocaleBase,a6
	jsr	-60(a6)	; FormatDate()
	movem.l	(sp)+,a0-a3/a6
	rts

; FormatString(locale, fmtTemplate, dataStream, putCharFunc)
	xdef	_FormatString
_FormatString:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; locale
	move.l	16(sp),a1	; fmtTemplate
	move.l	20(sp),a2	; dataStream
	move.l	24(sp),a3	; putCharFunc
	move.l	_LocaleBase,a6
	jsr	-66(a6)	; FormatString()
	movem.l	(sp)+,a0-a3/a6
	rts

; GetCatalogStr(catalog, stringNum, defaultString)
	xdef	_GetCatalogStr
_GetCatalogStr:
	movem.l	d0/a0-a1/a6,-(sp)
	move.l	16(sp),a0	; catalog
	move.l	20(sp),d0	; stringNum
	move.l	24(sp),a1	; defaultString
	move.l	_LocaleBase,a6
	jsr	-72(a6)	; GetCatalogStr()
	movem.l	(sp)+,d0/a0-a1/a6
	rts

; GetLocaleStr(locale, stringNum)
	xdef	_GetLocaleStr
_GetLocaleStr:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; stringNum
	move.l	_LocaleBase,a6
	jsr	-78(a6)	; GetLocaleStr()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsAlNum(locale, character)
	xdef	_IsAlNum
_IsAlNum:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-84(a6)	; IsAlNum()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsAlpha(locale, character)
	xdef	_IsAlpha
_IsAlpha:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-90(a6)	; IsAlpha()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsCntrl(locale, character)
	xdef	_IsCntrl
_IsCntrl:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-96(a6)	; IsCntrl()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsDigit(locale, character)
	xdef	_IsDigit
_IsDigit:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-102(a6)	; IsDigit()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsGraph(locale, character)
	xdef	_IsGraph
_IsGraph:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-108(a6)	; IsGraph()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsLower(locale, character)
	xdef	_IsLower
_IsLower:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-114(a6)	; IsLower()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsPrint(locale, character)
	xdef	_IsPrint
_IsPrint:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-120(a6)	; IsPrint()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsPunct(locale, character)
	xdef	_IsPunct
_IsPunct:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-126(a6)	; IsPunct()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsSpace(locale, character)
	xdef	_IsSpace
_IsSpace:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-132(a6)	; IsSpace()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsUpper(locale, character)
	xdef	_IsUpper
_IsUpper:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-138(a6)	; IsUpper()
	movem.l	(sp)+,d0/a0/a6
	rts

; IsXDigit(locale, character)
	xdef	_IsXDigit
_IsXDigit:
	movem.l	d0/a0/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),d0	; character
	move.l	_LocaleBase,a6
	jsr	-144(a6)	; IsXDigit()
	movem.l	(sp)+,d0/a0/a6
	rts

; OpenCatalogA(locale, name, tags)
	xdef	_OpenCatalogA
_OpenCatalogA:
	movem.l	a0-a2/a6,-(sp)
	move.l	12(sp),a0	; locale
	move.l	16(sp),a1	; name
	move.l	20(sp),a2	; tags
	move.l	_LocaleBase,a6
	jsr	-150(a6)	; OpenCatalogA()
	movem.l	(sp)+,a0-a2/a6
	rts

; OpenLocale(name)
	xdef	_OpenLocale
_OpenLocale:
	movem.l	a0/a6,-(sp)
	move.l	12(sp),a0	; name
	move.l	_LocaleBase,a6
	jsr	-156(a6)	; OpenLocale()
	movem.l	(sp)+,a0/a6
	rts

; ParseDate(locale, date, fmtTemplate, getCharFunc)
	xdef	_ParseDate
_ParseDate:
	movem.l	a0-a3/a6,-(sp)
	move.l	12(sp),a0	; locale
	move.l	16(sp),a1	; date
	move.l	20(sp),a2	; fmtTemplate
	move.l	24(sp),a3	; getCharFunc
	move.l	_LocaleBase,a6
	jsr	-162(a6)	; ParseDate()
	movem.l	(sp)+,a0-a3/a6
	rts

; StrConvert(locale, string, buffer, bufferSize, type)
	xdef	_StrConvert
_StrConvert:
	movem.l	d0-d1/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),a1	; string
	move.l	24(sp),a2	; buffer
	move.l	28(sp),d0	; bufferSize
	move.l	32(sp),d1	; type
	move.l	_LocaleBase,a6
	jsr	-174(a6)	; StrConvert()
	movem.l	(sp)+,d0-d1/a0-a2/a6
	rts

; StrnCmp(locale, string1, string2, length, type)
	xdef	_StrnCmp
_StrnCmp:
	movem.l	d0-d1/a0-a2/a6,-(sp)
	move.l	16(sp),a0	; locale
	move.l	20(sp),a1	; string1
	move.l	24(sp),a2	; string2
	move.l	28(sp),d0	; length
	move.l	32(sp),d1	; type
	move.l	_LocaleBase,a6
	jsr	-180(a6)	; StrnCmp()
	movem.l	(sp)+,d0-d1/a0-a2/a6
	rts

