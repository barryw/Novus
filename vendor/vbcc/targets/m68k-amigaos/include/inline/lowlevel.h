#ifndef _INLINE_LOWLEVEL_H
#define _INLINE_LOWLEVEL_H

#ifndef CLIB_LOWLEVEL_PROTOS_H
#define CLIB_LOWLEVEL_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  EXEC_INTERRUPTS_H
#include <exec/interrupts.h>
#endif
#ifndef  UTILITY_TAGITEM_H
#include <utility/tagitem.h>
#endif
#ifndef  DEVICES_TIMER_H
#include <devices/timer.h>
#endif
#ifndef  LIBRARIES_LOWLEVEL_H
#include <libraries/lowlevel.h>
#endif

#ifndef LOWLEVEL_BASE_NAME
#define LOWLEVEL_BASE_NAME LowLevelBase
#endif

#define ReadJoyPort(port) ({ \
  ULONG _ReadJoyPort_port = (port); \
  ({ \
  register char * _ReadJoyPort__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_ReadJoyPort__bn - 30))(_ReadJoyPort__bn, _ReadJoyPort_port); \
});})

#define GetLanguageSelection() ({ \
  register char * _GetLanguageSelection__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((UBYTE (*)(char * __asm("a6"))) \
  (_GetLanguageSelection__bn - 36))(_GetLanguageSelection__bn); \
})

#define GetKey() ({ \
  register char * _GetKey__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_GetKey__bn - 48))(_GetKey__bn); \
})

#define QueryKeys(queryArray, arraySize) ({ \
  struct KeyQuery * _QueryKeys_queryArray = (queryArray); \
  LONG _QueryKeys_arraySize = (arraySize); \
  ({ \
  register char * _QueryKeys__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct KeyQuery * __asm("a0"), LONG __asm("d1"))) \
  (_QueryKeys__bn - 54))(_QueryKeys__bn, _QueryKeys_queryArray, _QueryKeys_arraySize); \
});})

#define AddKBInt(intRoutine, intData) ({ \
  APTR _AddKBInt_intRoutine = (intRoutine); \
  APTR _AddKBInt_intData = (intData); \
  ({ \
  register char * _AddKBInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), APTR __asm("a0"), APTR __asm("a1"))) \
  (_AddKBInt__bn - 60))(_AddKBInt__bn, _AddKBInt_intRoutine, _AddKBInt_intData); \
});})

#define RemKBInt(intHandle) ({ \
  APTR _RemKBInt_intHandle = (intHandle); \
  ({ \
  register char * _RemKBInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"))) \
  (_RemKBInt__bn - 66))(_RemKBInt__bn, _RemKBInt_intHandle); \
});})

#define SystemControlA(tagList) ({ \
  CONST struct TagItem * _SystemControlA_tagList = (tagList); \
  ({ \
  register char * _SystemControlA__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST struct TagItem * __asm("a1"))) \
  (_SystemControlA__bn - 72))(_SystemControlA__bn, _SystemControlA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ ULONG ___SystemControl(struct Library * LowLevelBase, Tag tagList, ...)
{
  return SystemControlA((CONST struct TagItem *) &tagList);
}

#define SystemControl(tags...) ___SystemControl(LOWLEVEL_BASE_NAME, tags)
#endif

#define AddTimerInt(intRoutine, intData) ({ \
  APTR _AddTimerInt_intRoutine = (intRoutine); \
  APTR _AddTimerInt_intData = (intData); \
  ({ \
  register char * _AddTimerInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), APTR __asm("a0"), APTR __asm("a1"))) \
  (_AddTimerInt__bn - 78))(_AddTimerInt__bn, _AddTimerInt_intRoutine, _AddTimerInt_intData); \
});})

#define RemTimerInt(intHandle) ({ \
  APTR _RemTimerInt_intHandle = (intHandle); \
  ({ \
  register char * _RemTimerInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"))) \
  (_RemTimerInt__bn - 84))(_RemTimerInt__bn, _RemTimerInt_intHandle); \
});})

#define StopTimerInt(intHandle) ({ \
  APTR _StopTimerInt_intHandle = (intHandle); \
  ({ \
  register char * _StopTimerInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"))) \
  (_StopTimerInt__bn - 90))(_StopTimerInt__bn, _StopTimerInt_intHandle); \
});})

#define StartTimerInt(intHandle, timeInterval, continuous) ({ \
  APTR _StartTimerInt_intHandle = (intHandle); \
  ULONG _StartTimerInt_timeInterval = (timeInterval); \
  LONG _StartTimerInt_continuous = (continuous); \
  ({ \
  register char * _StartTimerInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"), ULONG __asm("d0"), LONG __asm("d1"))) \
  (_StartTimerInt__bn - 96))(_StartTimerInt__bn, _StartTimerInt_intHandle, _StartTimerInt_timeInterval, _StartTimerInt_continuous); \
});})

#define ElapsedTime(context) ({ \
  struct EClockVal * _ElapsedTime_context = (context); \
  ({ \
  register char * _ElapsedTime__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct EClockVal * __asm("a0"))) \
  (_ElapsedTime__bn - 102))(_ElapsedTime__bn, _ElapsedTime_context); \
});})

#define AddVBlankInt(intRoutine, intData) ({ \
  APTR _AddVBlankInt_intRoutine = (intRoutine); \
  APTR _AddVBlankInt_intData = (intData); \
  ({ \
  register char * _AddVBlankInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), APTR __asm("a0"), APTR __asm("a1"))) \
  (_AddVBlankInt__bn - 108))(_AddVBlankInt__bn, _AddVBlankInt_intRoutine, _AddVBlankInt_intData); \
});})

#define RemVBlankInt(intHandle) ({ \
  APTR _RemVBlankInt_intHandle = (intHandle); \
  ({ \
  register char * _RemVBlankInt__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a1"))) \
  (_RemVBlankInt__bn - 114))(_RemVBlankInt__bn, _RemVBlankInt_intHandle); \
});})

#define SetJoyPortAttrsA(portNumber, tagList) ({ \
  ULONG _SetJoyPortAttrsA_portNumber = (portNumber); \
  CONST struct TagItem * _SetJoyPortAttrsA_tagList = (tagList); \
  ({ \
  register char * _SetJoyPortAttrsA__bn __asm("a6") = (char *) (LOWLEVEL_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), ULONG __asm("d0"), CONST struct TagItem * __asm("a1"))) \
  (_SetJoyPortAttrsA__bn - 132))(_SetJoyPortAttrsA__bn, _SetJoyPortAttrsA_portNumber, _SetJoyPortAttrsA_tagList); \
});})

#ifndef NO_INLINE_STDARG
static __inline__ BOOL ___SetJoyPortAttrs(struct Library * LowLevelBase, ULONG portNumber, ...)
{
  return SetJoyPortAttrsA(portNumber, (CONST struct TagItem *) ((ULONG) &portNumber + sizeof(ULONG)));
}

#define SetJoyPortAttrs(portNumber...) ___SetJoyPortAttrs(LOWLEVEL_BASE_NAME, portNumber)
#endif

#endif /*  _INLINE_LOWLEVEL_H  */
