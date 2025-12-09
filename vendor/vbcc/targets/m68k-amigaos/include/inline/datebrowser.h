#ifndef _INLINE_DATEBROWSER_H
#define _INLINE_DATEBROWSER_H

#ifndef CLIB_DATEBROWSER_PROTOS_H
#define CLIB_DATEBROWSER_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  INTUITION_INTUITION_H
#include <intuition/intuition.h>
#endif
#ifndef  INTUITION_CLASSES_H
#include <intuition/classes.h>
#endif
#ifndef  GADGETS_DATEBROWSER_H
#include <gadgets/datebrowser.h>
#endif

#ifndef DATEBROWSER_BASE_NAME
#define DATEBROWSER_BASE_NAME DateBrowserBase
#endif

#define DATEBROWSER_GetClass() ({ \
  register char * _DATEBROWSER_GetClass__bn __asm("a6") = (char *) (DATEBROWSER_BASE_NAME);\
  ((Class * (*)(char * __asm("a6"))) \
  (_DATEBROWSER_GetClass__bn - 30))(_DATEBROWSER_GetClass__bn); \
})

#define JulianWeekDay(day, month, year) ({ \
  ULONG _JulianWeekDay_day = (day); \
  ULONG _JulianWeekDay_month = (month); \
  LONG _JulianWeekDay_year = (year); \
  ({ \
  register char * _JulianWeekDay__bn __asm("a6") = (char *) (DATEBROWSER_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"), LONG __asm("d2"))) \
  (_JulianWeekDay__bn - 36))(_JulianWeekDay__bn, _JulianWeekDay_day, _JulianWeekDay_month, _JulianWeekDay_year); \
});})

#define JulianMonthDays(month, year) ({ \
  ULONG _JulianMonthDays_month = (month); \
  LONG _JulianMonthDays_year = (year); \
  ({ \
  register char * _JulianMonthDays__bn __asm("a6") = (char *) (DATEBROWSER_BASE_NAME);\
  ((UWORD (*)(char * __asm("a6"), ULONG __asm("d0"), LONG __asm("d1"))) \
  (_JulianMonthDays__bn - 42))(_JulianMonthDays__bn, _JulianMonthDays_month, _JulianMonthDays_year); \
});})

#define JulianLeapYear(year) ({ \
  LONG _JulianLeapYear_year = (year); \
  ({ \
  register char * _JulianLeapYear__bn __asm("a6") = (char *) (DATEBROWSER_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), LONG __asm("d0"))) \
  (_JulianLeapYear__bn - 48))(_JulianLeapYear__bn, _JulianLeapYear_year); \
});})

#endif /*  _INLINE_DATEBROWSER_H  */
