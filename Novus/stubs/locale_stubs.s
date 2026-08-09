; Generated from SFD file by Novus SFD Parser
; Library: locale.library
; Base: _LocaleBase
; Each function is in its own section for dead code elimination

	xref	_LocaleBase

	section	_CloseCatalog_stub,code

; VOID CloseCatalog(struct Catalog * catalog)
	xdef	_CloseCatalog
_CloseCatalog:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LocaleBase,a6
	jsr	-36(a6)
	movem.l	(sp)+,a6
	rts

	section	_CloseLocale_stub,code

; VOID CloseLocale(struct Locale * locale)
	xdef	_CloseLocale
_CloseLocale:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LocaleBase,a6
	jsr	-42(a6)
	movem.l	(sp)+,a6
	rts

	section	_ConvToLower_stub,code

; ULONG ConvToLower(struct Locale * locale, ULONG character)
	xdef	_ConvToLower
_ConvToLower:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-48(a6)
	movem.l	(sp)+,a6
	rts

	section	_ConvToUpper_stub,code

; ULONG ConvToUpper(struct Locale * locale, ULONG character)
	xdef	_ConvToUpper
_ConvToUpper:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-54(a6)
	movem.l	(sp)+,a6
	rts

	section	_FormatDate_stub,code

; VOID FormatDate(struct Locale * locale, STRPTR fmtTemplate, struct DateStamp * date, struct Hook * putCharFunc)
	xdef	_FormatDate
_FormatDate:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_LocaleBase,a6
	jsr	-60(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_FormatString_stub,code

; APTR FormatString(struct Locale * locale, STRPTR fmtTemplate, APTR dataStream, struct Hook * putCharFunc)
	xdef	_FormatString
_FormatString:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_LocaleBase,a6
	jsr	-66(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_GetCatalogStr_stub,code

; STRPTR GetCatalogStr(struct Catalog * catalog, LONG stringNum, STRPTR defaultString)
	xdef	_GetCatalogStr
_GetCatalogStr:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	16(sp),a1
	movea.l	_LocaleBase,a6
	jsr	-72(a6)
	movem.l	(sp)+,a6
	rts

	section	_GetLocaleStr_stub,code

; STRPTR GetLocaleStr(struct Locale * locale, ULONG stringNum)
	xdef	_GetLocaleStr
_GetLocaleStr:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-78(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsAlNum_stub,code

; BOOL IsAlNum(struct Locale * locale, ULONG character)
	xdef	_IsAlNum
_IsAlNum:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-84(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsAlpha_stub,code

; BOOL IsAlpha(struct Locale * locale, ULONG character)
	xdef	_IsAlpha
_IsAlpha:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-90(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsCntrl_stub,code

; BOOL IsCntrl(struct Locale * locale, ULONG character)
	xdef	_IsCntrl
_IsCntrl:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-96(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsDigit_stub,code

; BOOL IsDigit(struct Locale * locale, ULONG character)
	xdef	_IsDigit
_IsDigit:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-102(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsGraph_stub,code

; BOOL IsGraph(struct Locale * locale, ULONG character)
	xdef	_IsGraph
_IsGraph:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-108(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsLower_stub,code

; BOOL IsLower(struct Locale * locale, ULONG character)
	xdef	_IsLower
_IsLower:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-114(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsPrint_stub,code

; BOOL IsPrint(struct Locale * locale, ULONG character)
	xdef	_IsPrint
_IsPrint:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-120(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsPunct_stub,code

; BOOL IsPunct(struct Locale * locale, ULONG character)
	xdef	_IsPunct
_IsPunct:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-126(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsSpace_stub,code

; BOOL IsSpace(struct Locale * locale, ULONG character)
	xdef	_IsSpace
_IsSpace:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-132(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsUpper_stub,code

; BOOL IsUpper(struct Locale * locale, ULONG character)
	xdef	_IsUpper
_IsUpper:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-138(a6)
	movem.l	(sp)+,a6
	rts

	section	_IsXDigit_stub,code

; BOOL IsXDigit(struct Locale * locale, ULONG character)
	xdef	_IsXDigit
_IsXDigit:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	move.l	12(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-144(a6)
	movem.l	(sp)+,a6
	rts

	section	_OpenCatalogA_stub,code

; struct Catalog * OpenCatalogA(struct Locale * locale, STRPTR name, struct TagItem * tags)
	xdef	_OpenCatalogA
_OpenCatalogA:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	movea.l	_LocaleBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_OpenCatalog_stub,code

; struct Catalog * OpenCatalog(struct Locale * locale, STRPTR name, Tag tags, ... )
	xdef	_OpenCatalog
_OpenCatalog:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	lea	20(sp),a2
	movea.l	_LocaleBase,a6
	jsr	-150(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_OpenLocale_stub,code

; struct Locale * OpenLocale(STRPTR name)
	xdef	_OpenLocale
_OpenLocale:
	movem.l	a6,-(sp)
	movea.l	8(sp),a0
	movea.l	_LocaleBase,a6
	jsr	-156(a6)
	movem.l	(sp)+,a6
	rts

	section	_ParseDate_stub,code

; BOOL ParseDate(struct Locale * locale, struct DateStamp * date, STRPTR fmtTemplate, struct Hook * getCharFunc)
	xdef	_ParseDate
_ParseDate:
	movem.l	a2/a3/a6,-(sp)
	movea.l	16(sp),a0
	movea.l	20(sp),a1
	movea.l	24(sp),a2
	movea.l	28(sp),a3
	movea.l	_LocaleBase,a6
	jsr	-162(a6)
	movem.l	(sp)+,a2/a3/a6
	rts

	section	_StrConvert_stub,code

; ULONG StrConvert(struct Locale * locale, STRPTR string, APTR buffer, ULONG bufferSize, ULONG type)
	xdef	_StrConvert
_StrConvert:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	move.l	24(sp),d0
	move.l	28(sp),d1
	movea.l	_LocaleBase,a6
	jsr	-174(a6)
	movem.l	(sp)+,a2/a6
	rts

	section	_StrnCmp_stub,code

; LONG StrnCmp(struct Locale * locale, STRPTR string1, STRPTR string2, LONG length, ULONG type)
	xdef	_StrnCmp
_StrnCmp:
	movem.l	a2/a6,-(sp)
	movea.l	12(sp),a0
	movea.l	16(sp),a1
	movea.l	20(sp),a2
	move.l	24(sp),d0
	move.l	28(sp),d1
	movea.l	_LocaleBase,a6
	jsr	-180(a6)
	movem.l	(sp)+,a2/a6
	rts

