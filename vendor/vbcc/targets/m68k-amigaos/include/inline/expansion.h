#ifndef _INLINE_EXPANSION_H
#define _INLINE_EXPANSION_H

#ifndef CLIB_EXPANSION_PROTOS_H
#define CLIB_EXPANSION_PROTOS_H
#endif

#ifndef  LIBRARIES_EXPANSIONBASE_H
#include <libraries/expansionbase.h>
#endif
#ifndef  DOS_FILEHANDLER_H
#include <dos/filehandler.h>
#endif

#ifndef EXPANSION_BASE_NAME
#define EXPANSION_BASE_NAME ExpansionBase
#endif

#define AddConfigDev(configDev) ({ \
  struct ConfigDev * _AddConfigDev_configDev = (configDev); \
  ({ \
  register char * _AddConfigDev__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ConfigDev * __asm("a0"))) \
  (_AddConfigDev__bn - 30))(_AddConfigDev__bn, _AddConfigDev_configDev); \
});})

#define AddBootNode(bootPri, flags, deviceNode, configDev) ({ \
  LONG _AddBootNode_bootPri = (bootPri); \
  ULONG _AddBootNode_flags = (flags); \
  struct DeviceNode * _AddBootNode_deviceNode = (deviceNode); \
  struct ConfigDev * _AddBootNode_configDev = (configDev); \
  ({ \
  register char * _AddBootNode__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), LONG __asm("d0"), ULONG __asm("d1"), struct DeviceNode * __asm("a0"), struct ConfigDev * __asm("a1"))) \
  (_AddBootNode__bn - 36))(_AddBootNode__bn, _AddBootNode_bootPri, _AddBootNode_flags, _AddBootNode_deviceNode, _AddBootNode_configDev); \
});})

#define AllocBoardMem(slotSpec) ({ \
  ULONG _AllocBoardMem_slotSpec = (slotSpec); \
  ({ \
  register char * _AllocBoardMem__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"))) \
  (_AllocBoardMem__bn - 42))(_AllocBoardMem__bn, _AllocBoardMem_slotSpec); \
});})

#define AllocConfigDev() ({ \
  register char * _AllocConfigDev__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((struct ConfigDev * (*)(char * __asm("a6"))) \
  (_AllocConfigDev__bn - 48))(_AllocConfigDev__bn); \
})

#define AllocExpansionMem(numSlots, slotAlign) ({ \
  ULONG _AllocExpansionMem_numSlots = (numSlots); \
  ULONG _AllocExpansionMem_slotAlign = (slotAlign); \
  ({ \
  register char * _AllocExpansionMem__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((APTR (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_AllocExpansionMem__bn - 54))(_AllocExpansionMem__bn, _AllocExpansionMem_numSlots, _AllocExpansionMem_slotAlign); \
});})

#define ConfigBoard(board, configDev) ({ \
  APTR _ConfigBoard_board = (board); \
  struct ConfigDev * _ConfigBoard_configDev = (configDev); \
  ({ \
  register char * _ConfigBoard__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"), struct ConfigDev * __asm("a1"))) \
  (_ConfigBoard__bn - 60))(_ConfigBoard__bn, _ConfigBoard_board, _ConfigBoard_configDev); \
});})

#define ConfigChain(baseAddr) ({ \
  APTR _ConfigChain_baseAddr = (baseAddr); \
  ({ \
  register char * _ConfigChain__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"))) \
  (_ConfigChain__bn - 66))(_ConfigChain__bn, _ConfigChain_baseAddr); \
});})

#define FindConfigDev(oldConfigDev, manufacturer, product) ({ \
  CONST struct ConfigDev * _FindConfigDev_oldConfigDev = (oldConfigDev); \
  LONG _FindConfigDev_manufacturer = (manufacturer); \
  LONG _FindConfigDev_product = (product); \
  ({ \
  register char * _FindConfigDev__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((struct ConfigDev * (*)(char * __asm("a6"), CONST struct ConfigDev * __asm("a0"), LONG __asm("d0"), LONG __asm("d1"))) \
  (_FindConfigDev__bn - 72))(_FindConfigDev__bn, _FindConfigDev_oldConfigDev, _FindConfigDev_manufacturer, _FindConfigDev_product); \
});})

#define FreeBoardMem(startSlot, slotSpec) ({ \
  ULONG _FreeBoardMem_startSlot = (startSlot); \
  ULONG _FreeBoardMem_slotSpec = (slotSpec); \
  ({ \
  register char * _FreeBoardMem__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_FreeBoardMem__bn - 78))(_FreeBoardMem__bn, _FreeBoardMem_startSlot, _FreeBoardMem_slotSpec); \
});})

#define FreeConfigDev(configDev) ({ \
  struct ConfigDev * _FreeConfigDev_configDev = (configDev); \
  ({ \
  register char * _FreeConfigDev__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ConfigDev * __asm("a0"))) \
  (_FreeConfigDev__bn - 84))(_FreeConfigDev__bn, _FreeConfigDev_configDev); \
});})

#define FreeExpansionMem(startSlot, numSlots) ({ \
  ULONG _FreeExpansionMem_startSlot = (startSlot); \
  ULONG _FreeExpansionMem_numSlots = (numSlots); \
  ({ \
  register char * _FreeExpansionMem__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_FreeExpansionMem__bn - 90))(_FreeExpansionMem__bn, _FreeExpansionMem_startSlot, _FreeExpansionMem_numSlots); \
});})

#define ReadExpansionByte(board, offset) ({ \
  CONST_APTR _ReadExpansionByte_board = (board); \
  ULONG _ReadExpansionByte_offset = (offset); \
  ({ \
  register char * _ReadExpansionByte__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((UBYTE (*)(char * __asm("a6"), CONST_APTR __asm("a0"), ULONG __asm("d0"))) \
  (_ReadExpansionByte__bn - 96))(_ReadExpansionByte__bn, _ReadExpansionByte_board, _ReadExpansionByte_offset); \
});})

#define ReadExpansionRom(board, configDev) ({ \
  CONST_APTR _ReadExpansionRom_board = (board); \
  struct ConfigDev * _ReadExpansionRom_configDev = (configDev); \
  ({ \
  register char * _ReadExpansionRom__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), CONST_APTR __asm("a0"), struct ConfigDev * __asm("a1"))) \
  (_ReadExpansionRom__bn - 102))(_ReadExpansionRom__bn, _ReadExpansionRom_board, _ReadExpansionRom_configDev); \
});})

#define RemConfigDev(configDev) ({ \
  struct ConfigDev * _RemConfigDev_configDev = (configDev); \
  ({ \
  register char * _RemConfigDev__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct ConfigDev * __asm("a0"))) \
  (_RemConfigDev__bn - 108))(_RemConfigDev__bn, _RemConfigDev_configDev); \
});})

#define WriteExpansionByte(board, offset, byte) ({ \
  APTR _WriteExpansionByte_board = (board); \
  ULONG _WriteExpansionByte_offset = (offset); \
  ULONG _WriteExpansionByte_byte = (byte); \
  ({ \
  register char * _WriteExpansionByte__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), APTR __asm("a0"), ULONG __asm("d0"), ULONG __asm("d1"))) \
  (_WriteExpansionByte__bn - 114))(_WriteExpansionByte__bn, _WriteExpansionByte_board, _WriteExpansionByte_offset, _WriteExpansionByte_byte); \
});})

#define ObtainConfigBinding() ({ \
  register char * _ObtainConfigBinding__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_ObtainConfigBinding__bn - 120))(_ObtainConfigBinding__bn); \
})

#define ReleaseConfigBinding() ({ \
  register char * _ReleaseConfigBinding__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"))) \
  (_ReleaseConfigBinding__bn - 126))(_ReleaseConfigBinding__bn); \
})

#define SetCurrentBinding(currentBinding, bindingSize) ({ \
  struct CurrentBinding * _SetCurrentBinding_currentBinding = (currentBinding); \
  ULONG _SetCurrentBinding_bindingSize = (bindingSize); \
  ({ \
  register char * _SetCurrentBinding__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((VOID (*)(char * __asm("a6"), struct CurrentBinding * __asm("a0"), ULONG __asm("d0"))) \
  (_SetCurrentBinding__bn - 132))(_SetCurrentBinding__bn, _SetCurrentBinding_currentBinding, _SetCurrentBinding_bindingSize); \
});})

#define GetCurrentBinding(currentBinding, bindingSize) ({ \
  CONST struct CurrentBinding * _GetCurrentBinding_currentBinding = (currentBinding); \
  ULONG _GetCurrentBinding_bindingSize = (bindingSize); \
  ({ \
  register char * _GetCurrentBinding__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((ULONG (*)(char * __asm("a6"), CONST struct CurrentBinding * __asm("a0"), ULONG __asm("d0"))) \
  (_GetCurrentBinding__bn - 138))(_GetCurrentBinding__bn, _GetCurrentBinding_currentBinding, _GetCurrentBinding_bindingSize); \
});})

#define MakeDosNode(parmPacket) ({ \
  CONST_APTR _MakeDosNode_parmPacket = (parmPacket); \
  ({ \
  register char * _MakeDosNode__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((struct DeviceNode * (*)(char * __asm("a6"), CONST_APTR __asm("a0"))) \
  (_MakeDosNode__bn - 144))(_MakeDosNode__bn, _MakeDosNode_parmPacket); \
});})

#define AddDosNode(bootPri, flags, deviceNode) ({ \
  LONG _AddDosNode_bootPri = (bootPri); \
  ULONG _AddDosNode_flags = (flags); \
  struct DeviceNode * _AddDosNode_deviceNode = (deviceNode); \
  ({ \
  register char * _AddDosNode__bn __asm("a6") = (char *) (EXPANSION_BASE_NAME);\
  ((BOOL (*)(char * __asm("a6"), LONG __asm("d0"), ULONG __asm("d1"), struct DeviceNode * __asm("a0"))) \
  (_AddDosNode__bn - 150))(_AddDosNode__bn, _AddDosNode_bootPri, _AddDosNode_flags, _AddDosNode_deviceNode); \
});})

#endif /*  _INLINE_EXPANSION_H  */
