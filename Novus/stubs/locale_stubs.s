; Generated from SFD file by Novus SFD Parser
; Library: locale.library
; Base: _LocaleBase
; Each function is in its own section for dead code elimination

	xref	_LocaleBase

	section	_CloseCatalog_stub,code

; VOID CloseCatalog(struct Catalog * catalog)
	xdef	_CloseCatalog
_CloseCatalog:
	movea.l	4(sp),a0
	movea.l	_LocaleBase,a6
	jsr	-36(a6)
	rts

	section	_CloseLocale_stub,code

; VOID CloseLocale(struct Locale * locale)
	xdef	_CloseLocale
_CloseLocale:
	movea.l	4(sp),a0
	movea.l	_LocaleBase,a6
	jsr	-42(a6)
	rts

	section	_ConvToLower_stub,code

; ULONG ConvToLower(struct Locale * locale, ULONG character)
	xdef	_ConvToLower
_ConvToLower:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-48(a6)
	rts

	section	_ConvToUpper_stub,code

; ULONG ConvToUpper(struct Locale * locale, ULONG character)
	xdef	_ConvToUpper
_ConvToUpper:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-54(a6)
	rts

	section	_FormatDate_stub,code

; VOID FormatDate(struct Locale * locale, STRPTR fmtTemplate, struct DateStamp * date, struct Hook * putCharFunc)
	xdef	_FormatDate
_FormatDate:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_LocaleBase,a6
	jsr	-60(a6)
	rts

	section	_FormatString_stub,code

; APTR FormatString(struct Locale * locale, STRPTR fmtTemplate, APTR dataStream, struct Hook * putCharFunc)
	xdef	_FormatString
_FormatString:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_LocaleBase,a6
	jsr	-66(a6)
	rts

	section	_GetCatalogStr_stub,code

; STRPTR GetCatalogStr(struct Catalog * catalog, LONG stringNum, STRPTR defaultString)
	xdef	_GetCatalogStr
_GetCatalogStr:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	12(sp),a1
	movea.l	_LocaleBase,a6
	jsr	-72(a6)
	rts

	section	_GetLocaleStr_stub,code

; STRPTR GetLocaleStr(struct Locale * locale, ULONG stringNum)
	xdef	_GetLocaleStr
_GetLocaleStr:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-78(a6)
	rts

	section	_IsAlNum_stub,code

; BOOL IsAlNum(struct Locale * locale, ULONG character)
	xdef	_IsAlNum
_IsAlNum:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-84(a6)
	rts

	section	_IsAlpha_stub,code

; BOOL IsAlpha(struct Locale * locale, ULONG character)
	xdef	_IsAlpha
_IsAlpha:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-90(a6)
	rts

	section	_IsCntrl_stub,code

; BOOL IsCntrl(struct Locale * locale, ULONG character)
	xdef	_IsCntrl
_IsCntrl:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-96(a6)
	rts

	section	_IsDigit_stub,code

; BOOL IsDigit(struct Locale * locale, ULONG character)
	xdef	_IsDigit
_IsDigit:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-102(a6)
	rts

	section	_IsGraph_stub,code

; BOOL IsGraph(struct Locale * locale, ULONG character)
	xdef	_IsGraph
_IsGraph:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-108(a6)
	rts

	section	_IsLower_stub,code

; BOOL IsLower(struct Locale * locale, ULONG character)
	xdef	_IsLower
_IsLower:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-114(a6)
	rts

	section	_IsPrint_stub,code

; BOOL IsPrint(struct Locale * locale, ULONG character)
	xdef	_IsPrint
_IsPrint:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-120(a6)
	rts

	section	_IsPunct_stub,code

; BOOL IsPunct(struct Locale * locale, ULONG character)
	xdef	_IsPunct
_IsPunct:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-126(a6)
	rts

	section	_IsSpace_stub,code

; BOOL IsSpace(struct Locale * locale, ULONG character)
	xdef	_IsSpace
_IsSpace:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-132(a6)
	rts

	section	_IsUpper_stub,code

; BOOL IsUpper(struct Locale * locale, ULONG character)
	xdef	_IsUpper
_IsUpper:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-138(a6)
	rts

	section	_IsXDigit_stub,code

; BOOL IsXDigit(struct Locale * locale, ULONG character)
	xdef	_IsXDigit
_IsXDigit:
	movea.l	4(sp),a0
	move.l	8(sp),d0
	movea.l	_LocaleBase,a6
	jsr	-144(a6)
	rts

	section	_OpenCatalogA_stub,code

; struct Catalog * OpenCatalogA(struct Locale * locale, STRPTR name, struct TagItem * tags)
	xdef	_OpenCatalogA
_OpenCatalogA:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	_LocaleBase,a6
	jsr	-150(a6)
	rts

	section	_OpenLocale_stub,code

; struct Locale * OpenLocale(STRPTR name)
	xdef	_OpenLocale
_OpenLocale:
	movea.l	4(sp),a0
	movea.l	_LocaleBase,a6
	jsr	-162(a6)
	rts

	section	_ParseDate_stub,code

; BOOL ParseDate(struct Locale * locale, struct DateStamp * date, STRPTR fmtTemplate, struct Hook * getCharFunc)
	xdef	_ParseDate
_ParseDate:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	movea.l	16(sp),a3
	movea.l	_LocaleBase,a6
	jsr	-168(a6)
	rts

	section	_StrConvert_stub,code

; ULONG StrConvert(struct Locale * locale, STRPTR string, APTR buffer, ULONG bufferSize, ULONG type)
	xdef	_StrConvert
_StrConvert:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_LocaleBase,a6
	jsr	-180(a6)
	rts

	section	_StrnCmp_stub,code

; LONG StrnCmp(struct Locale * locale, STRPTR string1, STRPTR string2, LONG length, ULONG type)
	xdef	_StrnCmp
_StrnCmp:
	movea.l	4(sp),a0
	movea.l	8(sp),a1
	movea.l	12(sp),a2
	move.l	16(sp),d0
	move.l	20(sp),d1
	movea.l	_LocaleBase,a6
	jsr	-186(a6)
	rts

