#ifndef _INLINE_CARDRES_H
#define _INLINE_CARDRES_H

#ifndef CLIB_CARDRES_PROTOS_H
#define CLIB_CARDRES_PROTOS_H
#endif

#ifndef  EXEC_LIBRARIES_H
#include <exec/libraries.h>
#endif
#ifndef  EXEC_RESIDENT_H
#include <exec/resident.h>
#endif
#ifndef  RESOURCES_CARD_H
#include <resources/card.h>
#endif

#ifndef CARDRES_BASE_NAME
#define CARDRES_BASE_NAME CardResource
#endif

#define OwnCard(handle) ({ \
  struct CardHandle * _OwnCard_handle = (handle); \
  ({ \
  register char * _OwnCard__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((struct CardHandle * (*)(char * __asm("a6"), struct CardHandle * __asm("a1"))) \
  (_OwnCard__bn - 6))(_OwnCard__bn, _OwnCard_handle); \
});})

#define ReleaseCard(handle, flags) ({ \
  struct CardHandle * _ReleaseCard_handle = (handle); \
  ULONG _ReleaseCard_flags = (flags); \
  ({ \
  register char * _ReleaseCard__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct CardHandle * __asm("a1"), ULONG __asm("d0"))) \
  (_ReleaseCard__bn - 12))(_ReleaseCard__bn, _ReleaseCard_handle, _ReleaseCard_flags); \
});})

#define GetCardMap() ({ \
  register char * _GetCardMap__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((struct CardMemoryMap * (*)(char * __asm("a6"))) \
  (_GetCardMap__bn - 18))(_GetCardMap__bn); \
})

#define BeginCardAccess(handle) ({ \
  struct CardHandle * _BeginCardAccess_handle = (handle); \
  ({ \
  register char * _BeginCardAccess__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct CardHandle * __asm("a1"))) \
  (_BeginCardAccess__bn - 24))(_BeginCardAccess__bn, _BeginCardAccess_handle); \
});})

#define EndCardAccess(handle) ({ \
  struct CardHandle * _EndCardAccess_handle = (handle); \
  ({ \
  register char * _EndCardAccess__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct CardHandle * __asm("a1"))) \
  (_EndCardAccess__bn - 30))(_EndCardAccess__bn, _EndCardAccess_handle); \
});})

#define ReadCardStatus() ({ \
  register char * _ReadCardStatus__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((UBYTE (*)(char * __asm("a6"))) \
  (_ReadCardStatus__bn - 36))(_ReadCardStatus__bn); \
})

#define CardResetRemove(handle, flag) ({ \
  struct CardHandle * _CardResetRemove_handle = (handle); \
  ULONG _CardResetRemove_flag = (flag); \
  ({ \
  register char * _CardResetRemove__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct CardHandle * __asm("a1"), ULONG __asm("d0"))) \
  (_CardResetRemove__bn - 42))(_CardResetRemove__bn, _CardResetRemove_handle, _CardResetRemove_flag); \
});})

#define CardMiscControl(handle, control_bits) ({ \
  struct CardHandle * _CardMiscControl_handle = (handle); \
  ULONG _CardMiscControl_control_bits = (control_bits); \
  ({ \
  register char * _CardMiscControl__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((UBYTE (*)(char * __asm("a6"), struct CardHandle * __asm("a1"), ULONG __asm("d1"))) \
  (_CardMiscControl__bn - 48))(_CardMiscControl__bn, _CardMiscControl_handle, _CardMiscControl_control_bits); \
});})

#define CardAccessSpeed(handle, nanoseconds) ({ \
  struct CardHandle * _CardAccessSpeed_handle = (handle); \
  ULONG _CardAccessSpeed_nanoseconds = (nanoseconds); \
  ({ \
  register char * _CardAccessSpeed__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), struct CardHandle * __asm("a1"), ULONG __asm("d0"))) \
  (_CardAccessSpeed__bn - 54))(_CardAccessSpeed__bn, _CardAccessSpeed_handle, _CardAccessSpeed_nanoseconds); \
});})

#define CardProgramVoltage(handle, voltage) ({ \
  struct CardHandle * _CardProgramVoltage_handle = (handle); \
  ULONG _CardProgramVoltage_voltage = (voltage); \
  ({ \
  register char * _CardProgramVoltage__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((LONG (*)(char * __asm("a6"), struct CardHandle * __asm("a1"), ULONG __asm("d0"))) \
  (_CardProgramVoltage__bn - 60))(_CardProgramVoltage__bn, _CardProgramVoltage_handle, _CardProgramVoltage_voltage); \
});})

#define CardResetCard(handle) ({ \
  struct CardHandle * _CardResetCard_handle = (handle); \
  ({ \
  register char * _CardResetCard__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct CardHandle * __asm("a1"))) \
  (_CardResetCard__bn - 66))(_CardResetCard__bn, _CardResetCard_handle); \
});})

#define CopyTuple(handle, buffer, tuplecode, size) ({ \
  struct CardHandle * _CopyTuple_handle = (handle); \
  UBYTE * _CopyTuple_buffer = (buffer); \
  ULONG _CopyTuple_tuplecode = (tuplecode); \
  ULONG _CopyTuple_size = (size); \
  ({ \
  register char * _CopyTuple__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), struct CardHandle * __asm("a1"), UBYTE * __asm("a0"), ULONG __asm("d1"), ULONG __asm("d0"))) \
  (_CopyTuple__bn - 72))(_CopyTuple__bn, _CopyTuple_handle, _CopyTuple_buffer, _CopyTuple_tuplecode, _CopyTuple_size); \
});})

#define DeviceTuple(tuple_data, storage) ({ \
  CONST UBYTE * _DeviceTuple_tuple_data = (tuple_data); \
  struct DeviceTData * _DeviceTuple_storage = (storage); \
  ({ \
  register char * _DeviceTuple__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST UBYTE * __asm("a0"), struct DeviceTData * __asm("a1"))) \
  (_DeviceTuple__bn - 78))(_DeviceTuple__bn, _DeviceTuple_tuple_data, _DeviceTuple_storage); \
});})

#define IfAmigaXIP(handle) ({ \
  struct CardHandle * _IfAmigaXIP_handle = (handle); \
  ({ \
  register char * _IfAmigaXIP__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((struct Resident * (*)(char * __asm("a6"), struct CardHandle * __asm("a2"))) \
  (_IfAmigaXIP__bn - 84))(_IfAmigaXIP__bn, _IfAmigaXIP_handle); \
});})

#define CardForceChange() ({ \
  register char * _CardForceChange__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"))) \
  (_CardForceChange__bn - 90))(_CardForceChange__bn); \
})

#define CardChangeCount() ({ \
  register char * _CardChangeCount__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_CardChangeCount__bn - 96))(_CardChangeCount__bn); \
})

#define CardInterface() ({ \
  register char * _CardInterface__bn __asm("a6") = (char *) (CARDRES_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"))) \
  (_CardInterface__bn - 102))(_CardInterface__bn); \
})

#endif /*  _INLINE_CARDRES_H  */
