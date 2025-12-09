#ifndef _INLINE_LOCALE_H
#define _INLINE_LOCALE_H

#ifndef CLIB_LOCALE_PROTOS_H
#define CLIB_LOCALE_PROTOS_H
#endif

#ifndef  LIBRARIES_LOCALE_H
#include <libraries/locale.h>
#endif
#ifndef  DOS_DOS_H
#include <dos/dos.h>
#endif
#ifndef  UTILITY_HOOKS_H
#include <utility/hooks.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  REXX_STORAGE_H
#include <rexx/storage.h>
#endif

#ifndef LOCALE_BASE_NAME
#define LOCALE_BASE_NAME LocaleBase
#endif

#define CloseCatalog(catalog) ({ \
  struct Catalog * _CloseCatalog_catalog = (catalog); \
  ({ \
  register char * _CloseCatalog__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Catalog * __asm("a0"))) \
  (_CloseCatalog__bn - 36))(_CloseCatalog__bn, _CloseCatalog_catalog); \
});})

#define CloseLocale(locale) ({ \
  struct Locale * _CloseLocale_locale = (locale); \
  ({ \
  register char * _CloseLocale__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Locale * __asm("a0"))) \
  (_CloseLocale__bn - 42))(_CloseLocale__bn, _CloseLocale_locale); \
});})

#define ConvToLower(locale, character) ({ \
  struct Locale * _ConvToLower_locale = (locale); \
  ULONG _ConvToLower_character = (character); \
  ({ \
  register char * _ConvToLower__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_ConvToLower__bn - 48))(_ConvToLower__bn, _ConvToLower_locale, _ConvToLower_character); \
});})

#define ConvToUpper(locale, character) ({ \
  struct Locale * _ConvToUpper_locale = (locale); \
  ULONG _ConvToUpper_character = (character); \
  ({ \
  register char * _ConvToUpper__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_ConvToUpper__bn - 54))(_ConvToUpper__bn, _ConvToUpper_locale, _ConvToUpper_character); \
});})

#define FormatDate(locale, fmtTemplate, date, putCharFunc) ({ \
  struct Locale * _FormatDate_locale = (locale); \
  CONST_STRPTR _FormatDate_fmtTemplate = (fmtTemplate); \
  CONST struct DateStamp * _FormatDate_date = (date); \
  struct Hook * _FormatDate_putCharFunc = (putCharFunc); \
  ({ \
  register char * _FormatDate__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct Locale * __asm("a0"), CONST_STRPTR __asm("a1"), CONST struct DateStamp * __asm("a2"), struct Hook * __asm("a3"))) \
  (_FormatDate__bn - 60))(_FormatDate__bn, _FormatDate_locale, _FormatDate_fmtTemplate, _FormatDate_date, _FormatDate_putCharFunc); \
});})

#define FormatString(locale, fmtTemplate, dataStream, putCharFunc) ({ \
  struct Locale * _FormatString_locale = (locale); \
  CONST_STRPTR _FormatString_fmtTemplate = (fmtTemplate); \
  APTR _FormatString_dataStream = (dataStream); \
  struct Hook * _FormatString_putCharFunc = (putCharFunc); \
  ({ \
  register char * _FormatString__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), struct Locale * __asm("a0"), CONST_STRPTR __asm("a1"), APTR __asm("a2"), struct Hook * __asm("a3"))) \
  (_FormatString__bn - 66))(_FormatString__bn, _FormatString_locale, _FormatString_fmtTemplate, _FormatString_dataStream, _FormatString_putCharFunc); \
});})

#define GetCatalogStr(catalog, stringNum, defaultString) ({ \
  CONST struct Catalog * _GetCatalogStr_catalog = (catalog); \
  LONG _GetCatalogStr_stringNum = (stringNum); \
  CONST_STRPTR _GetCatalogStr_defaultString = (defaultString); \
  ({ \
  register char * _GetCatalogStr__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), CONST struct Catalog * __asm("a0"), LONG __asm("d0"), CONST_STRPTR __asm("a1"))) \
  (_GetCatalogStr__bn - 72))(_GetCatalogStr__bn, _GetCatalogStr_catalog, _GetCatalogStr_stringNum, _GetCatalogStr_defaultString); \
});})

#define GetLocaleStr(locale, stringNum) ({ \
  struct Locale * _GetLocaleStr_locale = (locale); \
  ULONG _GetLocaleStr_stringNum = (stringNum); \
  ({ \
  register char * _GetLocaleStr__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((STRPTR (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_GetLocaleStr__bn - 78))(_GetLocaleStr__bn, _GetLocaleStr_locale, _GetLocaleStr_stringNum); \
});})

#define IsAlNum(locale, character) ({ \
  struct Locale * _IsAlNum_locale = (locale); \
  ULONG _IsAlNum_character = (character); \
  ({ \
  register char * _IsAlNum__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsAlNum__bn - 84))(_IsAlNum__bn, _IsAlNum_locale, _IsAlNum_character); \
});})

#define IsAlpha(locale, character) ({ \
  struct Locale * _IsAlpha_locale = (locale); \
  ULONG _IsAlpha_character = (character); \
  ({ \
  register char * _IsAlpha__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsAlpha__bn - 90))(_IsAlpha__bn, _IsAlpha_locale, _IsAlpha_character); \
});})

#define IsCntrl(locale, character) ({ \
  struct Locale * _IsCntrl_locale = (locale); \
  ULONG _IsCntrl_character = (character); \
  ({ \
  register char * _IsCntrl__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsCntrl__bn - 96))(_IsCntrl__bn, _IsCntrl_locale, _IsCntrl_character); \
});})

#define IsDigit(locale, character) ({ \
  struct Locale * _IsDigit_locale = (locale); \
  ULONG _IsDigit_character = (character); \
  ({ \
  register char * _IsDigit__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsDigit__bn - 102))(_IsDigit__bn, _IsDigit_locale, _IsDigit_character); \
});})

#define IsGraph(locale, character) ({ \
  struct Locale * _IsGraph_locale = (locale); \
  ULONG _IsGraph_character = (character); \
  ({ \
  register char * _IsGraph__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsGraph__bn - 108))(_IsGraph__bn, _IsGraph_locale, _IsGraph_character); \
});})

#define IsLower(locale, character) ({ \
  struct Locale * _IsLower_locale = (locale); \
  ULONG _IsLower_character = (character); \
  ({ \
  register char * _IsLower__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsLower__bn - 114))(_IsLower__bn, _IsLower_locale, _IsLower_character); \
});})

#define IsPrint(locale, character) ({ \
  struct Locale * _IsPrint_locale = (locale); \
  ULONG _IsPrint_character = (character); \
  ({ \
  register char * _IsPrint__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsPrint__bn - 120))(_IsPrint__bn, _IsPrint_locale, _IsPrint_character); \
});})

#define IsPunct(locale, character) ({ \
  struct Locale * _IsPunct_locale = (locale); \
  ULONG _IsPunct_character = (character); \
  ({ \
  register char * _IsPunct__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsPunct__bn - 126))(_IsPunct__bn, _IsPunct_locale, _IsPunct_character); \
});})

#define IsSpace(locale, character) ({ \
  struct Locale * _IsSpace_locale = (locale); \
  ULONG _IsSpace_character = (character); \
  ({ \
  register char * _IsSpace__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsSpace__bn - 132))(_IsSpace__bn, _IsSpace_locale, _IsSpace_character); \
});})

#define IsUpper(locale, character) ({ \
  struct Locale * _IsUpper_locale = (locale); \
  ULONG _IsUpper_character = (character); \
  ({ \
  register char * _IsUpper__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsUpper__bn - 138))(_IsUpper__bn, _IsUpper_locale, _IsUpper_character); \
});})

#define IsXDigit(locale, character) ({ \
  struct Locale * _IsXDigit_locale = (locale); \
  ULONG _IsXDigit_character = (character); \
  ({ \
  register char * _IsXDigit__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct Locale * __asm("a0"), ULONG __asm("d0"))) \
  (_IsXDigit__bn - 144))(_IsXDigit__bn, _IsXDigit_locale, _IsXDigit_character); \
});})

#define OpenCatalogA(locale, name, tags) ({ \
  struct Locale * _OpenCatalogA_locale = (locale); \
  CONST_STRPTR _OpenCatalogA_name = (name); \
  CONST struct TagItem * _OpenCatalogA_tags = (tags); \
  ({ \
  register char * _OpenCatalogA__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((struct Catalog * (*)(char * __asm("a6"), struct Locale * __asm("a0"), CONST_STRPTR __asm("a1"), CONST struct TagItem * __asm("a2"))) \
  (_OpenCatalogA__bn - 150))(_OpenCatalogA__bn, _OpenCatalogA_locale, _OpenCatalogA_name, _OpenCatalogA_tags); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ struct Catalog * ___OpenCatalog(struct LocaleBase * LocaleBase, struct Locale * locale, CONST_STRPTR name, ...)
{
  return OpenCatalogA(locale, name, (CONST struct TagItem *) ((ULONG) &name + sizeof(CONST_STRPTR)));
}

#define OpenCatalog(locale, name...) ___OpenCatalog(LOCALE_BASE_NAME, locale, name)
#endif

#define OpenLocale(name) ({ \
  CONST_STRPTR _OpenLocale_name = (name); \
  ({ \
  register char * _OpenLocale__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((struct Locale * (*)(char * __asm("a6"), CONST_STRPTR __asm("a0"))) \
  (_OpenLocale__bn - 156))(_OpenLocale__bn, _OpenLocale_name); \
});})

#define ParseDate(locale, date, fmtTemplate, getCharFunc) ({ \
  CONST struct Locale * _ParseDate_locale = (locale); \
  struct DateStamp * _ParseDate_date = (date); \
  CONST_STRPTR _ParseDate_fmtTemplate = (fmtTemplate); \
  struct Hook * _ParseDate_getCharFunc = (getCharFunc); \
  ({ \
  register char * _ParseDate__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), CONST struct Locale * __asm("a0"), struct DateStamp * __asm("a1"), CONST_STRPTR __asm("a2"), struct Hook * __asm("a3"))) \
  (_ParseDate__bn - 162))(_ParseDate__bn, _ParseDate_locale, _ParseDate_date, _ParseDate_fmtTemplate, _ParseDate_getCharFunc); \
});})

#define StrConvert(locale, string, buffer, bufferSize, type) ({ \
  struct Locale * _StrConvert_locale = (locale); \
  CONST_STRPTR _StrConvert_string = (string); \
  APTR _StrConvert_buffer = (buffer); \
  ULONG _StrConvert_bufferSize = (bufferSize); \
  ULONG _StrConvert_type = (type); \
  ({ \
  register char * _StrConvert__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct Locale * __asm("a0"), CONST_STRPTR __asm("a1"), APTR __asm("a2"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_StrConvert__bn - 174))(_StrConvert__bn, _StrConvert_locale, _StrConvert_string, _StrConvert_buffer, _StrConvert_bufferSize, _StrConvert_type); \
});})

#define StrnCmp(locale, string1, string2, length, type) ({ \
  struct Locale * _StrnCmp_locale = (locale); \
  CONST_STRPTR _StrnCmp_string1 = (string1); \
  CONST_STRPTR _StrnCmp_string2 = (string2); \
  LONG _StrnCmp_length = (length); \
  ULONG _StrnCmp_type = (type); \
  ({ \
  register char * _StrnCmp__bn __asm("a6") = (char *) (LOCALE_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct Locale * __asm("a0"), CONST_STRPTR __asm("a1"), CONST_STRPTR __asm("a2"), LONG __asm("d0"), ULONG __asm("d1"))) \
  (_StrnCmp__bn - 180))(_StrnCmp__bn, _StrnCmp_locale, _StrnCmp_string1, _StrnCmp_string2, _StrnCmp_length, _StrnCmp_type); \
});})

#endif /*  _INLINE_LOCALE_H  */
